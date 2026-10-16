using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AutoPileCap.Models;
using AutoPileCap.Services;

namespace AutoPileCap.UI
{
    public partial class StairRebarWindow : Window
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private readonly RebarGenerationService _rebarService;
        private readonly RevitElementService _elemService;
        private List<Element> _preselectedStairs;
        private AutoPileCap.Handlers.GenericEventHandler _handler;
        private ExternalEvent _exEvent;

        // View mode: SECTION, 3D, TOP, BOT
        private string _currentViewMode = "SECTION";

        // Stair dimensions (mm)
        private double _flightWidthMm = 1000;
        private double _flightHeightMm = 1500;
        private double _riserHeightMm = 170;
        private double _treadDepthMm = 280;

        // Covers (mm)
        private double _coverWaistMm = 30.0;
        private double _coverTopMm = 30.0;
        private double _coverSideMm = 30.0;

        // Canvas pan & zoom state
        private bool _isPanning = false;
        private System.Windows.Point _panStartPoint;

        // 3D Orbit state
        private bool _isOrbiting = false;
        private System.Windows.Point _orbitStartPoint;
        private double _viewAngleZ = -Math.PI / 4.0;
        private double _viewAngleX = Math.PI / 6.0;

        private bool _isInitialized = false;

        public StairRebarWindow(UIApplication uiapp, AutoPileCap.Handlers.GenericEventHandler handler, ExternalEvent exEvent, List<Element>? initialStairs = null)
        {
            InitializeComponent();

            _handler = handler;
            _exEvent = exEvent;

            _uidoc = uiapp.ActiveUIDocument;
            _doc = _uidoc.Document;
            _rebarService = new RebarGenerationService(_doc);
            _elemService = new RevitElementService(_doc);

            if (initialStairs != null && initialStairs.Count > 0)
            {
                _preselectedStairs = initialStairs;
            }
            else
            {
                var selectedIds = _uidoc.Selection.GetElementIds();
                _preselectedStairs = _elemService.GetStairs(selectedIds).ToList();
            }

            Loaded += StairRebarWindow_Loaded;
        }

