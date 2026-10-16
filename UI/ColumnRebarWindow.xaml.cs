using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AutoPileCap.Services;

namespace AutoPileCap.UI
{
    public class CustomInnerTie
    {
        public int MinX { get; set; }
        public int MaxX { get; set; }
        public int MinY { get; set; }
        public int MaxY { get; set; }
    }

    public partial class ColumnRebarWindow : Window
    {
        private UIApplication _uiapp;
        private Document _doc;
        private List<FamilyInstance> _columns;
        private AutoPileCap.Handlers.GenericEventHandler _handler;
        private Autodesk.Revit.UI.ExternalEvent _exEvent;
        private bool _isInitialized = false;

        // View State
        private string _currentViewMode = "3D";
        private double _zoom = 1.0;
        private double _panX = 0;
        private double _panY = 0;
        private bool _isPanning = false;
        private System.Windows.Point _panStartPoint;
        private bool _isOrbiting = false;
        
        private bool _isSelecting = false;
        private System.Windows.Point _selectStartPoint;
        private System.Windows.Point _selectCurrentPoint;
        private List<CustomInnerTie> _customInnerTies = new List<CustomInnerTie>();
        private System.Windows.Point _orbitStartPoint;
        private double _viewAngleZ = -Math.PI / 4;
        private double _viewAngleX = Math.PI / 6;

        public ColumnRebarWindow(UIApplication uiapp, AutoPileCap.Handlers.GenericEventHandler handler, Autodesk.Revit.UI.ExternalEvent exEvent, List<FamilyInstance> columns)
        {
            InitializeComponent();
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;
            _handler = handler;
            _exEvent = exEvent;
            _columns = columns ?? new List<FamilyInstance>();

            LoadData();
            UpdateSelectionText();

            Loaded += (s, e) => {
                _isInitialized = true;
                UpdateViewModeButtons();
                ResetZoomAndPan();
                RenderPreview();
            };
        }

        private void UpdateSelectionText()
        {
            if (_columns.Count == 1)
            {
                var col = _columns[0];
                string typeName = col.Name;
                
                Parameter markParam = col.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                string mark = markParam != null ? markParam.AsString() : null;

                if (!string.IsNullOrWhiteSpace(mark) && !typeName.StartsWith(mark))
                {
                    txtSelectedCount.Text = $"{mark} ({typeName})";
                }
                else
                {
                    txtSelectedCount.Text = typeName;
                }
            }
            else if (_columns.Count > 1)
            {
                var names = _columns.Select(c => 
                {
                    Parameter mParam = c.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    string m = mParam != null ? mParam.AsString() : null;
                    return !string.IsNullOrWhiteSpace(m) ? m : c.Name;
                }).ToList();
                
                string combined = string.Join(", ", names);
                if (combined.Length > 30)
                {
                    txtSelectedCount.Text = $"{_columns.Count} Cols: " + combined.Substring(0, 27) + "...";
                }
                else
                {
                    txtSelectedCount.Text = combined;
                }
            }
            else
            {
                txtSelectedCount.Text = "None (Click Select)";
            }
        }

