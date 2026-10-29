using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace LinkDrop
{
    internal sealed class AppSettings
    {
        public string DeviceName = Environment.MachineName;
        public string ReceiveDirectory = "";
        public bool AutoStart = false;
    }

    internal sealed class PairedDevice
    {
        public string DeviceId = "";
        public string DeviceName = "";
        public string Platform = "android";
        public string TokenHash = "";
        public string PairedAt = "";
        public string LastSeen = "";
    }

    internal static class AppPaths
    {
        public static string BaseDir
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LinkDrop");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string SettingsFile { get { return Path.Combine(BaseDir, "settings.json"); } }
        public static string TrustFile { get { return Path.Combine(BaseDir, "paired.json"); } }
        public static string IdentityPfx { get { return Path.Combine(BaseDir, "identity.pfx"); } }
        public static string IdentityPassword { get { return Path.Combine(BaseDir, "identity.pwd"); } }
        public static string LogFile { get { return Path.Combine(BaseDir, "linkdrop.log"); } }

        public static string DefaultReceiveDirectory
        {
            get
            {
                string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (!Directory.Exists(downloads)) downloads = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(downloads, "LinkDrop");
            }
        }
    }

    internal static class Log
    {
        private static readonly object Gate = new object();

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string message, Exception ex)
        {
            Write("ERROR", ex == null ? message : message + " :: " + ex.Message);
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + message + Environment.NewLine;
                    File.AppendAllText(AppPaths.LogFile, line, Encoding.UTF8);
                    FileInfo info = new FileInfo(AppPaths.LogFile);
                    if (info.Length > 512 * 1024)
                    {
                        string old = AppPaths.LogFile + ".1";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(AppPaths.LogFile, old);
                    }
                }
            }
            catch { }
        }
    }

    internal static class Dpapi
    {
        public static byte[] Protect(byte[] data)
        {
            return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        }

        public static byte[] Unprotect(byte[] data)
        {
            return ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
        }

        public static string ProtectString(string value)
        {
            return Convert.ToBase64String(Protect(Encoding.UTF8.GetBytes(value)));
        }

        public static string UnprotectString(string value)
        {
            return Encoding.UTF8.GetString(Unprotect(Convert.FromBase64String(value)));
        }
    }

    internal static class SettingsStore
    {
        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            try
            {
                if (File.Exists(AppPaths.SettingsFile))
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    AppSettings loaded = serializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile, Encoding.UTF8));
                    if (loaded != null) settings = loaded;
                }
            }
            catch (Exception ex)
            {
                Log.Error("读取设置失败", ex);
            }
            if (string.IsNullOrWhiteSpace(settings.DeviceName)) settings.DeviceName = Environment.MachineName;
            if (string.IsNullOrWhiteSpace(settings.ReceiveDirectory)) settings.ReceiveDirectory = AppPaths.DefaultReceiveDirectory;
            try { Directory.CreateDirectory(settings.ReceiveDirectory); } catch { }
            return settings;
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(settings);
                string temp = AppPaths.SettingsFile + ".tmp";
                File.WriteAllText(temp, json, Encoding.UTF8);
                if (File.Exists(AppPaths.SettingsFile)) File.Delete(AppPaths.SettingsFile);
                File.Move(temp, AppPaths.SettingsFile);
            }
            catch (Exception ex)
            {
                Log.Error("保存设置失败", ex);
            }
        }
    }

    internal static class TrustStore
    {
        public static PairedDevice Load()
        {
            try
            {
                if (!File.Exists(AppPaths.TrustFile)) return null;
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                PairedDevice device = serializer.Deserialize<PairedDevice>(File.ReadAllText(AppPaths.TrustFile, Encoding.UTF8));
                if (device == null || string.IsNullOrEmpty(device.TokenHash) || string.IsNullOrEmpty(device.DeviceId)) return null;
                return device;
            }
            catch (Exception ex)
            {
                Log.Error("读取配对记录失败", ex);
                return null;
            }
        }

        public static void Save(PairedDevice device)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(device);
            string temp = AppPaths.TrustFile + ".tmp";
            File.WriteAllText(temp, json, Encoding.UTF8);
            if (File.Exists(AppPaths.TrustFile)) File.Delete(AppPaths.TrustFile);
            File.Move(temp, AppPaths.TrustFile);
        }

        public static void Clear()
        {
            try { if (File.Exists(AppPaths.TrustFile)) File.Delete(AppPaths.TrustFile); }
            catch (Exception ex) { Log.Error("删除配对记录失败", ex); }
        }
    }

    internal static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "LinkDrop";

        public static void Apply(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;
                    if (enabled)
                        key.SetValue(ValueName, "\"" + System.Windows.Forms.Application.ExecutablePath + "\" --background");
                    else
                        key.DeleteValue(ValueName, false);
                }
            }
            catch (Exception ex)
            {
                Log.Error("设置开机启动失败", ex);
            }
        }

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null) return false;
                    return key.GetValue(ValueName) != null;
                }
            }
            catch { return false; }
        }
    }

    internal static class NetworkUtil
    {
        public static List<string> LocalIPv4Addresses()
        {
            List<string> result = new List<string>();
            try
            {
                foreach (System.Net.NetworkInformation.NetworkInterface nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    foreach (System.Net.NetworkInformation.UnicastIPAddressInformation info in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (info.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                        string value = info.Address.ToString();
                        if (!value.StartsWith("169.254.")) result.Add(value);
                    }
                }
            }
            catch { }
            return result;
        }

        public static string PrimaryIPv4Address()
        {
            try
            {
                using (System.Net.Sockets.Socket socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                    System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp))
                {
                    socket.Connect(new System.Net.IPEndPoint(System.Net.IPAddress.Parse("8.8.8.8"), 65530));
                    System.Net.IPEndPoint endpoint = socket.LocalEndPoint as System.Net.IPEndPoint;
                    if (endpoint != null && !System.Net.IPAddress.IsLoopback(endpoint.Address))
                        return endpoint.Address.ToString();
                }
            }
            catch { }
            List<string> addresses = LocalIPv4Addresses();
            return addresses.Count > 0 ? addresses[0] : "";
        }

        public static List<System.Net.IPAddress> BroadcastAddresses()
        {
            List<System.Net.IPAddress> result = new List<System.Net.IPAddress>();
            result.Add(System.Net.IPAddress.Broadcast);
            try
            {
                foreach (System.Net.NetworkInformation.NetworkInterface nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    foreach (System.Net.NetworkInformation.UnicastIPAddressInformation info in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (info.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                        byte[] ip = info.Address.GetAddressBytes();
                        byte[] mask = info.IPv4Mask.GetAddressBytes();
                        if (ip == null || mask == null || ip.Length != 4 || mask.Length != 4) continue;
                        byte[] broadcast = new byte[4];
                        for (int i = 0; i < 4; i++) broadcast[i] = (byte)(ip[i] | (~mask[i] & 0xFF));
                        result.Add(new System.Net.IPAddress(broadcast));
                    }
                }
            }
            catch { }
            return result;
        }
    }
}
