using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LinkDrop.UI;

namespace LinkDrop
{
    internal sealed class MainForm : Form
    {
        private readonly TitleStrip _titleBar;
        private readonly ConnectionHero _hero;
        private readonly PairCard _pairCard;
        private readonly CardPanel _transfersCard;
        private TransferListView _transferList;
        private readonly InfoCard _infoCard;
        private readonly ToastPanel _toast;
        private readonly ToolTip _toolTip;
        private readonly Timer _uiTimer;
        private readonly HashSet<long> _notifiedTransfers = new HashSet<long>();
        private AppSettings _settings;
        private DeviceIdentity _identity;
        private LinkDropServer _server;
        private bool _firstTick = true;
        private bool _lastConnected;
        private bool _startErrorShown;
        private bool _maximized;
        private Rectangle _restoreBounds;
        private bool _layoutInitialized;
        private Label _transfersTitle;
        private Label _transfersSub;
        private PillButton _transfersSend;
        private PillButton _transfersFolder;
        private readonly bool _startMinimized;
        private string _lastStatusSignature = "";
        private string _lastTransfersSignature = "";
        private string _lastTransferSummary = "";

        public MainForm(bool startMinimized)
        {
            _startMinimized = startMinimized;
            Text = "轻传 LinkDrop";
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Palette.Bg;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            MinimumSize = new Size(980, 660);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            _toolTip = new ToolTip();
            _toolTip.AutoPopDelay = 6000;
            _toolTip.InitialDelay = 500;

            _titleBar = new TitleStrip();
            _titleBar.Dock = DockStyle.Top;
            _titleBar.MinimizeRequested += delegate { WindowState = FormWindowState.Minimized; };
            _titleBar.MaximizeRequested += delegate { ToggleMaximize(); };
            _titleBar.CloseRequested += delegate { Close(); };
            Controls.Add(_titleBar);

            _hero = new ConnectionHero();
            _hero.SendRequested += delegate { ChooseAndSendFiles(); };
            Controls.Add(_hero);

            _pairCard = new PairCard();
            _pairCard.PairRequested += delegate { ShowPairingDialog(); };
            Controls.Add(_pairCard);

            _transfersCard = new CardPanel();
            _transfersCard.Radius = 22;
            Controls.Add(_transfersCard);
            BuildTransfersCard();

            _infoCard = new InfoCard();
            _infoCard.SettingsRequested += delegate { ShowSettings(); };
            _infoCard.FolderRequested += delegate { OpenReceiveFolder(); };
            _infoCard.FirewallRequested += delegate { RepairFirewall(); };
            Controls.Add(_infoCard);

            _toast = new ToastPanel();
            Controls.Add(_toast);
            _toast.BringToFront();

            _transfersCard.Resize += delegate { LayoutTransfersCard(); };
            Resize += delegate { LayoutEverything(); };
            DragEnter += OnDragEnter;
            DragOver += OnDragOver;
            DragLeave += delegate { };
            DragDrop += OnDragDrop;
            FormClosing += OnFormClosing;
            Shown += OnFormShown;

            LoadServices();
            _uiTimer = new Timer();
            _uiTimer.Interval = 250;
            _uiTimer.Tick += delegate { RefreshUi(); };
            _uiTimer.Start();
            RefreshUi();
        }

