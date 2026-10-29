using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using LinkDrop.UI;

namespace LinkDrop
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern IntPtr GetThreadDpiAwarenessContext();

        [DllImport("user32.dll")]
        private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        [STAThread]
        private static void Main(string[] args)
        {
            bool dpiSet = false;
            try { dpiSet = SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
            try
            {
                int awareness = GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext());
                Log.Info("DPI opt-in: set=" + dpiSet + " awareness=" + awareness + " systemDpi=" + GetDpiForSystem());
            }
            catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                Log.Error("UI 线程异常", e.Exception);
                MessageBox.Show("轻传遇到了一个问题：\n" + e.Exception.Message, "轻传 LinkDrop", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                Log.Error("未处理异常", ex);
            };

            bool background = false;
            foreach (string arg in args)
            {
                if (string.Equals(arg, "--background", StringComparison.OrdinalIgnoreCase)) background = true;
            }

            bool created;
            using (Mutex mutex = new Mutex(true, "QingChuan.LinkDrop.SingleInstance", out created))
            {
                if (!created)
                {
                    IntPtr existing = NativeMethods.FindWindow(null, "轻传 LinkDrop");
                    if (existing != IntPtr.Zero)
                    {
                        NativeMethods.ShowWindow(existing, 9);
                        NativeMethods.SetForegroundWindow(existing);
                    }
                    else
                    {
                        MessageBox.Show("轻传已经在运行。", "轻传 LinkDrop", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }
                Application.Run(new MainForm(background));
                GC.KeepAlive(mutex);
            }
        }
    }
}
