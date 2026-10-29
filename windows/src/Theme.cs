using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace LinkDrop.UI
{
    internal static class Palette
    {
        public static readonly Color Bg = Color.FromArgb(243, 247, 245);
        public static readonly Color Card = Color.FromArgb(255, 255, 255);
        public static readonly Color Ink = Color.FromArgb(20, 45, 43);
        public static readonly Color InkSoft = Color.FromArgb(78, 104, 100);
        public static readonly Color Muted = Color.FromArgb(124, 146, 142);
        public static readonly Color Line = Color.FromArgb(224, 234, 230);
        public static readonly Color LineSoft = Color.FromArgb(236, 243, 240);
        public static readonly Color Primary = Color.FromArgb(15, 78, 72);
        public static readonly Color PrimaryDark = Color.FromArgb(9, 48, 45);
        public static readonly Color PrimaryHover = Color.FromArgb(23, 97, 89);
        public static readonly Color Mint = Color.FromArgb(102, 240, 211);
        public static readonly Color MintSoft = Color.FromArgb(226, 250, 243);
        public static readonly Color MintDeep = Color.FromArgb(28, 176, 146);
        public static readonly Color Amber = Color.FromArgb(224, 158, 58);
        public static readonly Color AmberSoft = Color.FromArgb(255, 247, 229);
        public static readonly Color Red = Color.FromArgb(205, 78, 88);
        public static readonly Color RedSoft = Color.FromArgb(253, 238, 239);
        public static readonly Color White = Color.White;
        public static readonly Color HeroTop = Color.FromArgb(21, 91, 83);
        public static readonly Color HeroBottom = Color.FromArgb(8, 46, 43);
        public static readonly Color Overlay = Color.FromArgb(8, 32, 30);
        public static readonly Color Shadow = Color.FromArgb(12, 42, 39, 22);
        public static readonly Color HoverBg = Color.FromArgb(246, 250, 248);
    }

    internal static class Fonts
    {
        public static readonly Font Body;
        public static readonly Font BodyMedium;
        public static readonly Font Small;
        public static readonly Font SmallMedium;
        public static readonly Font Tiny;
        public static readonly Font Brand;
        public static readonly Font Title;
        public static readonly Font Hero;
        public static readonly Font Section;
        public static readonly Font Button;
        public static readonly Font Code;
        public static readonly Font Numeric;
        public static readonly Font CodeLarge;

        static Fonts()
        {
            string family = Pick("Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI");
            string numeric = Pick("Segoe UI", "Microsoft YaHei UI");
            string code = Pick("Cascadia Mono", "Consolas", "Segoe UI");
            Body = new Font(family, 9.5f, FontStyle.Regular, GraphicsUnit.Point);
            BodyMedium = new Font(family, 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            Small = new Font(family, 8.4f, FontStyle.Regular, GraphicsUnit.Point);
            SmallMedium = new Font(family, 8.4f, FontStyle.Bold, GraphicsUnit.Point);
            Tiny = new Font(family, 7.4f, FontStyle.Regular, GraphicsUnit.Point);
            Brand = new Font(family, 7.3f, FontStyle.Regular, GraphicsUnit.Point);
            Title = new Font(family, 21f, FontStyle.Bold, GraphicsUnit.Point);
            Hero = new Font(family, 20f, FontStyle.Bold, GraphicsUnit.Point);
            Section = new Font(family, 13.5f, FontStyle.Bold, GraphicsUnit.Point);
            Button = new Font(family, 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            Code = new Font(code, 9f, FontStyle.Regular, GraphicsUnit.Point);
            Numeric = new Font(numeric, 9f, FontStyle.Regular, GraphicsUnit.Point);
            CodeLarge = new Font(code, 28f, FontStyle.Bold, GraphicsUnit.Point);
        }

        private static string Pick(params string[] names)
        {
            foreach (string name in names)
            {
                try
                {
                    using (FontFamily family = new FontFamily(name))
                    {
                        if (family.Name == name) return name;
                    }
                }
                catch { }
            }
            return FontFamily.GenericSansSerif.Name;
        }
    }

    internal static class Gfx
    {
        public static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            radius = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f));
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0.5f)
            {
                path.AddRectangle(rect);
                path.CloseFigure();
                return path;
            }
            float d = radius * 2f;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void HighQuality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        public static Color Blend(Color a, Color b, float amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * amount),
                (int)(a.R + (b.R - a.R) * amount),
                (int)(a.G + (b.G - a.G) * amount),
                (int)(a.B + (b.B - a.B) * amount));
        }

        public static void DrawString(Graphics g, string text, Font font, Color color, RectangleF rect, StringAlignment hAlign, StringAlignment vAlign, bool ellipsis)
        {
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = hAlign;
                format.LineAlignment = vAlign;
                format.Trimming = ellipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None;
                format.FormatFlags = StringFormatFlags.NoWrap;
                using (SolidBrush brush = new SolidBrush(color)) g.DrawString(text ?? "", font, brush, rect, format);
            }
        }

        public static void FillRounded(Graphics g, RectangleF rect, float radius, Color color)
        {
            using (GraphicsPath path = Rounded(rect, radius))
            using (SolidBrush brush = new SolidBrush(color)) g.FillPath(brush, path);
        }

        public static void StrokeRounded(Graphics g, RectangleF rect, float radius, Color color, float width)
        {
            RectangleF r = new RectangleF(rect.X + width / 2f, rect.Y + width / 2f, rect.Width - width, rect.Height - width);
            using (GraphicsPath path = Rounded(r, Math.Max(0, radius - width / 2f)))
            using (Pen pen = new Pen(color, width)) g.DrawPath(pen, path);
        }

        public static string FitText(Graphics g, string text, Font font, float width)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (g.MeasureString(text, font).Width <= width) return text;
            string suffix = "…";
            int low = 0, high = text.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                string candidate = text.Substring(0, mid) + suffix;
                if (g.MeasureString(candidate, font).Width <= width) low = mid;
                else high = mid - 1;
            }
            return text.Substring(0, low) + suffix;
        }
    }

    internal enum ButtonKind
    {
        Primary,
        Secondary,
        Mint,
        Ghost,
        Danger
    }

    internal sealed class PillButton : Control
    {
        private bool _hover;
        private bool _pressed;
        private IconKind _icon;
        private ButtonKind _kind;

        public PillButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = Fonts.Button;
            Height = 42;
        }

        public IconKind Icon { get { return _icon; } set { _icon = value; Invalidate(); } }
        public ButtonKind Kind { get { return _kind; } set { _kind = value; Invalidate(); } }
        public int CornerRadius { get; set; }
        public int IconSize { get; set; }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Gfx.HighQuality(e.Graphics);
            float radius = CornerRadius > 0 ? CornerRadius : Height / 2f;
            Color fill;
            Color border;
            Color text;
            if (!Enabled)
            {
                fill = Color.FromArgb(234, 240, 237);
                border = Color.Transparent;
                text = Palette.Muted;
            }
            else if (_kind == ButtonKind.Primary)
            {
                fill = _pressed ? Palette.PrimaryDark : _hover ? Palette.PrimaryHover : Palette.Primary;
                border = Color.Transparent;
                text = Color.White;
            }
            else if (_kind == ButtonKind.Mint)
            {
                fill = _pressed ? Color.FromArgb(86, 220, 192) : _hover ? Color.FromArgb(127, 248, 220) : Palette.Mint;
                border = Color.Transparent;
                text = Palette.PrimaryDark;
            }
            else if (_kind == ButtonKind.Secondary)
            {
                fill = _pressed ? Color.FromArgb(240, 247, 244) : _hover ? Palette.HoverBg : Color.White;
                border = Palette.Line;
                text = Palette.Ink;
            }
            else if (_kind == ButtonKind.Danger)
            {
                fill = _pressed ? Color.FromArgb(248, 224, 226) : _hover ? Palette.RedSoft : Color.White;
                border = Color.FromArgb(238, 201, 205);
                text = Palette.Red;
            }
            else
            {
                fill = _hover ? Color.FromArgb(238, 247, 243) : Color.Transparent;
                border = Color.Transparent;
                text = Palette.Primary;
            }
            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            if (fill.A > 0) Gfx.FillRounded(e.Graphics, rect, radius, fill);
            if (border.A > 0) Gfx.StrokeRounded(e.Graphics, rect, radius, border, 1f);

            int iconSize = IconSize > 0 ? IconSize : (int)Math.Round(18 * DeviceDpi / 96f);
            float contentWidth = 0;
            string label = Text ?? "";
            SizeF textSize = e.Graphics.MeasureString(label, Font);
            if (_icon != IconKind.None && label.Length > 0) contentWidth = iconSize + 8 + textSize.Width;
            else if (_icon != IconKind.None) contentWidth = iconSize;
            else contentWidth = textSize.Width;
            float x = (Width - contentWidth) / 2f;
            if (_icon != IconKind.None)
            {
                RectangleF ib = new RectangleF(x, (Height - iconSize) / 2f, iconSize, iconSize);
                IconPainter.Draw(e.Graphics, _icon, ib, text, 1.8f);
                x += iconSize + 8;
            }
            if (label.Length > 0)
            {
                RectangleF tb = new RectangleF(x, 0, Width - x - 8, Height);
                Gfx.DrawString(e.Graphics, label, Font, text, tb, StringAlignment.Near, StringAlignment.Center, false);
            }
        }
    }

    internal sealed class ChromeButton : Control
    {
        private bool _hover;
        public bool IsClose { get; set; }
        public IconKind Icon { get; set; }

        public ChromeButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Gfx.HighQuality(e.Graphics);
            if (_hover)
            {
                Color fill = IsClose ? Color.FromArgb(224, 75, 83) : Color.FromArgb(236, 242, 239);
                Gfx.FillRounded(e.Graphics, new RectangleF(2, 2, Width - 4, Height - 4), 8, fill);
            }
            Color color = _hover && IsClose ? Color.White : Palette.InkSoft;
            int size = (int)Math.Round(14 * DeviceDpi / 96f);
            IconPainter.Draw(e.Graphics, Icon, new RectangleF((Width - size) / 2f, (Height - size) / 2f, size, size), color, 1.5f);
        }
    }

    internal sealed class IconView : Control
    {
        public IconKind Icon { get; set; }
        public Color IconColor { get; set; }
        public float Thickness { get; set; }
        public IconView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            IconColor = Palette.Primary;
            Thickness = 1.8f;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Gfx.HighQuality(e.Graphics);
            int pad = (int)Math.Round(4 * DeviceDpi / 96f);
            IconPainter.Draw(e.Graphics, Icon, new RectangleF(pad, pad, Width - pad * 2, Height - pad * 2), IconColor, Thickness);
        }
    }

    internal sealed class ToggleSwitch : Control
    {
        private bool _checked;
        public event EventHandler CheckedChanged;
        public bool Checked
        {
            get { return _checked; }
            set { if (_checked != value) { _checked = value; Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); } }
        }
        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(46, 26);
        }
        protected override void OnClick(EventArgs e)
        {
            if (Enabled) Checked = !Checked;
            base.OnClick(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Gfx.HighQuality(e.Graphics);
            float h = Height - 2;
            Color track = _checked ? Palette.MintDeep : Color.FromArgb(213, 223, 219);
            Gfx.FillRounded(e.Graphics, new RectangleF(1, 1, Width - 2, h), h / 2, track);
            float knob = h - 6;
            float x = _checked ? Width - knob - 4 : 4;
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                e.Graphics.FillEllipse(shadow, x, 4, knob, knob);
            using (SolidBrush brush = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(brush, x, 3, knob, knob);
        }
    }
}
