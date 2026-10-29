using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using LinkDrop.UI;

namespace LinkDrop
{
    internal sealed class SettingsForm : DialogBase
    {
        private readonly LinkDropServer _server;
        private readonly AppSettings _settings;
        private readonly DeviceIdentity _identity;
        private readonly CardPanel _generalCard;
        private readonly CardPanel _securityCard;
        private readonly FieldBox _deviceField;
        private readonly FieldBox _receiveField;
        private readonly ToggleSwitch _autoStart;
        private readonly PillButton _browse;
        private readonly PillButton _save;
        private readonly PillButton _cancel;
        private readonly PillButton _unpair;
        private readonly PillButton _firewall;
        private readonly Label _footer;
        private string _inlineError = "";

        public bool Saved { get; private set; }

        public SettingsForm(LinkDropServer server, AppSettings settings, DeviceIdentity identity) : base("设置", new Size(660, 680))
        {
            _server = server;
            _settings = settings;
            _identity = identity;

            _generalCard = new CardPanel();
            _generalCard.Radius = 18;
            _generalCard.DrawShadow = false;
            _generalCard.Paint += PaintGeneral;
            Body.Controls.Add(_generalCard);

            _securityCard = new CardPanel();
            _securityCard.Radius = 18;
            _securityCard.DrawShadow = false;
            _securityCard.Paint += PaintSecurity;
            Body.Controls.Add(_securityCard);

            _deviceField = new FieldBox();
            _deviceField.Field.MaxLength = 40;
            _deviceField.Text = settings.DeviceName;
            _generalCard.Controls.Add(_deviceField);

            _receiveField = new FieldBox();
            _receiveField.Text = settings.ReceiveDirectory;
            _generalCard.Controls.Add(_receiveField);

            _browse = new PillButton();
            _browse.Text = "浏览";
            _browse.Icon = IconKind.Folder;
            _browse.Kind = ButtonKind.Secondary;
            _browse.Size = new Size(LogicalToDeviceUnits(88), LogicalToDeviceUnits(40));
            _browse.Click += delegate { BrowseFolder(); };
            _generalCard.Controls.Add(_browse);

            _autoStart = new ToggleSwitch();
            _autoStart.Checked = AutoStart.IsEnabled();
            _generalCard.Controls.Add(_autoStart);

            _save = new PillButton();
            _save.Text = "保存设置";
            _save.Icon = IconKind.Check;
            _save.Kind = ButtonKind.Primary;
            _save.Size = new Size(LogicalToDeviceUnits(132), LogicalToDeviceUnits(42));
            _save.Click += delegate { SaveSettings(); };
            Body.Controls.Add(_save);

            _cancel = new PillButton();
            _cancel.Text = "取消";
            _cancel.Icon = IconKind.None;
            _cancel.Kind = ButtonKind.Secondary;
            _cancel.Size = new Size(LogicalToDeviceUnits(96), LogicalToDeviceUnits(42));
            _cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Body.Controls.Add(_cancel);

            _unpair = new PillButton();
            _unpair.Text = "解除配对";
            _unpair.Icon = IconKind.Trash;
            _unpair.Kind = ButtonKind.Danger;
            _unpair.Size = new Size(LogicalToDeviceUnits(114), LogicalToDeviceUnits(34));
            _unpair.Click += delegate { Unpair(); };
            _securityCard.Controls.Add(_unpair);

            _firewall = new PillButton();
            _firewall.Text = "修复防火墙";
            _firewall.Icon = IconKind.Shield;
            _firewall.Kind = ButtonKind.Secondary;
            _firewall.Size = new Size(LogicalToDeviceUnits(126), LogicalToDeviceUnits(34));
            _firewall.Click += delegate { SystemActions.ConfirmAndAddFirewallRule(this); };
            _securityCard.Controls.Add(_firewall);

            _footer = new Label();
            _footer.AutoSize = false;
            _footer.BackColor = Color.Transparent;
            _footer.Font = Fonts.Tiny;
            _footer.ForeColor = Palette.Muted;
            _footer.Text = "轻传 LinkDrop 1.0.0   ·   文件只在局域网内点对点传输";
            _footer.TextAlign = ContentAlignment.MiddleLeft;
            Body.Controls.Add(_footer);

            Body.Resize += delegate { LayoutCards(); };
            Load += delegate { LayoutCards(); };
        }

