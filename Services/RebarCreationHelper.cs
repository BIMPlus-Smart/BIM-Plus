using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace AutoPileCap.Services
{
    /// <summary>
    /// Helper to handle Rebar creation across Revit 2026 and Revit 2027 API differences.
    /// In Revit 2027, BarTerminationsData and RebarTerminationOrientation replaced RebarHookOrientation parameters.
    /// </summary>
    public static class RebarCreationHelper
    {
        public static Rebar CreateFromCurves(
            Document doc,
            RebarStyle style,
            RebarBarType barType,
            RebarHookType? startHook,
            RebarHookType? endHook,
            Element host,
            XYZ norm,
            IList<Curve> curves,
            bool hookStartRight = true,
            bool hookEndRight = true,
            bool useExistingShapeIfPossible = true,
            bool createNewShape = true)
        {
#if REVIT2027
            using var termData = new BarTerminationsData(doc);
            if (startHook != null) termData.HookTypeIdAtStart = startHook.Id;
            if (endHook != null) termData.HookTypeIdAtEnd = endHook.Id;
            termData.TerminationOrientationAtStart = hookStartRight ? RebarTerminationOrientation.Right : RebarTerminationOrientation.Left;
            termData.TerminationOrientationAtEnd = hookEndRight ? RebarTerminationOrientation.Right : RebarTerminationOrientation.Left;
            return Rebar.CreateFromCurves(doc, style, barType, host, norm, curves, termData, useExistingShapeIfPossible, createNewShape);
#else
            var orientStart = hookStartRight ? RebarHookOrientation.Right : RebarHookOrientation.Left;
            var orientEnd = hookEndRight ? RebarHookOrientation.Right : RebarHookOrientation.Left;
            return Rebar.CreateFromCurves(doc, style, barType, startHook, endHook, host, norm, curves, orientStart, orientEnd, useExistingShapeIfPossible, createNewShape);
#endif
        }

        public static Rebar CreateFromCurvesAndShape(
            Document doc,
            RebarShape shape,
            RebarBarType barType,
            RebarHookType? startHook,
            RebarHookType? endHook,
            Element host,
            XYZ norm,
            IList<Curve> curves,
            bool hookStartRight = true,
            bool hookEndRight = true)
        {
#if REVIT2027
            using var termData = new BarTerminationsData(doc);
            if (startHook != null) termData.HookTypeIdAtStart = startHook.Id;
            if (endHook != null) termData.HookTypeIdAtEnd = endHook.Id;
            termData.TerminationOrientationAtStart = hookStartRight ? RebarTerminationOrientation.Right : RebarTerminationOrientation.Left;
            termData.TerminationOrientationAtEnd = hookEndRight ? RebarTerminationOrientation.Right : RebarTerminationOrientation.Left;
            return Rebar.CreateFromCurvesAndShape(doc, shape, barType, host, norm, curves, termData);
#else
            var orientStart = hookStartRight ? RebarHookOrientation.Right : RebarHookOrientation.Left;
            var orientEnd = hookEndRight ? RebarHookOrientation.Right : RebarHookOrientation.Left;
            return Rebar.CreateFromCurvesAndShape(doc, shape, barType, startHook, endHook, host, norm, curves, orientStart, orientEnd);
#endif
        }
    }
}
