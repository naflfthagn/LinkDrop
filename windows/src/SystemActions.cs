using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace LinkDrop
{
    internal static class SystemActions
    {
        public static void OpenFolder(string directory)
        {
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + directory + "\"") { UseShellExecute = true });
        }

        public static void OpenPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        public static bool ConfirmAndAddFirewallRule(IWin32Window owner)
        {
            if (MessageBox.Show(owner,
                "Windows 防火墙可能会拦截手机连接。\n\n接下来会请求管理员权限，为轻传添加“专用/域网络”的入站放行规则。是否继续？",
                "修复 Windows 防火墙", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
                return false;
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
                        {
                            MessageBox.Show(owner, "已添加防火墙放行规则。", "轻传 LinkDrop", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return true;
                        }
                    }
                }
                MessageBox.Show(owner, "防火墙规则添加失败，请手动允许轻传通过专用网络。", "轻传 LinkDrop", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                    MessageBox.Show(owner, "已取消管理员授权。", "轻传 LinkDrop", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show(owner, "无法修改防火墙：" + ex.Message, "轻传 LinkDrop", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "无法修改防火墙：" + ex.Message, "轻传 LinkDrop", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
    }
}