        private void LoadData()
        {
            // Load Rebar Bar Types
            var barTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .OrderBy(b => b.Name)
                .ToList();

            // Load Hook Types
            var rawHookTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .OrderBy(h => h.Name)
                .ToList();

            var hookTypes = new List<RebarHookItem>();
            hookTypes.Add(new RebarHookItem { Id = ElementId.InvalidElementId, Name = "None" });
            hookTypes.Add(new RebarHookItem { Id = ElementId.InvalidElementId, Name = "Crank" });
            hookTypes.AddRange(rawHookTypes.Select(h => new RebarHookItem { Id = h.Id, Name = h.Name }));

            // Load End Treatments (Cranks)
            var endTreatmentsRaw = new FilteredElementCollector(_doc)
                .OfClass(typeof(EndTreatmentType))
                .Cast<EndTreatmentType>()
                .OrderBy(e => e.Name)
                .ToList();

            var endTreatments = new List<RebarHookItem>();
            endTreatments.Add(new RebarHookItem { Id = ElementId.InvalidElementId, Name = "None" });
            endTreatments.AddRange(endTreatmentsRaw.Select(e => new RebarHookItem { Id = e.Id, Name = e.Name }));

            // Load Rebar Shapes
            var shapes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .OrderBy(s => s.Name)
                .ToList();

            // Bind to ComboBoxes
            cmbVerticalBarType.ItemsSource = barTypes;
            cmbTieBarType.ItemsSource = barTypes;
            
            cmbTopHook.ItemsSource = hookTypes;
            cmbBottomHook.ItemsSource = hookTypes;
            cmbTieHookType.ItemsSource = hookTypes;
            cmbTopCrank.ItemsSource = endTreatments;
            cmbBottomCrank.ItemsSource = endTreatments;

            if (barTypes.Count > 0)
            {
                cmbVerticalBarType.SelectedIndex = 0;
                cmbTieBarType.SelectedIndex = 0;
            }

            if (hookTypes.Count > 0)
            {
                cmbTopHook.SelectedIndex = 0;
                cmbBottomHook.SelectedIndex = 0;
                cmbTieHookType.SelectedIndex = 0;
            }

            var shapeItems = shapes.Select(s => new RebarShapeItem 
            { 
                Id = s.Id, 
                Name = s.Name, 
                Geometry = RebarShapeWpfRenderer.GetShapeGeometry(s) 
            }).Where(i => i.Geometry != null).ToList();

            cmbTieShape.ItemsSource = shapes;
            cmbInnerTieShape.ItemsSource = shapes;
            lstShapeBrowser.ItemsSource = shapeItems;

            if (shapes.Count > 0)
            {
                var defaultShape = shapes.FirstOrDefault(s => s.Name.Contains("M_T1")) ?? shapes[0];
                cmbTieShape.SelectedItem = defaultShape;
                cmbInnerTieShape.SelectedItem = defaultShape;
                lstShapeBrowser.SelectedValue = defaultShape.Id;
            }

            cmbTieShape.SelectionChanged += (s, e) => { if (cmbTieShape.SelectedValue != null) lstShapeBrowser.SelectedValue = cmbTieShape.SelectedValue; if (_isInitialized) RenderPreview(); };
            cmbInnerTieShape.SelectionChanged += (s, e) => { if (_isInitialized) RenderPreview(); };
            lstShapeBrowser.SelectionChanged += (s, e) => { if (lstShapeBrowser.SelectedValue != null) cmbTieShape.SelectedValue = lstShapeBrowser.SelectedValue; if (_isInitialized) RenderPreview(); };
            cmbTieHookType.SelectionChanged += (s, e) => { if (_isInitialized) RenderPreview(); };

            if (endTreatmentsRaw.Count > 0)
            {
                cmbTopCrank.SelectedIndex = 0;
                cmbBottomCrank.SelectedIndex = 0;
            }
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate inputs
                if (!int.TryParse(txtNx.Text, out int nx) || nx < 2)
                    throw new Exception("Quantity along Width (B) must be at least 2.");
                    
                if (!int.TryParse(txtNy.Text, out int ny) || ny < 2)
                    throw new Exception("Quantity along Depth (H) must be at least 2.");

                if (!double.TryParse(txtTieSpacing.Text, out double tieSpacing) || tieSpacing <= 0)
                    throw new Exception("Invalid Tie Spacing.");
                    
                double innerTieSpacing = tieSpacing;
                if (chkInnerTies.IsChecked == true)
                {
                    if (!double.TryParse(txtInnerTieSpacing.Text, out innerTieSpacing) || innerTieSpacing <= 0)
                        throw new Exception("Invalid Inner Tie Spacing.");
                }
                if (!double.TryParse(txtLapFactor.Text, out double lapFactor) || lapFactor <= 0)
                    throw new Exception("Invalid Lap Splice Factor.");

                var verticalBarType = cmbVerticalBarType.SelectedItem as RebarBarType;
                var tieBarType = cmbTieBarType.SelectedItem as RebarBarType;
                var hookTypeId = cmbTieHookType.SelectedValue as ElementId;
                var tieShape = cmbTieShape.SelectedItem as RebarShape;
                var innerTieShape = cmbInnerTieShape.SelectedItem as RebarShape;
                var topCrankId = cmbTopCrank.SelectedValue as ElementId;
                var bottomCrankId = cmbBottomCrank.SelectedValue as ElementId;

                var topHookItem = cmbTopHook.SelectedItem as RebarHookItem;
                var bottomHookItem = cmbBottomHook.SelectedItem as RebarHookItem;
                
                var topHookId = cmbTopHook.SelectedValue as ElementId;
                var bottomHookId = cmbBottomHook.SelectedValue as ElementId;
                
                bool topIsCrank = topHookItem?.Name == "Crank";
                bool bottomIsCrank = bottomHookItem?.Name == "Crank";

                if (verticalBarType == null || tieBarType == null || hookTypeId == null)
                    throw new Exception("Please select valid rebar types.");

                // Gather config
                var config = new ColumnRebarConfig
                {
                    Nx = nx,
                    Ny = ny,
                    VerticalBarTypeId = verticalBarType.Id,
                    TieBarTypeId = tieBarType.Id,
                    TopHookTypeId = topIsCrank ? null : topHookId,
                    BottomHookTypeId = bottomIsCrank ? null : bottomHookId,
                    TopIsCrank = topIsCrank,
                    BottomIsCrank = bottomIsCrank,
                    HookTypeId = hookTypeId,
                    TieShapeId = tieShape?.Id,
                    TopCrankId = topCrankId,
                    BottomCrankId = bottomCrankId,
                    TieSpacingMm = tieSpacing,
                    InnerTieSpacingMm = innerTieSpacing,
                    InnerTieShapeId = innerTieShape?.Id,
                    GenerateInnerTies = chkInnerTies.IsChecked == true,
                    LapFactor = lapFactor,
                    ExtendDowels = chkDowels.IsChecked == true,
                    CustomInnerTies = _customInnerTies.ToList()
                };

                // Generate Rebar via ExternalEvent
                _handler.Action = (uiapp) =>
                {
                    Document doc = uiapp.ActiveUIDocument.Document;
                    using (Transaction t = new Transaction(doc, "Generate Column Rebar"))
                    {
                        t.Start();
                        foreach (var column in _columns)
                        {
                            ColumnRebarGenerationService.Generate(doc, column, config);
                        }
                        t.Commit();
                    }
                    
                    // Cannot use MessageBox from worker thread cleanly without Dispatcher, 
                    // but we can let the UI know or just finish silently, similar to Pile Cap.
                };
                _exEvent.Raise();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnSelectColumn_Click(object sender, RoutedEventArgs e)
        {
            _handler.Action = app =>
            {
                try
                {
                    var uidoc = app.ActiveUIDocument;
                    var doc = uidoc.Document;
                    
                    var filter = new StructuralColumnSelectionFilter();
                    var refs = uidoc.Selection.PickObjects(
                        ObjectType.Element,
                        filter,
                        "Select Structural Columns, then click Finish in the top options bar (or press ESC when done)");

                    var targetColumns = refs
                        .Select(r => doc.GetElement(r) as FamilyInstance)
                        .Where(c => c != null && c.Category != null && c.Category.Id.Value == (long)BuiltInCategory.OST_StructuralColumns)
                        .ToList();

                    // Update UI on UI thread
                    Dispatcher.Invoke(() =>
                    {
                        _columns = targetColumns;
                        UpdateSelectionText();
                    });
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // User cancelled with ESC
                }
            };
            _exEvent.Raise();
        }
        private void ChkInnerTies_Changed(object sender, RoutedEventArgs e)
        {
            if (pnlInnerTies != null)
                pnlInnerTies.Visibility = (chkInnerTies.IsChecked == true) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            
            RenderPreview();
        }

        private void BtnViewMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag != null)
            {
                _currentViewMode = btn.Tag.ToString() ?? "3D";
                UpdateViewModeButtons();
                ResetZoomAndPan();
                RenderPreview();
            }
        }

