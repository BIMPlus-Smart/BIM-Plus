using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AutoPileCap.UI;

namespace AutoPileCap.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AutoPileCapCommand : IExternalCommand
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
                    message = "Please open a Revit project before running Auto Pile Cap.";
                    return Result.Failed;
                }

                var handler = new AutoPileCap.Handlers.GenericEventHandler();
                var exEvent = ExternalEvent.Create(handler);

                var window = new AutoPileCapWindow(uiapp, handler, exEvent);
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Auto Pile Cap Error", $"An unexpected error occurred:\n{ex.Message}\n\nStack:\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}
