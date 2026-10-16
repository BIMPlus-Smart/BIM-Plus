using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AutoPileCap.UI;

namespace AutoPileCap.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ColumnRebarCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Check if user selected anything before clicking the command
                var selectedIds = uidoc.Selection.GetElementIds();
                var selectedColumns = new List<FamilyInstance>();

                if (selectedIds.Count > 0)
                {
                    foreach (var id in selectedIds)
                    {
                        var elem = doc.GetElement(id) as FamilyInstance;
                        if (elem != null && elem.Category.Id.Value == (long)BuiltInCategory.OST_StructuralColumns)
                        {
                            selectedColumns.Add(elem);
                        }
                    }
                }

                // (Removed selection required check so it opens modelessly first)

                // Show the UI modelessly
                var handler = new AutoPileCap.Handlers.GenericEventHandler();
                var exEvent = ExternalEvent.Create(handler);

                var window = new ColumnRebarWindow(commandData.Application, handler, exEvent, selectedColumns);
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = commandData.Application.MainWindowHandle;
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
