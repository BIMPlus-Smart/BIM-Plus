using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AutoPileCap.UI;

namespace AutoPileCap.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PartitionRebarCommand : IExternalCommand
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
                    message = "Please open a Revit structural model before running Partition Rebar.";
                    return Result.Failed;
                }

                Document doc = uidoc.Document;

                // Capture initial preselected elements if any
                var selectedIds = uidoc.Selection.GetElementIds();

                var handler = new AutoPileCap.Handlers.GenericEventHandler();
                var exEvent = ExternalEvent.Create(handler);

                var window = new PartitionRebarWindow(uiapp, handler, exEvent, selectedIds);
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Partition Rebar Error", $"An error occurred while launching Partition Rebar:\n{ex.Message}\n\nStack:\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}
