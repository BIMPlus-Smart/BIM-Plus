using System;

namespace AutoPileCap.Models
{

    public class SlabRebarConfig
    {
        // 1) General Settings & Covers (mm)
        public double CoverTopMm { get; set; } = 75.0;
        public double CoverBottomMm { get; set; } = 150.0;
        public double CoverSideMm { get; set; } = 75.0;

        // 2) Top Rebar (U-Shape Sets)
        public bool EnableTopRebar { get; set; } = true;
        public long TopBarTypeXId { get; set; } = -1; // Long Direction
        public long TopBarTypeYId { get; set; } = -1; // Short Direction
        public double TopHookXMm { get; set; } = 400.0;
        public double TopHookYMm { get; set; } = 400.0;
        public double TopSpacingXMm { get; set; } = 150.0;
        public double TopSpacingYMm { get; set; } = 150.0;
        public RebarHookMode TopHookMode { get; set; } = RebarHookMode.BentLegs;
        public long TopHookTypeId { get; set; } = -1;

        // 3) Bottom Rebar (U-Shape Sets)
        public bool EnableBottomRebar { get; set; } = true;
        public long BottomBarTypeXId { get; set; } = -1; // Long Direction
        public long BottomBarTypeYId { get; set; } = -1; // Short Direction
        public double BottomHookXMm { get; set; } = 450.0;
        public double BottomHookYMm { get; set; } = 450.0;
        public double BottomHookLengthMm { get => BottomHookXMm; set { BottomHookXMm = value; BottomHookYMm = value; } }
        public double BottomSpacingXMm { get; set; } = 150.0;
        public double BottomSpacingYMm { get; set; } = 150.0;
        public RebarHookMode BottomHookMode { get; set; } = RebarHookMode.BentLegs;
        public long BottomHookTypeId { get; set; } = -1;

        public double TopHookLengthMm { get => TopHookXMm; set { TopHookXMm = value; TopHookYMm = value; } }

        // 4) Side Bar / Skin Ties (Perimeter 21/M17)
        public bool EnableSideTies { get; set; } = true;
        public long SideTieBarTypeId { get; set; } = -1;
        public int SideTieQuantity { get; set; } = 3;
        public double SideTieLapLengthMm { get; set; } = 600.0;

        // Visualization & View Settings
        public bool ViewAsSolid3D { get; set; } = true;
        public bool ViewUnobscured { get; set; } = true;

        // Target Scope
        public bool UseSelectedOnly { get; set; } = true;

        // Conversions to feet
        public static double MmToFeet(double mm) => mm / 304.8;
        public double CoverBottomFeet => MmToFeet(CoverBottomMm);
        public double CoverTopFeet => MmToFeet(CoverTopMm);
        public double CoverSideFeet => MmToFeet(CoverSideMm);

        public double BottomSpacingXFeet => MmToFeet(BottomSpacingXMm);
        public double BottomSpacingYFeet => MmToFeet(BottomSpacingYMm);
        public double TopSpacingXFeet => MmToFeet(TopSpacingXMm);
        public double TopSpacingYFeet => MmToFeet(TopSpacingYMm);

        public double BottomHookXFeet => MmToFeet(BottomHookXMm);
        public double BottomHookYFeet => MmToFeet(BottomHookYMm);
        public double TopHookXFeet => MmToFeet(TopHookXMm);
        public double TopHookYFeet => MmToFeet(TopHookYMm);

        public double SideTieLapLengthFeet => MmToFeet(SideTieLapLengthMm);
    }
}
