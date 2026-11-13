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
        public const string Version = "7.2";
        public const string TaskName = "ShieldGate USB Guard";
        public const string TrayTaskName = "ShieldGate Tray";
        public const string MutexName = "Global\\ShieldGate_USBGuard_7";
        public const string UiMutexName = "Local\\ShieldGate_UI_7";
        public const string TrayMutexName = "Local\\ShieldGate_Tray_7";
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
        public static string SystemDrive;

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
            EnsureDirs();
        }

        public static void EnsureDirs()
        {
            foreach (string d in new[] { Root, LogDir, QuarDir, BakDir, BinDir })
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
                        if (File.Exists(Paths.LogFile) && new FileInfo(Paths.LogFile).Length > 5 * 1024 * 1024)
                            File.Copy(Paths.LogFile, Paths.LogFile + ".1", true);
                    }
                    catch { }
                    string line = string.Format("{0} [{1}] {2}\r\n",
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), level, message);
                    File.AppendAllText(Paths.LogFile, line, new UTF8Encoding(true));
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

        public static List<string> ScanDrives()
        {
            List<string> all = Drives(DriveType.Removable);
            all.AddRange(Drives(DriveType.Fixed));
            return all;
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
                    }
                    p.WaitForExit();
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
                if (new FileInfo(path).Length > 10 * 1024 * 1024) return "";
                using (SHA256 sha = SHA256.Create())
                using (FileStream fs = File.OpenRead(path))
                {
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
    }

    internal static class Tree
    {
        public static readonly HashSet<string> SkipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "$RECYCLE.BIN", "System Volume Information", ".Trashes",
            ".Spotlight-V100", ".fseventsd"
        };

        public static IEnumerable<TreeItem> Walk(string root, int maxFiles = 150000, int maxSeconds = 300)
        {
            DateTime start = DateTime.UtcNow;
            Stack<KeyValuePair<string, int>> stack = new Stack<KeyValuePair<string, int>>();
            stack.Push(new KeyValuePair<string, int>(root, 0));
            int count = 0;
            while (stack.Count > 0)
            {
                if ((DateTime.UtcNow - start).TotalSeconds > maxSeconds) yield break;
                KeyValuePair<string, int> cur = stack.Pop();
                string[] entries;
                try { entries = Directory.GetFileSystemEntries(cur.Key); }
                catch { continue; }
                foreach (string full in entries)
                {
                    if (++count > maxFiles) yield break;
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
                    if (isDir) stack.Push(new KeyValuePair<string, int>(full, cur.Value + 1));
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

            string destDir = System.IO.Path.Combine(System.IO.Path.Combine(batch, "files"),
                                                   System.IO.Path.GetDirectoryName(rel) ?? "");
            try { Directory.CreateDirectory(destDir); }
            catch { }

            string dest = System.IO.Path.Combine(batch, "files", rel) + ".sgq";
            int i = 1;
            while (File.Exists(dest))
            {
                dest = System.IO.Path.Combine(batch, "files", rel) + "." + i + ".sgq";
                i++;
            }

            long size = 0;
            try { size = new FileInfo(path).Length; }
            catch { }
            string sha = Native.Sha256(path);

            try
            {
                File.Copy(path, dest, true);
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
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
            Log.Write("Cach ly: " + path + " | " + reason, "WARN");
            return true;
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
                    for (int r = 1; r < rows.Count; r++)
                    {
                        b.Count++;
                        long sz;
                        int idx = CsvFile.Index(rows[0], "Size");
                        if (idx >= 0 && idx < rows[r].Length &&
                            long.TryParse(rows[r][idx], out sz)) b.Size += sz;
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
            if (iOrig < 0 || iQuar < 0) return;
            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                if (iOrig >= row.Length || iQuar >= row.Length) continue;
                string dest = row[iOrig], src = row[iQuar];
                if (string.IsNullOrEmpty(dest) || string.IsNullOrEmpty(src)) continue;
                if (!File.Exists(src)) { missing++; continue; }
                if (File.Exists(dest)) { skipped++; continue; }
                try
                {
                    string parent = System.IO.Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                    File.Copy(src, dest, false);
                    restored++;
                    Log.Write("Phuc hoi: " + dest);
                }
                catch { missing++; }
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
