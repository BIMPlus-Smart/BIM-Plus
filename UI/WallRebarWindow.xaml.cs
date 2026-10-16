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
    public partial class WallRebarWindow : Window
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

        public WallRebarWindow(UIApplication uiapp, AutoPileCap.Handlers.GenericEventHandler handler, ExternalEvent exEvent, List<Element>? initialCaps = null)
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
                _preselectedCaps = selectedIds.Select(id => _doc.GetElement(id)).Where(e => e.Category != null && e.Category.Id.Value == (long)BuiltInCategory.OST_Walls).ToList();
            }

            Loaded += WallRebarWindow_Loaded;
        }

        private void WallRebarWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PopulateRebarBarTypes();
            DetectSelectedWall();
            _isInitialized = true;
            RenderPreview();
        }

        private void DetectSelectedWall()
        {
            if (_preselectedCaps.Count > 0)
            {
                var cap = _preselectedCaps[0];
                if (_rebarService.GetWallLocalBounds(cap, out _, out double minX, out double maxX, out double minY, out double maxY, out double minZ, out double maxZ))
                {
                    _capLengthMm = Math.Max(300, (maxX - minX) * GeometryHelper.MmPerFoot);
                    _capWidthMm = Math.Max(300, (maxY - minY) * GeometryHelper.MmPerFoot);
                    _capDepthMm = Math.Max(200, (maxZ - minZ) * GeometryHelper.MmPerFoot);
                }

                _capCoverTopMm = GetCoverMm(cap, BuiltInParameter.CLEAR_COVER_TOP, 50.0);
                _capCoverBotMm = GetCoverMm(cap, BuiltInParameter.CLEAR_COVER_BOTTOM, 100.0);
                _capCoverSideMm = GetCoverMm(cap, BuiltInParameter.CLEAR_COVER_OTHER, 50.0);

                TxtWallName.Text = $"{cap.Name} [{_capLengthMm:F0}x{_capWidthMm:F0}x{_capDepthMm:F0}mm]";
            }
            else
            {
                TxtWallName.Text = "None (Click Select)";
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
            CmbBotTypeX.Items.Clear();
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
                CmbBotTypeX.Items.Add(new ComboBoxItem { Content = displayName, Tag = bar.Id.Value });
                CmbSideType.Items.Add(new ComboBoxItem { Content = displayName, Tag = bar.Id.Value });

                if (diaMm >= 11.0 && diaMm <= 14.0) defaultTopIdx = i;
                if (diaMm >= 15.0 && diaMm <= 18.0) defaultBotIdx = i;
                if (diaMm >= 11.0 && diaMm <= 14.0) defaultSideIdx = i;
            }

            CmbTopTypeX.SelectedIndex = defaultTopIdx;
            CmbBotTypeX.SelectedIndex = defaultBotIdx;
            CmbSideType.SelectedIndex = defaultSideIdx;

            // Load Rebar Shapes
            var shapes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .OrderBy(s => s.Name)
                .ToList();
            
            CmbSideShape.ItemsSource = shapes;
            if (shapes.Count > 0)
            {
                var mt9 = shapes.FirstOrDefault(s => s.Name == "M_T9");
                CmbSideShape.SelectedItem = mt9 ?? shapes[0];
            }
        }

        private void BtnSelectWall_Click(object sender, RoutedEventArgs e)
        {
            _handler.Action = app =>
            {
                try
                {
                    var uidoc = app.ActiveUIDocument;
                    var doc = uidoc.Document;
                    var filter = new StructuralWallSelectionFilter();
                    var refs = uidoc.Selection.PickObjects(
                        ObjectType.Element,
                        filter,
                        "Select Structural Walls (Walls), then click Finish in the top options bar (or press ESC when done)");

                    var targetWalls = refs
                        .Select(r => doc.GetElement(r))
                                                .Where(e => e.Category != null && e.Category.Id.Value == (long)BuiltInCategory.OST_Walls)
                        .ToList();

                    // Update UI on UI thread
                    Dispatcher.Invoke(() =>
                    {
                        _preselectedCaps = targetWalls;
                        DetectSelectedWall();
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

            double topHookX = ParseDouble(TxtTopHookX.Text, 400.0, true);
            double topHookY = topHookX;
            double topSpX = ParseDouble(TxtTopSpacingX.Text, 150.0);
            double topSpY = topSpX;

            double botHookX = ParseDouble(TxtBotHookX.Text, 450.0, true);
            double botHookY = botHookX;
            double botSpX = ParseDouble(TxtBotSpacingX.Text, 150.0);
            double botSpY = botSpX;

            double sideSpacing = ParseDouble(TxtSideSpacing.Text, 150.0);
            int sideQty = (int)Math.Max(1, 1000.0 / sideSpacing);

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
                    DrawBotView(cTop, cBot, cSide, botHookX, botHookY, topSpX, botSpX, botSpY, enableBot);
                    break;
                case "SECTION":
                    DrawSectionView(cTop, cBot, cSide, topHookX, topSpX, topSpY, botHookX, botSpX, botSpY, sideQty, enableTop, enableBot, enableSide);
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
                TxtSumSide.Text = $"{sName} @{sideSpacing}mm";
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
            double sw = _capWidthMm * secScale;
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

            // 1. Vertical Rebar (Orange)
            if (enableTop)
            {
                double ryTop = sy1 + cTopSec;
                double ryBot = sy2 - cBotSec;
                double hookH = Math.Min(topHookX * secScale, (_capWidthMm - cSide * 2) * secScale);

                var vertL = new PointCollection
                {
                    new System.Windows.Point(rx1 + hookH, ryTop),
                    new System.Windows.Point(rx1, ryTop),
                    new System.Windows.Point(rx1, ryBot),
                    new System.Windows.Point(rx1 + hookH, ryBot)
                };
                DrawPolyline(vertL, CreateBrush("#F59E0B"), 2.0);

                var vertR = new PointCollection
                {
                    new System.Windows.Point(rx2 - hookH, ryTop),
                    new System.Windows.Point(rx2, ryTop),
                    new System.Windows.Point(rx2, ryBot),
                    new System.Windows.Point(rx2 - hookH, ryBot)
                };
                DrawPolyline(vertR, CreateBrush("#F59E0B"), 2.0);
            }

            // 2. Horizontal Rebar (Blue)
            if (enableBot)
            {
                double ryBot = sy2 - cBotSec;
                double ryTop = sy1 + cTopSec;
                
                int dotCountY = (int)Math.Max(2, (ryBot - ryTop) / (botSpY * secScale));
                double dotStepY = (ryBot - ryTop) / dotCountY;

                for (int i = 0; i <= dotCountY; i++)
                {
                    DrawCircle(rx1 + 4, ryTop + i * dotStepY, 3.0, CreateBrush("#0D6EFD"));
                    DrawCircle(rx2 - 4, ryTop + i * dotStepY, 3.0, CreateBrush("#0D6EFD"));
                }
            }

            // 3. Pin Ties (Cyan)
            if (enableSide)
            {
                double ryBot = sy2 - cBotSec;
                double ryTop = sy1 + cTopSec;
                
                int tieCountY = sideQty;
                double stepY = (ryBot - ryTop) / (tieCountY + 1);

                for (int i = 1; i <= tieCountY; i++)
                {
                    double y = ryTop + i * stepY;
                    DrawLine(rx1 + 4, y, rx2 - 4, y, CreateBrush("#06B6D4"), 1.0, new DoubleCollection { 4, 4 });
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
            DrawText($"Wall: {_capLengthMm:F0} x {_capWidthMm:F0} x {_capDepthMm:F0} mm", cx, 62, CreateBrush("#64748B"), 11);

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

            // 2. Horizontal Rebar (Blue)
            if (enableBot)
            {
                var blueBrush = CreateBrush("#0D6EFD");
                int numZ = (int)Math.Max(2, (H - cTop - cBot) / botSpX);
                double stepZ = (H - cTop - cBot) / numZ;

                for (int i = 0; i <= numZ; i++)
                {
                    double z = cBot + i * stepZ;
                    
                    var ptsF = new PointCollection
                    {
                        IsoProject(cSide, cSide + 12, z),
                        IsoProject(L - cSide, cSide + 12, z)
                    };
                    DrawPolyline(ptsF, blueBrush, 1.4);
                    
                    var ptsB = new PointCollection
                    {
                        IsoProject(cSide, W - cSide - 12, z),
                        IsoProject(L - cSide, W - cSide - 12, z)
                    };
                    DrawPolyline(ptsB, blueBrush, 1.4);
                }
            }

            // 1. Vertical Rebar (Orange)
            if (enableTop)
            {
                var orangeBrush = CreateBrush("#F59E0B");
                int numX = (int)Math.Max(2, (L - 2 * cSide) / topSpX);
                double stepX = (L - 2 * cSide) / numX;
                double hHook = Math.Min(topHookX, W - 2 * cSide);

                for (int i = 0; i <= numX; i++)
                {
                    double x = cSide + i * stepX;
                    
                    var ptsF = new PointCollection
                    {
                        IsoProject(x, cSide + hHook, H - cTop),
                        IsoProject(x, cSide, H - cTop),
                        IsoProject(x, cSide, cBot),
                        IsoProject(x, cSide + hHook, cBot)
                    };
                    DrawPolyline(ptsF, orangeBrush, 1.8);

                    var ptsB = new PointCollection
                    {
                        IsoProject(x, W - cSide - hHook, H - cTop),
                        IsoProject(x, W - cSide, H - cTop),
                        IsoProject(x, W - cSide, cBot),
                        IsoProject(x, W - cSide - hHook, cBot)
                    };
                    DrawPolyline(ptsB, orangeBrush, 1.8);
                }
            }

            // 3. Pin Ties (Cyan)
            if (enableSide)
            {
                var cyanBrush = CreateBrush("#06B6D4");
                int numZ = sideQty;
                double stepZ = (H - cTop - cBot) / (numZ + 1);
                
                int numX = (int)Math.Max(2, (L - 2 * cSide) / topSpX);
                double stepX = (L - 2 * cSide) / numX;

                for (int i = 1; i <= numZ; i++)
                {
                    double z = cBot + i * stepZ;
                    
                    for (int j = 1; j < numX; j++)
                    {
                        double x = cSide + j * stepX;
                        var tiePts = new PointCollection
                        {
                            IsoProject(x, cSide, z),
                            IsoProject(x, W - cSide, z)
                        };
                        DrawPolyline(tiePts, cyanBrush, 1.2);
                    }
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

            DrawText("WALL PLAN VIEW (TOP)", cx, 40, CreateBrush("#F59E0B"), 14, isBold: true);
            DrawText($"Length: {_capLengthMm:F0}mm | Thickness: {_capWidthMm:F0}mm", cx, 62, CreateBrush("#64748B"), 11);

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

                var cyanBrush = CreateBrush("#F59E0B"); // Vertical bars (Dots in plan view)
                var blueBrush = CreateBrush("#0D6EFD"); // Horizontal bars (Lines in plan view)

                int countX = (int)Math.Max(2, Math.Round((mx2 - mx1) / (topSpX * planScale)));
                double actSpX = (mx2 - mx1) / countX;

                // Horizontal Bars (Blue Lines)
                DrawLine(mx1, my1, mx2, my1, blueBrush, 1.8);
                DrawLine(mx1, my2, mx2, my2, blueBrush, 1.8);

                // Vertical Bars (Orange Dots)
                for (int i = 0; i <= countX; i++)
                {
                    double x = mx1 + i * actSpX;
                    DrawCircle(x, my1, 3.5, cyanBrush);
                    DrawCircle(x, my2, 3.5, cyanBrush);
                }
            }
            else
            {
                DrawText("Top Rebar is currently DISABLED", cx, planY, CreateBrush("#64748B"), 14);
            }
        }
        #endregion

        #region Mode: BOT View
        private void DrawBotView(double cTop, double cBot, double cSide, double botHookX, double botHookY, double topSpX, double botSpX, double botSpY, bool enableBot)
        {
            double viewW = CanvasBorder.ActualWidth > 300 ? CanvasBorder.ActualWidth : 800;
            double viewH = CanvasBorder.ActualHeight > 300 ? CanvasBorder.ActualHeight : 750;

            double cx = viewW / 2 + 25;
            double planY = viewH / 2 - 40;

            DrawText("WALL ELEVATION VIEW", cx, 40, CreateBrush("#0D6EFD"), 14, isBold: true);
            DrawText($"Length: {_capLengthMm:F0}mm | Height: {_capDepthMm:F0}mm", cx, 62, CreateBrush("#64748B"), 11);

            double planScale = Math.Min(380.0 / _capLengthMm, 340.0 / _capDepthMm);
            double pw = _capLengthMm * planScale;
            double ph = _capDepthMm * planScale;

            double px1 = cx - pw / 2;
            double py1 = planY - ph / 2;
            double px2 = px1 + pw;
            double py2 = py1 + ph;

            // Dimensions
            DrawDimensionLine(px1 - 30, py1, px1 - 30, py2, $"{_capDepthMm:F0}", isVertical: true);
            DrawDimensionLine(px1, py1 - 25, px2, py1 - 25, $"{_capLengthMm:F0}", isVertical: false);

            // Concrete Boundary
            DrawRect(px1, py1, pw, ph, CreateBrush("#F8FAFC"), CreateBrush("#0D6EFD"), 1.5, new DoubleCollection { 4, 3 });

            if (enableBot)
            {
                double csPix = cSide * planScale;
                double ctPix = cTop * planScale;
                double cbPix = cBot * planScale;
                
                double mx1 = px1 + csPix;
                double my1 = py1 + ctPix;
                double mx2 = px2 - csPix;
                double my2 = py2 - cbPix;

                var blueBrush = CreateBrush("#0D6EFD"); // Horizontal bars
                var orangeBrush = CreateBrush("#F59E0B"); // Vertical bars

                int countZ = (int)Math.Max(2, Math.Round((my2 - my1) / (botSpX * planScale)));
                double actSpZ = (my2 - my1) / countZ;

                // Horizontal Bars
                for (int i = 0; i <= countZ; i++)
                {
                    double y = my1 + i * actSpZ;
                    DrawLine(mx1, y, mx2, y, blueBrush, 1.8);
                }

                int countX = (int)Math.Max(2, Math.Round((mx2 - mx1) / (topSpX * planScale)));
                double actSpX = (mx2 - mx1) / countX;

                // Vertical Bars
                for (int i = 0; i <= countX; i++)
                {
                    double x = mx1 + i * actSpX;
                    DrawLine(x, my1, x, my2, orangeBrush, 1.4);
                }
            }
            else
            {
                DrawText("Bottom Rebar is currently DISABLED", cx, planY, CreateBrush("#64748B"), 14);
            }
        }
        #endregion

        #region Mode: Detailed SECTION View
        private void DrawSectionView(double cTop, double cBot, double cSide, double topHookX, double topSpX, double topSpY,
                                     double botHookX, double botSpX, double botSpY, int sideQty,
                                     bool enableTop, bool enableBot, bool enableSide)
        {
            double viewW = CanvasBorder.ActualWidth > 300 ? CanvasBorder.ActualWidth : 800;
            double viewH = CanvasBorder.ActualHeight > 300 ? CanvasBorder.ActualHeight : 750;

            double cx = viewW / 2 + 25;
            double cy = viewH / 2;

            DrawText("DETAILED ELEVATION SECTION VIEW", cx - 130, 40, CreateBrush("#0F172A"), 14, isBold: true);

            double scale = Math.Min(320.0 / _capWidthMm, 500.0 / _capDepthMm);
            double sw = _capWidthMm * scale;
            double sh = _capDepthMm * scale;

            double sx1 = cx - sw / 2;
            double sy1 = cy - sh / 2;
            double sx2 = sx1 + sw;
            double sy2 = sy1 + sh;

            // Dimensions
            DrawDimensionLine(sx1 - 40, sy1, sx1 - 40, sy2, $"{_capDepthMm:F0}", isVertical: true);
            DrawDimensionLine(sx1, sy2 + 35, sx2, sy2 + 35, $"{_capWidthMm:F0}", isVertical: false);

            // Concrete Rectangle
            DrawRect(sx1, sy1, sw, sh, CreateBrush("#F8FAFC"), CreateBrush("#334155"), 2.0);

            // Concrete Cover Guides & Callouts
            double csPix = cSide * scale;
            double cbPix = cBot * scale;
            double ctPix = cTop * scale;

            double rx1 = sx1 + csPix;
            double rx2 = sx2 - csPix;

            // 1. Vertical Rebar (Orange)
            if (enableTop)
            {
                double ryTop = sy1 + ctPix;
                double ryBot = sy2 - cbPix;
                double hookH = Math.Min(topHookX * scale, (_capWidthMm - cSide * 2) * scale);
                
                var vertL = new PointCollection
                {
                    new System.Windows.Point(rx1 + hookH, ryTop),
                    new System.Windows.Point(rx1, ryTop),
                    new System.Windows.Point(rx1, ryBot),
                    new System.Windows.Point(rx1 + hookH, ryBot)
                };
                DrawPolyline(vertL, CreateBrush("#F59E0B"), 2.4);
                
                var vertR = new PointCollection
                {
                    new System.Windows.Point(rx2 - hookH, ryTop),
                    new System.Windows.Point(rx2, ryTop),
                    new System.Windows.Point(rx2, ryBot),
                    new System.Windows.Point(rx2 - hookH, ryBot)
                };
                DrawPolyline(vertR, CreateBrush("#F59E0B"), 2.4);
                
                DrawText($"Hook = {topHookX:F0}mm", rx1 + 20, ryTop - 12, CreateBrush("#F59E0B"), 11, isBold: true);
                DrawText($"Hook = {topHookX:F0}mm", rx1 + 20, ryBot + 12, CreateBrush("#F59E0B"), 11, isBold: true);
            }

            // 2. Horizontal Rebar (Blue)
            if (enableBot)
            {
                double ryBot = sy2 - cbPix;
                double ryTop = sy1 + ctPix;
                
                int dotCountY = (int)Math.Max(2, (ryBot - ryTop) / (botSpY * scale));
                double dotStepY = (ryBot - ryTop) / dotCountY;
                
                for (int i = 0; i <= dotCountY; i++)
                {
                    DrawCircle(rx1 + 6, ryTop + i * dotStepY, 4.0, CreateBrush("#0D6EFD"));
                    DrawCircle(rx2 - 6, ryTop + i * dotStepY, 4.0, CreateBrush("#0D6EFD"));
                }
            }

            // 3. Pin Ties (Cyan)
            if (enableSide)
            {
                double ryBot = sy2 - cbPix;
                double ryTop = sy1 + ctPix;
                
                int tieCountY = sideQty;
                double stepY = (ryBot - ryTop) / (tieCountY + 1);

                for (int i = 1; i <= tieCountY; i++)
                {
                    double y = ryTop + i * stepY;
                    DrawLine(rx1 + 6, y, rx2 - 6, y, CreateBrush("#06B6D4"), 1.2, new DoubleCollection { 4, 4 });
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

        private static double ParseDouble(string str, double defaultVal, bool allowZero = false)
        {
            if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
            {
                if (allowZero && val >= 0) return val;
                if (!allowZero && val > 0) return val;
            }
            return defaultVal;
        }
        #endregion
        #endregion

        #region Rebar Generation Execution
        private WallRebarConfig? BuildConfig()
        {
            var config = new WallRebarConfig();
            config.CoverTopMm = 75.0;
            config.CoverBottomMm = 150.0;
            config.CoverSideMm = 75.0;

            config.EnableTopRebar = ChkEnableTop.IsChecked == true;
            config.TopHookXMm = ParseDouble(TxtTopHookX.Text, 400.0, true);
            config.TopHookYMm = config.TopHookXMm;
            config.TopSpacingXMm = ParseDouble(TxtTopSpacingX.Text, 150.0);
            config.TopSpacingYMm = config.TopSpacingXMm;
            config.TopHookMode = RebarHookMode.BentLegs;

            config.EnableBottomRebar = ChkEnableBot.IsChecked == true;
            config.BottomHookXMm = ParseDouble(TxtBotHookX.Text, 450.0, true);
            config.BottomHookYMm = config.BottomHookXMm;
            config.BottomSpacingXMm = ParseDouble(TxtBotSpacingX.Text, 150.0);
            config.BottomSpacingYMm = config.BottomSpacingXMm;
            config.BottomHookMode = RebarHookMode.BentLegs;

            config.EnableSideTies = ChkEnableSide.IsChecked == true;
            config.SideTieSpacingMm = ParseDouble(TxtSideSpacing.Text, 150.0);
            config.SideTieLapLengthMm = 600.0;

            if (CmbTopTypeX.SelectedItem is ComboBoxItem tx && long.TryParse(tx.Tag?.ToString(), out long txId)) { config.TopBarTypeXId = txId; config.TopBarTypeYId = txId; }
            if (CmbBotTypeX.SelectedItem is ComboBoxItem bx && long.TryParse(bx.Tag?.ToString(), out long bxId)) { config.BottomBarTypeXId = bxId; config.BottomBarTypeYId = bxId; }
            if (CmbSideType.SelectedItem is ComboBoxItem side && long.TryParse(side.Tag?.ToString(), out long sideId)) config.SideTieBarTypeId = sideId;
            if (CmbSideShape.SelectedItem is RebarShape sideShape) config.SideTieShapeId = sideShape.Id.Value;

            return config;
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            var config = BuildConfig();
            if (config == null) return;

            // Target Walls
            List<Element> targetWalls;
            if (_preselectedCaps != null && _preselectedCaps.Count > 0)
            {
                targetWalls = _preselectedCaps.ToList(); // clone list
            }
            else
            {
                targetWalls = _elemService.GetStructuralWalls().Cast<Element>().ToList();
            }

            if (targetWalls.Count == 0)
            {
                MessageBox.Show(
                    "No Structural Wall Walls found to reinforce.\n\nPlease click 'Select' to pick Walls in the model.",
                    "No Walls Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            BtnGenerate.IsEnabled = false;
            PrgBar.Visibility = System.Windows.Visibility.Visible;
            TxtStatus.Text = $"Generating 3D rebar for {targetWalls.Count} Wall(s)...";

            _handler.Action = app =>
            {
                int capsReinforced = 0;
                int totalRebarSetsCreated = 0;
                Exception? error = null;

                try
                {
                    var doc = app.ActiveUIDocument.Document;
                    var rebarService = new RebarGenerationService(doc);
                    using (Transaction trans = new Transaction(doc, "Generate Wall Rebar"))
                    {
                        FailureHandlingOptions failOpt = trans.GetFailureHandlingOptions();
                        failOpt.SetFailuresPreprocessor(new RebarFailurePreprocessor());
                        trans.SetFailureHandlingOptions(failOpt);

                        trans.Start();

                        for (int i = 0; i < targetWalls.Count; i++)
                        {
                            var cap = targetWalls[i];
                            int created = rebarService.GenerateRebarForWall(cap, config);
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
                            $"Successfully reinforced:\n\n• {capsReinforced} Wall(s)\n• {totalRebarSetsCreated} 3D Rebar Sets Generated",
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
    /// Strict selection filter that only allows user to pick Structural Wall elements.
    /// </summary>
    public class StructuralWallSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem == null) return false;
            if (elem is FamilyInstance fi)
            {
                return fi.Category != null && fi.Category.Id.Value == (long)BuiltInCategory.OST_Walls;
            }
            return elem.Category != null && elem.Category.Id.Value == (long)BuiltInCategory.OST_Walls;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}















