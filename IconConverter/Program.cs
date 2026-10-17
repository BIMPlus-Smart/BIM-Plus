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
                int[] sizes = new int[] { 256, 128, 64, 48, 32, 24, 16 };
                var bitmaps = new Bitmap[sizes.Length];

                using (Image img = Image.FromFile(inputPath))
                {
                    using (Bitmap originalBmp = new Bitmap(img))
                    {
                        Console.WriteLine($"Loaded image with size: {originalBmp.Width}x{originalBmp.Height}");
                        
                        for (int i = 0; i < sizes.Length; i++)
                        {
                            int s = sizes[i];
                            var resizedBmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
                            using (Graphics g = Graphics.FromImage(resizedBmp))
                            {
                                g.Clear(Color.Transparent);
                                g.SmoothingMode = SmoothingMode.HighQuality;
                                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                                g.CompositingQuality = CompositingQuality.HighQuality;
                                g.DrawImage(originalBmp, new Rectangle(0, 0, s, s));
                            }
                            bitmaps[i] = resizedBmp;
                        }

                        SaveMultiIcon(bitmaps, sizes, outputPath);
                        Console.WriteLine("Successfully created multi-resolution icon.");
                    }
                }

                foreach (var b in bitmaps) b.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        public static void SaveMultiIcon(Bitmap[] bitmaps, int[] sizes, string filePath)
        {
            var pngBuffers = new byte[bitmaps.Length][];
            for (int i = 0; i < bitmaps.Length; i++)
            {
                using var ms = new MemoryStream();
                bitmaps[i].Save(ms, ImageFormat.Png);
                pngBuffers[i] = ms.ToArray();
            }

            using var fs = new FileStream(filePath, FileMode.Create);
            // ICO Header
            fs.WriteByte(0); fs.WriteByte(0); // Reserved
            fs.WriteByte(1); fs.WriteByte(0); // Type 1 = Icon
            short count = (short)bitmaps.Length;
            fs.WriteByte((byte)(count & 0xFF));
            fs.WriteByte((byte)((count >> 8) & 0xFF));

            int currentOffset = 6 + (bitmaps.Length * 16);

            // Write directory entries
            for (int i = 0; i < bitmaps.Length; i++)
            {
                int s = sizes[i];
                byte w = (byte)(s >= 256 ? 0 : s);
                byte h = (byte)(s >= 256 ? 0 : s);

                fs.WriteByte(w);
                fs.WriteByte(h);
                fs.WriteByte(0); // Colors
                fs.WriteByte(0); // Reserved
                fs.WriteByte(1); fs.WriteByte(0); // Color planes
                fs.WriteByte(32); fs.WriteByte(0); // Bits per pixel

                int len = pngBuffers[i].Length;
                fs.WriteByte((byte)(len & 0xFF));
                fs.WriteByte((byte)((len >> 8) & 0xFF));
                fs.WriteByte((byte)((len >> 16) & 0xFF));
                fs.WriteByte((byte)((len >> 24) & 0xFF));

                fs.WriteByte((byte)(currentOffset & 0xFF));
                fs.WriteByte((byte)((currentOffset >> 8) & 0xFF));
                fs.WriteByte((byte)((currentOffset >> 16) & 0xFF));
                fs.WriteByte((byte)((currentOffset >> 24) & 0xFF));

                currentOffset += len;
            }

            // Write image data
            for (int i = 0; i < bitmaps.Length; i++)
            {
                fs.Write(pngBuffers[i], 0, pngBuffers[i].Length);
            }
        }
    }
}
