// ShieldGate - loi: duong dan, win32, nhat ky, vung cach ly.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace ShieldGate
{
    internal static class AppInfo
    {
        public const string Name = "ShieldGate";
        public const string Version = "3.3.1";
        // Địa chỉ công khai của kho mã nguồn; nút GitHub ở trang Giới thiệu dùng.
        public const string ProjectUrl = "https://github.com/GnasValluk/shieldgate";
        public const string TaskName = "ShieldGate USB Guard";
        public const string TrayTaskName = "ShieldGate Tray";
        // Giữ hậu tố _2 khi lên 3.0: bản 2.3 và 3.0 dùng chung một mutex để lúc
        // nâng cấp không có hai tiến trình nền cùng chạy.
        public const string MutexName = "Global\\ShieldGate_USBGuard_2";
        public const string UiMutexName = "Local\\ShieldGate_UI_2";
        public const string TrayMutexName = "Local\\ShieldGate_Tray_2";
    }

    internal static class Paths
    {
        public static string Root;
        public static string LogDir;
        public static string QuarDir;
        public static string BakDir;
        public static string BinDir;
        public static string LogFile;
        public static string Heartbeat;
        public static string StopFlag;
        public static string GuardExe;
        public static string GuardHash;
        public static string SettingsFile;
        public static string PolicyBackup;
        public static string LastScan;
        public static string GuardConfig;
        public static string GuardLast;
        public static string ClientPin;
        public static string SystemDrive;
        public static string RulesFile;
        public static string DevicesFile;
        public static string RegViewPrefix;
        public static string EventsFile;
        public static string RepairDir;
        public static string VtKeyFile;

        static Paths()
        {
            SystemDrive = (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:") + "\\";
            Configure(ChooseRoot());
        }

        private static string ChooseRoot()
        {
            string env = Environment.GetEnvironmentVariable("SHIELDGATE_HOME");
            if (!string.IsNullOrEmpty(env)) return env;

            // Bản chạy nền được cài ở <dữ liệu>\bin\ShieldGate.exe và có thể chạy bằng SYSTEM,
            // nên tự nhận thư mục dữ liệu từ vị trí tệp thay vì hồ sơ người dùng.
            try
            {
                string exe = Assembly.GetEntryAssembly().Location;
                string dir = Path.GetDirectoryName(exe);
                if (dir != null
                    && string.Equals(Path.GetFileName(exe), "ShieldGate.exe", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Path.GetFileName(dir), "bin", StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(Path.Combine(Path.GetDirectoryName(dir), "quarantine")))
                    return Path.GetDirectoryName(dir);
            }
            catch { }

            Bootstrap.Load();
            if (!string.IsNullOrEmpty(Bootstrap.Home)) return Bootstrap.Home;
            return Bootstrap.DefaultHome;
        }

        public static void Configure(string root)
        {
            Root = string.IsNullOrEmpty(root) ? Bootstrap.DefaultHome : root;
            LogDir = Path.Combine(Root, "log");
            QuarDir = Path.Combine(Root, "quarantine");
            BakDir = Path.Combine(Root, "backup");
            BinDir = Path.Combine(Root, "bin");
            LogFile = Path.Combine(LogDir, "shieldgate.log");
            Heartbeat = Path.Combine(Root, "guard.heartbeat");
            StopFlag = Path.Combine(Root, "stop.flag");
            GuardExe = Path.Combine(BinDir, "ShieldGate.exe");
            GuardHash = Path.Combine(BinDir, "ShieldGate.sha256");
            SettingsFile = Path.Combine(Root, "settings.txt");
            PolicyBackup = Path.Combine(BakDir, "immunize.txt");
            LastScan = Path.Combine(Root, "last_scan.txt");
            GuardConfig = Path.Combine(Root, "guard.cfg");
            // Lựa chọn gần nhất trước khi người dùng tắt hết bảo vệ: guard.cfg
            // phải nói đúng trạng thái hiện tại (toàn 0) để công tắc không tự
            // bật lại, còn tệp này giữ ý định cũ cho lần "Bật bảo vệ" sau.
            GuardLast = Path.Combine(Root, "guard.last");
            // Pin client cho named pipe: đường dẫn exe + SHA-256 do --install ghi.
            ClientPin = Path.Combine(BinDir, "client.pin");
            RulesFile = Path.Combine(Root, "rules.json");
            DevicesFile = Path.Combine(Root, "devices.json");
            // Ảnh chụp cờ xem tệp của Explorer, tách theo SID người dùng. Chỉ ghi
            // từ tiến trình trong phiên người dùng, không ghi từ SYSTEM.
            RegViewPrefix = Path.Combine(Root, "regview.");
            EventsFile = Path.Combine(Root, "events.tsv");
            RepairDir = Path.Combine(Root, "repair");
            // Khoa VirusTotal luu bang DPAPI (CurrentUser); khong nam trong settings.txt.
            VtKeyFile = Path.Combine(Root, "vt.key");
            EnsureDirs();
        }

        private static bool _hardened;

        // Thư mục dữ liệu chứa bản sao lưu cấu hình, tệp tác vụ và tệp cách ly.
        // Nếu người dùng thường tạo được thư mục này trước, họ có thể thay nội dung
        // mà chương trình đọc bằng quyền quản trị. Khoá lại: chỉ SYSTEM và quản trị ghi.
        public static void HardenRoot()
        {
            if (_hardened) return;
            _hardened = true;
            if (!Native.IsAdmin()) return;
            try
            {
                EnsureDirs();
                string ignored;
                Grant(Root, true, out ignored);
                Grant(BinDir, false, out ignored);
                Grant(BakDir, false, out ignored);
                // Tệp cách ly là mẫu virus sống: người dùng thường không cần đọc.
                Grant(QuarDir, false, out ignored);
                // Bản sao lưu ổ và marker chkdsk không được để người dùng thường sửa.
                Grant(RepairDir, false, out ignored);
            }
            catch { }
        }

        private static void Grant(string dir, bool usersRead, out string output)
        {
            // /reset trước để xoá ACE cũ (bản 7.x từng cấp Users quyền đọc bin).
            Native.Run(Native.SysExe("icacls.exe"), "\"" + dir + "\" /setowner \"*S-1-5-32-544\" /reset",
                       20000, out output);
            string args = "\"" + dir + "\" /setowner \"*S-1-5-32-544\" /inheritance:r " +
                          "/grant:r \"*S-1-5-18:(OI)(CI)F\" \"*S-1-5-32-544:(OI)(CI)F\"";
            if (usersRead) args += " \"*S-1-5-32-545:(OI)(CI)RX\"";
            else args += " /remove:g \"*S-1-5-32-545\"";
            Native.Run(Native.SysExe("icacls.exe"), args, 20000, out output);
        }

        public static void EnsureDirs()
        {
            foreach (string d in new[] { Root, LogDir, QuarDir, BakDir, BinDir, RepairDir })
            {
                try
                {
                    Directory.CreateDirectory(d);
                    string marker = MarkerIn(d);
                    if (marker.Length > 0 && !File.Exists(marker))
                        File.WriteAllText(marker, MarkerText(), new UTF8Encoding(false));
                }
                catch { }
            }
        }

        // Tep danh dau "thu muc nay do ShieldGate tao". Bo go cai dat chi xoa cay
        // con co marker (hoac khop chu ky tep ben trong), nho vay thu muc nguoi
        // dung chon lam noi luu du lieu khong bi xoa nham.
        public const string OwnerMarker = ".shieldgate";

        public static string MarkerIn(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return "";
            try { return Path.Combine(dir, OwnerMarker); }
            catch { return ""; }
        }

        private static string MarkerText()
        {
            return "app=" + AppInfo.Name + "\r\n"
                 + "version=" + AppInfo.Version + "\r\n"
                 + "created=" + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                 + "\r\n";
        }
    }

    internal static class Log
    {
        private static readonly object Gate = new object();

        public static void Write(string message, string level = "INFO")
        {
            try
            {
                lock (Gate)
                {
                    Paths.EnsureDirs();
                    try
                    {
                        FileInfo fi = new FileInfo(Paths.LogFile);
                        // Cắt tệp sau khi sao lưu, nếu không tệp phình mãi và
                        // mỗi dòng sau mốc 5 MB lại copy toàn bộ tệp một lần.
                        if (fi.Exists && fi.Length > 5 * 1024 * 1024)
                        {
                            try { File.Copy(Paths.LogFile, Paths.LogFile + ".1", true); } catch { }
                            try
                            {
                                using (FileStream cut = new FileStream(Paths.LogFile, FileMode.Truncate,
                                                                       FileAccess.Write, FileShare.ReadWrite))
                                { }
                            }
                            catch { }
                        }
                    }
                    catch { }
                    string line = string.Format("{0} [{1}] {2}\r\n",
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), level, message);
                    // Cho tiến trình khác (giao diện, bảo vệ nền) cùng ghi.
                    using (FileStream fs = new FileStream(Paths.LogFile, FileMode.Append,
                                                          FileAccess.Write, FileShare.ReadWrite))
                    using (StreamWriter w = new StreamWriter(fs, new UTF8Encoding(true)))
                        w.Write(line);
                }
            }
            catch { }
        }
    }

    internal static class Native
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetLogicalDrives();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetDriveTypeW(string root);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool GetVolumeInformationW(string root, StringBuilder label, int labelSize,
                                                         out uint serial, out uint maxComponent,
                                                         out uint flags, StringBuilder fileSystem, int fsSize);

        private const uint FileReadOnlyVolume = 0x00080000;

        [DllImport("kernel32.dll")]
        public static extern bool AttachConsole(int processId);

        private const uint ProcessQueryLimitedInformation = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags,
                                                              StringBuilder exeName, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        // Doc duong dan tep chay cua tien trinh khac. Khong dung
        // Process.MainModule: tien trinh 32-bit khong doc duoc MainModule cua
        // tien trinh 64-bit. Tra ve chuoi rong khi khong mo duoc (da thoat,
        // thieu quyen, PID he thong) de nguoi goi bo qua thay vi nem loi.
        public static string ProcessImagePath(int processId)
        {
            if (processId <= 0) return "";
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
                if (handle == IntPtr.Zero) return "";
                int size = 1024;
                StringBuilder sb = new StringBuilder(size);
                if (!QueryFullProcessImageNameW(handle, 0, sb, ref size)) return "";
                if (size <= 0 || size > sb.Length) return "";
                return sb.ToString(0, size);
            }
            catch { return ""; }
            finally
            {
                if (handle != IntPtr.Zero) { try { CloseHandle(handle); } catch { } }
            }
        }

        public static bool IsAdmin()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal p = new WindowsPrincipal(id);
                    return p.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        public static bool ClearHidden(string path)
        {
            try
            {
                FileAttributes a = File.GetAttributes(path);
                if ((a & (FileAttributes.Hidden | FileAttributes.System)) == 0) return false;
                FileAttributes n = a & ~(FileAttributes.Hidden | FileAttributes.System | FileAttributes.Directory);
                if (n == 0) n = FileAttributes.Normal;
                File.SetAttributes(path, n);
                return true;
            }
            catch { return false; }
        }

        public static List<string> RemovableDrives()
        {
            return Drives(DriveType.Removable);
        }

        // O duoc gan o che do chi doc: USB co khoa ghi, dia CD/DVD, anh ISO...
        public static bool IsReadOnlyVolume(string root)
        {
            try
            {
                uint serial, maxComponent, flags;
                StringBuilder label = new StringBuilder(8), fs = new StringBuilder(8);
                if (!GetVolumeInformationW(root, label, 8, out serial, out maxComponent,
                                           out flags, fs, 8))
                    return false;
                return (flags & FileReadOnlyVolume) != 0;
            }
            catch { return false; }
        }

        // ADS chi ton tai tren NTFS. Goi mot lan cho moi o quet, khong phai moi tep.
        public static bool IsNtfsVolume(string root)
        {
            try
            {
                uint serial, maxComponent, flags;
                StringBuilder label = new StringBuilder(8), fs = new StringBuilder(16);
                if (!GetVolumeInformationW(root, label, 8, out serial, out maxComponent,
                                           out flags, fs, 16))
                    return false;
                return string.Equals(fs.ToString(), "NTFS", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        // Serial cua volume (khong phai chu o). Marker sao luu phai gan voi serial
        // de USB khac cam vao cung chu E: khong duoc dung lai ban sao luu cu.
        public static uint VolumeSerial(string root)
        {
            try
            {
                uint serial, maxComponent, flags;
                StringBuilder label = new StringBuilder(8), fs = new StringBuilder(8);
                if (!GetVolumeInformationW(root, label, 8, out serial, out maxComponent,
                                           out flags, fs, 8))
                    return 0;
                return serial;
            }
            catch { return 0; }
        }

        // CreateProcess tim tep trong thu muc chua ShieldGate truoc. Goi ten tran
        // khi chay tu USB (hoac thu muc Downloads) co the chay exe gia voi quyen
        // admin, nen moi cong cu he thong phai di qua day.
        //
        // Thu tu: Sysnative (chi co khi tien trinh 32-bit tren Windows 64-bit, tro
        // thang vao System32 that), SystemDirectory (SysWOW64 khi chay x86),
        // System32 (dung khi tien trinh 64-bit), roi WindowsPowerShell\v1.0 vi
        // powershell.exe khong nam trong System32.
        public static string SysExe(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            try
            {
                if (name.IndexOf('\\') >= 0 || name.IndexOf('/') >= 0) return name;
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string[] dirs = new string[]
                {
                    Path.Combine(win, "Sysnative"),
                    Environment.SystemDirectory,
                    Path.Combine(win, "System32"),
                    Path.Combine(win, "System32\\WindowsPowerShell\\v1.0")
                };
                foreach (string dir in dirs)
                {
                    if (string.IsNullOrEmpty(dir)) continue;
                    string full = Path.Combine(dir, name);
                    if (File.Exists(full)) return full;
                }
            }
            catch { }
            return name;
        }

        // explorer.exe nam o %WINDIR%, khong phai System32.
        public static string ShellExe(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            try
            {
                if (name.IndexOf('\\') >= 0) return name;
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string full = Path.Combine(win, name);
                if (File.Exists(full)) return full;
            }
            catch { }
            return name;
        }

        // Lop phong thu thu hai: du mot cho goi con quen, ten tran van duoc
        // phan giai truoc khi CreateProcess tim trong thu muc hien tai.
        private static string ResolveTool(string file)
        {
            if (string.IsNullOrEmpty(file)) return file;
            if (file.IndexOf('\\') >= 0 || file.IndexOf('/') >= 0) return file;
            return SysExe(file);
        }

        // Thu ghi that mot tep tam roi xoa ngay. Dung truoc khi di chuyen / cach ly
        // de o chi doc hoac o khong co quyen ghi chi bao mot loi thay vi hang loat.
        public static bool CanWrite(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, ".shieldgate-write-test");
                using (FileStream fs = new FileStream(probe, FileMode.Create, FileAccess.Write,
                                                      FileShare.None, 1, FileOptions.DeleteOnClose))
                    fs.WriteByte(0);
                return true;
            }
            catch { return false; }
        }

        private static List<string> Drives(DriveType type)
        {
            List<string> outList = new List<string>();
            try
            {
                foreach (DriveInfo d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (d.DriveType != type || !d.IsReady) continue;
                        if (string.Equals(d.RootDirectory.FullName, Paths.SystemDrive,
                                          StringComparison.OrdinalIgnoreCase)) continue;
                        outList.Add(d.RootDirectory.FullName);
                    }
                    catch { }
                }
            }
            catch { }
            return outList;
        }

        public static int Run(string file, string args, int timeoutMs, out string output)
        {
            return Run(file, args, timeoutMs, null, out output);
        }

        // stdin != null: ghi san cau tra loi roi dong stdin. Dung cho chkdsk /f,
        // lenh hoi "force a dismount? (Y/N)" va se cho mai neu khong ai tra loi.
        public static int Run(string file, string args, int timeoutMs, string stdin,
                              out string output)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(ResolveTool(file), args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.RedirectStandardInput = stdin != null;
                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                    { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                    { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (stdin != null)
                    {
                        try
                        {
                            p.StandardInput.Write(stdin);
                            p.StandardInput.Close();
                        }
                        catch { }
                    }
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        // Không chờ vô hạn: nếu Kill bị từ chối (AV, tiến trình SYSTEM)
                        // thì trả về lỗi thay vì treo giao diện.
                        try { p.WaitForExit(2000); } catch { }
                    }
                    output = sb.ToString();
                    return p.HasExited ? p.ExitCode : -1;
                }
            }
            catch (Exception e)
            {
                output = e.Message;
                return -1;
            }
        }

        public static string Sha256(string path)
        {
            return Sha256(path, 10 * 1024 * 1024);
        }

        // maxBytes <= 0: khong gioi han (dung cho --vt; chi can hash, khong doc
        // noi dung). Cac call site cu van giu gioi han 10 MB.
        public static string Sha256(string path, long maxBytes)
        {
            try
            {
                // Mo bang handle de doc duoc ca duong dan dai hon 260 ky tu.
                using (SHA256 sha = SHA256.Create())
                using (FileStream fs = WinPath.OpenRead(WinPath.Ext(path)))
                {
                    if (maxBytes > 0 && fs.Length > maxBytes) return "";
                    byte[] hash = sha.ComputeHash(fs);
                    StringBuilder sb = new StringBuilder(hash.Length * 2);
                    foreach (byte b in hash) sb.Append(b.ToString("X2"));
                    return sb.ToString();
                }
            }
            catch { return ""; }
        }
    }

    internal sealed class TreeItem
    {
        public string Path;
        public string Name;
        public FileAttributes Attrs;
        public bool IsDir;
        public int Depth;
        public string Rel;

        // Chỉ đọc khi detector cần; không gọi FileInfo cho mọi tệp trong lúc walk.
        public long Size
        {
            get
            {
                try { return new FileInfo(Path).Length; }
                catch { return -1; }
            }
        }
    }

    internal sealed class WalkStats
    {
        public bool Truncated;
    }

    internal static class Tree
    {
        public static readonly HashSet<string> SkipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "$RECYCLE.BIN", "System Volume Information", ".Trashes",
            ".Spotlight-V100", ".fseventsd"
        };

        public static IEnumerable<TreeItem> Walk(string root, WalkStats stats = null,
                                                 int maxFiles = 150000, int maxSeconds = 300,
                                                 int maxDepth = int.MaxValue)
        {
            DateTime start = DateTime.UtcNow;
            Stack<KeyValuePair<string, int>> stack = new Stack<KeyValuePair<string, int>>();
            stack.Push(new KeyValuePair<string, int>(root, 0));
            int count = 0;
            while (stack.Count > 0)
            {
                if ((DateTime.UtcNow - start).TotalSeconds > maxSeconds)
                {
                    if (stats != null) stats.Truncated = true;
                    yield break;
                }
                KeyValuePair<string, int> cur = stack.Pop();
                // WinPath.List doc duoc ca cay dai hon 260 ky tu; System.IO thi
                // nem PathTooLongException va ca nhanh thu muc bi bo qua.
                List<DirEntry> entries;
                try { entries = WinPath.List(WinPath.Ext(cur.Key)); }
                catch { continue; }
                foreach (DirEntry e in entries)
                {
                    if (++count > maxFiles)
                    {
                        if (stats != null) stats.Truncated = true;
                        yield break;
                    }
                    string name = e.Name;
                    if (SkipNames.Contains(name)) continue;
                    FileAttributes attrs = (FileAttributes)e.Attr;
                    if ((attrs & FileAttributes.ReparsePoint) != 0) continue;
                    bool isDir = (attrs & FileAttributes.Directory) != 0;
                    string full = WinPath.Join(cur.Key, name);
                    TreeItem item = new TreeItem();
                    item.Path = full;
                    item.Name = name;
                    item.Attrs = attrs;
                    item.IsDir = isDir;
                    item.Depth = cur.Value;
                    item.Rel = full.Substring(root.Length).TrimStart('\\');
                    if (item.Rel.Length == 0) item.Rel = name;
                    yield return item;
                    if (isDir && cur.Value < maxDepth) stack.Push(new KeyValuePair<string, int>(full, cur.Value + 1));
                }
            }
        }
    }

    internal sealed class BatchInfo
    {
        public string Name;
        public string Path;
        public int Count;
        public long Size;
        public string WhenText = "";
        public bool FromUsb;
    }

    internal static class Quarantine
    {
        public static string NewBatch(string prefix, string note)
        {
            Paths.EnsureDirs();
            Paths.HardenRoot();
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string dir = System.IO.Path.Combine(Paths.QuarDir, prefix + "_" + stamp);
            int i = 1;
            while (Directory.Exists(dir))
            {
                dir = System.IO.Path.Combine(Paths.QuarDir, prefix + "_" + stamp + "_" + i);
                i++;
            }
            Directory.CreateDirectory(System.IO.Path.Combine(dir, "files"));
            WriteText(System.IO.Path.Combine(dir, "manifest.csv"),
                      "Time,OriginalPath,QuarantinePath,Reason,Size,SHA256\r\n");
            if (!string.IsNullOrEmpty(note))
                WriteText(System.IO.Path.Combine(dir, "note.txt"), note + "\r\n");
            return dir;
        }

        private static void WriteText(string path, string text)
        {
            File.WriteAllText(path, text, new UTF8Encoding(true));
        }

        public static string Csv(string s)
        {
            return "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        }

        public static bool Add(string path, string root, string batch, string reason, out string error)
        {
            error = "";
            string rel;
            try { rel = path.Substring(root.Length).TrimStart('\\'); }
            catch { rel = System.IO.Path.GetFileName(path); }
            if (string.IsNullOrEmpty(rel)) rel = System.IO.Path.GetFileName(path);

            string from = WinPath.Ext(path);
            if (!WinPath.Exists(from))
            {
                // Tệp đã biến mất (bảo vệ nền hoặc người dùng vừa cách ly trước đó).
                // Không phải lỗi: để trống error để bên gọi không ghi ERROR/đếm lỗi.
                error = "";
                return false;
            }

            // Thư mục cách ly cộng với tên gốc có thể vượt 260 ký tự (virus đặt tên
            // tệp bằng chuỗi Base64 rất dài). Khi đó dùng tên ngắn, đường dẫn gốc
            // vẫn được ghi trong manifest để khôi phục đúng chỗ.
            string baseDest = System.IO.Path.Combine(batch, "files", rel);
            string dest;
            if ((baseDest + ".sgq").Length > 230)
                dest = System.IO.Path.Combine(batch, "files", "_long",
                                              Guid.NewGuid().ToString("N") + ".sgq");
            else
            {
                dest = baseDest + ".sgq";
                int i = 1;
                while (WinPath.Exists(WinPath.Ext(dest)))
                {
                    dest = baseDest + "." + i + ".sgq";
                    i++;
                }
            }
            string destDir = System.IO.Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(destDir)) WinPath.CreateDirTree(WinPath.Ext(destDir));

            long size = WinPath.Size(from);
            if (size < 0) size = 0;
            string sha = Native.Sha256(path);

            int code = WinPath.CopyFile(from, WinPath.Ext(dest));
            if (code != 0)
            {
                error = WinPath.ErrorText(code);
                // Nếu nguồn vẫn còn thì bản sao trong vùng cách ly là mồ côi.
                if (WinPath.Exists(from)) WinPath.DeleteFile(WinPath.Ext(dest));
                // Antivirus (Defender) chặn đọc tệp này vì nó là mã độc: không sao lưu được,
                // nên xoá thẳng khỏi ổ. Antivirus vẫn giữ bản của nó trong lịch sử bảo vệ.
                if ((code == 225 || code == 226) && RemoveFlagged(path))
                {
                    error = "";
                    reason = reason + "; antivirus-blocked, deleted";
                }
                else return false;
            }
            else
            {
                WinPath.SetNormal(from);
                if (!WinPath.DeleteFile(from))
                {
                    // Bản sao đã tạo nhưng nguồn chưa rời đi: đừng ghi nhận đã cách ly,
                    // và bỏ bản sao để vùng cách ly không có tệp mồ côi.
                    int delCode = Marshal.GetLastWin32Error();
                    WinPath.DeleteFile(WinPath.Ext(dest));
                    // Nguồn đã bị luồng khác lấy đi trong lúc sao chép: không phải lỗi.
                    error = (delCode == 2 || delCode == 3) ? "" : WinPath.ErrorText(delCode);
                    return false;
                }
            }

            string row = string.Join(",", new string[]
            {
                Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                Csv(path), Csv(dest), Csv(reason),
                size.ToString(CultureInfo.InvariantCulture), Csv(sha)
            });
            try
            {
                File.AppendAllText(System.IO.Path.Combine(batch, "manifest.csv"),
                                   row + "\r\n", new UTF8Encoding(true));
            }
            catch { }
            Log.Write(S.Log.Quarantined(path, reason), "WARN");
            return true;
        }

        private static bool RemoveFlagged(string path)
        {
            try
            {
                string ext = WinPath.Ext(path);
                WinPath.SetNormal(ext);
                WinPath.DeleteFile(ext);
                return !WinPath.Exists(ext);
            }
            catch { return false; }
        }

        public static List<BatchInfo> ListBatches()
        {
            List<BatchInfo> list = new List<BatchInfo>();
            if (!Directory.Exists(Paths.QuarDir)) return list;
            string[] dirs;
            try { dirs = Directory.GetDirectories(Paths.QuarDir); }
            catch { return list; }
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            Array.Reverse(dirs);
            foreach (string d in dirs)
            {
                string mf = System.IO.Path.Combine(d, "manifest.csv");
                if (!File.Exists(mf)) continue;
                BatchInfo b = new BatchInfo();
                b.Name = System.IO.Path.GetFileName(d);
                b.Path = d;
                b.FromUsb = b.Name.StartsWith("usb_", StringComparison.OrdinalIgnoreCase)
                         || b.Name.StartsWith("auto_", StringComparison.OrdinalIgnoreCase);
                try
                {
                    List<string[]> rows = CsvFile.Read(mf);
                    int si = CsvFile.Index(rows[0], "Size");
                    for (int r = 1; r < rows.Count; r++)
                    {
                        b.Count++;
                        long sz;
                        if (si >= 0 && si < rows[r].Length &&
                            long.TryParse(rows[r][si], out sz)) b.Size += sz;
                    }
                    int ti = CsvFile.Index(rows[0], "Time");
                    if (rows.Count > 1 && ti >= 0 && ti < rows[1].Length)
                    {
                        DateTime when;
                        if (DateTime.TryParse(rows[1][ti], out when))
                            b.WhenText = when.ToString("dd/MM/yyyy HH:mm");
                    }
                }
                catch { }
                if (b.WhenText.Length == 0)
                {
                    DateTime fallback;
                    try
                    {
                        fallback = Directory.GetCreationTime(d);
                        b.WhenText = fallback.ToString("dd/MM/yyyy HH:mm");
                    }
                    catch { }
                }
                list.Add(b);
            }
            return list;
        }

        public static void RestoreBatch(string batchPath, out int restored, out int skipped, out int missing)
        {
            int refused;
            RestoreBatch(batchPath, out restored, out skipped, out missing, out refused);
        }

        // Manifest.csv la du lieu khong tin duoc: dong trong do co the bi sua de tro
        // nguon ra ngoai lan cach ly (doc tep bat ky) hoac dich vao thu muc he thong
        // (ghi de tep he thong bang quyen quan tri). Moi dong phai qua kiem tra nay.
        public static void RestoreBatch(string batchPath, out int restored, out int skipped,
                                        out int missing, out int refused)
        {
            restored = 0; skipped = 0; missing = 0; refused = 0;
            string mf = System.IO.Path.Combine(batchPath, "manifest.csv");
            if (!File.Exists(mf)) return;
            List<string[]> rows = CsvFile.Read(mf);
            if (rows.Count < 2) return;
            int iOrig = CsvFile.Index(rows[0], "OriginalPath");
            int iQuar = CsvFile.Index(rows[0], "QuarantinePath");
            int iSha = CsvFile.Index(rows[0], "SHA256");
            if (iOrig < 0 || iQuar < 0) return;

            string batchRoot = Canonical(batchPath);
            string quarRoot = Canonical(Paths.QuarDir);
            if (batchRoot.Length == 0) return;
            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                if (iOrig >= row.Length || iQuar >= row.Length) continue;
                string dest = row[iOrig], src = row[iQuar];
                if (string.IsNullOrEmpty(dest) || string.IsNullOrEmpty(src)) continue;

                // Nguon phai nam trong dung lan cach ly nay.
                if (!Inside(src, batchRoot))
                {
                    refused++;
                    RefuseRestore(src, S.Log.RestoreOutside);
                    continue;
                }
                string badTarget;
                if (!RestoreTargetOk(dest, quarRoot, out badTarget))
                {
                    refused++;
                    RefuseRestore(dest, badTarget);
                    continue;
                }
                if (!WinPath.Exists(WinPath.Ext(src))) { missing++; continue; }
                if (WinPath.Exists(WinPath.Ext(dest))) { skipped++; continue; }
                // Bản cách ly bị sửa hoặc hỏng thì không ghi trả về ổ.
                if (iSha >= 0 && iSha < row.Length && row[iSha].Length > 0)
                {
                    string now = Native.Sha256(src);
                    if (now.Length > 0 && !string.Equals(now, row[iSha], StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Write(S.Log.QuarantineChanged(src), "WARN");
                        missing++;
                        continue;
                    }
                }
                string parent = WinPath.Parent(dest);
                if (!string.IsNullOrEmpty(parent)) WinPath.CreateDirTree(WinPath.Ext(parent));
                int code = WinPath.CopyFile(WinPath.Ext(src), WinPath.Ext(dest));
                if (code == 0)
                {
                    restored++;
                    Log.Write(S.Log.Restored(dest));
                }
                else missing++;
            }
        }

        // Duong dan dich hop le: tuyet doi, co ten tep, khong nam trong vung cach ly,
        // thu muc chuong trinh hay thu muc he thong.
        private static bool RestoreTargetOk(string dest, string quarRoot, out string reason)
        {
            reason = S.Log.RestoreBadPath;
            if (string.IsNullOrEmpty(dest) || !System.IO.Path.IsPathRooted(dest)) return false;
            string full = Canonical(dest);
            if (full.Length == 0) return false;
            // "C:" hoac "C:\": dich phai la mot tep, khong phai ca o.
            int slash = full.LastIndexOf('\\');
            if (slash < 0 || slash == full.Length - 1) return false;

            // Vùng cách ly và thư mục cài đặt không phải chỗ khôi phục: dòng manifest
            // trỏ vào đó là dấu hiệu bị sửa. Không chặn cả thư mục chứa exe vì bản
            // chạy từ USB quét chính USB đó, khôi phục về gốc USB là hợp lệ (tệp đã
            // tồn tại thì luôn bị bỏ qua, không ghi đè).
            if (Inside(full, quarRoot) || Inside(full, Paths.BinDir))
            {
                reason = S.Log.RestoreAppFolder;
                return false;
            }

            string[] systemDirs = new string[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            foreach (string dir in systemDirs)
            {
                if (Inside(full, dir))
                {
                    reason = S.Log.RestoreSystemFolder;
                    return false;
                }
            }
            return true;
        }

        // Chuan hoa khong dung System.IO.Path.GetFullPath: tren .NET Framework 4.0
        // no nem PathTooLongException voi duong dan > 260 ky tu, dung loai duong dan
        // ma vung cach ly phai xu ly. Chi gop "." va ".." bang chuoi, khong cham dia.
        private static string Canonical(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string p = path;
            if (p.StartsWith(@"\\?\", StringComparison.Ordinal)) p = p.Substring(4);
            else if (p.StartsWith(@"\\", StringComparison.Ordinal)) return p.TrimEnd('\\');
            if (!System.IO.Path.IsPathRooted(p)) return "";

            string[] parts = p.Split('\\');
            List<string> stack = new List<string>();
            foreach (string part in parts)
            {
                if (part.Length == 0 || part == ".") continue;
                if (part == "..")
                {
                    // Khong duoc lui qua goc o hay goc chia se.
                    if (stack.Count > 1) stack.RemoveAt(stack.Count - 1);
                    continue;
                }
                stack.Add(part);
            }
            StringBuilder sb = new StringBuilder();
            foreach (string part in stack)
            {
                if (sb.Length > 0) sb.Append('\\');
                sb.Append(part);
            }
            return sb.ToString();
        }

        // So sanh tien to theo duong dan da chuan hoa, co them dau "\" de
        // "C:\Temp2" khong bi coi la nam trong "C:\Temp".
        private static bool Inside(string path, string dir)
        {
            string full = Canonical(path);
            string root = Canonical(dir);
            if (full.Length == 0 || root.Length == 0) return false;
            return full.Length > root.Length
                && full.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private static void RefuseRestore(string path, string reason)
        {
            string line = S.Log.RestoreRefused(path, reason);
            Log.Write(line, "WARN");
            try { Events.Add(Events.KindScan, "WARN", path, reason); }
            catch { }
        }

        // Xoá hẳn một lần cách ly sau khi đã khôi phục đủ. Chỉ xoá bên trong thư mục cách ly.
        public static bool RemoveBatch(string batchPath)
        {
            try
            {
                string full = System.IO.Path.GetFullPath(batchPath);
                string root = System.IO.Path.GetFullPath(Paths.QuarDir)
                    .TrimEnd(System.IO.Path.DirectorySeparatorChar)
                    + System.IO.Path.DirectorySeparatorChar;
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;
                if (Directory.Exists(full)) Directory.Delete(full, true);
                return true;
            }
            catch { return false; }
        }

        // Dọn sạch toàn bộ vùng cách ly. Trả về số lần cách ly và số tệp đã xoá được.
        public static void RemoveAll(out int batches, out int files)
        {
            batches = 0;
            files = 0;
            foreach (BatchInfo b in ListBatches())
            {
                if (!RemoveBatch(b.Path)) continue;
                batches++;
                files += b.Count;
            }
        }
    }

    internal static class CsvFile
    {
        public static int Index(string[] header, string name)
        {
            for (int i = 0; i < header.Length; i++)
                if (string.Equals(header[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        public static List<string[]> Read(string path)
        {
            List<string[]> rows = new List<string[]>();
            string text = File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF');
            List<string> fields = new List<string>();
            StringBuilder sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                        else quoted = false;
                    }
                    else sb.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Length = 0; }
                else if (c == '\n')
                {
                    fields.Add(sb.ToString().TrimEnd('\r'));
                    sb.Length = 0;
                    rows.Add(fields.ToArray());
                    fields.Clear();
                }
                else sb.Append(c);
            }
            if (sb.Length > 0 || fields.Count > 0)
            {
                fields.Add(sb.ToString().TrimEnd('\r'));
                rows.Add(fields.ToArray());
            }
            return rows;
        }
    }
}
