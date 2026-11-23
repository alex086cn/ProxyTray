
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class MakeIcon2
{
    public static void Main(string[] args)
    {
        string outIco = args.Length > 0 ? args[0] : "proxytray2.ico";
        string previewDir = args.Length > 1 ? args[1] : ".";

        int[] sizes = new int[] { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 };

        List<byte[]> frames = new List<byte[]>();
        foreach (int s in sizes)
        {
            using (Bitmap bmp = Draw(s, true))
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                frames.Add(ms.ToArray());
            }
        }

        using (FileStream fs = new FileStream(outIco, FileMode.Create))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write((ushort)0);
            w.Write((ushort)1);
            w.Write((ushort)frames.Count);
            int offset = 6 + 16 * frames.Count;
            for (int i = 0; i < frames.Count; i++)
            {
                int s = sizes[i];
                w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((ushort)1);
                w.Write((ushort)32);
                w.Write((uint)frames[i].Length);
                w.Write((uint)offset);
                offset += frames[i].Length;
            }
            foreach (byte[] f in frames) w.Write(f);
        }

        // 预览大图
        using (Bitmap big = Draw(512, true))
            big.Save(Path.Combine(previewDir, "icon_preview_512.png"), ImageFormat.Png);
        using (Bitmap big2 = Draw(512, false))
            big2.Save(Path.Combine(previewDir, "icon_preview_512_off.png"), ImageFormat.Png);

        // 实际尺寸拼图
        int[] row = new int[] { 16, 24, 32, 48, 64, 128, 256 };
        Bitmap sheet = new Bitmap(760, 300);
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(246, 247, 250));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int x = 20;
            foreach (int s in row)
            {
                using (Bitmap b = Draw(s, true))
                {
                    g.DrawImageUnscaled(b, x, 20);
                }
                x += s + 22;
            }
            using (Bitmap b256 = Draw(256, true)) g.DrawImageUnscaled(b256, 20, 20);
            using (Bitmap b48 = Draw(256, false)) g.DrawImage(b48, 300, 20, 256, 256);
        }
        sheet.Save(Path.Combine(previewDir, "icon_sheet.png"), ImageFormat.Png);
        sheet.Dispose();

        Console.WriteLine("OK " + outIco + " " + new FileInfo(outIco).Length);
    }

    private static Color C(int a, int r, int g, int b)
    {
        return Color.FromArgb(a, r, g, b);
    }

    private static Bitmap Draw(int s, bool on)
    {
        Bitmap bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        bool detail = s >= 32;

        // ===== 底板：深色圆角方 =====
        float pad = s * 0.03f;
        RectangleF rc = new RectangleF(pad, pad, s - pad * 2, s - pad * 2);
        float rad = s * 0.225f;

        using (GraphicsPath plate = Round(rc, rad))
        {
            using (LinearGradientBrush bg = new LinearGradientBrush(
                new RectangleF(rc.X, rc.Y, rc.Width, rc.Height),
                C(255, 34, 47, 82), C(255, 9, 15, 30), LinearGradientMode.ForwardDiagonal))
            {
                ColorBlend blend = new ColorBlend();
                blend.Colors = new Color[] { C(255, 38, 52, 92), C(255, 21, 30, 55), C(255, 8, 13, 26) };
                blend.Positions = new float[] { 0f, 0.55f, 1f };
                bg.InterpolationColors = blend;
                g.FillPath(bg, plate);
            }

            // 内侧细高光
            using (Pen inner = new Pen(C(70, 255, 255, 255), Math.Max(0.8f, s * 0.012f)))
            {
                inner.Alignment = PenAlignment.Inset;
                g.DrawPath(inner, plate);
            }

            // 顶部柔光
            if (detail)
            {
                using (GraphicsPath glowPath = (GraphicsPath)plate.Clone())
                {
                    Region old = g.Clip;
                    g.SetClip(plate, CombineMode.Intersect);
                    using (LinearGradientBrush topGlow = new LinearGradientBrush(
                        new RectangleF(rc.X, rc.Y, rc.Width, rc.Height * 0.62f),
                        C(46, 140, 190, 255), C(0, 140, 190, 255), LinearGradientMode.Vertical))
                    {
                        g.FillRectangle(topGlow, rc);
                    }
                    g.Clip = old;
                }
            }
        }

        float cx = s * 0.5f;
        float cy = s * 0.565f;
        float rOuter = s * 0.235f;
        float ringW = s * 0.095f;

        Color hi = on ? C(255, 74, 222, 168) : C(255, 255, 138, 120);
        Color lo = on ? C(255, 22, 168, 132) : C(255, 216, 58, 62);
        Color glow = on ? C(150, 45, 212, 160) : C(150, 240, 100, 96);

        // 环外发光
        if (detail)
        {
            using (Pen gp = new Pen(C(70, glow.R, glow.G, glow.B), ringW * 2.6f))
            {
                gp.StartCap = LineCap.Round;
                gp.EndCap = LineCap.Round;
                g.DrawArc(gp, cx - rOuter, cy - rOuter, rOuter * 2, rOuter * 2, -52f, 284f);
            }
        }

        // 主环
        RectangleF ringRect = new RectangleF(cx - rOuter, cy - rOuter, rOuter * 2, rOuter * 2);
        using (Pen pen = new Pen(C(255, 255, 255, 255), ringW))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            using (LinearGradientBrush lb = new LinearGradientBrush(
                new RectangleF(cx - rOuter, cy - rOuter, rOuter * 2, rOuter * 2), hi, lo, LinearGradientMode.ForwardDiagonal))
            {
                pen.Brush = lb;
                g.DrawArc(pen, ringRect, -52f, 284f);
            }
        }

        // 竖杆
        using (Pen bar = new Pen(C(255, 255, 255, 255), ringW))
        {
            bar.StartCap = LineCap.Round;
            bar.EndCap = LineCap.Round;
            using (LinearGradientBrush lb2 = new LinearGradientBrush(
                new RectangleF(cx - ringW, s * 0.22f, ringW * 2, s * 0.30f), hi, lo, LinearGradientMode.Vertical))
            {
                bar.Brush = lb2;
                g.DrawLine(bar, cx, s * 0.225f, cx, cy - rOuter * 0.42f);
            }
        }

        if (!on)
        {
            // 关闭态：斜杠
            using (Pen sp = new Pen(C(235, 236, 242, 250), Math.Max(1.6f, s * 0.075f)))
            {
                sp.StartCap = LineCap.Round;
                sp.EndCap = LineCap.Round;
                g.DrawLine(sp, s * 0.28f, s * 0.74f, s * 0.72f, s * 0.28f);
            }
        }

        // 数据轨道弧（细节层）
        if (detail)
        {
            using (Pen orbt = new Pen(C(120, 120, 205, 255), Math.Max(1.2f, s * 0.022f)))
            {
                orbt.StartCap = LineCap.Round;
                orbt.EndCap = LineCap.Round;
                float ro = s * 0.395f;
                g.DrawArc(orbt, cx - ro, cy - ro, ro * 2, ro * 2, 152f, 56f);
                g.DrawArc(orbt, cx - ro, cy - ro, ro * 2, ro * 2, -28f, 56f);
            }
            // 轨道端点小圆
            using (SolidBrush dot = new SolidBrush(C(210, 120, 205, 255)))
            {
                float ro = s * 0.395f;
                double a1 = 152.0 * Math.PI / 180.0;
                double a2 = 28.0 * Math.PI / 180.0;
                float d = Math.Max(1.6f, s * 0.032f);
                g.FillEllipse(dot, cx + (float)Math.Cos(a1) * ro - d / 2, cy + (float)Math.Sin(a1) * ro - d / 2, d, d);
                g.FillEllipse(dot, cx + (float)Math.Cos(a2) * ro - d / 2, cy + (float)Math.Sin(a2) * ro - d / 2, d, d);
            }
        }

        g.Dispose();
        return bmp;
    }

    private static GraphicsPath Round(RectangleF r, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        float d = radius * 2f;
        if (d > r.Width) d = r.Width;
        if (d > r.Height) d = r.Height;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
