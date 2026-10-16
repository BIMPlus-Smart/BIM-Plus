using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using AutoPileCap.Models;

namespace AutoPileCap.Services
{
    public class PartitionRebarService
    {
        private static readonly System.Windows.Media.Color[] PaletteColors = new[]
        {
            System.Windows.Media.Color.FromRgb(37, 99, 235),   // Blue
            System.Windows.Media.Color.FromRgb(16, 185, 129),  // Emerald
            System.Windows.Media.Color.FromRgb(245, 158, 11),  // Amber
            System.Windows.Media.Color.FromRgb(139, 92, 246),  // Purple
            System.Windows.Media.Color.FromRgb(236, 72, 153),  // Pink
            System.Windows.Media.Color.FromRgb(6, 182, 212),   // Cyan
            System.Windows.Media.Color.FromRgb(249, 115, 22),  // Orange
            System.Windows.Media.Color.FromRgb(20, 184, 166),  // Teal
            System.Windows.Media.Color.FromRgb(99, 102, 241),  // Indigo
            System.Windows.Media.Color.FromRgb(132, 204, 22),  // Lime
            System.Windows.Media.Color.FromRgb(217, 70, 239),  // Fuchsia
            System.Windows.Media.Color.FromRgb(14, 165, 233),  // Sky
            System.Windows.Media.Color.FromRgb(239, 68, 68),   // Red
            System.Windows.Media.Color.FromRgb(168, 85, 247),  // Violet
            System.Windows.Media.Color.FromRgb(34, 197, 94),   // Green
            System.Windows.Media.Color.FromRgb(234, 179, 8),   // Yellow
            System.Windows.Media.Color.FromRgb(100, 116, 139)  // Slate
        };

