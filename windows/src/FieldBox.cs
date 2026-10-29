using System;
using System.Drawing;
using System.Windows.Forms;

namespace LinkDrop.UI
{
    internal sealed class FieldBox : Panel
    {
        private bool _focused;
        public TextBox Field { get; private set; }

        public FieldBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Palette.Card;
            Field = new TextBox();
            Field.BorderStyle = BorderStyle.None;
            Field.BackColor = Color.FromArgb(247, 250, 248);
            Field.ForeColor = Palette.Ink;
            Field.Font = Fonts.Body;
            Field.AutoSize = false;
            Field.Height = 24;
            Field.GotFocus += delegate { _focused = true; Invalidate(); };
            Field.LostFocus += delegate { _focused = false; Invalidate(); };
            Controls.Add(Field);
        }

        public override string Text
        {
            get { return Field.Text; }
            set { Field.Text = value; }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            int left = LogicalToDeviceUnits(12);
            int fieldHeight = Math.Max(LogicalToDeviceUnits(20), Height - LogicalToDeviceUnits(10));
            Field.Height = fieldHeight;
            Field.SetBounds(left, Math.Max(0, (Height - fieldHeight) / 2), Math.Max(10, Width - left * 2), fieldHeight);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Gfx.HighQuality(e.Graphics);
            e.Graphics.Clear(Palette.Card);
            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Gfx.FillRounded(e.Graphics, rect, LogicalToDeviceUnits(11), Color.FromArgb(247, 250, 248));
            Gfx.StrokeRounded(e.Graphics, rect, LogicalToDeviceUnits(11), _focused ? Palette.MintDeep : Palette.Line, _focused ? 1.5f : 1f);
        }
    }
}
