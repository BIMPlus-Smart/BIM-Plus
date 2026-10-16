using System;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace AutoPileCap
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            string tabName = "BIM Plus⁺";
            string panelName = "Pile Cap Rebar";
            string columnPanelName = "Columns";

            // Create Ribbon Tab safely
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch
            {
                // Tab may already exist if previously created
            }

            RibbonPanel rebarPanel = GetOrCreateRibbonPanel(application, tabName, panelName);
            RibbonPanel columnPanel = GetOrCreateRibbonPanel(application, tabName, columnPanelName);
            RibbonPanel wallPanel = GetOrCreateRibbonPanel(application, tabName, "Walls");
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            // Pile Cap Rebar Button
            var btnRebarData = new PushButtonData(
                "btnPileCapRebar",
                "Pile Cap\nRebar",
                assemblyPath,
                "AutoPileCap.Commands.PileCapRebarCommand")
            {
                ToolTip = "Design and generate 3D reinforcement cages (bottom mat, top mat, and ties) inside structural pile caps.",
                LargeImage = LoadEmbeddedImage("pile_cap_rebar.jpg", 96) ?? CreateIcon(32, System.Windows.Media.Color.FromRgb(16, 185, 129), "⚡"),
                Image = LoadEmbeddedImage("pile_cap_rebar.jpg", 48) ?? CreateIcon(16, System.Windows.Media.Color.FromRgb(16, 185, 129), "⚡")
            };
            
            // Auto Section Box Button
            var btnSectionBoxData = new PushButtonData(
                "btnAutoSectionBox",
                "Auto Section\nBox",
                assemblyPath,
                "AutoPileCap.Commands.AutoSectionBoxCommand")
            {
                ToolTip = "Automatically generate a 3D view with a section box around selected elements.",
                LargeImage = LoadEmbeddedImage("auto_section_box.jpg", 96) ?? CreateIcon(32, System.Windows.Media.Color.FromRgb(239, 68, 68), "📦"),
                Image = LoadEmbeddedImage("auto_section_box.jpg", 48) ?? CreateIcon(16, System.Windows.Media.Color.FromRgb(239, 68, 68), "📦")
            };

            // Column Rebar Button
            var btnColumnRebarData = new PushButtonData(
                "btnColumnRebar",
                "Column\nRebar",
                assemblyPath,
                "AutoPileCap.Commands.ColumnRebarCommand")
            {
                ToolTip = "Design and generate 3D reinforcement for Rectangular, Circular, and L/T-Shaped columns with full options for main bars, ties, and lap splices.",
                LargeImage = LoadEmbeddedImage("column_rebar.jpg", 96) ?? CreateIcon(32, System.Windows.Media.Color.FromRgb(59, 130, 246), "🏗"),
                Image = LoadEmbeddedImage("column_rebar.jpg", 48) ?? CreateIcon(16, System.Windows.Media.Color.FromRgb(59, 130, 246), "🏗")
            };

            // Wall Rebar Button
            var btnWallRebarData = new PushButtonData(
                "btnWallRebar",
                "Wall\nRebar",
                assemblyPath,
                "AutoPileCap.Commands.WallRebarCommand")
            {
                ToolTip = "Design and generate 3D reinforcement cages for walls.",
                LargeImage = LoadEmbeddedImage("wall_rebar.jpg", 96) ?? CreateIcon(32, System.Windows.Media.Color.FromRgb(16, 185, 129), "🧱"),
                Image = LoadEmbeddedImage("wall_rebar.jpg", 48) ?? CreateIcon(16, System.Windows.Media.Color.FromRgb(16, 185, 129), "🧱")
            };

            rebarPanel.AddItem(btnRebarData);
            rebarPanel.AddItem(btnSectionBoxData);
            columnPanel.AddItem(btnColumnRebarData);
            wallPanel.AddItem(btnWallRebarData);

            RibbonPanel slabPanel = GetOrCreateRibbonPanel(application, tabName, "Slabs");
            var btnSlabRebarData = new PushButtonData("btnSlabRebar", "Slab\nRebar", assemblyPath, "AutoPileCap.Commands.SlabRebarCommand") { ToolTip = "Design and generate 3D reinforcement cages for slabs.", LargeImage = LoadEmbeddedImage("slab_rebar.jpg", 96) ?? CreateIcon(32, System.Windows.Media.Color.FromRgb(245, 158, 11), "S"), Image = LoadEmbeddedImage("slab_rebar.jpg", 48) ?? CreateIcon(16, System.Windows.Media.Color.FromRgb(245, 158, 11), "S") };
            slabPanel.AddItem(btnSlabRebarData);

            RibbonPanel beamPanel = GetOrCreateRibbonPanel(application, tabName, "Beams");
            var btnBeamRebarData = new PushButtonData(
                "btnBeamRebar",
                "Beam\nRebar",
                assemblyPath,
                "AutoPileCap.Commands.BeamRebarCommand")
            {
                ToolTip = "Design and generate 3D reinforcement cages for beams.",
                LargeImage = LoadEmbeddedImage("beam_rebar.jpg", 96) ?? CreateIcon(32, System.Windows.Media.Color.FromRgb(168, 85, 247), "B"),
                Image = LoadEmbeddedImage("beam_rebar.jpg", 48) ?? CreateIcon(16, System.Windows.Media.Color.FromRgb(168, 85, 247), "B")
            };
            beamPanel.AddItem(btnBeamRebarData);

            // Partitions Panel
            RibbonPanel partitionPanel = GetOrCreateRibbonPanel(application, tabName, "Partitions");
            var btnPartitionRebarData = new PushButtonData(
                "btnPartitionRebar",
                "Partition\nRebar",
                assemblyPath,
                "AutoPileCap.Commands.PartitionRebarCommand")
            {
                ToolTip = "Select, filter, isolate, colorize, and manage structural rebar grouped by Partition.",
                LargeImage = LoadEmbeddedImage("partition_rebar.jpg", 96) ?? CreateIcon(32, System.Windows.Media.Color.FromRgb(14, 165, 233), "📑"),
                Image = LoadEmbeddedImage("partition_rebar.jpg", 48) ?? CreateIcon(16, System.Windows.Media.Color.FromRgb(14, 165, 233), "📑")
            };
            partitionPanel.AddItem(btnPartitionRebarData);

            // Silent background auto-updater: checks online manifest and updates DLL seamlessly
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await AutoPileCap.Services.AutoUpdateService.CheckAndApplyUpdateSilentlyAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Ignore all errors to guarantee Revit stability
                }
            });

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private static RibbonPanel GetOrCreateRibbonPanel(UIControlledApplication application, string tabName, string panelName)
        {
            var panels = application.GetRibbonPanels(tabName);
            foreach (var p in panels)
            {
                if (p.Name.Equals(panelName, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }
            return application.CreateRibbonPanel(tabName, panelName);
        }

        private static BitmapSource LoadEmbeddedImage(string resourceName, int size)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var fullResourceName = $"AutoPileCap.Resources.{resourceName}";
                using (var stream = assembly.GetManifestResourceStream(fullResourceName))
                {
                    if (stream != null)
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.StreamSource = stream;
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.DecodePixelWidth = size;
                        image.DecodePixelHeight = size;
                        image.EndInit();
                        
                        var convertedImage = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
                        convertedImage.Freeze();
                        return convertedImage;
                    }
                }
            }
            catch {}
            return null;
        }

        private static BitmapSource CreateIcon(int size, System.Windows.Media.Color bgColor, string symbol)
        {
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                var bgBrush = new SolidColorBrush(bgColor);
                var pen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 255, 255, 255)), 1);
                dc.DrawRoundedRectangle(bgBrush, pen, new System.Windows.Rect(0, 0, size, size), size * 0.2, size * 0.2);

                var formattedText = new FormattedText(
                    symbol,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface(new System.Windows.Media.FontFamily("Segoe UI Emoji, Segoe UI Symbol, Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Bold, System.Windows.FontStretches.Normal),
                    size * 0.52,
                    System.Windows.Media.Brushes.White,
                    VisualTreeHelper.GetDpi(visual).PixelsPerDip);

                double x = (size - formattedText.Width) / 2.0;
                double y = (size - formattedText.Height) / 2.0;
                dc.DrawText(formattedText, new System.Windows.Point(x, y));
            }

            var renderTarget = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(visual);
            renderTarget.Freeze();
            return renderTarget;
        }
    }
}