        private void UpdateViewModeButtons()
        {
            var buttons = new[] { BtnView3D, BtnViewTop, BtnViewBot, BtnViewSection };
            var activeBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(13, 110, 253));
            var activeFg = System.Windows.Media.Brushes.White;
            var normalBg = System.Windows.Media.Brushes.Transparent;
            var normalFg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139));

            foreach (var b in buttons)
            {
                if (b.Tag.ToString() == _currentViewMode)
                {
                    b.Background = activeBg;
                    b.Foreground = activeFg;
                }
                else
                {
                    b.Background = normalBg;
                    b.Foreground = normalFg;
                }
            }
        }

        private void ResetZoomAndPan()
        {
            _zoom = 1.0;
            _panX = 0;
            _panY = 0;
            _viewAngleZ = -Math.PI / 4;
            _viewAngleX = Math.PI / 6;
        }

        private void CanvasBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isInitialized) RenderPreview();
        }

        private void TxtNxNy_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _customInnerTies.Clear();
            if (_isInitialized) RenderPreview();
        }

        private void Canvas_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 1.15 : 0.87;
            System.Windows.Point mousePos = e.GetPosition(CanvasBorder);
            _panX = mousePos.X - (mousePos.X - _panX) * zoomFactor;
            _panY = mousePos.Y - (mousePos.Y - _panY) * zoomFactor;
            _zoom *= zoomFactor;
            RenderPreview();
        }

        private void Canvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) || System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift) || e.ChangedButton == System.Windows.Input.MouseButton.Right || e.ChangedButton == System.Windows.Input.MouseButton.Middle)
            {
                if (_currentViewMode == "3D")
                {
                    _isOrbiting = true;
                    _orbitStartPoint = e.GetPosition(CanvasBorder);
                    CanvasBorder.CaptureMouse();
                }
                else
                {
                    _isPanning = true;
                    _panStartPoint = e.GetPosition(CanvasBorder);
                    CanvasBorder.CaptureMouse();
                }
            }
            else if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                if (_currentViewMode == "SECTION" && chkInnerTies?.IsChecked == true)
                {
                    _isSelecting = true;
                    _selectStartPoint = e.GetPosition(DrawingCanvas);
                    _selectCurrentPoint = _selectStartPoint;
                    CanvasBorder.CaptureMouse();
                }
                else
                {
                    _isPanning = true;
                    _panStartPoint = e.GetPosition(CanvasBorder);
                    CanvasBorder.CaptureMouse();
                }
            }
        }

        private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isOrbiting)
            {
                System.Windows.Point currentPoint = e.GetPosition(CanvasBorder);
                double deltaX = currentPoint.X - _orbitStartPoint.X;
                double deltaY = currentPoint.Y - _orbitStartPoint.Y;
                _viewAngleZ += deltaX * 0.01;
                _viewAngleX += deltaY * 0.01;
                _viewAngleX = Math.Max(-Math.PI / 2, Math.Min(Math.PI / 2, _viewAngleX));
                _orbitStartPoint = currentPoint;
                RenderPreview();
            }
            else if (_isSelecting)
            {
                _selectCurrentPoint = e.GetPosition(DrawingCanvas);
                RenderPreview();
            }
            else if (_isPanning)
            {
                System.Windows.Point currentPoint = e.GetPosition(CanvasBorder);
                _panX += (currentPoint.X - _panStartPoint.X);
                _panY += (currentPoint.Y - _panStartPoint.Y);
                _panStartPoint = currentPoint;
                RenderPreview();
            }
        }

        private void Canvas_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isOrbiting)
            {
                _isOrbiting = false;
                CanvasBorder.ReleaseMouseCapture();
            }
            if (_isPanning)
            {
                _isPanning = false;
                CanvasBorder.ReleaseMouseCapture();
            }
            if (_isSelecting)
            {
                _isSelecting = false;
                CanvasBorder.ReleaseMouseCapture();
                ProcessSelectionBox();
                RenderPreview();
            }
        }

        private void ProcessSelectionBox()
        {
            if (_currentViewMode != "SECTION") return;

            int nx = 2;
            if (txtNx != null) int.TryParse(txtNx.Text, out nx);
            if (nx < 2) nx = 2;
            
            int ny = 2;
            if (txtNy != null) int.TryParse(txtNy.Text, out ny);
            if (ny < 2) ny = 2;

            double w = CanvasBorder.ActualWidth;
            double h = CanvasBorder.ActualHeight;
            if (w == 0 || h == 0) return;

            var cx = w / 2 + _panX;
            var cy = h / 2 + _panY;
            var scale = _zoom * 100;
            double colW = scale * 1.5;
            double colH = scale * 1.5;
            double stW = colW * 0.8;
            double stH = colH * 0.8;

            double spacingX = nx > 1 ? stW / (nx - 1) : 0;
            double spacingY = ny > 1 ? stH / (ny - 1) : 0;

            double minSelX = Math.Min(_selectStartPoint.X, _selectCurrentPoint.X);
            double maxSelX = Math.Max(_selectStartPoint.X, _selectCurrentPoint.X);
            double minSelY = Math.Min(_selectStartPoint.Y, _selectCurrentPoint.Y);
            double maxSelY = Math.Max(_selectStartPoint.Y, _selectCurrentPoint.Y);

            int minCol = int.MaxValue;
            int maxCol = int.MinValue;
            int minRow = int.MaxValue;
            int maxRow = int.MinValue;
            bool foundAny = false;

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    if (i > 0 && i < nx - 1 && j > 0 && j < ny - 1) continue;
                    
                    double px = cx - stW / 2 + i * spacingX;
                    double py = cy - stH / 2 + j * spacingY;

                    if (px >= minSelX && px <= maxSelX && py >= minSelY && py <= maxSelY)
                    {
                        if (i < minCol) minCol = i;
                        if (i > maxCol) maxCol = i;
                        if (j < minRow) minRow = j;
                        if (j > maxRow) maxRow = j;
                        foundAny = true;
                    }
                }
            }

            if (foundAny)
            {
                // If it's just a single click (minCol == maxCol && minRow == maxRow), we could ignore it, 
                // but let's just let it create a 1x1 tie which will be skipped or drawn small.
                _customInnerTies.Add(new CustomInnerTie { MinX = minCol, MaxX = maxCol, MinY = minRow, MaxY = maxRow });
            }
        }

        private void Canvas_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_currentViewMode == "SECTION")
            {
                _customInnerTies.Clear();
                RenderPreview();
            }
        }

        private System.Windows.Media.Geometry GetCanvasTieGeometry(RebarShape shape, double stW, double stH)
        {
            if (shape == null) return null;
            System.Windows.Media.PathGeometry pathGeo = new System.Windows.Media.PathGeometry();
            IList<Curve> curves;
            try { curves = shape.GetCurvesForBrowser(); }
            catch { return null; }

            if (curves == null || curves.Count == 0) return null;

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

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

            foreach (var pts in discretizedCurves)
            {
                if (pts.Count < 2) continue;
                System.Windows.Media.PathFigure figure = new System.Windows.Media.PathFigure();
                
                System.Windows.Point MapPoint(System.Windows.Point p)
                {
                    double nx = width > 0 ? (p.X - minX) / width : 0.5;
                    double ny = height > 0 ? (p.Y - minY) / height : 0.5;
                    return new System.Windows.Point(-stW / 2 + nx * stW, stH / 2 - ny * stH);
                }

                List<System.Windows.Point> mappedPts = new List<System.Windows.Point>();
                foreach(var pt in pts) {
                    var mp = MapPoint(pt);
                    if (mappedPts.Count == 0 || (Math.Abs(mappedPts.Last().X - mp.X) > 0.1 || Math.Abs(mappedPts.Last().Y - mp.Y) > 0.1))
                        mappedPts.Add(mp);
                }

                if (mappedPts.Count < 2) continue;

                double radius = 10.0; // Corner radius for the tie

                System.Windows.Point GetPointAtDist(System.Windows.Point p1, System.Windows.Point p2, double dist)
                {
                    double dx = p2.X - p1.X;
                    double dy = p2.Y - p1.Y;
                    double len = Math.Sqrt(dx*dx + dy*dy);
                    if (len <= dist) return p2;
                    return new System.Windows.Point(p1.X + dx * (dist/len), p1.Y + dy * (dist/len));
                }

                bool isClosed = (Math.Abs(mappedPts[0].X - mappedPts.Last().X) < 0.1 && Math.Abs(mappedPts[0].Y - mappedPts.Last().Y) < 0.1);
                
                if (isClosed && mappedPts.Count > 2)
                {
                    List<System.Windows.Point> corners = mappedPts.Take(mappedPts.Count - 1).ToList();
                    System.Windows.Point firstArcStart = new System.Windows.Point();
                    
                    for (int i = 0; i < corners.Count; i++)
                    {
                        System.Windows.Point pPrev = corners[(i - 1 + corners.Count) % corners.Count];
                        System.Windows.Point pCurr = corners[i];
                        System.Windows.Point pNext = corners[(i + 1) % corners.Count];
                        
                        double distPrev = Math.Sqrt(Math.Pow(pCurr.X - pPrev.X, 2) + Math.Pow(pCurr.Y - pPrev.Y, 2));
                        double distNext = Math.Sqrt(Math.Pow(pNext.X - pCurr.X, 2) + Math.Pow(pNext.Y - pCurr.Y, 2));
                        
                        double r = Math.Min(radius, Math.Min(distPrev / 2.1, distNext / 2.1));
                        
                        System.Windows.Point arcStart = GetPointAtDist(pCurr, pPrev, r);
                        System.Windows.Point arcEnd = GetPointAtDist(pCurr, pNext, r);
                        
                        if (i == 0)
                        {
                            firstArcStart = arcStart;
                            figure.StartPoint = arcStart;
                        }
                        else
                        {
                            figure.Segments.Add(new System.Windows.Media.LineSegment(arcStart, true));
                        }
                        
                        if (r > 1.0)
                        {
                            double cross = (pCurr.X - pPrev.X) * (pNext.Y - pCurr.Y) - (pCurr.Y - pPrev.Y) * (pNext.X - pCurr.X);
                            System.Windows.Media.SweepDirection dir = cross > 0 ? System.Windows.Media.SweepDirection.Clockwise : System.Windows.Media.SweepDirection.Counterclockwise;
                            figure.Segments.Add(new System.Windows.Media.ArcSegment(arcEnd, new System.Windows.Size(r, r), 0, false, dir, true));
                        }
                        else
                        {
                            figure.Segments.Add(new System.Windows.Media.LineSegment(pCurr, true));
                        }
                    }
                    figure.Segments.Add(new System.Windows.Media.LineSegment(firstArcStart, true));
                    figure.IsClosed = true;
                }
                else
                {
                    figure.StartPoint = mappedPts[0];
                    for (int i = 0; i < mappedPts.Count; i++)
                    {
                        if (i == 0) continue;
                        
                        if (i < mappedPts.Count - 1)
                        {
                            System.Windows.Point pPrev = mappedPts[i - 1];
                            System.Windows.Point pCurr = mappedPts[i];
                            System.Windows.Point pNext = mappedPts[i + 1];
                            
                            double distPrev = Math.Sqrt(Math.Pow(pCurr.X - pPrev.X, 2) + Math.Pow(pCurr.Y - pPrev.Y, 2));
                            double distNext = Math.Sqrt(Math.Pow(pNext.X - pCurr.X, 2) + Math.Pow(pNext.Y - pCurr.Y, 2));
                            
                            double r = Math.Min(radius, Math.Min(distPrev / 2.1, distNext / 2.1));
                            
                            if (r > 1.0)
                            {
                                System.Windows.Point arcStart = GetPointAtDist(pCurr, pPrev, r);
                                System.Windows.Point arcEnd = GetPointAtDist(pCurr, pNext, r);
                                
                                figure.Segments.Add(new System.Windows.Media.LineSegment(arcStart, true));
                                
                                double cross = (pCurr.X - pPrev.X) * (pNext.Y - pCurr.Y) - (pCurr.Y - pPrev.Y) * (pNext.X - pCurr.X);
                                System.Windows.Media.SweepDirection dir = cross > 0 ? System.Windows.Media.SweepDirection.Clockwise : System.Windows.Media.SweepDirection.Counterclockwise;
                                
                                figure.Segments.Add(new System.Windows.Media.ArcSegment(arcEnd, new System.Windows.Size(r, r), 0, false, dir, true));
                            }
                            else
                            {
                                figure.Segments.Add(new System.Windows.Media.LineSegment(pCurr, true));
                            }
                        }
                        else
                        {
                            figure.Segments.Add(new System.Windows.Media.LineSegment(mappedPts[i], true));
                        }
                    }
                }
                pathGeo.Figures.Add(figure);
            }

            return pathGeo;
        }

        private void RenderPreview()
        {
            if (!_isInitialized || DrawingCanvas == null) return;
            DrawingCanvas.Children.Clear();
            
            double w = CanvasBorder.ActualWidth;
            double h = CanvasBorder.ActualHeight;
            if (w == 0 || h == 0) return;

            // Basic transform
            var cx = w / 2 + _panX;
            var cy = h / 2 + _panY;
            var scale = _zoom * 100; // 100px base size

            System.Windows.Media.Brush fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 13, 110, 253));
            System.Windows.Media.Brush stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(13, 110, 253));
            System.Windows.Media.Brush tieBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)); // LimeGreen
            System.Windows.Media.Brush rebarBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38)); // Red

            if (_currentViewMode == "SECTION" || _currentViewMode == "TOP" || _currentViewMode == "BOT")
            {
                // Draw 2D Square for Column
                double colW = scale * 1.5;
                double colH = scale * 1.5;
                
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = colW,
                    Height = colH,
                    Fill = fill,
                    Stroke = stroke,
                    StrokeThickness = 2
                };
                System.Windows.Controls.Canvas.SetLeft(rect, cx - colW / 2);
                System.Windows.Controls.Canvas.SetTop(rect, cy - colH / 2);
                DrawingCanvas.Children.Add(rect);

                // Draw Stirrup
                double stW = colW * 0.8;
                double stH = colH * 0.8;
                
                var selectedShape = cmbTieShape.SelectedItem as RebarShape;
                System.Windows.Media.Geometry customGeo = null;
                if (selectedShape != null)
                {
                    customGeo = GetCanvasTieGeometry(selectedShape, stW, stH);
                }

                if (customGeo != null)
                {
                    var stirrupPath = new System.Windows.Shapes.Path
                    {
                        Data = customGeo,
                        Stroke = tieBrush,
                        StrokeThickness = 2
                    };
                    System.Windows.Controls.Canvas.SetLeft(stirrupPath, cx);
                    System.Windows.Controls.Canvas.SetTop(stirrupPath, cy);
                    DrawingCanvas.Children.Add(stirrupPath);
                }
                else
                {
                    var stirrup = new System.Windows.Shapes.Rectangle
                    {
                        Width = stW,
                        Height = stH,
                        Stroke = tieBrush,
                        StrokeThickness = 2,
                        RadiusX = 10,
                        RadiusY = 10
                    };
                    System.Windows.Controls.Canvas.SetLeft(stirrup, cx - stW / 2);
                    System.Windows.Controls.Canvas.SetTop(stirrup, cy - stH / 2);
                    DrawingCanvas.Children.Add(stirrup);
                }

                // Draw Inner Ties if checked
                if (chkInnerTies != null && chkInnerTies.IsChecked == true)
                {
                    int tempNx = 2;
                    if (txtNx != null) int.TryParse(txtNx.Text, out tempNx);
                    if (tempNx < 2) tempNx = 2;
                    
                    int tempNy = 2;
                    if (txtNy != null) int.TryParse(txtNy.Text, out tempNy);
                    if (tempNy < 2) tempNy = 2;

                    double tBarW = stW;
                    double tBarH = stH;

                    var innerShape = cmbInnerTieShape.SelectedItem as RebarShape;

                    if (_customInnerTies.Count > 0)
                    {
                        double spacingX = tempNx > 1 ? tBarW / (tempNx - 1) : 0;
                        double spacingY = tempNy > 1 ? tBarH / (tempNy - 1) : 0;

                        foreach (var cTie in _customInnerTies)
                        {
                            double left = -tBarW / 2 + cTie.MinX * spacingX;
                            double right = -tBarW / 2 + cTie.MaxX * spacingX;
                            double top = -tBarH / 2 + cTie.MinY * spacingY;
                            double bottom = -tBarH / 2 + cTie.MaxY * spacingY;

                            double innerW = Math.Max(right - left, 4);
                            double innerH = Math.Max(bottom - top, 4);

                            System.Windows.Media.Geometry iGeo = null;
                            if (innerShape != null) iGeo = GetCanvasTieGeometry(innerShape, innerW, innerH);
                            
                            if (iGeo != null)
                            {
                                var iStirrupPath = new System.Windows.Shapes.Path
                                {
                                    Data = iGeo,
                                    Stroke = tieBrush,
                                    StrokeThickness = 2
                                };
                                System.Windows.Controls.Canvas.SetLeft(iStirrupPath, cx + (left + right) / 2);
                                System.Windows.Controls.Canvas.SetTop(iStirrupPath, cy + (top + bottom) / 2);
                                DrawingCanvas.Children.Add(iStirrupPath);
                            }
                            else
                            {
                                var iStirrup = new System.Windows.Shapes.Rectangle
                                {
                                    Width = innerW,
                                    Height = innerH,
                                    Stroke = tieBrush,
                                    StrokeThickness = 2,
                                    RadiusX = 5,
                                    RadiusY = 5
                                };
                                System.Windows.Controls.Canvas.SetLeft(iStirrup, cx + (left + right) / 2 - innerW / 2);
                                System.Windows.Controls.Canvas.SetTop(iStirrup, cy + (top + bottom) / 2 - innerH / 2);
                                DrawingCanvas.Children.Add(iStirrup);
                            }
                        }
                    }
                    else
                    {
                        if (tempNx > 2)
                        {
                            double left = -tBarW / 2 + (tBarW / (tempNx - 1));
                            double right = -tBarW / 2 + (tempNx - 2) * (tBarW / (tempNx - 1));
                            double innerW = Math.Max(right - left, 4);
                            
                            System.Windows.Media.Geometry iGeo = null;
                            if (innerShape != null) iGeo = GetCanvasTieGeometry(innerShape, innerW, stH);
                            
                            if (iGeo != null)
                            {
                                var iStirrupPath = new System.Windows.Shapes.Path
                                {
                                    Data = iGeo,
                                    Stroke = tieBrush,
                                    StrokeThickness = 2
                                };
                                System.Windows.Controls.Canvas.SetLeft(iStirrupPath, cx + (left + right) / 2);
                                System.Windows.Controls.Canvas.SetTop(iStirrupPath, cy);
                                DrawingCanvas.Children.Add(iStirrupPath);
                            }
                            else
                            {
                                var iStirrup = new System.Windows.Shapes.Rectangle
                                {
                                    Width = innerW,
                                    Height = stH,
                                    Stroke = tieBrush,
                                    StrokeThickness = 2,
                                    RadiusX = 5,
                                    RadiusY = 5
                                };
                                System.Windows.Controls.Canvas.SetLeft(iStirrup, cx + (left + right) / 2 - innerW / 2);
                                System.Windows.Controls.Canvas.SetTop(iStirrup, cy - stH / 2);
                                DrawingCanvas.Children.Add(iStirrup);
                            }
                        }
                        
                        if (tempNy > 2)
                        {
                            double top = -tBarH / 2 + (tBarH / (tempNy - 1));
                            double bottom = -tBarH / 2 + (tempNy - 2) * (tBarH / (tempNy - 1));
                            double innerH = Math.Max(bottom - top, 4);
                            
                            System.Windows.Media.Geometry iGeo = null;
                            if (innerShape != null) iGeo = GetCanvasTieGeometry(innerShape, stW, innerH);
                            
                            if (iGeo != null)
                            {
                                var iStirrupPath = new System.Windows.Shapes.Path
                                {
                                    Data = iGeo,
                                    Stroke = tieBrush,
                                    StrokeThickness = 2
                                };
                                System.Windows.Controls.Canvas.SetLeft(iStirrupPath, cx);
                                System.Windows.Controls.Canvas.SetTop(iStirrupPath, cy + (top + bottom) / 2);
                                DrawingCanvas.Children.Add(iStirrupPath);
                            }
                            else
                            {
                                var iStirrup = new System.Windows.Shapes.Rectangle
                                {
                                    Width = stW,
                                    Height = innerH,
                                    Stroke = tieBrush,
                                    StrokeThickness = 2,
                                    RadiusX = 5,
                                    RadiusY = 5
                                };
                                System.Windows.Controls.Canvas.SetLeft(iStirrup, cx - stW / 2);
                                System.Windows.Controls.Canvas.SetTop(iStirrup, cy + (top + bottom) / 2 - innerH / 2);
                                DrawingCanvas.Children.Add(iStirrup);
                            }
                        }
                    }
                }

                // Draw Hooks
                var selectedHook = cmbTieHookType.SelectedItem as RebarHookItem;
                if (selectedHook != null && selectedHook.Name != "None" && selectedHook.Name != "Crank")
                {
                    string hookName = selectedHook.Name.ToLower();
                    bool is135 = hookName.Contains("135");
                    bool is90 = hookName.Contains("90");
                    bool is180 = hookName.Contains("180");
                    
                    if (is135 || is90 || is180)
                    {
                        bool isClosedShape = false;
                        try { 
                            var cvs = selectedShape?.GetCurvesForBrowser(); 
                            if (cvs != null && cvs.Count > 0) {
                                var first = cvs[0].GetEndPoint(0);
                                var last = cvs[cvs.Count-1].GetEndPoint(1);
                                if (first.DistanceTo(last) < 0.1) isClosedShape = true;
                            }
                        } catch {}

                        var hPath = new System.Windows.Shapes.Path { 
                            Stroke = tieBrush, 
                            StrokeThickness = 2,
                            StrokeLineJoin = System.Windows.Media.PenLineJoin.Round,
                            StrokeEndLineCap = System.Windows.Media.PenLineCap.Round
                        };
                        var hGeo = new System.Windows.Media.PathGeometry();
                        double hLen = scale * 0.40;

                        if (isClosedShape)
                        {
                            double cr = 10.0;
                            System.Windows.Point topTangent = new System.Windows.Point(stW / 2 - cr, -stH / 2);
                            System.Windows.Point rightTangent = new System.Windows.Point(stW / 2, -stH / 2 + cr);
                            
                            if (is135)
                            {
                                double angle = Math.PI * 0.75; // 135 deg Left-Down
                                var fig1 = new System.Windows.Media.PathFigure { StartPoint = topTangent };
                                fig1.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(topTangent.X + hLen * Math.Cos(angle), topTangent.Y + hLen * Math.Sin(angle)), true));
                                hGeo.Figures.Add(fig1);
                                
                                var fig2 = new System.Windows.Media.PathFigure { StartPoint = rightTangent };
                                fig2.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(rightTangent.X + hLen * Math.Cos(angle), rightTangent.Y + hLen * Math.Sin(angle)), true));
                                hGeo.Figures.Add(fig2);
                            }
                            else if (is90)
                            {
                                // The 45-degree point on the corner arc
                                double ptOffset = 0.293 * cr; // cr - cr * sin(45)
                                System.Windows.Point pt45 = new System.Windows.Point(stW / 2 - ptOffset, -stH / 2 + ptOffset);
                                
                                // Horizontal hook goes LEFT (parallel to top edge)
                                var fig1 = new System.Windows.Media.PathFigure { StartPoint = pt45 };
                                fig1.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(pt45.X - hLen, pt45.Y), true));
                                hGeo.Figures.Add(fig1);
                                
                                // Vertical hook goes DOWN (parallel to right edge)
                                var fig2 = new System.Windows.Media.PathFigure { StartPoint = pt45 };
                                fig2.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(pt45.X, pt45.Y + hLen), true));
                                hGeo.Figures.Add(fig2);
                            }
                            else if (is180)
                            {
                                var fig1 = new System.Windows.Media.PathFigure { StartPoint = topTangent };
                                fig1.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(topTangent.X - hLen, topTangent.Y), true));
                                hGeo.Figures.Add(fig1);
                                
                                var fig2 = new System.Windows.Media.PathFigure { StartPoint = rightTangent };
                                fig2.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(rightTangent.X, rightTangent.Y + hLen), true));
                                hGeo.Figures.Add(fig2);
                            }
                        }
                        else
                        {
                            System.Windows.Point hookStart = new System.Windows.Point(stW / 2, -stH / 2);
                            System.Windows.Point hookStartRef = new System.Windows.Point(0, -stH / 2);
                            System.Windows.Point hookEnd = new System.Windows.Point(stW / 2, -stH / 2);
                            System.Windows.Point hookEndRef = new System.Windows.Point(stW / 2, 0);
                            
                            if (customGeo is System.Windows.Media.PathGeometry pathGeo && pathGeo.Figures.Count > 0)
                            {
                                var firstFig = pathGeo.Figures[0];
                                hookStart = firstFig.StartPoint;
                                if (firstFig.Segments.Count > 0 && firstFig.Segments[0] is System.Windows.Media.LineSegment firstSeg)
                                    hookStartRef = firstSeg.Point;
                                else if (firstFig.Segments.Count > 0 && firstFig.Segments[0] is System.Windows.Media.ArcSegment firstArc)
                                    hookStartRef = firstArc.Point;
                                    
                                var lastFig = pathGeo.Figures[pathGeo.Figures.Count - 1];
                                if (lastFig.Segments.Count > 0 && lastFig.Segments[lastFig.Segments.Count - 1] is System.Windows.Media.LineSegment lastSeg)
                                {
                                    hookEnd = lastSeg.Point;
                                    if (lastFig.Segments.Count > 1 && lastFig.Segments[lastFig.Segments.Count - 2] is System.Windows.Media.LineSegment prevSeg)
                                        hookEndRef = prevSeg.Point;
                                    else
                                        hookEndRef = lastFig.StartPoint;
                                }
                            }

                            void DrawCadHook(System.Windows.Point pt, System.Windows.Point refPt, double bendAngleDegrees, double offsetDist)
                            {
                                double dx = pt.X - refPt.X;
                                double dy = pt.Y - refPt.Y;
                                double len = Math.Sqrt(dx*dx + dy*dy);
                                if (len < 0.001) { dx = -pt.X; dy = -pt.Y; len = Math.Sqrt(dx*dx + dy*dy); }
                                
                                double segAngle = Math.Atan2(dy, dx);
                                double toCenterX = 0 - pt.X;
                                double toCenterY = 0 - pt.Y;
                                double toCenterAngle = Math.Atan2(toCenterY, toCenterX);
                                double a1 = segAngle + bendAngleDegrees * Math.PI / 180.0;
                                double a2 = segAngle - bendAngleDegrees * Math.PI / 180.0;
                                double diff1 = Math.Cos(a1 - toCenterAngle);
                                double diff2 = Math.Cos(a2 - toCenterAngle);
                                double finalAngle = diff1 > diff2 ? a1 : a2;
                                
                                System.Windows.Point startPt = new System.Windows.Point(pt.X - (dx/len)*offsetDist, pt.Y - (dy/len)*offsetDist);
                                System.Windows.Point hEnd = new System.Windows.Point(startPt.X + hLen * Math.Cos(finalAngle), startPt.Y + hLen * Math.Sin(finalAngle));
                                
                                var fig = new System.Windows.Media.PathFigure { StartPoint = startPt };
                                fig.Segments.Add(new System.Windows.Media.LineSegment(hEnd, true));
                                hGeo.Figures.Add(fig);
                            }

                            if (is135)
                            {
                                DrawCadHook(hookStart, hookStartRef, 135, 3.0);
                                DrawCadHook(hookEnd, hookEndRef, 135, 6.0);
                            }
                            else if (is90)
                            {
                                DrawCadHook(hookStart, hookStartRef, 90, 0);
                                DrawCadHook(hookEnd, hookEndRef, 90, 4.0);
                            }
                            else if (is180)
                            {
                                DrawCadHook(hookStart, hookStartRef, 180, 0);
                                DrawCadHook(hookEnd, hookEndRef, 180, 4.0);
                            }
                        }
                        
                        hPath.Data = hGeo;
                        System.Windows.Controls.Canvas.SetLeft(hPath, cx);
                        System.Windows.Controls.Canvas.SetTop(hPath, cy);
                        DrawingCanvas.Children.Add(hPath);
                    }
                }

                // Draw dynamic rebars based on Nx, Ny
                int nx = 2; int ny = 2;
                if (txtNx != null) int.TryParse(txtNx.Text, out nx);
                if (txtNy != null) int.TryParse(txtNy.Text, out ny);
                if (nx < 2) nx = 2;
                if (ny < 2) ny = 2;

                double rSize = scale * 0.15;
                // Inset the vertical bars slightly so they nest inside the tie bounds
                double inset = rSize / 2.0; 
                double barW = stW - inset * 2;
                double barH = stH - inset * 2;

                List<System.Windows.Point> pts = new List<System.Windows.Point>();
                
                for (int i = 0; i < nx; i++)
                {
                    double tx = -barW / 2 + i * (barW / (nx - 1));
                    pts.Add(new System.Windows.Point(tx, -barH / 2));
                    pts.Add(new System.Windows.Point(tx, barH / 2));
                }
                for (int i = 1; i < ny - 1; i++)
                {
                    double ty = -barH / 2 + i * (barH / (ny - 1));
                    pts.Add(new System.Windows.Point(-barW / 2, ty));
                    pts.Add(new System.Windows.Point(barW / 2, ty));
                }

                foreach (var p in pts)
                {
                    var r = new System.Windows.Shapes.Ellipse
                    {
                        Width = rSize,
                        Height = rSize,
                        Fill = rebarBrush
                    };
                    System.Windows.Controls.Canvas.SetLeft(r, cx + p.X - rSize / 2);
                    System.Windows.Controls.Canvas.SetTop(r, cy + p.Y - rSize / 2);
                    DrawingCanvas.Children.Add(r);
                }
                
                // Draw Selection Box
                if (_isSelecting)
                {
                    double minSelX = Math.Min(_selectStartPoint.X, _selectCurrentPoint.X);
                    double maxSelX = Math.Max(_selectStartPoint.X, _selectCurrentPoint.X);
                    double minSelY = Math.Min(_selectStartPoint.Y, _selectCurrentPoint.Y);
                    double maxSelY = Math.Max(_selectStartPoint.Y, _selectCurrentPoint.Y);

                    var selRect = new System.Windows.Shapes.Rectangle
                    {
                        Width = maxSelX - minSelX,
                        Height = maxSelY - minSelY,
                        Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 13, 110, 253)),
                        Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(13, 110, 253)),
                        StrokeThickness = 1,
                        StrokeDashArray = new System.Windows.Media.DoubleCollection(new double[] { 4, 4 })
                    };
                    System.Windows.Controls.Canvas.SetLeft(selRect, minSelX);
                    System.Windows.Controls.Canvas.SetTop(selRect, minSelY);
                    DrawingCanvas.Children.Add(selRect);
                }
            }
            else // 3D
            {
                double colW = scale * 1.0;
                double colH = scale * 3.0;

                // Simple 3D Box front face
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = colW,
                    Height = colH,
                    Fill = fill,
                    Stroke = stroke,
                    StrokeThickness = 1
                };
                System.Windows.Controls.Canvas.SetLeft(rect, cx - colW / 2);
                System.Windows.Controls.Canvas.SetTop(rect, cy - colH / 2);
                DrawingCanvas.Children.Add(rect);

                // Draw vertical rebars based on Nx
                int nx = 2;
                if (txtNx != null) int.TryParse(txtNx.Text, out nx);
                if (nx < 2) nx = 2;

                for (int i = 0; i < nx; i++)
                {
                    double tx = -colW * 0.4 + i * ((colW * 0.8) / (nx - 1));
                    var line = new System.Windows.Shapes.Line
                    {
                        X1 = cx + tx,
                        Y1 = cy - colH * 0.45,
                        X2 = cx + tx,
                        Y2 = cy + colH * 0.45,
                        Stroke = rebarBrush,
                        StrokeThickness = 2
                    };
                    DrawingCanvas.Children.Add(line);
                }

                // Draw horizontal stirrups
                for (int i = 0; i < 6; i++)
                {
                    double yPos = cy - colH * 0.45 + i * (colH * 0.9 / 5);
                    var line = new System.Windows.Shapes.Line
                    {
                        X1 = cx - colW * 0.4,
                        Y1 = yPos,
                        X2 = cx + colW * 0.4,
                        Y2 = yPos,
                        Stroke = rebarBrush,
                        StrokeThickness = 2
                    };
                    DrawingCanvas.Children.Add(line);
                }
            }

            var txt = new System.Windows.Controls.TextBlock
            {
                Text = $"{_currentViewMode} View",
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            System.Windows.Controls.Canvas.SetLeft(txt, 10);
            System.Windows.Controls.Canvas.SetTop(txt, 10);
            DrawingCanvas.Children.Add(txt);
        }
    }

    public class StructuralColumnSelectionFilter : Autodesk.Revit.UI.Selection.ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem.Category != null && elem.Category.Id.Value == (long)BuiltInCategory.OST_StructuralColumns;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    public class ColumnRebarConfig
    {
        public int Nx { get; set; }
        public int Ny { get; set; }
        public ElementId VerticalBarTypeId { get; set; }
        public ElementId TieBarTypeId { get; set; }
        public ElementId TopHookTypeId { get; set; }
        public ElementId BottomHookTypeId { get; set; }
        public bool TopIsCrank { get; set; }
        public bool BottomIsCrank { get; set; }
        public ElementId HookTypeId { get; set; }
        public ElementId TieShapeId { get; set; }
        public ElementId TopCrankId { get; set; }
        public ElementId BottomCrankId { get; set; }
        public double TieSpacingMm { get; set; }
        public double InnerTieSpacingMm { get; set; }
        public ElementId InnerTieShapeId { get; set; }
        public bool GenerateInnerTies { get; set; }
        public double LapFactor { get; set; }
        public bool ExtendDowels { get; set; }
        public List<CustomInnerTie> CustomInnerTies { get; set; }
    }

    public class RebarHookItem
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
    }
}
