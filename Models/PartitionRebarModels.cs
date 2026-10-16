using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Autodesk.Revit.DB;

namespace AutoPileCap.Models
{
    public enum SelectionScope
    {
        ActiveView,
        WholeModel,
        CurrentSelection
    }

    /// <summary>
    /// Represents an individual Structural Rebar or Rebar Set in Revit.
    /// </summary>
    public class RebarItemModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public ElementId RebarId { get; set; } = ElementId.InvalidElementId;
        public string CategoryName { get; set; } = "Structural Rebar";
        public string FamilyName { get; set; } = "Rebar Bar";
        public string TypeName { get; set; } = string.Empty;
        public ElementId TypeId { get; set; } = ElementId.InvalidElementId;
        public string Partition { get; set; } = "(Unassigned)";
        public string HostCategory { get; set; } = "None";
        public string HostName { get; set; } = string.Empty;
        public ElementId HostId { get; set; } = ElementId.InvalidElementId;
        public string ShapeName { get; set; } = "-";

        /// <summary>
        /// Single set bar quantity modeled in Revit (e.g. 2).
        /// </summary>
        public int SingleQuantity { get; set; } = 1;

        /// <summary>
        /// Multiplier from "Rebar Sets" parameter (e.g. 2 if representing 2 columns).
        /// </summary>
        public double RebarSets { get; set; } = 1.0;

        /// <summary>
        /// Total bars = SingleQuantity * RebarSets (matches 'Total Num. of B' in Revit schedule).
        /// </summary>
        public int Quantity { get; set; } = 1;

        public double DiameterMm { get; set; } = 0.0;

        /// <summary>
        /// Total bar length in meters multiplied by Rebar Sets (matches 'Total Bar Lengt' in Revit schedule).
        /// </summary>
        public double TotalLengthM { get; set; } = 0.0;

        /// <summary>
        /// Total weight in kg multiplied by Rebar Sets (matches 'Total Weight (kg)' in Revit schedule).
        /// </summary>
        public double WeightKg { get; set; } = 0.0;

        public string ScheduleMark { get; set; } = string.Empty;
        public string RebarNumber { get; set; } = string.Empty;

        public System.Windows.Media.Color Color { get; set; } = System.Windows.Media.Colors.DodgerBlue;
        public SolidColorBrush ColorBrush { get; set; } = new SolidColorBrush(System.Windows.Media.Colors.DodgerBlue);

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// Represents a Partition Group summary for the left sidebar filter.
    /// </summary>
    public class PartitionGroupItem : INotifyPropertyChanged
    {
        private bool? _isChecked = false;
        private bool _isHighlighted;

        private System.Windows.Media.Color _color = System.Windows.Media.Colors.DodgerBlue;
        private SolidColorBrush _colorBrush = new SolidColorBrush(System.Windows.Media.Colors.DodgerBlue);
        private SolidColorBrush? _mediumColorBrush;
        private SolidColorBrush? _borderColorBrush;

        public string PartitionName { get; set; } = string.Empty;
        public int ItemCount { get; set; } = 0;
        public int TotalBarQuantity { get; set; } = 0;
        public double TotalLengthM { get; set; } = 0.0;
        public double TotalWeightKg { get; set; } = 0.0;
        public List<RebarItemModel> Items { get; set; } = new List<RebarItemModel>();

        public System.Windows.Media.Color Color
        {
            get => _color;
            set
            {
                _color = value;
                _mediumColorBrush = null;
                _borderColorBrush = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MediumColorBrush));
                OnPropertyChanged(nameof(BorderColorBrush));
            }
        }

        public SolidColorBrush ColorBrush
        {
            get => _colorBrush;
            set
            {
                _colorBrush = value;
                _mediumColorBrush = null;
                _borderColorBrush = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MediumColorBrush));
                OnPropertyChanged(nameof(BorderColorBrush));
            }
        }

        /// <summary>
        /// Medium soft pastel background tint (approx 16% opacity) for high contrast readability of dark text.
        /// </summary>
        public SolidColorBrush MediumColorBrush
        {
            get
            {
                if (_mediumColorBrush == null)
                {
                    var c = System.Windows.Media.Color.FromArgb(40, Color.R, Color.G, Color.B);
                    _mediumColorBrush = new SolidColorBrush(c);
                    _mediumColorBrush.Freeze();
                }
                return _mediumColorBrush;
            }
        }

        /// <summary>
        /// Medium matching border outline (approx 37% opacity) for crisp card definition.
        /// </summary>
        public SolidColorBrush BorderColorBrush
        {
            get
            {
                if (_borderColorBrush == null)
                {
                    var c = System.Windows.Media.Color.FromArgb(95, Color.R, Color.G, Color.B);
                    _borderColorBrush = new SolidColorBrush(c);
                    _borderColorBrush.Freeze();
                }
                return _borderColorBrush;
            }
        }

        public bool? IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                if (_isHighlighted != value)
                {
                    _isHighlighted = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// Represents a table row grouped by Partition + Type (matches DiRoots OneFilter table from user image).
    /// </summary>
    public class PartitionTypeRowModel : INotifyPropertyChanged
    {
        private bool _isChecked;

        public string CategoryName { get; set; } = "Structural Rebar";
        public string FamilyName { get; set; } = "Rebar Bar";
        public string TypeName { get; set; } = string.Empty;
        public string Partition { get; set; } = string.Empty;
        public string HostCategory { get; set; } = "None";
        public string ShapeName { get; set; } = "-";
        public double DiameterMm { get; set; } = 0.0;
        public int Count { get; set; } = 0; // Number of modeled rebar sets/elements

        /// <summary>
        /// Display text for Rebar Sets multiplier (e.g. "2" or "1" or "<varies>").
        /// </summary>
        public string RebarSetsDisplay { get; set; } = "1";
        public double RebarSets { get; set; } = 1.0;

        /// <summary>
        /// Total bars across all sets = SingleSetBars * RebarSets.
        /// </summary>
        public int BarQuantity { get; set; } = 0;

        public double TotalLengthM { get; set; } = 0.0;
        public double TotalWeightKg { get; set; } = 0.0;

        public List<ElementId> ElementIds { get; set; } = new List<ElementId>();
        public List<RebarItemModel> ChildItems { get; set; } = new List<RebarItemModel>();

        public System.Windows.Media.Color Color { get; set; } = System.Windows.Media.Colors.DodgerBlue;
        public SolidColorBrush ColorBrush { get; set; } = new SolidColorBrush(System.Windows.Media.Colors.DodgerBlue);

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();
                    foreach (var item in ChildItems)
                    {
                        item.IsSelected = value;
                    }
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