        private void StairRebarWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PopulateRebarBarTypes();
            DetectSelectedStair();
            _isInitialized = true;
            RenderPreview();
        }

        private void DetectSelectedStair()
        {
            if (_preselectedStairs.Count > 0)
            {
                var elem = _preselectedStairs[0];
                if (elem is Autodesk.Revit.DB.Architecture.Stairs stairs)
                {
                    var runIds = stairs.GetStairsRuns();
                    if (runIds != null && runIds.Count > 0)
                    {
                        var run = _doc.GetElement(runIds.First()) as Autodesk.Revit.DB.Architecture.StairsRun;
                        if (run != null)
                        {
                            _flightWidthMm = Math.Max(500, run.ActualRunWidth * GeometryHelper.MmPerFoot);
                            _flightHeightMm = Math.Max(500, run.Height * GeometryHelper.MmPerFoot);
                        }
                    }
                }
                else if (elem is Autodesk.Revit.DB.Architecture.StairsRun run)
                {
                    _flightWidthMm = Math.Max(500, run.ActualRunWidth * GeometryHelper.MmPerFoot);
                    _flightHeightMm = Math.Max(500, run.Height * GeometryHelper.MmPerFoot);
                }
                else
                {
                    BoundingBoxXYZ bbox = elem.get_BoundingBox(null);
                    if (bbox != null)
                    {
                        _flightWidthMm = Math.Max(500, (bbox.Max.X - bbox.Min.X) * GeometryHelper.MmPerFoot);
                        _flightHeightMm = Math.Max(500, (bbox.Max.Z - bbox.Min.Z) * GeometryHelper.MmPerFoot);
                    }
                }

                _coverWaistMm = GetCoverMm(elem, BuiltInParameter.CLEAR_COVER_BOTTOM, 30.0);
                _coverTopMm = GetCoverMm(elem, BuiltInParameter.CLEAR_COVER_TOP, 30.0);
                _coverSideMm = GetCoverMm(elem, BuiltInParameter.CLEAR_COVER_OTHER, 30.0);

                TxtCoverWaist.Text = _coverWaistMm.ToString("F0");
                TxtCoverTop.Text = _coverTopMm.ToString("F0");
                TxtCoverSide.Text = _coverSideMm.ToString("F0");

                TxtStairName.Text = $"{elem.Name} [W:{_flightWidthMm:F0}mm, H:{_flightHeightMm:F0}mm]";
            }
            else
            {
                TxtStairName.Text = "None (Click Pick Stair)";
            }
        }

        private double GetCoverMm(Element element, BuiltInParameter paramId, double defaultMm)
        {
            Parameter param = element.get_Parameter(paramId);
            if (param != null && param.HasValue)
            {
                ElementId coverTypeId = param.AsElementId();
                if (coverTypeId != ElementId.InvalidElementId)
                {
                    RebarCoverType coverType = element.Document.GetElement(coverTypeId) as RebarCoverType;
                    if (coverType != null)
                    {
                        return coverType.CoverDistance * GeometryHelper.MmPerFoot;
                    }
                }
            }
            return defaultMm;
        }

        private void PopulateRebarBarTypes()
        {
            var barTypes = _rebarService.GetRebarBarTypes();
            if (barTypes.Count == 0)
            {
                TxtStatus.Text = "⚠️ Warning: No Rebar Bar Types loaded in this Revit project.";
                TxtStatus.Visibility = System.Windows.Visibility.Visible;
                return;
            }

            CmbBotType.Items.Clear();
            CmbTopType.Items.Clear();
            CmbDistType.Items.Clear();

            int defaultBotIdx = 0;
            int defaultTopIdx = 0;
            int defaultDistIdx = 0;

            for (int i = 0; i < barTypes.Count; i++)
            {
                var bar = barTypes[i];
                double diaMm = bar.BarNominalDiameter * GeometryHelper.MmPerFoot;
                string label = $"{bar.Name} (Ø{diaMm:F0}mm)";

                var itemBot = new ComboBoxItem { Content = label, Tag = bar.Id.Value };
                var itemTop = new ComboBoxItem { Content = label, Tag = bar.Id.Value };
                var itemDist = new ComboBoxItem { Content = label, Tag = bar.Id.Value };

                CmbBotType.Items.Add(itemBot);
                CmbTopType.Items.Add(itemTop);
                CmbDistType.Items.Add(itemDist);

                if (Math.Abs(diaMm - 12) < 1.5) { defaultBotIdx = i; defaultTopIdx = i; }
                if (Math.Abs(diaMm - 10) < 1.5) { defaultDistIdx = i; }
            }

            CmbBotType.SelectedIndex = defaultBotIdx;
            CmbTopType.SelectedIndex = defaultTopIdx;
            CmbDistType.SelectedIndex = defaultDistIdx;
        }

        private void Inputs_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitialized)
            {
                RenderPreview();
            }
        }

        private void BtnSelectStair_Click(object sender, RoutedEventArgs e)
        {
            _handler.Action = app =>
            {
                try
                {
                    var uidoc = app.ActiveUIDocument;
                    var doc = uidoc.Document;
                    var filter = new StairSelectionFilter();
                    var refs = uidoc.Selection.PickObjects(
                        ObjectType.Element,
                        filter,
                        "Select Stair or Stair Run, then click Finish (or press ESC when done)");

                    var targetStairs = refs
                        .Select(r => doc.GetElement(r))
                        .Where(el => el != null)
                        .ToList();

                    Dispatcher.Invoke(() =>
                    {
                        if (targetStairs.Count > 0)
                        {
                            _preselectedStairs = targetStairs;
                            DetectSelectedStair();
                            RenderPreview();
                        }
                    });
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // User pressed ESC
                }
            };
            _exEvent.Raise();
        }

        #region View Mode Switching
        private void BtnViewMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                _currentViewMode = btn.Tag.ToString() ?? "SECTION";
                UpdateViewModeButtons();
                ResetZoomAndPan();
                RenderPreview();
            }
        }

        private void UpdateViewModeButtons()
        {
            var buttons = new[] { BtnViewSection, BtnView3D, BtnViewTop, BtnViewBot, NavViewSection, NavView3D, NavViewTop, NavViewBot };
            var activeBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(13, 110, 253)); // #0D6EFD
            var activeFg = System.Windows.Media.Brushes.White;
            var normalBg = System.Windows.Media.Brushes.Transparent;
            var normalFg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)); // #64748B

            foreach (var b in buttons)
            {
                if (b == null) continue;
                bool isActive = (b.Tag?.ToString() ?? "") == _currentViewMode;
                b.Background = isActive ? activeBg : normalBg;
                b.Foreground = isActive ? activeFg : normalFg;
            }
        }
        #endregion

        #region Pan & Zoom Canvas Interactions
        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 1.15 : 0.87;
            System.Windows.Point mousePos = e.GetPosition(CanvasBorder);

            double newScaleX = CanvasScale.ScaleX * zoomFactor;
            double newScaleY = CanvasScale.ScaleY * zoomFactor;

            if (newScaleX < 0.1 || newScaleX > 15.0) return;

            CanvasTranslate.X = mousePos.X - (mousePos.X - CanvasTranslate.X) * zoomFactor;
            CanvasTranslate.Y = mousePos.Y - (mousePos.Y - CanvasTranslate.Y) * zoomFactor;
            CanvasScale.ScaleX = newScaleX;
            CanvasScale.ScaleY = newScaleY;
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (_currentViewMode == "3D")
                {
                    _isOrbiting = true;
                    _orbitStartPoint = e.GetPosition(CanvasBorder);
                }
                else
                {
                    _isPanning = true;
                    _panStartPoint = e.GetPosition(CanvasBorder);
                }
                CanvasBorder.CaptureMouse();
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                System.Windows.Point currentPoint = e.GetPosition(CanvasBorder);
                CanvasTranslate.X += (currentPoint.X - _panStartPoint.X);
                CanvasTranslate.Y += (currentPoint.Y - _panStartPoint.Y);
                _panStartPoint = currentPoint;
            }
            else if (_isOrbiting)
            {
                System.Windows.Point currentPoint = e.GetPosition(CanvasBorder);
                double deltaX = currentPoint.X - _orbitStartPoint.X;
                double deltaY = currentPoint.Y - _orbitStartPoint.Y;

                _viewAngleZ += deltaX * 0.01;
                _viewAngleX += deltaY * 0.01;

                _viewAngleX = Math.Max(0.05, Math.Min(Math.PI / 2.2, _viewAngleX));

                _orbitStartPoint = currentPoint;
                RenderPreview();
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _isPanning = false;
                _isOrbiting = false;
                CanvasBorder.ReleaseMouseCapture();
            }
        }

        private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ResetZoomAndPan();
            RenderPreview();
        }

        private void CanvasBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isInitialized) RenderPreview();
        }

        private void ResetZoomAndPan()
        {
            CanvasScale.ScaleX = 1.0;
            CanvasScale.ScaleY = 1.0;
            CanvasTranslate.X = 0.0;
            CanvasTranslate.Y = 0.0;
        }
        #endregion

        #region Preview Rendering
        private void RenderPreview()
        {
            if (PreviewCanvas == null || !_isInitialized) return;
            PreviewCanvas.Children.Clear();

            // Read form values
            double cWaist = ParseDouble(TxtCoverWaist.Text, 30.0);
            double cTop = ParseDouble(TxtCoverTop.Text, 30.0);
            double cSide = ParseDouble(TxtCoverSide.Text, 30.0);
            double tWaist = ParseDouble(TxtWaistThick.Text, 160.0);

            double botSpacing = ParseDouble(TxtBotSpacing.Text, 150.0);
            double botAncBot = ParseDouble(TxtBotAnchorBot.Text, 500.0);
            double botAncTop = ParseDouble(TxtBotAnchorTop.Text, 500.0);
            bool botHook = ChkBotHook.IsChecked == true;
            bool enableBot = ChkEnableBot.IsChecked == true;

            double topSpacing = ParseDouble(TxtTopSpacing.Text, 150.0);
            double topScissorLap = ParseDouble(TxtTopScissorLap.Text, 650.0);
            double topAncTop = ParseDouble(TxtTopAnchorTop.Text, 600.0);
            bool topHook = ChkTopHook.IsChecked == true;
            bool enableTop = ChkEnableTop.IsChecked == true;

            double distSpacing = ParseDouble(TxtDistSpacing.Text, 200.0);
            bool enableDist = ChkEnableDist.IsChecked == true;
            bool enableTopDist = ChkTopDist.IsChecked == true;

            switch (_currentViewMode)
            {
                case "SECTION":
                    DrawSectionView(cWaist, cTop, tWaist, botAncBot, botAncTop, botHook, enableBot, topScissorLap, topAncTop, topHook, enableTop, distSpacing, enableDist, enableTopDist);
                    break;
                case "3D":
                    Draw3DView(cWaist, cTop, cSide, tWaist, botSpacing, botAncBot, botAncTop, botHook, enableBot, topSpacing, topScissorLap, topAncTop, topHook, enableTop, distSpacing, enableDist);
                    break;
                case "TOP":
                    DrawPlanView(cSide, botSpacing, topSpacing, distSpacing, enableBot, enableTop, enableDist);
                    break;
                case "BOT":
                    DrawBotView(cSide, botSpacing, distSpacing, enableBot, enableDist);
                    break;
                default:
                    DrawSectionView(cWaist, cTop, tWaist, botAncBot, botAncTop, botHook, enableBot, topScissorLap, topAncTop, topHook, enableTop, distSpacing, enableDist, enableTopDist);
                    break;
            }

            // Summary Texts
            string botName = CmbBotType.SelectedItem is ComboBoxItem cbb ? cbb.Content.ToString() : "T12";
            string topName = CmbTopType.SelectedItem is ComboBoxItem cbt ? cbt.Content.ToString() : "T12";
            string distName = CmbDistType.SelectedItem is ComboBoxItem cbd ? cbd.Content.ToString() : "T10";

            TxtSumBot.Text = $"{botName} @{botSpacing:F0}mm (Anchor {botAncBot:F0}mm)";
            TxtSumTop.Text = $"{topName} @{topSpacing:F0}mm (Lap {topScissorLap:F0}mm)";
            TxtSumDist.Text = $"{distName} @{distSpacing:F0}mm";

            TxtDrawingInfo.Text = $"Waist: {tWaist:F0}mm | Width: {_flightWidthMm:F0}mm | Covers: {cWaist:F0}mm";
        }

        #region Section View Rendering
        private void DrawSectionView(
            double cWaist, double cTop, double tWaist,
            double botAncBot, double botAncTop, bool botHook, bool enableBot,
            double topScissorLap, double topAncTop, bool topHook, bool enableTop,
            double distSpacing, bool enableDist, bool enableTopDist)
        {
            double canvasW = CanvasBorder.ActualWidth > 300 ? CanvasBorder.ActualWidth : 800;
            double canvasH = CanvasBorder.ActualHeight > 300 ? CanvasBorder.ActualHeight : 600;

            int numSteps = 8;
            double riserH = _riserHeightMm;
            double treadD = _treadDepthMm;
            double landingL = 400; // Display length of landings in mm

            double totalFlightL = numSteps * treadD;
            double totalFlightH = numSteps * riserH;
            double totalWidthMm = landingL + totalFlightL + landingL;
            double totalHeightMm = totalFlightH + tWaist + 200;

            double scale = Math.Min((canvasW - 140) / totalWidthMm, (canvasH - 140) / totalHeightMm);
            scale = Math.Max(0.12, Math.Min(1.0, scale));

            double originX = (canvasW - totalWidthMm * scale) / 2.0;
            double originY = (canvasH + totalHeightMm * scale) / 2.0 - 60;

            // Coordinate conversion: X right, Y up in real space -> Canvas X right, Canvas Y down
            System.Windows.Point ToCanvas(double xMm, double yMm)
            {
                return new System.Windows.Point(originX + xMm * scale, originY - yMm * scale);
            }

            // 1. Draw Concrete Flight Polygon
            var concretePts = new List<System.Windows.Point>();

            // Lower landing bottom
            concretePts.Add(ToCanvas(0, 0));
            // Lower landing top
            concretePts.Add(ToCanvas(0, tWaist));
            concretePts.Add(ToCanvas(landingL, tWaist));

            // Steps profile
            double curX = landingL;
            double curY = tWaist;
            for (int s = 0; s < numSteps; s++)
            {
                curY += riserH;
                concretePts.Add(ToCanvas(curX, curY)); // Riser
                curX += treadD;
                concretePts.Add(ToCanvas(curX, curY)); // Tread
            }

            // Upper landing top
            concretePts.Add(ToCanvas(curX + landingL, curY));
            // Upper landing bottom
            concretePts.Add(ToCanvas(curX + landingL, curY - tWaist));

            // Top kink bottom
            double topKinkX = curX;
            double topKinkY = curY - tWaist;
            concretePts.Add(ToCanvas(topKinkX, topKinkY));

            // Bottom kink bottom
            double botKinkX = landingL;
            double botKinkY = 0;
            concretePts.Add(ToCanvas(botKinkX, botKinkY));

            // Draw concrete fill & stroke
            var poly = new Polygon
            {
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(28, 14, 165, 233)), // Soft translucent cyan
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)), // #94A3B8
                StrokeThickness = 2.0
            };
            foreach (var p in concretePts) poly.Points.Add(p);
            PreviewCanvas.Children.Add(poly);

            // Step riser guidelines / labels
            double angle = Math.Atan2(totalFlightH, totalFlightL);
            double cosA = Math.Cos(angle);
            double sinA = Math.Sin(angle);

            // 2. Draw Bottom Reinforcement (Blue #0D6EFD)
            if (enableBot)
            {
                double dBot = cWaist + 6;
                double botLineY = dBot;

                var botRebarPts = new List<System.Windows.Point>();

                double startAncX = Math.Max(20, landingL - botAncBot);
                if (botHook)
                {
                    botRebarPts.Add(ToCanvas(startAncX, botLineY + 120));
                }
                botRebarPts.Add(ToCanvas(startAncX, botLineY));

                // Bottom kink intersection
                double miter = dBot * Math.Tan(angle * 0.5);
                double kinkBotX = landingL + miter;
                double kinkBotY = botLineY;
                botRebarPts.Add(ToCanvas(kinkBotX, kinkBotY));

                // Top kink intersection
                double kinkTopX = curX - miter;
                double kinkTopY = curY - tWaist + dBot;
                botRebarPts.Add(ToCanvas(kinkTopX, kinkTopY));

                // Upper landing extension
                double endAncX = Math.Min(curX + landingL - 20, curX + botAncTop);
                botRebarPts.Add(ToCanvas(endAncX, kinkTopY));

                DrawPolyline(botRebarPts, CreateBrush("#0D6EFD"), 3.0);

                // Label
                var labelPt = ToCanvas((kinkBotX + kinkTopX) / 2.0 - 50, (kinkBotY + kinkTopY) / 2.0 - 30);
                DrawBadge(labelPt.X, labelPt.Y, "Bottom Bar: Main Waist (T12@150)", "#0D6EFD");
            }

            // 3. Draw Top Reinforcement & Scissor Detail (Amber #F59E0B)
            if (enableTop)
            {
                var topRebarPts = new List<System.Windows.Point>();

                // Scissor crank detail: starts in bottom landing
                double scissorStartX = Math.Max(20, landingL - topScissorLap);
                double scissorY = cWaist + 6; // Along lower landing soffit cover

                topRebarPts.Add(ToCanvas(scissorStartX, scissorY));
                topRebarPts.Add(ToCanvas(landingL, scissorY));

                // Inclined waist top line
                double topWaistOff = tWaist - cTop - 6;
                double kinkTopX = curX;
                double kinkTopY = curY - cTop - 6;

                topRebarPts.Add(ToCanvas(kinkTopX, kinkTopY));

                // Upper landing extension
                double topEndX = Math.Min(curX + landingL - 20, curX + topAncTop);
                topRebarPts.Add(ToCanvas(topEndX, kinkTopY));

                if (topHook)
                {
                    topRebarPts.Add(ToCanvas(topEndX, kinkTopY - 100));
                }

                DrawPolyline(topRebarPts, CreateBrush("#F59E0B"), 3.0);

                // Scissor detail badge
                var scissorPt = ToCanvas(landingL - 40, scissorY + 30);
                DrawBadge(scissorPt.X, scissorPt.Y, "Scissor Detail Lap (650mm)", "#F59E0B");
            }

            // 4. Draw Transverse / Distribution Circles (Emerald #10B981)
            if (enableDist)
            {
                double distStep = distSpacing;
                double lSlopeMm = Math.Sqrt(totalFlightL * totalFlightL + totalFlightH * totalFlightH);
                int count = (int)Math.Max(3, Math.Floor(lSlopeMm / distStep));

                double slopeDx = totalFlightL / count;
                double slopeDy = totalFlightH / count;

                // Bottom distribution bars (circles resting above bottom bars)
                for (int i = 0; i <= count; i++)
                {
                    double bx = landingL + i * slopeDx;
                    double by = cWaist + 16 + i * slopeDy;
                    var pt = ToCanvas(bx, by);
                    DrawCircle(pt.X, pt.Y, 4.0, CreateBrush("#10B981"), Brushes.White);
                }

                // Top distribution bars
                if (enableTopDist)
                {
                    for (int i = 0; i <= count; i++)
                    {
                        double tx = landingL + i * slopeDx;
                        double ty = tWaist - cTop - 16 + i * slopeDy;
                        var pt = ToCanvas(tx, ty);
                        DrawCircle(pt.X, pt.Y, 4.0, CreateBrush("#10B981"), Brushes.White);
                    }
                }
            }

            // Section View Title & Annotations
            DrawText(originX + 10, originY - totalHeightMm * scale - 20, "STAIR FLIGHT LONGITUDINAL SECTION", 14, FontWeights.Bold, CreateBrush("#0F172A"));
            DrawText(originX + 10, originY - totalHeightMm * scale, "Standard ACI 318 / BS 8110 Detail with Scissor Joint", 11, FontWeights.Normal, CreateBrush("#64748B"));
        }
        #endregion

        #region 3D Axonometric View Rendering
        private void Draw3DView(
            double cWaist, double cTop, double cSide, double tWaist,
            double botSpacing, double botAncBot, double botAncTop, bool botHook, bool enableBot,
            double topSpacing, double topScissorLap, double topAncTop, bool topHook, bool enableTop,
            double distSpacing, bool enableDist)
        {
            double canvasW = CanvasBorder.ActualWidth > 300 ? CanvasBorder.ActualWidth : 800;
            double canvasH = CanvasBorder.ActualHeight > 300 ? CanvasBorder.ActualHeight : 600;

            double cx = canvasW / 2.0;
            double cy = canvasH / 2.0 + 40;

            double flightW = _flightWidthMm;
            double numSteps = 7;
            double riserH = _riserHeightMm;
            double treadD = _treadDepthMm;
            double landingL = 350;

            double flightL = numSteps * treadD;
            double flightH = numSteps * riserH;

            double scale3D = Math.Min(canvasW / 2200.0, canvasH / 2200.0) * 1.5;

            // 3D coordinate transformation: Azimuth (_viewAngleZ) and Elevation (_viewAngleX)
            System.Windows.Point Project3D(double x, double y, double z)
            {
                // Centering
                double cx0 = x - (flightL + 2 * landingL) * 0.5;
                double cy0 = y - flightW * 0.5;
                double cz0 = z - (flightH + tWaist) * 0.5;

                // Rotate around Z (Azimuth)
                double x1 = cx0 * Math.Cos(_viewAngleZ) - cy0 * Math.Sin(_viewAngleZ);
                double y1 = cx0 * Math.Sin(_viewAngleZ) + cy0 * Math.Cos(_viewAngleZ);

                // Rotate around X (Elevation)
                double y2 = y1 * Math.Cos(_viewAngleX) - cz0 * Math.Sin(_viewAngleX);
                double z2 = y1 * Math.Sin(_viewAngleX) + cz0 * Math.Cos(_viewAngleX);

                return new System.Windows.Point(cx + x1 * scale3D, cy - z2 * scale3D);
            }

            // 1. Draw Translucent Concrete Stair Flight Volume
            // Front Profile (Y = 0) and Back Profile (Y = flightW)
            var profileFront = new List<System.Windows.Point>();
            var profileBack = new List<System.Windows.Point>();

            // Points on Y=0
            var pts3D = new List<Point3D>();
            pts3D.Add(new Point3D(0, 0, 0));
            pts3D.Add(new Point3D(0, 0, tWaist));
            pts3D.Add(new Point3D(landingL, 0, tWaist));

            double curX = landingL;
            double curZ = tWaist;
            for (int s = 0; s < numSteps; s++)
            {
                curZ += riserH;
                pts3D.Add(new Point3D(curX, 0, curZ));
                curX += treadD;
                pts3D.Add(new Point3D(curX, 0, curZ));
            }
            pts3D.Add(new Point3D(curX + landingL, 0, curZ));
            pts3D.Add(new Point3D(curX + landingL, 0, curZ - tWaist));
            pts3D.Add(new Point3D(curX, 0, curZ - tWaist));
            pts3D.Add(new Point3D(landingL, 0, 0));

            // Draw Back Face
            var backPoly = new Polygon
            {
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(18, 14, 165, 233)),
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(90, 148, 163, 184)),
                StrokeThickness = 1.0
            };
            foreach (var p in pts3D) backPoly.Points.Add(Project3D(p.X, flightW, p.Z));
            PreviewCanvas.Children.Add(backPoly);

            // Connect steps between Y=0 and Y=flightW
            for (int i = 0; i < pts3D.Count; i++)
            {
                var pA = pts3D[i];
                var pB = pts3D[(i + 1) % pts3D.Count];

                var stepPoly = new Polygon
                {
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(24, 203, 213, 225)),
                    Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 148, 163, 184)),
                    StrokeThickness = 1.0
                };
                stepPoly.Points.Add(Project3D(pA.X, 0, pA.Z));
                stepPoly.Points.Add(Project3D(pB.X, 0, pB.Z));
                stepPoly.Points.Add(Project3D(pB.X, flightW, pB.Z));
                stepPoly.Points.Add(Project3D(pA.X, flightW, pA.Z));
                PreviewCanvas.Children.Add(stepPoly);
            }

            // Draw Front Face
            var frontPoly = new Polygon
            {
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 14, 165, 233)),
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)),
                StrokeThickness = 1.5
            };
            foreach (var p in pts3D) frontPoly.Points.Add(Project3D(p.X, 0, p.Z));
            PreviewCanvas.Children.Add(frontPoly);

            // 2. Draw 3D Bottom Reinforcement Cage (Blue)
            if (enableBot)
            {
                int numBars = Math.Max(2, (int)Math.Floor((flightW - 2 * cSide) / botSpacing) + 1);
                double actualSpacing = (flightW - 2 * cSide) / Math.Max(1, numBars - 1);

                double dBot = cWaist + 6;
                double startAncX = Math.Max(20, landingL - botAncBot);
                double endAncX = Math.Min(curX + landingL - 20, curX + botAncTop);

                for (int b = 0; b < numBars; b++)
                {
                    double y = cSide + b * actualSpacing;
                    var pts = new List<System.Windows.Point>();

                    if (botHook) pts.Add(Project3D(startAncX, y, dBot + 100));
                    pts.Add(Project3D(startAncX, y, dBot));
                    pts.Add(Project3D(landingL, y, dBot));
                    pts.Add(Project3D(curX, y, curZ - tWaist + dBot));
                    pts.Add(Project3D(endAncX, y, curZ - tWaist + dBot));

                    DrawPolyline(pts, CreateBrush("#0D6EFD"), 2.0);
                }
            }

            // 3. Draw 3D Top Reinforcement & Scissor Detail (Amber)
            if (enableTop)
            {
                int numBars = Math.Max(2, (int)Math.Floor((flightW - 2 * cSide) / topSpacing) + 1);
                double actualSpacing = (flightW - 2 * cSide) / Math.Max(1, numBars - 1);

                double scissorStartX = Math.Max(20, landingL - topScissorLap);
                double topEndX = Math.Min(curX + landingL - 20, curX + topAncTop);
                double topZ = curZ - cTop - 6;

                for (int b = 0; b < numBars; b++)
                {
                    double y = cSide + b * actualSpacing;
                    var pts = new List<System.Windows.Point>();

                    pts.Add(Project3D(scissorStartX, y, cWaist + 6));
                    pts.Add(Project3D(landingL, y, cWaist + 6));
                    pts.Add(Project3D(curX, y, topZ));
                    pts.Add(Project3D(topEndX, y, topZ));
                    if (topHook) pts.Add(Project3D(topEndX, y, topZ - 100));

                    DrawPolyline(pts, CreateBrush("#F59E0B"), 2.0);
                }
            }

            // 4. Draw 3D Transverse Distribution Bars (Emerald)
            if (enableDist)
            {
                int numDist = Math.Max(3, (int)Math.Floor(flightL / distSpacing));
                double stepX = flightL / numDist;
                double stepZ = flightH / numDist;

                for (int d = 0; d <= numDist; d++)
                {
                    double x = landingL + d * stepX;
                    double z = cWaist + 16 + d * stepZ;

                    var p1 = Project3D(x, cSide, z);
                    var p2 = Project3D(x, flightW - cSide, z);
                    DrawLine(p1.X, p1.Y, p2.X, p2.Y, CreateBrush("#10B981"), 2.0);
                }
            }

            // 3D Orbit Tip
            DrawText(20, 20, "🖱 Drag with Left Mouse Button to Orbit in 3D | Mouse Wheel to Zoom", 11, FontWeights.Normal, CreateBrush("#64748B"));
        }

        private struct Point3D
        {
            public double X, Y, Z;
            public Point3D(double x, double y, double z) { X = x; Y = y; Z = z; }
        }
        #endregion

        #region Plan View Rendering
        private void DrawPlanView(double cSide, double botSpacing, double topSpacing, double distSpacing, bool enableBot, bool enableTop, bool enableDist)
        {
            double canvasW = CanvasBorder.ActualWidth > 300 ? CanvasBorder.ActualWidth : 800;
            double canvasH = CanvasBorder.ActualHeight > 300 ? CanvasBorder.ActualHeight : 600;

            double flightW = _flightWidthMm;
            double flightL = 7 * _treadDepthMm;
            double landingL = 350;
            double totalL = flightL + 2 * landingL;

            double scale = Math.Min((canvasW - 120) / totalL, (canvasH - 120) / flightW);
            double originX = (canvasW - totalL * scale) / 2.0;
            double originY = (canvasH - flightW * scale) / 2.0;

            // Stair footprint
            DrawRect(originX, originY, totalL * scale, flightW * scale, new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 14, 165, 233)), CreateBrush("#94A3B8"), 1.5);

            // Step risers
            for (int s = 0; s <= 7; s++)
            {
                double rx = originX + (landingL + s * _treadDepthMm) * scale;
                DrawLine(rx, originY, rx, originY + flightW * scale, CreateBrush("#CBD5E1"), 1.2);
            }

            // Longitudinal rebar lines
            if (enableBot)
            {
                int numBars = Math.Max(2, (int)Math.Floor((flightW - 2 * cSide) / botSpacing) + 1);
                double spacingPx = (flightW - 2 * cSide) / Math.Max(1, numBars - 1) * scale;

                for (int b = 0; b < numBars; b++)
                {
                    double y = originY + (cSide * scale) + b * spacingPx;
                    DrawLine(originX + 20, y, originX + (totalL * scale) - 20, y, CreateBrush("#0D6EFD"), 2.0);
                }
            }

            // Transverse distribution lines
            if (enableDist)
            {
                int numDist = Math.Max(3, (int)Math.Floor(flightL / distSpacing));
                for (int d = 0; d <= numDist; d++)
                {
                    double x = originX + (landingL + d * (flightL / numDist)) * scale;
                    DrawLine(x, originY + cSide * scale, x, originY + (flightW - cSide) * scale, CreateBrush("#10B981"), 2.0);
                }
            }

            DrawText(originX, originY - 30, "STAIR FLIGHT PLAN / TOP VIEW", 14, FontWeights.Bold, CreateBrush("#0F172A"));
        }
        #endregion

        #region Bottom View Rendering
        private void DrawBotView(double cSide, double botSpacing, double distSpacing, bool enableBot, bool enableDist)
        {
            DrawPlanView(cSide, botSpacing, botSpacing, distSpacing, enableBot, false, enableDist);
            DrawText(20, 20, "VIEW LOOKING UP AT WAIST SOFFIT (Bottom Reinforcement Mesh)", 12, FontWeights.SemiBold, CreateBrush("#0D6EFD"));
        }
        #endregion

        #endregion

        #region Rebar Generation Execution
        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            var config = new StairRebarConfig
            {
                CoverWaistMm = ParseDouble(TxtCoverWaist.Text, 30.0),
                CoverTopMm = ParseDouble(TxtCoverTop.Text, 30.0),
                CoverSideMm = ParseDouble(TxtCoverSide.Text, 30.0),
                WaistThicknessMm = ParseDouble(TxtWaistThick.Text, 160.0),

                EnableBottomRebar = ChkEnableBot.IsChecked == true,
                BottomBarTypeId = CmbBotType.SelectedItem is ComboBoxItem cb ? (long)cb.Tag : -1,
                BottomSpacingMm = ParseDouble(TxtBotSpacing.Text, 150.0),
                BottomAnchorBotMm = ParseDouble(TxtBotAnchorBot.Text, 500.0),
                BottomAnchorTopMm = ParseDouble(TxtBotAnchorTop.Text, 500.0),
                BottomHookBot = ChkBotHook.IsChecked == true,

                EnableTopRebar = ChkEnableTop.IsChecked == true,
                TopBarTypeId = CmbTopType.SelectedItem is ComboBoxItem ct ? (long)ct.Tag : -1,
                TopSpacingMm = ParseDouble(TxtTopSpacing.Text, 150.0),
                TopScissorLapMm = ParseDouble(TxtTopScissorLap.Text, 650.0),
                TopAnchorTopMm = ParseDouble(TxtTopAnchorTop.Text, 600.0),
                TopHookTop = ChkTopHook.IsChecked == true,

                EnableDistRebar = ChkEnableDist.IsChecked == true,
                DistBarTypeId = CmbDistType.SelectedItem is ComboBoxItem cd ? (long)cd.Tag : -1,
                DistSpacingMm = ParseDouble(TxtDistSpacing.Text, 200.0),
                EnableTopDistRebar = ChkTopDist.IsChecked == true,

                PartitionName = TxtPartition.Text.Trim(),
                ViewAsSolid3D = ChkViewSolid.IsChecked == true,
                ViewUnobscured = true
            };

            var targetStairs = _preselectedStairs;
            if (targetStairs == null || targetStairs.Count == 0)
            {
                MessageBox.Show("Please pick or select at least one Stair first.", "No Stair Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnGenerate.IsEnabled = false;
            PrgBar.Visibility = System.Windows.Visibility.Visible;
            TxtStatus.Visibility = System.Windows.Visibility.Visible;
            TxtStatus.Text = "⏳ Generating Stair Reinforcement...";

            _handler.Action = app =>
            {
                int stairsReinforced = 0;
                int totalRebarSetsCreated = 0;
                Exception? error = null;

                try
                {
                    var doc = app.ActiveUIDocument.Document;
                    using (Transaction trans = new Transaction(doc, "Generate Stair Rebar"))
                    {
                        FailureHandlingOptions failOpt = trans.GetFailureHandlingOptions();
                        failOpt.SetFailuresPreprocessor(new RebarFailurePreprocessor());
                        trans.SetFailureHandlingOptions(failOpt);

                        trans.Start();

                        for (int i = 0; i < targetStairs.Count; i++)
                        {
                            var stair = targetStairs[i];
                            int created = StairRebarGenerationService.Generate(doc, stair, config);
                            if (created > 0)
                            {
                                stairsReinforced++;
                                totalRebarSetsCreated += created;
                            }
                        }

                        trans.Commit();
                    }
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                Dispatcher.Invoke(() =>
                {
                    BtnGenerate.IsEnabled = true;
                    PrgBar.Visibility = System.Windows.Visibility.Collapsed;

                    if (error != null)
                    {
                        TxtStatus.Text = "❌ Error during stair rebar generation.";
                        MessageBox.Show($"Failed to generate rebar:\n\n{error.Message}\n\n{error.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        TxtStatus.Text = $"Completed: Reinforced {stairsReinforced} stair(s) ({totalRebarSetsCreated} rebar sets).";
                        MessageBox.Show(
                            $"Successfully reinforced:\n\n• {stairsReinforced} Stair Run(s)\n• {totalRebarSetsCreated} 3D Rebar Sets Generated\n\n(Waist Bottom Bars, Scissor Joint Top Bars, and Transverse Distribution Bars)",
                            "BIM Plus — Stair Reinforcement Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                });
            };

            _exEvent.Raise();
        }
        #endregion

        #region Helper Drawing Primitives
        private static SolidColorBrush CreateBrush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex);

        private void DrawLine(double x1, double y1, double x2, double y2, Brush stroke, double thickness)
        {
            var line = new System.Windows.Shapes.Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = stroke, StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            };
            PreviewCanvas.Children.Add(line);
        }

        private void DrawPolyline(IList<System.Windows.Point> pts, Brush stroke, double thickness)
        {
            if (pts.Count < 2) return;
            var polyline = new Polyline
            {
                Stroke = stroke, StrokeThickness = thickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            };
            foreach (var p in pts) polyline.Points.Add(p);
            PreviewCanvas.Children.Add(polyline);
        }

        private void DrawCircle(double cx, double cy, double radius, Brush fill, Brush stroke)
        {
            var ellipse = new System.Windows.Shapes.Ellipse
            {
                Width = radius * 2, Height = radius * 2,
                Fill = fill, Stroke = stroke, StrokeThickness = 1.0
            };
            Canvas.SetLeft(ellipse, cx - radius);
            Canvas.SetTop(ellipse, cy - radius);
            PreviewCanvas.Children.Add(ellipse);
        }

        private void DrawRect(double x, double y, double w, double h, Brush fill, Brush stroke, double thickness)
        {
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = Math.Max(1, w), Height = Math.Max(1, h),
                Fill = fill, Stroke = stroke, StrokeThickness = thickness
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            PreviewCanvas.Children.Add(rect);
        }

        private void DrawText(double x, double y, string text, double size, FontWeight weight, Brush brush)
        {
            var tb = new TextBlock
            {
                Text = text, FontSize = size, FontWeight = weight, Foreground = brush
            };
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
            PreviewCanvas.Children.Add(tb);
        }

        private void DrawBadge(double x, double y, string text, string hexColor)
        {
            var border = new Border
            {
                Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2)
            };
            var tb = new TextBlock
            {
                Text = text, Foreground = Brushes.White, FontSize = 10, FontWeight = FontWeights.Bold
            };
            border.Child = tb;
            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);
            PreviewCanvas.Children.Add(border);
        }

        private static double ParseDouble(string text, double fallback)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double val)) return val;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out val)) return val;
            return fallback;
        }
        #endregion
    }

    public class StairSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem == null || elem.Category == null) return false;
            long catId = elem.Category.Id.Value;
            return catId == (long)BuiltInCategory.OST_Stairs ||
                   catId == (long)BuiltInCategory.OST_StairsRuns ||
                   catId == (long)BuiltInCategory.OST_StairsLandings;
        }

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
