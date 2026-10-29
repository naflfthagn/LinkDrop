using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LinkDrop.UI
{
    internal class CardPanel : Panel
    {
        public int Radius { get; set; }
        public Color FillColor { get; set; }
        public bool DrawBorder { get; set; }
        public bool DrawShadow { get; set; }

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Radius = 22;
            FillColor = Palette.Card;
            DrawBorder = true;
            DrawShadow = true;
        }

        protected int S(int value)
        {
            return (int)Math.Round(value * DeviceDpi / 96f);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Gfx.HighQuality(e.Graphics);
            Color parent = Parent != null ? Parent.BackColor : Palette.Bg;
            e.Graphics.Clear(parent);
            RectangleF rect = new RectangleF(0f, 0f, Width, Height);
            if (DrawShadow)
            {
                int spread = S(5);
                for (int i = spread; i >= 1; i -= 2)
                {
                    int alpha = 5 + (spread - i);
                    using (GraphicsPath path = Gfx.Rounded(new RectangleF(i, i + S(3), Width - i * 2, Height - i * 2 - S(1)), Math.Max(1, Radius - i / 2f)))
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, 27, 61, 55)))
                        e.Graphics.FillPath(brush, path);
                }
            }
            float radius = LogicalToDeviceUnits(Radius);
            RectangleF card = new RectangleF(S(1), S(1), Width - S(2), Height - S(2));
            using (GraphicsPath path = Gfx.Rounded(card, radius))
            using (SolidBrush brush = new SolidBrush(FillColor))
                e.Graphics.FillPath(brush, path);
            if (DrawBorder)
            {
                Gfx.StrokeRounded(e.Graphics, card, radius, Palette.Line, 1f);
            }
        }
    }

    internal sealed class ConnectionHero : CardPanel
    {
        public bool Connected { get; set; }
        public string PeerName { get; set; }
        public string PairedName { get; set; }
        public string Address { get; set; }
        public string DeviceName { get; set; }
        public PillButton SendButton { get; private set; }

        public ConnectionHero()
        {
            Radius = 24;
            DrawBorder = false;
            DrawShadow = true;
            FillColor = Palette.Primary;
            PeerName = "";
            PairedName = "";
            Address = "";
            DeviceName = "";
            SendButton = new PillButton();
            SendButton.Text = "发送文件";
            SendButton.Icon = IconKind.Send;
            SendButton.Kind = ButtonKind.Mint;
            SendButton.Size = new Size(152, 44);
            SendButton.Click += OnSendClicked;
            Controls.Add(SendButton);
            LayoutChildren();
        }

        public event EventHandler SendRequested;

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            LayoutChildren();
        }

        private void LayoutChildren()
        {
            if (SendButton == null) return;
            SendButton.Size = new Size(S(152), S(44));
            SendButton.Location = new Point(S(24), Height - SendButton.Height - S(22));
        }

        private void OnSendClicked(object sender, EventArgs e)
        {
            if (SendRequested != null) SendRequested(this, EventArgs.Empty);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Gfx.HighQuality(e.Graphics);
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Palette.Bg);
            RectangleF card = new RectangleF(1, 1, Width - 2, Height - 2);
            float heroRadius = LogicalToDeviceUnits(Radius);
            // soft shadow
            for (int i = 6; i >= 1; i -= 2)
            {
                int di = S(i);
                using (GraphicsPath p = Gfx.Rounded(new RectangleF(di, di + S(4), Width - di * 2, Height - di * 2 - S(2)), Math.Max(S(4), heroRadius - di)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(5 + (6 - i) * 2, 10, 54, 48)))
                    e.Graphics.FillPath(b, p);
            }
            using (GraphicsPath clip = Gfx.Rounded(card, heroRadius))
            {
                GraphicsState state = e.Graphics.Save();
                e.Graphics.SetClip(clip);
                using (LinearGradientBrush gradient = new LinearGradientBrush(card, Palette.HeroTop, Palette.HeroBottom, 18f))
                    e.Graphics.FillRectangle(gradient, card);
                // ambient glows
                using (GraphicsPath glow = new GraphicsPath())
                {
                    glow.AddEllipse(Width - S(240), -S(120), S(340), S(340));
                    using (PathGradientBrush brush = new PathGradientBrush(glow))
                    {
                        brush.CenterColor = Color.FromArgb(44, 102, 240, 211);
                        brush.SurroundColors = new Color[] { Color.FromArgb(0, 102, 240, 211) };
                        e.Graphics.FillPath(brush, glow);
                    }
                }
                using (Pen ring = new Pen(Color.FromArgb(28, 191, 255, 235), Math.Max(1, S(1))))
                {
                    e.Graphics.DrawEllipse(ring, Width - S(230), S(20), S(270), S(270));
                    e.Graphics.DrawEllipse(ring, Width - S(190), S(175), S(360), S(360));
                    e.Graphics.DrawEllipse(ring, -S(90), Height - S(140), S(230), S(230));
                }
                e.Graphics.Restore(state);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            Gfx.HighQuality(g);
            int pad = S(24);
            int rightReserve = S(230);
            int textWidth = Math.Max(S(220), Width - pad - rightReserve);

            // status pill
            RectangleF badge = new RectangleF(pad, S(20), Connected ? S(122) : S(138), S(28));
            using (GraphicsPath p = Gfx.Rounded(badge, badge.Height / 2))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
                g.FillPath(b, p);
            using (SolidBrush dot = new SolidBrush(Connected ? Palette.Mint : Color.FromArgb(244, 192, 93)))
                g.FillEllipse(dot, badge.X + S(11), badge.Y + badge.Height / 2 - S(3), S(6), S(6));
            Gfx.DrawString(g, Connected ? "已安全连接" : "等待连接", Fonts.SmallMedium,
                Color.FromArgb(233, 250, 246), new RectangleF(badge.X + S(24), badge.Y, badge.Width - S(30), badge.Height),
                StringAlignment.Near, StringAlignment.Center, false);

            string title = Connected && !string.IsNullOrEmpty(PeerName) ? PeerName : (string.IsNullOrEmpty(PairedName) ? "让文件走一条近路" : "等待手机连接");
            Gfx.DrawString(g, title, Fonts.Hero, Color.White,
                new RectangleF(pad, S(62), textWidth, S(36)), StringAlignment.Near, StringAlignment.Center, true);

            string subtitle;
            if (Connected)
                subtitle = "手机已就绪 · 局域网 TLS 加密直连";
            else if (!string.IsNullOrEmpty(PairedName))
                subtitle = "已配对 " + PairedName + "，打开手机端轻传后会自动连接";
            else
                subtitle = "同一 Wi-Fi 下，手机与电脑配对后即可直接互传文件";
            Gfx.DrawString(g, subtitle, Fonts.Body, Color.FromArgb(202, 232, 226),
                new RectangleF(pad, S(102), textWidth, S(24)), StringAlignment.Near, StringAlignment.Center, true);

            // send button and hint at bottom
            RectangleF hint = new RectangleF(SendButton.Right + S(16), SendButton.Top, Width - SendButton.Right - S(34), SendButton.Height);
            string hintText = Connected ? "也可以把文件直接拖进窗口" : (string.IsNullOrEmpty(PairedName) ? "配对新手机后即可发送" : "电脑端会持续等待自动重连");
            Gfx.DrawString(g, hintText, Fonts.Small, Color.FromArgb(170, 214, 205), hint, StringAlignment.Near, StringAlignment.Center, true);

            DrawDeviceArt(g);
        }

        private void DrawDeviceArt(Graphics g)
        {
            int iconArea = S(160);
            float x = Width - iconArea - S(34);
            float y = S(50);
            using (SolidBrush circle = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
                g.FillEllipse(circle, x - S(10), y - S(5), iconArea + S(25), iconArea + S(25));
            using (Pen border = new Pen(Color.FromArgb(28, 210, 249, 240), S(1)))
                g.DrawEllipse(border, x - S(10), y - S(5), iconArea + S(25), iconArea + S(25));

            if (Connected)
            {
                IconPainter.Draw(g, IconKind.Laptop, new RectangleF(x + S(4), y + S(25), S(82), S(82)), Color.White, 1.4f);
                IconPainter.Draw(g, IconKind.Phone, new RectangleF(x + S(96), y + S(34), S(52), S(52)), Palette.Mint, 1.6f);
                using (Pen link = new Pen(Color.FromArgb(160, 137, 239, 218), S(2)))
                {
                    link.StartCap = LineCap.Round;
                    link.EndCap = LineCap.Round;
                    g.DrawLine(link, x + S(84), y + S(68), x + S(96), y + S(62));
                }
            }
            else
            {
                IconPainter.Draw(g, IconKind.Phone, new RectangleF(x + S(38), y + S(22), S(88), S(88)), Color.FromArgb(210, 241, 236), 1.5f);
                IconPainter.Draw(g, IconKind.Wifi, new RectangleF(x + S(113), y + S(84), S(32), S(32)), Palette.Mint, 1.8f);
            }
        }
    }

    internal sealed class PairCard : CardPanel
    {
        private bool _connected;
        public PillButton PairButton { get; private set; }
        public bool Connected
        {
            get { return _connected; }
            set { if (_connected != value) { _connected = value; UpdateButtonState(); Invalidate(); } }
        }
        public bool Paired { get; set; }
        public string PairedName { get; set; }

        public PairCard()
        {
            Radius = 22;
            PairedName = "";
            PairButton = new PillButton();
            PairButton.Text = "配对新手机";
            PairButton.Icon = IconKind.Plus;
            PairButton.Kind = ButtonKind.Mint;
            PairButton.Size = new Size(152, 42);
            PairButton.Click += delegate
            {
                if (PairRequested != null) PairRequested(this, EventArgs.Empty);
            };
            Controls.Add(PairButton);
            UpdateButtonState();
            LayoutChildren();
        }

        public event EventHandler PairRequested;

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            LayoutChildren();
        }

        private void LayoutChildren()
        {
            if (PairButton == null) return;
            int pad = S(22);
            PairButton.Size = new Size(Math.Max(S(140), Width - pad * 2), S(44));
            PairButton.Location = new Point(pad, Height - PairButton.Height - S(18));
        }

        private void UpdateButtonState()
        {
            if (PairButton == null) return;
            PairButton.Text = _connected ? "手机已连接" : "生成安全码";
            PairButton.Icon = _connected ? IconKind.Check : IconKind.Plus;
            PairButton.Enabled = !_connected;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            Gfx.HighQuality(g);
            int pad = S(22);
            Gfx.DrawString(g, Connected ? "连接状态" : "首次连接", Fonts.SmallMedium, Palette.MintDeep,
                new RectangleF(pad, S(18), Width - pad * 2, S(20)), StringAlignment.Near, StringAlignment.Center, false);
            Gfx.DrawString(g, Connected ? "手机已连接" : "配对新手机", Fonts.Section, Palette.Ink,
                new RectangleF(pad, S(40), Width - pad * 2, S(30)), StringAlignment.Near, StringAlignment.Center, false);

            if (Connected)
            {
                Gfx.DrawString(g, "手机已通过安全配对连接。", Fonts.Small, Palette.InkSoft,
                    new RectangleF(pad, S(75), Width - pad * 2, S(20)), StringAlignment.Near, StringAlignment.Center, true);
                Gfx.DrawString(g, "更换设备请先在设置中解除配对。", Fonts.Small, Palette.InkSoft,
                    new RectangleF(pad, S(96), Width - pad * 2, S(20)), StringAlignment.Near, StringAlignment.Center, true);
            }
            else
            {
                Gfx.DrawString(g, "电脑端生成安全码，在手机上选择这台电脑并核对相同安全码。", Fonts.Small, Palette.InkSoft,
                    new RectangleF(pad, S(75), Width - pad * 2, S(38)), StringAlignment.Near, StringAlignment.Near, true);
            }

            if (!Connected)
            {
                float sy = S(112);
                DrawStep(g, 1, "点击下方按钮，生成 6 位安全码", pad, sy);
                DrawStep(g, 2, "打开手机端轻传，选择此电脑", pad, sy + S(28));
                if (Paired)
                {
                    string text = "已配对：" + PairedName;
                    float cw = Math.Min(Width - pad * 2, g.MeasureString(text, Fonts.Tiny).Width + S(22));
                    RectangleF chip = new RectangleF(pad, Height - S(91), cw, S(24));
                    using (GraphicsPath p = Gfx.Rounded(chip, chip.Height / 2))
                    using (SolidBrush b = new SolidBrush(Palette.MintSoft)) g.FillPath(b, p);
                    Gfx.DrawString(g, text, Fonts.Tiny, Palette.Primary, new RectangleF(chip.X + S(10), chip.Y, chip.Width - S(14), chip.Height), StringAlignment.Near, StringAlignment.Center, true);
                }
            }
            else
            {
                RectangleF status = new RectangleF(pad, S(118), Width - pad * 2, S(46));
                using (GraphicsPath p = Gfx.Rounded(status, S(12)))
                using (SolidBrush b = new SolidBrush(Palette.MintSoft)) g.FillPath(b, p);
                using (SolidBrush checkBg = new SolidBrush(Color.White))
                    g.FillEllipse(checkBg, status.X + S(11), status.Y + S(11), S(24), S(24));
                IconPainter.Draw(g, IconKind.Check, new RectangleF(status.X + S(17), status.Y + S(17), S(13), S(13)), Palette.MintDeep, 1.6f);
                string name = string.IsNullOrEmpty(PairedName) ? "已配对手机" : PairedName;
                Gfx.DrawString(g, name, Fonts.SmallMedium, Palette.Primary,
                    new RectangleF(status.X + S(45), status.Y, status.Width - S(55), status.Height), StringAlignment.Near, StringAlignment.Center, true);
            }

        }

        private void DrawStep(Graphics g, int number, string text, float x, float y)
        {
            int d = S(18);
            using (SolidBrush b = new SolidBrush(Palette.MintSoft)) g.FillEllipse(b, x, y, d, d);
            Gfx.DrawString(g, number.ToString(), Fonts.Tiny, Palette.Primary,
                new RectangleF(x, y, d, d), StringAlignment.Center, StringAlignment.Center, false);
            Gfx.DrawString(g, text, Fonts.Tiny, Palette.InkSoft,
                new RectangleF(x + d + S(8), y - S(1), Width - x - d - S(18), d + S(4)), StringAlignment.Near, StringAlignment.Center, true);
        }
    }

    internal sealed class InfoCard : CardPanel
    {
        public PillButton SettingsButton { get; private set; }
        public PillButton FolderButton { get; private set; }
        public PillButton FirewallButton { get; private set; }
        public string DeviceName { get; set; }
        public string Addresses { get; set; }
        public string Port { get; set; }
        public string ReceiveDirectory { get; set; }
        public bool Paired { get; set; }
        public string PairedName { get; set; }

        public InfoCard()
        {
            Radius = 22;
            DeviceName = "";
            Addresses = "";
            Port = "";
            ReceiveDirectory = "";
            PairedName = "";
            SettingsButton = new PillButton();
            SettingsButton.Text = "设置";
            SettingsButton.Icon = IconKind.Settings;
            SettingsButton.Kind = ButtonKind.Secondary;
            SettingsButton.Size = new Size(96, 38);
            SettingsButton.Click += delegate { if (SettingsRequested != null) SettingsRequested(this, EventArgs.Empty); };
            Controls.Add(SettingsButton);

            FolderButton = new PillButton();
            FolderButton.Text = "打开";
            FolderButton.Icon = IconKind.Folder;
            FolderButton.Kind = ButtonKind.Ghost;
            FolderButton.Size = new Size(84, 34);
            FolderButton.Click += delegate { if (FolderRequested != null) FolderRequested(this, EventArgs.Empty); };
            Controls.Add(FolderButton);

            FirewallButton = new PillButton();
            FirewallButton.Text = "修复防火墙";
            FirewallButton.Icon = IconKind.Shield;
            FirewallButton.Kind = ButtonKind.Ghost;
            FirewallButton.Size = new Size(126, 34);
            FirewallButton.Click += delegate { if (FirewallRequested != null) FirewallRequested(this, EventArgs.Empty); };
            Controls.Add(FirewallButton);
            LayoutChildren();
        }

        public event EventHandler SettingsRequested;
        public event EventHandler FolderRequested;
        public event EventHandler FirewallRequested;

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            LayoutChildren();
        }

        private void LayoutChildren()
        {
            if (SettingsButton == null) return;
            int pad = S(22);
            SettingsButton.Size = new Size(S(100), S(38));
            FirewallButton.Size = new Size(S(132), S(38));
            FolderButton.Size = new Size(S(88), S(36));
            FolderButton.Location = new Point(pad, S(298));
            int bottom = Height - S(56);
            SettingsButton.Location = new Point(pad, bottom);
            FirewallButton.Location = new Point(pad + SettingsButton.Width + S(8), bottom + S(2));
            if (FirewallButton.Right > Width - pad)
                FirewallButton.Location = new Point(Width - pad - FirewallButton.Width, bottom + S(2));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            Gfx.HighQuality(g);
            int pad = S(22);
            int w = Width - pad * 2;
            Gfx.DrawString(g, "本机与接收", Fonts.Section, Palette.Ink,
                new RectangleF(pad, S(20), w, S(28)), StringAlignment.Near, StringAlignment.Center, false);
            Gfx.DrawString(g, "文件只通过局域网传输，不经过云端", Fonts.Tiny, Palette.Muted,
                new RectangleF(pad, S(49), w, S(20)), StringAlignment.Near, StringAlignment.Center, false);

            float y = S(84);
            DrawKeyValue(g, "电脑名称", DeviceName, pad, y, w);
            DrawKeyValue(g, "局域网地址", Addresses, pad, y + S(44), w);
            DrawKeyValue(g, "监听端口", Port, pad, y + S(88), w);

            // receive folder block
            float folderY = y + S(138);
            RectangleF folder = new RectangleF(pad, folderY, w, S(64));
            using (GraphicsPath p = Gfx.Rounded(folder, S(14)))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(247, 250, 248))) g.FillPath(b, p);
            Gfx.StrokeRounded(g, folder, S(14), Palette.LineSoft, 1f);
            using (SolidBrush iconBg = new SolidBrush(Palette.MintSoft))
                g.FillEllipse(iconBg, folder.X + S(12), folder.Y + S(14), S(34), S(34));
            IconPainter.Draw(g, IconKind.Folder, new RectangleF(folder.X + S(20), folder.Y + S(22), S(18), S(18)), Palette.Primary, 1.6f);
            Gfx.DrawString(g, "接收文件夹", Fonts.Tiny, Palette.Muted,
                new RectangleF(folder.X + S(56), folder.Y + S(10), folder.Width - S(64), S(18)), StringAlignment.Near, StringAlignment.Center, false);
            Gfx.DrawString(g, ReceiveDirectory, Fonts.SmallMedium, Palette.Ink,
                new RectangleF(folder.X + S(56), folder.Y + S(30), folder.Width - S(64), S(22)), StringAlignment.Near, StringAlignment.Center, true);

            if (Paired)
                Gfx.DrawString(g, "已配对手机：" + PairedName, Fonts.Tiny, Palette.MintDeep,
                    new RectangleF(pad, folder.Bottom + S(50), w, S(20)), StringAlignment.Near, StringAlignment.Center, true);
            else
                Gfx.DrawString(g, "还没有配对手机", Fonts.Tiny, Palette.Muted,
                    new RectangleF(pad, folder.Bottom + S(50), w, S(20)), StringAlignment.Near, StringAlignment.Center, false);

            // bottom actions
            int bottom = Height - S(56);
            using (Pen p = new Pen(Palette.LineSoft, 1))
                g.DrawLine(p, pad, bottom - S(16), Width - pad, bottom - S(16));
        }

        private void DrawKeyValue(Graphics g, string key, string value, int x, float y, int width)
        {
            Gfx.DrawString(g, key, Fonts.Tiny, Palette.Muted,
                new RectangleF(x, y, width, S(16)), StringAlignment.Near, StringAlignment.Center, false);
            Gfx.DrawString(g, string.IsNullOrEmpty(value) ? "—" : value, Fonts.SmallMedium, Palette.Ink,
                new RectangleF(x, y + S(16), width, S(22)), StringAlignment.Near, StringAlignment.Center, true);
        }
    }

    internal sealed class TransferListView : ScrollableControl
    {
        private List<TransferSnapshot> _items = new List<TransferSnapshot>();
        private int _hoverIndex = -1;
        private const int RowHeight = 76;

        public TransferListView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Palette.Card;
            AutoScroll = true;
            TabStop = true;
        }

        public event EventHandler<string> OpenRequested;

        public void SetItems(List<TransferSnapshot> items)
        {
            _items = items ?? new List<TransferSnapshot>();
            AutoScrollMinSize = new Size(0, Math.Max(0, _items.Count * S(RowHeight)));
            Invalidate();
        }

        private int S(int value)
        {
            return (int)Math.Round(value * DeviceDpi / 96f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            Gfx.HighQuality(g);
            g.Clear(Palette.Card);
            if (_items.Count == 0)
            {
                DrawEmpty(g);
                return;
            }
            GraphicsState state = g.Save();
            g.TranslateTransform(0, AutoScrollPosition.Y);
            int top = 0;
            int rowH = S(RowHeight);
            for (int i = 0; i < _items.Count; i++)
            {
                RectangleF row = new RectangleF(0, top, Math.Max(0, ClientSize.Width - 2), rowH);
                if (row.Bottom >= -AutoScrollPosition.Y - rowH && row.Top <= -AutoScrollPosition.Y + ClientSize.Height + rowH)
                {
                    DrawRow(g, _items[i], row, i == _hoverIndex);
                }
                top += rowH;
            }
            g.Restore(state);
        }

        private void DrawEmpty(Graphics g)
        {
            int w = ClientSize.Width;
            float centerY = Math.Max(S(80), ClientSize.Height / 2f - S(20));
            using (SolidBrush circle = new SolidBrush(Palette.MintSoft))
                g.FillEllipse(circle, w / 2f - S(34), centerY - S(58), S(68), S(68));
            IconPainter.Draw(g, IconKind.Send, new RectangleF(w / 2f - S(17), centerY - S(41), S(34), S(34)), Palette.Primary, 1.5f);
            Gfx.DrawString(g, "还没有传输记录", Fonts.BodyMedium, Palette.Ink,
                new RectangleF(S(10), centerY + S(18), w - S(20), S(24)), StringAlignment.Center, StringAlignment.Center, false);
            Gfx.DrawString(g, "选择文件，或直接把文件拖进这个窗口", Fonts.Small, Palette.Muted,
                new RectangleF(S(10), centerY + S(44), w - S(20), S(22)), StringAlignment.Center, StringAlignment.Center, false);
        }

        private void DrawRow(Graphics g, TransferSnapshot item, RectangleF row, bool hover)
        {
            int pad = S(8);
            RectangleF inner = new RectangleF(pad, row.Y + S(4), row.Width - pad * 2, row.Height - S(8));
            if (hover) Gfx.FillRounded(g, inner, S(14), Color.FromArgb(247, 251, 249));
            int iconBox = S(42);
            RectangleF iconArea = new RectangleF(inner.X + S(8), inner.Y + S(14), iconBox, iconBox);
            using (GraphicsPath p = Gfx.Rounded(iconArea, S(12)))
            using (SolidBrush b = new SolidBrush(item.Direction == TransferDirection.Incoming ? Palette.MintSoft : Color.FromArgb(238, 244, 252)))
                g.FillPath(b, p);
            IconPainter.Draw(g, IconKind.File, new RectangleF(iconArea.X + S(10), iconArea.Y + S(10), S(22), S(22)),
                item.Direction == TransferDirection.Incoming ? Palette.MintDeep : Color.FromArgb(79, 113, 179), 1.6f);

            float x = iconArea.Right + S(14);
            float rightW = S(132);
            string title = item.Name;
            Gfx.DrawString(g, title, Fonts.BodyMedium, Palette.Ink,
                new RectangleF(x, inner.Y + S(11), inner.Width - (x - inner.X) - rightW, S(22)), StringAlignment.Near, StringAlignment.Center, true);

            string direction = item.Direction == TransferDirection.Incoming ? "接收自手机" : "发送到手机";
            string meta = direction + "  ·  " + HumanSize.Format(item.Size);
            if (item.State == TransferState.Transferring && item.Speed > 0)
                meta += "  ·  " + HumanSize.Format((long)item.Speed) + "/s";
            Gfx.DrawString(g, meta, Fonts.Tiny, Palette.Muted,
                new RectangleF(x, inner.Y + S(34), inner.Width - (x - inner.X) - rightW, S(18)), StringAlignment.Near, StringAlignment.Center, true);

            // progress track
            float trackY = inner.Bottom - S(17);
            RectangleF track = new RectangleF(x, trackY, inner.Width - (x - inner.X) - S(14), S(4));
            Color trackBg = Color.FromArgb(238, 244, 241);
            Gfx.FillRounded(g, track, S(2), trackBg);
            if (item.State != TransferState.Failed && item.State != TransferState.Waiting)
            {
                float progress = item.State == TransferState.Completed || item.State == TransferState.Verifying ? 1f : (float)item.Progress;
                Color fill = item.State == TransferState.Completed ? Palette.MintDeep : Palette.Primary;
                if (item.State == TransferState.Verifying) fill = Palette.Amber;
                if (progress > 0)
                    Gfx.FillRounded(g, new RectangleF(track.X, track.Y, Math.Max(S(4), track.Width * progress), track.Height), S(2), fill);
            }

            // state at right
            string status;
            Color statusColor;
            IconKind stateIcon = IconKind.Clock;
            switch (item.State)
            {
                case TransferState.Completed:
                    status = item.Direction == TransferDirection.Incoming ? "已保存" : "已送达";
                    statusColor = Palette.MintDeep; stateIcon = IconKind.Check; break;
                case TransferState.Failed:
                    status = "失败";
                    statusColor = Palette.Red; stateIcon = IconKind.Alert; break;
                case TransferState.Verifying:
                    status = "校验中";
                    statusColor = Palette.Amber; stateIcon = IconKind.Shield; break;
                case TransferState.Transferring:
                    status = item.Size > 0 ? ((int)(item.Progress * 100)).ToString() + "%" : "传输中";
                    statusColor = Palette.Primary; stateIcon = IconKind.Bolt; break;
                default:
                    status = "等待中";
                    statusColor = Palette.Muted; stateIcon = IconKind.Clock; break;
            }
            RectangleF statusBox = new RectangleF(inner.Right - rightW - S(4), inner.Y + S(13), rightW, S(22));
            IconPainter.Draw(g, stateIcon, new RectangleF(statusBox.Right - S(18), statusBox.Y + S(3), S(15), S(15)), statusColor, 1.4f);
            Gfx.DrawString(g, status, Fonts.SmallMedium, statusColor,
                new RectangleF(statusBox.X, statusBox.Y, statusBox.Width - S(22), S(22)), StringAlignment.Far, StringAlignment.Center, false);
            if (item.State == TransferState.Failed && !string.IsNullOrEmpty(item.Error))
            {
                Gfx.DrawString(g, item.Error, Fonts.Tiny, Palette.Red,
                    new RectangleF(statusBox.X - S(80), inner.Y + S(36), rightW + S(76), S(18)), StringAlignment.Far, StringAlignment.Center, true);
            }

            using (Pen separator = new Pen(Palette.LineSoft, 1))
                g.DrawLine(separator, pad + S(8), row.Bottom - 1, row.Right - pad - S(8), row.Bottom - 1);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitTest(e.Y);
            if (idx != _hoverIndex)
            {
                _hoverIndex = idx;
                bool canOpen = false;
                if (idx >= 0 && idx < _items.Count)
                    canOpen = _items[idx].State == TransferState.Completed && !string.IsNullOrEmpty(_items[idx].SavedPath);
                Cursor = canOpen ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverIndex = -1;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        private int HitTest(int y)
        {
            int top = AutoScrollPosition.Y;
            int rowH = S(RowHeight);
            int index = (y - top) / rowH;
            if (index < 0 || index >= _items.Count) return -1;
            return index;
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int index = HitTest(e.Y);
            if (index >= 0 && index < _items.Count)
            {
                TransferSnapshot item = _items[index];
                if (item.State == TransferState.Completed && !string.IsNullOrEmpty(item.SavedPath) && OpenRequested != null)
                    OpenRequested(this, item.SavedPath);
            }
        }
    }

    internal sealed class ToastPanel : Control
    {
        private readonly Timer _timer;
        private string _message = "";
        private bool _error;

        public ToastPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Visible = false;
            _timer = new Timer();
            _timer.Interval = 3200;
            _timer.Tick += delegate { HideToast(); };
        }

        public void ShowMessage(string message, bool error)
        {
            _message = message;
            _error = error;
            using (Graphics g = CreateGraphics())
            {
                int width = (int)g.MeasureString(message, Fonts.SmallMedium).Width + LogicalToDeviceUnits(58);
                Width = Math.Min(LogicalToDeviceUnits(620), Math.Max(LogicalToDeviceUnits(220), width));
            }
            Height = LogicalToDeviceUnits(46);
            _timer.Stop();
            Visible = true;
            BringToFront();
            Reposition();
            Invalidate();
            _timer.Start();
        }

        public void Reposition()
        {
            if (Parent == null) return;
            Location = new Point((Parent.ClientSize.Width - Width) / 2, Parent.ClientSize.Height - Height - LogicalToDeviceUnits(22));
        }

        private void HideToast()
        {
            _timer.Stop();
            Visible = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Gfx.HighQuality(e.Graphics);
            RectangleF rect = new RectangleF(0, 0, Width, Height);
            for (int i = 3; i >= 1; i--)
            {
                using (GraphicsPath p = Gfx.Rounded(new RectangleF(i, i + 3, Width - i * 2, Height - i * 2), Height / 2f))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(10, 0, 0, 0))) e.Graphics.FillPath(b, p);
            }
            Gfx.FillRounded(e.Graphics, new RectangleF(1, 1, Width - 2, Height - 2), (Height - 2) / 2f, _error ? Palette.Red : Palette.PrimaryDark);
            IconPainter.Draw(e.Graphics, _error ? IconKind.Alert : IconKind.Check,
                new RectangleF(LogicalToDeviceUnits(16), Height / 2f - LogicalToDeviceUnits(9), LogicalToDeviceUnits(18), LogicalToDeviceUnits(18)),
                Color.White, 1.6f);
            Gfx.DrawString(e.Graphics, _message, Fonts.SmallMedium, Color.White,
                new RectangleF(LogicalToDeviceUnits(43), 0, Width - LogicalToDeviceUnits(58), Height),
                StringAlignment.Near, StringAlignment.Center, true);
        }
    }
}
