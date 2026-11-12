// ShieldGate - tạo tệp biểu tượng .ico từ nét vẽ logo, không cần tệp ảnh nguồn.
// Chạy trong lúc dựng: makico <đường dẫn .ico>
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace ShieldGate
{
    internal static class MakeIcon
    {
        private static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Cách dùng: makico <tệp.ico>");
                return 2;
            }

            int[] sizes = { 16, 24, 32, 48, 64, 256 };
            List<byte[]> images = new List<byte[]>();
            List<int> widths = new List<int>();

            foreach (int size in sizes)
            {
                using (Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.Clear(Color.Transparent);
                        float pad = size <= 24 ? 1f : 2f;
                        Icons.DrawLogo(g, new RectangleF(pad, pad, size - pad * 2, size - pad * 2),
                            Color.FromArgb(0x1F, 0x5E, 0x3D));
                    }
                    images.Add(size >= 256 ? Png(bmp) : Dib(bmp));
                    widths.Add(size);
                }
            }

            try
            {
                using (FileStream fs = new FileStream(args[0], FileMode.Create, FileAccess.Write))
                using (BinaryWriter w = new BinaryWriter(fs))
                {
                    w.Write((short)0);
                    w.Write((short)1);
                    w.Write((short)images.Count);

                    int offset = 6 + 16 * images.Count;
                    for (int i = 0; i < images.Count; i++)
                    {
                        int s = widths[i];
                        w.Write((byte)(s >= 256 ? 0 : s));
                        w.Write((byte)(s >= 256 ? 0 : s));
                        w.Write((byte)0);
                        w.Write((byte)0);
                        w.Write((short)1);
                        w.Write((short)32);
                        w.Write(images[i].Length);
                        w.Write(offset);
                        offset += images[i].Length;
                    }
                    foreach (byte[] data in images) w.Write(data);
                }
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine("Không tạo được biểu tượng: " + e.Message);
                return 1;
            }
        }

        private static byte[] Png(Bitmap bmp)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        // Ảnh BMP trong ICO: BITMAPINFOHEADER, chiều cao gấp đôi, các dòng từ dưới lên.
        private static byte[] Dib(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(40);
                bw.Write(w);
                bw.Write(h * 2);
                bw.Write((short)1);
                bw.Write((short)32);
                bw.Write(0);
                bw.Write(w * h * 4);
                bw.Write(2835);
                bw.Write(2835);
                bw.Write(0);
                bw.Write(0);

                for (int y = h - 1; y >= 0; y--)
                {
                    for (int x = 0; x < w; x++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        bw.Write(c.B);
                        bw.Write(c.G);
                        bw.Write(c.R);
                        bw.Write(c.A);
                    }
                }

                int maskRow = ((w + 31) / 32) * 4;
                for (int y = 0; y < h; y++)
                {
                    byte[] row = new byte[maskRow];
                    for (int x = 0; x < w; x++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        if (c.A < 128) row[x / 8] |= (byte)(1 << (7 - (x % 8)));
                    }
                    bw.Write(row);
                }

                bw.Flush();
                return ms.ToArray();
            }
        }
    }
}
