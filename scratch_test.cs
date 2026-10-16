using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.Attributes;

[Transaction(TransactionMode.Manual)]
public class TestShapeBrowser : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;
        var shapes = new FilteredElementCollector(doc).OfClass(typeof(RebarShape)).Cast<RebarShape>().ToList();
        
        string output = "";
        foreach (var shape in shapes.Take(5))
        {
            try {
                var curves = shape.GetCurvesForBrowser();
                output += $"{shape.Name}: {curves.Count} curves\n";
            } catch (Exception ex) {
                output += $"{shape.Name}: Error {ex.Message}\n";
            }
        }
        
        TaskDialog.Show("Output", output);
        return Result.Succeeded;
    }
}