        private void BuildTransfersCard()
        {
            _transfersTitle = new Label();
            _transfersTitle.AutoSize = false;
            _transfersTitle.BackColor = Color.Transparent;
            _transfersTitle.Font = Fonts.Section;
            _transfersTitle.ForeColor = Palette.Ink;
            _transfersTitle.Text = "传输记录";
            _transfersTitle.TextAlign = ContentAlignment.MiddleLeft;
            _transfersCard.Controls.Add(_transfersTitle);

            _transfersSub = new Label();
            _transfersSub.AutoSize = false;
            _transfersSub.BackColor = Color.Transparent;
            _transfersSub.Font = Fonts.Tiny;
            _transfersSub.ForeColor = Palette.Muted;
            _transfersSub.Text = "最近的文件会显示在这里";
            _transfersSub.TextAlign = ContentAlignment.MiddleLeft;
            _transfersCard.Controls.Add(_transfersSub);

            _transfersSend = new PillButton();
            _transfersSend.Text = "选择文件";
            _transfersSend.Icon = IconKind.Send;
            _transfersSend.Kind = ButtonKind.Secondary;
            _transfersSend.Size = new Size(126, 38);
            _transfersSend.Click += delegate { ChooseAndSendFiles(); };
            _transfersCard.Controls.Add(_transfersSend);

            _transfersFolder = new PillButton();
            _transfersFolder.Icon = IconKind.Folder;
            _transfersFolder.Kind = ButtonKind.Ghost;
            _transfersFolder.Size = new Size(38, 38);
            _transfersFolder.Click += delegate { OpenReceiveFolder(); };
            _transfersCard.Controls.Add(_transfersFolder);
            _toolTip.SetToolTip(_transfersFolder, "打开接收文件夹");

            _transferList = new TransferListView();
            _transferList.OpenRequested += delegate(object sender, string path) { OpenPath(path); };
            _transfersCard.Controls.Add(_transferList);
        }

        private void LayoutTransfersCard()
        {
            if (_transfersTitle == null || _transfersCard == null) return;
            int pad = LogicalToDeviceUnits(22);
            int top = LogicalToDeviceUnits(16);
            _transfersTitle.SetBounds(pad, top, LogicalToDeviceUnits(180), LogicalToDeviceUnits(28));
            _transfersSub.SetBounds(pad, top + LogicalToDeviceUnits(28), LogicalToDeviceUnits(320), LogicalToDeviceUnits(18));
            _transfersSend.Size = new Size(LogicalToDeviceUnits(126), LogicalToDeviceUnits(38));
            _transfersFolder.Size = new Size(LogicalToDeviceUnits(38), LogicalToDeviceUnits(38));
            _transfersSend.Location = new Point(_transfersCard.Width - pad - _transfersSend.Width, top + LogicalToDeviceUnits(2));
            _transfersFolder.Location = new Point(_transfersSend.Left - _transfersFolder.Width - LogicalToDeviceUnits(6), top + LogicalToDeviceUnits(2));
            int listTop = top + LogicalToDeviceUnits(54);
            _transferList.SetBounds(pad, listTop, Math.Max(10, _transfersCard.Width - pad * 2), Math.Max(10, _transfersCard.Height - listTop - LogicalToDeviceUnits(16)));
        }

        private void LoadServices()
        {
            try
            {
                _settings = SettingsStore.Load();
                _identity = DeviceIdentity.LoadOrCreate();
                _server = new LinkDropServer(_identity, _settings);
                if (!_server.Start())
                {
                    ShowToast("局域网服务启动失败，请查看设置中的诊断信息", true);
                }
            }
            catch (Exception ex)
            {
                Log.Error("应用初始化失败", ex);
                ShowToast("应用初始化失败：" + ex.Message, true);
                _startErrorShown = true;
            }
        }

        private void OnFormShown(object sender, EventArgs e)
        {
            Rectangle working = Screen.FromControl(this).WorkingArea;
            float scale = DeviceDpi / 96f;
            Log.Info("UI DPI=" + DeviceDpi + " scale=" + scale + " screen=" + working.Width + "x" + working.Height + " form=" + Width + "x" + Height);
            int targetW = (int)Math.Min(1220 * scale, working.Width * 0.92);
            int targetH = (int)Math.Min(820 * scale, working.Height * 0.90);
            targetW = Math.Max(targetW, (int)(900 * scale));
            targetH = Math.Max(targetH, (int)(640 * scale));
            targetW = Math.Min(targetW, working.Width - 16);
            targetH = Math.Min(targetH, working.Height - 16);
            MinimumSize = new Size(Math.Min((int)(960 * scale), working.Width - 16), Math.Min((int)(640 * scale), working.Height - 16));
            _layoutInitialized = true;
            ClientSize = new Size(targetW, targetH);
            Location = new Point(working.Left + (working.Width - Width) / 2, working.Top + (working.Height - Height) / 2);
            LayoutEverything();
            NativeMethods.EnableWindowShadow(this);
            if (_startMinimized) WindowState = FormWindowState.Minimized;
        }

