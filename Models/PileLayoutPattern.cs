using System;
using System.Collections.Generic;

namespace AutoPileCap.Models
{
    public struct PileOffset2D
    {
        public double X { get; set; }
        public double Y { get; set; }

        public PileOffset2D(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    public class PileLayoutResult
    {
        public List<PileOffset2D> Offsets { get; set; } = new List<PileOffset2D>();
        public double CapWidthFeet { get; set; }
        public double CapLengthFeet { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public static class PileLayoutPattern
    {
        public static PileLayoutResult Calculate(PileCapConfig config)
        {
            var result = new PileLayoutResult();
            double s = config.EffectiveSpacingFeet;
            double d = config.PileDiameterFeet;
            double edge = config.EdgeDistanceFeet;

            switch (config.PileCount)
            {
                case 1:
                    result.Offsets.Add(new PileOffset2D(0, 0));
                    result.CapWidthFeet = d + 2 * edge;
                    result.CapLengthFeet = d + 2 * edge;
                    result.Description = "Single Pile Cap (Centric)";
                    break;

                case 2:
                    result.Offsets.Add(new PileOffset2D(-s / 2.0, 0));
                    result.Offsets.Add(new PileOffset2D(s / 2.0, 0));
                    result.CapWidthFeet = s + d + 2 * edge;
                    result.CapLengthFeet = d + 2 * edge;
                    result.Description = "2-Pile Linear Cap";
                    break;

                case 3:
                    // Equilateral triangle
                    double h = s * Math.Sqrt(3) / 2.0;
                    result.Offsets.Add(new PileOffset2D(0, 2.0 / 3.0 * h));
                    result.Offsets.Add(new PileOffset2D(-s / 2.0, -1.0 / 3.0 * h));
                    result.Offsets.Add(new PileOffset2D(s / 2.0, -1.0 / 3.0 * h));
                    result.CapWidthFeet = s + d + 2 * edge;
                    result.CapLengthFeet = h + d + 2 * edge;
                    result.Description = "3-Pile Triangular Cap";
                    break;

                case 4:
                    result.Offsets.Add(new PileOffset2D(-s / 2.0, -s / 2.0));
                    result.Offsets.Add(new PileOffset2D(s / 2.0, -s / 2.0));
                    result.Offsets.Add(new PileOffset2D(s / 2.0, s / 2.0));
                    result.Offsets.Add(new PileOffset2D(-s / 2.0, s / 2.0));
                    result.CapWidthFeet = s + d + 2 * edge;
                    result.CapLengthFeet = s + d + 2 * edge;
                    result.Description = "4-Pile Square Cap";
                    break;

                case 5:
                    result.Offsets.Add(new PileOffset2D(-s / 2.0, -s / 2.0));
                    result.Offsets.Add(new PileOffset2D(s / 2.0, -s / 2.0));
                    result.Offsets.Add(new PileOffset2D(s / 2.0, s / 2.0));
                    result.Offsets.Add(new PileOffset2D(-s / 2.0, s / 2.0));
                    result.Offsets.Add(new PileOffset2D(0, 0)); // Center pile
                    result.CapWidthFeet = s + d + 2 * edge;
                    result.CapLengthFeet = s + d + 2 * edge;
                    result.Description = "5-Pile Quincunx Cap";
                    break;

                case 6:
                    // 2x3 Grid: 3 in X, 2 in Y
                    result.Offsets.Add(new PileOffset2D(-s, -s / 2.0));
                    result.Offsets.Add(new PileOffset2D(0, -s / 2.0));
                    result.Offsets.Add(new PileOffset2D(s, -s / 2.0));
                    result.Offsets.Add(new PileOffset2D(-s, s / 2.0));
                    result.Offsets.Add(new PileOffset2D(0, s / 2.0));
                    result.Offsets.Add(new PileOffset2D(s, s / 2.0));
                    result.CapWidthFeet = 2 * s + d + 2 * edge;
                    result.CapLengthFeet = s + d + 2 * edge;
                    result.Description = "6-Pile Rectangular Cap (2x3)";
                    break;

                case 8:
                    // 3x3 outer ring without center
                    result.Offsets.Add(new PileOffset2D(-s, -s));
                    result.Offsets.Add(new PileOffset2D(0, -s));
                    result.Offsets.Add(new PileOffset2D(s, -s));
                    result.Offsets.Add(new PileOffset2D(-s, 0));
                    result.Offsets.Add(new PileOffset2D(s, 0));
                    result.Offsets.Add(new PileOffset2D(-s, s));
                    result.Offsets.Add(new PileOffset2D(0, s));
                    result.Offsets.Add(new PileOffset2D(s, s));
                    result.CapWidthFeet = 2 * s + d + 2 * edge;
                    result.CapLengthFeet = 2 * s + d + 2 * edge;
                    result.Description = "8-Pile Perimeter Cap";
                    break;

                case 9:
                    // 3x3 Full Grid
                    for (int ix = -1; ix <= 1; ix++)
                    {
                        for (int iy = -1; iy <= 1; iy++)
                        {
                            result.Offsets.Add(new PileOffset2D(ix * s, iy * s));
                        }
                    }
                    result.CapWidthFeet = 2 * s + d + 2 * edge;
                    result.CapLengthFeet = 2 * s + d + 2 * edge;
                    result.Description = "9-Pile Square Grid Cap";
                    break;

                default:
                    // Fallback to 4-pile
                    result.Offsets.Add(new PileOffset2D(-s / 2.0, -s / 2.0));
                    result.Offsets.Add(new PileOffset2D(s / 2.0, -s / 2.0));
                    result.Offsets.Add(new PileOffset2D(s / 2.0, s / 2.0));
                    result.Offsets.Add(new PileOffset2D(-s / 2.0, s / 2.0));
                    result.CapWidthFeet = s + d + 2 * edge;
                    result.CapLengthFeet = s + d + 2 * edge;
                    result.Description = $"{config.PileCount}-Pile Cap";
                    break;
            }

            return result;
        }
    }
}
