// ShieldGate - go cai dat hoan toan: tat bao ve, khoi phuc Windows, xoa du lieu va tep chay.
// Chi xoa thu muc co tep danh dau .shieldgate (hoac khop chu ky tep ben trong) de
// khong xoa nham thu muc nguoi dung chon lam noi luu du lieu.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace ShieldGate
{
    internal static class Uninstaller
    {
        // Tra ve true khi da tat bao ve (co the dong ung dung).
        // Mac dinh giu cach ly va ban sao luu o: nguoi dung khong mat du lieu da
        // sao luu chi vi go cong cu.
        public static bool Run(out bool clean)
        {
            return Run(true, out clean);
        }

        // clean = false nghia la con tep chua xoa duoc; tien trinh nen se don not sau khi thoat.
        public static bool Run(bool keepData, out bool clean)
        {
            clean = false;
            if (!Native.IsAdmin()) return false;

            try
            {
                if (!Guard.Uninstall(null)) return false;
            }
            catch
            {
                return false;
            }

            clean = RemoveData(keepData, true);
            ScheduleCleanup(keepData);
            return true;
        }

        // ------------------------------------------------------------- xoá dữ liệu

        // Danh sach muc ShieldGate tao ra. keepData = true bo qua cach ly va ban
        // sao luu o (du lieu nguoi dung can giu lai).
        internal static List<string> DataEntries(bool keepData)
        {
            List<string> list = new List<string>();
            // Marker cua chinh thu muc goc: xoa truoc khi don thu muc rong.
            Add(list, Paths.MarkerIn(Paths.Root));
            Add(list, Paths.LogDir);
            Add(list, Paths.BakDir);
            Add(list, Paths.BinDir);
            Add(list, Paths.SettingsFile);
            Add(list, Paths.SettingsFile + ".tmp");
            Add(list, Paths.LastScan);
            Add(list, Paths.LastScan + ".tmp");
            Add(list, Paths.GuardConfig);
            Add(list, Paths.Heartbeat);
            Add(list, Paths.StopFlag);
            // Khoá dọn ổ (F1) không phải dữ liệu nhưng nằm ở gốc; để lại thì
            // PruneEmpty không xoá được thư mục dữ liệu sau khi gỡ.
            Add(list, CleanLock.FilePath);
            Add(list, Paths.PolicyBackup + ".bad");
            // Dữ liệu 2.0: luật, kho thiết bị, timeline.
            Add(list, Paths.RulesFile);
            Add(list, Paths.RulesFile + ".bak");
            Add(list, Paths.DevicesFile);
            Add(list, Paths.DevicesFile + ".tmp");
            Add(list, Paths.RegViewPrefix + "default.snapshot");
            Add(list, Paths.RegViewPrefix + "default.snapshot.bad");
            // Mỗi người dùng có ảnh chụp riêng; gỡ cài đặt xoá tất cả.
            try
            {
                foreach (string f in Directory.GetFiles(Paths.Root, "regview.*.snapshot*"))
                    Add(list, f);
            }
            catch { }
            Add(list, Paths.EventsFile);
            Add(list, Paths.EventsFile + ".1");
            Add(list, Paths.EventsFile + ".id");
            Add(list, Paths.EventsFile + ".lock");
            // Khoa VirusTotal la du lieu ca nhan: go cai dat luon xoa, ke ca khi
            // nguoi dung chon giu cach ly/ban sao luu.
            Add(list, Paths.VtKeyFile);
            if (!keepData)
            {
                // Cach ly la mau song va ban sao luu o la du lieu nguoi dung: chi xoa khi duoc yeu cau.
                Add(list, Paths.QuarDir);
                Add(list, Paths.RepairDir);
            }
            return list;
        }

        private static void Add(List<string> list, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!list.Contains(path)) list.Add(path);
        }

        // True khi thu muc chac chan do ShieldGate tao: co marker, hoac khop chu ky
        // tep dac trung cua ban 2.0 (chua co marker), hoac dang rong.
        internal static bool Owned(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return false;
            try
            {
                if (File.Exists(Paths.MarkerIn(dir))) return true;
            }
            catch { }
            try
            {
                // Thu muc rong: xoa khong lam mat gi cua nguoi dung.
                if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0)
                    return true;
            }
            catch { }
            try
            {
                if (Same(dir, Paths.LogDir) && File.Exists(Paths.LogFile)) return true;
                if (Same(dir, Paths.BinDir) && File.Exists(Paths.GuardExe)) return true;
                if (Same(dir, Paths.QuarDir))
                {
                    foreach (string sub in Directory.GetDirectories(dir))
                        if (File.Exists(Path.Combine(sub, "manifest.csv"))) return true;
                }
                if (Same(dir, Paths.RepairDir))
                {
                    if (Directory.GetFiles(dir, "*.ok").Length > 0) return true;
                    foreach (string sub in Directory.GetDirectories(dir))
                        if (Path.GetFileName(sub).StartsWith("backup-", StringComparison.OrdinalIgnoreCase))
                            return true;
                }
            }
            catch { }
            return false;
        }

        private static bool Same(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            return string.Equals(a.TrimEnd(Path.DirectorySeparatorChar),
                                 b.TrimEnd(Path.DirectorySeparatorChar),
                                 StringComparison.OrdinalIgnoreCase);
        }

        internal static bool RemoveData(bool keepData, bool includeSetup)
        {
            List<string> targets = DataEntries(keepData);
            if (includeSetup) Add(targets, Bootstrap.SetupFile);

            bool clean = true;
            foreach (string path in targets)
            {
                bool isDir = Directory.Exists(path);
                if (!isDir && !File.Exists(path)) continue;
                if (isDir)
                {
                    if (!Owned(path))
                    {
                        // Khong xoa thu muc khong phai cua minh, ke ca khi no nam
                        // dung ten trong thu muc du lieu.
                        // (bo qua im lang se khien nguoi dung tuong da xoa sach)
                        Log.Write(S.Log.UninstallSkip(path), "WARN");
                        continue;
                    }
                    Adopt(path);
                }
                RemovePath(path);
                if (isDir ? Directory.Exists(path) : File.Exists(path)) clean = false;
            }

            if (!keepData && clean)
            {
                PruneEmpty(Paths.Root);
                if (includeSetup) PruneEmpty(Path.GetDirectoryName(Bootstrap.SetupFile));
            }
            return clean;
        }

        // Ban 2.0 chua ghi marker: ghi bu truoc khi xoa de lan sau (neu con) biet
        // day la thu muc cua ShieldGate.
        private static void Adopt(string dir)
        {
            try
            {
                string marker = Paths.MarkerIn(dir);
                if (marker.Length == 0 || File.Exists(marker)) return;
                File.WriteAllText(marker, "app=" + AppInfo.Name + "\r\nversion=" + AppInfo.Version
                    + "\r\nadopted=" + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss",
                                                          CultureInfo.InvariantCulture) + "\r\n",
                    new UTF8Encoding(false));
            }
            catch { }
        }

        private static bool PathExists(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try { return File.Exists(path) || Directory.Exists(path); }
            catch { return false; }
        }

        // Thử xoá vài lần vì bảo vệ nền hoặc trình quét virus có thể còn giữ tệp trong giây lát.
        private static void RemovePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            for (int i = 0; i < 6; i++)
            {
                try
                {
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                    else if (File.Exists(path)) File.Delete(path);
                }
                catch { }
                if (!PathExists(path)) return;
                Thread.Sleep(250);
            }
        }

        // Chỉ xoá thư mục khi đã rỗng: không đụng tới tệp khác của người dùng.
        private static void PruneEmpty(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            try
            {
                if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0)
                    Directory.Delete(dir);
            }
            catch { }
        }

        // Dung luong se bi xoa neu nguoi dung bo tich "giu cach ly va ban sao luu".
        // Gioi han thoi gian: thu muc sao luu co the rat lon, khong duoc lam treo giao dien.
        internal static void KeptSizes(out long quarantine, out long repair)
        {
            Stopwatch sw = Stopwatch.StartNew();
            quarantine = DirBytes(Paths.QuarDir, sw, 300);
            repair = DirBytes(Paths.RepairDir, sw, 600);
        }

        private static long DirBytes(string dir, Stopwatch sw, int limitMs)
        {
            long sum = 0;
            try
            {
                if (!Directory.Exists(dir)) return 0;
                foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    if (sw.ElapsedMilliseconds > limitMs) break;
                    try { sum += new FileInfo(file).Length; }
                    catch { }
                }
            }
            catch { }
            return sum;
        }

        // --------------------------------------------------- dọn nốt sau khi thoát

        // Tệp đang chạy không thể tự xoá. Một tiến trình PowerShell chờ đúng PID này
        // thoát rồi xoá tệp chạy và các mục còn sót, kể cả khi người dùng đọc thông báo lâu.
        private static void ScheduleCleanup(bool keepData)
        {
            try
            {
                List<string> targets = DataEntries(keepData);
                Add(targets, Bootstrap.SetupFile);

                string exe = CurrentExe();
                Add(targets, exe);

                string exeDir = string.IsNullOrEmpty(exe) ? "" : Path.GetDirectoryName(exe);
                string setupDir = Path.GetDirectoryName(Bootstrap.SetupFile);

                StringBuilder ps = new StringBuilder();
                ps.Append("Wait-Process -Id ");
                ps.Append(Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
                ps.Append(" -ErrorAction SilentlyContinue;");
                ps.Append("Start-Sleep -Milliseconds 400;");

                ps.Append("$p=@(");
                bool first = true;
                foreach (string path in targets)
                {
                    if (string.IsNullOrEmpty(path)) continue;
                    if (!first) ps.Append(',');
                    ps.Append(Quote(path));
                    first = false;
                }
                ps.Append(");");

                // Thử lại tối đa 30 lần (~15 giây) sau khi tiến trình thoát.
                ps.Append("for($i=0;$i -lt 30;$i++){");
                ps.Append("foreach($x in $p){if($x){Remove-Item -LiteralPath $x -Recurse -Force -ErrorAction SilentlyContinue}};");
                ps.Append("$left=$false;");
                ps.Append("foreach($x in $p){if($x -and (Test-Path -LiteralPath $x)){$left=$true}};");
                ps.Append("if(-not $left){break};");
                ps.Append("Start-Sleep -Milliseconds 500};");

                // Dọn các thư mục đã rỗng (chỉ xoá khi không còn tệp nào bên trong).
                ps.Append("foreach($d in @(");
                ps.Append(Quote(exeDir)).Append(',').Append(Quote(keepData ? "" : Paths.Root))
                  .Append(',').Append(Quote(setupDir));
                ps.Append(")){");
                ps.Append("if($d -and (Test-Path -LiteralPath $d)){");
                ps.Append("try{[System.IO.Directory]::Delete($d,$false)}catch{}}}");

                ProcessStartInfo psi = new ProcessStartInfo(Native.SysExe("powershell.exe"),
                    "-NoProfile -NonInteractive -WindowStyle Hidden -Command \"" + ps + "\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch
            {
                // PowerShell bị chặn: ít nhất cũng xoá được tệp chạy bằng cmd.
                try
                {
                    string exe = CurrentExe();
                    if (string.IsNullOrEmpty(exe)) return;
                    ProcessStartInfo psi = new ProcessStartInfo(Native.SysExe("cmd.exe"),
                        "/c ping 127.0.0.1 -n 4 >nul & del /f /q \"" + exe + "\"");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    Process.Start(psi);
                }
                catch { }
            }
        }

        private static string Quote(string value)
        {
            return "'" + (value ?? "").Replace("'", "''") + "'";
        }

        private static string CurrentExe()
        {
            try { return Assembly.GetEntryAssembly().Location; }
            catch { }
            try { return Process.GetCurrentProcess().MainModule.FileName; }
            catch { return ""; }
        }
    }
}