        private void LayoutEverything()
        {
            if (!_layoutInitialized) return;
            int pad = LogicalToDeviceUnits(24);
            int gap = LogicalToDeviceUnits(18);
            _titleBar.Height = LogicalToDeviceUnits(58);
            int titleH = _titleBar.Height;
            int contentTop = titleH + LogicalToDeviceUnits(18);
            int contentBottom = ClientSize.Height - LogicalToDeviceUnits(20);
            int contentHeight = Math.Max(100, contentBottom - contentTop);
            int availableW = ClientSize.Width - pad * 2;
            int leftW = (int)Math.Round((availableW - gap) * 0.66);
            int rightW = availableW - gap - leftW;
            int topH = Math.Min(LogicalToDeviceUnits(252), Math.Max(LogicalToDeviceUnits(220), (int)(contentHeight * 0.36)));
            int bottomH = Math.Max(100, contentHeight - topH - gap);

            _hero.SetBounds(pad, contentTop, leftW, topH);
            _pairCard.SetBounds(pad + leftW + gap, contentTop, rightW, topH);
            _transfersCard.SetBounds(pad, contentTop + topH + gap, leftW, bottomH);
            _infoCard.SetBounds(pad + leftW + gap, contentTop + topH + gap, rightW, bottomH);
            LayoutTransfersCard();
            if (_toast.Visible) _toast.Reposition();
            _toast.BringToFront();
            _titleBar.SetMaximized(_maximized);
        }

        private void RefreshUi()
        {
            if (_server == null)
            {
                _hero.TitleFallback();
                return;
            }
            ServerStatus status;
            try { status = _server.GetStatus(); }
            catch { return; }

            string statusSignature = string.Join("|", new string[] {
                status.Running.ToString(), status.Connected.ToString(), status.Paired.ToString(),
                status.DeviceName ?? "", status.PeerName ?? "", status.PeerPlatform ?? "",
                status.PairedDeviceName ?? "", status.PairedPlatform ?? "", status.LocalAddresses ?? "",
                status.Port ?? "", status.PairingActive.ToString(), status.LastError ?? "",
                _settings != null ? _settings.ReceiveDirectory : ""
            });
            bool statusChanged = statusSignature != _lastStatusSignature;
            if (statusChanged)
            {
                _hero.Connected = status.Connected;
                _hero.PeerName = status.PeerName ?? "";
                _hero.PairedName = status.PairedDeviceName ?? "";
                _hero.Invalidate();
                _pairCard.Connected = status.Connected;
                _pairCard.Paired = status.Paired;
                _pairCard.PairedName = status.PairedDeviceName ?? "";
                _pairCard.Invalidate();
                _infoCard.DeviceName = status.DeviceName;
                _infoCard.Addresses = status.LocalAddresses;
                _infoCard.Port = status.Port;
                _infoCard.ReceiveDirectory = _settings != null ? _settings.ReceiveDirectory : "";
                _infoCard.Paired = status.Paired;
                _infoCard.PairedName = status.PairedDeviceName ?? "";
                _infoCard.Invalidate();
                _titleBar.Connected = status.Connected;
                _titleBar.PeerName = status.PeerName ?? "";
                _hero.SendButton.Enabled = status.Connected;
                _transfersSend.Enabled = status.Connected;
                _pairCard.PairButton.Enabled = !status.Connected && status.Running;
                if (!_firstTick)
                {
                    if (status.Connected && !_lastConnected)
                        ShowToast("手机已连接：" + status.PeerName);
                    else if (!status.Connected && _lastConnected)
                        ShowToast("手机已断开，电脑端会继续等待自动重连");
                }
                if (_firstTick && status.Connected)
                    ShowToast("手机已连接：" + status.PeerName);
                _lastConnected = status.Connected;
                _firstTick = false;
                if (!string.IsNullOrEmpty(status.LastError) && !_startErrorShown)
                {
                    ShowToast(status.LastError, true);
                    _startErrorShown = true;
                }
                _lastStatusSignature = statusSignature;
            }

            List<TransferSnapshot> snapshots;
            try { snapshots = _server.Transfers.Snapshots(); }
            catch { snapshots = new List<TransferSnapshot>(); }

            int active = 0;
            StringBuilder transferSignature = new StringBuilder();
            foreach (TransferSnapshot item in snapshots)
            {
                if (item.State == TransferState.Transferring || item.State == TransferState.Waiting || item.State == TransferState.Verifying) active++;
                transferSignature.Append(item.Id).Append(':').Append((int)item.State).Append(':').Append(item.Bytes).Append(';');
                if (!_notifiedTransfers.Contains(item.Id) &&
                    (item.State == TransferState.Completed || item.State == TransferState.Failed))
                {
                    _notifiedTransfers.Add(item.Id);
                    if (item.State == TransferState.Completed)
                        ShowToast(item.Direction == TransferDirection.Incoming ? "已收到 " + ShortName(item.Name) : "已送达 " + ShortName(item.Name));
                    else
                        ShowToast((item.Direction == TransferDirection.Incoming ? "接收失败：" : "发送失败：") + (string.IsNullOrEmpty(item.Error) ? item.Name : item.Error), true);
                }
            }
            string transferSig = transferSignature.ToString();
            if (transferSig != _lastTransfersSignature)
            {
                _transferList.SetItems(snapshots);
                _lastTransfersSignature = transferSig;
                string summary = snapshots.Count == 0
                    ? "最近的文件会显示在这里"
                    : (active > 0 ? active + " 个传输正在进行" : "共 " + snapshots.Count + " 个传输记录");
                if (summary != _lastTransferSummary)
                {
                    _transfersSub.Text = summary;
                    _lastTransferSummary = summary;
                }
            }
        }

