using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using AutoPileCap.Models;

namespace AutoPileCap.Services
{
    public static class StairRebarGenerationService
    {
        public static int Generate(Document doc, Element stairOrRun, StairRebarConfig config)
        {
            if (stairOrRun == null) return 0;

            int totalCreated = 0;

            // 1. If element is a Stairs assembly, iterate over its runs
            if (stairOrRun is Autodesk.Revit.DB.Architecture.Stairs stairs)
            {
                ICollection<ElementId> runIds = stairs.GetStairsRuns();
                if (runIds != null && runIds.Count > 0)
                {
                    foreach (var runId in runIds)
                    {
                        var runElem = doc.GetElement(runId);
                        if (runElem != null)
                        {
                            totalCreated += GenerateForRun(doc, runElem, stairs, config);
                        }
                    }
                    return totalCreated;
                }
            }

            // 2. If element is an individual StairsRun
            if (stairOrRun is Autodesk.Revit.DB.Architecture.StairsRun run)
            {
                Element hostStairs = run.GetStairs() ?? (Element)run;
                return GenerateForRun(doc, run, hostStairs, config);
            }

            // 3. Fallback for generic stairs or solid flight
            return GenerateForGenericFlight(doc, stairOrRun, config);
        }

        private static int GenerateForRun(Document doc, Element runElem, Element parentStair, StairRebarConfig config)
        {
            int createdCount = 0;

            // Determine host element
            Element host = runElem;
            if (!RebarHostData.IsValidHost(host) && parentStair != null && RebarHostData.IsValidHost(parentStair))
            {
                host = parentStair;
            }

            // Extract bar types
            RebarBarType? botBarType = doc.GetElement(new ElementId(config.BottomBarTypeId)) as RebarBarType;
            RebarBarType? topBarType = doc.GetElement(new ElementId(config.TopBarTypeId)) as RebarBarType ?? botBarType;
            RebarBarType? distBarType = doc.GetElement(new ElementId(config.DistBarTypeId)) as RebarBarType ?? botBarType;

            if (botBarType == null && topBarType == null && distBarType == null)
            {
                // Fallback to first available bar type
                var allBarTypes = new FilteredElementCollector(doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>().ToList();
                if (allBarTypes.Count == 0) return 0;
                botBarType = allBarTypes.FirstOrDefault();
                topBarType = botBarType;
                distBarType = botBarType;
            }

            double dBot = botBarType?.BarNominalDiameter ?? GeometryHelper.MmToFeet(12);
            double dTop = topBarType?.BarNominalDiameter ?? GeometryHelper.MmToFeet(12);
            double dDist = distBarType?.BarNominalDiameter ?? GeometryHelper.MmToFeet(10);

            // Extract flight geometry
            double flightWidth = GeometryHelper.MmToFeet(1000);
            double flightHeight = GeometryHelper.MmToFeet(1500);
            XYZ pStart = XYZ.Zero;
            XYZ pEnd = XYZ.Zero;
            bool foundCenterline = false;

            if (runElem is Autodesk.Revit.DB.Architecture.StairsRun sRun)
            {
                flightWidth = sRun.ActualRunWidth;
                flightHeight = sRun.Height;

                try
                {
                    CurveLoop pathLoop = sRun.GetStairsPath();
                    if (pathLoop != null && pathLoop.Any())
                    {
                        Curve centerline = pathLoop.First();
                        XYZ pt0 = centerline.GetEndPoint(0);
                        XYZ pt1 = centerline.GetEndPoint(1);
                        pStart = pt0.Z <= pt1.Z ? pt0 : pt1;
                        pEnd = pt0.Z <= pt1.Z ? pt1 : pt0;
                        foundCenterline = true;
                    }
                }
                catch { }
            }

            if (!foundCenterline)
            {
                BoundingBoxXYZ bbox = runElem.get_BoundingBox(null) ?? parentStair.get_BoundingBox(null);
                if (bbox == null) return 0;

                flightWidth = Math.Max(GeometryHelper.MmToFeet(800), bbox.Max.X - bbox.Min.X);
                flightHeight = Math.Max(GeometryHelper.MmToFeet(1000), bbox.Max.Z - bbox.Min.Z);
                pStart = new XYZ((bbox.Min.X + bbox.Max.X) / 2.0, bbox.Min.Y, bbox.Min.Z);
                pEnd = new XYZ((bbox.Min.X + bbox.Max.X) / 2.0, bbox.Max.Y, bbox.Max.Z);
            }

            // Direction vectors
            XYZ deltaXY = new XYZ(pEnd.X - pStart.X, pEnd.Y - pStart.Y, 0);
            double horizDist = deltaXY.GetLength();
            if (horizDist < 0.1) horizDist = GeometryHelper.MmToFeet(2000);

            XYZ vecU = deltaXY.GetLength() > 0.001 ? deltaXY.Normalize() : XYZ.BasisY;
            XYZ vecV = new XYZ(-vecU.Y, vecU.X, 0); // Transverse across stair width
            XYZ vecZ = XYZ.BasisZ;

            double riseH = pEnd.Z - pStart.Z;
            if (riseH < 0.1) riseH = flightHeight;
            double slopeAngle = Math.Atan2(riseH, horizDist);
            double cosA = Math.Cos(slopeAngle);
            double sinA = Math.Sin(slopeAngle);

            XYZ vecSlope = cosA * vecU + sinA * vecZ;
            XYZ vecNormWaist = -sinA * vecU + cosA * vecZ; // Perpendicular to waist soffit pointing upward

            double cWaist = config.CoverWaistFeet;
            double cTop = config.CoverTopFeet;
            double cSide = config.CoverSideFeet;
            double tWaist = config.WaistThicknessFeet;

            double arrayWidth = flightWidth - 2.0 * cSide;
            if (arrayWidth <= 0.1) arrayWidth = flightWidth * 0.85;

            double lSlope = Math.Sqrt(horizDist * horizDist + riseH * riseH);

            // Kink points
            XYZ pKinkBot = pStart - (tWaist / cosA) * vecZ;
            XYZ pKinkTop = pKinkBot + lSlope * vecSlope;

            // 1. Bottom Longitudinal Reinforcement (Waist tension bars)
            if (config.EnableBottomRebar && botBarType != null)
            {
                double dOffBot = cWaist + dBot * 0.5;
                double miterBot = dOffBot * Math.Tan(slopeAngle * 0.5);
                double miterTop = dOffBot * Math.Tan(slopeAngle * 0.5);

                XYZ pIntBot = pKinkBot + miterBot * vecU + dOffBot * vecZ;
                XYZ pIntTop = pKinkTop - miterTop * vecU + dOffBot * vecZ;

                XYZ pBotAnc = pIntBot - config.BottomAnchorBotFeet * vecU;
                XYZ pTopAnc = pIntTop + config.BottomAnchorTopFeet * vecU;

                var botCurves = new List<Curve>();

                if (config.BottomHookBot)
                {
                    XYZ pHook = pBotAnc + GeometryHelper.MmToFeet(150) * vecZ;
                    botCurves.Add(Line.CreateBound(pHook, pBotAnc));
                }

                botCurves.Add(Line.CreateBound(pBotAnc, pIntBot));
                botCurves.Add(Line.CreateBound(pIntBot, pIntTop));
                botCurves.Add(Line.CreateBound(pIntTop, pTopAnc));

                if (config.BottomHookTop)
                {
                    XYZ pHook = pTopAnc - GeometryHelper.MmToFeet(150) * vecZ;
                    botCurves.Add(Line.CreateBound(pTopAnc, pHook));
                }

                int created = CreateRebarGroup(doc, host, botBarType, botCurves, vecV, config.BottomSpacingFeet, arrayWidth, config);
                createdCount += created;
            }

            // 2. Top Longitudinal Reinforcement (Scissor Bar Detail)
            if (config.EnableTopRebar && topBarType != null)
            {
                double dOffTop = tWaist - cTop - dTop * 0.5;
                double miterTop = (cTop + dTop * 0.5) * Math.Tan(slopeAngle * 0.5);

                XYZ qIntTop = pKinkTop + miterTop * vecU + dOffTop * vecZ;
                XYZ qTopAnc = qIntTop + config.TopAnchorTopFeet * vecU;

                // Lower kink scissor crank detail: extends diagonally through re-entrant angle into bottom landing
                XYZ qScissorKink = pKinkBot + (cWaist + dTop * 0.5) * vecZ;
                XYZ qScissorEnd = qScissorKink - config.TopScissorLapFeet * vecU;

                var topCurves = new List<Curve>
                {
                    Line.CreateBound(qScissorEnd, qScissorKink),
                    Line.CreateBound(qScissorKink, qIntTop),
                    Line.CreateBound(qIntTop, qTopAnc)
                };

                if (config.TopHookTop)
                {
                    XYZ qHook = qTopAnc - GeometryHelper.MmToFeet(150) * vecZ;
                    topCurves.Add(Line.CreateBound(qTopAnc, qHook));
                }

                int created = CreateRebarGroup(doc, host, topBarType, topCurves, vecV, config.TopSpacingFeet, arrayWidth, config);
                createdCount += created;
            }

            // 3. Transverse / Distribution Reinforcement
            if (config.EnableDistRebar && distBarType != null)
            {
                double distSpacing = config.DistSpacingFeet > 0.05 ? config.DistSpacingFeet : GeometryHelper.MmToFeet(200);
                int numSteps = (int)Math.Max(2, Math.Floor(lSlope / distSpacing));

                double halfW = arrayWidth * 0.5;
                double zOffDistBot = cWaist + dBot + dDist * 0.5;

                // Distribution bars along inclined waist (resting on bottom bars)
                for (int i = 0; i <= numSteps; i++)
                {
                    double dist = i * distSpacing;
                    if (dist > lSlope) break;

                    XYZ centerPt = pKinkBot + dist * vecSlope + zOffDistBot * vecNormWaist;
                    XYZ ptLeft = centerPt - halfW * vecV;
                    XYZ ptRight = centerPt + halfW * vecV;

                    var singleCurve = new List<Curve> { Line.CreateBound(ptLeft, ptRight) };
                    var rebar = CreateSingleRebar(doc, host, distBarType, singleCurve, vecSlope, config);
                    if (rebar != null) createdCount++;
                }

                // Top distribution bars (under top bars)
                if (config.EnableTopDistRebar)
                {
                    double zOffDistTop = tWaist - cTop - dTop - dDist * 0.5;
                    for (int i = 0; i <= numSteps; i++)
                    {
                        double dist = i * distSpacing;
                        if (dist > lSlope) break;

                        XYZ centerPt = pKinkBot + dist * vecSlope + zOffDistTop * vecNormWaist;
                        XYZ ptLeft = centerPt - halfW * vecV;
                        XYZ ptRight = centerPt + halfW * vecV;

                        var singleCurve = new List<Curve> { Line.CreateBound(ptLeft, ptRight) };
                        var rebar = CreateSingleRebar(doc, host, distBarType, singleCurve, vecSlope, config);
                        if (rebar != null) createdCount++;
                    }
                }
            }

            return createdCount;
        }

        private static int GenerateForGenericFlight(Document doc, Element stairElem, StairRebarConfig config)
        {
            BoundingBoxXYZ bbox = stairElem.get_BoundingBox(null);
            if (bbox == null) return 0;

            double width = bbox.Max.X - bbox.Min.X;
            double length = bbox.Max.Y - bbox.Min.Y;
            double height = bbox.Max.Z - bbox.Min.Z;

            // Treat longer horizontal dimension as run direction
            XYZ pStart = (length >= width) ? new XYZ((bbox.Min.X + bbox.Max.X) / 2.0, bbox.Min.Y, bbox.Min.Z)
                                           : new XYZ(bbox.Min.X, (bbox.Min.Y + bbox.Max.Y) / 2.0, bbox.Min.Z);
            XYZ pEnd = (length >= width) ? new XYZ((bbox.Min.X + bbox.Max.X) / 2.0, bbox.Max.Y, bbox.Max.Z)
                                         : new XYZ(bbox.Max.X, (bbox.Min.Y + bbox.Max.Y) / 2.0, bbox.Max.Z);

            return GenerateForRun(doc, stairElem, stairElem, config);
        }

        private static int CreateRebarGroup(Document doc, Element host, RebarBarType barType, IList<Curve> curves, XYZ arrayNormal, double spacing, double arrayLength, StairRebarConfig config)
        {
            try
            {
                Rebar rebar = Rebar.CreateFromCurves(
                    doc,
                    RebarStyle.Standard,
                    barType,
                    null, null,
                    host,
                    arrayNormal,
                    curves,
                    RebarHookOrientation.Right,
                    RebarHookOrientation.Right,
                    true, true);

                if (rebar != null)
                {
                    RebarShapeDrivenAccessor accessor = rebar.GetShapeDrivenAccessor();
                    if (accessor != null && spacing > 0.05 && arrayLength > 0.1)
                    {
                        accessor.SetLayoutAsMaximumSpacing(spacing, arrayLength, true, true, true);
                    }

                    ApplyProperties(doc, rebar, config);
                    return 1;
                }
            }
            catch
            {
                // Fallback: create individual bars across array width
                int placed = 0;
                double halfLen = arrayLength * 0.5;
                for (double offset = -halfLen; offset <= halfLen + 0.001; offset += spacing)
                {
                    try
                    {
                        Transform trans = Transform.CreateTranslation(offset * arrayNormal);
                        var transCurves = curves.Select(c => c.CreateTransformed(trans)).ToList();
                        Rebar singleBar = Rebar.CreateFromCurves(
                            doc,
                            RebarStyle.Standard,
                            barType,
                            null, null,
                            host,
                            arrayNormal,
                            transCurves,
                            RebarHookOrientation.Right,
                            RebarHookOrientation.Right,
                            true, true);

                        if (singleBar != null)
                        {
                            ApplyProperties(doc, singleBar, config);
                            placed++;
                        }
                    }
                    catch { }
                }
                return placed;
            }

            return 0;
        }

        private static Rebar? CreateSingleRebar(Document doc, Element host, RebarBarType barType, IList<Curve> curves, XYZ normal, StairRebarConfig config)
        {
            try
            {
                Rebar rebar = Rebar.CreateFromCurves(
                    doc,
                    RebarStyle.Standard,
                    barType,
                    null, null,
                    host,
                    normal,
                    curves,
                    RebarHookOrientation.Right,
                    RebarHookOrientation.Right,
                    true, true);

                if (rebar != null)
                {
                    ApplyProperties(doc, rebar, config);
                }
                return rebar;
            }
            catch
            {
                return null;
            }
        }

        private static void ApplyProperties(Document doc, Rebar rebar, StairRebarConfig config)
        {
            if (rebar == null) return;

            // Partition
            if (!string.IsNullOrWhiteSpace(config.PartitionName))
            {
                Parameter partParam = rebar.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM)
                                   ?? rebar.LookupParameter("Partition");
                if (partParam != null && !partParam.IsReadOnly)
                {
                    partParam.Set(config.PartitionName);
                }
            }

            // 3D Solid & Unobscured Visibility
            try
            {
                if (config.ViewUnobscured && doc.ActiveView != null)
                {
                    rebar.SetUnobscuredInView(doc.ActiveView, true);
                }

                if (config.ViewAsSolid3D)
                {
                    var views3D = new FilteredElementCollector(doc)
                        .OfClass(typeof(View3D))
                        .Cast<View3D>()
                        .Where(v => !v.IsTemplate);

                    foreach (var v in views3D)
                    {
                        try
                        {
                            rebar.SetUnobscuredInView(v, true);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
    }
}
