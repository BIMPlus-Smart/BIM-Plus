using System;

namespace AutoPileCap.Models
{
    public class PileCapConfig
    {
        // Pile Count: 1, 2, 3, 4, 5, 6, 8, 9
        public int PileCount { get; set; } = 4;

        // Metric Dimensions in Millimeters
        public double PileDiameterMm { get; set; } = 400.0;
        public double SpacingMultiplier { get; set; } = 3.0; // Standard 3*D spacing
        public bool UseCustomSpacing { get; set; } = false;
        public double CustomSpacingMm { get; set; } = 1200.0;
        public double EdgeDistanceMm { get; set; } = 250.0; // Overhang from pile edge
        public double CapThicknessMm { get; set; } = 800.0;
        public double PileLengthMm { get; set; } = 12000.0; // 12 meters
        public double LevelOffsetMm { get; set; } = 0.0;

        // Options
        public bool CreatePilesUnderCap { get; set; } = true;
        public bool AlignWithColumnRotation { get; set; } = true;

        // Revit Element References (Ids)
        public long SelectedCapSymbolId { get; set; } = -1;
        public long SelectedPileSymbolId { get; set; } = -1;
        public long SelectedLevelId { get; set; } = -1;

        // Calculated Spacing in Millimeters
        public double EffectiveSpacingMm => UseCustomSpacing ? CustomSpacingMm : (PileDiameterMm * SpacingMultiplier);

        // Conversion helper from Millimeters to Revit Decimal Feet
        public static double MmToFeet(double mm) => mm / 304.8;
        public static double FeetToMm(double feet) => feet * 304.8;

        public double EffectiveSpacingFeet => MmToFeet(EffectiveSpacingMm);
        public double PileDiameterFeet => MmToFeet(PileDiameterMm);
        public double EdgeDistanceFeet => MmToFeet(EdgeDistanceMm);
        public double CapThicknessFeet => MmToFeet(CapThicknessMm);
        public double PileLengthFeet => MmToFeet(PileLengthMm);
        public double LevelOffsetFeet => MmToFeet(LevelOffsetMm);
    }
}
