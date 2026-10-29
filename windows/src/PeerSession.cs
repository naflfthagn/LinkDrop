using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LinkDrop
{
    internal sealed class PeerSession
    {
        private sealed class Waiter
        {
            public TaskCompletionSource<bool> Completion = new TaskCompletionSource<bool>();
            public string Error = "";
        }

        private sealed class IncomingTransfer
        {
            public long Id;
            public string Name;
            public string Mime;
            public long Size;
            public long Received;
            public string TempPath;
            public FileStream Stream;
            public SHA256 Hash;
            public TransferItem Item;
            public DateTime LastUpdate;
            public Stopwatch Clock;
        }

        private readonly LinkDropServer _server;
        private readonly TcpClient _client;
        private readonly SemaphoreSlim _fileSendGate = new SemaphoreSlim(1, 1);
        private readonly object _stateGate = new object();
        private readonly Dictionary<long, Waiter> _offerWaits = new Dictionary<long, Waiter>();
        private readonly Dictionary<long, Waiter> _completeWaits = new Dictionary<long, Waiter>();
        private readonly Dictionary<long, IncomingTransfer> _incoming = new Dictionary<long, IncomingTransfer>();
        private readonly Dictionary<long, TransferItem> _outgoing = new Dictionary<long, TransferItem>();
        private SslStream _ssl;
        private FrameReader _reader;
        private FrameWriter _writer;
        private Timer _pingTimer;
        private volatile bool _open;
        private int _closed;
        private string _peerName = "";
        private string _peerPlatform = "";
        private string _peerAddress = "";
        private string _peerId = "";

        public PeerSession(LinkDropServer server, TcpClient client)
        {
            _server = server;
            _client = client;
        }

        internal string PeerName { get { return _peerName; } }
        internal string PeerPlatform { get { return _peerPlatform; } }
        internal string PeerAddress { get { return _peerAddress; } }

        public void Run()
        {
            string closeReason = "连接已断开";
            try
            {
                _client.NoDelay = true;
                _client.ReceiveTimeout = 15000;
                _client.SendTimeout = 15000;
                _ssl = new SslStream(_client.GetStream(), false);
                _ssl.ReadTimeout = 15000;
                _ssl.WriteTimeout = 15000;
                _ssl.AuthenticateAsServer(_server.Identity.Certificate, false, SslProtocols.Tls12, false);
                _reader = new FrameReader(_ssl);
                _writer = new FrameWriter(_ssl);
                _ssl.ReadTimeout = 60000;
                _ssl.WriteTimeout = 60000;

                Frame first = _reader.ReadFrame();
                if (first.Kind != FrameKind.Json) throw new ProtocolException("首条消息必须是 JSON");
                Dictionary<string, object> message = Json.Decode(first.Json);
                string op = JVal.Str(message, "op");
                if (op == "pair_request")
                {
                    HandlePairRequest(message);
                    closeReason = "配对流程结束";
                    return;
                }
                if (op != "hello") throw new ProtocolException("未知的握手消息");
                if (!HandleHello(message))
                {
                    closeReason = "身份验证失败";
                    return;
                }

                _open = true;
                _ssl.ReadTimeout = 60000;
                _pingTimer = new Timer(PingTick, null, 15000, 15000);
                StartKeepaliveTimestamp();
                ReadLoop();
            }
            catch (AuthenticationException ex)
            {
                Log.Error("TLS 握手失败", ex);
                closeReason = "安全连接失败";
            }
            catch (IOException ex)
            {
                if (_open) Log.Error("连接读取失败", ex);
                closeReason = "连接已断开";
            }
            catch (SocketException ex)
            {
                if (_open) Log.Error("网络连接异常", ex);
                closeReason = "网络连接异常";
            }
            catch (ObjectDisposedException)
            {
                closeReason = "连接已关闭";
            }
            catch (ProtocolException ex)
            {
                Log.Error("协议错误", ex);
                closeReason = "协议不兼容";
                try { SendError("protocol", "协议错误：" + ex.Message); } catch { }
            }
            catch (Exception ex)
            {
                Log.Error("连接处理异常", ex);
                closeReason = "连接异常";
            }
            finally
            {
                Shutdown(closeReason);
            }
        }

        private void StartKeepaliveTimestamp()
        {
            _lastPing = DateTime.UtcNow;
        }

        private DateTime _lastPing;

        private void PingTick(object state)
        {
            if (!_open) return;
            try
            {
                Dictionary<string, object> message = new Dictionary<string, object>();
                message["op"] = "ping";
                message["t"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                SendJson(message);
                _lastPing = DateTime.UtcNow;
            }
            catch
            {
                Close("心跳超时");
            }
        }

        private void HandlePairRequest(Dictionary<string, object> message)
        {
            string deviceId = JVal.Str(message, "deviceId");
            string deviceName = JVal.Str(message, "deviceName");
            string platform = JVal.Str(message, "platform");
            if (string.IsNullOrEmpty(deviceId) || deviceId.Length < 8)
            {
                SendPairResult(false, null, "手机设备标识无效");
                return;
            }
            PendingPairRequest pending;
            try
            {
                pending = _server.BeginPairRequest(deviceId, deviceName, platform);
            }
            catch (Exception ex)
            {
                SendPairResult(false, null, ex.Message);
                return;
            }

            Dictionary<string, object> waiting = new Dictionary<string, object>();
            waiting["op"] = "pair_pending";
            waiting["message"] = "请在电脑上核对安全码并允许配对";
            SendJson(waiting);

            bool decided = false;
            bool allowed = false;
            try
            {
                decided = pending.Decision.Task.Wait(TimeSpan.FromSeconds(90));
                if (decided) allowed = pending.Decision.Task.Result;
            }
            catch { }

            if (!decided)
            {
                _server.FailPairing(pending, "等待电脑确认超时");
                SendPairResult(false, null, "等待电脑确认超时");
                return;
            }
            if (!allowed)
            {
                _server.FailPairing(pending, "电脑拒绝或取消了配对");
                SendPairResult(false, null, "电脑拒绝或取消了配对");
                return;
            }

            string token = CryptoUtil.RandomHex(32);
            _server.CompletePairing(pending, token);
            SendPairResult(true, token, null);
            Thread.Sleep(120);
        }

        private void SendPairResult(bool ok, string token, string error)
        {
            try
            {
                Dictionary<string, object> result = new Dictionary<string, object>();
                result["op"] = "pair_result";
                result["ok"] = ok;
                result["v"] = ProtocolConst.Version;
                result["serverId"] = _server.Identity.ShortId;
                result["serverPin"] = _server.Identity.Fingerprint;
                result["serverName"] = _server.Settings.DeviceName;
                result["serverPlatform"] = ProtocolConst.Platform;
                if (ok) result["token"] = token;
                if (!string.IsNullOrEmpty(error)) result["error"] = error;
                SendJson(result);
            }
            catch (Exception ex)
            {
                Log.Error("发送配对结果失败", ex);
            }
        }

        private bool HandleHello(Dictionary<string, object> message)
        {
            string deviceId = JVal.Str(message, "deviceId");
            string deviceName = JVal.Str(message, "deviceName");
            string platform = JVal.Str(message, "platform");
            string token = JVal.Str(message, "token");
            string error;
            if (!_server.TryAuthenticate(deviceId, deviceName, platform, token, out error))
            {
                SendError("unauthorized", error);
                Log.Info("手机鉴权失败：" + error);
                return false;
            }

            _peerId = deviceId;
            _peerName = string.IsNullOrEmpty(deviceName) ? "Android 手机" : deviceName;
            _peerPlatform = string.IsNullOrEmpty(platform) ? "android" : platform;
            try { _peerAddress = ((IPEndPoint)_client.Client.RemoteEndPoint).Address.ToString(); }
            catch { _peerAddress = "局域网"; }

            Dictionary<string, object> welcome = new Dictionary<string, object>();
            welcome["op"] = "welcome";
            welcome["v"] = ProtocolConst.Version;
            welcome["deviceId"] = _server.Identity.ShortId;
            welcome["deviceName"] = _server.Settings.DeviceName;
            welcome["platform"] = ProtocolConst.Platform;
            welcome["port"] = _server.Port;
            SendJson(welcome);
            _server.AttachSession(this);
            Log.Info("手机已连接：" + _peerName + " @ " + _peerAddress);
            return true;
        }

        private void ReadLoop()
        {
            while (_open)
            {
                Frame frame = _reader.ReadFrame();
                if (frame.Kind == FrameKind.Json)
                {
                    HandleControl(Json.Decode(frame.Json));
                }
                else if (frame.Kind == FrameKind.Binary)
                {
                    HandleBinary(frame);
                }
            }
        }

        private void HandleControl(Dictionary<string, object> message)
        {
            string op = JVal.Str(message, "op");
            long id = JVal.Long(message, "id");
            if (op == "offer")
            {
                HandleIncomingOffer(message, id);
            }
            else if (op == "accept")
            {
                Waiter waiter = TakeWaiter(_offerWaits, id);
                if (waiter != null) waiter.Completion.TrySetResult(true);
            }
            else if (op == "reject")
            {
                Waiter waiter = TakeWaiter(_offerWaits, id);
                if (waiter != null)
                {
                    waiter.Error = JVal.Str(message, "reason");
                    if (string.IsNullOrEmpty(waiter.Error)) waiter.Error = "手机拒绝了文件";
                    waiter.Completion.TrySetResult(false);
                }
            }
            else if (op == "done")
            {
                HandleIncomingDone(message, id);
            }
            else if (op == "complete")
            {
                Waiter waiter = TakeWaiter(_completeWaits, id);
                if (waiter != null) waiter.Completion.TrySetResult(true);
            }
            else if (op == "fail")
            {
                string error = JVal.Str(message, "message");
                if (string.IsNullOrEmpty(error)) error = "传输失败";
                Waiter waiter = TakeWaiter(_completeWaits, id);
                if (waiter != null)
                {
                    waiter.Error = error;
                    waiter.Completion.TrySetResult(false);
                }
                AbortIncoming(id, error);
            }
            else if (op == "ping")
            {
                Dictionary<string, object> pong = new Dictionary<string, object>();
                pong["op"] = "pong";
                pong["t"] = JVal.Long(message, "t");
                SendJson(pong);
            }
            else if (op == "pong")
            {
            }
            else if (op == "error")
            {
                Log.Info("手机返回错误：" + JVal.Str(message, "message"));
            }
        }

        private Waiter TakeWaiter(Dictionary<long, Waiter> map, long id)
        {
            lock (_stateGate)
            {
                Waiter waiter;
                if (map.TryGetValue(id, out waiter))
                {
                    map.Remove(id);
                    return waiter;
                }
            }
            return null;
        }

        private void HandleIncomingOffer(Dictionary<string, object> message, long id)
        {
            string name = JVal.Str(message, "name");
            long size = JVal.Long(message, "size");
            string mime = JVal.Str(message, "mime");
            if (id <= 0 || size < 0 || size > (long)1024 * 1024 * 1024 * 1024)
            {
                SendReject(id, "文件信息无效");
                return;
            }
            string directory = _server.Settings.ReceiveDirectory;
            if (string.IsNullOrEmpty(directory)) directory = AppPaths.DefaultReceiveDirectory;
            try { Directory.CreateDirectory(directory); }
            catch (Exception ex)
            {
                SendReject(id, "无法创建接收文件夹：" + ex.Message);
                return;
            }
            if (size > 0)
            {
                try
                {
                    DriveInfo drive = new DriveInfo(Path.GetPathRoot(directory));
                    if (drive.AvailableFreeSpace < size + 64L * 1024 * 1024)
                    {
                        SendReject(id, "电脑磁盘空间不足");
                        return;
                    }
                }
                catch { }
            }

            lock (_stateGate)
            {
                if (_incoming.ContainsKey(id) || _outgoing.ContainsKey(id))
                {
                    SendReject(id, "传输编号冲突");
                    return;
                }
            }

            string temp = Path.Combine(directory, ".linkdrop-" + id.ToString() + ".part");
            FileStream stream = null;
            try
            {
                stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan);
                IncomingTransfer incoming = new IncomingTransfer();
                incoming.Id = id;
                incoming.Name = FileNames.Safe(name);
                incoming.Mime = mime;
                incoming.Size = size;
                incoming.TempPath = temp;
                incoming.Stream = stream;
                incoming.Hash = SHA256.Create();
                incoming.Item = _server.Transfers.Add(id, TransferDirection.Incoming, incoming.Name, size, mime);
                incoming.Item.SetState(TransferState.Transferring);
                incoming.LastUpdate = DateTime.MinValue;
                incoming.Clock = Stopwatch.StartNew();
                lock (_stateGate) _incoming[id] = incoming;

                Dictionary<string, object> accept = new Dictionary<string, object>();
                accept["op"] = "accept";
                accept["id"] = id.ToString();
                SendJson(accept);
                Log.Info("开始接收：" + incoming.Name + " (" + HumanSize.Format(size) + ")");
            }
            catch (Exception ex)
            {
                if (stream != null) { try { stream.Dispose(); } catch { } }
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                SendReject(id, "无法创建临时文件");
                Log.Error("准备接收文件失败", ex);
            }
        }

        private void SendReject(long id, string reason)
        {
            try
            {
                Dictionary<string, object> reject = new Dictionary<string, object>();
                reject["op"] = "reject";
                reject["id"] = id.ToString();
                reject["reason"] = reason;
                SendJson(reject);
            }
            catch { }
        }

        private void HandleBinary(Frame frame)
        {
            IncomingTransfer incoming;
            lock (_stateGate) _incoming.TryGetValue(frame.TransferId, out incoming);
            if (incoming == null)
            {
                Log.Info("收到未知文件块，忽略");
                return;
            }
            int count = frame.Data == null ? 0 : frame.Data.Length;
            if (count == 0) return;
            if (incoming.Received + count > incoming.Size)
            {
                AbortIncoming(incoming.Id, "传输数据超过文件大小");
                return;
            }
            try
            {
                incoming.Stream.Write(frame.Data, 0, count);
                incoming.Hash.TransformBlock(frame.Data, 0, count, null, 0);
                incoming.Received += count;
                DateTime now = DateTime.UtcNow;
                if ((now - incoming.LastUpdate).TotalMilliseconds > 90 || incoming.Received == incoming.Size)
                {
                    double speed = incoming.Clock.Elapsed.TotalSeconds > 0.2
                        ? incoming.Received / incoming.Clock.Elapsed.TotalSeconds : 0;
                    incoming.Item.SetProgress(incoming.Received, speed);
                    incoming.LastUpdate = now;
                }
            }
            catch (Exception ex)
            {
                AbortIncoming(incoming.Id, "写入文件失败：" + ex.Message);
            }
        }

        private void HandleIncomingDone(Dictionary<string, object> message, long id)
        {
            string expectedHash = JVal.Str(message, "sha256");
            IncomingTransfer incoming;
            lock (_stateGate)
            {
                _incoming.TryGetValue(id, out incoming);
                if (incoming != null) _incoming.Remove(id);
            }
            if (incoming == null)
            {
                SendFail(id, "接收端找不到该传输");
                return;
            }
            try
            {
                if (incoming.Received != incoming.Size)
                    throw new IOException("接收大小不一致");
                incoming.Hash.TransformFinalBlock(new byte[0], 0, 0);
                string actualHash = CryptoUtil.ToHex(incoming.Hash.Hash);
                if (!CryptoUtil.FixedEquals(expectedHash, actualHash))
                    throw new IOException("SHA-256 校验失败");
                incoming.Stream.Flush();
                incoming.Stream.Dispose();
                incoming.Stream = null;
                incoming.Hash.Dispose();
                incoming.Hash = null;
                incoming.Item.SetState(TransferState.Verifying);
                string directory = Path.GetDirectoryName(incoming.TempPath);
                string finalPath = FileNames.UniquePath(directory, incoming.Name);
                File.Move(incoming.TempPath, finalPath);
                incoming.Item.Complete(finalPath, incoming.Size);
                Dictionary<string, object> complete = new Dictionary<string, object>();
                complete["op"] = "complete";
                complete["id"] = id.ToString();
                SendJson(complete);
                Log.Info("接收完成：" + finalPath);
                try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
            }
            catch (Exception ex)
            {
                SafeDisposeIncoming(incoming);
                incoming.Item.Fail(ex.Message);
                SendFail(id, ex.Message);
                Log.Error("接收文件校验失败", ex);
            }
        }

        private void AbortIncoming(long id, string reason)
        {
            IncomingTransfer incoming;
            lock (_stateGate)
            {
                _incoming.TryGetValue(id, out incoming);
                if (incoming != null) _incoming.Remove(id);
            }
            if (incoming == null) return;
            SafeDisposeIncoming(incoming);
            incoming.Item.Fail(reason);
            try
            {
                Dictionary<string, object> fail = new Dictionary<string, object>();
                fail["op"] = "fail";
                fail["id"] = id.ToString();
                fail["message"] = reason;
                SendJson(fail);
            }
            catch { }
            Log.Info("接收中止：" + incoming.Name + " - " + reason);
        }

        private void SafeDisposeIncoming(IncomingTransfer incoming)
        {
            try { if (incoming.Stream != null) incoming.Stream.Dispose(); } catch { }
            incoming.Stream = null;
            try { if (incoming.Hash != null) incoming.Hash.Dispose(); } catch { }
            incoming.Hash = null;
            try { if (File.Exists(incoming.TempPath)) File.Delete(incoming.TempPath); } catch { }
        }

        private void SendFail(long id, string message)
        {
            try
            {
                Dictionary<string, object> fail = new Dictionary<string, object>();
                fail["op"] = "fail";
                fail["id"] = id.ToString();
                fail["message"] = message;
                SendJson(fail);
            }
            catch { }
        }

        public async Task SendFilesAsync(string[] paths)
        {
            await Task.Yield();
            await _fileSendGate.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (string path in paths)
                {
                    if (!_open) throw new InvalidOperationException("连接已断开");
                    await SendOneFileAsync(path).ConfigureAwait(false);
                }
            }
            finally
            {
                _fileSendGate.Release();
            }
        }

        private async Task SendOneFileAsync(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("文件不存在", path);
            FileInfo info = new FileInfo(path);
            long size = info.Length;
            long id = CryptoUtil.RandomPositiveLong();
            TransferItem transfer = _server.Transfers.Add(id, TransferDirection.Outgoing, info.Name, size, "");
            lock (_stateGate) _outgoing[id] = transfer;

            Waiter offerWaiter = new Waiter();
            lock (_stateGate) _offerWaits[id] = offerWaiter;
            try
            {
                Dictionary<string, object> offer = new Dictionary<string, object>();
                offer["op"] = "offer";
                offer["id"] = id.ToString();
                offer["name"] = info.Name;
                offer["size"] = size;
                offer["mime"] = "";
                SendJson(offer);
                transfer.SetState(TransferState.Waiting);

                Task completed = await Task.WhenAny(offerWaiter.Completion.Task, Task.Delay(45000)).ConfigureAwait(false);
                if (completed != offerWaiter.Completion.Task)
                {
                    throw new IOException("等待手机接受文件超时");
                }
                if (!offerWaiter.Completion.Task.Result)
                {
                    throw new IOException(string.IsNullOrEmpty(offerWaiter.Error) ? "手机拒绝了文件" : offerWaiter.Error);
                }

                transfer.SetState(TransferState.Transferring);
                using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan))
                using (SHA256 hash = SHA256.Create())
                {
                    byte[] buffer = new byte[ProtocolConst.ChunkSize];
                    long sent = 0;
                    DateTime lastUpdate = DateTime.MinValue;
                    Stopwatch clock = Stopwatch.StartNew();
                    while (true)
                    {
                        if (!_open) throw new IOException("连接已断开");
                        int read = input.Read(buffer, 0, buffer.Length);
                        if (read <= 0) break;
                        hash.TransformBlock(buffer, 0, read, null, 0);
                        _writer.WriteChunk(id, buffer, read);
                        sent += read;
                        DateTime now = DateTime.UtcNow;
                        if ((now - lastUpdate).TotalMilliseconds > 90 || sent == size)
                        {
                            double speed = clock.Elapsed.TotalSeconds > 0.2 ? sent / clock.Elapsed.TotalSeconds : 0;
                            transfer.SetProgress(sent, speed);
                            lastUpdate = now;
                        }
                    }
                    if (sent != size) throw new IOException("文件大小在发送过程中发生变化");
                    hash.TransformFinalBlock(new byte[0], 0, 0);
                    string digest = CryptoUtil.ToHex(hash.Hash);
                    transfer.SetProgress(size, 0);
                    transfer.SetState(TransferState.Verifying);

                    Waiter doneWaiter = new Waiter();
                    lock (_stateGate) _completeWaits[id] = doneWaiter;
                    Dictionary<string, object> done = new Dictionary<string, object>();
                    done["op"] = "done";
                    done["id"] = id.ToString();
                    done["sha256"] = digest;
                    SendJson(done);

                    Task doneTask = await Task.WhenAny(doneWaiter.Completion.Task, Task.Delay(30000)).ConfigureAwait(false);
                    if (doneTask != doneWaiter.Completion.Task || !doneWaiter.Completion.Task.Result)
                    {
                        string error = string.IsNullOrEmpty(doneWaiter.Error) ? "等待手机确认超时" : doneWaiter.Error;
                        transfer.Fail(error);
                        throw new IOException(error);
                    }
                    transfer.Complete(path, size);
                    Log.Info("发送完成：" + info.Name);
                }
            }
            catch (Exception ex)
            {
                transfer.Fail(ex.Message);
                try { SendFail(id, ex.Message); } catch { }
                Log.Error("发送文件失败：" + path, ex);
            }
            finally
            {
                lock (_stateGate)
                {
                    _offerWaits.Remove(id);
                    _completeWaits.Remove(id);
                    _outgoing.Remove(id);
                }
            }
        }

        private void SendJson(Dictionary<string, object> message)
        {
            if (_writer == null) throw new IOException("连接尚未建立");
            _writer.WriteJson(message);
        }

        private void SendError(string code, string message)
        {
            try
            {
                Dictionary<string, object> error = new Dictionary<string, object>();
                error["op"] = "error";
                error["code"] = code;
                error["message"] = message;
                SendJson(error);
            }
            catch { }
        }

        internal void Close(string reason)
        {
            Shutdown(reason);
        }

        private void Shutdown(string reason)
        {
            if (Interlocked.Exchange(ref _closed, 1) == 1) return;
            bool wasOpen = _open;
            _open = false;
            try { if (_pingTimer != null) _pingTimer.Dispose(); } catch { }
            _pingTimer = null;
            try { if (_ssl != null) _ssl.Close(); } catch { }
            try { if (_client != null) _client.Close(); } catch { }

            List<Waiter> waiters = new List<Waiter>();
            List<IncomingTransfer> incoming = new List<IncomingTransfer>();
            List<TransferItem> outgoing = new List<TransferItem>();
            lock (_stateGate)
            {
                waiters.AddRange(_offerWaits.Values);
                waiters.AddRange(_completeWaits.Values);
                _offerWaits.Clear();
                _completeWaits.Clear();
                incoming.AddRange(_incoming.Values);
                _incoming.Clear();
                outgoing.AddRange(_outgoing.Values);
                _outgoing.Clear();
            }
            foreach (Waiter waiter in waiters)
            {
                waiter.Error = reason;
                try { waiter.Completion.TrySetResult(false); } catch { }
            }
            foreach (IncomingTransfer transfer in incoming)
            {
                SafeDisposeIncoming(transfer);
                transfer.Item.Fail(reason);
            }
            foreach (TransferItem transfer in outgoing)
            {
                transfer.Fail(reason);
            }
            try { _server.SessionClosed(this); } catch { }
            if (wasOpen) Log.Info("连接关闭：" + reason + " - " + _peerName);
        }
    }
}
