using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using AutoPileCap.Models;

namespace AutoPileCap.Services
{
    public class RevitElementService
    {
        private readonly Document _doc;

        public RevitElementService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        /// <summary>
        /// Gets all Structural Columns or filters by preselected elements if provided.
        /// </summary>
        public List<FamilyInstance> GetStructuralColumns(ICollection<ElementId>? preselectedIds = null)
        {
            var collector = new FilteredElementCollector(_doc);

            if (preselectedIds != null && preselectedIds.Count > 0)
            {
                return collector
                    .WherePasses(new ElementMulticategoryFilter(new List<BuiltInCategory> { BuiltInCategory.OST_StructuralColumns }))
                    .WherePasses(new ElementIdSetFilter(preselectedIds))
                    .WhereElementIsNotElementType()
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .ToList();
            }

            return collector
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();
        }

        /// <summary>
        /// Gets all Structural Foundation elements (piles/caps) or filtered by preselected elements.
        /// </summary>
        public List<FamilyInstance> GetStructuralFoundations(ICollection<ElementId>? preselectedIds = null)
        {
            var collector = new FilteredElementCollector(_doc);

            if (preselectedIds != null && preselectedIds.Count > 0)
            {
                return collector
                    .WherePasses(new ElementMulticategoryFilter(new List<BuiltInCategory> { BuiltInCategory.OST_StructuralFoundation }))
                    .WherePasses(new ElementIdSetFilter(preselectedIds))
                    .WhereElementIsNotElementType()
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .ToList();
            }

            return collector
                .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();
        }

        /// <summary>
        /// Retrieves all loaded levels sorted by elevation.
        /// </summary>
        public List<Level> GetLevels()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();
        }

        /// <summary>
        /// Retrieves all Structural Foundation FamilySymbols (types) loaded in project.
        /// </summary>
        public List<FamilySymbol> GetFoundationSymbols()
        {
            return new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .OrderBy(s => s.FamilyName)
                .ThenBy(s => s.Name)
                .ToList();
        }

        /// <summary>
        /// Extracts base point XYZ, rotation angle (radians), and base level of a column.
        /// </summary>
        public bool GetColumnBaseInfo(FamilyInstance column, out XYZ basePoint, out double rotationAngle, out Level baseLevel)
        {
            basePoint = XYZ.Zero;
            rotationAngle = 0.0;
            baseLevel = null!;

            if (column == null) return false;

            // Determine base level
            Parameter baseLevelParam = column.get_Parameter(BuiltInParameter.SCHEDULE_BASE_LEVEL_PARAM)
                                    ?? column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);

            if (baseLevelParam != null && baseLevelParam.AsElementId() != ElementId.InvalidElementId)
            {
                baseLevel = (_doc.GetElement(baseLevelParam.AsElementId()) as Level)!;
            }

            double baseOffset = 0.0;
            Parameter baseOffsetParam = column.LookupParameter("Base Offset")
                                     ?? column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
            if (baseOffsetParam != null)
            {
                baseOffset = baseOffsetParam.AsDouble();
            }

            // Extract position & rotation from Location
            if (column.Location is LocationPoint locPoint)
            {
                double z = (baseLevel != null) ? (baseLevel.Elevation + baseOffset) : locPoint.Point.Z;
                basePoint = new XYZ(locPoint.Point.X, locPoint.Point.Y, z);
                rotationAngle = locPoint.Rotation;
                return true;
            }
            else if (column.Location is LocationCurve locCurve)
            {
                // Slanted column: lower end is the base
                XYZ p0 = locCurve.Curve.GetEndPoint(0);
                XYZ p1 = locCurve.Curve.GetEndPoint(1);
                basePoint = (p0.Z <= p1.Z) ? p0 : p1;

                XYZ dir = (p0.Z <= p1.Z) ? (p1 - p0) : (p0 - p1);
                rotationAngle = Math.Atan2(dir.Y, dir.X);

                if (baseLevel == null)
                {
                    baseLevel = GetClosestLevelBelow(basePoint.Z);
                }
                return true;
            }

