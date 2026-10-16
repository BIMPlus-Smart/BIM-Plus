using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AutoPileCap.Services;

namespace AutoPileCap.UI
{
    public partial class CapExistingPilesWindow : Window
    {
        private readonly Document _doc;
        private readonly RevitElementService _service;
        private readonly List<FamilyInstance> _preselectedPiles;
        private AutoPileCap.Handlers.GenericEventHandler _handler;
        private ExternalEvent _exEvent;

        public CapExistingPilesWindow(UIApplication uiapp, AutoPileCap.Handlers.GenericEventHandler handler, ExternalEvent exEvent)
        {
            InitializeComponent();

            _handler = handler;
            _exEvent = exEvent;

            UIDocument uidoc = uiapp.ActiveUIDocument;
            _doc = uidoc.Document;
            _service = new RevitElementService(_doc);

            var selectedIds = uidoc.Selection.GetElementIds();
            _preselectedPiles = _service.GetStructuralFoundations(selectedIds);

            Loaded += CapExistingPilesWindow_Loaded;
        }

        private void CapExistingPilesWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PopulateCapSymbols();
            UpdateScopeLabels();
        }

        private void PopulateCapSymbols()
        {
            var symbols = _service.GetFoundationSymbols();
            CmbCapSymbol.Items.Clear();

            int capIndex = 0;
            int i = 0;
            foreach (var sym in symbols)
            {
                string displayName = $"{sym.FamilyName} : {sym.Name}";
                var item = new ComboBoxItem { Content = displayName, Tag = sym.Id.Value };
                CmbCapSymbol.Items.Add(item);

                string lower = displayName.ToLowerInvariant();
                if (lower.Contains("cap") || lower.Contains("footing"))
                {
                    capIndex = i;
                }
                i++;
            }

            if (symbols.Count > 0)
            {
                CmbCapSymbol.SelectedIndex = capIndex;
            }
        }

        private void UpdateScopeLabels()
        {
            int count = _preselectedPiles.Count;
            RbSelectedPiles.Content = $"Selected Piles in Model ({count})";
            if (count == 0)
            {
                RbAllPiles.IsChecked = true;
                RbSelectedPiles.IsEnabled = false;
            }
        }

        private void BtnClusterAndCap_Click(object sender, RoutedEventArgs e)
        {
            // Parse inputs
            if (!double.TryParse(TxtClusterDistance.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double clusterDistMm) || clusterDistMm <= 0)
            {
                MessageBox.Show("Please enter a valid positive clustering proximity in mm.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(TxtEdgeDistance.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double edgeMm) || edgeMm < 0)
            {
                MessageBox.Show("Please enter a valid edge distance in mm.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(TxtCapThickness.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double thkMm) || thkMm <= 0)
            {
                MessageBox.Show("Please enter a valid cap thickness in mm.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CmbCapSymbol.SelectedItem is not ComboBoxItem selectedItem || !long.TryParse(selectedItem.Tag?.ToString(), out long capSymId))
            {
                MessageBox.Show("Please select a Pile Cap family symbol.", "Missing Family", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var capSymbol = _doc.GetElement(new ElementId(capSymId)) as FamilySymbol;
            if (capSymbol == null)
            {
                MessageBox.Show("Selected pile cap symbol not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Gather piles
            List<FamilyInstance> piles = (RbSelectedPiles.IsChecked == true && _preselectedPiles.Count > 0)
                ? _preselectedPiles
                : _service.GetStructuralFoundations();

            if (piles.Count == 0)
            {
                MessageBox.Show("No foundation piles found to group and cap.", "No Piles", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            double maxDistFeet = GeometryHelper.MmToFeet(clusterDistMm);
            double edgeFeet = GeometryHelper.MmToFeet(edgeMm);
            double thkFeet = GeometryHelper.MmToFeet(thkMm);

            _handler.Action = app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var service = new RevitElementService(doc);

                // Extract locations
                var pilePoints = new List<Tuple<FamilyInstance, XYZ>>();
                foreach (var pile in piles)
                {
                    if (pile.Location is LocationPoint locPt)
                    {
                        pilePoints.Add(Tuple.Create(pile, locPt.Point));
                    }
                }

                if (pilePoints.Count == 0)
                {
                    Dispatcher.Invoke(() => MessageBox.Show("Could not extract coordinate locations from piles.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning));
                    return;
                }

                // Cluster piles
                var clusters = GeometryHelper.ClusterElements(pilePoints, p => p.Item2, maxDistFeet);

                int capsCreated = 0;

                using (Transaction trans = new Transaction(doc, "Cap Existing Piles"))
                {
                    trans.Start();

                    foreach (var cluster in clusters)
                    {
                        if (cluster.Count == 0) continue;

                        var pts = cluster.Select(c => c.Item2).ToList();
                        XYZ centroid = GeometryHelper.CalculateCentroid(pts);

                        // Max pile top Z in cluster
                        double topZ = pts.Max(p => p.Z);

                        GeometryHelper.GetBounds2D(pts, out double minX, out double maxX, out double minY, out double maxY);

                        // Width along X, Length along Y (plus 2*edge)
                        double widthFeet = Math.Max((maxX - minX) + (2 * edgeFeet), 2 * edgeFeet + GeometryHelper.MmToFeet(400));
                        double lengthFeet = Math.Max((maxY - minY) + (2 * edgeFeet), 2 * edgeFeet + GeometryHelper.MmToFeet(400));

                        Level level = service.GetClosestLevelBelow(topZ);
                        XYZ capLocation = new XYZ(centroid.X, centroid.Y, topZ);

                        var cap = service.PlacePileCap(
                            capLocation,
                            0.0,
                            capSymbol,
                            level,
                            0.0,
                            widthFeet,
                            lengthFeet,
                            thkFeet);

                        if (cap != null) capsCreated++;
                    }

                    trans.Commit();
                }

                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"Successfully created {capsCreated} Pile Caps over {clusters.Count} pile clusters (Total {pilePoints.Count} piles).",
                        "Cap Piles Completed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    Close();
                });
            };
            _exEvent.Raise();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
