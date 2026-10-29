using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LinkDrop.UI;

namespace LinkDrop
{
    internal sealed class PairingForm : DialogBase
    {
        private readonly LinkDropServer _server;
        private readonly DeviceIdentity _identity;
        private readonly Timer _timer;
        private readonly PillButton _allow;
        private readonly PillButton _deny;
        private readonly PillButton _retry;
        private readonly PillButton _cancel;
        private PairingSnapshot _snapshot;
        private bool _started;
        private bool _processing;
        private bool _success;
        private bool _expired;
        private string _error = "";
        private DateTime _closeAt = DateTime.MinValue;
        private bool _pollLogged;
        private string _lastVisualToken = "";
        private string _lastControlToken = "";

        public PairingForm(LinkDropServer server, DeviceIdentity identity) : base("配对设备", new Size(580, 620))
        {
            _server = server;
            _identity = identity;
            Body.Paint += PaintBody;
            Body.Resize += delegate { LayoutButtons(); };
            _allow = MakeButton("允许配对", IconKind.Check, ButtonKind.Mint, 138);
            _allow.Click += delegate { Approve(true); };
            _deny = MakeButton("拒绝", IconKind.Close, ButtonKind.Secondary, 92);
            _deny.Click += delegate { Approve(false); };
            _retry = MakeButton("重新生成", IconKind.Refresh, ButtonKind.Mint, 128);
            _retry.Click += delegate { Restart(); };
            _cancel = MakeButton("取消", IconKind.None, ButtonKind.Secondary, 82);
            _cancel.Click += delegate { Close(); };
            Body.Controls.Add(_allow);
            Body.Controls.Add(_deny);
            Body.Controls.Add(_retry);
            Body.Controls.Add(_cancel);
            _timer = new Timer();
            _timer.Interval = 200;
            _timer.Tick += delegate { Poll(); };
            Load += delegate
            {
                StartSession();
                _timer.Start();
                Log.Info("配对界面已就绪，等待手机请求");
            };
            FormClosing += delegate { if (!_success) { try { _server.CancelPairing(); } catch { } } };
            FormClosed += delegate { _timer.Stop(); _timer.Dispose(); };
        }

        private PillButton MakeButton(string text, IconKind icon, ButtonKind kind, int width)
        {
            PillButton button = new PillButton();
            button.Text = text;
            button.Icon = icon;
            button.Kind = kind;
            button.Size = new Size(width, 42);
            button.Visible = false;
            return button;
        }

