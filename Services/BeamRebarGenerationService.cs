using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using AutoPileCap.UI;

namespace AutoPileCap.Services
{
    public static class BeamRebarGenerationService
    {
        public static void Generate(Document doc, FamilyInstance beam, BeamRebarConfig config)
        {
            // 1. Get Beam Geometry
            BoundingBoxXYZ bbox = beam.get_BoundingBox(null);
            if (bbox == null) return;

            double widthX = bbox.Max.X - bbox.Min.X;
            double depthY = bbox.Max.Y - bbox.Min.Y;
            double heightZ = bbox.Max.Z - bbox.Min.Z;
            
            Transform t = beam.GetTransform();
            
            XYZ center = t.OfPoint(new XYZ((bbox.Max.X + bbox.Min.X) / 2, (bbox.Max.Y + bbox.Min.Y) / 2, bbox.Min.Z));

            double cover = 40.0 / 304.8;
            Parameter coverParam = beam.get_Parameter(BuiltInParameter.CLEAR_COVER_OTHER);
            if (coverParam != null && coverParam.HasValue)
            {
                ElementId coverTypeId = coverParam.AsElementId();
                if (coverTypeId != ElementId.InvalidElementId)
                {
                    RebarCoverType coverType = doc.GetElement(coverTypeId) as RebarCoverType;
                    if (coverType != null)
                    {
                        cover = coverType.CoverDistance;
                    }
                }
            }
            
            RebarBarType vertBarType = doc.GetElement(config.VerticalBarTypeId) as RebarBarType;
            RebarBarType tieBarType = doc.GetElement(config.TieBarTypeId) as RebarBarType;
            
            double vertDiameter = vertBarType.BarModelDiameter;
            double tieDiameter = tieBarType.BarModelDiameter;
            
            // Rebar Cage Dimensions
            double cageMinX = bbox.Min.X + cover + tieDiameter + vertDiameter / 2.0;
            double cageMaxX = bbox.Max.X - cover - tieDiameter - vertDiameter / 2.0;
            
            double cageMinY = bbox.Min.Y + cover + tieDiameter + vertDiameter / 2.0;
            double cageMaxY = bbox.Max.Y - cover - tieDiameter - vertDiameter / 2.0;

            double botZ = bbox.Min.Z;
            double topZ = bbox.Max.Z;
            
            if (config.ExtendDowels)
            {
                topZ += (vertDiameter * config.LapFactor);
            }

            // 2. Generate Vertical Bars
            RebarHookType topHook = config.TopHookTypeId != null && config.TopHookTypeId != ElementId.InvalidElementId ? doc.GetElement(config.TopHookTypeId) as RebarHookType : null;
            RebarHookType botHook = config.BottomHookTypeId != null && config.BottomHookTypeId != ElementId.InvalidElementId ? doc.GetElement(config.BottomHookTypeId) as RebarHookType : null;
            
            List<Curve> vertCurves = new List<Curve>();
            
            // X-Edges
            double spacingX = (cageMaxX - cageMinX) / (config.Nx - 1);
            for (int i = 0; i < config.Nx; i++)
            {
                double x = cageMinX + i * spacingX;
                // Bottom edge (Min Y)
                vertCurves.Add(Line.CreateBound(new XYZ(x, cageMinY, botZ), new XYZ(x, cageMinY, topZ)));
                // Top edge (Max Y)
                vertCurves.Add(Line.CreateBound(new XYZ(x, cageMaxY, botZ), new XYZ(x, cageMaxY, topZ)));
            }

            // Y-Edges (exclude corners already added)
            double spacingY = (cageMaxY - cageMinY) / (config.Ny - 1);
            for (int i = 1; i < config.Ny - 1; i++)
            {
                double y = cageMinY + i * spacingY;
                // Left edge (Min X)
                vertCurves.Add(Line.CreateBound(new XYZ(cageMinX, y, botZ), new XYZ(cageMinX, y, topZ)));
                // Right edge (Max X)
                vertCurves.Add(Line.CreateBound(new XYZ(cageMaxX, y, botZ), new XYZ(cageMaxX, y, topZ)));
            }
            
            foreach (var curve in vertCurves)
            {
                try
                {
                    // Create vertical lines directly in global coordinate system if they are vertical
                    Rebar rebar = Rebar.CreateFromCurves(doc, RebarStyle.Standard, vertBarType, botHook, topHook, beam, XYZ.BasisX, new List<Curve> { curve }, RebarHookOrientation.Left, RebarHookOrientation.Left, true, true);
                    SetRebarVisibility(doc, rebar);
                    
                    if (config.BottomIsCrank)
                    {
                        try 
                        { 
                            if (config.BottomCrankId != null && config.BottomCrankId != ElementId.InvalidElementId)
                            {
                                rebar.SetEndTreatmentTypeId(0, config.BottomCrankId); 
                            }
                            
                            Parameter pStart = rebar.LookupParameter("Start of Bar");
                            if (pStart != null && !pStart.IsReadOnly)
                            {
                                if (!pStart.SetValueString("Crank"))
                                {
                                    for (int idx = 0; idx < 10; idx++)
                                    {
                                        pStart.Set(idx);
                                        if (pStart.AsValueString() == "Crank") break;
                                    }
                                }
                            }
                        } 
                        catch { }
                    }
                    if (config.TopIsCrank)
                    {
                        try 
                        { 
                            if (config.TopCrankId != null && config.TopCrankId != ElementId.InvalidElementId)
                            {
                                rebar.SetEndTreatmentTypeId(1, config.TopCrankId); 
                            }
                            
                            Parameter pEnd = rebar.LookupParameter("End of Bar");
                            if (pEnd != null && !pEnd.IsReadOnly)
                            {
                                if (!pEnd.SetValueString("Crank"))
                                {
                                    for (int idx = 0; idx < 10; idx++)
                                    {
                                        pEnd.Set(idx);
                                        if (pEnd.AsValueString() == "Crank") break;
                                    }
                                }
                            }
                        } 
                        catch { }
                    }
                }
                catch { /* Ignore invalid vertical bars */ }
            }

            // 3. Generate Ties (Stirrups)
            // Determine Z elevations based on zones
            double tieBotZ = bbox.Min.Z + cover;
            double tieTopZ = bbox.Max.Z - cover;
            double sp = config.TieSpacingMm / 304.8;

            double tieMinX = bbox.Min.X + cover + tieDiameter / 2.0;
            double tieMaxX = bbox.Max.X - cover - tieDiameter / 2.0;
            double tieMinY = bbox.Min.Y + cover + tieDiameter / 2.0;
            double tieMaxY = bbox.Max.Y - cover - tieDiameter / 2.0;

            RebarHookType hookType = doc.GetElement(config.HookTypeId) as RebarHookType;

            Action<double, double, double, double, double, ElementId> createTie = (minX, maxX, minY, maxY, spacing, shapeId) =>
            {
                List<Curve> curves = new List<Curve>();
                curves.Add(Line.CreateBound(new XYZ(minX, minY, tieBotZ), new XYZ(maxX, minY, tieBotZ)));
                curves.Add(Line.CreateBound(new XYZ(maxX, minY, tieBotZ), new XYZ(maxX, maxY, tieBotZ)));
                curves.Add(Line.CreateBound(new XYZ(maxX, maxY, tieBotZ), new XYZ(minX, maxY, tieBotZ)));
                curves.Add(Line.CreateBound(new XYZ(minX, maxY, tieBotZ), new XYZ(minX, minY, tieBotZ)));

                try
                {
                    Rebar tie = Rebar.CreateFromCurves(doc, RebarStyle.StirrupTie, tieBarType, hookType, hookType, beam, XYZ.BasisZ, curves, RebarHookOrientation.Left, RebarHookOrientation.Left, true, true);
                    
                    double arrayLength = tieTopZ - tieBotZ;
                    if (arrayLength > 0 && tie.IsRebarShapeDriven())
                    {
                        double spFeet = spacing / 304.8;
                        tie.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(spFeet, arrayLength, true, true, true);
                    }
                    
                    SetRebarVisibility(doc, tie);
                    
                    if (shapeId != null && shapeId != ElementId.InvalidElementId)
                    {
                        try
                        {
                            var shapeParam = tie.get_Parameter(BuiltInParameter.REBAR_SHAPE);
                            if (shapeParam != null && !shapeParam.IsReadOnly)
                            {
                                shapeParam.Set(shapeId);
                            }
                        }
                        catch { /* Ignore if shape is incompatible */ }
                    }
                }
                catch (Autodesk.Revit.Exceptions.InternalException)
                {
                    // Ignore if bend radius is too large
                }
                catch { }
            };

            // Main outer tie
            createTie(tieMinX, tieMaxX, tieMinY, tieMaxY, config.TieSpacingMm, config.TieShapeId);

            // Inner ties
            if (config.GenerateInnerTies)
            {
                if (config.CustomInnerTies != null && config.CustomInnerTies.Count > 0)
                {
                    foreach (var cTie in config.CustomInnerTies)
                    {
                        double innerMinX = cageMinX + cTie.MinX * spacingX - vertDiameter / 2.0 - tieDiameter / 2.0;
                        double innerMaxX = cageMinX + cTie.MaxX * spacingX + vertDiameter / 2.0 + tieDiameter / 2.0;
                        double innerMinY = cageMinY + cTie.MinY * spacingY - vertDiameter / 2.0 - tieDiameter / 2.0;
                        double innerMaxY = cageMinY + cTie.MaxY * spacingY + vertDiameter / 2.0 + tieDiameter / 2.0;

                        if (innerMaxX - innerMinX < 1.0 / 12.0)
                        {
                            innerMinX -= 0.5 / 12.0;
                            innerMaxX += 0.5 / 12.0;
                        }
                        if (innerMaxY - innerMinY < 1.0 / 12.0)
                        {
                            innerMinY -= 0.5 / 12.0;
                            innerMaxY += 0.5 / 12.0;
                        }

                        createTie(innerMinX, innerMaxX, innerMinY, innerMaxY, config.InnerTieSpacingMm, config.InnerTieShapeId);
                    }
                }
                else
                {
                    if (config.Nx > 2)
                    {
                        double innerMinX = cageMinX + spacingX - vertDiameter / 2.0 - tieDiameter / 2.0;
                        double innerMaxX = cageMinX + (config.Nx - 2) * spacingX + vertDiameter / 2.0 + tieDiameter / 2.0;
                        if (innerMaxX > innerMinX + 1.0 / 12.0) // Minimum width sanity check
                        {
                            createTie(innerMinX, innerMaxX, tieMinY, tieMaxY, config.InnerTieSpacingMm, config.InnerTieShapeId);
                        }
                    }
                    
                    if (config.Ny > 2)
                    {
                        double innerMinY = cageMinY + spacingY - vertDiameter / 2.0 - tieDiameter / 2.0;
                        double innerMaxY = cageMinY + (config.Ny - 2) * spacingY + vertDiameter / 2.0 + tieDiameter / 2.0;
                        if (innerMaxY > innerMinY + 1.0 / 12.0)
                        {
                            createTie(tieMinX, tieMaxX, innerMinY, innerMaxY, config.InnerTieSpacingMm, config.InnerTieShapeId);
                        }
                    }
                }
            }
        }

        private static void SetRebarVisibility(Document doc, Rebar rebar)
        {
            if (rebar == null || doc.ActiveView == null) return;
            try
            {
                if (doc.ActiveView is View3D view3d)
                {
                    rebar.SetUnobscuredInView(view3d, true);
                    // In Revit 2026 solid in view is controlled by detail level.
                }
                else
                {
                    rebar.SetUnobscuredInView(doc.ActiveView, true);
                }
            }
            catch { }
        }
    }
}



