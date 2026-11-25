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
            string lang = Bootstrap.Language;
            if (!Bootstrap.IsLanguage(lang))
            {
                // Tác vụ bảo vệ chạy bằng SYSTEM không có setup.ini theo người dùng,
                // nên đọc ngôn ngữ đã lưu chung để nhật ký không lẫn tiếng Anh.
                try { lang = Settings.Get("language", ""); }
                catch { }
            }
            S.SetLanguage(Bootstrap.IsLanguage(lang)
                ? lang
                : Bootstrap.DefaultLanguage);
            ApplyCulture();

            string command = args.Length > 0 ? args[0].TrimStart('-').ToLowerInvariant() : "";

            // Giao diện và các lệnh cài đặt cần quyền quản trị. Tự nâng quyền thay vì
            // đặt requireAdministrator trong manifest: nếu manifest bắt buộc, Windows
            // khởi chạy lại qua UAC và làm mất stdout khi người dùng chuyển hướng lệnh
            // `ShieldGate.exe scan` ra tệp.
            if (!Native.IsAdmin() && NeedsAdmin(command, args))
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
                    case "uninstall": return Uninstall(args);
                    case "status": return Status();
                    case "pipe": return PipeCommand(args);
                    case "scan": return Scan(args);
                    case "devices": return DevicesCommand();
                    case "events": return EventsCommand(args);
                    case "repair": return RepairCommand(args);
                    case "defender": return DefenderCommand(args);
                    case "vt": return VtCommand(args);
                    case "help":
                    case "?": return Help();
                    case "version": return VersionCommand();
                    case "import-rules":
                    case "importrules": return ImportRules(args);
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

                // Ghi ngôn ngữ vào thư mục dữ liệu chung (settings.txt) để tiến trình
                // bảo vệ nền dùng lại; setup.ini chỉ có trong hồ sơ người dùng.
                try
                {
                    if (Bootstrap.IsLanguage(S.Language)
                        && !string.Equals(S.Language, Settings.Get("language", null),
                                          StringComparison.OrdinalIgnoreCase))
                        Settings.Set("language", S.Language);
                }
                catch { }

                // Bản cài cũ có thể chưa có biểu tượng khay: tạo lại nếu bảo vệ đang chạy.
                try { Guard.EnsureTray(); } catch { }

                try
                {
                    // Khoá ghi có thể còn tắt do lần chạy trước bị kill giữa lúc
                    // cách ly; trả lại trạng thái trước khi mở giao diện.
                    try { Policy.RecoverPending(); } catch { }
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

        private static bool NeedsAdmin(string command, string[] args)
        {
            switch (command)
            {
                case "": case "install": case "uninstall": case "watch": case "tray":
                case "import-rules": case "importrules":
                    return true;
                case "pipe":
                    // Đọc trạng thái và giá trị gõ sai không cần quyền: tránh bật
                    // hộp UAC chỉ vì một lệnh không hợp lệ.
                    return IsPipeWrite(args);
                default:
                    return false;
            }
        }

        private static bool IsPipeWrite(string[] args)
        {
            if (args.Length < 2) return false;
            string value = args[1].Trim().TrimStart('-');
            return string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase);
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

        private static int Uninstall(string[] args)
        {
            // Mac dinh giu cach ly va ban sao luu o; --purge-data de xoa het.
            bool keep = true;
            foreach (string a in args)
            {
                string t = a.TrimStart('-').ToLowerInvariant();
                if (t == "purge-data" || t == "purge") keep = false;
                if (t == "keep-data" || t == "keep") keep = true;
            }

            bool clean;
            bool ok = Uninstaller.Run(keep, out clean);
            if (!ok) return 1;
            if (!clean) Console.WriteLine(S.Cli.UninstallPending);
            else if (keep) Console.WriteLine(S.Cli.UninstallKept);
            else Console.WriteLine(S.Cli.UninstallDone);
            return 0;
        }

        private static int Status()
        {
            StatusInfo st = Guard.GetStatus();
            Console.WriteLine(S.Cli.StatusTitle + " " + AppInfo.Version);
            Console.WriteLine(S.Cli.StatusProtect(st.ProtectionOn));
            Console.WriteLine(S.Cli.StatusAutorun(st.Autorun));
            Console.WriteLine(S.Cli.StatusIni(st.Ini));
            Console.WriteLine(S.Cli.StatusWsh(st.Wsh));
            Console.WriteLine(S.Cli.StatusRegistry(st.Registry));
            Console.WriteLine(S.Cli.StatusGuard(st.Running));
            Console.WriteLine(S.Cli.StatusWriteProtect(st.WriteProtect));
            Console.WriteLine(S.Cli.StatusBlockExec(st.BlockExec));
            Console.WriteLine(S.Cli.StatusAdmin(st.Admin));
            Console.WriteLine(S.Cli.StatusQuarantine(st.QuarantineFiles, st.QuarantineBatches));
            return 0;
        }

        private static int PipeCommand(string[] args)
        {
            string value = args.Length > 1 ? args[1].Trim().TrimStart('-').ToLowerInvariant() : "status";
            if (value.Length == 0) value = "status";
            if (value != "on" && value != "off" && value != "status")
            {
                Console.WriteLine(S.Cli.PipeBadArg);
                Console.WriteLine(S.Cli.PipeUsage);
                return 1;
            }

            if (value == "status")
            {
                StatusInfo st = Guard.GetStatus();
                Console.WriteLine(S.Cli.PipeStatus(Settings.PipeEnabled, st.Running, PipePinned()));
                if (Settings.PipeEnabled && !PipePinned()) Console.WriteLine(S.Cli.PipeNoPin);
                return 0;
            }

            // NeedsAdmin đã nâng quyền trước khi vào đây; kiểm tra lại để lệnh
            // không ghi settings.txt nếu bị gọi trực tiếp từ mã khác.
            if (!Native.IsAdmin())
            {
                Console.WriteLine(S.Cli.AdminRequired);
                return 1;
            }

            bool on = value == "on";
            Settings.SetPipeEnabled(on);
            if (Settings.PipeEnabled != on)
            {
                Console.WriteLine(S.Cli.PipeSetFailed);
                return 1;
            }
            Console.WriteLine(on ? S.Cli.PipeOn : S.Cli.PipeOff);
            if (on && !PipePinned()) Console.WriteLine(S.Cli.PipeNoPin);
            return 0;
        }

        private static bool PipePinned()
        {
            try
            {
                string path, hash;
                return PipeAuth.ReadPin(out path, out hash);
            }
            catch { return false; }
        }

        private static int Scan(string[] args)
        {
            bool json = false;
            string root = "";
            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i].Trim();
                if (string.Equals(a.TrimStart('-'), "json", StringComparison.OrdinalIgnoreCase))
                {
                    json = true;
                    continue;
                }
                if (root.Length == 0) root = a;
            }
            if (root.Trim().Length < 2)
            {
                Console.WriteLine(S.Cli.ScanUsage);
                return 1;
            }

            try { root = System.IO.Path.GetFullPath(root); }
            catch { }
            if (!root.EndsWith("\\")) root += "\\";

            UsbScanResult result = UsbScan.Detect(root, null, null);
            if (json)
            {
                Console.Write(ScanJson.Text(root, result));
                return result.Items.Count == 0 ? 0 : 2;
            }

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

        // ------------------------------------------------------- lệnh VirusTotal

        private static int VtCommand(string[] args)
        {
            if (args.Length < 2 || args[1].Trim().Length == 0)
            {
                Console.WriteLine(S.Cli.VtUsage);
                return 1;
            }
            string path = args[1].Trim();
            if (!File.Exists(path))
            {
                Console.WriteLine(S.Cli.VtMissing(path));
                return 1;
            }
            if (!Settings.VirusTotalEnabled)
            {
                Console.WriteLine(S.Cli.VtOff);
                return 1;
            }

            // Chi gui hash; khong gui ten/duong dan. Khong gioi han kich thuoc vi
            // chi doc de bam, khong tai len.
            string hash = Native.Sha256(path, 0);
            if (hash.Length == 0)
            {
                Console.WriteLine(S.Cli.VtHashFailed(path));
                return 1;
            }
            Console.WriteLine(S.Cli.VtHash(hash));

            VtResult r = VirusTotal.Query(hash);
            if (!r.Ran || !r.Ok)
            {
                Console.WriteLine(r.Error);
                return 1;
            }
            Console.WriteLine(S.Cli.VtResult(r.Malicious, r.Suspicious, r.Total));
            return r.Clean ? 0 : 2;
        }

        private static int Help()
        {
            Console.WriteLine(S.Cli.Help);
            return 0;
        }

        private static int VersionCommand()
        {
            Console.WriteLine(AppInfo.Name + " " + AppInfo.Version);
            return 0;
        }

        // ------------------------------------------------------- lệnh ngoại vi

        private static int DevicesCommand()
        {
            DeviceScanResult result = Devices.Scan();
            Console.WriteLine(S.Cli.DevicesTitle);
            if (result.Devices.Count == 0) Console.WriteLine(S.Cli.NoDevices);
            foreach (DeviceInfo d in result.Devices)
            {
                Console.WriteLine(S.Cli.DeviceLine(d.Name, S.Devices.TypeName(d.DeviceClass),
                    S.Devices.TrustName(d.Trust), string.Join(" ", d.Drives.ToArray())));
            }
            if (result.Error.Length > 0)
            {
                Console.WriteLine(result.Error);
                return 1;
            }
            return 0;
        }

        private static int EventsCommand(string[] args)
        {
            int max = 40;
            if (args.Length > 1)
            {
                int parsed;
                if (int.TryParse(args[1], out parsed) && parsed > 0) max = Math.Min(parsed, 2000);
            }
            List<EventEntry> entries = ShieldGate.Events.Read(max);
            Console.WriteLine(S.Cli.EventsTitle);
            if (entries.Count == 0) Console.WriteLine(S.Cli.NoEvents);
            foreach (EventEntry e in entries)
            {
                Console.WriteLine(S.Cli.EventLine(e.TimeText, e.Kind, e.Severity,
                                                 e.Subject, e.Detail));
            }
            return 0;
        }

        // Chỉ chẩn đoán và in thông tin. Dòng lệnh không bao giờ sao lưu, không chạy
        // chkdsk và không ghi lên ổ; các thao tác đó chỉ có trong giao diện.
        private static int RepairCommand(string[] args)
        {
            if (args.Length < 2 || args[1].Trim().Length < 2)
            {
                Console.WriteLine(S.Cli.RepairUsage);
                return 1;
            }
            RepairInfo info = Repair.Inspect(args[1]);
            Console.WriteLine(S.Cli.RepairSummary(info.Drive, info.FileSystem, info.FreeBytes,
                                                  info.TotalBytes, info.ReadOnly, info.SystemDrive));
            if (info.DirtyKnown)
            {
                Console.WriteLine(S.Repair.Info(S.Repair.InfoStatus,
                    info.Dirty ? S.Repair.DirtyLabel : S.Repair.CleanLabel));
            }
            if (info.Problem.Length > 0) Console.WriteLine(S.Cli.RepairProblem(info.Problem));
            return info.Problem.Length > 0 ? 1 : 0;
        }

        private static int ImportRules(string[] args)
        {
            string path = args.Length > 1 ? args[1].Trim() : "";
            bool confirmed = false;
            for (int i = 2; i < args.Length; i++)
            {
                string t = args[i].TrimStart('-').ToLowerInvariant();
                if (t == "yes" || t == "y") confirmed = true;
            }
            if (path.Length == 0)
            {
                Console.WriteLine(S.Cli.ImportRulesUsage);
                return 1;
            }
            if (!confirmed)
            {
                // Ghi đè tệp luật là thao tác phá huỷ: bắt buộc --yes.
                Console.WriteLine(S.Cli.ImportRulesConfirm(path));
                Console.WriteLine(S.Cli.ImportRulesUsage);
                return 1;
            }
            string error;
            if (!Rules.Import(path, out error))
            {
                Console.WriteLine(error);
                return 1;
            }
            Console.WriteLine(S.Cli.ImportRulesDone(Paths.RulesFile));
            return 0;
        }

        private static int DefenderCommand(string[] args)
        {
            if (args.Length < 2 || args[1].Trim().Length == 0)
            {
                Console.WriteLine(S.Cli.DefenderUsage);
                return 1;
            }
            string path = args[1].Trim();
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                Console.WriteLine(S.Cli.DefenderResult(false, -1));
                return 1;
            }
            DefenderResult result = Defender.Scan(path, 30 * 60 * 1000);
            if (!result.Ran)
            {
                Console.WriteLine(result.Error.Length > 0 ? result.Error : S.Err.Unknown);
                return 1;
            }
            Console.WriteLine(S.Cli.DefenderResult(result.Clean, result.ExitCode));
            return result.Clean ? 0 : 2;
        }
    }
}
