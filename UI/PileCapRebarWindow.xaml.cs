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
    public partial class PileCapRebarWindow : Window
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private readonly RebarGenerationService _rebarService;
        private readonly RevitElementService _elemService;
        private List<Element> _preselectedCaps;
        private AutoPileCap.Handlers.GenericEventHandler _handler;
        private ExternalEvent _exEvent;

        // View mode: ALL, 3D, TOP, BOT, SECTION
        private string _currentViewMode = "ALL";

        // Footing dimensions (mm)
        private double _capLengthMm = 1500;
        private double _capWidthMm = 1500;
        private double _capDepthMm = 600;

        private double _capCoverTopMm = 50.0;
        private double _capCoverBotMm = 100.0;
        private double _capCoverSideMm = 50.0;

        // Canvas pan & zoom state
        private bool _isPanning = false;
        private System.Windows.Point _panStartPoint;
        
        // 3D Orbit state
        private bool _isOrbiting = false;
        private System.Windows.Point _orbitStartPoint;
        private double _viewAngleZ = -Math.PI / 4;
        private double _viewAngleX = Math.PI / 6;

        private bool _isInitialized = false;

        public bool RequestPick { get; set; } = false;

        public PileCapRebarWindow(UIApplication uiapp, AutoPileCap.Handlers.GenericEventHandler handler, ExternalEvent exEvent, List<Element>? initialCaps = null)
        {
            InitializeComponent();

            _handler = handler;
            _exEvent = exEvent;

            _uidoc = uiapp.ActiveUIDocument;
            _doc = _uidoc.Document;
            _rebarService = new RebarGenerationService(_doc);
            _elemService = new RevitElementService(_doc);

            if (initialCaps != null && initialCaps.Count > 0)
            {
                _preselectedCaps = initialCaps;
            }
            else
            {
                var selectedIds = _uidoc.Selection.GetElementIds();
                _preselectedCaps = selectedIds.Select(id => _doc.GetElement(id)).Where(e => e.Category != null && e.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFoundation).ToList();
            }

            Loaded += PileCapRebarWindow_Loaded;
        }

        private void PileCapRebarWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PopulateRebarBarTypes();
            DetectSelectedFoundation();
            _isInitialized = true;
            RenderPreview();
        }

        private void DetectSelectedFoundation()
        {
            if (_preselectedCaps.Count > 0)
            {
                var cap = _preselectedCaps[0];
                if (_rebarService.GetPileCapLocalBounds(cap, out _, out double minX, out double maxX, out double minY, out double maxY, out double minZ, out double maxZ))
                {
                    _capLengthMm = Math.Max(300, (maxX - minX) * GeometryHelper.MmPerFoot);
                    _capWidthMm = Math.Max(300, (maxY - minY) * GeometryHelper.MmPerFoot);
                    _capDepthMm = Math.Max(200, (maxZ - minZ) * GeometryHelper.MmPerFoot);
                }

                _capCoverTopMm = GetCoverMm(cap, BuiltInParameter.CLEAR_COVER_TOP, 50.0);
                _capCoverBotMm = GetCoverMm(cap, BuiltInParameter.CLEAR_COVER_BOTTOM, 100.0);
                _capCoverSideMm = GetCoverMm(cap, BuiltInParameter.CLEAR_COVER_OTHER, 50.0);

                TxtFoundationName.Text = $"{cap.Name} [{_capLengthMm:F0}x{_capWidthMm:F0}x{_capDepthMm:F0}mm]";
            }
            else
            {
                TxtFoundationName.Text = "None (Click Select)";
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
                return;
            }

            CmbTopTypeX.Items.Clear();
            CmbTopTypeY.Items.Clear();
            CmbBotTypeX.Items.Clear();
            CmbBotTypeY.Items.Clear();
            CmbSideType.Items.Clear();

            int defaultTopIdx = 0;
            int defaultBotIdx = 0;
            int defaultSideIdx = 0;

            for (int i = 0; i < barTypes.Count; i++)
            {
                var bar = barTypes[i];
                double diaMm = bar.BarNominalDiameter * GeometryHelper.MmPerFoot;
                string displayName = $"{bar.Name} (Ø{diaMm:F0}mm)";

                CmbTopTypeX.Items.Add(new ComboBoxItem { Content = displayName, Tag = bar.Id.Value });
                CmbTopTypeY.Items.Add(new ComboBoxItem { Content = displayName, Tag = bar.Id.Value });
                CmbBotTypeX.Items.Add(new ComboBoxItem { Content = displayName, Tag = bar.Id.Value });
                CmbBotTypeY.Items.Add(new ComboBoxItem { Content = displayName, Tag = bar.Id.Value });
                CmbSideType.Items.Add(new ComboBoxItem { Content = displayName, Tag = bar.Id.Value });

                if (diaMm >= 11.0 && diaMm <= 14.0) defaultTopIdx = i;
                if (diaMm >= 15.0 && diaMm <= 18.0) defaultBotIdx = i;
                if (diaMm >= 11.0 && diaMm <= 14.0) defaultSideIdx = i;
            }

            CmbTopTypeX.SelectedIndex = defaultTopIdx;
            CmbTopTypeY.SelectedIndex = defaultTopIdx;
            CmbBotTypeX.SelectedIndex = defaultBotIdx;
            CmbBotTypeY.SelectedIndex = defaultBotIdx;
            CmbSideType.SelectedIndex = defaultSideIdx;
        }

        private void BtnSelectFoundation_Click(object sender, RoutedEventArgs e)
        {
            _handler.Action = app =>
            {
                try
                {
                    var uidoc = app.ActiveUIDocument;
                    var doc = uidoc.Document;
                    var filter = new StructuralFoundationSelectionFilter();
                    var refs = uidoc.Selection.PickObjects(
                        ObjectType.Element,
                        filter,
                        "Select Structural Foundations (Pile Caps), then click Finish in the top options bar (or press ESC when done)");

                    var targetCaps = refs
                        .Select(r => doc.GetElement(r))
                                                .Where(e => e.Category != null && e.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFoundation)
                        .ToList();

                    // Update UI on UI thread
                    Dispatcher.Invoke(() =>
                    {
                        _preselectedCaps = targetCaps;
                        DetectSelectedFoundation();
                        RenderPreview();
                    });
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // User cancelled with ESC
                }
            };
            _exEvent.Raise();
        }

        #region View Mode Switching & Toolbar
        private void BtnViewMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                _currentViewMode = btn.Tag.ToString() ?? "ALL";
                UpdateViewModeButtons();
                ResetZoomAndPan();
                RenderPreview();
            }
        }

        private void UpdateViewModeButtons()
        {
            var buttons = new[] { BtnViewAll, BtnView3D, BtnViewTop, BtnViewBot, BtnViewSection, NavView3D, NavViewTop, NavViewBot, NavViewSection };
            var activeBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(13, 110, 253)); // #0D6EFD Blue
            var activeFg = System.Windows.Media.Brushes.White;
            var normalBg = System.Windows.Media.Brushes.Transparent;
            var normalFg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139));    // #64748B

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
            
            // Get mouse position relative to the canvas border
            System.Windows.Point mousePos = e.GetPosition(CanvasBorder);

            // Calculate new scale
            double newScaleX = CanvasScale.ScaleX * zoomFactor;
            double newScaleY = CanvasScale.ScaleY * zoomFactor;

            // Optional limit on scale
            if (newScaleX < 0.1 || newScaleX > 15.0) return;

            // Adjust translation to zoom into mouse cursor
            CanvasTranslate.X = mousePos.X - (mousePos.X - CanvasTranslate.X) * zoomFactor;
            CanvasTranslate.Y = mousePos.Y - (mousePos.Y - CanvasTranslate.Y) * zoomFactor;

            CanvasScale.ScaleX = newScaleX;
            CanvasScale.ScaleY = newScaleY;

            e.Handled = true;
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) || e.ChangedButton == MouseButton.Right)
            {
                if (_currentViewMode == "3D" || _currentViewMode == "ALL")
                {
                    _isOrbiting = true;
                    _orbitStartPoint = e.GetPosition(CanvasBorder);
                    CanvasBorder.CaptureMouse();
                    e.Handled = true;
                    return;
                }
            }

            if (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Middle)
            {
                _isPanning = true;
                _panStartPoint = e.GetPosition(CanvasBorder);
                CanvasBorder.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isOrbiting)
            {
                System.Windows.Point currentPoint = e.GetPosition(CanvasBorder);
                double deltaX = currentPoint.X - _orbitStartPoint.X;
                double deltaY = currentPoint.Y - _orbitStartPoint.Y;

                _viewAngleZ += deltaX * 0.01;
                _viewAngleX += deltaY * 0.01;

                // Clamp X angle to avoid flipping upside down
                if (_viewAngleX > Math.PI / 2 - 0.01) _viewAngleX = Math.PI / 2 - 0.01;
                if (_viewAngleX < -Math.PI / 2 + 0.01) _viewAngleX = -Math.PI / 2 + 0.01;

                _orbitStartPoint = currentPoint;
                RenderPreview();
                e.Handled = true;
                return;
            }

            if (_isPanning)
            {
                System.Windows.Point currentPoint = e.GetPosition(CanvasBorder);
                CanvasTranslate.X += currentPoint.X - _panStartPoint.X;
                CanvasTranslate.Y += currentPoint.Y - _panStartPoint.Y;
                _panStartPoint = currentPoint;
                e.Handled = true;
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isOrbiting)
            {
                _isOrbiting = false;
                CanvasBorder.ReleaseMouseCapture();
                e.Handled = true;
            }

            if (_isPanning)
            {
                _isPanning = false;
                CanvasBorder.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Handled by Canvas_MouseDown
        }


        private void ResetZoomAndPan()
        {
            CanvasScale.ScaleX = 1.0;
            CanvasScale.ScaleY = 1.0;
            CanvasTranslate.X = 0;
            CanvasTranslate.Y = 0;
        }

        private void CanvasBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isInitialized)
            {
                RenderPreview();
            }
        }

        private void Inputs_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            if (PanelTop != null) PanelTop.IsEnabled = ChkEnableTop.IsChecked == true;
            if (PanelBot != null) PanelBot.IsEnabled = ChkEnableBot.IsChecked == true;
            if (PanelSide != null) PanelSide.IsEnabled = ChkEnableSide.IsChecked == true;

            RenderPreview();
        }
        #endregion

        #region CAD Vector Rendering Engine
        private void RenderPreview()
        {
            if (PreviewCanvas == null || !_isInitialized) return;
            PreviewCanvas.Children.Clear();

            // Read Form Values
            double cTop = _capCoverTopMm;
            double cBot = _capCoverBotMm;
            double cSide = _capCoverSideMm;

            double topHookX = ParseDouble(TxtTopHookX.Text, 400.0);
            double topHookY = ParseDouble(TxtTopHookY.Text, 400.0);
            double topSpX = ParseDouble(TxtTopSpacingX.Text, 150.0);
            double topSpY = ParseDouble(TxtTopSpacingY.Text, 150.0);

            double botHookX = ParseDouble(TxtBotHookX.Text, 450.0);
            double botHookY = ParseDouble(TxtBotHookY.Text, 450.0);
            double botSpX = ParseDouble(TxtBotSpacingX.Text, 150.0);
            double botSpY = ParseDouble(TxtBotSpacingY.Text, 150.0);

            int sideQty = (int)ParseDouble(TxtSideQty.Text, 3.0);

            bool enableTop = ChkEnableTop.IsChecked == true;
            bool enableBot = ChkEnableBot.IsChecked == true;
            bool enableSide = ChkEnableSide.IsChecked == true;

            switch (_currentViewMode)
            {
                case "ALL":
                    DrawAllView(cTop, cBot, cSide, topHookX, topSpX, topSpY, botHookX, botSpX, botSpY, sideQty, enableTop, enableBot, enableSide);
                    break;
                case "3D":
                    Draw3DView(cTop, cBot, cSide, topHookX, topHookY, topSpX, topSpY, botHookX, botHookY, botSpX, botSpY, sideQty, enableTop, enableBot, enableSide);
                    break;
                case "TOP":
                    DrawTopView(cTop, cSide, topHookX, topHookY, topSpX, topSpY, enableTop);
                    break;
                case "BOT":
                    DrawBotView(cBot, cSide, botHookX, botHookY, botSpX, botSpY, enableBot);
                    break;
                case "SECTION":
                    DrawSectionView(cTop, cBot, cSide, topHookX, topSpX, botHookX, botSpX, sideQty, enableTop, enableBot, enableSide);
                    break;
                default:
                    DrawAllView(cTop, cBot, cSide, topHookX, topSpX, topSpY, botHookX, botSpX, botSpY, sideQty, enableTop, enableBot, enableSide);
                    break;
            }

            // Update Summary Texts
            if (TxtSumTop != null) 
            {
                string tName = CmbTopTypeX.SelectedItem is ComboBoxItem cbt ? cbt.Content.ToString() : "Top";
                TxtSumTop.Text = $"{tName} @{topSpX}mm (Hook {topHookX}mm)";
            }
            if (TxtSumBot != null) 
            {
                string bName = CmbBotTypeX.SelectedItem is ComboBoxItem cbb ? cbb.Content.ToString() : "Bot";
                TxtSumBot.Text = $"{bName} @{botSpX}mm (Hook {botHookX}mm)";
            }
            if (TxtSumSide != null) 
            {
                string sName = CmbSideType.SelectedItem is ComboBoxItem cbs ? cbs.Content.ToString() : "Side";
                TxtSumSide.Text = $"{sName} (Qty {sideQty})";
            }
            if (TxtDrawingCovers != null)
            {
                TxtDrawingCovers.Text = $"Top: {cTop}mm | Bot: {cBot}mm | Side: {cSide}mm";
            }
        }

        #region Mode: ALL View (Combined Plan Mesh + Section View)
        private void DrawAllView(double cTop, double cBot, double cSide, double topHookX, double topSpX, double topSpY,
                                double botHookX, double botSpX, double botSpY, int sideQty,
                                bool enableTop, bool enableBot, bool enableSide)
        {
            double viewW = CanvasBorder.ActualWidth > 300 ? CanvasBorder.ActualWidth : 750;
            double viewH = CanvasBorder.ActualHeight > 300 ? CanvasBorder.ActualHeight : 750;

            double cx = viewW / 2 + 30; // Center shifted slightly right of the left toolbar
            double planY = 175;

            // Plan scale
            double planScale = Math.Min(320.0 / _capLengthMm, 260.0 / _capWidthMm);
            double pw = _capLengthMm * planScale;
            double ph = _capWidthMm * planScale;

            double px1 = cx - pw / 2;
            double py1 = planY - ph / 2;
            double px2 = px1 + pw;
            double py2 = py1 + ph;

            // Footing Concrete Outline in Plan
            var dashStroke = new DoubleCollection { 4, 3 };
            DrawRect(px1, py1, pw, ph, Brushes.Transparent, CreateBrush("#38BDF8"), 1.2, dashStroke);

            // Plan Dimension on Left
            DrawDimensionLine(px1 - 25, py1, px1 - 25, py2, $"{_capWidthMm:F0}", isVertical: true);

            // Rebar Mesh in Plan (Inside Side Cover)
            double csPix = cSide * planScale;
            double mx1 = px1 + csPix;
            double my1 = py1 + csPix;
            double mx2 = px2 - csPix;
            double my2 = py2 - csPix;

            // X-direction Bars (horizontal lines)
            double spYPix = botSpY * planScale;
            if (spYPix < 6) spYPix = 6;
            int countX = (int)Math.Max(2, Math.Round((my2 - my1) / spYPix));
            double actSpY = (my2 - my1) / countX;

            for (int i = 0; i <= countX; i++)
            {
                double y = my1 + i * actSpY;
                var colorBrush = enableTop && !enableBot ? CreateBrush("#F59E0B") : (enableBot ? CreateBrush("#0D6EFD") : CreateBrush("#64748B"));
                DrawLine(mx1, y, mx2, y, colorBrush, 1.0, enableTop && enableBot ? dashStroke : null);
            }

            // Y-direction Bars (vertical lines)
            double spXPix = botSpX * planScale;
            if (spXPix < 6) spXPix = 6;
            int countY = (int)Math.Max(2, Math.Round((mx2 - mx1) / spXPix));
            double actSpX = (mx2 - mx1) / countY;

            for (int i = 0; i <= countY; i++)
            {
                double x = mx1 + i * actSpX;
                var colorBrush = enableTop && !enableBot ? CreateBrush("#F59E0B") : (enableBot ? CreateBrush("#0D6EFD") : CreateBrush("#64748B"));
                DrawLine(x, my1, x, my2, colorBrush, 1.0, enableTop && enableBot ? dashStroke : null);
            }

            // Section View Title in Bright Yellow
            double titleY = py2 + 45;
            DrawText("SECTION VIEW", cx, titleY, CreateBrush("#D97706"), 13, isBold: true);

            // Section View Parameters
            double secY = titleY + 75;
            double secScale = planScale;
            double sw = _capLengthMm * secScale;
            double sh = _capDepthMm * secScale;

            double sx1 = cx - sw / 2;
            double sy1 = secY;
            double sx2 = sx1 + sw;
            double sy2 = sy1 + sh;

            // Section Dimension on Left
            DrawDimensionLine(sx1 - 25, sy1, sx1 - 25, sy2, $"{_capDepthMm:F0}", isVertical: true);

            // Footing Concrete Outline
            DrawRect(sx1, sy1, sw, sh, CreateBrush("#F8FAFC"), CreateBrush("#334155"), 1.6);

            // Section Rebar Coordinates
            double cSideSec = cSide * secScale;
            double cBotSec = cBot * secScale;
            double cTopSec = cTop * secScale;

            double rx1 = sx1 + cSideSec;
            double rx2 = sx2 - cSideSec;

            // 1. Bottom U-Bar (RED)
            if (enableBot)
            {
                double ryBot = sy2 - cBotSec;
                double hookH = Math.Min(botHookX, _capDepthMm - cBot - 20) * secScale;
                double ryHook = ryBot - hookH;

                var botU = new PointCollection
                {
                    new System.Windows.Point(rx1, ryHook),
                    new System.Windows.Point(rx1, ryBot),
                    new System.Windows.Point(rx2, ryBot),
                    new System.Windows.Point(rx2, ryHook)
                };
                DrawPolyline(botU, CreateBrush("#0D6EFD"), 2.0);

                // Transverse dots inside bottom bar
                for (int i = 0; i <= countX; i++)
                {
                    double dotX = rx1 + i * ((rx2 - rx1) / countX);
                    DrawCircle(dotX, ryBot - 4, 3.0, CreateBrush("#0D6EFD"));
                }
            }

            // 2. Top U-Bar (CYAN)
            if (enableTop)
            {
                double ryTop = sy1 + cTopSec;
                double hookH = Math.Min(topHookX, _capDepthMm - cTop - 20) * secScale;
                double ryHook = ryTop + hookH;

                var topU = new PointCollection
                {
                    new System.Windows.Point(rx1, ryHook),
                    new System.Windows.Point(rx1, ryTop),
                    new System.Windows.Point(rx2, ryTop),
                    new System.Windows.Point(rx2, ryHook)
                };
                DrawPolyline(topU, CreateBrush("#F59E0B"), 2.0);

                // Transverse dots under top bar
                for (int i = 0; i <= countX; i++)
                {
                    double dotX = rx1 + i * ((rx2 - rx1) / countX);
                    DrawCircle(dotX, ryTop + 4, 3.0, CreateBrush("#F59E0B"));
                }
            }

            // 3. Side Ties (GREEN)
            if (enableSide)
            {
                double ryBot = sy2 - cBotSec;
                double ryTop = sy1 + cTopSec;
                double yStart = ryBot;
                double yEnd = ryTop;
                
                double stepY = (yEnd - yStart) / (sideQty + 1);

                for (int i = 1; i <= sideQty; i++)
                {
                    double y = yStart + i * stepY;
                    DrawCircle(rx1 - 3.5, y, 2.5, CreateBrush("#06B6D4"));
                    DrawCircle(rx2 + 3.5, y, 2.5, CreateBrush("#06B6D4"));
                }
            }
        }
        #endregion

        #region Mode: 3D Isometric View
        private void Draw3DView(double cTop, double cBot, double cSide, double topHookX, double topHookY,
                               double topSpX, double topSpY, double botHookX, double botHookY,
                               double botSpX, double botSpY, int sideQty,
                               bool enableTop, bool enableBot, bool enableSide)
        {
            double viewW = CanvasBorder.ActualWidth > 300 ? CanvasBorder.ActualWidth : 800;
            double viewH = CanvasBorder.ActualHeight > 300 ? CanvasBorder.ActualHeight : 750;

            double cx = viewW / 2 + 20;
            double cy = viewH / 2 + 10;

            // 3D Isometric Projection Function using Rotation Matrix
            double scale = Math.Min(260.0 / _capLengthMm, Math.Min(260.0 / _capWidthMm, 220.0 / _capDepthMm));

            System.Windows.Point IsoProject(double x, double y, double z)
            {
                // Centered at (_capLengthMm/2, _capWidthMm/2, _capDepthMm/2)
                double dx = x - _capLengthMm / 2;
                double dy = y - _capWidthMm / 2;
                double dz = z - _capDepthMm / 2;

                // Rotate around Z-axis
                double x1 = dx * Math.Cos(_viewAngleZ) - dy * Math.Sin(_viewAngleZ);
                double y1 = dx * Math.Sin(_viewAngleZ) + dy * Math.Cos(_viewAngleZ);

                // Rotate around X-axis
                double y2 = y1 * Math.Cos(_viewAngleX) - dz * Math.Sin(_viewAngleX);
                // double z2 = y1 * Math.Sin(_viewAngleX) + dz * Math.Cos(_viewAngleX);

                // Project to 2D Canvas space
                double px = cx + x1 * scale;
                double py = cy + y2 * scale; // Y is inverted in WPF, so +y2 maps to moving down
                return new System.Windows.Point(px, py);
            }

            // Title
            DrawText("3D ISOMETRIC VIEW — REINFORCEMENT CAGE", cx, 40, CreateBrush("#38BDF8"), 14, isBold: true);
            DrawText($"Pile Cap: {_capLengthMm:F0} x {_capWidthMm:F0} x {_capDepthMm:F0} mm", cx, 62, CreateBrush("#64748B"), 11);

            double L = _capLengthMm;
            double W = _capWidthMm;
            double H = _capDepthMm;

            // 1. Concrete Footing Box (Wireframe + Translucent Shaded Faces)
            var p000 = IsoProject(0, 0, 0);
            var pL00 = IsoProject(L, 0, 0);
            var pLW0 = IsoProject(L, W, 0);
            var p0W0 = IsoProject(0, W, 0);
            var p00H = IsoProject(0, 0, H);
            var pL0H = IsoProject(L, 0, H);
            var pLWH = IsoProject(L, W, H);
            var p0WH = IsoProject(0, W, H);

            // Shaded top face
            var topFace = new PointCollection { p00H, pL0H, pLWH, p0WH };
            DrawPolygon(topFace, new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 148, 163, 184)), CreateBrush("#64748B"), 1.5);

            // Shaded right face
            var rightFace = new PointCollection { pL00, pL0H, pLWH, pLW0 };
            DrawPolygon(rightFace, new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 148, 163, 184)), CreateBrush("#64748B"), 1.5);

            // Shaded front-left face
            var leftFace = new PointCollection { p000, p00H, p0WH, p0W0 };
            DrawPolygon(leftFace, new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 148, 163, 184)), CreateBrush("#64748B"), 1.5);

            // 2. Bottom Rebar Cage (RED)
            if (enableBot)
            {
                var redBrush = CreateBrush("#0D6EFD");
                double mainSide = cSide + (enableSide ? 12 : 0);

                // Longitudinal Bars (running X direction, distributed along Y)
                int numY = (int)Math.Max(2, (W - 2 * mainSide) / botSpY);
                double stepY = (W - 2 * mainSide) / numY;
                double hBotX = Math.Min(botHookX, H - cBot - 30);

                for (int i = 0; i <= numY; i++)
                {
                    double y = mainSide + i * stepY;
                    var pts = new PointCollection
                    {
                        IsoProject(mainSide, y, cBot + hBotX),
                        IsoProject(mainSide, y, cBot),
                        IsoProject(L - mainSide, y, cBot),
                        IsoProject(L - mainSide, y, cBot + hBotX)
                    };
                    DrawPolyline(pts, redBrush, 1.8);
                }

                // Transverse Bars (running Y direction, distributed along X)
                int numX = (int)Math.Max(2, (L - 2 * mainSide) / botSpX);
                double stepX = (L - 2 * mainSide) / numX;
                double hBotY = Math.Min(botHookY, H - cBot - 30);

                for (int i = 0; i <= numX; i++)
                {
                    double x = mainSide + i * stepX;
                    var pts = new PointCollection
                    {
                        IsoProject(x, mainSide, cBot + hBotY),
                        IsoProject(x, mainSide, cBot),
                        IsoProject(x, W - mainSide, cBot),
                        IsoProject(x, W - mainSide, cBot + hBotY)
                    };
                    DrawPolyline(pts, redBrush, 1.4);
                }
            }

            // 3. Top Rebar Cage (CYAN)
            if (enableTop)
            {
                var cyanBrush = CreateBrush("#F59E0B");
                double mainSide = cSide + (enableSide ? 12 : 0);

                // Longitudinal Bars (running X direction, distributed along Y)
                int numY = (int)Math.Max(2, (W - 2 * mainSide) / topSpY);
                double stepY = (W - 2 * mainSide) / numY;
                double hTopX = Math.Min(topHookX, H - cTop - 30);

                for (int i = 0; i <= numY; i++)
                {
                    double y = mainSide + i * stepY;
                    var pts = new PointCollection
                    {
                        IsoProject(mainSide, y, H - cTop - hTopX),
                        IsoProject(mainSide, y, H - cTop),
                        IsoProject(L - mainSide, y, H - cTop),
                        IsoProject(L - mainSide, y, H - cTop - hTopX)
                    };
                    DrawPolyline(pts, cyanBrush, 1.8);
                }

                // Transverse Bars (running Y direction, distributed along X)
                int numX = (int)Math.Max(2, (L - 2 * mainSide) / topSpX);
                double stepX = (L - 2 * mainSide) / numX;
                double hTopY = Math.Min(topHookY, H - cTop - 30);

                for (int i = 0; i <= numX; i++)
                {
                    double x = mainSide + i * stepX;
                    var pts = new PointCollection
                    {
                        IsoProject(x, mainSide, H - cTop - hTopY),
                        IsoProject(x, mainSide, H - cTop),
                        IsoProject(x, W - mainSide, H - cTop),
                        IsoProject(x, W - mainSide, H - cTop - hTopY)
                    };
                    DrawPolyline(pts, cyanBrush, 1.4);
                }
            }

            // 4. Perimeter Side Ties (GREEN) - Overlapping U-bars
            if (enableSide)
            {
                var greenBrush = CreateBrush("#06B6D4");
                double zStart = cBot;
                double zEnd = H - cTop;
                double sideLap = 600.0;

                double overlapY = (W - 2 * cSide) / 2 + sideLap / 2;
                overlapY = Math.Min(overlapY, W - 2 * cSide);

                double stepZ = (zEnd - zStart) / (sideQty + 1);

                for (int i = 1; i <= sideQty; i++)
                {
                    double z = zStart + i * stepZ;

                    // Top U-Bar (Inverted U)
                    var topU = new PointCollection
                    {
                        IsoProject(cSide, W - cSide - overlapY, z),
                        IsoProject(cSide, W - cSide, z),
                        IsoProject(L - cSide, W - cSide, z),
                        IsoProject(L - cSide, W - cSide - overlapY, z)
                    };
                    DrawPolyline(topU, greenBrush, 1.5);

                    // Bottom U-Bar (U-shape)
                    var botU = new PointCollection
                    {
                        IsoProject(L - cSide, cSide + overlapY, z),
                        IsoProject(L - cSide, cSide, z),
                        IsoProject(cSide, cSide, z),
                        IsoProject(cSide, cSide + overlapY, z)
                    };
                    DrawPolyline(botU, greenBrush, 1.5);
                }
            }

            // Outer Concrete Wireframe Edges
            var edgeBrush = CreateBrush("#64748B");
            DrawLine(p000.X, p000.Y, pL00.X, pL00.Y, edgeBrush, 1.0);
            DrawLine(pL00.X, pL00.Y, pLW0.X, pLW0.Y, edgeBrush, 1.0);
            DrawLine(p00H.X, p00H.Y, pL0H.X, pL0H.Y, edgeBrush, 1.2);
            DrawLine(pL0H.X, pL0H.Y, pLWH.X, pLWH.Y, edgeBrush, 1.2);
            DrawLine(pLWH.X, pLWH.Y, p0WH.X, p0WH.Y, edgeBrush, 1.2);
            DrawLine(p0WH.X, p0WH.Y, p00H.X, p00H.Y, edgeBrush, 1.2);
            DrawLine(pL00.X, pL00.Y, pL0H.X, pL0H.Y, edgeBrush, 1.2);
            DrawLine(pLW0.X, pLW0.Y, pLWH.X, pLWH.Y, edgeBrush, 1.2);

            // 3D Axis Indicator (Bottom Right)
            double ax = viewW - 90;
            double ay = viewH - 70;

            System.Windows.Point IsoAxis(double dx, double dy, double dz)
            {
                double x1 = dx * Math.Cos(_viewAngleZ) - dy * Math.Sin(_viewAngleZ);
                double y1 = dx * Math.Sin(_viewAngleZ) + dy * Math.Cos(_viewAngleZ);
                double y2 = y1 * Math.Cos(_viewAngleX) - dz * Math.Sin(_viewAngleX);
                return new System.Windows.Point(ax + x1, ay + y2);
            }

            var origin = IsoAxis(0, 0, 0);
            var pxX = IsoAxis(35, 0, 0);
            var pxY = IsoAxis(0, 35, 0);
            var pxZ = IsoAxis(0, 0, 35);
            var txX = IsoAxis(45, 0, 0);
            var txY = IsoAxis(0, 45, 0);
            var txZ = IsoAxis(0, 0, 45);

            DrawLine(origin.X, origin.Y, pxX.X, pxX.Y, CreateBrush("#0D6EFD"), 2.0); // X Axis
            DrawText("X", txX.X, txX.Y, CreateBrush("#0D6EFD"), 10, isBold: true);

            DrawLine(origin.X, origin.Y, pxY.X, pxY.Y, CreateBrush("#06B6D4"), 2.0); // Y Axis
            DrawText("Y", txY.X, txY.Y, CreateBrush("#06B6D4"), 10, isBold: true);

            DrawLine(origin.X, origin.Y, pxZ.X, pxZ.Y, CreateBrush("#38BDF8"), 2.0); // Z Axis
            DrawText("Z", txZ.X, txZ.Y, CreateBrush("#38BDF8"), 10, isBold: true);
        }
        #endregion

        #region Mode: TOP View
        private void DrawTopView(double cTop, double cSide, double topHookX, double topHookY, double topSpX, double topSpY, bool enableTop)
        {
            double viewW = CanvasBorder.ActualWidth > 300 ? CanvasBorder.ActualWidth : 800;
            double viewH = CanvasBorder.ActualHeight > 300 ? CanvasBorder.ActualHeight : 750;

            double cx = viewW / 2 + 25;
            double planY = viewH / 2 - 40;

            DrawText("TOP REBAR LAYER (U-SHAPE SETS)", cx, 40, CreateBrush("#F59E0B"), 14, isBold: true);
            DrawText($"Spacing: {topSpX:F0}mm x {topSpY:F0}mm | Downward Hooks: {topHookX:F0}mm", cx, 62, CreateBrush("#64748B"), 11);

            double planScale = Math.Min(380.0 / _capLengthMm, 340.0 / _capWidthMm);
            double pw = _capLengthMm * planScale;
            double ph = _capWidthMm * planScale;

            double px1 = cx - pw / 2;
            double py1 = planY - ph / 2;
            double px2 = px1 + pw;
            double py2 = py1 + ph;

            // Dimensions
            DrawDimensionLine(px1 - 30, py1, px1 - 30, py2, $"{_capWidthMm:F0}", isVertical: true);
            DrawDimensionLine(px1, py1 - 25, px2, py1 - 25, $"{_capLengthMm:F0}", isVertical: false);

            // Concrete Boundary
            DrawRect(px1, py1, pw, ph, CreateBrush("#F8FAFC"), CreateBrush("#38BDF8"), 1.5, new DoubleCollection { 4, 3 });

            if (enableTop)
            {
                double csPix = cSide * planScale;
                double mx1 = px1 + csPix;
                double my1 = py1 + csPix;
                double mx2 = px2 - csPix;
                double my2 = py2 - csPix;

                int countY = (int)Math.Max(2, Math.Round((my2 - my1) / (topSpY * planScale)));
                double actSpY = (my2 - my1) / countY;

                var cyanBrush = CreateBrush("#F59E0B");

                // Longitudinal Bars (X Direction)
                for (int i = 0; i <= countY; i++)
                {
                    double y = my1 + i * actSpY;
                    DrawLine(mx1, y, mx2, y, cyanBrush, 1.8);
                    // Hook representations at ends (downward bent arrows)
                    DrawCircle(mx1, y, 3.0, cyanBrush);
                    DrawCircle(mx2, y, 3.0, cyanBrush);
                }

                int countX = (int)Math.Max(2, Math.Round((mx2 - mx1) / (topSpX * planScale)));
                double actSpX = (mx2 - mx1) / countX;

                // Transverse Bars (Y Direction)
                for (int i = 0; i <= countX; i++)
                {
                    double x = mx1 + i * actSpX;
                    DrawLine(x, my1, x, my2, cyanBrush, 1.4);
                    DrawCircle(x, my1, 2.5, cyanBrush);
                    DrawCircle(x, my2, 2.5, cyanBrush);
                }

                // Callout Badge at Bottom
            }
            else
            {
                DrawText("Top Rebar is currently DISABLED", cx, planY, CreateBrush("#64748B"), 14);
            }
        }
        #endregion

        #region Mode: BOT View
        private void DrawBotView(double cBot, double cSide, double botHookX, double botHookY, double botSpX, double botSpY, bool enableBot)
        {
            double viewW = CanvasBorder.ActualWidth > 300 ? CanvasBorder.ActualWidth : 800;
            double viewH = CanvasBorder.ActualHeight > 300 ? CanvasBorder.ActualHeight : 750;

            double cx = viewW / 2 + 25;
            double planY = viewH / 2 - 40;

            DrawText("BOTTOM REBAR LAYER (U-SHAPE SETS)", cx, 40, CreateBrush("#0D6EFD"), 14, isBold: true);
            DrawText($"Spacing: {botSpX:F0}mm x {botSpY:F0}mm | Upward Hooks: {botHookX:F0}mm", cx, 62, CreateBrush("#64748B"), 11);

            double planScale = Math.Min(380.0 / _capLengthMm, 340.0 / _capWidthMm);
            double pw = _capLengthMm * planScale;
            double ph = _capWidthMm * planScale;

            double px1 = cx - pw / 2;
            double py1 = planY - ph / 2;
            double px2 = px1 + pw;
            double py2 = py1 + ph;

            // Dimensions
            DrawDimensionLine(px1 - 30, py1, px1 - 30, py2, $"{_capWidthMm:F0}", isVertical: true);
            DrawDimensionLine(px1, py1 - 25, px2, py1 - 25, $"{_capLengthMm:F0}", isVertical: false);

            // Concrete Boundary
            DrawRect(px1, py1, pw, ph, CreateBrush("#F8FAFC"), CreateBrush("#0D6EFD"), 1.5, new DoubleCollection { 4, 3 });

            if (enableBot)
            {
                double csPix = cSide * planScale;
                double mx1 = px1 + csPix;
                double my1 = py1 + csPix;
                double mx2 = px2 - csPix;
                double my2 = py2 - csPix;

                int countY = (int)Math.Max(2, Math.Round((my2 - my1) / (botSpY * planScale)));
                double actSpY = (my2 - my1) / countY;

                var redBrush = CreateBrush("#0D6EFD");

                // Longitudinal Bars (X Direction)
                for (int i = 0; i <= countY; i++)
                {
                    double y = my1 + i * actSpY;
                    DrawLine(mx1, y, mx2, y, redBrush, 1.8);
                    // Hook representations at ends (upward bent arrows)
                    DrawCircle(mx1, y, 3.0, redBrush);
                    DrawCircle(mx2, y, 3.0, redBrush);
                }

                int countX = (int)Math.Max(2, Math.Round((mx2 - mx1) / (botSpX * planScale)));
                double actSpX = (mx2 - mx1) / countX;

                // Transverse Bars (Y Direction)
                for (int i = 0; i <= countX; i++)
                {
                    double x = mx1 + i * actSpX;
                    DrawLine(x, my1, x, my2, redBrush, 1.4);
                    DrawCircle(x, my1, 2.5, redBrush);
                    DrawCircle(x, my2, 2.5, redBrush);
                }

                // Callout Badge at Bottom
            }
            else
            {
                DrawText("Bottom Rebar is currently DISABLED", cx, planY, CreateBrush("#64748B"), 14);
            }
        }
        #endregion

        #region Mode: Detailed SECTION View
        private void DrawSectionView(double cTop, double cBot, double cSide, double topHookX, double topSpX,
                                     double botHookX, double botSpX, int sideQty,
                                     bool enableTop, bool enableBot, bool enableSide)
        {
            double viewW = CanvasBorder.ActualWidth > 300 ? CanvasBorder.ActualWidth : 800;
            double viewH = CanvasBorder.ActualHeight > 300 ? CanvasBorder.ActualHeight : 750;

            double cx = viewW / 2 + 25;
            double cy = viewH / 2;

            DrawText("DETAILED ELEVATION SECTION VIEW", cx - 130, 40, CreateBrush("#0F172A"), 14, isBold: true);

            double scale = Math.Min(500.0 / _capLengthMm, 320.0 / _capDepthMm);
            double sw = _capLengthMm * scale;
            double sh = _capDepthMm * scale;

            double sx1 = cx - sw / 2;
            double sy1 = cy - sh / 2;
            double sx2 = sx1 + sw;
            double sy2 = sy1 + sh;

            // Dimensions
            DrawDimensionLine(sx1 - 40, sy1, sx1 - 40, sy2, $"{_capDepthMm:F0}", isVertical: true);
            DrawDimensionLine(sx1, sy2 + 35, sx2, sy2 + 35, $"{_capLengthMm:F0}", isVertical: false);

            // Concrete Rectangle
            DrawRect(sx1, sy1, sw, sh, CreateBrush("#F8FAFC"), CreateBrush("#334155"), 2.0);

            // Concrete Cover Guides & Callouts
            double csPix = cSide * scale;
            double cbPix = cBot * scale;
            double ctPix = cTop * scale;

            double rx1 = sx1 + csPix;
            double rx2 = sx2 - csPix;

            // 1. Bottom U-Bar (RED)
            if (enableBot)
            {
                double ryBot = sy2 - cbPix;
                double hookH = Math.Min(botHookX, _capDepthMm - cBot - 20) * scale;
                double ryHook = ryBot - hookH;

                var botU = new PointCollection
                {
                    new System.Windows.Point(rx1, ryHook),
                    new System.Windows.Point(rx1, ryBot),
                    new System.Windows.Point(rx2, ryBot),
                    new System.Windows.Point(rx2, ryHook)
                };
                DrawPolyline(botU, CreateBrush("#0D6EFD"), 2.4);

                // Transverse dots inside bottom bar
                int dotCount = (int)Math.Max(3, (rx2 - rx1) / (botSpX * scale));
                double dotStep = (rx2 - rx1) / dotCount;
                for (int i = 0; i <= dotCount; i++)
                {
                    DrawCircle(rx1 + i * dotStep, ryBot - 5, 4.0, CreateBrush("#0D6EFD"));
                }

                // Annotation for Bottom Hook
                DrawText($"Hook = {botHookX:F0}mm", rx1 + 40, ryHook - 12, CreateBrush("#F59E0B"), 11, isBold: true);
            }

            // 2. Top U-Bar (CYAN)
            if (enableTop)
            {
                double ryTop = sy1 + ctPix;
                double hookH = Math.Min(topHookX, _capDepthMm - cTop - 20) * scale;
                double ryHook = ryTop + hookH;

                var topU = new PointCollection
                {
                    new System.Windows.Point(rx1, ryHook),
                    new System.Windows.Point(rx1, ryTop),
                    new System.Windows.Point(rx2, ryTop),
                    new System.Windows.Point(rx2, ryHook)
                };
                DrawPolyline(topU, CreateBrush("#F59E0B"), 2.4);

                // Transverse dots under top bar
                int dotCount = (int)Math.Max(3, (rx2 - rx1) / (topSpX * scale));
                double dotStep = (rx2 - rx1) / dotCount;
                for (int i = 0; i <= dotCount; i++)
                {
                    DrawCircle(rx1 + i * dotStep, ryTop + 5, 4.0, CreateBrush("#F59E0B"));
                }

                // Annotation for Top Hook
                DrawText($"Hook = {topHookX:F0}mm", rx1 + 40, ryHook + 12, CreateBrush("#F59E0B"), 11, isBold: true);
            }

            // 3. Side Ties (GREEN)
            if (enableSide)
            {
                double ryBot = sy2 - cbPix;
                double ryTop = sy1 + ctPix;
                double yStart = ryBot;
                double yEnd = ryTop;
                
                double stepY = (yEnd - yStart) / (sideQty + 1);

                for (int i = 1; i <= sideQty; i++)
                {
                    double y = yStart + i * stepY;
                    DrawCircle(rx1 - 4.2, y, 3.2, CreateBrush("#06B6D4"));
                    DrawCircle(rx2 + 4.2, y, 3.2, CreateBrush("#06B6D4"));
                }
            }

            // Annotations Table / Legend at Bottom
            double legendY = sy2 + 70;
            DrawText($"Covers: Top={cTop:F0}mm | Bot={cBot:F0}mm | Side={cSide:F0}mm", cx, legendY, CreateBrush("#475569"), 11);
        }
        #endregion

        #region CAD Vector Helper Methods
        private void DrawLine(double x1, double y1, double x2, double y2, Brush stroke, double thickness, DoubleCollection? dash = null)
        {
            var line = new System.Windows.Shapes.Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = stroke,
                StrokeThickness = (thickness > 1.2 && dash == null) ? thickness * 2.0 : thickness,
                StrokeDashArray = dash,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            PreviewCanvas.Children.Add(line);
        }

        private void DrawRect(double x, double y, double w, double h, Brush fill, Brush stroke, double thickness, DoubleCollection? dash = null)
        {
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = Math.Max(0, w),
                Height = Math.Max(0, h),
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = (thickness > 1.2 && dash == null) ? thickness * 2.0 : thickness,
                StrokeDashArray = dash,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            PreviewCanvas.Children.Add(rect);
        }

        private void DrawPolyline(PointCollection points, Brush stroke, double thickness)
        {
            var poly = new Polyline
            {
                Points = points,
                Stroke = stroke,
                StrokeThickness = thickness * 2.0,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            PreviewCanvas.Children.Add(poly);
        }

        private void DrawPolygon(PointCollection points, Brush fill, Brush stroke, double thickness)
        {
            var poly = new Polygon
            {
                Points = points,
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = thickness * 2.0,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            PreviewCanvas.Children.Add(poly);
        }

        private void DrawCircle(double cx, double cy, double radius, Brush fill)
        {
            var ell = new System.Windows.Shapes.Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = fill
            };
            Canvas.SetLeft(ell, cx - radius);
            Canvas.SetTop(ell, cy - radius);
            PreviewCanvas.Children.Add(ell);
        }

        private void DrawText(string text, double cx, double cy, Brush foreground, double fontSize, bool isBold = false)
        {
            var tb = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontSize = fontSize,
                FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal
            };
            tb.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, cx - tb.DesiredSize.Width / 2);
            Canvas.SetTop(tb, cy - tb.DesiredSize.Height / 2);
            PreviewCanvas.Children.Add(tb);
        }

        private void DrawDimensionLine(double x1, double y1, double x2, double y2, string text, bool isVertical)
        {
            var stroke = CreateBrush("#64748B");
            DrawLine(x1, y1, x2, y2, stroke, 1.0);

            // 45-degree architectural tick marks
            const double tick = 4.0;
            DrawLine(x1 - tick, y1 + tick, x1 + tick, y1 - tick, stroke, 1.2);
            DrawLine(x2 - tick, y2 + tick, x2 + tick, y2 - tick, stroke, 1.2);

            // Dimension text
            double mx = (x1 + x2) / 2;
            double my = (y1 + y2) / 2;

            if (isVertical)
            {
                var tb = new TextBlock
                {
                    Text = text,
                    Foreground = CreateBrush("#1E293B"),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold
                };
                tb.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(tb, mx - tb.DesiredSize.Width - 4);
                Canvas.SetTop(tb, my - tb.DesiredSize.Height / 2);
                PreviewCanvas.Children.Add(tb);
            }
            else
            {
                var tb = new TextBlock
                {
                    Text = text,
                    Foreground = CreateBrush("#1E293B"),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold
                };
                tb.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(tb, mx - tb.DesiredSize.Width / 2);
                Canvas.SetTop(tb, my - tb.DesiredSize.Height - 3);
                PreviewCanvas.Children.Add(tb);
            }
        }

        private static SolidColorBrush CreateBrush(string hex)
        {
            return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        }

        private static double ParseDouble(string str, double defaultVal)
        {
            if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double val) && val > 0)
                return val;
            return defaultVal;
        }
        #endregion
        #endregion

        #region Rebar Generation Execution
        private PileCapRebarConfig? BuildConfig()
        {
            var config = new PileCapRebarConfig();
            config.CoverTopMm = 75.0;
            config.CoverBottomMm = 150.0;
            config.CoverSideMm = 75.0;

            config.EnableTopRebar = ChkEnableTop.IsChecked == true;
            config.TopHookXMm = ParseDouble(TxtTopHookX.Text, 400.0);
            config.TopHookYMm = ParseDouble(TxtTopHookY.Text, 400.0);
            config.TopSpacingXMm = ParseDouble(TxtTopSpacingX.Text, 150.0);
            config.TopSpacingYMm = ParseDouble(TxtTopSpacingY.Text, 150.0);
            config.TopHookMode = RebarHookMode.BentLegs;

            config.EnableBottomRebar = ChkEnableBot.IsChecked == true;
            config.BottomHookXMm = ParseDouble(TxtBotHookX.Text, 450.0);
            config.BottomHookYMm = ParseDouble(TxtBotHookY.Text, 450.0);
            config.BottomSpacingXMm = ParseDouble(TxtBotSpacingX.Text, 150.0);
            config.BottomSpacingYMm = ParseDouble(TxtBotSpacingY.Text, 150.0);
            config.BottomHookMode = RebarHookMode.BentLegs;

            config.EnableSideTies = ChkEnableSide.IsChecked == true;
            config.SideTieQuantity = (int)ParseDouble(TxtSideQty.Text, 3.0);
            config.SideTieLapLengthMm = 600.0;

            if (CmbTopTypeX.SelectedItem is ComboBoxItem tx && long.TryParse(tx.Tag?.ToString(), out long txId)) config.TopBarTypeXId = txId;
            if (CmbTopTypeY.SelectedItem is ComboBoxItem ty && long.TryParse(ty.Tag?.ToString(), out long tyId)) config.TopBarTypeYId = tyId;
            if (CmbBotTypeX.SelectedItem is ComboBoxItem bx && long.TryParse(bx.Tag?.ToString(), out long bxId)) config.BottomBarTypeXId = bxId;
            if (CmbBotTypeY.SelectedItem is ComboBoxItem by && long.TryParse(by.Tag?.ToString(), out long byId)) config.BottomBarTypeYId = byId;
            if (CmbSideType.SelectedItem is ComboBoxItem side && long.TryParse(side.Tag?.ToString(), out long sideId)) config.SideTieBarTypeId = sideId;

            return config;
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            var config = BuildConfig();
            if (config == null) return;

            // Target pile caps
            List<Element> targetCaps;
            if (_preselectedCaps != null && _preselectedCaps.Count > 0)
            {
                targetCaps = _preselectedCaps.ToList(); // clone list
            }
            else
            {
                targetCaps = _elemService.GetStructuralFoundations().Cast<Element>().ToList();
            }

            if (targetCaps.Count == 0)
            {
                MessageBox.Show(
                    "No Structural Foundation pile caps found to reinforce.\n\nPlease click 'Select' to pick foundations in the model.",
                    "No Pile Caps Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            BtnGenerate.IsEnabled = false;
            PrgBar.Visibility = System.Windows.Visibility.Visible;
            TxtStatus.Text = $"Generating 3D rebar for {targetCaps.Count} pile cap(s)...";

            _handler.Action = app =>
            {
                int capsReinforced = 0;
                int totalRebarSetsCreated = 0;
                Exception? error = null;

                try
                {
                    var doc = app.ActiveUIDocument.Document;
                    var rebarService = new RebarGenerationService(doc);
                    using (Transaction trans = new Transaction(doc, "Generate Pile Cap Rebar"))
                    {
                        FailureHandlingOptions failOpt = trans.GetFailureHandlingOptions();
                        failOpt.SetFailuresPreprocessor(new RebarFailurePreprocessor());
                        trans.SetFailureHandlingOptions(failOpt);

                        trans.Start();

                        for (int i = 0; i < targetCaps.Count; i++)
                        {
                            var cap = targetCaps[i];
                            int created = rebarService.GenerateRebarForPileCap(cap, config);
                            if (created > 0)
                            {
                                capsReinforced++;
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
                        TxtStatus.Text = "❌ Error during generation.";
                        MessageBox.Show($"Failed to generate rebar:\n\n{error.Message}\n\n{error.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        TxtStatus.Text = $"Completed: Reinforced {capsReinforced} cap(s) ({totalRebarSetsCreated} rebar sets).";
                        MessageBox.Show(
                            $"Successfully reinforced:\n\n• {capsReinforced} Pile Cap(s)\n• {totalRebarSetsCreated} 3D Rebar Sets Generated\n\n(Bottom U-Bars, Top U-Bars, and Perimeter Side Ties)",
                            "BIM Plus — Reinforcement Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                });
            };
            _exEvent.Raise();
        }
        #endregion
    }

    /// <summary>
    /// Strict selection filter that only allows user to pick Structural Foundation elements.
    /// </summary>
    public class StructuralFoundationSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem == null) return false;
            if (elem is FamilyInstance fi)
            {
                return fi.Category != null && fi.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFoundation;
            }
            return elem.Category != null && elem.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFoundation;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}













