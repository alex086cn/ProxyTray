
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ProxyTray
{
    public class Route
    {
        public string Name;
        public string Address;

        public Route(string name, string address)
        {
            Name = name;
            Address = address;
        }

        public string DisplayName
        {
            get
            {
                if (Name == null || Name.Length == 0) return Address;
                return Name;
            }
        }

        public override string ToString()
        {
            if (Name == Address || Name.Length == 0) return Address;
            return Name + "    " + Address;
        }
    }

    public static class Config
    {
        public static string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProxyTray");
        public static string RouteFile = Path.Combine(Dir, "routes.txt");
        public static string CurrentFile = Path.Combine(Dir, "current.txt");
        public static List<Route> Routes = new List<Route>();
        public static int Current = 0;

        public static void Load()
        {
            Routes.Clear();
            try
            {
                if (File.Exists(RouteFile))
                {
                    string[] lines = File.ReadAllLines(RouteFile, Encoding.UTF8);
                    foreach (string raw in lines)
                    {
                        string line = raw.Trim();
                        if (line.Length == 0) continue;
                        int p = line.IndexOf('|');
                        if (p > 0) Routes.Add(new Route(line.Substring(0, p).Trim(), line.Substring(p + 1).Trim()));
                        else Routes.Add(new Route(line, line));
                    }
                }
            }
            catch { }

            if (Routes.Count == 0)
            {
                Routes.Add(new Route("局域网代理", "192.168.199.250:7890"));
                SaveRoutes();
            }

            try
            {
                if (File.Exists(CurrentFile))
                    Current = int.Parse(File.ReadAllText(CurrentFile, Encoding.UTF8).Trim());
            }
            catch { Current = 0; }

            if (Current < 0 || Current >= Routes.Count) Current = 0;
        }

        public static void SaveRoutes()
        {
            try
            {
                if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
                List<string> lines = new List<string>();
                foreach (Route r in Routes) lines.Add(r.Name + "|" + r.Address);
                File.WriteAllLines(RouteFile, lines.ToArray(), Encoding.UTF8);
            }
            catch { }
        }

        public static void SaveCurrent()
        {
            try
            {
                if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
                File.WriteAllText(CurrentFile, Current.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static Route Cur
        {
            get
            {
                if (Routes.Count == 0) return new Route("", "");
                int i = Current;
                if (i < 0 || i >= Routes.Count) i = 0;
                return Routes[i];
            }
        }

        public static string CurAddress { get { return Cur.Address; } }
    }

    public static class Proxy
    {
        private const string RegPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;

        public static bool Enabled
        {
            get
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegPath, false))
                    {
                        if (k == null) return false;
                        object v = k.GetValue("ProxyEnable", 0);
                        return Convert.ToInt32(v) == 1;
                    }
                }
                catch { return false; }
            }
        }

        public static string Server
        {
            get
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegPath, false))
                    {
                        if (k == null) return "";
                        object v = k.GetValue("ProxyServer", "");
                        if (v == null) return "";
                        return v.ToString();
                    }
                }
                catch { return ""; }
            }
        }

        public static bool IsOn
        {
            get
            {
                if (!Enabled) return false;
                return string.Equals(Server.Trim(), Config.CurAddress.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void Set(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegPath, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue("ProxyServer", Config.CurAddress, RegistryValueKind.String);
                    k.SetValue("ProxyEnable", on ? 1 : 0, RegistryValueKind.DWord);
                }
            }
            catch { return; }

            try
            {
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
            }
            catch { }
        }
    }

    public static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "ProxyTray";

        public static bool IsOn()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    return k.GetValue(ValueName) != null;
                }
            }
            catch { return false; }
        }

        public static void Set(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"", RegistryValueKind.String);
                    else k.DeleteValue(ValueName, false);
                }
            }
            catch { }
        }
    }

    public static class Icons
    {
        public static readonly Color Ink = Color.FromArgb(68, 74, 86);
        public static readonly Color Accent = Color.FromArgb(46, 111, 232);
        public static readonly Color Green = Color.FromArgb(24, 160, 92);
        public static readonly Color Red = Color.FromArgb(214, 66, 60);

        public static Bitmap Canvas()
        {
            Bitmap b = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
            }
            return b;
        }

        public static Bitmap Dot(Color c)
        {
            Bitmap b = Canvas();
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush halo = new SolidBrush(Color.FromArgb(58, c))) g.FillEllipse(halo, 0.5f, 0.5f, 15, 15);
                using (SolidBrush solid = new SolidBrush(c)) g.FillEllipse(solid, 3.5f, 3.5f, 9, 9);
            }
            return b;
        }

        public static Bitmap Power(bool on)
        {
            Bitmap b = Canvas();
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Color c = on ? Red : Green;
                using (Pen p = new Pen(c, 1.6f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawArc(p, 3f, 4f, 10f, 10f, -52f, 284f);
                    g.DrawLine(p, 8f, 2f, 8f, 8f);
                }
            }
            return b;
        }

        public static Bitmap Routes()
        {
            Bitmap b = Canvas();
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(Ink, 1.4f)) p.StartCap = LineCap.Round;
                using (Pen p2 = new Pen(Ink, 1.4f))
                {
                    p2.StartCap = LineCap.Round;
                    p2.EndCap = LineCap.Round;
                    g.DrawLine(p2, 6.5f, 4f, 14f, 4f);
                    g.DrawLine(p2, 6.5f, 8f, 14f, 8f);
                    g.DrawLine(p2, 6.5f, 12f, 14f, 12f);
                    using (SolidBrush sb = new SolidBrush(Ink))
                    {
                        g.FillEllipse(sb, 1.5f, 2.5f, 3f, 3f);
                        g.FillEllipse(sb, 1.5f, 6.5f, 3f, 3f);
                        g.FillEllipse(sb, 1.5f, 10.5f, 3f, 3f);
                    }
                }
            }
            return b;
        }

        public static Bitmap Gear()
        {
            Bitmap b = Canvas();
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(Ink, 1.4f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawEllipse(p, 4.5f, 4.5f, 7f, 7f);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4.0;
                        float x1 = 8f + (float)(Math.Cos(a) * 5.4);
                        float y1 = 8f + (float)(Math.Sin(a) * 5.4);
                        float x2 = 8f + (float)(Math.Cos(a) * 7.4);
                        float y2 = 8f + (float)(Math.Sin(a) * 7.4);
                        g.DrawLine(p, x1, y1, x2, y2);
                    }
                }
            }
            return b;
        }

        public static Bitmap CheckBox(bool on)
        {
            Bitmap b = Canvas();
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(2, 2, 12, 12);
                if (on)
                {
                    using (GraphicsPath gp = Round(r, 3))
                    {
                        using (SolidBrush fb = new SolidBrush(Accent)) g.FillPath(fb, gp);
                    }
                    using (Pen wp = new Pen(Color.White, 1.9f))
                    {
                        wp.StartCap = LineCap.Round;
                        wp.EndCap = LineCap.Round;
                        g.DrawLines(wp, new PointF[] { new PointF(4.8f, 8.2f), new PointF(7f, 10.4f), new PointF(11.3f, 5.4f) });
                    }
                }
                else
                {
                    using (Pen p = new Pen(Color.FromArgb(158, 164, 174), 1.4f))
                    {
                        using (GraphicsPath gp = Round(r, 3)) g.DrawPath(p, gp);
                    }
                }
            }
            return b;
        }

        public static Bitmap Exit()
        {
            Bitmap b = Canvas();
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(Ink, 1.5f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawLines(p, new PointF[] { new PointF(9f, 2.5f), new PointF(2.5f, 2.5f), new PointF(2.5f, 13.5f), new PointF(9f, 13.5f) });
                    g.DrawLine(p, 7f, 8f, 14f, 8f);
                    g.DrawLines(p, new PointF[] { new PointF(11.5f, 5.5f), new PointF(14f, 8f), new PointF(11.5f, 10.5f) });
                }
            }
            return b;
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
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

    public class ModernColorTable : ProfessionalColorTable
    {
        private static readonly Color Panel = Color.FromArgb(252, 252, 254);

        public override Color ToolStripDropDownBackground { get { return Panel; } }
        public override Color ImageMarginGradientBegin { get { return Panel; } }
        public override Color ImageMarginGradientMiddle { get { return Panel; } }
        public override Color ImageMarginGradientEnd { get { return Panel; } }
        public override Color MenuBorder { get { return Color.FromArgb(200, 205, 215); } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuItemSelected { get { return Color.FromArgb(232, 240, 254); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(234, 242, 255); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(214, 230, 252); } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(222, 236, 254); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(206, 226, 251); } }
        public override Color SeparatorDark { get { return Color.FromArgb(227, 230, 236); } }
        public override Color SeparatorLight { get { return Panel; } }
        public override Color CheckBackground { get { return Color.Transparent; } }
        public override Color CheckSelectedBackground { get { return Color.Transparent; } }
        public override Color CheckPressedBackground { get { return Color.Transparent; } }
    }

    public class ModernRenderer : ToolStripProfessionalRenderer
    {
        public ModernRenderer() : base(new ModernColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            ToolStripItem item = e.Item;
            if (!item.Selected || !item.Enabled) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(4, 1, item.Width - 8, item.Height - 2);
            if (r.Width <= 0 || r.Height <= 0) return;

            using (GraphicsPath gp = Icons.Round(r, 5))
            {
                using (LinearGradientBrush lb = new LinearGradientBrush(
                    new Rectangle(r.X, r.Y, r.Width, r.Height + 1),
                    Color.FromArgb(236, 243, 255), Color.FromArgb(213, 229, 252), LinearGradientMode.Vertical))
                {
                    g.FillPath(lb, gp);
                }
                using (Pen pen = new Pen(Color.FromArgb(198, 220, 250)))
                {
                    g.DrawPath(pen, gp);
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (!e.Item.Enabled) e.TextColor = Color.FromArgb(104, 110, 120);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            int y = e.Item.Height / 2;
            using (Pen p = new Pen(Color.FromArgb(228, 231, 237)))
            {
                g.DrawLine(p, 10, y, e.Item.Width - 12, y);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Rectangle r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using (Pen p = new Pen(Color.FromArgb(198, 204, 214)))
            {
                e.Graphics.DrawRectangle(p, r);
            }
        }
    }

    public class AddRouteForm : Form
    {
        private TextBox txtName;
        private TextBox txtAddr;

        public string RouteName { get { return txtName.Text.Trim(); } }
        public string RouteAddress { get { return txtAddr.Text.Trim(); } }

        public AddRouteForm()
        {
            Text = "增加线路";
            Font = new Font("Microsoft YaHei", 9F);
            BackColor = Color.FromArgb(250, 251, 253);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(430, 186);

            Label l1 = new Label();
            l1.Text = "名称";
            l1.ForeColor = Color.FromArgb(86, 92, 102);
            l1.Location = new Point(20, 26);
            l1.Size = new Size(56, 20);
            Controls.Add(l1);

            txtName = new TextBox();
            txtName.Location = new Point(82, 23);
            txtName.Size = new Size(326, 24);
            txtName.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(txtName);

            Label l2 = new Label();
            l2.Text = "地址";
            l2.ForeColor = Color.FromArgb(86, 92, 102);
            l2.Location = new Point(20, 66);
            l2.Size = new Size(56, 20);
            Controls.Add(l2);

            txtAddr = new TextBox();
            txtAddr.Location = new Point(82, 63);
            txtAddr.Size = new Size(326, 24);
            txtAddr.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(txtAddr);

            Label l3 = new Label();
            l3.Text = "举例 192.168.199.250:7890，名称留空时自动使用地址";
            l3.ForeColor = Color.FromArgb(150, 156, 166);
            l3.Location = new Point(20, 96);
            l3.Size = new Size(390, 20);
            Controls.Add(l3);

            Button ok = new Button();
            ok.Text = "确定";
            ok.FlatStyle = FlatStyle.System;
            ok.Location = new Point(214, 136);
            ok.Size = new Size(92, 32);
            ok.Click += new EventHandler(OnOk);
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.FlatStyle = FlatStyle.System;
            cancel.Location = new Point(316, 136);
            cancel.Size = new Size(92, 32);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void OnOk(object sender, EventArgs e)
        {
            if (txtAddr.Text.Trim().Length == 0)
            {
                txtAddr.Focus();
                return;
            }
            if (txtName.Text.Trim().Length == 0) txtName.Text = txtAddr.Text.Trim();
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    public class SettingsForm : Form
    {
        private ListBox list;
        private Font fName = new Font("Microsoft YaHei", 9.5F, FontStyle.Regular);
        private Font fAddr = new Font("Microsoft YaHei", 8.5F, FontStyle.Regular);

        public SettingsForm()
        {
            Text = "代理设置";
            Font = new Font("Microsoft YaHei", 9F);
            BackColor = Color.FromArgb(250, 251, 253);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 366);

            Label title = new Label();
            title.Text = "代理线路";
            title.Font = new Font("Microsoft YaHei", 10.5F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(52, 58, 68);
            title.Location = new Point(20, 16);
            title.Size = new Size(200, 24);
            Controls.Add(title);

            Label sub = new Label();
            sub.Text = "绿色圆点为当前使用的线路";
            sub.ForeColor = Color.FromArgb(150, 156, 166);
            sub.Location = new Point(20, 42);
            sub.Size = new Size(300, 20);
            Controls.Add(sub);

            list = new ListBox();
            list.Location = new Point(20, 68);
            list.Size = new Size(480, 232);
            list.BorderStyle = BorderStyle.FixedSingle;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = 30;
            list.BackColor = Color.White;
            list.DrawItem += new DrawItemEventHandler(OnDrawItem);
            Controls.Add(list);

            Button btnAdd = new Button();
            btnAdd.Text = "增加";
            btnAdd.FlatStyle = FlatStyle.System;
            btnAdd.Location = new Point(20, 314);
            btnAdd.Size = new Size(104, 34);
            btnAdd.Click += new EventHandler(OnAdd);
            Controls.Add(btnAdd);

            Button btnDel = new Button();
            btnDel.Text = "删除";
            btnDel.FlatStyle = FlatStyle.System;
            btnDel.Location = new Point(136, 314);
            btnDel.Size = new Size(104, 34);
            btnDel.Click += new EventHandler(OnDel);
            Controls.Add(btnDel);

            Button btnClose = new Button();
            btnClose.Text = "关闭";
            btnClose.FlatStyle = FlatStyle.System;
            btnClose.Location = new Point(396, 314);
            btnClose.Size = new Size(104, 34);
            btnClose.DialogResult = DialogResult.OK;
            Controls.Add(btnClose);

            if (Config.Routes.Count == 0) Config.Load();
            Reload();
        }

        private void Reload()
        {
            list.Items.Clear();
            for (int i = 0; i < Config.Routes.Count; i++) list.Items.Add(Config.Routes[i]);
        }

        private void OnDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Config.Routes.Count) return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color back = selected ? Color.FromArgb(232, 240, 254) : Color.White;
            using (SolidBrush bb = new SolidBrush(back)) e.Graphics.FillRectangle(bb, e.Bounds);

            Route r = Config.Routes[e.Index];
            bool cur = (e.Index == Config.Current);

            Color dot = cur ? Color.FromArgb(24, 160, 92) : Color.FromArgb(198, 203, 212);
            using (SolidBrush db = new SolidBrush(dot)) e.Graphics.FillEllipse(db, e.Bounds.X + 10, e.Bounds.Y + 11, 9, 9);

            using (SolidBrush tb = new SolidBrush(Color.FromArgb(44, 50, 60)))
            {
                e.Graphics.DrawString(r.DisplayName, fName, tb, e.Bounds.X + 30, e.Bounds.Y + 5);
            }
            using (SolidBrush tb2 = new SolidBrush(Color.FromArgb(138, 144, 154)))
            {
                SizeF sz = e.Graphics.MeasureString(r.Address, fAddr);
                float x = e.Bounds.Right - sz.Width - 14;
                e.Graphics.DrawString(r.Address, fAddr, tb2, x, e.Bounds.Y + 7);
            }
        }

        private void OnAdd(object sender, EventArgs e)
        {
            using (AddRouteForm f = new AddRouteForm())
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    Config.Routes.Add(new Route(f.RouteName, f.RouteAddress));
                    Config.SaveRoutes();
                    Reload();
                    list.SelectedIndex = Config.Routes.Count - 1;
                }
            }
        }

        private void OnDel(object sender, EventArgs e)
        {
            int i = list.SelectedIndex;
            if (i < 0 || i >= Config.Routes.Count) return;
            if (Config.Routes.Count <= 1) return;

            Config.Routes.RemoveAt(i);
            if (Config.Current == i)
            {
                Config.Current = 0;
                Config.SaveCurrent();
                if (Proxy.Enabled) Proxy.Set(true);
            }
            else if (Config.Current > i)
            {
                Config.Current = Config.Current - 1;
                Config.SaveCurrent();
            }
            Config.SaveRoutes();
            Reload();
            if (list.Items.Count > 0)
            {
                int n = i;
                if (n >= list.Items.Count) n = list.Items.Count - 1;
                list.SelectedIndex = n;
            }
        }
    }

    public class TrayApp : ApplicationContext
    {
        private NotifyIcon icon;
        private ContextMenuStrip menu;
        private ModernRenderer renderer;
        private ToolStripMenuItem miStatus;
        private ToolStripMenuItem miToggle;
        private ToolStripMenuItem miRoutes;
        private ToolStripMenuItem miAuto;
        private ToolStripMenuItem miExit;
        private Icon iconOn;
        private Icon iconOff;
        private System.Windows.Forms.Timer timer;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public TrayApp()
        {
            Config.Load();
            iconOn = BuildTrayIcon(true);
            iconOff = BuildTrayIcon(false);

            renderer = new ModernRenderer();

            menu = new ContextMenuStrip();
            menu.Font = new Font("Microsoft YaHei", 9F);
            menu.Renderer = renderer;
            menu.ShowImageMargin = true;
            menu.ImageScalingSize = new Size(16, 16);
            menu.Padding = new Padding(2, 7, 2, 7);
            menu.BackColor = Color.FromArgb(252, 252, 254);

            miStatus = new ToolStripMenuItem("状态");
            miStatus.Enabled = false;

            miToggle = MakeItem("开启代理", Icons.Power(false), new EventHandler(OnToggle));

            miRoutes = new ToolStripMenuItem("切换线路");
            miRoutes.Image = Icons.Routes();
            miRoutes.ImageScaling = ToolStripItemImageScaling.None;
            miRoutes.Padding = new Padding(0, 3, 0, 3);
            ToolStripDropDownMenu dm = miRoutes.DropDown as ToolStripDropDownMenu;
            if (dm != null)
            {
                dm.Renderer = renderer;
                dm.Font = menu.Font;
                dm.ShowImageMargin = true;
                dm.ShowCheckMargin = true;
                dm.ImageScalingSize = new Size(16, 16);
                dm.Padding = new Padding(2, 7, 2, 7);
                dm.BackColor = Color.FromArgb(252, 252, 254);
            }

            miAuto = MakeItem("开机自启", Icons.CheckBox(false), new EventHandler(OnToggleAuto));

            miExit = MakeItem("退出", Icons.Exit(), new EventHandler(OnExit));

            menu.Items.Add(miStatus);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miToggle);
            menu.Items.Add(miRoutes);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miAuto);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miExit);

            menu.Opening += new CancelEventHandler(OnMenuOpening);

            icon = new NotifyIcon();
            icon.ContextMenuStrip = menu;
            icon.Text = "代理开关";
            icon.Visible = true;
            icon.DoubleClick += new EventHandler(OnIconDoubleClick);

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 2000;
            timer.Tick += new EventHandler(delegate(object s, EventArgs e) { RefreshState(); });
            timer.Start();

            BuildRouteMenu();
            RefreshState();
        }

        private ToolStripMenuItem MakeItem(string text, Bitmap img, EventHandler onClick)
        {
            ToolStripMenuItem mi = new ToolStripMenuItem(text);
            mi.Image = img;
            mi.ImageScaling = ToolStripItemImageScaling.None;
            mi.Padding = new Padding(0, 3, 0, 3);
            if (onClick != null) mi.Click += onClick;
            return mi;
        }

        private static Icon BuildTrayIcon(bool on)
        {
            int s = 32;
            Bitmap bmp = new Bitmap(s, s);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                Color top, bottom, ring;
                if (on)
                {
                    top = Color.FromArgb(102, 214, 130);
                    bottom = Color.FromArgb(22, 150, 86);
                    ring = Color.FromArgb(17, 126, 71);
                }
                else
                {
                    top = Color.FromArgb(250, 126, 116);
                    bottom = Color.FromArgb(198, 58, 54);
                    ring = Color.FromArgb(168, 46, 43);
                }

                Rectangle rc = new Rectangle(2, 2, s - 4, s - 4);
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse(rc);
                    using (LinearGradientBrush lb = new LinearGradientBrush(
                        new Rectangle(rc.X, rc.Y, rc.Width, rc.Height + 1), top, bottom, LinearGradientMode.Vertical))
                    {
                        g.FillPath(lb, gp);
                    }
                    using (Pen pen = new Pen(ring, 1.6f))
                    {
                        g.DrawPath(pen, gp);
                    }
                }

                using (GraphicsPath hl = new GraphicsPath())
                {
                    hl.AddEllipse(new Rectangle(6, 4, s - 12, 12));
                    using (PathGradientBrush pg = new PathGradientBrush(hl))
                    {
                        pg.CenterColor = Color.FromArgb(96, 255, 255, 255);
                        pg.SurroundColors = new Color[] { Color.FromArgb(0, 255, 255, 255) };
                        g.FillPath(pg, hl);
                    }
                }

                using (Pen wp = new Pen(Color.White, 2.8f))
                {
                    wp.StartCap = LineCap.Round;
                    wp.EndCap = LineCap.Round;
                    g.DrawArc(wp, 9f, 11f, 14f, 14f, -55f, 290f);
                    g.DrawLine(wp, 16f, 6.5f, 16f, 16f);
                }

                if (!on)
                {
                    using (Pen sp = new Pen(Color.White, 2.6f))
                    {
                        sp.StartCap = LineCap.Round;
                        sp.EndCap = LineCap.Round;
                        g.DrawLine(sp, 7.5f, 24.5f, 24.5f, 7.5f);
                    }
                }
            }

            IntPtr h = bmp.GetHicon();
            Icon tmp = Icon.FromHandle(h);
            Icon result = (Icon)tmp.Clone();
            DestroyIcon(h);
            bmp.Dispose();
            return result;
        }

        private void OnMenuOpening(object sender, CancelEventArgs e)
        {
            BuildRouteMenu();
            RefreshState();
        }

        private void BuildRouteMenu()
        {
            miRoutes.DropDownItems.Clear();

            for (int i = 0; i < Config.Routes.Count; i++)
            {
                int idx = i;
                Route r = Config.Routes[i];
                ToolStripMenuItem mi = new ToolStripMenuItem(r.ToString());
                mi.Checked = (idx == Config.Current);
                mi.CheckOnClick = false;
                mi.Image = idx == Config.Current ? Icons.Dot(Icons.Green) : null;
                mi.ImageScaling = ToolStripItemImageScaling.None;
                mi.Padding = new Padding(0, 3, 0, 3);
                mi.Click += new EventHandler(delegate(object s, EventArgs e) { SwitchRoute(idx); });
                miRoutes.DropDownItems.Add(mi);
            }

            miRoutes.DropDownItems.Add(new ToolStripSeparator());

            ToolStripMenuItem miSet = new ToolStripMenuItem("代理设置...");
            miSet.Image = Icons.Gear();
            miSet.ImageScaling = ToolStripItemImageScaling.None;
            miSet.Padding = new Padding(0, 3, 0, 3);
            miSet.Click += new EventHandler(delegate(object s, EventArgs e) { OpenSettings(); });
            miRoutes.DropDownItems.Add(miSet);
        }

        private void RefreshState()
        {
            bool on = Proxy.IsOn;
            bool auto = AutoStart.IsOn();

            try { icon.Icon = on ? iconOn : iconOff; } catch { }

            string name = Config.Cur.DisplayName;
            if (name.Length == 0) name = Config.CurAddress;

            miStatus.Text = on ? ("已开启  ·  " + name) : ("已关闭  ·  " + name);
            miStatus.Image = Icons.Dot(on ? Icons.Green : Icons.Red);
            miStatus.ImageScaling = ToolStripItemImageScaling.None;
            miStatus.Padding = new Padding(0, 4, 0, 4);

            miToggle.Text = on ? "关闭代理" : "开启代理";
            miToggle.Image = Icons.Power(on);

            miAuto.Text = auto ? "开机自启：已开启" : "开机自启：未开启";
            miAuto.Image = Icons.CheckBox(auto);

            string tip = on ? "代理开关：已开启" : "代理开关：已关闭";
            if (tip.Length > 63) tip = tip.Substring(0, 63);
            try { icon.Text = tip; } catch { }
        }

        private void OnIconDoubleClick(object sender, EventArgs e)
        {
            ToggleProxy();
        }

        private void OnToggle(object sender, EventArgs e)
        {
            ToggleProxy();
        }

        private void ToggleProxy()
        {
            Proxy.Set(!Proxy.IsOn);
            RefreshState();
        }

        private void SwitchRoute(int idx)
        {
            if (idx < 0 || idx >= Config.Routes.Count) return;
            Config.Current = idx;
            Config.SaveCurrent();
            if (Proxy.Enabled) Proxy.Set(true);
            RefreshState();
        }

        private void OpenSettings()
        {
            using (SettingsForm f = new SettingsForm())
            {
                f.ShowDialog();
            }
            BuildRouteMenu();
            RefreshState();
        }

        private void OnToggleAuto(object sender, EventArgs e)
        {
            AutoStart.Set(!AutoStart.IsOn());
            RefreshState();
        }

        private void OnExit(object sender, EventArgs e)
        {
            try { timer.Stop(); } catch { }
            try { icon.Visible = false; icon.Dispose(); } catch { }
            ExitThread();
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            bool created = false;
            Mutex mtx = new Mutex(true, "ProxyTray_v2_single_instance", out created);
            if (!created) return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApp());

            GC.KeepAlive(mtx);
        }
    }
}
