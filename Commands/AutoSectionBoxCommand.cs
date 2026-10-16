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
    public class AutoSectionBoxCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            var selectedIds = uidoc.Selection.GetElementIds().ToList();
            if (selectedIds.Count == 0)
            {
                if (doc.ActiveView != null && doc.ActiveView.CropBoxActive && doc.ActiveView.CropBoxVisible)
                {
                    selectedIds.Add(doc.ActiveView.Id);
                }
                else
                {
                    TaskDialog.Show("Selection Required", "Please select one or more elements, or ensure the active view has 'Crop Region Visible' enabled.");
                    return Result.Cancelled;
                }
            }

            var window = new AutoSectionBoxWindow(doc, selectedIds);
            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}
