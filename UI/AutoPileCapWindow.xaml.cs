using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AutoPileCap.Models;
using AutoPileCap.Services;

namespace AutoPileCap.UI
{
    public partial class AutoPileCapWindow : Window
    {
        private readonly Document _doc;
        private readonly RevitElementService _service;
        private readonly List<FamilyInstance> _preselectedColumns;
        private AutoPileCap.Handlers.GenericEventHandler _handler;
        private ExternalEvent _exEvent;

        public AutoPileCapWindow(UIApplication uiapp, AutoPileCap.Handlers.GenericEventHandler handler, ExternalEvent exEvent)
        {
            InitializeComponent();

            _handler = handler;
            _exEvent = exEvent;

            UIDocument uidoc = uiapp.ActiveUIDocument;
            _doc = uidoc.Document;
            _service = new RevitElementService(_doc);

            var selectedIds = uidoc.Selection.GetElementIds();
            _preselectedColumns = _service.GetStructuralColumns(selectedIds);

            Loaded += AutoPileCapWindow_Loaded;
        }

        private void AutoPileCapWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PopulateLevels();
            PopulateFoundationSymbols();
            UpdateScopeLabels();
            UpdateCalculatedDimensions();
        }

        private void PopulateLevels()
        {
            var levels = _service.GetLevels();
            CmbLevels.Items.Clear();

            ComboBoxItem autoItem = new ComboBoxItem
            {
                Content = "-- Auto Detect from Column Base --",
                Tag = -1L
            };
            CmbLevels.Items.Add(autoItem);
            CmbLevels.SelectedIndex = 0;

            foreach (var lvl in levels)
            {
                double elevMm = lvl.Elevation * GeometryHelper.MmPerFoot;
                var item = new ComboBoxItem
                {
                    Content = $"{lvl.Name} ({elevMm:F0} mm)",
                    Tag = lvl.Id.Value
                };
                CmbLevels.Items.Add(item);
            }
        }

        private void PopulateFoundationSymbols()
        {
            var symbols = _service.GetFoundationSymbols();
            CmbCapSymbol.Items.Clear();
            CmbPileSymbol.Items.Clear();

            if (symbols.Count == 0)
            {
                TxtStatus.Text = "⚠️ Warning: No Structural Foundation families loaded in document.";
                return;
            }

            int capIndex = 0;
            int pileIndex = 0;
            int i = 0;

            foreach (var sym in symbols)
            {
                string displayName = $"{sym.FamilyName} : {sym.Name}";
                var capItem = new ComboBoxItem { Content = displayName, Tag = sym.Id.Value };
                var pileItem = new ComboBoxItem { Content = displayName, Tag = sym.Id.Value };

                CmbCapSymbol.Items.Add(capItem);
                CmbPileSymbol.Items.Add(pileItem);

                string lower = displayName.ToLowerInvariant();
                if (lower.Contains("cap") || lower.Contains("footing") || lower.Contains("pile cap"))
                {
                    capIndex = i;
                }
                if (lower.Contains("pile") || lower.Contains("bored") || lower.Contains("driven"))
                {
                    pileIndex = i;
                }
                i++;
            }

            CmbCapSymbol.SelectedIndex = capIndex;
            CmbPileSymbol.SelectedIndex = pileIndex;
        }

        private void UpdateScopeLabels()
        {
            int count = _preselectedColumns.Count;
            RbSelectedColumns.Content = $"Selected Columns in Model ({count})";
            if (count == 0)
            {
                RbAllColumns.IsChecked = true;
                RbSelectedColumns.IsEnabled = false;
            }
        }