        private static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "文件";
            return name.Length > 26 ? name.Substring(0, 24) + "…" : name;
        }

        private void ShowPairingDialog()
        {
            if (_server == null)
            {
                ShowToast("服务尚未启动", true);
                return;
            }
            if (_server.GetStatus().Connected)
            {
                ShowToast("请先在设置中解除当前配对，再添加新手机", true);
                return;
            }
            using (PairingForm form = new PairingForm(_server, _identity))
            {
                form.ShowDialog(this);
            }
            RefreshUi();
        }

        private void ShowSettings()
        {
            if (_server == null || _settings == null)
            {
                ShowToast("服务尚未启动，无法打开设置", true);
                return;
            }
            using (SettingsForm form = new SettingsForm(_server, _settings, _identity))
            {
                form.ShowDialog(this);
                if (form.Saved)
                {
                    _settings = SettingsStore.Load();
                    _server.Settings.DeviceName = _settings.DeviceName;
                    _server.Settings.ReceiveDirectory = _settings.ReceiveDirectory;
                    RefreshUi();
                    ShowToast("设置已保存");
                }
            }
        }

        private void ChooseAndSendFiles()
        {
            if (_server == null || !_server.GetStatus().Connected)
            {
                ShowToast("手机还未连接，请先完成配对并保持两端应用打开", true);
                return;
            }
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择要发送到手机的文件";
                dialog.Multiselect = true;
                dialog.Filter = "所有文件 (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                SendFiles(dialog.FileNames);
            }
        }

        private async void SendFiles(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;
            if (_server == null || !_server.GetStatus().Connected)
            {
                ShowToast("手机未连接，暂时不能发送", true);
                return;
            }
            try
            {
                ShowToast("正在发送 " + paths.Length + " 个文件到手机");
                await _server.SendFilesAsync(paths);
            }
            catch (Exception ex)
            {
                ShowToast("发送失败：" + ex.Message, true);
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                bool connected = _server != null && _server.GetStatus().Connected;
                e.Effect = connected ? DragDropEffects.Copy : DragDropEffects.None;
                if (!connected) ShowToast("手机还未连接，无法接收拖入的文件", true);
            }
            else e.Effect = DragDropEffects.None;
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = (_server != null && _server.GetStatus().Connected) ? DragDropEffects.Copy : DragDropEffects.None;
            else e.Effect = DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;
            SendFiles(files);
        }

        private void OpenReceiveFolder()
        {
            try
            {
                string dir = _settings != null ? _settings.ReceiveDirectory : AppPaths.DefaultReceiveDirectory;
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + dir + "\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowToast("无法打开接收文件夹：" + ex.Message, true);
            }
        }

        private void OpenPath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowToast("无法打开文件：" + ex.Message, true);
            }
        }

        private void RepairFirewall()
        {
            if (MessageBox.Show(this,
                "Windows 防火墙可能会拦截手机连接。\n\n接下来会请求管理员权限，为轻传添加“专用/域网络”的入站放行规则。是否继续？",
                "修复 Windows 防火墙", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
                return;
            try
            {
                string exe = Application.ExecutablePath;
                string arguments = "advfirewall firewall add rule name=\"LinkDrop\" dir=in action=allow program=\"" + exe + "\" enable=yes profile=private,domain";
                ProcessStartInfo info = new ProcessStartInfo("netsh.exe", arguments);
                info.UseShellExecute = true;
                info.Verb = "runas";
                info.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process process = Process.Start(info))
                {
                    if (process != null)
                    {
                        process.WaitForExit(30000);
                        if (process.HasExited && process.ExitCode == 0)
                            ShowToast("已添加防火墙放行规则");
                        else
                            ShowToast("防火墙规则添加失败，请手动允许轻传通过专用网络", true);
                    }
                }
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223) ShowToast("已取消管理员授权");
                else ShowToast("无法修改防火墙：" + ex.Message, true);
            }
            catch (Exception ex)
            {
                ShowToast("无法修改防火墙：" + ex.Message, true);
            }
        }

        private void ShowToast(string message, bool error = false)
        {
            if (IsDisposed || _toast == null) return;
            if (InvokeRequired) { BeginInvoke(new Action(delegate { ShowToast(message, error); })); return; }
            _toast.ShowMessage(message, error);
        }

        private void ToggleMaximize()
        {
            if (!_maximized)
            {
                _restoreBounds = Bounds;
                Rectangle working = Screen.FromControl(this).WorkingArea;
                Bounds = working;
                _maximized = true;
                _titleBar.SetMaximized(true);
                NativeMethods.SetRoundCorners(Handle, false);
            }
            else
            {
                Bounds = _restoreBounds;
                _maximized = false;
                _titleBar.SetMaximized(false);
                NativeMethods.SetRoundCorners(Handle, true);
            }
            LayoutEverything();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_maximized && WindowState == FormWindowState.Normal && Bounds.Width > 0) _restoreBounds = Bounds;
            List<TransferSnapshot> active = _server == null ? new List<TransferSnapshot>() : _server.Transfers.Snapshots();
            bool busy = active.Any(delegate(TransferSnapshot s) {
                return s.State == TransferState.Waiting || s.State == TransferState.Transferring || s.State == TransferState.Verifying;
            });
            if (busy)
            {
                DialogResult result = MessageBox.Show(this, "还有文件正在传输，退出会中断传输。仍要退出吗？", "轻传 LinkDrop",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) { e.Cancel = true; return; }
            }
            if (_uiTimer != null) _uiTimer.Stop();
            if (_server != null)
            {
                try { _server.Dispose(); } catch { }
                _server = null;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.SetRoundCorners(Handle, true);
            NativeMethods.SetBorderNone(Handle);
        }

    }

    internal static class ConnectionHeroExtensions
    {
        public static void TitleFallback(this ConnectionHero hero)
        {
            hero.Connected = false;
            hero.PeerName = "";
            hero.PairedName = "";
            hero.Invalidate();
        }
    }
}