        private new int S(int value)
        {
            return LogicalToDeviceUnits(value);
        }

        private void LayoutCards()
        {
            int pad = S(24);
            int width = Body.ClientSize.Width - pad * 2;
            _generalCard.SetBounds(pad, S(8), width, S(272));
            _securityCard.SetBounds(pad, S(292), width, S(214));
            int inner = S(22);
            _browse.Size = new Size(S(88), S(40));
            _save.Size = new Size(S(132), S(42));
            _cancel.Size = new Size(S(96), S(42));
            _unpair.Size = new Size(S(114), S(34));
            _firewall.Size = new Size(S(126), S(34));
            _autoStart.Size = new Size(S(46), S(26));
            _deviceField.SetBounds(inner, S(86), S(370), S(40));
            _receiveField.SetBounds(inner, S(158), width - inner * 2 - _browse.Width - S(10), S(40));
            _browse.Location = new Point(_receiveField.Right + S(10), S(159));
            _autoStart.Location = new Point(inner, S(216));
            _unpair.Location = new Point(width - inner - _unpair.Width, S(14));
            _firewall.Location = new Point(width - pad - _firewall.Width, S(160));
            _unpair.Enabled = _server.Paired != null;
            int footerY = Body.ClientSize.Height - S(58);
            _cancel.Location = new Point(Body.ClientSize.Width - pad - _cancel.Width, footerY);
            _save.Location = new Point(_cancel.Left - _save.Width - S(10), footerY);
            _footer.SetBounds(pad, footerY + S(8), Math.Max(S(120), Body.ClientSize.Width - pad * 2 - _save.Width - _cancel.Width - S(40)), S(24));
            Body.Invalidate();
        }

        private void PaintGeneral(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Gfx.HighQuality(g);
            int pad = S(22);
            int width = _generalCard.Width;
            Gfx.DrawString(g, "常规", Fonts.Section, Palette.Ink,
                new RectangleF(pad, S(14), width - pad * 2, S(26)), StringAlignment.Near, StringAlignment.Center, false);
            Gfx.DrawString(g, "手机会在附近设备中看到这个名称", Fonts.Tiny, Palette.Muted,
                new RectangleF(pad, S(40), width - pad * 2, S(18)), StringAlignment.Near, StringAlignment.Center, false);
            Gfx.DrawString(g, "电脑名称", Fonts.SmallMedium, Palette.InkSoft,
                new RectangleF(pad, S(64), width - pad * 2, S(20)), StringAlignment.Near, StringAlignment.Center, false);
            Gfx.DrawString(g, "接收文件夹", Fonts.SmallMedium, Palette.InkSoft,
                new RectangleF(pad, S(136), width - pad * 2, S(20)), StringAlignment.Near, StringAlignment.Center, false);
            Gfx.DrawString(g, "开机时自动启动", Fonts.SmallMedium, Palette.InkSoft,
                new RectangleF(pad + S(58), S(210), width - pad * 2 - S(58), S(20)), StringAlignment.Near, StringAlignment.Center, false);
            Gfx.DrawString(g, "登录 Windows 后自动在后台等待手机连接", Fonts.Tiny, Palette.Muted,
                new RectangleF(pad + S(58), S(230), width - pad * 2 - S(58), S(18)), StringAlignment.Near, StringAlignment.Center, false);
            Gfx.DrawString(g, "文件将以临时文件写入，完整校验后才会出现在接收目录", Fonts.Tiny, Palette.Muted,
                new RectangleF(pad, S(252), width - pad * 2, S(16)), StringAlignment.Near, StringAlignment.Center, false);
            if (!string.IsNullOrEmpty(_inlineError))
                Gfx.DrawString(g, _inlineError, Fonts.Tiny, Palette.Red,
                    new RectangleF(pad, S(250), width - pad * 2, S(18)), StringAlignment.Far, StringAlignment.Center, true);
        }