        private void StartSession()
        {
            try
            {
                _server.BeginPairing();
                _started = true;
                _error = "";
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
            LayoutButtons();
            InvalidateBody();
        }

        private void Restart()
        {
            try
            {
                _server.CancelPairing();
                _server.BeginPairing();
                _started = true;
                _processing = false;
                _success = false;
                _expired = false;
                _error = "";
                _snapshot = null;
                _timer.Start();
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
            LayoutButtons();
            InvalidateBody();
        }

        private void Approve(bool allow)
        {
            if (_processing) return;
            _processing = true;
            _server.ApprovePairing(allow);
            LayoutButtons();
            InvalidateBody();
        }

        private void Poll()
        {
            try
            {
                _snapshot = _server.GetPairingSnapshot();
                if (!_pollLogged)
                {
                    _pollLogged = true;
                    Log.Info("配对界面轮询：active=" + _snapshot.Active + " code=" + _snapshot.Code);
                }
            }
            catch (Exception ex)
            {
                Log.Error("配对界面轮询失败", ex);
                return;
            }

            string controlToken = string.Join("|", new string[] {
                _snapshot.Active.ToString(), _snapshot.Success.ToString(), _snapshot.HasPending.ToString(),
                _snapshot.PendingDeviceName ?? "", _snapshot.Error ?? "",
                _processing.ToString(), _expired.ToString(), _started.ToString(), _error ?? ""
            });
            if (controlToken != _lastControlToken)
            {
                _lastControlToken = controlToken;
                LayoutButtons();
            }

            if (_snapshot.Success && !_success)
            {
                _success = true;
                _processing = false;
                _closeAt = DateTime.Now.AddMilliseconds(1500);
                LayoutButtons();
                InvalidateBody();
            }
            if (!_success && !_expired && _snapshot.Active && _snapshot.ExpiresAt <= DateTime.Now)
            {
                _expired = true;
                _processing = false;
                _error = "安全码已过期，请重新生成";
                try { _server.CancelPairing(); } catch { }
                LayoutButtons();
                InvalidateBody();
            }
            if (!_success && !string.IsNullOrEmpty(_snapshot.Error) && !_expired)
            {
                _error = _snapshot.Error;
                _processing = false;
                LayoutButtons();
            }
            if (_success && _closeAt != DateTime.MinValue && DateTime.Now >= _closeAt)
            {
                _timer.Stop();
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            int secondsLeft = -1;
            if (_snapshot != null && _snapshot.Active)
            {
                TimeSpan left = _snapshot.ExpiresAt - DateTime.Now;
                secondsLeft = Math.Max(0, (int)left.TotalSeconds);
            }
            string visualToken = string.Join("|", new string[] {
                _snapshot == null ? "null" : _snapshot.Active.ToString(),
                _snapshot == null ? "" : _snapshot.Success.ToString(),
                _snapshot == null ? "" : _snapshot.HasPending.ToString(),
                _snapshot == null ? "" : _snapshot.PendingDeviceName ?? "",
                _snapshot == null ? "" : _snapshot.Error ?? "",
                _processing.ToString(), _expired.ToString(), _started.ToString(), _success.ToString(),
                _error ?? "", secondsLeft.ToString()
            });
            if (visualToken != _lastVisualToken)
            {
                _lastVisualToken = visualToken;
                InvalidateBody();
            }
        }

        private void InvalidateBody()
        {
            if (!IsDisposed) Body.Invalidate();
        }

        private void LayoutButtons()
        {
            if (Body == null) return;
            int cardX = S(18);
            int cardW = Body.ClientSize.Width - cardX * 2;
            int bottom = Body.ClientSize.Height - S(68);
            _allow.Size = new Size(S(138), S(42));
            _deny.Size = new Size(S(92), S(42));
            _retry.Size = new Size(S(128), S(42));
            _cancel.Size = new Size(S(82), S(42));
            _cancel.Location = new Point(cardX + S(22), bottom);
            _allow.Location = new Point(cardX + cardW - S(22) - _allow.Width, bottom);
            _deny.Location = new Point(_allow.Left - _deny.Width - S(10), bottom);
            _retry.Location = new Point(cardX + cardW - S(22) - _retry.Width, bottom);

            bool pending = !_success && !_expired && _snapshot != null && _snapshot.HasPending;
            _allow.Visible = pending && !_processing;
            _deny.Visible = pending && !_processing;
            _retry.Visible = _expired || (!_success && !_started) || (!_success && !_processing && !string.IsNullOrEmpty(_error));
            _cancel.Visible = !_success;
            _deny.Enabled = !_processing;
            _allow.Enabled = !_processing;
            if (!pending)
            {
                _allow.Visible = false;
                _deny.Visible = false;
            }
            Body.Invalidate();
        }

        private void PaintBody(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Gfx.HighQuality(g);
            int S = LogicalToDeviceUnits(1);
            int width = Body.ClientSize.Width;
            int height = Body.ClientSize.Height;
            g.Clear(Palette.Bg);

            // main card
            int cardX = S * 18;
            int cardY = S * 8;
            int cardW = width - cardX * 2;
            int cardH = height - cardY - S * 16;
            using (GraphicsPath shadow = Gfx.Rounded(new RectangleF(cardX + 2, cardY + 4, cardW, cardH), S * 24))
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(14, 20, 61, 55))) g.FillPath(sb, shadow);
            using (GraphicsPath card = Gfx.Rounded(new RectangleF(cardX, cardY, cardW, cardH), S * 24))
            using (SolidBrush white = new SolidBrush(Palette.Card)) g.FillPath(white, card);
            Gfx.StrokeRounded(g, new RectangleF(cardX + 0.5f, cardY + 0.5f, cardW - 1, cardH - 1), S * 24, Palette.Line, 1f);

            if (_success)
            {
                PaintSuccess(g, cardX, cardY, cardW, cardH, S);
                return;
            }

            int cx = cardX + cardW / 2;
            int iconSize = S * 48;
            using (SolidBrush b = new SolidBrush(Palette.MintSoft))
                g.FillEllipse(b, cx - iconSize / 2, cardY + S * 26, iconSize, iconSize);
            IconPainter.Draw(g, IconKind.Shield, new RectangleF(cx - S * 13, cardY + S * 39, S * 26, S * 26), Palette.Primary, 1.7f);

            Gfx.DrawString(g, "配对新手机", Fonts.Section, Palette.Ink,
                new RectangleF(cardX + S * 24, cardY + S * 86, cardW - S * 48, S * 28), StringAlignment.Center, StringAlignment.Center, false);
            Gfx.DrawString(g, "请确认手机屏幕显示的安全码与下方一致", Fonts.Small, Palette.Muted,
                new RectangleF(cardX + S * 24, cardY + S * 116, cardW - S * 48, S * 20), StringAlignment.Center, StringAlignment.Center, false);

            string code = _snapshot != null && !string.IsNullOrEmpty(_snapshot.Code) ? _snapshot.Code : (string.IsNullOrEmpty(_error) ? _identity.SecurityCode : _identity.SecurityCode);
            PaintCodeCard(g, cx, cardY + S * 150, S * 330, S * 112, code, S);

            int statusY = cardY + S * 282;
            if (_snapshot != null && _snapshot.HasPending && !_expired)
                PaintPending(g, cardX, statusY, cardW, S);
            else if (!string.IsNullOrEmpty(_error))
                PaintError(g, cardX, statusY, cardW, S);
            else
                PaintWaiting(g, cardX, statusY, cardW, S);

            Gfx.DrawString(g, "配对成功后，两端下次启动会自动连接，无需重复确认。", Fonts.Tiny, Palette.Muted,
                new RectangleF(cardX + S * 24, cardH + cardY - S * 104, cardW - S * 48, S * 18), StringAlignment.Center, StringAlignment.Center, true);
        }

        private void PaintCodeCard(Graphics g, int centerX, int y, int width, int height, string code, int S)
        {
            RectangleF card = new RectangleF(centerX - width / 2f, y, width, height);
            using (GraphicsPath path = Gfx.Rounded(card, S * 18))
            {
                using (LinearGradientBrush gradient = new LinearGradientBrush(card, Palette.HeroTop, Palette.HeroBottom, 15f))
                    g.FillPath(gradient, path);
            }
            Gfx.DrawString(g, "安全码  ·  SECURITY CODE", Fonts.Tiny, Color.FromArgb(171, 220, 211),
                new RectangleF(card.X + S * 20, card.Y + S * 10, card.Width - S * 40, S * 16), StringAlignment.Center, StringAlignment.Center, false);
            string display = SecurityCode.FormatForDisplay(code);
            Gfx.DrawString(g, display, Fonts.CodeLarge,
                Color.White, new RectangleF(card.X + S * 16, card.Y + S * 28, card.Width - S * 32, S * 48),
                StringAlignment.Center, StringAlignment.Center, false);
            string countdown = "--:--";
            if (_expired) countdown = "已过期";
            else if (_snapshot != null && _snapshot.Active)
            {
                TimeSpan left = _snapshot.ExpiresAt - DateTime.Now;
                if (left.TotalSeconds < 0) left = TimeSpan.Zero;
                countdown = ((int)left.TotalMinutes).ToString("00") + ":" + left.Seconds.ToString("00") + " 后失效";
            }
            Gfx.DrawString(g, countdown, Fonts.Tiny, Color.FromArgb(157, 211, 201),
                new RectangleF(card.X + S * 20, card.Bottom - S * 24, card.Width - S * 40, S * 16), StringAlignment.Center, StringAlignment.Center, false);
        }

        private void PaintPending(Graphics g, int cardX, int y, int cardW, int S)
        {
            RectangleF panel = new RectangleF(cardX + S * 24, y, cardW - S * 48, S * 66);
            using (GraphicsPath p = Gfx.Rounded(panel, S * 14))
            using (SolidBrush b = new SolidBrush(Palette.MintSoft)) g.FillPath(b, p);
            using (SolidBrush iconBg = new SolidBrush(Color.White))
                g.FillEllipse(iconBg, panel.X + S * 12, panel.Y + S * 12, S * 42, S * 42);
            IconPainter.Draw(g, IconKind.Phone, new RectangleF(panel.X + S * 24, panel.Y + S * 23, S * 20, S * 20), Palette.Primary, 1.5f);
            string name = _snapshot != null ? _snapshot.PendingDeviceName : "";
            Gfx.DrawString(g, string.IsNullOrEmpty(name) ? "Android 手机" : name, Fonts.BodyMedium, Palette.Ink,
                new RectangleF(panel.X + S * 66, panel.Y + S * 10, panel.Width - S * 84, S * 22), StringAlignment.Near, StringAlignment.Center, true);
            Gfx.DrawString(g, _processing ? "正在完成配对…" : "请求与这台电脑配对，请核对上方安全码", Fonts.Tiny, Palette.InkSoft,
                new RectangleF(panel.X + S * 66, panel.Y + S * 33, panel.Width - S * 84, S * 22), StringAlignment.Near, StringAlignment.Center, true);
        }

        private void PaintWaiting(Graphics g, int cardX, int y, int cardW, int S)
        {
            RectangleF panel = new RectangleF(cardX + S * 24, y, cardW - S * 48, S * 66);
            using (GraphicsPath p = Gfx.Rounded(panel, S * 14))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(247, 250, 248))) g.FillPath(b, p);
            Gfx.StrokeRounded(g, panel, S * 14, Palette.LineSoft, 1f);
            Gfx.DrawString(g, _started ? "等待手机发起配对…" : "无法开启配对模式", Fonts.BodyMedium, Palette.Ink,
                new RectangleF(panel.X + S * 20, panel.Y + S * 10, panel.Width - S * 40, S * 22), StringAlignment.Center, StringAlignment.Center, false);
            Gfx.DrawString(g, "打开手机端轻传，选择这台电脑即可", Fonts.Tiny, Palette.Muted,
                new RectangleF(panel.X + S * 20, panel.Y + S * 34, panel.Width - S * 40, S * 20), StringAlignment.Center, StringAlignment.Center, false);
        }