        /// <summary>
        /// Retrieves all Structural Rebar elements according to the chosen scope,
        /// accurately multiplying quantities, lengths, and weights by the "Rebar Sets" parameter.
        /// </summary>
        public List<RebarItemModel> GetRebars(Document doc, SelectionScope scope, ICollection<ElementId>? preselectedIds = null)
        {
            var results = new List<RebarItemModel>();
            if (doc == null) return results;

            FilteredElementCollector collector;

            switch (scope)
            {
                case SelectionScope.ActiveView:
                    if (doc.ActiveView == null) return results;
                    collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                    break;

                case SelectionScope.CurrentSelection:
                    if (preselectedIds == null || preselectedIds.Count == 0) return results;
                    collector = new FilteredElementCollector(doc, preselectedIds);
                    break;

                case SelectionScope.WholeModel:
                default:
                    collector = new FilteredElementCollector(doc);
                    break;
            }

            var elements = collector
                .OfCategory(BuiltInCategory.OST_Rebar)
                .WhereElementIsNotElementType()
                .ToElements();

            // Distinct partition colors map
            var partitionColorMap = new Dictionary<string, (System.Windows.Media.Color color, SolidColorBrush brush)>(StringComparer.OrdinalIgnoreCase);
            int colorIdx = 0;

            foreach (var elem in elements)
            {
                try
                {
                    string partition = GetPartitionName(elem);
                    if (string.IsNullOrWhiteSpace(partition))
                    {
                        partition = "(Unassigned)";
                    }

                    if (!partitionColorMap.TryGetValue(partition, out var colorPair))
                    {
                        var c = PaletteColors[colorIdx % PaletteColors.Length];
                        var b = new SolidColorBrush(c);
                        b.Freeze();
                        colorPair = (c, b);
                        partitionColorMap[partition] = colorPair;
                        colorIdx++;
                    }

                    // Bar Type & diameter
                    string typeName = "Rebar Bar";
                    double diameterMm = 0.0;
                    ElementId typeId = elem.GetTypeId();
                    if (typeId != ElementId.InvalidElementId)
                    {
                        var barType = doc.GetElement(typeId) as RebarBarType;
                        if (barType != null)
                        {
                            typeName = barType.Name;
                            double diaFt = barType.BarNominalDiameter > 0 ? barType.BarNominalDiameter : barType.BarModelDiameter;
                            diameterMm = diaFt * GeometryHelper.MmPerFoot;
                        }
                    }

                    // Base Single-Set Quantity & Base Total Length
                    int singleQuantity = 1;
                    double singleSetLengthM = 0.0;

                    if (elem is Rebar r)
                    {
                        singleQuantity = r.Quantity > 0 ? r.Quantity : 1;
                        double lenFt = r.TotalLength;
                        if (lenFt <= 0.0001)
                        {
                            Parameter? lenParam = r.get_Parameter(BuiltInParameter.REBAR_ELEM_LENGTH);
                            if (lenParam != null && lenParam.HasValue)
                            {
                                lenFt = lenParam.AsDouble() * singleQuantity;
                            }
                        }
                        singleSetLengthM = lenFt * 0.3048;
                    }
                    else if (elem is RebarInSystem ris)
                    {
                        singleQuantity = ris.Quantity > 0 ? ris.Quantity : 1;
                        singleSetLengthM = ris.TotalLength * 0.3048;
                    }
                    else
                    {
                        Parameter? qParam = elem.get_Parameter(BuiltInParameter.REBAR_ELEM_QUANTITY_OF_BARS);
                        if (qParam != null && qParam.HasValue)
                        {
                            singleQuantity = qParam.AsInteger();
                            if (singleQuantity <= 0) singleQuantity = 1;
                        }
                        Parameter? lenParam = elem.get_Parameter(BuiltInParameter.REBAR_ELEM_LENGTH);
                        if (lenParam != null && lenParam.HasValue)
                        {
                            singleSetLengthM = lenParam.AsDouble() * singleQuantity * 0.3048;
                        }
                    }

                    // Read Rebar Sets multiplier parameter (e.g. 2 for 2 identical columns)
                    double rebarSets = GetRebarSets(elem);

                    // Multiplied quantities according to Rebar Sets (matching Revit Schedule)
                    int totalBars = (int)Math.Round(singleQuantity * rebarSets);
                    double totalLengthM = singleSetLengthM * rebarSets;

                    // Calculate weight (kg): totalLength(m) * (d^2 / 162.197)
                    double weightKg = totalLengthM * (diameterMm * diameterMm / 162.197);

                    // Host Element
                    string hostCategory = "None";
                    string hostName = string.Empty;
                    ElementId hostId = ElementId.InvalidElementId;

                    if (elem is Rebar rebarObj)
                    {
                        hostId = rebarObj.GetHostId();
                    }
                    else if (elem is RebarInSystem rebarSys)
                    {
                        hostId = rebarSys.GetHostId();
                    }

                    if (hostId != null && hostId != ElementId.InvalidElementId)
                    {
                        var host = doc.GetElement(hostId);
                        if (host != null)
                        {
                            hostCategory = host.Category?.Name ?? "Host";
                            hostName = host.Name;
                            string mark = host.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(mark))
                            {
                                hostName = $"{mark} ({hostName})";
                            }
                        }
                    }

                    // Shape
                    string shapeName = "-";
                    var shapeParam = elem.get_Parameter(BuiltInParameter.REBAR_SHAPE);
                    if (shapeParam != null && shapeParam.HasValue)
                    {
                        string strVal = shapeParam.AsValueString();
                        if (!string.IsNullOrEmpty(strVal))
                        {
                            shapeName = strVal;
                        }
                        else
                        {
                            var sId = shapeParam.AsElementId();
                            if (sId != null && sId != ElementId.InvalidElementId)
                            {
                                var shapeElem = doc.GetElement(sId);
                                if (shapeElem != null) shapeName = shapeElem.Name;
                            }
                        }
                    }

                    // Schedule Mark / Rebar Number
                    string schedMark = string.Empty;
                    if (elem is Rebar rebarMarkObj)
                    {
                        schedMark = rebarMarkObj.ScheduleMark;
                    }
                    if (string.IsNullOrEmpty(schedMark))
                    {
                        schedMark = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? string.Empty;
                    }

                    string rebarNum = elem.get_Parameter(BuiltInParameter.REBAR_NUMBER)?.AsString() 
                                      ?? elem.get_Parameter(BuiltInParameter.REBAR_NUMBER)?.AsInteger().ToString() 
                                      ?? string.Empty;

                    results.Add(new RebarItemModel
                    {
                        RebarId = elem.Id,
                        CategoryName = elem.Category?.Name ?? "Structural Rebar",
                        FamilyName = "Rebar Bar",
                        TypeName = typeName,
                        TypeId = typeId,
                        Partition = partition,
                        HostCategory = hostCategory,
                        HostName = hostName,
                        HostId = hostId,
                        ShapeName = shapeName,
                        SingleQuantity = singleQuantity,
                        RebarSets = rebarSets,
                        Quantity = totalBars, // Total bars = SingleQty * RebarSets
                        DiameterMm = Math.Round(diameterMm, 1),
                        TotalLengthM = Math.Round(totalLengthM, 2),
                        WeightKg = Math.Round(weightKg, 2),
                        ScheduleMark = schedMark,
                        RebarNumber = rebarNum,
                        Color = colorPair.color,
                        ColorBrush = colorPair.brush
                    });
                }
                catch
                {
                    // Continue scanning remaining rebars safely
                }
            }

