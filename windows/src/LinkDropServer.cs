using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LinkDrop
{
    internal sealed class PendingPairRequest
    {
        public string DeviceId;
        public string DeviceName;
        public string Platform;
        public DateTime RequestedAt;
        public TaskCompletionSource<bool> Decision = new TaskCompletionSource<bool>();
    }

    internal sealed class PairingSession
    {
        public string Code;
        public DateTime ExpiresAt;
        public PendingPairRequest Pending;
        public bool Success;
        public string SuccessName;
        public string Error;
    }

    internal sealed class PairingSnapshot
    {
        public bool Active;
        public bool Success;
        public bool HasPending;
        public string Code;
        public DateTime ExpiresAt;
        public string PendingDeviceName;
        public string PendingDeviceId;
        public string PendingPlatform;
        public string SuccessName;
        public string Error;
    }

    internal sealed class ServerStatus
    {
        public bool Running;
        public bool Connected;
        public bool Paired;
        public bool PairingActive;
        public string DeviceName;
        public string DeviceId;
        public string SecurityCode;
        public string Port;
        public string LocalAddresses;
        public string PairedDeviceName;
        public string PairedPlatform;
        public string PeerName;
        public string PeerPlatform;
        public string PeerAddress;
        public string LastError;
    }

    internal sealed class LinkDropServer : IDisposable
    {
        private readonly object _gate = new object();
        private readonly object _pairingGate = new object();
        private readonly DeviceIdentity _identity;
        private readonly AppSettings _settings;
        private readonly TransferManager _transfers = new TransferManager();
        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;
        private PairingSession _pairing;
        private PeerSession _active;
        private PairedDevice _paired;
        private DiscoveryService _discovery;
        private string _lastError = "";
        private int _port;
        private string _cachedAddress = "";
        private DateTime _addressCachedAt = DateTime.MinValue;

        public LinkDropServer(DeviceIdentity identity, AppSettings settings)
        {
            _identity = identity;
            _settings = settings;
            _paired = TrustStore.Load();
        }

        public DeviceIdentity Identity { get { return _identity; } }
        public AppSettings Settings { get { return _settings; } }
        public TransferManager Transfers { get { return _transfers; } }
        public int Port { get { return _port; } }
        public PairedDevice Paired { get { lock (_gate) return _paired; } }

        public bool Start()
        {
            for (int port = ProtocolConst.DefaultTcpPort; port <= ProtocolConst.MaxTcpPort; port++)
            {
                try
                {
                    TcpListener listener = new TcpListener(IPAddress.Any, port);
                    listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    listener.Start(20);
                    _listener = listener;
                    _port = port;
                    _running = true;
                    _acceptThread = new Thread(AcceptLoop);
                    _acceptThread.IsBackground = true;
                    _acceptThread.Name = "LinkDrop accept";
                    _acceptThread.Start();
                    Log.Info("服务已启动，TCP " + port + "，安全码 " + _identity.SecurityCode);
                    _discovery = new DiscoveryService(_identity.ShortId,
                        delegate { return _settings.DeviceName; },
                        delegate { return _port; },
                        delegate { return IsPairingOpen; });
                    _discovery.Start();
                    return true;
                }
                catch (SocketException ex)
                {
                    if (port == ProtocolConst.MaxTcpPort)
                    {
                        _lastError = "无法监听局域网端口：" + ex.Message;
                        Log.Error("启动 TCP 服务失败", ex);
                    }
                }
                catch (Exception ex)
                {
                    _lastError = "启动服务失败：" + ex.Message;
                    Log.Error("启动服务失败", ex);
                    break;
                }
            }
            return false;
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    PeerSession session = new PeerSession(this, client);
                    Task.Factory.StartNew(delegate { session.Run(); });
                }
                catch (SocketException)
                {
                    if (_running) Thread.Sleep(200);
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (_running)
                    {
                        Log.Error("接受客户端失败", ex);
                        Thread.Sleep(300);
                    }
                }
            }
        }

        internal bool TryAuthenticate(string deviceId, string deviceName, string platform, string token, out string error)
        {
            error = "";
            PairedDevice paired;
            lock (_gate) paired = _paired;
            if (paired == null)
            {
                error = "这台电脑还没有与轻传手机配对";
                return false;
            }
            if (!string.Equals(paired.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                error = "配对信息与这台手机不一致，请重新配对";
                return false;
            }
            if (string.IsNullOrEmpty(token) || token.Length < 32)
            {
                error = "配对令牌无效";
                return false;
            }
            string tokenHash = CryptoUtil.Sha256Hex(token);
            if (!CryptoUtil.FixedEquals(paired.TokenHash, tokenHash))
            {
                error = "配对令牌已失效，请重新配对";
                return false;
            }
            paired.LastSeen = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            paired.DeviceName = string.IsNullOrEmpty(deviceName) ? paired.DeviceName : deviceName;
            paired.Platform = string.IsNullOrEmpty(platform) ? paired.Platform : platform;
            try { TrustStore.Save(paired); }
            catch (Exception ex) { Log.Error("更新配对记录失败", ex); }
            lock (_gate) _paired = paired;
            return true;
        }

        internal void AttachSession(PeerSession session)
        {
            PeerSession previous;
            lock (_gate)
            {
                previous = _active;
                _active = session;
            }
            if (previous != null && previous != session)
            {
                Log.Info("新连接替换旧连接");
                try { previous.Close("已有新的连接"); } catch { }
            }
        }

        internal void SessionClosed(PeerSession session)
        {
            lock (_gate)
            {
                if (_active == session) _active = null;
            }
        }

        public Task SendFilesAsync(string[] paths)
        {
            PeerSession session;
            lock (_gate) session = _active;
            if (session == null) throw new InvalidOperationException("手机未连接，暂时不能发送文件");
            return session.SendFilesAsync(paths);
        }

        public void Unpair()
        {
            PeerSession session;
            lock (_gate)
            {
                session = _active;
                _active = null;
                _paired = null;
            }
            TrustStore.Clear();
            if (session != null)
            {
                try { session.Close("电脑端已解除配对"); } catch { }
            }
            CancelPairing();
            Log.Info("已解除配对");
        }

        public void BeginPairing()
        {
            lock (_gate)
            {
                if (_active != null) throw new InvalidOperationException("请先断开当前手机，再配对新的手机");
            }
            lock (_pairingGate)
            {
                if (_pairing != null && !_pairing.Success && DateTime.Now < _pairing.ExpiresAt) return;
                PairingSession session = new PairingSession();
                session.Code = _identity.SecurityCode;
                session.ExpiresAt = DateTime.Now.AddSeconds(120);
                _pairing = session;
                Log.Info("已开启配对模式，安全码 " + session.Code);
            }
        }

        public void CancelPairing()
        {
            PendingPairRequest pending = null;
            lock (_pairingGate)
            {
                if (_pairing != null)
                {
                    pending = _pairing.Pending;
                    _pairing = null;
                }
            }
            if (pending != null)
            {
                try { pending.Decision.TrySetResult(false); } catch { }
            }
        }

        public void ApprovePairing(bool allow)
        {
            PendingPairRequest pending = null;
            lock (_pairingGate)
            {
                if (_pairing == null) return;
                pending = _pairing.Pending;
            }
            if (pending != null)
            {
                try { pending.Decision.TrySetResult(allow); } catch { }
            }
        }

        internal bool IsPairingOpen
        {
            get
            {
                lock (_pairingGate)
                {
                    return _pairing != null && !_pairing.Success && DateTime.Now < _pairing.ExpiresAt;
                }
            }
        }

        internal PairingSnapshot GetPairingSnapshot()
        {
            lock (_pairingGate)
            {
                PairingSnapshot snapshot = new PairingSnapshot();
                if (_pairing == null) return snapshot;
                snapshot.Active = DateTime.Now < _pairing.ExpiresAt;
                snapshot.Success = _pairing.Success;
                snapshot.Code = _pairing.Code;
                snapshot.ExpiresAt = _pairing.ExpiresAt;
                snapshot.SuccessName = _pairing.SuccessName;
                snapshot.Error = _pairing.Error;
                if (_pairing.Pending != null)
                {
                    snapshot.HasPending = true;
                    snapshot.PendingDeviceName = _pairing.Pending.DeviceName;
                    snapshot.PendingDeviceId = _pairing.Pending.DeviceId;
                    snapshot.PendingPlatform = _pairing.Pending.Platform;
                }
                return snapshot;
            }
        }

        internal PendingPairRequest BeginPairRequest(string deviceId, string deviceName, string platform)
        {
            lock (_pairingGate)
            {
                if (_pairing == null) throw new InvalidOperationException("电脑未开启配对模式");
                if (_pairing.Success) throw new InvalidOperationException("本次配对已完成");
                if (DateTime.Now >= _pairing.ExpiresAt) throw new InvalidOperationException("配对模式已超时，请重新生成安全码");
                if (_pairing.Pending != null) throw new InvalidOperationException("电脑正在处理另一个配对请求");
                PendingPairRequest pending = new PendingPairRequest();
                pending.DeviceId = deviceId;
                pending.DeviceName = string.IsNullOrEmpty(deviceName) ? "Android 手机" : deviceName;
                pending.Platform = string.IsNullOrEmpty(platform) ? "android" : platform;
                pending.RequestedAt = DateTime.Now;
                _pairing.Pending = pending;
                Log.Info("收到配对请求：" + pending.DeviceName);
                return pending;
            }
        }

        internal void CompletePairing(PendingPairRequest pending, string token)
        {
            PairedDevice device = new PairedDevice();
            device.DeviceId = pending.DeviceId;
            device.DeviceName = pending.DeviceName;
            device.Platform = pending.Platform;
            device.TokenHash = CryptoUtil.Sha256Hex(token);
            device.PairedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            device.LastSeen = device.PairedAt;
            lock (_gate) _paired = device;
            TrustStore.Save(device);
            lock (_pairingGate)
            {
                if (_pairing != null)
                {
                    _pairing.Pending = null;
                    _pairing.Success = true;
                    _pairing.SuccessName = device.DeviceName;
                    _pairing.ExpiresAt = DateTime.Now.AddSeconds(8);
                }
            }
            Log.Info("配对成功：" + device.DeviceName);
        }

        internal void FailPairing(PendingPairRequest pending, string message)
        {
            lock (_pairingGate)
            {
                if (_pairing != null)
                {
                    if (_pairing.Pending == pending) _pairing.Pending = null;
                    _pairing.Error = message;
                    _pairing.ExpiresAt = DateTime.Now.AddSeconds(8);
                }
            }
            Log.Info("配对未完成：" + message);
        }

        public ServerStatus GetStatus()
        {
            PeerSession active;
            PairedDevice paired;
            PairingSnapshot pairing;
            lock (_gate)
            {
                active = _active;
                paired = _paired;
            }
            pairing = GetPairingSnapshot();
            ServerStatus status = new ServerStatus();
            status.Running = _running;
            status.DeviceName = _settings.DeviceName;
            status.DeviceId = _identity.ShortId;
            status.SecurityCode = _identity.SecurityCode;
            status.Port = _port.ToString();
            if (DateTime.UtcNow - _addressCachedAt > TimeSpan.FromSeconds(10))
            {
                _cachedAddress = NetworkUtil.PrimaryIPv4Address();
                _addressCachedAt = DateTime.UtcNow;
            }
            status.LocalAddresses = string.IsNullOrEmpty(_cachedAddress) ? "未连接到局域网" : _cachedAddress;
            status.Paired = paired != null;
            status.PairedDeviceName = paired == null ? "" : paired.DeviceName;
            status.PairedPlatform = paired == null ? "" : paired.Platform;
            status.Connected = active != null;
            if (active != null)
            {
                status.PeerName = active.PeerName;
                status.PeerPlatform = active.PeerPlatform;
                status.PeerAddress = active.PeerAddress;
            }
            status.PairingActive = pairing.Active;
            status.LastError = _lastError;
            return status;
        }

        public void Dispose()
        {
            _running = false;
            CancelPairing();
            try { if (_listener != null) _listener.Stop(); } catch { }
            try { if (_acceptThread != null && _acceptThread.IsAlive) _acceptThread.Join(1500); } catch { }
            PeerSession session;
            lock (_gate) { session = _active; _active = null; }
            if (session != null)
            {
                try { session.Close("电脑端已退出"); } catch { }
            }
            if (_discovery != null) _discovery.Dispose();
            _discovery = null;
        }
    }
}