        private void PaintError(Graphics g, int cardX, int y, int cardW, int S)
        {
            RectangleF panel = new RectangleF(cardX + S * 24, y, cardW - S * 48, S * 66);
            using (GraphicsPath p = Gfx.Rounded(panel, S * 14))
            using (SolidBrush b = new SolidBrush(Palette.RedSoft)) g.FillPath(b, p);
            IconPainter.Draw(g, IconKind.Alert, new RectangleF(panel.X + S * 16, panel.Y + S * 21, S * 22, S * 22), Palette.Red, 1.6f);
            Gfx.DrawString(g, string.IsNullOrEmpty(_error) ? "配对未完成" : _error, Fonts.SmallMedium, Palette.Red,
                new RectangleF(panel.X + S * 50, panel.Y + S * 12, panel.Width - S * 68, S * 42), StringAlignment.Near, StringAlignment.Center, true);
            if (_expired)
                Gfx.DrawString(g, "点击右下角重新生成安全码", Fonts.Tiny, Palette.Red,
                    new RectangleF(panel.X + S * 50, panel.Y + S * 40, panel.Width - S * 68, S * 20), StringAlignment.Near, StringAlignment.Center, false);
        }

        private void PaintSuccess(Graphics g, int cardX, int cardY, int cardW, int cardH, int S)
        {
            int cx = cardX + cardW / 2;
            using (SolidBrush b = new SolidBrush(Palette.MintSoft))
                g.FillEllipse(b, cx - S * 44, cardY + S * 112, S * 88, S * 88);
            using (Pen pen = new Pen(Palette.MintDeep, S * 3))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, new PointF[] {
                    new PointF(cx - S * 18, cardY + S * 156),
                    new PointF(cx - S * 4, cardY + S * 171),
                    new PointF(cx + S * 21, cardY + S * 141)
                });
            }
            Gfx.DrawString(g, "配对成功", Fonts.Section, Palette.Ink,
                new RectangleF(cardX + S * 24, cardY + S * 220, cardW - S * 48, S * 32), StringAlignment.Center, StringAlignment.Center, false);
            string name = _snapshot != null && !string.IsNullOrEmpty(_snapshot.SuccessName) ? _snapshot.SuccessName : "手机";
            Gfx.DrawString(g, name + " 已受信任，会自动连接这台电脑", Fonts.Small, Palette.InkSoft,
                new RectangleF(cardX + S * 24, cardY + S * 254, cardW - S * 48, S * 24), StringAlignment.Center, StringAlignment.Center, false);
            Gfx.DrawString(g, "正在返回主界面…", Fonts.Tiny, Palette.Muted,
                new RectangleF(cardX + S * 24, cardY + S * 286, cardW - S * 48, S * 20), StringAlignment.Center, StringAlignment.Center, false);
        }
    }
}