            return results;
        }

        /// <summary>
        /// Reads the 'Rebar Sets' multiplier parameter from the rebar element.
        /// Searches instance parameters, case-insensitively, and type parameters as fallback.
        /// Defaults to 1.0 if not found or invalid.
        /// </summary>
        public static double GetRebarSets(Element elem)
        {
            if (elem == null) return 1.0;

            string[] candidateNames = new[] { "Rebar Sets", "Rebar Set", "RebarSets", "Number of Sets", "Sets" };

            // 1. Check exact candidate names on element
            foreach (var name in candidateNames)
            {
                var p = elem.LookupParameter(name);
                if (p != null && p.HasValue)
                {
                    double val = ReadNumericParam(p);
                    if (val > 0) return val;
                }
            }

            // 2. Case-insensitive search across all parameters
            foreach (Parameter p in elem.Parameters)
            {
                if (p.Definition != null)
                {
                    string pName = p.Definition.Name;
                    if (pName.Equals("Rebar Sets", StringComparison.OrdinalIgnoreCase) ||
                        pName.Equals("Rebar Set", StringComparison.OrdinalIgnoreCase) ||
                        pName.Equals("RebarSets", StringComparison.OrdinalIgnoreCase) ||
                        pName.Equals("Number of Sets", StringComparison.OrdinalIgnoreCase))
                    {
                        if (p.HasValue)
                        {
                            double val = ReadNumericParam(p);
                            if (val > 0) return val;
                        }
                    }
                }
            }

            // 3. Fallback to Type parameter if defined on Type
            var typeId = elem.GetTypeId();
            if (typeId != null && typeId != ElementId.InvalidElementId && elem.Document != null)
            {
                var typeElem = elem.Document.GetElement(typeId);
                if (typeElem != null)
                {
                    foreach (var name in candidateNames)
                    {
                        var p = typeElem.LookupParameter(name);
                        if (p != null && p.HasValue)
                        {
                            double val = ReadNumericParam(p);
                            if (val > 0) return val;
                        }
                    }
                }
            }

            return 1.0;
        }

