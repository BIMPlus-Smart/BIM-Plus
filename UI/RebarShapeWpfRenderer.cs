using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Shapes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace AutoPileCap.UI
{
    public class RebarShapeItem
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public Geometry Geometry { get; set; }
    }

    public static class RebarShapeWpfRenderer
    {
        public static Geometry GetShapeGeometry(RebarShape shape)
        {
            PathGeometry pathGeo = new PathGeometry();
            IList<Curve> curves;
            try { curves = shape.GetCurvesForBrowser(); }
            catch { return null; }

            if (curves == null || curves.Count == 0) return null;

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            // Discretize and find bounds
            List<List<System.Windows.Point>> discretizedCurves = new List<List<System.Windows.Point>>();
            foreach (var curve in curves)
            {
                List<System.Windows.Point> pts = new List<System.Windows.Point>();
                if (curve is Autodesk.Revit.DB.Line)
                {
                    var p0 = curve.GetEndPoint(0);
                    var p1 = curve.GetEndPoint(1);
                    pts.Add(new System.Windows.Point(p0.X, p0.Y));
                    pts.Add(new System.Windows.Point(p1.X, p1.Y));
                }
                else
                {
                    for (double t = 0; t <= 1.0; t += 0.1)
                    {
                        var pt = curve.Evaluate(t, true);
                        pts.Add(new System.Windows.Point(pt.X, pt.Y));
                    }
                }
                
                foreach(var p in pts)
                {
                    minX = Math.Min(minX, p.X);
                    minY = Math.Min(minY, p.Y);
                    maxX = Math.Max(maxX, p.X);
                    maxY = Math.Max(maxY, p.Y);
                }
                discretizedCurves.Add(pts);
            }

            double width = maxX - minX;
            double height = maxY - minY;
            double scale = 1.0;
            if (width > 0 || height > 0)
            {
                scale = 36.0 / Math.Max(width, height);
            }

            foreach (var pts in discretizedCurves)
            {
                if (pts.Count < 2) continue;
                PathFigure figure = new PathFigure();
                figure.StartPoint = new System.Windows.Point((pts[0].X - minX) * scale + 12, 48 - (pts[0].Y - minY) * scale);
                for (int i = 1; i < pts.Count; i++)
                {
                    figure.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point((pts[i].X - minX) * scale + 12, 48 - (pts[i].Y - minY) * scale), true));
                }
                pathGeo.Figures.Add(figure);
            }

            return pathGeo;
        }
    }
}
