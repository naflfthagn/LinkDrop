using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LinkDrop.Tests
{
    internal static class Program
    {
        private static int _passed;
        private static int _failed;

        private static void Check(bool condition, string message)
        {
            if (condition) { _passed++; Console.WriteLine("  PASS  " + message); }
            else { _failed++; Console.WriteLine("  FAIL  " + message); }
        }

        private static void Main()
        {
            string receiveDir = Path.Combine(Path.GetTempPath(), "linkdrop-integration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(receiveDir);
            string fileToSend = Path.Combine(Path.GetTempPath(), "linkdrop-out-" + Guid.NewGuid().ToString("N") + ".bin");
            byte[] outgoing = new byte[700000];
            new Random(42).NextBytes(outgoing);
            File.WriteAllBytes(fileToSend, outgoing);

            LinkDropServer server = null;
            try
            {
                Console.WriteLine("== LinkDrop Windows protocol integration test ==");
                DeviceIdentity identity = DeviceIdentity.LoadOrCreate();
                Check(identity.Fingerprint.Length == 64, "device certificate fingerprint");
                Check(identity.SecurityCode.Length == 6, "six digit security code");

                AppSettings settings = new AppSettings();
                settings.DeviceName = "Integration-PC";
                settings.ReceiveDirectory = receiveDir;
                server = new LinkDropServer(identity, settings);
                Check(server.Start(), "start TLS server on LAN");
                Console.WriteLine("  port=" + server.Port + "  code=" + identity.SecurityCode);

                server.BeginPairing();
                PairTestClient pairClient = PairTestClient.Connect(server.Port);
                Dictionary<string, object> req = new Dictionary<string, object>();
                req["op"] = "pair_request";
                req["v"] = 1;
                req["deviceId"] = "aabbccddeeff0011";
                req["deviceName"] = "Integration-Phone";
                req["platform"] = "android";
                pairClient.Send(req);
                Dictionary<string, object> pendingNotice = pairClient.ReadJson();
                Check(JVal.Str(pendingNotice, "op") == "pair_pending", "pairing pending notice sent");

                PairingSnapshot pairing = null;
                for (int i = 0; i < 30; i++)
                {
                    pairing = server.GetPairingSnapshot();
                    if (pairing.HasPending) break;
                    Thread.Sleep(50);
                }
                Check(pairing != null && pairing.HasPending, "PC sees pending phone");
                Check(JVal.Str(pendingNotice, "message").Contains("电脑"), "pairing message is local");
                server.ApprovePairing(true);
                Dictionary<string, object> pairResult = pairClient.ReadJson();
                Check(JVal.Str(pairResult, "op") == "pair_result" && JVal.Bool(pairResult, "ok"), "pairing approved");
                string token = JVal.Str(pairResult, "token");
                Check(token.Length == 64, "pairing token issued");
                Check(JVal.Str(pairResult, "serverPin") == identity.Fingerprint, "server pin returned");
                pairClient.Close();
                Thread.Sleep(200);

                // Normal authenticated connection.
                TestClient client = TestClient.Connect(server.Port);
                Dictionary<string, object> hello = new Dictionary<string, object>();
                hello["op"] = "hello";
                hello["v"] = 1;
                hello["deviceId"] = "aabbccddeeff0011";
                hello["deviceName"] = "Integration-Phone";
                hello["platform"] = "android";
                hello["token"] = token;
                client.Send(hello);
                Dictionary<string, object> welcome = client.ReadJson();
                Check(JVal.Str(welcome, "op") == "welcome", "authenticated hello accepted");
                Check(server.GetStatus().Connected, "server sees connected phone");

                // Phone -> PC.
                byte[] incoming = Encoding.UTF8.GetBytes("hello from the phone\nLinkDrop integration\n");
                long inboundId = 123456789012345;
                Dictionary<string, object> offer = new Dictionary<string, object>();
                offer["op"] = "offer";
                offer["id"] = inboundId.ToString();
                offer["name"] = "from-phone.txt";
                offer["size"] = incoming.Length;
                offer["mime"] = "text/plain";
                client.Send(offer);
                Dictionary<string, object> accept = client.ReadJson();
                Check(JVal.Str(accept, "op") == "accept", "PC auto accepts incoming offer");
                client.SendChunk(inboundId, incoming, 0, incoming.Length / 2);
                client.SendChunk(inboundId, incoming, incoming.Length / 2, incoming.Length - incoming.Length / 2);
                Dictionary<string, object> done = new Dictionary<string, object>();
                done["op"] = "done";
                done["id"] = inboundId.ToString();
                done["sha256"] = Sha(incoming);
                client.Send(done);
                Dictionary<string, object> complete = client.ReadJson();
                Check(JVal.Str(complete, "op") == "complete", "PC confirms received file");
                string receivedPath = Path.Combine(receiveDir, "from-phone.txt");
                Check(File.Exists(receivedPath) && File.ReadAllBytes(receivedPath).Length == incoming.Length, "incoming file saved");
                Check(server.Transfers.Snapshots().Exists(delegate(TransferSnapshot s) { return s.Id == inboundId && s.State == TransferState.Completed; }), "incoming transfer completed in list");

                // PC -> phone.
                Task sendTask = server.SendFilesAsync(new string[] { fileToSend });
                Dictionary<string, object> outOffer = client.ReadUntil("offer");
                long outId = JVal.Long(outOffer, "id");
                Dictionary<string, object> ok = new Dictionary<string, object>();
                ok["op"] = "accept";
                ok["id"] = outId.ToString();
                client.Send(ok);
                MemoryStream received = new MemoryStream();
                string expected = null;
                while (true)
                {
                    ProtocolFrame frame = client.ReadFrame();
                    if (frame.Kind == 2)
                    {
                        if (frame.TransferId != outId) throw new Exception("wrong transfer id");
                        received.Write(frame.Data, 0, frame.Data.Length);
                    }
                    else
                    {
                        Dictionary<string, object> msg = Json.Decode(frame.Json);
                        string op = JVal.Str(msg, "op");
                        if (op == "done")
                        {
                            expected = JVal.Str(msg, "sha256");
                            break;
                        }
                    }
                }
                Check(received.Length == outgoing.Length, "outgoing file length matches");
                Check(Sha(received.ToArray()) == expected, "outgoing SHA-256 matches");
                Dictionary<string, object> finish = new Dictionary<string, object>();
                finish["op"] = "complete";
                finish["id"] = outId.ToString();
                client.Send(finish);
                sendTask.Wait(10000);
                Check(sendTask.IsCompleted && !sendTask.IsFaulted, "PC send task completed");
                Thread.Sleep(200);
                Check(server.Transfers.Snapshots().Exists(delegate(TransferSnapshot s) { return s.Id == outId && s.State == TransferState.Completed; }), "outgoing transfer completed");

                // Wrong token must be rejected.
                TestClient bad = TestClient.Connect(server.Port);
                Dictionary<string, object> badHello = new Dictionary<string, object>();
                badHello["op"] = "hello";
                badHello["v"] = 1;
                badHello["deviceId"] = "aabbccddeeff0011";
                badHello["deviceName"] = "Attacker";
                badHello["platform"] = "android";
                badHello["token"] = new string('0', 64);
                bad.Send(badHello);
                Dictionary<string, object> authError = bad.ReadJson();
                Check(JVal.Str(authError, "op") == "error" && JVal.Str(authError, "code") == "unauthorized", "invalid token rejected");
                bad.Close();
                client.Close();
                Thread.Sleep(300);

                Console.WriteLine("== " + _passed + " passed, " + _failed + " failed ==");
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("EXCEPTION: " + ex);
            }
            finally
            {
                if (server != null)
                {
                    try { server.Unpair(); } catch { }
                    try { server.Dispose(); } catch { }
                }
                try { TrustStore.Clear(); } catch { }
                try { Directory.Delete(receiveDir, true); } catch { }
                try { File.Delete(fileToSend); } catch { }
            }
            Environment.ExitCode = _failed == 0 ? 0 : 1;
        }

        private static string Sha(byte[] data)
        {
            using (SHA256 sha = SHA256.Create()) return CryptoUtil.ToHex(sha.ComputeHash(data));
        }
    }

    internal sealed class ProtocolFrame
    {
        public int Kind;
        public string Json;
        public long TransferId;
        public byte[] Data;
    }

    internal class ProtocolClient : IDisposable
    {
        protected TcpClient Client;
        protected SslStream Stream;
        protected FrameReader Reader;
        protected FrameWriter Writer;

        protected void Open(int port)
        {
            Client = new TcpClient();
            Client.Connect(IPAddress.Loopback, port);
            Client.ReceiveTimeout = 15000;
            Stream = new SslStream(Client.GetStream(), false, delegate(object s, System.Security.Cryptography.X509Certificates.X509Certificate c, X509Chain ch, SslPolicyErrors e) { return true; });
            Stream.AuthenticateAsClient("linkdrop.local", null, SslProtocols.Tls12, false);
            Reader = new FrameReader(Stream);
            Writer = new FrameWriter(Stream);
        }

        public void Send(Dictionary<string, object> message) { Writer.WriteJson(message); }
        public void SendChunk(long id, byte[] data, int offset, int count)
        {
            byte[] copy = new byte[count];
            Buffer.BlockCopy(data, offset, copy, 0, count);
            Writer.WriteChunk(id, copy, count);
        }
        public Dictionary<string, object> ReadJson() { return Json.Decode(Reader.ReadFrame().Json); }

        public ProtocolFrame ReadFrame()
        {
            Frame f = Reader.ReadFrame();
            ProtocolFrame result = new ProtocolFrame();
            result.Kind = (int)f.Kind;
            result.Json = f.Json;
            result.TransferId = f.TransferId;
            result.Data = f.Data;
            return result;
        }

        public Dictionary<string, object> ReadUntil(string op)
        {
            while (true)
            {
                Frame f = Reader.ReadFrame();
                if (f.Kind != FrameKind.Json) continue;
                Dictionary<string, object> msg = Json.Decode(f.Json);
                if (JVal.Str(msg, "op") == op) return msg;
            }
        }

        public void Close()
        {
            try { if (Stream != null) Stream.Close(); } catch { }
            try { if (Client != null) Client.Close(); } catch { }
        }

        public void Dispose() { Close(); }
    }

    internal sealed class TestClient : ProtocolClient
    {
        public static TestClient Connect(int port)
        {
            TestClient c = new TestClient();
            c.Open(port);
            return c;
        }
    }

    internal sealed class PairTestClient : ProtocolClient
    {
        public static PairTestClient Connect(int port)
        {
            PairTestClient c = new PairTestClient();
            c.Open(port);
            return c;
        }
    }
}