        private static double ReadNumericParam(Parameter p)
        {
            if (p == null || !p.HasValue) return 0;
            if (p.StorageType == StorageType.Integer)
            {
                return p.AsInteger();
            }
            if (p.StorageType == StorageType.Double)
            {
                return p.AsDouble();
            }
            if (p.StorageType == StorageType.String)
            {
                string s = p.AsString();
                if (!string.IsNullOrWhiteSpace(s) && double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsed))
                {
                    return parsed;
                }
            }
            return 0;
        }

        /// <summary>
        /// Gets the partition string parameter from a rebar element safely.
        /// </summary>
        public static string GetPartitionName(Element elem)
        {
            if (elem == null) return string.Empty;

            var param = elem.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM);
            if (param != null && param.HasValue)
            {
                string val = param.AsString();
                if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
            }

            var customParam = elem.LookupParameter("Partition");
            if (customParam != null && customParam.HasValue)
            {
                string val = customParam.AsString();
                if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
            }

            return string.Empty;
        }

        /// <summary>
        /// Groups raw rebar items into Partition groups for the left sidebar.
        /// </summary>
        public List<PartitionGroupItem> GroupByPartitions(List<RebarItemModel> items)
        {
            var groups = items
                .GroupBy(i => i.Partition, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key.Equals("(Unassigned)", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(g => g.Key)
                .Select(g =>
                {
                    var first = g.First();
                    return new PartitionGroupItem
                    {
                        PartitionName = g.Key,
                        ItemCount = g.Count(),
                        TotalBarQuantity = g.Sum(x => x.Quantity), // total bars accounting for Rebar Sets
                        TotalLengthM = Math.Round(g.Sum(x => x.TotalLengthM), 2),
                        TotalWeightKg = Math.Round(g.Sum(x => x.WeightKg), 2),
                        Items = g.ToList(),
                        Color = first.Color,
                        ColorBrush = first.ColorBrush,
                        IsChecked = false
                    };
                })
                .ToList();

            return groups;
        }

        /// <summary>
        /// Groups items into Partition + Type rows (matches the DiRoots OneFilter table in user image),
        /// displaying Rebar Sets and multiplied weights/lengths.
        /// </summary>
        public List<PartitionTypeRowModel> GroupByPartitionAndType(List<RebarItemModel> items)
        {
            var rows = items
                .GroupBy(i => (Partition: i.Partition, TypeName: i.TypeName, HostCategory: i.HostCategory), (key, group) =>
                {
                    var first = group.First();
                    var list = group.ToList();

                    // Calculate distinct rebar sets
                    var setsList = list.Select(x => x.RebarSets).Distinct().ToList();
                    string setsDisplay = setsList.Count == 1 
                        ? (setsList[0] == 1.0 ? "1" : $"{setsList[0]:G}") 
                        : (setsList.All(s => s > 1) ? $"{setsList.Min():G}-{setsList.Max():G}" : "<varies>");
                    double avgRebarSets = setsList.Count == 1 ? setsList[0] : list.Average(x => x.RebarSets);

                    return new PartitionTypeRowModel
                    {
                        CategoryName = first.CategoryName,
                        FamilyName = first.FamilyName,
                        TypeName = key.TypeName,
                        Partition = key.Partition,
                        HostCategory = key.HostCategory,
                        ShapeName = list.Select(x => x.ShapeName).Distinct().Count() == 1 ? first.ShapeName : $"{list.Select(x => x.ShapeName).Distinct().Count()} Shapes",
                        DiameterMm = first.DiameterMm,
                        Count = list.Count, // number of modeled rebar element sets
                        RebarSets = avgRebarSets,
                        RebarSetsDisplay = setsDisplay,
                        BarQuantity = list.Sum(x => x.Quantity), // total bars multiplied by Rebar Sets
                        TotalLengthM = Math.Round(list.Sum(x => x.TotalLengthM), 2),
                        TotalWeightKg = Math.Round(list.Sum(x => x.WeightKg), 2),
                        ElementIds = list.Select(x => x.RebarId).ToList(),
                        ChildItems = list,
                        Color = first.Color,
                        ColorBrush = first.ColorBrush,
                        IsChecked = false
                    };
                })
                .OrderBy(r => r.Partition.Equals("(Unassigned)", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(r => r.Partition)
                .ThenBy(r => r.TypeName)
                .ToList();

            return rows;
        }

        /// <summary>
        /// Selects the given element IDs in the Revit active document.
        /// </summary>
        public void SelectElementsInRevit(UIDocument uidoc, ICollection<ElementId> ids)
        {
            if (uidoc == null) return;
            uidoc.Selection.SetElementIds(ids ?? new List<ElementId>());
        }

        /// <summary>
        /// Temporarily isolates elements in the active Revit view.
        /// </summary>
        public void IsolateElementsInView(UIDocument uidoc, ICollection<ElementId> ids)
        {
            if (uidoc == null || uidoc.ActiveView == null || ids == null || ids.Count == 0) return;
            var view = uidoc.ActiveView;
            if (view.CanEnableTemporaryViewPropertiesMode() || !view.IsTemplate)
            {
                view.IsolateElementsTemporary(ids);
            }
        }

        /// <summary>
        /// Resets temporary hide/isolate mode in the active view.
        /// </summary>
        public void ResetView(UIDocument uidoc)
        {
            if (uidoc == null || uidoc.ActiveView == null) return;
            var view = uidoc.ActiveView;
            if (view.IsInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate))
            {
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
            }
        }

        /// <summary>
        /// Colorizes each partition with a distinct vibrant color override in the active view.
        /// </summary>
        public void ColorizePartitions(Document doc, View activeView, List<PartitionGroupItem> partitions)
        {
            if (doc == null || activeView == null || partitions == null || partitions.Count == 0) return;

            // Find solid fill pattern element if present
            FillPatternElement? solidFill = FillPatternElement.GetFillPatternElementByName(doc, FillPatternTarget.Drafting, "<Solid fill>")
                ?? new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(f => f.Name.IndexOf("Solid", StringComparison.OrdinalIgnoreCase) >= 0);

            using (var trans = new Transaction(doc, "Colorize Rebar Partitions"))
            {
                trans.Start();

                foreach (var part in partitions)
                {
                    var revitColor = new Autodesk.Revit.DB.Color(part.Color.R, part.Color.G, part.Color.B);
                    var ogs = new OverrideGraphicSettings();
                    ogs.SetProjectionLineColor(revitColor);
                    ogs.SetCutLineColor(revitColor);

                    if (solidFill != null)
                    {
                        ogs.SetSurfaceForegroundPatternId(solidFill.Id);
                        ogs.SetSurfaceForegroundPatternColor(revitColor);
                        ogs.SetSurfaceForegroundPatternVisible(true);

                        ogs.SetCutForegroundPatternId(solidFill.Id);
                        ogs.SetCutForegroundPatternColor(revitColor);
                        ogs.SetCutForegroundPatternVisible(true);
                    }

                    foreach (var item in part.Items)
                    {
                        activeView.SetElementOverrides(item.RebarId, ogs);
                    }
                }

                trans.Commit();
            }
        }

        /// <summary>
        /// Resets graphic color overrides on rebar elements.
        /// </summary>
        public void ResetColors(Document doc, View activeView, IEnumerable<ElementId> ids)
        {
            if (doc == null || activeView == null || ids == null) return;

            using (var trans = new Transaction(doc, "Reset Rebar Graphic Overrides"))
            {
                trans.Start();
                var emptyOgs = new OverrideGraphicSettings();
                foreach (var id in ids)
                {
                    activeView.SetElementOverrides(id, emptyOgs);
                }
                trans.Commit();
            }
        }

        /// <summary>
        /// Updates the Partition parameter value on the specified elements.
        /// </summary>
        public int UpdatePartition(Document doc, ICollection<ElementId> ids, string newPartition)
        {
            if (doc == null || ids == null || ids.Count == 0) return 0;

            int updatedCount = 0;
            using (var trans = new Transaction(doc, "Assign Rebar Partition"))
            {
                trans.Start();

                foreach (var id in ids)
                {
                    var elem = doc.GetElement(id);
                    if (elem == null) continue;

                    var param = elem.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM);
                    if (param != null && !param.IsReadOnly)
                    {
                        param.Set(newPartition ?? string.Empty);
                        updatedCount++;
                    }
                    else
                    {
                        var customParam = elem.LookupParameter("Partition");
                        if (customParam != null && !customParam.IsReadOnly)
                        {
                            customParam.Set(newPartition ?? string.Empty);
                            updatedCount++;
                        }
                    }
                }

                trans.Commit();
            }

            return updatedCount;
        }

        /// <summary>
        /// Updates the 'Rebar Sets' multiplier parameter on the specified elements.
        /// </summary>
        public int UpdateRebarSets(Document doc, ICollection<ElementId> ids, double newRebarSets)
        {
            if (doc == null || ids == null || ids.Count == 0 || newRebarSets <= 0) return 0;

            int updatedCount = 0;
            using (var trans = new Transaction(doc, "Set Rebar Sets Multiplier"))
            {
                trans.Start();

                foreach (var id in ids)
                {
                    var elem = doc.GetElement(id);
                    if (elem == null) continue;

                    Parameter? p = elem.LookupParameter("Rebar Sets")
                                ?? elem.LookupParameter("Rebar Set")
                                ?? elem.LookupParameter("RebarSets")
                                ?? elem.LookupParameter("Number of Sets");

                    if (p != null && !p.IsReadOnly)
                    {
                        if (p.StorageType == StorageType.Integer)
                        {
                            p.Set((int)Math.Round(newRebarSets));
                            updatedCount++;
                        }
                        else if (p.StorageType == StorageType.Double)
                        {
                            p.Set(newRebarSets);
                            updatedCount++;
                        }
                        else if (p.StorageType == StorageType.String)
                        {
                            p.Set(((int)Math.Round(newRebarSets)).ToString());
                            updatedCount++;
                        }
                    }
                }

                trans.Commit();
            }

            return updatedCount;
        }

        /// <summary>
        /// Exports partition type rows to a CSV file.
        /// </summary>
        public void ExportToCsv(string filePath, IEnumerable<PartitionTypeRowModel> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Category,Family,Type Name,Partition,Host Category,Diameter (mm),Rebar Sets,Count (Sets),Bar Quantity,Total Length (m),Estimated Weight (kg)");

            foreach (var r in rows)
            {
                sb.AppendLine($"\"{EscapeCsv(r.CategoryName)}\",\"{EscapeCsv(r.FamilyName)}\",\"{EscapeCsv(r.TypeName)}\",\"{EscapeCsv(r.Partition)}\",\"{EscapeCsv(r.HostCategory)}\",{r.DiameterMm},\"{r.RebarSetsDisplay}\",{r.Count},{r.BarQuantity},{r.TotalLengthM},{r.TotalWeightKg}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string val)
        {
            if (string.IsNullOrEmpty(val)) return string.Empty;
            return val.Replace("\"", "\"\"");
        }
    }
}
