using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace LinkDrop
{
    internal static class ProtocolConst
    {
        public const string Magic = "QCLINK1";
        public const int Version = 1;
        public const int DiscoveryPort = 48721;
        public const int DefaultTcpPort = 48622;
        public const int MaxTcpPort = 48632;
        public const int MaxFrame = 4 * 1024 * 1024;
        public const int ChunkSize = 256 * 1024;
        public const string MulticastGroup = "239.255.47.22";
        public const string Platform = "windows";
    }

    internal enum FrameKind
    {
        Json = 1,
        Binary = 2
    }

    internal sealed class Frame
    {
        public FrameKind Kind;
        public string Json;
        public long TransferId;
        public byte[] Data;
    }

    internal static class Json
    {
        private static readonly object Gate = new object();

        public static string Encode(Dictionary<string, object> value)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = ProtocolConst.MaxFrame;
            lock (Gate)
            {
                return serializer.Serialize(value);
            }
        }

        public static Dictionary<string, object> Decode(string value)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = ProtocolConst.MaxFrame;
            object parsed;
            lock (Gate)
            {
                parsed = serializer.DeserializeObject(value);
            }
            Dictionary<string, object> map = parsed as Dictionary<string, object>;
            if (map == null) throw new ProtocolException("消息不是有效的 JSON 对象");
            return map;
        }
    }

    internal static class JVal
    {
        public static string Str(Dictionary<string, object> map, string key)
        {
            object v;
            if (map != null && map.TryGetValue(key, out v) && v != null)
                return Convert.ToString(v, CultureInfo.InvariantCulture);
            return "";
        }

        public static long Long(Dictionary<string, object> map, string key)
        {
            object v;
            if (map == null || !map.TryGetValue(key, out v) || v == null) return 0;
            try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        public static bool Bool(Dictionary<string, object> map, string key)
        {
            object v;
            if (map == null || !map.TryGetValue(key, out v) || v == null) return false;
            if (v is bool) return (bool)v;
            string s = Convert.ToString(v, CultureInfo.InvariantCulture);
            return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message) { }
    }

    internal sealed class FrameReader
    {
        private readonly Stream _stream;

        public FrameReader(Stream stream)
        {
            _stream = stream;
        }

        public Frame ReadFrame()
        {
            byte[] header = ReadExact(4);
            uint length = ((uint)header[0] << 24) | ((uint)header[1] << 16) | ((uint)header[2] << 8) | header[3];
            if (length < 2 || length > ProtocolConst.MaxFrame)
                throw new ProtocolException("收到的数据帧长度异常: " + length);

            byte[] payload = ReadExact((int)length);
            Frame frame = new Frame();
            frame.Kind = (FrameKind)payload[0];
            if (frame.Kind == FrameKind.Json)
            {
                frame.Json = Encoding.UTF8.GetString(payload, 1, payload.Length - 1);
            }
            else if (frame.Kind == FrameKind.Binary)
            {
                if (payload.Length < 13) throw new ProtocolException("二进制帧过短");
                byte[] idBytes = new byte[8];
                Buffer.BlockCopy(payload, 1, idBytes, 0, 8);
                Array.Reverse(idBytes);
                frame.TransferId = BitConverter.ToInt64(idBytes, 0);
                uint chunkLength = ((uint)payload[9] << 24) | ((uint)payload[10] << 16) | ((uint)payload[11] << 8) | payload[12];
                if (chunkLength > (uint)(payload.Length - 13))
                    throw new ProtocolException("文件块长度异常");
                frame.Data = new byte[chunkLength];
                Buffer.BlockCopy(payload, 13, frame.Data, 0, (int)chunkLength);
            }
            else
            {
                throw new ProtocolException("未知的数据帧类型");
            }
            return frame;
        }

        private byte[] ReadExact(int count)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = _stream.Read(buffer, offset, count - offset);
                if (read <= 0) throw new EndOfStreamException("连接已关闭");
                offset += read;
            }
            return buffer;
        }
    }

    internal sealed class FrameWriter
    {
        private readonly Stream _stream;
        private readonly object _gate = new object();

        public FrameWriter(Stream stream)
        {
            _stream = stream;
        }

        public void WriteJson(Dictionary<string, object> message)
        {
            string json = Json.Encode(message);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            if (bytes.Length + 1 > ProtocolConst.MaxFrame) throw new ProtocolException("控制消息过大");
            lock (_gate)
            {
                WriteUInt32((uint)(bytes.Length + 1));
                _stream.WriteByte((byte)FrameKind.Json);
                _stream.Write(bytes, 0, bytes.Length);
                _stream.Flush();
            }
        }

        public void WriteChunk(long transferId, byte[] data, int count)
        {
            if (count < 0 || count > ProtocolConst.ChunkSize) throw new ProtocolException("文件块大小非法");
            byte[] idBytes = BitConverter.GetBytes(transferId);
            Array.Reverse(idBytes);
            lock (_gate)
            {
                WriteUInt32((uint)(1 + 8 + 4 + count));
                _stream.WriteByte((byte)FrameKind.Binary);
                _stream.Write(idBytes, 0, 8);
                WriteUInt32((uint)count);
                _stream.Write(data, 0, count);
                _stream.Flush();
            }
        }

        private void WriteUInt32(uint value)
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(value >> 24);
            bytes[1] = (byte)(value >> 16);
            bytes[2] = (byte)(value >> 8);
            bytes[3] = (byte)value;
            _stream.Write(bytes, 0, 4);
        }
    }

    internal static class SecurityCode
    {
        public static string FromFingerprint(string fingerprintHex)
        {
            if (string.IsNullOrEmpty(fingerprintHex) || fingerprintHex.Length < 8) return "000000";
            uint value = uint.Parse(fingerprintHex.Substring(0, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (value % 1000000).ToString("D6", CultureInfo.InvariantCulture);
        }

        public static string FormatForDisplay(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != 6) return code ?? "";
            return code.Substring(0, 3) + " " + code.Substring(3);
        }
    }

    internal static class CryptoUtil
    {
        public static string Sha256Hex(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(data));
            }
        }

        public static string Sha256Hex(string text)
        {
            return Sha256Hex(Encoding.UTF8.GetBytes(text));
        }

        public static string RandomHex(int byteCount)
        {
            byte[] bytes = new byte[byteCount];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return ToHex(bytes);
        }

        public static long RandomPositiveLong()
        {
            byte[] bytes = new byte[8];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            long value = BitConverter.ToInt64(bytes, 0) & 0x7FFFFFFFFFFFFFFF;
            return value == 0 ? 1 : value;
        }

        public static string ToHex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        public static bool FixedEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            byte[] aa = Encoding.UTF8.GetBytes(a);
            byte[] bb = Encoding.UTF8.GetBytes(b);
            int diff = aa.Length ^ bb.Length;
            int count = Math.Min(aa.Length, bb.Length);
            for (int i = 0; i < count; i++) diff |= aa[i] ^ bb[i];
            return diff == 0;
        }
    }

    internal static class FileNames
    {
        public static string Safe(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "文件";
            name = Path.GetFileName(name);
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                bool bad = false;
                foreach (char x in invalid) { if (c == x) { bad = true; break; } }
                sb.Append(bad || c == '\r' || c == '\n' ? '_' : c);
            }
            string clean = sb.ToString().Trim().TrimEnd('.');
            if (string.IsNullOrEmpty(clean)) clean = "文件";
            if (clean.Length > 140)
            {
                string ext = Path.GetExtension(clean);
                if (ext.Length > 16) ext = "";
                clean = clean.Substring(0, Math.Max(1, 140 - ext.Length)) + ext;
            }
            return clean;
        }

        public static string UniquePath(string directory, string fileName)
        {
            string safe = Safe(fileName);
            string candidate = Path.Combine(directory, safe);
            if (!File.Exists(candidate)) return candidate;
            string baseName = Path.GetFileNameWithoutExtension(safe);
            string ext = Path.GetExtension(safe);
            for (int i = 2; i < 10000; i++)
            {
                candidate = Path.Combine(directory, baseName + " (" + i.ToString(CultureInfo.InvariantCulture) + ")" + ext);
                if (!File.Exists(candidate)) return candidate;
            }
            return Path.Combine(directory, baseName + " (" + DateTime.Now.ToString("yyyyMMddHHmmss") + ")" + ext);
        }
    }

    internal static class HumanSize
    {
        public static string Format(long bytes)
        {
            if (bytes < 1024) return bytes.ToString(CultureInfo.InvariantCulture) + " B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return kb.ToString("0.#", CultureInfo.InvariantCulture) + " KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return mb.ToString("0.#", CultureInfo.InvariantCulture) + " MB";
            double gb = mb / 1024.0;
            return gb.ToString("0.##", CultureInfo.InvariantCulture) + " GB";
        }
    }
}
