using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AutoPileCap.Models;
using AutoPileCap.Services;
using WpfVisibility = System.Windows.Visibility;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace AutoPileCap.UI
{
    public partial class PartitionRebarWindow : Window
    {
        private readonly UIApplication _uiapp;
        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private readonly AutoPileCap.Handlers.GenericEventHandler _handler;
        private readonly ExternalEvent _exEvent;
        private readonly PartitionRebarService _service;
        private ICollection<ElementId>? _preselectedIds;

        private List<RebarItemModel> _allItems = new List<RebarItemModel>();
        private List<PartitionGroupItem> _partitionGroups = new List<PartitionGroupItem>();
        private List<PartitionTypeRowModel> _groupedRows = new List<PartitionTypeRowModel>();
        private List<PartitionTypeRowModel> _visibleGroupedRows = new List<PartitionTypeRowModel>();
        private List<RebarItemModel> _visibleDetailedRows = new List<RebarItemModel>();

        private SelectionScope _currentScope = SelectionScope.ActiveView;
        private string? _selectedPartitionFilter = null;
        private bool _isUpdatingCheckboxes = false;

        public PartitionRebarWindow(
            UIApplication uiapp,
            AutoPileCap.Handlers.GenericEventHandler handler,
            ExternalEvent exEvent,
            ICollection<ElementId>? preselectedIds = null)
        {
            InitializeComponent();

            _uiapp = uiapp;
            _handler = handler;
            _exEvent = exEvent;
            _uidoc = uiapp.ActiveUIDocument;
            _doc = _uidoc.Document;
            _service = new PartitionRebarService();
            _preselectedIds = preselectedIds;

            Loaded += PartitionRebarWindow_Loaded;
        }

        private void PartitionRebarWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ReloadData();
        }

        /// <summary>
        /// Reads all rebars from Revit DB according to current scope and rebuilds collections.
        /// Accounts for the "Rebar Sets" parameter multiplier for quantities, lengths, and weights.
        /// </summary>
        public void ReloadData()
        {
            try
            {
                // If scope is CurrentSelection, refresh preselected IDs from current Revit selection
                if (_currentScope == SelectionScope.CurrentSelection)
                {
                    _preselectedIds = _uidoc.Selection.GetElementIds();
                }

                _allItems = _service.GetRebars(_doc, _currentScope, _preselectedIds);
                _partitionGroups = _service.GroupByPartitions(_allItems);
                _groupedRows = _service.GroupByPartitionAndType(_allItems);

                // Update KPI Cards (bars and weights reflect the Rebar Sets multiplier)
                TxtKpiPartitions.Text = $"{_partitionGroups.Count} Partitions";
                TxtKpiSets.Text = $"{_allItems.Count:N0} Sets";
                TxtKpiBars.Text = $"{_allItems.Sum(x => x.Quantity):N0} Bars";
                TxtBadgePartitions.Text = _partitionGroups.Count.ToString();

                // Populate sidebar
                IcPartitions.ItemsSource = null;
                IcPartitions.ItemsSource = _partitionGroups;

                // Apply active filters to tables
                ApplyFilter();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Partition Rebar Error", $"Failed to load rebar elements:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Filters table items based on search text and sidebar selection.
        /// </summary>
        private void ApplyFilter()
        {
            string search = TxtSearch.Text?.Trim().ToLowerInvariant() ?? string.Empty;

            // Grouped Rows Filter
            var filteredGrouped = _groupedRows.AsEnumerable();

            if (!string.IsNullOrEmpty(_selectedPartitionFilter))
            {
                filteredGrouped = filteredGrouped.Where(r => r.Partition.Equals(_selectedPartitionFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(search))
            {
                filteredGrouped = filteredGrouped.Where(r =>
                    r.Partition.ToLowerInvariant().Contains(search) ||
                    r.TypeName.ToLowerInvariant().Contains(search) ||
                    r.HostCategory.ToLowerInvariant().Contains(search) ||
                    r.ShapeName.ToLowerInvariant().Contains(search) ||
                    r.RebarSetsDisplay.ToLowerInvariant().Contains(search) ||
                    r.DiameterMm.ToString().Contains(search));
            }

            _visibleGroupedRows = filteredGrouped.ToList();
            DgGrouped.ItemsSource = null;
            DgGrouped.ItemsSource = _visibleGroupedRows;

            // Detailed Rows Filter
            var filteredDetailed = _allItems.AsEnumerable();

            if (!string.IsNullOrEmpty(_selectedPartitionFilter))
            {
                filteredDetailed = filteredDetailed.Where(i => i.Partition.Equals(_selectedPartitionFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(search))
            {
                filteredDetailed = filteredDetailed.Where(i =>
                    i.Partition.ToLowerInvariant().Contains(search) ||
                    i.TypeName.ToLowerInvariant().Contains(search) ||
                    i.HostName.ToLowerInvariant().Contains(search) ||
                    i.HostCategory.ToLowerInvariant().Contains(search) ||
                    i.ShapeName.ToLowerInvariant().Contains(search) ||
                    i.RebarSets.ToString().Contains(search) ||
                    i.DiameterMm.ToString().Contains(search) ||
                    i.ScheduleMark.ToLowerInvariant().Contains(search));
            }

            _visibleDetailedRows = filteredDetailed.ToList();
            DgDetailed.ItemsSource = null;
            DgDetailed.ItemsSource = _visibleDetailedRows;

            // Table Title text
            if (!string.IsNullOrEmpty(_selectedPartitionFilter))
            {
                TxtTableFilterStatus.Text = $"• Filtered by Partition: \"{_selectedPartitionFilter}\"";
            }
            else
            {
                TxtTableFilterStatus.Text = string.Empty;
            }

            UpdateSelectionStats();
        }

        /// <summary>
        /// Updates the KPI cards and bottom status bar with checked items,
        /// ensuring calculated weight and length include the Rebar Sets multiplier.
        /// </summary>
        private void UpdateSelectionStats()
        {
            var selectedIds = GetSelectedElementIds();
            int selectedCount = selectedIds.Count;

            // Calculate selected weight and length
            double selectedWeight = 0.0;
            double selectedLength = 0.0;
            int selectedTotalBars = 0;

            if (CmbViewMode.SelectedIndex == 1) // Detailed view
            {
                var checkedItems = _allItems.Where(i => i.IsSelected).ToList();
                selectedWeight = checkedItems.Sum(x => x.WeightKg);
                selectedLength = checkedItems.Sum(x => x.TotalLengthM);
                selectedTotalBars = checkedItems.Sum(x => x.Quantity);
            }
            else
            {
                var checkedRows = _groupedRows.Where(r => r.IsChecked).ToList();
                selectedWeight = checkedRows.Sum(x => x.TotalWeightKg);
                selectedLength = checkedRows.Sum(x => x.TotalLengthM);
                selectedTotalBars = checkedRows.Sum(x => x.BarQuantity);
            }

            TxtKpiSelected.Text = $"{selectedCount:N0} Selected ({selectedWeight:N1} kg)";
            TxtStatus.Text = $"Ready: {selectedCount:N0} elements ({selectedTotalBars:N0} total bars | {selectedLength:N1} m | {selectedWeight:N1} kg)";

            // Update master header checkboxes state
            if (!_isUpdatingCheckboxes)
            {
                _isUpdatingCheckboxes = true;

                if (DgGrouped.Visibility == WpfVisibility.Visible)
                {
                    bool allChecked = _visibleGroupedRows.Count > 0 && _visibleGroupedRows.All(r => r.IsChecked);
                    ChkHeaderGrouped.IsChecked = allChecked;
                }
                else
                {
                    bool allChecked = _visibleDetailedRows.Count > 0 && _visibleDetailedRows.All(i => i.IsSelected);
                    ChkHeaderDetailed.IsChecked = allChecked;
                }

                _isUpdatingCheckboxes = false;
            }
        }

        /// <summary>
        /// Collects all unique Revit ElementIds from currently checked rows or items.
        /// </summary>
        public HashSet<ElementId> GetSelectedElementIds()
        {
            var ids = new HashSet<ElementId>();

            if (CmbViewMode.SelectedIndex == 1) // Detailed
            {
                foreach (var item in _allItems)
                {
                    if (item.IsSelected)
                    {
                        ids.Add(item.RebarId);
                    }
                }
            }
            else // Grouped by Partition & Type
            {
                foreach (var row in _groupedRows)
                {
                    if (row.IsChecked)
                    {
                        foreach (var id in row.ElementIds)
                        {
                            ids.Add(id);
                        }
                    }
                }
            }

            return ids;
        }

        #region Scope and View Mode Handlers

        private void RbScope_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            if (RbScopeActiveView.IsChecked == true)
            {
                _currentScope = SelectionScope.ActiveView;
            }
            else if (RbScopeWholeModel.IsChecked == true)
            {
                _currentScope = SelectionScope.WholeModel;
            }
            else if (RbScopeSelection.IsChecked == true)
            {
                _currentScope = SelectionScope.CurrentSelection;
            }

            ReloadData();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            ReloadData();
        }

        private void CmbViewMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            if (CmbViewMode.SelectedIndex == 1)
            {
                DgGrouped.Visibility = WpfVisibility.Collapsed;
                DgDetailed.Visibility = WpfVisibility.Visible;
                TxtTableTitle.Text = "All Individual Rebar Elements";
            }
            else
            {
                DgGrouped.Visibility = WpfVisibility.Visible;
                DgDetailed.Visibility = WpfVisibility.Collapsed;
                TxtTableTitle.Text = "Rebar Elements Grouped by Partition & Type";
            }

            ApplyFilter();
        }

        #endregion

        #region Search and Partition Selection

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool hasText = !string.IsNullOrEmpty(TxtSearch.Text);
            TxtSearchPlaceholder.Visibility = hasText ? WpfVisibility.Collapsed : WpfVisibility.Visible;
            BtnClearSearch.Visibility = hasText ? WpfVisibility.Visible : WpfVisibility.Collapsed;
            ApplyFilter();
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = string.Empty;
        }

        private void PartitionItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is PartitionGroupItem group)
            {
                // Highlight clicked partition
                foreach (var p in _partitionGroups)
                {
                    p.IsHighlighted = (p == group);
                }

                _selectedPartitionFilter = group.PartitionName;
                ApplyFilter();
            }
        }

        private void BtnShowAllPartitions_Click(object sender, RoutedEventArgs e)
        {
            foreach (var p in _partitionGroups)
            {
                p.IsHighlighted = false;
            }
            _selectedPartitionFilter = null;
            ApplyFilter();
        }

        private void PartitionCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is PartitionGroupItem group)
            {
                bool isChecked = cb.IsChecked == true;
                foreach (var row in _groupedRows.Where(r => r.Partition.Equals(group.PartitionName, StringComparison.OrdinalIgnoreCase)))
                {
                    row.IsChecked = isChecked;
                }
                foreach (var item in group.Items)
                {
                    item.IsSelected = isChecked;
                }

                UpdateSelectionStats();
            }
        }

        private void SelectAllPartitions_Click(object sender, MouseButtonEventArgs e)
        {
            foreach (var group in _partitionGroups)
            {
                group.IsChecked = true;
            }
            foreach (var row in _groupedRows)
            {
                row.IsChecked = true;
            }
            foreach (var item in _allItems)
            {
                item.IsSelected = true;
            }
            UpdateSelectionStats();
        }

        private void DeselectAllPartitions_Click(object sender, MouseButtonEventArgs e)
        {
            foreach (var group in _partitionGroups)
            {
                group.IsChecked = false;
            }
            foreach (var row in _groupedRows)
            {
                row.IsChecked = false;
            }
            foreach (var item in _allItems)
            {
                item.IsSelected = false;
            }
            UpdateSelectionStats();
        }

        private void TableSelectAll_Click(object sender, MouseButtonEventArgs e)
        {
            if (DgGrouped.Visibility == WpfVisibility.Visible)
            {
                foreach (var row in _visibleGroupedRows)
                {
                    row.IsChecked = true;
                }
            }
            else
            {
                foreach (var item in _visibleDetailedRows)
                {
                    item.IsSelected = true;
                }
            }
            UpdateSelectionStats();
        }

        private void TableClearSelection_Click(object sender, MouseButtonEventArgs e)
        {
            if (DgGrouped.Visibility == WpfVisibility.Visible)
            {
                foreach (var row in _visibleGroupedRows)
                {
                    row.IsChecked = false;
                }
            }
            else
            {
                foreach (var item in _visibleDetailedRows)
                {
                    item.IsSelected = false;
                }
            }
            UpdateSelectionStats();
        }

        private void ChkHeaderGrouped_Click(object sender, RoutedEventArgs e)
        {
            bool check = ChkHeaderGrouped.IsChecked == true;
            foreach (var row in _visibleGroupedRows)
            {
                row.IsChecked = check;
            }
            UpdateSelectionStats();
        }

        private void ChkHeaderDetailed_Click(object sender, RoutedEventArgs e)
        {
            bool check = ChkHeaderDetailed.IsChecked == true;
            foreach (var item in _visibleDetailedRows)
            {
                item.IsSelected = check;
            }
            UpdateSelectionStats();
        }

        private void RowCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateSelectionStats();
        }

        private void DetailedCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateSelectionStats();
        }

        #endregion

        #region Actions Execution (Select, Isolate, Colorize, Rename, Rebar Sets, Export)

        private void BtnSelectInRevit_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetSelectedElementIds();
            if (ids.Count == 0)
            {
                if (DgGrouped.Visibility == WpfVisibility.Visible && DgGrouped.SelectedItems.Count > 0)
                {
                    foreach (var item in DgGrouped.SelectedItems.OfType<PartitionTypeRowModel>())
                    {
                        foreach (var id in item.ElementIds) ids.Add(id);
                    }
                }
                else if (DgDetailed.Visibility == WpfVisibility.Visible && DgDetailed.SelectedItems.Count > 0)
                {
                    foreach (var item in DgDetailed.SelectedItems.OfType<RebarItemModel>())
                    {
                        ids.Add(item.RebarId);
                    }
                }
            }

            if (ids.Count == 0)
            {
                TaskDialog.Show("Partition Rebar", "Please select or check at least one rebar partition row to select in Revit.");
                return;
            }

            _handler.Action = (app) =>
            {
                try
                {
                    _service.SelectElementsInRevit(app.ActiveUIDocument, ids);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Selection Error", ex.Message);
                }
            };
            _exEvent.Raise();
        }

        private void BtnIsolateSelection_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetSelectedElementIds();
            if (ids.Count == 0)
            {
                TaskDialog.Show("Partition Rebar", "Please check at least one rebar row to isolate in view.");
                return;
            }

            _handler.Action = (app) =>
            {
                try
                {
                    _service.IsolateElementsInView(app.ActiveUIDocument, ids);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Isolate Error", ex.Message);
                }
            };
            _exEvent.Raise();
        }

        private void BtnResetView_Click(object sender, RoutedEventArgs e)
        {
            _handler.Action = (app) =>
            {
                try
                {
                    _service.ResetView(app.ActiveUIDocument);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Reset View Error", ex.Message);
                }
            };
            _exEvent.Raise();
        }

        private void BtnColorize_Click(object sender, RoutedEventArgs e)
        {
            if (_partitionGroups.Count == 0)
            {
                TaskDialog.Show("Partition Rebar", "No partition groups available to colorize.");
                return;
            }

            _handler.Action = (app) =>
            {
                try
                {
                    var doc = app.ActiveUIDocument.Document;
                    var view = app.ActiveUIDocument.ActiveView;
                    _service.ColorizePartitions(doc, view, _partitionGroups);
                    TaskDialog.Show("Colorize Partitions", $"Successfully applied color overrides to {_partitionGroups.Count} partitions in view \"{view.Name}\".");
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Colorize Error", ex.Message);
                }
            };
            _exEvent.Raise();
        }

        private void BtnResetColors_Click(object sender, RoutedEventArgs e)
        {
            var allIds = _allItems.Select(i => i.RebarId).Distinct().ToList();
            if (allIds.Count == 0) return;

            _handler.Action = (app) =>
            {
                try
                {
                    var doc = app.ActiveUIDocument.Document;
                    var view = app.ActiveUIDocument.ActiveView;
                    _service.ResetColors(doc, view, allIds);
                    TaskDialog.Show("Reset Colors", "Successfully cleared rebar color overrides in the active view.");
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Reset Colors Error", ex.Message);
                }
            };
            _exEvent.Raise();
        }

        private void BtnAssignPartition_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetSelectedElementIds();
            if (ids.Count == 0)
            {
                TaskDialog.Show("Partition Rebar", "Please check at least one rebar element or partition row to rename / assign.");
                return;
            }

            string promptTitle = "Set Rebar Partition";
            string currentPartition = _visibleGroupedRows.FirstOrDefault(r => r.IsChecked)?.Partition ?? string.Empty;
            if (currentPartition.Equals("(Unassigned)", StringComparison.OrdinalIgnoreCase)) currentPartition = string.Empty;

            var inputWindow = new Window
            {
                Title = promptTitle,
                Width = 420,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252))
            };

            var stack = new StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(new TextBlock
            {
                Text = $"Assign new Partition for {ids.Count} selected rebars:",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42))
            });

            var tb = new WpfTextBox
            {
                Text = currentPartition,
                Height = 34,
                Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stack.Children.Add(tb);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = "Assign Partition", Width = 120, Height = 32, Margin = new Thickness(0, 0, 8, 0), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(13, 110, 253)), Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold };
            var btnCancel = new Button { Content = "Cancel", Width = 80, Height = 32, Background = System.Windows.Media.Brushes.White };

            btnOk.Click += (s, args) => { inputWindow.DialogResult = true; inputWindow.Close(); };
            btnCancel.Click += (s, args) => { inputWindow.DialogResult = false; inputWindow.Close(); };

            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);
            inputWindow.Content = stack;

            if (inputWindow.ShowDialog() == true)
            {
                string newPartition = tb.Text.Trim();
                _handler.Action = (app) =>
                {
                    try
                    {
                        int updated = _service.UpdatePartition(app.ActiveUIDocument.Document, ids, newPartition);
                        TaskDialog.Show("Partition Updated", $"Successfully updated Partition to \"{newPartition}\" on {updated} rebar elements.");
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Update Partition Error", ex.Message);
                    }
                };
                _exEvent.Raise();

                // Reload after update
                Dispatcher.InvokeAsync(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(500);
                    ReloadData();
                });
            }
        }

        private void BtnSetRebarSets_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetSelectedElementIds();
            if (ids.Count == 0)
            {
                TaskDialog.Show("Partition Rebar", "Please check at least one rebar element or row to change 'Rebar Sets'.");
                return;
            }

            double currentSets = _visibleGroupedRows.FirstOrDefault(r => r.IsChecked)?.RebarSets ?? 1.0;
            if (currentSets <= 0) currentSets = 1.0;

            var inputWindow = new Window
            {
                Title = "Set Rebar Sets Multiplier",
                Width = 440,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252))
            };

            var stack = new StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(new TextBlock
            {
                Text = $"Set 'Rebar Sets' multiplier for {ids.Count} selected rebars:\n(Quantities, total lengths, and kg will follow this multiplier)",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42))
            });

            var tb = new WpfTextBox
            {
                Text = currentSets.ToString("G"),
                Height = 34,
                Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stack.Children.Add(tb);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = "Set Multiplier", Width = 120, Height = 32, Margin = new Thickness(0, 0, 8, 0), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(13, 110, 253)), Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold };
            var btnCancel = new Button { Content = "Cancel", Width = 80, Height = 32, Background = System.Windows.Media.Brushes.White };

            btnOk.Click += (s, args) => { inputWindow.DialogResult = true; inputWindow.Close(); };
            btnCancel.Click += (s, args) => { inputWindow.DialogResult = false; inputWindow.Close(); };

            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);
            inputWindow.Content = stack;

            if (inputWindow.ShowDialog() == true)
            {
                if (double.TryParse(tb.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedVal) && parsedVal > 0)
                {
                    _handler.Action = (app) =>
                    {
                        try
                        {
                            int updated = _service.UpdateRebarSets(app.ActiveUIDocument.Document, ids, parsedVal);
                            TaskDialog.Show("Rebar Sets Updated", $"Successfully updated 'Rebar Sets' to {parsedVal} on {updated} rebar elements.\nTotal weights, lengths, and bar counts recalculated.");
                        }
                        catch (Exception ex)
                        {
                            TaskDialog.Show("Update Error", ex.Message);
                        }
                    };
                    _exEvent.Raise();

                    // Reload after update
                    Dispatcher.InvokeAsync(async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(500);
                        ReloadData();
                    });
                }
                else
                {
                    TaskDialog.Show("Invalid Input", "Please enter a valid positive number for Rebar Sets (e.g. 1, 2, 3).");
                }
            }
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    FileName = $"Rebar_Partitions_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                    Title = "Export Partition Rebar Schedule to CSV"
                };

                if (sfd.ShowDialog() == true)
                {
                    _service.ExportToCsv(sfd.FileName, _visibleGroupedRows);
                    TaskDialog.Show("Export Successful", $"Exported {_visibleGroupedRows.Count} partition rows to:\n{sfd.FileName}");
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Export Error", ex.Message);
            }
        }

        private void Settings_Click(object sender, MouseButtonEventArgs e)
        {
            TaskDialog.Show("Partition Rebar Pro Settings", "BIM Plus — Partition Rebar Pro v1.0\n\nConfigure rebar steel density and calculation options in the project config.");
        }

        private void Help_Click(object sender, MouseButtonEventArgs e)
        {
            TaskDialog.Show("Partition Rebar Help", 
                "Partition Rebar Pro Quick Guide:\n\n" +
                "1. Scope: Choose Active View, Whole Model, or Current Selection.\n" +
                "2. Rebar Sets Multiplier: All bar quantities, lengths, and kg automatically multiply by the 'Rebar Sets' parameter value (e.g. 2 Sets multiplies by 2, matching Revit Schedules).\n" +
                "3. Filter: Click any Partition on the left sidebar to isolate it in the table, or use the search bar.\n" +
                "4. Select in Revit: Check partitions or types and click 'SELECT IN REVIT' to highlight them in Revit.\n" +
                "5. Isolate Selection: View only the checked rebars in your active 3D/2D view.\n" +
                "6. Colorize: Color-code every partition with distinct 3D view overrides.\n" +
                "7. Set Rebar Sets: Batch-modify the 'Rebar Sets' parameter for selected rebars.");
        }

        #endregion
    }
}
