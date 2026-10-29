using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LinkDrop
{
    internal sealed class DiscoveryService : IDisposable
    {
        private readonly Func<int> _portProvider;
        private readonly Func<bool> _pairingProvider;
        private readonly Func<string> _nameProvider;
        private readonly string _deviceId;
        private Socket _socket;
        private Thread _thread;
        private volatile bool _running;
        private IPEndPoint _multicastEndpoint;

        public DiscoveryService(string deviceId, Func<string> nameProvider, Func<int> portProvider, Func<bool> pairingProvider)
        {
            _deviceId = deviceId;
            _nameProvider = nameProvider;
            _portProvider = portProvider;
            _pairingProvider = pairingProvider;
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _multicastEndpoint = new IPEndPoint(IPAddress.Parse(ProtocolConst.MulticastGroup), ProtocolConst.DiscoveryPort);
            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Name = "LinkDrop discovery";
            _thread.Start();
        }

        private void Loop()
        {
            Socket socket = null;
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.Bind(new IPEndPoint(IPAddress.Any, ProtocolConst.DiscoveryPort));
                try
                {
                    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                        new MulticastOption(IPAddress.Parse(ProtocolConst.MulticastGroup)));
                    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
                }
                catch (Exception ex)
                {
                    Log.Error("加入局域网组播失败，将仅使用广播", ex);
                }
                socket.EnableBroadcast = true;
                socket.ReceiveTimeout = 500;
                _socket = socket;
                Log.Info("局域网发现服务已启动，UDP " + ProtocolConst.DiscoveryPort);

                DateTime lastSend = DateTime.MinValue;
                byte[] buffer = new byte[4096];
                while (_running)
                {
                    if ((DateTime.UtcNow - lastSend).TotalMilliseconds > 1800)
                    {
                        SendBeacon(socket);
                        lastSend = DateTime.UtcNow;
                    }
                    try
                    {
                        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                        int read = socket.ReceiveFrom(buffer, ref remote);
                        if (read > 0) ProcessIncoming(buffer, read, (IPEndPoint)remote);
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode != SocketError.TimedOut && _running)
                        {
                            Log.Error("局域网发现接收失败", ex);
                            Thread.Sleep(700);
                        }
                    }
                    catch (ObjectDisposedException) { break; }
                    catch (Exception ex)
                    {
                        if (_running) { Log.Error("局域网发现循环异常", ex); Thread.Sleep(500); }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("启动局域网发现失败", ex);
            }
            finally
            {
                try { if (socket != null) socket.Close(); } catch { }
            }
        }

        private void SendBeacon(Socket socket)
        {
            try
            {
                Dictionary<string, object> beacon = new Dictionary<string, object>();
                beacon["m"] = ProtocolConst.Magic;
                beacon["v"] = ProtocolConst.Version;
                beacon["kind"] = "beacon";
                beacon["id"] = _deviceId;
                beacon["name"] = _nameProvider();
                beacon["platform"] = ProtocolConst.Platform;
                beacon["port"] = _portProvider();
                beacon["pairing"] = _pairingProvider();
                byte[] bytes = Encoding.UTF8.GetBytes(Json.Encode(beacon));

                try { socket.SendTo(bytes, _multicastEndpoint); } catch { }
                List<IPAddress> broadcasts = NetworkUtil.BroadcastAddresses();
                foreach (IPAddress address in broadcasts)
                {
                    try { socket.SendTo(bytes, new IPEndPoint(address, ProtocolConst.DiscoveryPort)); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                if (_running) Log.Error("发送发现信标失败", ex);
            }
        }

        private void ProcessIncoming(byte[] buffer, int count, IPEndPoint remote)
        {
            try
            {
                string text = Encoding.UTF8.GetString(buffer, 0, count);
                Dictionary<string, object> message = Json.Decode(text);
                if (JVal.Str(message, "m") != ProtocolConst.Magic) return;
                if (JVal.Str(message, "id") == _deviceId) return;
                string platform = JVal.Str(message, "platform");
                if (platform == "android")
                {
                    // A phone has announced itself. The Windows side does not need to dial,
                    // but remembering the address is useful for diagnostics.
                    string name = JVal.Str(message, "name");
                    if (!string.IsNullOrEmpty(name))
                    {
                        string key = "phone:" + JVal.Str(message, "id");
                        if (key != _lastPhoneKey)
                        {
                            _lastPhoneKey = key;
                            Log.Info("发现手机 " + name + " @ " + remote.Address);
                        }
                    }
                }
            }
            catch { }
        }

        private volatile string _lastPhoneKey;

        public void Dispose()
        {
            _running = false;
            Socket socket = _socket;
            _socket = null;
            try { if (socket != null) socket.Close(); } catch { }
            try { if (_thread != null && _thread.IsAlive) _thread.Join(1200); } catch { }
            _thread = null;
        }
    }
}