            return false;
        }

        public Level GetClosestLevelBelow(double elevation)
        {
            var levels = GetLevels();
            if (levels.Count == 0) return null!;

            Level? candidate = levels.LastOrDefault(l => l.Elevation <= elevation);
            return candidate ?? levels.First();
        }

        /// <summary>
        /// Creates a Pile Cap FamilyInstance at the specified point, optionally rotates and sets dimensions.
        /// </summary>
        public FamilyInstance? PlacePileCap(
            XYZ location,
            double rotationAngle,
            FamilySymbol capSymbol,
            Level level,
            double levelOffsetFeet,
            double widthFeet,
            double lengthFeet,
            double thicknessFeet)
        {
            if (!capSymbol.IsActive)
            {
                capSymbol.Activate();
                _doc.Regenerate();
            }

            XYZ insertPoint = new XYZ(location.X, location.Y, location.Z + levelOffsetFeet);
            FamilyInstance instance = _doc.Create.NewFamilyInstance(
                insertPoint,
                capSymbol,
                level,
                StructuralType.Footing);

            if (instance == null) return null;

            // Apply rotation if needed
            if (Math.Abs(rotationAngle) > 0.0001)
            {
                Line axis = Line.CreateBound(insertPoint, insertPoint + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(_doc, instance.Id, axis, rotationAngle);
            }

            // Attempt to set instance dimension parameters if present
            TrySetParameter(instance, "Width", widthFeet);
            TrySetParameter(instance, "Length", lengthFeet);
            TrySetParameter(instance, "Thickness", thicknessFeet);
            TrySetParameter(instance, "Foundation Thickness", thicknessFeet);
            TrySetParameter(instance, "Depth", thicknessFeet);

            return instance;
        }

        /// <summary>
        /// Creates an individual Pile FamilyInstance at the given location, embedded below the pile cap.
        /// </summary>
        public FamilyInstance? PlacePile(
            XYZ topPoint,
            double rotationAngle,
            FamilySymbol pileSymbol,
            Level level,
            double pileLengthFeet)
        {
            if (!pileSymbol.IsActive)
            {
                pileSymbol.Activate();
                _doc.Regenerate();
            }

            FamilyInstance pileInstance = _doc.Create.NewFamilyInstance(
                topPoint,
                pileSymbol,
                level,
                StructuralType.Footing);

            if (pileInstance == null) return null;

            if (Math.Abs(rotationAngle) > 0.0001)
            {
                Line axis = Line.CreateBound(topPoint, topPoint + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(_doc, pileInstance.Id, axis, rotationAngle);
            }

            // Set length parameter if available
            TrySetParameter(pileInstance, "Length", pileLengthFeet);
            TrySetParameter(pileInstance, "Pile Length", pileLengthFeet);
            TrySetParameter(pileInstance, "Depth", pileLengthFeet);

            return pileInstance;
        }

        private static bool TrySetParameter(Element elem, string paramName, double value)
        {
            Parameter param = elem.LookupParameter(paramName);
            if (param != null && !param.IsReadOnly && param.StorageType == StorageType.Double)
            {
                param.Set(value);
                return true;
            }
            return false;
        }
        public System.Collections.Generic.IEnumerable<Autodesk.Revit.DB.Element> GetStructuralWalls(System.Collections.Generic.ICollection<Autodesk.Revit.DB.ElementId> selectedIds)
        {
            var doc = _doc;
            var selectedWalls = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(System.Linq.Enumerable.Select(selectedIds, id => doc.GetElement(id)), e => e != null && e.Category != null && e.Category.Id.Value == (long)Autodesk.Revit.DB.BuiltInCategory.OST_Walls));
            if (selectedWalls.Count > 0) return selectedWalls;
            return GetStructuralWalls();
        }

        public System.Collections.Generic.IEnumerable<Autodesk.Revit.DB.Element> GetStructuralWalls()
        {
            return new Autodesk.Revit.DB.FilteredElementCollector(_doc).OfCategory(Autodesk.Revit.DB.BuiltInCategory.OST_Walls).WhereElementIsNotElementType().ToElements();
        }

        public System.Collections.Generic.IEnumerable<Autodesk.Revit.DB.Element> GetFloors(System.Collections.Generic.ICollection<Autodesk.Revit.DB.ElementId> selectedIds)
        {
            var doc = _doc;
            var selectedFloors = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(System.Linq.Enumerable.Select(selectedIds, id => doc.GetElement(id)), e => e != null && e.Category != null && e.Category.Id.Value == (long)Autodesk.Revit.DB.BuiltInCategory.OST_Floors));
            if (selectedFloors.Count > 0) return selectedFloors;
            return GetFloors();
        }

        public System.Collections.Generic.IEnumerable<Autodesk.Revit.DB.Element> GetFloors()
        {
            return new Autodesk.Revit.DB.FilteredElementCollector(_doc).OfCategory(Autodesk.Revit.DB.BuiltInCategory.OST_Floors).WhereElementIsNotElementType().ToElements();
        }
    }
}
