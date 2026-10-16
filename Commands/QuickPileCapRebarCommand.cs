using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using AutoPileCap.Models;
using AutoPileCap.Services;

namespace AutoPileCap.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class QuickPileCapRebarCommand : IExternalCommand
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
                    message = "Please open a Revit structural project.";
                    return Result.Failed;
                }

                Document doc = uidoc.Document;
                var elemService = new RevitElementService(doc);
                var rebarService = new RebarGenerationService(doc);

                var selectedIds = uidoc.Selection.GetElementIds();
                var pileCaps = elemService.GetStructuralFoundations(selectedIds);

                if (pileCaps.Count == 0)
                {
                    TaskDialog.Show("Quick Pile Cap Rebar", "Please select at least one Pile Cap / Structural Foundation in the model first, then click Quick Rebar.");
                    return Result.Cancelled;
                }

                var barTypes = rebarService.GetRebarBarTypes();
                if (barTypes.Count == 0)
                {
                    TaskDialog.Show("Quick Pile Cap Rebar", "No Rebar Bar Types are loaded in this project. Please load structural rebar types first.");
                    return Result.Failed;
                }

                // Pick smart bar types
                RebarBarType mainBar = barTypes.FirstOrDefault(b => (b.BarNominalDiameter * GeometryHelper.MmPerFoot) >= 15.0) ?? barTypes.Last();
                RebarBarType tieBar  = barTypes.FirstOrDefault(b => (b.BarNominalDiameter * GeometryHelper.MmPerFoot) >= 9.5 && (b.BarNominalDiameter * GeometryHelper.MmPerFoot) <= 13.0) ?? barTypes.First();

                var config = new PileCapRebarConfig
                {
                    EnableBottomRebar = true,
                    BottomBarTypeXId = mainBar.Id.Value,
                    BottomBarTypeYId = mainBar.Id.Value,
                    BottomSpacingXMm = 150.0,
                    BottomSpacingYMm = 150.0,
                    BottomHookMode = RebarHookMode.BentLegs,
                    BottomHookLengthMm = 150.0,

                    EnableTopRebar = true,
                    TopBarTypeXId = mainBar.Id.Value,
                    TopBarTypeYId = mainBar.Id.Value,
                    TopSpacingXMm = 200.0,
                    TopSpacingYMm = 200.0,
                    TopHookMode = RebarHookMode.BentLegs,
                    TopHookLengthMm = 150.0,

                    EnableSideTies = true,
                    SideTieBarTypeId = tieBar.Id.Value,
                    SideTieQuantity = 3,

                    CoverBottomMm = 75.0,
                    CoverTopMm = 50.0,
                    CoverSideMm = 50.0,

                    ViewAsSolid3D = true,
                    ViewUnobscured = true
                };

                int capsReinforced = 0;
                int totalSets = 0;

                using (Transaction trans = new Transaction(doc, "Quick Pile Cap Rebar"))
                {
                    FailureHandlingOptions failOpt = trans.GetFailureHandlingOptions();
                    failOpt.SetFailuresPreprocessor(new RebarFailurePreprocessor());
                    trans.SetFailureHandlingOptions(failOpt);

                    trans.Start();

                    foreach (var cap in pileCaps)
                    {
                        int created = rebarService.GenerateRebarForPileCap(cap, config);
                        if (created > 0)
                        {
                            capsReinforced++;
                            totalSets += created;
                        }
                    }

                    trans.Commit();
                }

                TaskDialog.Show("Quick Rebar Completed",
                    $"Successfully reinforced:\n\n• {capsReinforced} Pile Caps\n• {totalSets} 3D Rebar Sets Generated\n\nBottom Mat: {mainBar.Name} @ 150mm (U-Bar)\nTop Mat: {mainBar.Name} @ 200mm (U-Bar)\nTies: {tieBar.Name} @ 200mm");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Quick Rebar Error", $"An error occurred:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
