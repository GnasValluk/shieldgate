// ShieldGate - go cai dat hoan toan: tat bao ve, khoi phuc Windows, xoa du lieu va tep chay.
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
        // clean = false nghia la con tep chua xoa duoc; tien trinh nen se don not sau khi thoat.
        public static bool Run(out bool clean)
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

            clean = RemoveData();
            ScheduleCleanup();
            return true;
        }

        // ------------------------------------------------------------- xoá dữ liệu

        // Chỉ xoá những mục do ShieldGate tạo. Thư mục dữ liệu có thể do người dùng
        // chọn chung với tệp khác, nên không bao giờ xoá cả cây thư mục gốc.
        private static List<string> DataEntries()
        {
            List<string> list = new List<string>();
            Add(list, Paths.LogDir);
            Add(list, Paths.QuarDir);
            Add(list, Paths.BakDir);
            Add(list, Paths.BinDir);
            Add(list, Paths.SettingsFile);
            Add(list, Paths.SettingsFile + ".tmp");
            Add(list, Paths.LastScan);
            Add(list, Paths.LastScan + ".tmp");
            Add(list, Paths.GuardConfig);
            Add(list, Paths.Heartbeat);
            Add(list, Paths.StopFlag);
            Add(list, Paths.PolicyBackup + ".bad");
            return list;
        }

        private static void Add(List<string> list, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!list.Contains(path)) list.Add(path);
        }

        private static bool RemoveData()
        {
            foreach (string path in DataEntries()) RemovePath(path);
            RemovePath(Bootstrap.SetupFile);

            PruneEmpty(Paths.Root);
            PruneEmpty(Path.GetDirectoryName(Bootstrap.SetupFile));

            foreach (string path in DataEntries())
                if (PathExists(path)) return false;
            if (File.Exists(Bootstrap.SetupFile)) return false;
            return true;
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

        // --------------------------------------------------- dọn nốt sau khi thoát

        // Tệp đang chạy không thể tự xoá. Một tiến trình PowerShell chờ đúng PID này
        // thoát rồi xoá tệp chạy và các mục còn sót, kể cả khi người dùng đọc thông báo lâu.
        private static void ScheduleCleanup()
        {
            try
            {
                List<string> targets = DataEntries();
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
                ps.Append(Quote(exeDir)).Append(',').Append(Quote(Paths.Root))
                  .Append(',').Append(Quote(setupDir));
                ps.Append(")){");
                ps.Append("if($d -and (Test-Path -LiteralPath $d)){");
                ps.Append("try{[System.IO.Directory]::Delete($d,$false)}catch{}}}");

                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe",
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
                    ProcessStartInfo psi = new ProcessStartInfo("cmd.exe",
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
