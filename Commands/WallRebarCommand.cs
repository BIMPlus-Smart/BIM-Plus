using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AutoPileCap.Services;
using AutoPileCap.UI;

namespace AutoPileCap.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class WallRebarCommand : IExternalCommand
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
                    message = "Please open a Revit structural model before running Wall Rebar.";
                    return Result.Failed;
                }

                Document doc = uidoc.Document;
                var elemService = new RevitElementService(doc);

                // Initial target Walls from current selection
                var selectedIds = uidoc.Selection.GetElementIds();
                var targetWalls = elemService.GetStructuralWalls(selectedIds).Cast<Element>().ToList();

                var handler = new AutoPileCap.Handlers.GenericEventHandler();
                var exEvent = ExternalEvent.Create(handler);

                var window = new WallRebarWindow(uiapp, handler, exEvent, targetWalls);
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Wall Rebar Error", $"An error occurred:\n{ex.Message}\n\nStack:\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}



