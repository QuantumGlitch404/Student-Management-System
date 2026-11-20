using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace StudentManagementSystem.Build
{
    /// <summary>
    /// Build-time only: draws the application icon and writes a multi-size .ico
    /// (PNG-compressed entries, Vista+). No third-party image library needed.
    /// Usage: MakeIcon.exe out.ico
    /// </summary>
    internal static class Program
    {
        private static readonly int[] Sizes = { 16, 24, 32, 48, 64, 128, 256 };

        private static int Main(string[] args)
        {
            string path = args.Length > 0 ? args[0] : "sms.ico";
            var images = new List<byte[]>();
            foreach (int size in Sizes) images.Add(Png(size));

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs))
            {
                w.Write((ushort)0);            // reserved
                w.Write((ushort)1);            // type: icon
                w.Write((ushort)Sizes.Length); // image count

                int offset = 6 + 16 * Sizes.Length;
                for (int i = 0; i < Sizes.Length; i++)
                {
                    int s = Sizes[i];
                    w.Write((byte)(s >= 256 ? 0 : s)); // width  (0 means 256)
                    w.Write((byte)(s >= 256 ? 0 : s)); // height
                    w.Write((byte)0);                  // palette colours
                    w.Write((byte)0);                  // reserved
                    w.Write((ushort)1);                // colour planes
                    w.Write((ushort)32);               // bits per pixel
                    w.Write(images[i].Length);         // bytes of image data
                    w.Write(offset);                   // offset of image data
                    offset += images[i].Length;
                }
                foreach (byte[] img in images) w.Write(img);
            }

            Console.WriteLine("Icon written: " + path + " (" + new FileInfo(path).Length + " bytes)");
            return 0;
        }

        /// <summary>Rounded navy tile with a white mortarboard and the letters SMS.</summary>
        private static byte[] Png(int size)
        {
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                float pad = size * 0.04f;
                var rect = new RectangleF(pad, pad, size - 2 * pad, size - 2 * pad);
                float radius = size * 0.22f;
                using (var path = RoundedRect(rect, radius))
                using (var brush = new LinearGradientBrush(rect,
                           Color.FromArgb(37, 59, 104), Color.FromArgb(16, 30, 58), 90f))
                    g.FillPath(brush, path);

                // mortarboard
                float cx = size / 2f, cy = size * 0.40f;
                float half = size * 0.30f, thickness = size * 0.10f;
                var board = new[]
                {
                    new PointF(cx, cy - thickness),
                    new PointF(cx + half, cy),
                    new PointF(cx, cy + thickness),
                    new PointF(cx - half, cy)
                };
                using (var white = new SolidBrush(Color.FromArgb(245, 248, 255)))
                    g.FillPolygon(white, board);

                // tassel
                using (var pen = new Pen(Color.FromArgb(120, 190, 255), Math.Max(1f, size * 0.035f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, cx + half * 0.72f, cy + thickness * 0.25f,
                                    cx + half * 0.72f, cy + size * 0.20f);
                }

                // SMS
                using (var font = new Font("Segoe UI", size * 0.21f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var text = new SolidBrush(Color.FromArgb(245, 248, 255)))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("SMS", font, text, new RectangleF(0, size * 0.58f, size, size * 0.34f), fmt);

                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
