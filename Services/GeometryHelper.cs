using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AutoPileCap.Services
{
    public static class GeometryHelper
    {
        public const double MmPerFoot = 304.8;

        public static double MmToFeet(double mm) => mm / MmPerFoot;
        public static double FeetToMm(double feet) => feet * MmPerFoot;

        /// <summary>
        /// Rotates a 2D vector (dx, dy) by angle in radians, and returns the new world XYZ on the base plane.
        /// </summary>
        public static XYZ TransformLocalOffset(XYZ basePoint, double localDx, double localDy, double localDz, double rotationRadians)
        {
            double cos = Math.Cos(rotationRadians);
            double sin = Math.Sin(rotationRadians);

            double worldDx = localDx * cos - localDy * sin;
            double worldDy = localDx * sin + localDy * cos;

            return new XYZ(basePoint.X + worldDx, basePoint.Y + worldDy, basePoint.Z + localDz);
        }

        /// <summary>
        /// Calculates the centroid of a collection of points.
        /// </summary>
        public static XYZ CalculateCentroid(IList<XYZ> points)
        {
            if (points == null || points.Count == 0) return XYZ.Zero;

            double sumX = 0, sumY = 0, sumZ = 0;
            foreach (var pt in points)
            {
                sumX += pt.X;
                sumY += pt.Y;
                sumZ += pt.Z;
            }

            int count = points.Count;
            return new XYZ(sumX / count, sumY / count, sumZ / count);
        }

        /// <summary>
        /// Calculates min/max bounds of points in XY plane relative to a center point.
        /// </summary>
        public static void GetBounds2D(IList<XYZ> points, out double minX, out double maxX, out double minY, out double maxY)
        {
            minX = double.MaxValue;
            maxX = double.MinValue;
            minY = double.MaxValue;
            maxY = double.MinValue;

            foreach (var pt in points)
            {
                if (pt.X < minX) minX = pt.X;
                if (pt.X > maxX) maxX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
                if (pt.Y > maxY) maxY = pt.Y;
            }
        }

        /// <summary>
        /// Clusters a set of points based on a maximum distance threshold.
        /// Uses simple connected component graph clustering.
        /// </summary>
        public static List<List<T>> ClusterElements<T>(IList<T> items, Func<T, XYZ> pointSelector, double maxDistanceFeet)
        {
            var clusters = new List<List<T>>();
            var visited = new HashSet<int>();
            int n = items.Count;

            for (int i = 0; i < n; i++)
            {
                if (visited.Contains(i)) continue;

                var currentCluster = new List<T>();
                var queue = new Queue<int>();
                queue.Enqueue(i);
                visited.Add(i);

                while (queue.Count > 0)
                {
                    int currIdx = queue.Dequeue();
                    currentCluster.Add(items[currIdx]);
                    XYZ p1 = pointSelector(items[currIdx]);

                    for (int j = 0; j < n; j++)
                    {
                        if (visited.Contains(j)) continue;

                        XYZ p2 = pointSelector(items[j]);
                        double dist2D = Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));

                        if (dist2D <= maxDistanceFeet)
                        {
                            visited.Add(j);
                            queue.Enqueue(j);
                        }
                    }
                }

                clusters.Add(currentCluster);
            }

            return clusters;
        }
    }
}