        private void CmbPileCount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCalculatedDimensions();
        }

        private void Parameters_Changed(object sender, TextChangedEventArgs e)
        {
            UpdateCalculatedDimensions();
        }

        private void SpacingRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (TxtCustomSpacing != null)
            {
                TxtCustomSpacing.IsEnabled = (RbSpacingCustom.IsChecked == true);
            }
            UpdateCalculatedDimensions();
        }

        private void ChkCreatePiles_Changed(object sender, RoutedEventArgs e)
        {
            bool create = ChkCreatePiles.IsChecked == true;
            if (CmbPileSymbol != null) CmbPileSymbol.IsEnabled = create;
            if (TxtPileLength != null) TxtPileLength.IsEnabled = create;
            if (LblPileSymbol != null) LblPileSymbol.Opacity = create ? 1.0 : 0.5;
            if (LblPileLength != null) LblPileLength.Opacity = create ? 1.0 : 0.5;
        }

        private PileCapConfig? BuildConfiguration()
        {
            var config = new PileCapConfig();

            // Pile Count
            if (CmbPileCount.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int count))
            {
                config.PileCount = count;
            }

            // Pile Diameter
            if (double.TryParse(TxtPileDiameter.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double dia) && dia > 0)
            {
                config.PileDiameterMm = dia;
            }
            else
            {
                MessageBox.Show("Please enter a valid positive pile diameter in mm.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            // Spacing
            if (RbSpacing3D.IsChecked == true)
            {
                config.SpacingMultiplier = 3.0;
                config.UseCustomSpacing = false;
            }
            else if (RbSpacing25D.IsChecked == true)
            {
                config.SpacingMultiplier = 2.5;
                config.UseCustomSpacing = false;
            }
            else
            {
                config.UseCustomSpacing = true;
                if (double.TryParse(TxtCustomSpacing.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double customSp) && customSp > 0)
                {
                    config.CustomSpacingMm = customSp;
                }
                else
                {
                    MessageBox.Show("Please enter a valid positive custom spacing in mm.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
            }

            // Edge Distance
            if (double.TryParse(TxtEdgeDistance.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double edge) && edge >= 0)
            {
                config.EdgeDistanceMm = edge;
            }

            // Cap Thickness
            if (double.TryParse(TxtCapThickness.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double thk) && thk > 0)
            {
                config.CapThicknessMm = thk;
            }

            // Pile Length
            if (double.TryParse(TxtPileLength.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double pileLenM) && pileLenM > 0)
            {
                config.PileLengthMm = pileLenM * 1000.0; // convert meters to mm
            }

            // Level Offset
            if (double.TryParse(TxtLevelOffset.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double offsetMm))
            {
                config.LevelOffsetMm = offsetMm;
            }

            config.CreatePilesUnderCap = ChkCreatePiles.IsChecked == true;
            config.AlignWithColumnRotation = ChkAlignRotation.IsChecked == true;

            // Symbols
            if (CmbCapSymbol.SelectedItem is ComboBoxItem capItem && long.TryParse(capItem.Tag?.ToString(), out long capId))
            {
                config.SelectedCapSymbolId = capId;
            }

            if (CmbPileSymbol.SelectedItem is ComboBoxItem pileItem && long.TryParse(pileItem.Tag?.ToString(), out long pileId))
            {
                config.SelectedPileSymbolId = pileId;
            }

            // Level
            if (CmbLevels.SelectedItem is ComboBoxItem lvlItem && long.TryParse(lvlItem.Tag?.ToString(), out long lvlId))
            {
                config.SelectedLevelId = lvlId;
            }

            return config;
        }

        private void UpdateCalculatedDimensions()
        {
            if (TxtCalculatedDimensions == null) return;

            try
            {
                int count = 4;
                if (CmbPileCount?.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int c))
                {
                    count = c;
                }

                double dia = 400;
                double.TryParse(TxtPileDiameter?.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out dia);

                double spacing = dia * 3.0;
                if (RbSpacing25D?.IsChecked == true) spacing = dia * 2.5;
                if (RbSpacingCustom?.IsChecked == true && double.TryParse(TxtCustomSpacing?.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double custom))
                {
                    spacing = custom;
                }

                double edge = 250;
                double.TryParse(TxtEdgeDistance?.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out edge);

                double thk = 800;
                double.TryParse(TxtCapThickness?.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out thk);

                var tempConfig = new PileCapConfig
                {
                    PileCount = count,
                    PileDiameterMm = dia,
                    UseCustomSpacing = true,
                    CustomSpacingMm = spacing,
                    EdgeDistanceMm = edge,
                    CapThicknessMm = thk
                };

                var layout = PileLayoutPattern.Calculate(tempConfig);
                double wMm = layout.CapWidthFeet * GeometryHelper.MmPerFoot;
                double lMm = layout.CapLengthFeet * GeometryHelper.MmPerFoot;

                TxtCalculatedDimensions.Text = $"Width: {wMm:F0} mm × Length: {lMm:F0} mm | Depth: {thk:F0} mm ({layout.Description})";
            }
            catch
            {
                // Fallback silently during input typing
            }
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            var config = BuildConfiguration();
            if (config == null) return;

            if (config.SelectedCapSymbolId == -1)
            {
                MessageBox.Show("Please select a Pile Cap family symbol.", "Missing Family", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FamilySymbol? capSymbol = _doc.GetElement(new ElementId(config.SelectedCapSymbolId)) as FamilySymbol;
            if (capSymbol == null)
            {
                MessageBox.Show("Selected Pile Cap symbol was not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            FamilySymbol? pileSymbol = null;
            if (config.CreatePilesUnderCap && config.SelectedPileSymbolId != -1)
            {
                pileSymbol = _doc.GetElement(new ElementId(config.SelectedPileSymbolId)) as FamilySymbol;
            }

            // Collect target columns
            List<FamilyInstance> targetColumns;
            if (RbSelectedColumns.IsChecked == true && _preselectedColumns.Count > 0)
            {
                targetColumns = _preselectedColumns;
            }
            else
            {
                targetColumns = _service.GetStructuralColumns();
            }

            if (targetColumns.Count == 0)
            {
                MessageBox.Show("No structural columns found to place pile caps under.", "No Columns", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Calculate layout offsets
            var layout = PileLayoutPattern.Calculate(config);

            BtnGenerate.IsEnabled = false;
            PrgProgress.Visibility = System.Windows.Visibility.Visible;
            PrgProgress.Value = 0;
            PrgProgress.Maximum = targetColumns.Count;

            _handler.Action = app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var service = new RevitElementService(doc);

                int capsCreated = 0;
                int pilesCreated = 0;

                using (Transaction trans = new Transaction(doc, "Auto Generate Pile Caps"))
                {
                    trans.Start();

                    for (int i = 0; i < targetColumns.Count; i++)
                    {
                        var col = targetColumns[i];
                        if (!service.GetColumnBaseInfo(col, out XYZ basePoint, out double rotationAngle, out Level baseLevel))
                        {
                            continue;
                        }

                        // Override level if chosen
                        Level targetLevel = baseLevel;
                        if (config.SelectedLevelId != -1)
                        {
                            var specifiedLevel = doc.GetElement(new ElementId(config.SelectedLevelId)) as Level;
                            if (specifiedLevel != null) targetLevel = specifiedLevel;
                        }

                        if (targetLevel == null)
                        {
                            targetLevel = service.GetClosestLevelBelow(basePoint.Z);
                        }

                        double effectiveRotation = config.AlignWithColumnRotation ? rotationAngle : 0.0;

                        // Place Pile Cap
                        var cap = service.PlacePileCap(
                            basePoint,
                            effectiveRotation,
                            capSymbol,
                            targetLevel,
                            config.LevelOffsetFeet,
                            layout.CapWidthFeet,
                            layout.CapLengthFeet,
                            config.CapThicknessFeet);

                        if (cap != null) capsCreated++;

                        // Place Piles underneath
                        if (config.CreatePilesUnderCap && pileSymbol != null)
                        {
                            double pileTopElevationOffset = config.LevelOffsetFeet - config.CapThicknessFeet;
                            foreach (var offset in layout.Offsets)
                            {
                                XYZ pileTopPoint = GeometryHelper.TransformLocalOffset(
                                    basePoint,
                                    offset.X,
                                    offset.Y,
                                    pileTopElevationOffset,
                                    effectiveRotation);

                                var pile = service.PlacePile(
                                    pileTopPoint,
                                    effectiveRotation,
                                    pileSymbol,
                                    targetLevel,
                                    config.PileLengthFeet);

                                if (pile != null) pilesCreated++;
                            }
                        }

                        int currentIndex = i;
                        Dispatcher.Invoke(() => PrgProgress.Value = currentIndex + 1);
                    }

                    trans.Commit();
                }

                Dispatcher.Invoke(() =>
                {
                    BtnGenerate.IsEnabled = true;
                    PrgProgress.Visibility = System.Windows.Visibility.Collapsed;
                    TxtStatus.Text = $"Completed: Created {capsCreated} pile caps and {pilesCreated} piles.";

                    MessageBox.Show(
                        $"Successfully created:\n\n• {capsCreated} Pile Caps\n• {pilesCreated} Embedded Piles\n\nUnder {targetColumns.Count} structural columns.",
                        "Auto Pile Cap - Done",
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
