using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LinkDrop.UI
{
    internal static class NativeMethods
    {
        public const int WM_NCHITTEST = 0x0084;
        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int HTCAPTION = 2;
        public const int HTLEFT = 10;
        public const int HTRIGHT = 11;
        public const int HTTOP = 12;
        public const int HTTOPLEFT = 13;
        public const int HTTOPRIGHT = 14;
        public const int HTBOTTOM = 15;
        public const int HTBOTTOMLEFT = 16;
        public const int HTBOTTOMRIGHT = 17;
        public const int WS_THICKFRAME = 0x00040000;
        public const int WS_CAPTION = 0x00C00000;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWCP_ROUND = 2;
        public const int DWMWCP_DONOTROUND = 1;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public static void SetRoundCorners(IntPtr handle, bool round)
        {
            try
            {
                int value = round ? DWMWCP_ROUND : DWMWCP_DONOTROUND;
                DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref value, sizeof(int));
            }
            catch { }
        }

        public static void SetBorderNone(IntPtr handle)
        {
            try
            {
                // DWMWA_BORDER_COLOR; DWMWA_COLOR_NONE removes the Windows 11 accent border.
                int color = unchecked((int)0xFFFFFFFE);
                DwmSetWindowAttribute(handle, 34, ref color, sizeof(int));
            }
            catch { }
        }

        public static void EnableWindowShadow(Form form)
        {
            try
            {
                if (form.Handle != IntPtr.Zero) SetRoundCorners(form.Handle, form.WindowState != FormWindowState.Maximized);
            }
            catch { }
        }
    }

    internal sealed class TitleStrip : Panel
    {
        private readonly ChromeButton _min;
        private readonly ChromeButton _max;
        private readonly ChromeButton _close;
        private bool _maximized;
        private bool _connected;
        private string _peerName = "";

        public bool Connected
        {
            get { return _connected; }
            set { if (_connected != value) { _connected = value; Invalidate(); } }
        }

        public string PeerName
        {
            get { return _peerName; }
            set { if (_peerName != value) { _peerName = value; Invalidate(); } }
        }

        public event EventHandler MinimizeRequested;
        public event EventHandler MaximizeRequested;
        public event EventHandler CloseRequested;

        public TitleStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Palette.Bg;
            Height = 58;
            _min = MakeChrome(IconKind.Minimize, false);
            _max = MakeChrome(IconKind.Maximize, false);
            _close = MakeChrome(IconKind.Close, true);
            _min.Click += delegate { if (MinimizeRequested != null) MinimizeRequested(this, EventArgs.Empty); };
            _max.Click += delegate { if (MaximizeRequested != null) MaximizeRequested(this, EventArgs.Empty); };
            _close.Click += delegate { if (CloseRequested != null) CloseRequested(this, EventArgs.Empty); };
            Controls.Add(_min);
            Controls.Add(_max);
            Controls.Add(_close);
        }

        private int S(int value)
        {
            return (int)Math.Round(value * DeviceDpi / 96f);
        }

        private ChromeButton MakeChrome(IconKind icon, bool close)
        {
            ChromeButton button = new ChromeButton();
            button.Icon = icon;
            button.IsClose = close;
            button.Size = new Size(S(40), S(30));
            return button;
        }

        public void SetMaximized(bool maximized)
        {
            _maximized = maximized;
            _max.Icon = maximized ? IconKind.Restore : IconKind.Maximize;
            _max.Invalidate();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (_min == null || _max == null || _close == null) return;
            int gap = S(4);
            int right = Width - S(14);
            _close.Size = new Size(S(40), S(30));
            _max.Size = new Size(S(40), S(30));
            _min.Size = new Size(S(40), S(30));
            _close.Location = new Point(right - _close.Width, (Height - _close.Height) / 2);
            _max.Location = new Point(_close.Left - _max.Width - gap, (Height - _max.Height) / 2);
            _min.Location = new Point(_max.Left - _min.Width - gap, (Height - _min.Height) / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            Gfx.HighQuality(g);
            g.Clear(Palette.Bg);
            int y = Height / 2;
            IconPainter.DrawLogo(g, new RectangleF(S(26), y - S(16), S(32), S(32)));
            Gfx.DrawString(g, "轻传", Fonts.BodyMedium, Palette.Ink,
                new RectangleF(S(66), S(9), S(100), S(24)), StringAlignment.Near, StringAlignment.Center, false);
            Gfx.DrawString(g, "LinkDrop", Fonts.Brand, Palette.Muted,
                new RectangleF(S(66), S(30), S(100), S(16)), StringAlignment.Near, StringAlignment.Center, false);

            using (Pen separator = new Pen(Palette.Line, 1))
                g.DrawLine(separator, S(142), S(18), S(142), Height - S(18));

            float x = S(160);
            if (_connected)
            {
                string label = "已连接" + (string.IsNullOrEmpty(_peerName) ? "" : " · " + _peerName);
                RectangleF pill = new RectangleF(x, y - S(13), Math.Min(S(360), g.MeasureString(label, Fonts.Small).Width + S(38)), S(26));
                using (GraphicsPath p = Gfx.Rounded(pill, S(13)))
                using (SolidBrush b = new SolidBrush(Palette.MintSoft)) g.FillPath(b, p);
                using (SolidBrush b = new SolidBrush(Palette.MintDeep)) g.FillEllipse(b, pill.X + S(12), pill.Y + S(10), S(6), S(6));
                Gfx.DrawString(g, label, Fonts.Small, Palette.Primary,
                    new RectangleF(pill.X + S(26), pill.Y, pill.Width - S(32), pill.Height), StringAlignment.Near, StringAlignment.Center, true);
            }
            else
            {
                RectangleF pill = new RectangleF(x, y - S(13), S(112), S(26));
                using (GraphicsPath p = Gfx.Rounded(pill, S(13)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(239, 243, 241))) g.FillPath(b, p);
                using (SolidBrush b = new SolidBrush(Palette.Muted)) g.FillEllipse(b, pill.X + S(13), pill.Y + S(10), S(6), S(6));
                Gfx.DrawString(g, "等待手机连接", Fonts.Small, Palette.InkSoft,
                    new RectangleF(pill.X + S(27), pill.Y, pill.Width - S(32), pill.Height), StringAlignment.Near, StringAlignment.Center, false);
            }

            using (Pen line = new Pen(Palette.LineSoft, 1))
                g.DrawLine(line, S(24), Height - 1, Width - S(24), Height - 1);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (e.Clicks == 2)
            {
                if (MaximizeRequested != null) MaximizeRequested(this, EventArgs.Empty);
                return;
            }
            Form form = FindForm();
            if (form != null)
            {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(form.Handle, NativeMethods.WM_NCLBUTTONDOWN, (IntPtr)NativeMethods.HTCAPTION, IntPtr.Zero);
            }
        }
    }

    internal class DialogBase : Form
    {
        private readonly Panel _header;
        private readonly Label _titleLabel;
        private readonly ChromeButton _close;
        private readonly Panel _body;
        private readonly Size _designSize;
        private bool _dpiApplied;

        public Panel Body { get { return _body; } }
        public Label HeaderLabel { get { return _titleLabel; } }
        protected int S(int value) { return LogicalToDeviceUnits(value); }

        public DialogBase(string title, Size size)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Palette.Bg;
            AutoScaleMode = AutoScaleMode.None;
            _designSize = size;
            ClientSize = size;
            DoubleBuffered = true;

            _header = new Panel();
            _header.BackColor = Palette.Bg;
            _header.Dock = DockStyle.Top;
            _header.Height = 56;
            _header.MouseDown += DragWindow;

            _titleLabel = new Label();
            _titleLabel.AutoSize = false;
            _titleLabel.BackColor = Color.Transparent;
            _titleLabel.Font = Fonts.BodyMedium;
            _titleLabel.ForeColor = Palette.Ink;
            _titleLabel.Text = title;
            _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            _titleLabel.Location = new Point(24, 0);
            _titleLabel.Size = new Size(400, 56);
            _titleLabel.MouseDown += DragWindow;
            _header.Controls.Add(_titleLabel);

            _close = new ChromeButton();
            _close.Icon = IconKind.Close;
            _close.IsClose = true;
            _close.Size = new Size(38, 30);
            _close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _close.Location = new Point(size.Width - 52, 13);
            _close.Click += delegate { Close(); };
            _header.Controls.Add(_close);

            _body = new Panel();
            _body.Dock = DockStyle.Fill;
            _body.BackColor = Palette.Bg;
            Controls.Add(_body);
            Controls.Add(_header);

            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
        }

        protected override void OnLoad(EventArgs e)
        {
            ApplyDpiLayout();
            base.OnLoad(e);
        }

        private void ApplyDpiLayout()
        {
            if (_dpiApplied) return;
            _dpiApplied = true;
            float scale = DeviceDpi / 96f;
            ClientSize = new Size((int)Math.Round(_designSize.Width * scale), (int)Math.Round(_designSize.Height * scale));
            _header.Height = S(56);
            _titleLabel.SetBounds(S(24), 0, ClientSize.Width - S(90), S(56));
            _close.Size = new Size(S(38), S(30));
            _close.Location = new Point(ClientSize.Width - S(52), S(13));
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, NativeMethods.WM_NCLBUTTONDOWN, (IntPtr)NativeMethods.HTCAPTION, IntPtr.Zero);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            NativeMethods.SetRoundCorners(Handle, true);
            NativeMethods.SetBorderNone(Handle);
        }

    }
}
