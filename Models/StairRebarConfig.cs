using System;

namespace AutoPileCap.Models
{
    public class StairRebarConfig
    {
        // 1) Concrete Covers (mm)
        public double CoverWaistMm { get; set; } = 30.0;
        public double CoverTopMm { get; set; } = 30.0;
        public double CoverSideMm { get; set; } = 30.0;
        public double CoverLandingMm { get; set; } = 30.0;

        // 2) Dimensions (mm)
        public double WaistThicknessMm { get; set; } = 160.0;

        // 3) Bottom Longitudinal Reinforcement (Waist tension bars)
        public bool EnableBottomRebar { get; set; } = true;
        public long BottomBarTypeId { get; set; } = -1;
        public double BottomSpacingMm { get; set; } = 150.0;
        public double BottomAnchorBotMm { get; set; } = 500.0;  // Lower slab/landing anchorage
        public double BottomAnchorTopMm { get; set; } = 500.0;  // Upper landing/beam anchorage
        public bool BottomHookBot { get; set; } = true;         // 90° hook upward at lower end
        public bool BottomHookTop { get; set; } = false;        // 90° hook downward at upper end

        // 4) Top Longitudinal Reinforcement (Scissor Bar Detail & Negative moment)
        public bool EnableTopRebar { get; set; } = true;
        public long TopBarTypeId { get; set; } = -1;
        public double TopSpacingMm { get; set; } = 150.0;
        public double TopScissorLapMm { get; set; } = 650.0;   // Scissor crank lap length into lower slab
        public double TopAnchorTopMm { get; set; } = 600.0;    // Upper landing extension
        public bool TopHookTop { get; set; } = true;           // 90° hook downward into upper landing

        // 5) Transverse / Distribution Reinforcement
        public bool EnableDistRebar { get; set; } = true;
        public long DistBarTypeId { get; set; } = -1;
        public double DistSpacingMm { get; set; } = 200.0;
        public double DistLandingSpacingMm { get; set; } = 200.0;
        public bool EnableTopDistRebar { get; set; } = true;   // Transverse bars at top mat as well

        // 6) Landing Reinforcement
        public bool EnableLandingRebar { get; set; } = true;
        public long LandingBarTypeId { get; set; } = -1;
        public double LandingSpacingMm { get; set; } = 150.0;

        // 7) Identification & Partition
        public string PartitionName { get; set; } = "Stair";

        // 8) Visualization & View Settings
        public bool ViewAsSolid3D { get; set; } = true;
        public bool ViewUnobscured { get; set; } = true;

        // Target Scope
        public bool UseSelectedOnly { get; set; } = true;

        // Conversions to feet
        public static double MmToFeet(double mm) => mm / 304.8;
        public double CoverWaistFeet => MmToFeet(CoverWaistMm);
        public double CoverTopFeet => MmToFeet(CoverTopMm);
        public double CoverSideFeet => MmToFeet(CoverSideMm);
        public double CoverLandingFeet => MmToFeet(CoverLandingMm);
        public double WaistThicknessFeet => MmToFeet(WaistThicknessMm);

        public double BottomSpacingFeet => MmToFeet(BottomSpacingMm);
        public double BottomAnchorBotFeet => MmToFeet(BottomAnchorBotMm);
        public double BottomAnchorTopFeet => MmToFeet(BottomAnchorTopMm);

        public double TopSpacingFeet => MmToFeet(TopSpacingMm);
        public double TopScissorLapFeet => MmToFeet(TopScissorLapMm);
        public double TopAnchorTopFeet => MmToFeet(TopAnchorTopMm);

        public double DistSpacingFeet => MmToFeet(DistSpacingMm);
        public double DistLandingSpacingFeet => MmToFeet(DistLandingSpacingMm);
        public double LandingSpacingFeet => MmToFeet(LandingSpacingMm);
    }
}
