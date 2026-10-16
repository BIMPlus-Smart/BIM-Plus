using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AutoPileCap.Services;
using AutoPileCap.UI;

namespace AutoPileCap.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class StairRebarCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                
                if (!LicenseManager.VerifyLicense(uiapp))
                    return Result.Cancelled;

                UIDocument uidoc = uiapp.ActiveUIDocument;

                if (uidoc == null || uidoc.Document == null)
                {
                    message = "Please open a Revit structural model before running Stair Rebar.";
                    return Result.Failed;
                }

                Document doc = uidoc.Document;
                var elemService = new RevitElementService(doc);

                // Initial target stairs from current selection
                var selectedIds = uidoc.Selection.GetElementIds();
                var targetStairs = elemService.GetStairs(selectedIds).ToList();

                var handler = new AutoPileCap.Handlers.GenericEventHandler();
                var exEvent = ExternalEvent.Create(handler);

                var window = new StairRebarWindow(uiapp, handler, exEvent, targetStairs);
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Stair Rebar Error", $"An error occurred:\n{ex.Message}\n\nStack:\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}
