using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace IconConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.WriteLine("Only supported on Windows.");
                return;
            }

            string inputPath = @"C:\Users\ADMIN\.gemini\antigravity-ide\brain\61e8d573-cece-4568-a73a-d010c1a5f06b\.user_uploaded\media_1790913646889.jpg";
            string outputPath = @"..\BIM Plus.ico";

            try
            {
                // Load the image and convert to bitmap
                using (Image img = Image.FromFile(inputPath))
                {
                    using (Bitmap originalBmp = new Bitmap(img))
                    {
                        Console.WriteLine($"Loaded image with size: {originalBmp.Width}x{originalBmp.Height}");
                        
                        using (Bitmap resizedBmp = new Bitmap(256, 256))
                        {
                            using (Graphics g = Graphics.FromImage(resizedBmp))
                            {
                                g.Clear(Color.Transparent);
                                g.SmoothingMode = SmoothingMode.AntiAlias;
                                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                                // Draw the original image onto the 256x256 bitmap
                                g.DrawImage(originalBmp, new Rectangle(0, 0, 256, 256));
                            }

                            // Save as an icon
                            SaveAsIcon(resizedBmp, outputPath);
                            Console.WriteLine("Successfully created icon from JPEG.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        public static void SaveAsIcon(Bitmap bitmap, string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                // ICO header
                fs.WriteByte(0); fs.WriteByte(0);
                fs.WriteByte(1); fs.WriteByte(0);
                fs.WriteByte(1); fs.WriteByte(0); // 1 image

                int width = bitmap.Width;
                int height = bitmap.Height;
                if (width >= 256) width = 0;
                if (height >= 256) height = 0;

                fs.WriteByte((byte)width);
                fs.WriteByte((byte)height);
                fs.WriteByte(0);
                fs.WriteByte(0);
                fs.WriteByte(0); fs.WriteByte(0);
                fs.WriteByte(32); fs.WriteByte(0); // 32 bpp

                using (MemoryStream ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    byte[] pngData = ms.ToArray();

                    int size = pngData.Length;
                    fs.WriteByte((byte)(size & 0xFF));
                    fs.WriteByte((byte)((size >> 8) & 0xFF));
                    fs.WriteByte((byte)((size >> 16) & 0xFF));
                    fs.WriteByte((byte)((size >> 24) & 0xFF));

                    // Offset of image data
                    int offset = 22;
                    fs.WriteByte((byte)(offset & 0xFF));
                    fs.WriteByte((byte)((offset >> 8) & 0xFF));
                    fs.WriteByte((byte)((offset >> 16) & 0xFF));
                    fs.WriteByte((byte)((offset >> 24) & 0xFF));

                    fs.Write(pngData, 0, size);
                }
            }
        }
    }
}
