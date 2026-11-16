// ShieldGate - điểm vào: thiết lập lần đầu, giao diện, dòng lệnh, bảo vệ nền.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ShieldGate
{
    internal static class Program
    {
        private const string UiMutexName = AppInfo.UiMutexName;

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(int which);

        [STAThread]
        private static int Main(string[] args)
        {
            Bootstrap.Load();
            S.SetLanguage(Bootstrap.IsLanguage(Bootstrap.Language)
                ? Bootstrap.Language
                : Bootstrap.DefaultLanguage);
            ApplyCulture();

            string command = args.Length > 0 ? args[0].TrimStart('-').ToLowerInvariant() : "";

            // Giao diện và các lệnh cài đặt cần quyền quản trị. Tự nâng quyền thay vì
            // đặt requireAdministrator trong manifest: nếu manifest bắt buộc, Windows
            // khởi chạy lại qua UAC và làm mất stdout khi người dùng chuyển hướng lệnh
            // `ShieldGate.exe scan` ra tệp.
            if (!Native.IsAdmin() && NeedsAdmin(command))
            {
                if (!RelaunchElevated(args))
                {
                    if (command.Length > 0) Console.WriteLine(S.Cli.AdminRequired);
                    return 1;
                }
                return 0;
            }

            if (command.Length > 0)
            {
                // Chỉ gắn vào console của tiến trình cha khi chưa có stdout (không bị
                // chuyển hướng). Kiểm tra bằng handle thật của Win32.
                if (!StdOutIsRedirected()) AttachConsole();
                try { Console.OutputEncoding = Encoding.UTF8; } catch { }
                try { Console.Title = S.Cli.ConsoleTitle; } catch { }
            }

            try
            {
                switch (command)
                {
                    case "watch": return Guard.RunWatch();
                    case "tray": return TrayApp.Run();
                    case "install": return Install(args);
                    case "uninstall": return Uninstall();
                    case "status": return Status();
                    case "scan": return Scan(args);
                    case "selftest": return SelfTest.Run();
                    case "":
                        break;
                    default:
                        Console.WriteLine(S.Cli.UnknownArg);
                        Console.WriteLine(S.Cli.ScanUsage);
                        Console.WriteLine(S.Cli.Usage);
                        return 1;
                }
            }
            catch (Exception e)
            {
                try { Log.Write(S.Log.CommandLineError(e.ToString()), "ERROR"); } catch { }
                Console.WriteLine(S.Err.Unknown + " " + e.Message);
                return 1;
            }

            return RunGui();
        }

        private static void ApplyCulture()
        {
            try
            {
                CultureInfo c = CultureInfo.GetCultureInfo(S.English ? "en-US" : "vi-VN");
                Thread.CurrentThread.CurrentCulture = c;
                Thread.CurrentThread.CurrentUICulture = c;
            }
            catch { }
        }

        // ------------------------------------------------------------- giao diện

        private static int RunGui()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Phải đăng ký trước khi tạo cửa sổ đầu tiên (kể cả hộp thiết lập),
            // nếu không .NET ném InvalidOperationException và tiến trình thoát im lặng.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                ReportError(e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                ReportError(e.ExceptionObject as Exception);
            };

            bool created;
            using (Mutex mutex = new Mutex(true, UiMutexName, out created))
            {
                if (!created)
                {
                    MessageBox.Show(S.Cli.AlreadyRunning, S.App.Name,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                Ui.InitScale();

                if (!Bootstrap.HasSetup)
                {
                    using (SetupForm setup = new SetupForm())
                    {
                        if (setup.ShowDialog() != DialogResult.OK) return 0;
                        try
                        {
                            Bootstrap.Save(setup.SelectedHome, setup.SelectedLanguage);
                        }
                        catch
                        {
                            MessageBox.Show(S.Setup.SaveFailed, S.App.Name,
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return 1;
                        }
                    }
                    S.SetLanguage(Bootstrap.Language);
                    ApplyCulture();
                }

                Paths.Configure(string.IsNullOrEmpty(Bootstrap.Home) ? Bootstrap.DefaultHome : Bootstrap.Home);

                // Bản cài cũ có thể chưa có biểu tượng khay: tạo lại nếu bảo vệ đang chạy.
                try { Guard.EnsureTray(); } catch { }

                try
                {
                    Log.Write(S.Log.Open(AppInfo.Version, S.Language));
                    Application.Run(new MainForm());
                }
                catch (Exception e)
                {
                    ReportError(e);
                    return 1;
                }

                GC.KeepAlive(mutex);
                return 0;
            }
        }

        private static void ReportError(Exception e)
        {
            try { Log.Write(S.Log.UnhandledError(e == null ? "?" : e.ToString()), "ERROR"); }
            catch { }
            try
            {
                MessageBox.Show(S.Err.Unknown, S.App.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }

        // ------------------------------------------------------------ dòng lệnh

        private static void AttachConsole()
        {
            try { Native.AttachConsole(-1); }
            catch { }
        }

        // True khi stdout đã là tệp hoặc pipe do tiến trình gọi truyền vào.
        private static bool StdOutIsRedirected()
        {
            try
            {
                IntPtr h = GetStdHandle(-11);
                return h != IntPtr.Zero && h != new IntPtr(-1);
            }
            catch { return false; }
        }

        private static bool NeedsAdmin(string command)
        {
            switch (command)
            {
                case "": case "install": case "uninstall": case "watch": case "tray":
                    return true;
                default:
                    return false;
            }
        }

        private static bool RelaunchElevated(string[] args)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (string a in args)
                {
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append('"').Append(a.Replace("\"", "\\\"")).Append('"');
                }
                System.Diagnostics.ProcessStartInfo psi =
                    new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath);
                psi.Arguments = sb.ToString();
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                System.Diagnostics.Process.Start(psi);
                return true;
            }
            catch { return false; }
        }

        private static int Install(string[] args)
        {
            bool blockScript = false;
            foreach (string a in args)
            {
                string t = a.TrimStart('-').ToLowerInvariant();
                if (t == "blockscript" || t == "block-script") blockScript = true;
            }
            bool ok = Guard.Install(blockScript, Console.WriteLine);
            return ok ? 0 : 1;
        }

        private static int Uninstall()
        {
            return Guard.Uninstall(Console.WriteLine) ? 0 : 1;
        }

        private static int Status()
        {
            StatusInfo st = Guard.GetStatus();
            Console.WriteLine(S.Cli.StatusTitle + " " + AppInfo.Version);
            Console.WriteLine(S.Cli.StatusProtect(st.FullyProtected));
            Console.WriteLine(S.Cli.StatusAutorun(st.Autorun));
            Console.WriteLine(S.Cli.StatusIni(st.Ini));
            Console.WriteLine(S.Cli.StatusWsh(st.Wsh));
            Console.WriteLine(S.Cli.StatusGuard(st.Running));
            Console.WriteLine(S.Cli.StatusAdmin(st.Admin));
            Console.WriteLine(S.Cli.StatusQuarantine(st.QuarantineFiles, st.QuarantineBatches));
            return 0;
        }

        private static int Scan(string[] args)
        {
            if (args.Length < 2 || args[1].Trim().Length < 2)
            {
                Console.WriteLine(S.Cli.ScanUsage);
                return 1;
            }

            string root = args[1].Trim();
            try { root = System.IO.Path.GetFullPath(root); }
            catch { }
            if (!root.EndsWith("\\")) root += "\\";

            UsbScanResult result = UsbScan.Detect(root, null, null);
            Console.WriteLine(S.Cli.ScanSummary(root, result.Files, result.Items.Count, result.Seconds));
            if (result.Truncated) Console.WriteLine(S.Cli.ScanTruncated(result.Files));
            if (result.Items.Count == 0)
            {
                Console.WriteLine(S.Cli.ScanClean);
                return 0;
            }

            foreach (UsbItem item in result.Items)
                Console.WriteLine(S.Cli.ScanItem(item.Rel, item.ReasonText));
            // Mã 2: tìm thấy mục cần xử lý, khác với lỗi chạy (1).
            return 2;
        }
    }
}