        private void PaintSecurity(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Gfx.HighQuality(g);
            int pad = S(22);
            int width = _securityCard.Width;
            Gfx.DrawString(g, "安全与连接", Fonts.Section, Palette.Ink,
                new RectangleF(pad, S(12), width - pad * 2, S(26)), StringAlignment.Near, StringAlignment.Center, false);
            PairedDevice paired = _server.Paired;
            string pairedName = paired == null ? "尚未配对" : paired.DeviceName;
            Gfx.DrawString(g, "已配对手机", Fonts.Tiny, Palette.Muted,
                new RectangleF(pad, S(48), width - pad * 2 - S(130), S(18)), StringAlignment.Near, StringAlignment.Center, false);
            Gfx.DrawString(g, pairedName, Fonts.BodyMedium, paired == null ? Palette.Muted : Palette.Ink,
                new RectangleF(pad, S(65), width - pad * 2 - S(140), S(26)), StringAlignment.Near, StringAlignment.Center, true);

            string addresses = _server.GetStatus().LocalAddresses;
            if (string.IsNullOrEmpty(addresses)) addresses = "未连接到局域网";
            Gfx.DrawString(g, "本机地址  " + addresses + "   端口 " + _server.Port, Fonts.Small, Palette.InkSoft,
                new RectangleF(pad, S(98), width - pad * 2, S(20)), StringAlignment.Near, StringAlignment.Center, true);
            Gfx.DrawString(g, "设备 ID  " + _identity.ShortId + "   ·   连接使用 TLS 1.2 加密并由配对令牌鉴权", Fonts.Tiny, Palette.Muted,
                new RectangleF(pad, S(120), width - pad * 2, S(18)), StringAlignment.Near, StringAlignment.Center, true);

            using (Pen line = new Pen(Palette.LineSoft, 1))
                g.DrawLine(line, pad, S(147), width - pad, S(147));
            Gfx.DrawString(g, "手机连接不上？", Fonts.SmallMedium, Palette.Ink,
                new RectangleF(pad, S(156), width - pad * 2 - _firewall.Width - S(18), S(34)), StringAlignment.Near, StringAlignment.Center, false);
            Gfx.DrawString(g, "请确保同一 Wi-Fi，并放行 Windows 专用网络防火墙。", Fonts.Tiny, Palette.Muted,
                new RectangleF(pad, S(176), width - pad * 2 - _firewall.Width - S(18), S(24)), StringAlignment.Near, StringAlignment.Center, true);
        }

        private void BrowseFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择手机发送文件的保存位置";
                dialog.SelectedPath = Directory.Exists(_receiveField.Text) ? _receiveField.Text : AppPaths.DefaultReceiveDirectory;
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _receiveField.Text = dialog.SelectedPath;
                    _inlineError = "";
                    Body.Invalidate();
                }
            }
        }

        private void SaveSettings()
        {
            string name = _deviceField.Text == null ? "" : _deviceField.Text.Trim();
            if (name.Length == 0)
            {
                _inlineError = "电脑名称不能为空";
                _generalCard.Invalidate();
                return;
            }
            if (name.Length > 40) name = name.Substring(0, 40);
            string folder = _receiveField.Text == null ? "" : _receiveField.Text.Trim();
            if (folder.Length == 0)
            {
                _inlineError = "请选择接收文件夹";
                _generalCard.Invalidate();
                return;
            }
            try { Directory.CreateDirectory(folder); }
            catch (Exception ex)
            {
                _inlineError = "无法创建接收文件夹：" + ex.Message;
                _generalCard.Invalidate();
                return;
            }
            _settings.DeviceName = name;
            _settings.ReceiveDirectory = folder;
            _settings.AutoStart = _autoStart.Checked;
            SettingsStore.Save(_settings);
            AutoStart.Apply(_autoStart.Checked);
            Saved = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Unpair()
        {
            if (_server.Paired == null) return;
            DialogResult result = MessageBox.Show(this,
                "解除后，当前手机需要重新配对才能连接这台电脑。是否继续？",
                "解除配对", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;
            try
            {
                _server.Unpair();
                _inlineError = "";
                _securityCard.Invalidate();
                _unpair.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "解除配对失败：" + ex.Message, "轻传 LinkDrop", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
