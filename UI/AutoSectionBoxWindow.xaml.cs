using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AutoPileCap.UI
{
    public partial class AutoSectionBoxWindow : Window
    {
        private Document _doc;
        private List<ElementId> _selectedIds;
        private List<View3D> _all3DViews;

        public AutoSectionBoxWindow(Document doc, List<ElementId> selectedIds)
        {
            InitializeComponent();
            _doc = doc;
            _selectedIds = selectedIds;
            LoadData();
        }

        private void LoadData()
        {
            // Load 3D Views
            _all3DViews = new FilteredElementCollector(_doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .Where(v => !v.IsTemplate && !v.IsAssemblyView)
                .OrderBy(v => v.Name)
                .ToList();

            foreach (var view in _all3DViews)
            {
                cmbViews.Items.Add(view.Name);
            }
            
            var activeView = _doc.ActiveView as View3D;
            if (activeView != null && !activeView.IsTemplate)
            {
                cmbViews.SelectedItem = activeView.Name;
            }
            else
            {
                cmbViews.Text = "Selection Box - " + DateTime.Now.ToString("HHmmss");
            }
            
            cmbViews.SelectionChanged += (s, e) => CheckViewStatus();
            cmbViews.KeyUp += (s, e) => CheckViewStatus();

            // Load View Templates
            var templates = new FilteredElementCollector(_doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .Where(v => v.IsTemplate)
                .OrderBy(v => v.Name)
                .ToList();

            cmbTemplates.Items.Add(new ViewTemplateItem { Name = "<None>", Id = ElementId.InvalidElementId });
            foreach (var t in templates)
            {
                cmbTemplates.Items.Add(new ViewTemplateItem { Name = t.Name, Id = t.Id });
            }
            cmbTemplates.SelectedIndex = 0;
            
            CheckViewStatus();
        }

        private void CheckViewStatus()
        {
            string viewName = cmbViews.Text;
            var existingView = _all3DViews.FirstOrDefault(v => v.Name.Equals(viewName, StringComparison.OrdinalIgnoreCase));
            if (existingView != null)
            {
                // Check if it's on a sheet
                bool onSheet = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .Any(vp => vp.ViewId == existingView.Id);

                if (onSheet)
                {
                    txtViewWarning.Text = "Existing View (Used On Sheet) Will Be Used";
                    txtViewWarning.Visibility = System.Windows.Visibility.Visible;
                    txtViewWarning.Foreground = System.Windows.Media.Brushes.Red;
                }
                else
                {
                    txtViewWarning.Text = "Existing View Will Be Overwritten";
                    txtViewWarning.Visibility = System.Windows.Visibility.Visible;
                    txtViewWarning.Foreground = System.Windows.Media.Brushes.DarkOrange;
                }
            }
            else
            {
                txtViewWarning.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(txtBuffer.Text, out double bufferMm))
            {
                MessageBox.Show("Please enter a valid number for the buffer.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string targetViewName = cmbViews.Text;
            if (string.IsNullOrWhiteSpace(targetViewName))
            {
                MessageBox.Show("Please specify a view name.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ElementId templateId = ElementId.InvalidElementId;
            if (cmbTemplates.SelectedItem is ViewTemplateItem item)
            {
                templateId = item.Id;
            }

            try
            {
                using (Transaction t = new Transaction(_doc, "Auto Section Box"))
                {
                    t.Start();

                    View3D targetView = _all3DViews.FirstOrDefault(v => v.Name.Equals(targetViewName, StringComparison.OrdinalIgnoreCase));
                    
                    if (targetView == null)
                    {
                        var viewFamilyType = new FilteredElementCollector(_doc)
                            .OfClass(typeof(ViewFamilyType))
                            .Cast<ViewFamilyType>()
                            .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                        targetView = View3D.CreateIsometric(_doc, viewFamilyType.Id);
                        targetView.Name = targetViewName;
                    }

                    // Compute Bounding Box
                    BoundingBoxXYZ combinedBox = null;
                    foreach (var id in _selectedIds)
                    {
                        var elem = _doc.GetElement(id);
                        if (elem != null)
                        {
                            BoundingBoxXYZ bbox = null;
                            if (elem is View view && view.CropBoxActive)
                            {
                                bbox = view.CropBox;
                            }
                            else if (elem.Category != null && elem.Category.Id.Value == (long)BuiltInCategory.OST_Viewers)
                            {
                                // If they selected the crop region/viewer element, use the active view's crop box if available
                                if (_doc.ActiveView != null && _doc.ActiveView.CropBoxActive)
                                {
                                    bbox = _doc.ActiveView.CropBox;
                                }
                            }
                            else
                            {
                                // Normal physical element
                                bbox = elem.get_BoundingBox(null);
                            }
                            
                            if (bbox != null)
                            {
                                // We want to maintain the Transform (orientation) if it's a rotated crop box
                                if (combinedBox == null)
                                {
                                    combinedBox = new BoundingBoxXYZ();
                                    combinedBox.Transform = bbox.Transform;
                                    combinedBox.Min = bbox.Min;
                                    combinedBox.Max = bbox.Max;
                                }
                                else
                                {
                                    // Transform the new bbox corners into the combinedBox local coordinate system
                                    Transform tToCombined = combinedBox.Transform.Inverse.Multiply(bbox.Transform);
                                    XYZ[] corners = new XYZ[8];
                                    corners[0] = tToCombined.OfPoint(new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Min.Z));
                                    corners[1] = tToCombined.OfPoint(new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Min.Z));
                                    corners[2] = tToCombined.OfPoint(new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Min.Z));
                                    corners[3] = tToCombined.OfPoint(new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Min.Z));
                                    corners[4] = tToCombined.OfPoint(new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Max.Z));
                                    corners[5] = tToCombined.OfPoint(new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Max.Z));
                                    corners[6] = tToCombined.OfPoint(new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Max.Z));
                                    corners[7] = tToCombined.OfPoint(new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Max.Z));

                                    combinedBox.Min = new XYZ(
                                        Math.Min(combinedBox.Min.X, corners.Min(c => c.X)),
                                        Math.Min(combinedBox.Min.Y, corners.Min(c => c.Y)),
                                        Math.Min(combinedBox.Min.Z, corners.Min(c => c.Z)));
                                    combinedBox.Max = new XYZ(
                                        Math.Max(combinedBox.Max.X, corners.Max(c => c.X)),
                                        Math.Max(combinedBox.Max.Y, corners.Max(c => c.Y)),
                                        Math.Max(combinedBox.Max.Z, corners.Max(c => c.Z)));
                                }
                            }
                        }
                    }

                    if (combinedBox != null)
                    {
                        double bufferFeet = bufferMm / 304.8;
                        XYZ bufferVec = new XYZ(bufferFeet, bufferFeet, bufferFeet);
                        
                        BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
                        sectionBox.Transform = combinedBox.Transform;
                        sectionBox.Min = combinedBox.Min - bufferVec;
                        sectionBox.Max = combinedBox.Max + bufferVec;
                        
                        targetView.IsSectionBoxActive = true;
                        targetView.SetSectionBox(sectionBox);
                    }

                    if (templateId != ElementId.InvalidElementId)
                    {
                        targetView.ViewTemplateId = templateId;
                    }

                    t.Commit();

                    // Open the view
                    var uidoc = new UIDocument(_doc);
                    uidoc.ActiveView = targetView;
                }
                
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error creating section box:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
        
        public class ViewTemplateItem
        {
            public string Name { get; set; }
            public ElementId Id { get; set; }
        }
    }
}
