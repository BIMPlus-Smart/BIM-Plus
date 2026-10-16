using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AutoPileCap.Models;
using AutoPileCap.Services;

namespace AutoPileCap.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class QuickPileCapCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                
                if (!Services.LicenseManager.VerifyLicense(uiapp))
                    return Result.Cancelled;

                UIDocument uidoc = uiapp.ActiveUIDocument;

                if (uidoc == null || uidoc.Document == null)
                {
                    message = "Please open a Revit project.";
                    return Result.Failed;
                }

                Document doc = uidoc.Document;
                var service = new RevitElementService(doc);

                var selectedIds = uidoc.Selection.GetElementIds();
                var columns = service.GetStructuralColumns(selectedIds);

                if (columns.Count == 0)
                {
                    TaskDialog.Show("Quick Pile Cap", "Please select at least one Structural Column in the model first, then click Quick 4-Pile Cap.");
                    return Result.Cancelled;
                }

                var symbols = service.GetFoundationSymbols();
                if (symbols.Count == 0)
                {
                    TaskDialog.Show("Quick Pile Cap", "No Structural Foundation families are loaded in this project. Please load a Pile Cap / Footing family first.");
                    return Result.Failed;
                }

                // Pick best matching cap and pile symbols
                FamilySymbol capSymbol = symbols.FirstOrDefault(s => s.FamilyName.Contains("Cap", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("Cap", StringComparison.OrdinalIgnoreCase))
                                      ?? symbols.FirstOrDefault(s => s.FamilyName.Contains("Footing", StringComparison.OrdinalIgnoreCase))
                                      ?? symbols.First();

                FamilySymbol pileSymbol = symbols.FirstOrDefault(s => s.FamilyName.Contains("Pile", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("Pile", StringComparison.OrdinalIgnoreCase))
                                       ?? capSymbol;

                var config = new PileCapConfig
                {
                    PileCount = 4,
                    PileDiameterMm = 400.0,
                    SpacingMultiplier = 3.0,
                    EdgeDistanceMm = 250.0,
                    CapThicknessMm = 800.0,
                    PileLengthMm = 12000.0,
                    CreatePilesUnderCap = (pileSymbol != null && pileSymbol.Id != capSymbol.Id),
                    AlignWithColumnRotation = true
                };

                var layout = PileLayoutPattern.Calculate(config);

                int capsCreated = 0;
                int pilesCreated = 0;

                using (Transaction trans = new Transaction(doc, "Quick 4-Pile Cap"))
                {
                    trans.Start();

                    foreach (var col in columns)
                    {
                        if (!service.GetColumnBaseInfo(col, out XYZ basePoint, out double rotation, out Level baseLevel))
                        {
                            continue;
                        }

                        if (baseLevel == null)
                        {
                            baseLevel = service.GetClosestLevelBelow(basePoint.Z);
                        }

                        var cap = service.PlacePileCap(
                            basePoint,
                            rotation,
                            capSymbol,
                            baseLevel,
                            0.0,
                            layout.CapWidthFeet,
                            layout.CapLengthFeet,
                            config.CapThicknessFeet);

                        if (cap != null) capsCreated++;

                        if (config.CreatePilesUnderCap && pileSymbol != null)
                        {
                            double pileOffsetZ = -config.CapThicknessFeet;
                            foreach (var offset in layout.Offsets)
                            {
                                XYZ pileTop = GeometryHelper.TransformLocalOffset(
                                    basePoint,
                                    offset.X,
                                    offset.Y,
                                    pileOffsetZ,
                                    rotation);

                                var pile = service.PlacePile(
                                    pileTop,
                                    rotation,
                                    pileSymbol,
                                    baseLevel,
                                    config.PileLengthFeet);

                                if (pile != null) pilesCreated++;
                            }
                        }
                    }

                    trans.Commit();
                }

                TaskDialog.Show("Quick Pile Cap Complete",
                    $"Successfully created:\n\n• {capsCreated} Pile Caps ({capSymbol.Name})\n• {pilesCreated} Piles\n\nUnder {columns.Count} selected columns.");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Quick Pile Cap Error", $"An error occurred:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
