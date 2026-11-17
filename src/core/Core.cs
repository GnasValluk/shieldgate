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
        public const string Version = "2.0";
        public const string TaskName = "ShieldGate USB Guard";
        public const string TrayTaskName = "ShieldGate Tray";
        // Tên mutex nội bộ đổi theo phiên bản lớn để bản 1.0 và 2.0 không tranh nhau.
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
        public static string SystemDrive;
        public static string RulesFile;
        public static string DevicesFile;
        public static string EventsFile;
        public static string RepairDir;

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
            RulesFile = Path.Combine(Root, "rules.json");
            DevicesFile = Path.Combine(Root, "devices.json");
            EventsFile = Path.Combine(Root, "events.tsv");
            RepairDir = Path.Combine(Root, "repair");
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
            Native.Run("icacls.exe", "\"" + dir + "\" /setowner \"*S-1-5-32-544\" /reset",
                       20000, out output);
            string args = "\"" + dir + "\" /setowner \"*S-1-5-32-544\" /inheritance:r " +
                          "/grant:r \"*S-1-5-18:(OI)(CI)F\" \"*S-1-5-32-544:(OI)(CI)F\"";
            if (usersRead) args += " \"*S-1-5-32-545:(OI)(CI)RX\"";
            else args += " /remove:g \"*S-1-5-32-545\"";
            Native.Run("icacls.exe", args, 20000, out output);
        }

        public static void EnsureDirs()
        {
            foreach (string d in new[] { Root, LogDir, QuarDir, BakDir, BinDir, RepairDir })
            {
                try { Directory.CreateDirectory(d); }
                catch { }
            }
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
            StringBuilder sb = new StringBuilder();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
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
            try
            {
                // Mo bang handle de doc duoc ca duong dan dai hon 260 ky tu.
                using (SHA256 sha = SHA256.Create())
                using (FileStream fs = WinPath.OpenRead(WinPath.Ext(path)))
                {
                    if (fs.Length > 10 * 1024 * 1024) return "";
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
                string[] entries;
                try { entries = Directory.GetFileSystemEntries(cur.Key); }
                catch { continue; }
                foreach (string full in entries)
                {
                    if (++count > maxFiles)
                    {
                        if (stats != null) stats.Truncated = true;
                        yield break;
                    }
                    string name = System.IO.Path.GetFileName(full);
                    if (SkipNames.Contains(name)) continue;
                    FileAttributes attrs;
                    try { attrs = File.GetAttributes(full); }
                    catch { continue; }
                    if ((attrs & FileAttributes.ReparsePoint) != 0) continue;
                    bool isDir = (attrs & FileAttributes.Directory) != 0;
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
                error = "not found";
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
                    error = WinPath.ErrorText(delCode);
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
        {            restored = 0; skipped = 0; missing = 0;
            string mf = System.IO.Path.Combine(batchPath, "manifest.csv");
            if (!File.Exists(mf)) return;
            List<string[]> rows = CsvFile.Read(mf);
            if (rows.Count < 2) return;
            int iOrig = CsvFile.Index(rows[0], "OriginalPath");
            int iQuar = CsvFile.Index(rows[0], "QuarantinePath");
            int iSha = CsvFile.Index(rows[0], "SHA256");
            if (iOrig < 0 || iQuar < 0) return;
            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                if (iOrig >= row.Length || iQuar >= row.Length) continue;
                string dest = row[iOrig], src = row[iQuar];
                if (string.IsNullOrEmpty(dest) || string.IsNullOrEmpty(src)) continue;
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
