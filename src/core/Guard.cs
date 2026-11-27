// ShieldGate - bật/tắt bảo vệ, tác vụ chạy nền, tín hiệu sống, chặn autorun.
// Mọi thay đổi registry đều được sao lưu trước khi ghi; tắt bảo vệ trả lại đúng cấu hình cũ.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace ShieldGate
{
    internal sealed class StatusInfo
    {
        public bool Admin;
        public bool Autorun;
        public bool Ini;
        public bool Wsh;
        public bool Registry;
        public bool Task;
        public bool Running;
        public bool WriteProtect;
        public bool BlockExec;
        public int QuarantineFiles;
        public int QuarantineBatches;

        // Bốn thiết lập chính đang có hiệu lực: chặn autorun, chặn autorun.inf,
        // bảo vệ Registry File Explorer và chạy nền.
        public bool FullyProtected { get { return Autorun && Ini && Registry && Running; } }

        // Bảo vệ đang bật khi còn ít nhất một lớp chính đang chạy. Người dùng có
        // quyền chủ động tắt một lớp (ví dụ chặn AutoRun); khi đó vẫn là đang bảo
        // vệ, không phải "chưa bật", nếu không nút Bật bảo vệ sẽ bật lại lớp đã
        // tắt. Bốn lớp này khớp với dòng Bảo vệ ở trang Tổng quan.
        public bool ProtectionOn
        {
            get { return Running || Autorun || Ini || Registry; }
        }
    }

    // Lựa chọn người dùng đã đặt, để bảo vệ nền chỉ áp đúng phần đang bật.
    internal sealed class GuardConfig
    {
        public bool Autorun = true;
        public bool Ini = true;
        public bool Wsh;
        public bool Registry;
        public bool WriteProtect;
        public bool BlockExec;
    }

    internal static class Guard
    {
        public const string ExplorerHklm = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer";
        public const string ExplorerHkcu = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
        public const string AutoplayHklm = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers";
        public const string AutoplayHkcu = @"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers";
        public const string IniPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\IniFileMapping\Autorun.inf";
        public const string WshPath = @"SOFTWARE\Microsoft\Windows Script Host\Settings";
        public const string WshPath32 = @"SOFTWARE\Wow6432Node\Microsoft\Windows Script Host\Settings";

        private static readonly string[][] SnapshotItems =
        {
            new[] { "HKLM", ExplorerHklm, "NoDriveTypeAutoRun" },
            new[] { "HKLM", ExplorerHklm, "NoAutorun" },
            new[] { "HKCU", ExplorerHkcu, "NoDriveTypeAutoRun" },
            new[] { "HKLM", AutoplayHklm, "DisableAutoplay" },
            new[] { "HKCU", AutoplayHkcu, "DisableAutoplay" },
            new[] { "HKLM", IniPath, "" },
            new[] { "HKLM", WshPath, "Enabled" },
            new[] { "HKLM", WshPath32, "Enabled" }
        };

        // Ổ chỉ đọc chỉ báo một lần, để vòng lặp nền không ghi lại cùng câu mỗi 10 giây.
        private static readonly HashSet<string> ReadOnlyLogged =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Ổ không ghi được (khoá ghi hoặc ACL) bị bỏ qua 5 phút; ổ ghi được chỉ
        // thử lại mỗi phút. Trước đây mỗi 10 giây lại quét rồi ghi WARN, nhật ký
        // máy bệnh 05/11 đầy dòng "chỉ đọc hoặc không có quyền ghi" của ổ D:.
        private static readonly Dictionary<string, DateTime> UnwritableUntil =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, DateTime> WritableSeen =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private static bool SkipReadOnly(string volume)
        {
            DateTime until;
            if (UnwritableUntil.TryGetValue(volume, out until))
            {
                if (DateTime.Now < until) return true;
                UnwritableUntil.Remove(volume);
            }
            DateTime seen;
            if (WritableSeen.TryGetValue(volume, out seen)
                && (DateTime.Now - seen).TotalSeconds < 60)
                return false;

            bool unwritable = Native.IsReadOnlyVolume(volume);
            if (!unwritable && Policy.WriteProtectActive)
            {
                // Khoa do ShieldGate bat se duoc tam mo khi cach ly; khoa do
                // nguoi dung bat thi van phai coi la khong ghi duoc.
                unwritable = !Policy.CanPauseWriteProtect;
            }
            if (!unwritable && !Policy.WriteProtectActive && !Native.CanWrite(volume))
                unwritable = true;

            if (unwritable)
            {
                UnwritableUntil[volume] = DateTime.Now.AddMinutes(5);
                WritableSeen.Remove(volume);
                bool first;
                lock (ReadOnlyLogged) first = ReadOnlyLogged.Add(volume);
                if (first) Log.Write(S.Log.DriveNotWritable(volume), "WARN");
                return true;
            }
            WritableSeen[volume] = DateTime.Now;
            return false;
        }

        // ---------------------------------------------------------- chính sách

        // G1: giữ giá trị chính sách do ShieldGate đặt. Lần đầu trong tiến trình
        // chỉ ghi; nếu giá trị đã ghi bị đổi ngược ở vòng sau thì đặt lại và ghi
        // một dòng log. Không dùng cho nhánh tắt bảo vệ (đó là thay đổi có chủ ý).
        private static readonly Dictionary<string, string> Enforced =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static bool Keep(RegistryHive hive, string path, string name,
                                 object value, RegistryValueKind kind)
        {
            string key = hive + "|" + path + "|" + name;
            bool known;
            lock (Enforced) known = Enforced.ContainsKey(key);

            object actual;
            RegistryValueKind actualKind;
            bool exists = Reg.Read(hive, path, name, out actual, out actualKind);
            bool changed;
            if (!exists) changed = true;
            else if (kind == RegistryValueKind.DWord) changed = ToInt(actual) != ToInt(value);
            else
                changed = !string.Equals((Convert.ToString(actual) ?? "").Trim(),
                                         (Convert.ToString(value) ?? "").Trim(),
                                         StringComparison.OrdinalIgnoreCase);

            if (!Reg.Write(hive, path, name, value, kind)) return false;
            if (changed && known)
            {
                string label = name.Length == 0 ? "(Default)" : name;
                Log.Write(S.Log.PolicyReverted(label), "WARN");
            }
            lock (Enforced) Enforced[key] = Convert.ToString(value) ?? "";
            return true;
        }

        private static int ToInt(object value)
        {
            try { return Convert.ToInt32(value); }
            catch { return 0; }
        }

        public static bool ApplyMachinePolicy(bool? blockWsh)
        {
            bool ok = true;
            ok &= Keep(RegistryHive.LocalMachine, ExplorerHklm, "NoDriveTypeAutoRun", 255, RegistryValueKind.DWord);
            ok &= Keep(RegistryHive.LocalMachine, ExplorerHklm, "NoAutorun", 1, RegistryValueKind.DWord);
            ok &= Keep(RegistryHive.LocalMachine, AutoplayHklm, "DisableAutoplay", 1, RegistryValueKind.DWord);
            ok &= Keep(RegistryHive.LocalMachine, IniPath, "", "@SYS:DoesNotExist", RegistryValueKind.String);
            ok &= Keep(RegistryHive.CurrentUser, ExplorerHkcu, "NoDriveTypeAutoRun", 255, RegistryValueKind.DWord);
            ok &= Keep(RegistryHive.CurrentUser, AutoplayHkcu, "DisableAutoplay", 1, RegistryValueKind.DWord);
            if (blockWsh == true)
            {
                ok &= Keep(RegistryHive.LocalMachine, WshPath, "Enabled", 0, RegistryValueKind.DWord);
                ok &= Keep(RegistryHive.LocalMachine, WshPath32, "Enabled", 0, RegistryValueKind.DWord);
            }
            else if (blockWsh == false)
            {
                ok &= Reg.Write(RegistryHive.LocalMachine, WshPath, "Enabled", 1, RegistryValueKind.DWord);
                ok &= Reg.Write(RegistryHive.LocalMachine, WshPath32, "Enabled", 1, RegistryValueKind.DWord);
            }
            return ok;
        }

        // ------------------------------------------------- lựa chọn đã lưu

        public static GuardConfig LoadConfig()
        {
            return LoadConfig(Paths.GuardConfig);
        }

        private static GuardConfig LoadConfig(string path)
        {
            GuardConfig c = new GuardConfig();
            try
            {
                if (!File.Exists(path)) return c;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    bool on = line.Substring(eq + 1).Trim() == "1";
                    if (string.Equals(key, "autorun", StringComparison.OrdinalIgnoreCase)) c.Autorun = on;
                    else if (string.Equals(key, "ini", StringComparison.OrdinalIgnoreCase)) c.Ini = on;
                    else if (string.Equals(key, "wsh", StringComparison.OrdinalIgnoreCase)) c.Wsh = on;
                    else if (string.Equals(key, "registry", StringComparison.OrdinalIgnoreCase)) c.Registry = on;
                    else if (string.Equals(key, "writeprotect", StringComparison.OrdinalIgnoreCase)) c.WriteProtect = on;
                    else if (string.Equals(key, "blockexec", StringComparison.OrdinalIgnoreCase)) c.BlockExec = on;
                }
            }
            catch { }
            return c;
        }

        // Đã có lựa chọn nào được lưu chưa. Lần đầu bật bảo vệ dùng bộ mặc định;
        // từ lần sau tôn trọng đúng những mục người dùng đã tắt.
        public static bool HasConfig
        {
            get
            {
                try
                {
                    return File.Exists(Paths.GuardConfig) || File.Exists(Paths.GuardLast);
                }
                catch { return false; }
            }
        }

        private static bool AnyOn(GuardConfig c)
        {
            return c != null && (c.Autorun || c.Ini || c.Wsh || c.Registry
                                 || c.WriteProtect || c.BlockExec);
        }

        // Bộ công tắc dùng khi bấm "Bật bảo vệ": đúng lựa chọn đã lưu, không bật
        // lại mục người dùng chủ động tắt. Khi guard.cfg toàn 0 (người dùng đã
        // tắt hết), lấy lại lựa chọn trước đó từ guard.last.
        public static GuardConfig EnableConfig()
        {
            // GuardConfig mặc định Autorun/Ini = true, nên chỉ dùng LoadConfig
            // khi tệp thật sự tồn tại; nếu không sẽ coi "chưa có tệp" là đã bật.
            if (File.Exists(Paths.GuardConfig))
            {
                GuardConfig saved = LoadConfig();
                if (AnyOn(saved)) return saved;
            }

            if (File.Exists(Paths.GuardLast))
            {
                GuardConfig last = LoadConfig(Paths.GuardLast);
                if (AnyOn(last)) return last;
            }

            GuardConfig c = new GuardConfig();
            c.Autorun = true;
            c.Ini = true;
            c.Registry = true;
            return c;
        }

        public static void SaveConfig(bool autorun, bool ini, bool wsh, bool registry,
                                      bool writeProtect = false, bool blockExec = false)
        {
            try
            {
                Paths.EnsureDirs();
                // Tắt hết: guard.cfg phải phản ánh trạng thái thật (toàn 0) để
                // công tắc Registry không tự bật lại sau khi làm mới. Giữ bản
                // lựa chọn cũ trong guard.last cho lần "Bật bảo vệ" kế tiếp.
                if (!autorun && !ini && !wsh && !registry && !writeProtect && !blockExec)
                {
                    try
                    {
                        if (File.Exists(Paths.GuardConfig))
                            File.Copy(Paths.GuardConfig, Paths.GuardLast, true);
                    }
                    catch { }
                }
                File.WriteAllText(Paths.GuardConfig,
                    "autorun=" + (autorun ? "1" : "0") + "\r\n" +
                    "ini=" + (ini ? "1" : "0") + "\r\n" +
                    "wsh=" + (wsh ? "1" : "0") + "\r\n" +
                    "registry=" + (registry ? "1" : "0") + "\r\n" +
                    "writeprotect=" + (writeProtect ? "1" : "0") + "\r\n" +
                    "blockexec=" + (blockExec ? "1" : "0") + "\r\n",
                    new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Log.Write(S.Log.ConfigSaveFailed(e.Message), "ERROR");
            }
        }

        // Chỉ áp phần người dùng đang bật. Phần đã tắt do Configure trả về
        // giá trị gốc một lần và không bị vòng lặp nền bật lại.
        public static bool ApplyConfig(GuardConfig c)
        {
            if (c == null) return true;
            bool ok = true;
            if (c.Autorun)
            {
                ok &= Keep(RegistryHive.LocalMachine, ExplorerHklm, "NoDriveTypeAutoRun", 255, RegistryValueKind.DWord);
                ok &= Keep(RegistryHive.LocalMachine, ExplorerHklm, "NoAutorun", 1, RegistryValueKind.DWord);
                ok &= Keep(RegistryHive.LocalMachine, AutoplayHklm, "DisableAutoplay", 1, RegistryValueKind.DWord);
                ok &= Keep(RegistryHive.CurrentUser, ExplorerHkcu, "NoDriveTypeAutoRun", 255, RegistryValueKind.DWord);
                ok &= Keep(RegistryHive.CurrentUser, AutoplayHkcu, "DisableAutoplay", 1, RegistryValueKind.DWord);
            }
            if (c.Ini)
            {
                ok &= Keep(RegistryHive.LocalMachine, IniPath, "", "@SYS:DoesNotExist",
                           RegistryValueKind.String);
            }
            if (c.Wsh)
            {
                ok &= Keep(RegistryHive.LocalMachine, WshPath, "Enabled", 0, RegistryValueKind.DWord);
                ok &= Keep(RegistryHive.LocalMachine, WshPath32, "Enabled", 0, RegistryValueKind.DWord);
            }
            if (c.Registry) ok &= RegistryGuard.Ensure(true);
            // Chặn ghi ổ USB là chính sách toàn máy: bật/tắt đều idempotent.
            if (Native.IsAdmin()) ok &= Policy.ApplyWriteProtect(c.WriteProtect);
            return ok;
        }

        private static RegistryHive Hive(string h)
        {
            return h == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
        }

        public static void SaveSnapshot()
        {
            StringBuilder sb = new StringBuilder();
            foreach (string[] item in SnapshotItems)
            {
                object value;
                RegistryValueKind kind;
                bool exists = Reg.Read(Hive(item[0]), item[1], item[2], out value, out kind);
                string val = exists ? (Convert.ToString(value) ?? "") : "";
                sb.Append(item[0]).Append('|').Append(item[1]).Append('|').Append(item[2])
                  .Append('|').Append(exists ? "1" : "0").Append('|')
                  .Append((int)kind).Append('|')
                  .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(val))).Append("\r\n");
            }
            Paths.EnsureDirs();
            File.WriteAllText(Paths.PolicyBackup, sb.ToString(), new UTF8Encoding(true));
        }

        private static List<string[]> LoadSnapshot()
        {
            List<string[]> rows = new List<string[]>();
            if (!File.Exists(Paths.PolicyBackup)) return rows;
            foreach (string line in File.ReadAllLines(Paths.PolicyBackup))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] p = line.Split('|');
                if (p.Length < 6) throw new FormatException("bản sao lưu hỏng");
                rows.Add(p);
            }
            return rows;
        }

        private static bool RestoreSnapshot(List<string[]> rows)
        {
            bool ok = true;
            foreach (string[] p in rows)
            {
                try
                {
                    RegistryHive hive = Hive(p[0]);
                    string path = p[1], name = p[2];
                    bool exists = p[3] == "1";
                    RegistryValueKind kind = (RegistryValueKind)int.Parse(p[4]);
                    string value = Encoding.UTF8.GetString(Convert.FromBase64String(p[5]));
                    if (exists)
                    {
                        object v = value;
                        if (kind == RegistryValueKind.DWord) v = int.Parse(value);
                        ok &= Reg.Write(hive, path, name, v, kind);
                    }
                    else ok &= Reg.DeleteValue(hive, path, name);
                }
                catch { ok = false; }
            }
            return ok;
        }

        public static bool RestoreFromSnapshot()
        {
            try
            {
                List<string[]> rows = LoadSnapshot();
                return rows.Count > 0 && RestoreSnapshot(rows);
            }
            catch { return false; }
        }

        private static void EnsureSnapshot()
        {
            if (File.Exists(Paths.PolicyBackup))
            {
                try { LoadSnapshot(); return; }
                catch
                {
                    try { File.Move(Paths.PolicyBackup, Paths.PolicyBackup + ".bad"); }
                    catch { }
                }
            }
            SaveSnapshot();
        }

        // ------------------------------------------------------------- tác vụ

        // Tên tác vụ của bản cũ (Lá Chắn). Dọn khi cài/gỡ để không có hai bảo vệ nền.
        private const string LegacyTaskName = "Lá Chắn - bảo vệ USB";

        private static void RemoveLegacyTask()
        {
            string output;
            Native.Run(Native.SysExe("schtasks.exe"), "/End /TN \"" + LegacyTaskName + "\"", 10000, out output);
            Native.Run(Native.SysExe("schtasks.exe"), "/Delete /TN \"" + LegacyTaskName + "\" /F", 10000, out output);
        }

        private const string TaskXml =
@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>ShieldGate: blocks autorun and scans USB drives when plugged in.</Description>
  </RegistrationInfo>
  <Triggers><BootTrigger><Enabled>true</Enabled></BootTrigger></Triggers>
  <Principals>
    <Principal id=""Author""><UserId>S-1-5-18</UserId><RunLevel>HighestAvailable</RunLevel></Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>true</Hidden>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <RestartOnFailure><Interval>PT1M</Interval><Count>999</Count></RestartOnFailure>
  </Settings>
  <Actions Context=""Author"">
    <Exec><Command>{0}</Command><Arguments>{1}</Arguments></Exec>
  </Actions>
</Task>";

        // Biểu tượng khay chạy trong phiên đăng nhập của người dùng, không phải SYSTEM,
        // để người dùng thấy bảo vệ nền đang chạy.
        private const string TrayTaskXml =
@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>ShieldGate: shows a tray icon while background protection runs.</Description>
  </RegistrationInfo>
  <Triggers><LogonTrigger><Enabled>true</Enabled><Delay>PT5S</Delay></LogonTrigger></Triggers>
  <Principals>
    <Principal id=""Author""><UserId>{1}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <RestartOnFailure><Interval>PT1M</Interval><Count>3</Count></RestartOnFailure>
  </Settings>
  <Actions Context=""Author"">
    <Exec><Command>{0}</Command><Arguments>--tray</Arguments></Exec>
  </Actions>
</Task>";

        private static string XmlEscape(string s)
        {
            return (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;")
                            .Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        public static bool TaskExists()
        {
            if (!Native.IsAdmin()) return false;
            string output;
            return Native.Run(Native.SysExe("schtasks.exe"), "/Query /TN \"" + AppInfo.TaskName + "\"", 15000, out output) == 0;
        }

        private static string SelfPath()
        {
            try { return Assembly.GetEntryAssembly().Location; }
            catch { return ""; }
        }

        private static bool IsSamePath(string a, string b)
        {
            try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        // Hai tệp trùng nội dung thì không cần chép đè lên bản đang cài.
        private static bool SameFile(string a, string b)
        {
            try
            {
                if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
                return string.Equals(Native.Sha256(a), Native.Sha256(b), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        // Đợi tiến trình nền nhả tệp sau khi dừng tác vụ, tránh lỗi "tệp đang được dùng".
        private static void WaitGuardExit(int timeoutMs)
        {
            DateTime end = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < end)
            {
                bool found = false;
                try
                {
                    foreach (Process p in Process.GetProcessesByName("ShieldGate"))
                    {
                        try
                        {
                            if (!IsSamePath(Native.ProcessImagePath(p.Id), Paths.GuardExe)) continue;
                            found = true;
                            p.WaitForExit(200);
                        }
                        catch { }
                    }
                }
                catch { }
                if (!found) return;
                Thread.Sleep(150);
            }
        }

        // Thư mục chứa bản chạy nền: chỉ quản trị và SYSTEM được ghi.
        private static bool PinBinary(out string problem)
        {
            problem = "";
            string self = SelfPath();
            if (self.Length == 0) { problem = "không xác định được đường dẫn chương trình"; return false; }

            if (!IsSamePath(self, Paths.GuardExe))
            {
                if (File.Exists(Paths.GuardExe) && SameFile(self, Paths.GuardExe))
                {
                    // Bản đã cài trùng bản đang chạy: giữ nguyên, không chép đè.
                }
                else
                {
                    Paths.HardenRoot();
                    bool copied = false;
                    for (int i = 0; i < 3 && !copied; i++)
                    {
                        try { File.Copy(self, Paths.GuardExe, true); copied = true; }
                        catch (Exception e)
                        {
                            problem = e.Message;
                            // Bản nền đang chạy giữ tệp: dừng tác vụ, đợi nhả tệp rồi thử lại.
                            if (i == 0)
                            {
                                StopGuardProcess();
                                WaitGuardExit(5000);
                            }
                            Thread.Sleep(500);
                        }
                    }
                    if (!copied) { problem = "không chép được bản chạy nền: " + problem; return false; }
                }
            }

            string hash = Native.Sha256(Paths.GuardExe);
            if (hash.Length == 0) { problem = "không đọc được tệp đã cài"; return false; }

            // Tệp vân tay có thể còn thuộc tính chỉ-đọc từ bản cũ: ghi đè sẽ bị từ chối.
            // Nếu nội dung đã đúng thì không ghi lại; nếu cần ghi thì bỏ thuộc tính trước.
            string current = "";
            try
            {
                if (File.Exists(Paths.GuardHash))
                    current = File.ReadAllText(Paths.GuardHash).Trim();
            }
            catch { }

            if (string.Equals(current, hash, StringComparison.OrdinalIgnoreCase))
            {
                try { File.SetAttributes(Paths.GuardHash, FileAttributes.Hidden | FileAttributes.System); }
                catch { }
                return true;
            }

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    try { File.SetAttributes(Paths.GuardHash, FileAttributes.Normal); }
                    catch { }
                    File.WriteAllText(Paths.GuardHash, hash + "\r\n", new UTF8Encoding(false));
                    File.SetAttributes(Paths.GuardHash, FileAttributes.Hidden | FileAttributes.System);
                    return true;
                }
                catch (Exception e)
                {
                    problem = e.Message;
                    try { File.Delete(Paths.GuardHash); } catch { }
                    Thread.Sleep(200);
                }
            }
            problem = "không ghi được dấu vân tay bản chạy nền: " + problem;
            return false;
        }

        private static void StopGuardProcess()
        {
            string output;
            try { File.WriteAllText(Paths.StopFlag, "stop"); } catch { }
            Native.Run(Native.SysExe("schtasks.exe"), "/End /TN \"" + AppInfo.TaskName + "\"", 20000, out output);
            try { File.Delete(Paths.StopFlag); } catch { }
        }

        private static bool VerifyGuardBinary()
        {
            try
            {
                if (!File.Exists(Paths.GuardExe) || !File.Exists(Paths.GuardHash)) return false;
                string want = File.ReadAllText(Paths.GuardHash).Trim();
                string now = Native.Sha256(Paths.GuardExe);
                return want.Length > 0 && string.Equals(want, now, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool RegisterTask(out string output)
        {
            output = "";
            if (!PinBinary(out output)) return false;
            string xml = TaskXml.Replace("{0}", XmlEscape(Paths.GuardExe)).Replace("{1}", "--watch");
            string xmlPath = Path.Combine(Paths.BinDir, "guard_task.xml");
            try { File.WriteAllText(xmlPath, xml, new UnicodeEncoding(false, true)); }
            catch (Exception e) { output = "không ghi được tệp tác vụ: " + e.Message; return false; }

            int code = Native.Run(Native.SysExe("schtasks.exe"),
                "/Create /XML \"" + xmlPath + "\" /TN \"" + AppInfo.TaskName + "\" /F",
                30000, out output);
            if (code == 0)
            {
                try { File.Delete(xmlPath); } catch { }
                return true;
            }

            // Một số máy không nhận tệp XML (khác biệt quyền tài khoản SYSTEM):
            // thử tạo bằng tham số dòng lệnh rồi mới báo lỗi.
            string xmlError = output;
            string simple = "/Create /TN \"" + AppInfo.TaskName + "\" /SC ONSTART /RU SYSTEM /RL HIGHEST /TR \"\\\""
                            + Paths.GuardExe + "\\\" --watch\" /F";
            int simpleCode = Native.Run(Native.SysExe("schtasks.exe"), simple, 30000, out output);
            if (simpleCode == 0) return true;

            output = "XML(" + code + "): " + (xmlError ?? "").Trim()
                     + " | SIMPLE(" + simpleCode + "): " + (output ?? "").Trim();
            return false;
        }

        // -------------------------------------------------- biểu tượng khay

        private static string CurrentUserSid()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                    return id.User == null ? "" : id.User.Value;
            }
            catch { return ""; }
        }

        public static bool TrayTaskExists()
        {
            if (!Native.IsAdmin()) return false;
            string output;
            return Native.Run(Native.SysExe("schtasks.exe"), "/Query /TN \"" + AppInfo.TrayTaskName + "\"", 15000, out output) == 0;
        }

        private static bool RegisterTrayTask(out string output)
        {
            output = "";
            string sid = CurrentUserSid();
            if (sid.Length == 0) { output = "không xác định được tài khoản người dùng"; return false; }

            string xml = TrayTaskXml.Replace("{0}", XmlEscape(Paths.GuardExe)).Replace("{1}", sid);
            string xmlPath = Path.Combine(Paths.BinDir, "tray_task.xml");
            try { File.WriteAllText(xmlPath, xml, new UnicodeEncoding(false, true)); }
            catch (Exception e) { output = "không ghi được tệp tác vụ: " + e.Message; return false; }

            bool made = Native.Run(Native.SysExe("schtasks.exe"),
                "/Create /XML \"" + xmlPath + "\" /TN \"" + AppInfo.TrayTaskName + "\" /F",
                30000, out output) == 0;
            try { File.Delete(xmlPath); } catch { }
            return made;
        }

        private static void StartTray()
        {
            string output;
            if (!RegisterTrayTask(out output))
            {
                Log.Write(S.Log.TrayCreateFailed((output ?? "").Trim()), "WARN");
                return;
            }
            Native.Run(Native.SysExe("schtasks.exe"), "/Run /TN \"" + AppInfo.TrayTaskName + "\"", 20000, out output);
        }

        private static void StopTray()
        {
            string output;
            Native.Run(Native.SysExe("schtasks.exe"), "/End /TN \"" + AppInfo.TrayTaskName + "\"", 20000, out output);
            Native.Run(Native.SysExe("schtasks.exe"), "/Delete /TN \"" + AppInfo.TrayTaskName + "\" /F", 20000, out output);
        }

        // Bảo đảm có biểu tượng khay khi bảo vệ nền đang chạy (kể cả bản cài từ trước).
        public static void EnsureTray()
        {
            if (!Native.IsAdmin()) return;
            if (HeartbeatAge() >= 30) return;

            if (!TrayTaskExists())
            {
                string created;
                if (!RegisterTrayTask(out created))
                {
                    Log.Write(S.Log.TrayCreateFailed((created ?? "").Trim()), "WARN");
                    return;
                }
            }
            string output;
            Native.Run(Native.SysExe("schtasks.exe"), "/Run /TN \"" + AppInfo.TrayTaskName + "\"", 20000, out output);
        }

        public static double HeartbeatAge()
        {
            try { return (DateTime.Now - File.GetLastWriteTime(Paths.Heartbeat)).TotalSeconds; }
            catch { return 1e9; }
        }

        public static void WriteHeartbeat()
        {
            try
            {
                Paths.EnsureDirs();
                File.WriteAllText(Paths.Heartbeat, DateTime.Now.Ticks.ToString());
            }
            catch { }
        }

        // ------------------------------------------------------------- cài đặt

        public static bool Install(bool blockWsh, Action<string> log)
        {
            if (!Native.IsAdmin())
            {
                if (log != null) log(S.Cli.AdminRequired);
                return false;
            }
            Paths.EnsureDirs();
            Paths.HardenRoot();
            // Giữ lựa chọn chặn ghi / chặn thực thi đã bật trong GUI; --install
            // không được âm thầm tắt chúng khi cài lại hoặc cập nhật.
            GuardConfig prior = LoadConfig();
            RemoveLegacyTask();
            EnsureSnapshot();
            if (log != null) log(S.Cli.SnapshotSaved);

            if (ApplyMachinePolicy(blockWsh))
            {
                if (log != null) log(S.Cli.PolicyApplied);
            }
            else
            {
                if (log != null) log(S.Cli.PolicyPartial);
            }
            SaveConfig(true, true, blockWsh, true, prior.WriteProtect, prior.BlockExec);
            RegistryGuard.Ensure(true);

            string output;
            bool taskOk = RegisterTask(out output);
            if (log != null) log(taskOk ? S.Cli.TaskCreated : S.Cli.TaskFailed);
            if (!taskOk && output != null && output.Trim().Length > 0)
                Log.Write(S.Log.TaskCreateFailed(output.Trim()), "ERROR");

            try { File.Delete(Paths.StopFlag); }
            catch { }
            if (taskOk)
            {
                // Pin bản GUI đã cài để named pipe chỉ nhận đúng tệp này.
                if (!PipeAuth.PinClient(Paths.GuardExe))
                    Log.Write(S.Log.PipeError("cannot pin client"), "WARN");
                Native.Run(Native.SysExe("schtasks.exe"), "/Run /TN \"" + AppInfo.TaskName + "\"", 20000, out output);
                bool alive = false;
                for (int i = 0; i < 16; i++)
                {
                    if (HeartbeatAge() < 20) { alive = true; break; }
                    Thread.Sleep(1000);
                }
                if (log != null) log(alive ? S.Cli.GuardStarted : S.Cli.GuardStarting);
            }

            Log.Write(S.Log.ProtectionOn(blockWsh, taskOk));
            return taskOk;
        }

        public static bool Uninstall(Action<string> log)
        {
            if (!Native.IsAdmin())
            {
                if (log != null) log(S.Cli.AdminRequired);
                return false;
            }
            RemoveLegacyTask();
            try { File.WriteAllText(Paths.StopFlag, "stop"); }
            catch { }
            for (int i = 0; i < 12; i++)
            {
                if (HeartbeatAge() > 15) break;
                Thread.Sleep(500);
            }
            string output;
            Native.Run(Native.SysExe("schtasks.exe"), "/End /TN \"" + AppInfo.TaskName + "\"", 20000, out output);
            bool deleted = Native.Run(Native.SysExe("schtasks.exe"), "/Delete /TN \"" + AppInfo.TaskName + "\" /F",
                                      20000, out output) == 0;
            StopTray();
            if (log != null) log(deleted ? S.Cli.TaskRemoved : S.Cli.TaskMissing);

            bool restored = RestoreFromSnapshot();
            if (log != null) log(restored ? S.Cli.PolicyRestored : S.Cli.PolicyRestoreFailed);

            // Trả lại giá trị StorageDevicePolicies trước khi xoá guard.cfg và
            // repair\write-protect.txt; nếu không, WriteProtect=1 sẽ kẹt lại.
            if (!Policy.ApplyWriteProtect(false))
                Log.Write(S.Log.WriteProtect(false, false), "WARN");

            try { File.Delete(Paths.StopFlag); } catch { }
            try { File.Delete(Paths.Heartbeat); } catch { }
            try { File.Delete(Paths.GuardConfig); } catch { }
            PipeAuth.ClearPin();
            if (!IsSamePath(SelfPath(), Paths.GuardExe))
            {
                try { File.Delete(Paths.GuardExe); } catch { }
                try { File.Delete(Paths.GuardHash); } catch { }
            }
            if (log != null) log(S.Cli.ProtectOffDone);
            Log.Write(S.Log.ProtectionOff);
            return true;
        }

        // --------------------------------------------------------------- trạng thái

        public static StatusInfo GetStatus()
        {
            Paths.EnsureDirs();
            StatusInfo st = new StatusInfo();
            st.Admin = Native.IsAdmin();
            st.Task = TaskExists();
            object value;
            RegistryValueKind kind;
            st.Autorun = Reg.Read(RegistryHive.LocalMachine, ExplorerHklm, "NoDriveTypeAutoRun",
                                  out value, out kind) && Convert.ToInt32(value) == 255;
            st.Ini = Reg.Read(RegistryHive.LocalMachine, IniPath, "", out value, out kind)
                     && string.Equals(Convert.ToString(value), "@SYS:DoesNotExist",
                                      StringComparison.OrdinalIgnoreCase);
            st.Wsh = Reg.Read(RegistryHive.LocalMachine, WshPath, "Enabled", out value, out kind)
                     && Convert.ToInt32(value) == 0;
            st.Registry = LoadConfig().Registry;
            GuardConfig cfg = LoadConfig();
            st.WriteProtect = cfg.WriteProtect &&
                              (Policy.WriteProtectActive || Policy.PendingPause);
            st.BlockExec = cfg.BlockExec;
            st.Running = HeartbeatAge() < 30;
            foreach (BatchInfo b in Quarantine.ListBatches())
            {
                st.QuarantineFiles += b.Count;
                st.QuarantineBatches++;
            }
            return st;
        }

        // -------------------------------------------------------------- chặn autorun

        public static bool Vaccinate(string drive, out bool existed, out string error)
        {
            existed = false;
            error = "";
            if (string.IsNullOrEmpty(drive) || drive.Length < 2)
            {
                error = S.Cli.NoDriveSelected;
                return false;
            }
            string path = Path.Combine(drive, "autorun.inf");
            if (Directory.Exists(path))
            {
                existed = true;
                return true;
            }
            if (File.Exists(path))
            {
                error = S.Err.AutrunConflict;
                return false;
            }
            try
            {
                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, "readme.txt"),
                    "This folder was created by ShieldGate to stop viruses from writing autorun.inf.\r\n" +
                    "Do not delete it if you still use this USB drive on other computers.\r\n\r\n" +
                    "Thư mục này do ShieldGate tạo để chặn virus ghi tệp autorun.inf.\r\n" +
                    "Không xoá thư mục này nếu vẫn dùng USB trên máy tính khác.\r\n",
                    new UTF8Encoding(true));
                Log.Write(S.Log.AutorunBlocked(drive));
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        // ---------------------------------------------------------------- chạy nền

        public static int RunWatch()
        {
            Mutex mutex = null;
            try
            {
                mutex = new Mutex(false, AppInfo.MutexName);
                if (!mutex.WaitOne(0))
                {
                    Log.Write(S.Log.WatchAlreadyRunning, "WARN");
                    return 0;
                }
            }
            catch { }

            try
            {
                if (!Native.IsAdmin())
                {
                    Log.Write(S.Log.WatchNeedsAdmin, "ERROR");
                    return 1;
                }
                if (IsSamePath(SelfPath(), Paths.GuardExe) && !VerifyGuardBinary())
                {
                    Log.Write(S.Log.WatchFingerprintMismatch, "ERROR");
                    return 2;
                }

                Paths.EnsureDirs();
                try { File.Delete(Paths.StopFlag); } catch { }
                Log.Write(S.Log.WatchStarted(AppInfo.Version));
                WriteHeartbeat();

                HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> scanning = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> warnedBlocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int[] deviceBusy = new int[1];
                DateTime lastPolicy = DateTime.MinValue;
                DateTime lastBeat = DateTime.MinValue;
                DateTime lastRoot = DateTime.MinValue;
                DateTime lastDevices = DateTime.MinValue;
                DateTime lastExec = DateTime.MinValue;
                GuardConfig lastConfig = null;

                BehaviorMonitor behavior = new BehaviorMonitor();
                behavior.Suspicious += delegate(string drive) { StartVolumeScan(scanning, drive); };

                PipeServer pipe = null;
                if (Settings.PipeEnabled)
                {
                    try
                    {
                        pipe = new PipeServer(PipeProtocol.Name, null);
                        pipe.Start();
                        Log.Write(S.Log.PipeStarted);
                    }
                    catch (Exception e)
                    {
                        Log.Write(S.Log.PipeError(e.Message), "WARN");
                        pipe = null;
                    }
                }

                try
                {
                // Neu lan chay truoc bi kill khi dang tam mo khoa de cach ly,
                // bat khoa lai ngay khi tien trinh bao ve khoi dong.
                Policy.RecoverPending();
                while (true)
                {
                    if (File.Exists(Paths.StopFlag)) break;
                    try
                    {
                        Policy.RecoverPending();
                        DateTime now = DateTime.Now;
                        if ((now - lastPolicy).TotalSeconds >= 60)
                        {
                            // Chỉ áp phần người dùng đang bật; công tắc đã tắt phải ở yên.
                            lastConfig = LoadConfig();
                            ApplyConfig(lastConfig);
                            lastPolicy = now;
                        }
                        if ((now - lastBeat).TotalSeconds >= 4)
                        {
                            WriteHeartbeat();
                            lastBeat = now;
                        }

                        List<string> drives = Native.RemovableDrives();
                        behavior.Update(drives);
                        foreach (string vol in drives)
                        {
                            if (known.Contains(vol)) continue;
                            known.Add(vol);
                            StartVolumeScan(scanning, vol);
                        }

                        // O da cam van co the bi virus tren may ghi lai shortcut sau lan quet dau.
                        // Kiem tra lai phan goc o moi 10 giay de don ngay khi no xuat hien.
                        if ((now - lastRoot).TotalSeconds >= 10)
                        {
                            lastRoot = now;
                            foreach (string vol in drives) StartRootCheck(scanning, vol);
                        }

                        // Kiem ke thiet bi: chi doc WMI, chay o luong rieng de khong treo vong lap.
                        if ((now - lastDevices).TotalSeconds >= 15)
                        {
                            lastDevices = now;
                            StartDeviceCheck(deviceBusy, warnedBlocked);
                        }

                        // Chặn thực thi từ ổ rời là opt-in; kiểm tra mỗi 4 giây.
                        if ((now - lastExec).TotalSeconds >= 4)
                        {
                            lastExec = now;
                            if (lastConfig != null && lastConfig.BlockExec)
                            {
                                string detail;
                                Policy.StopRemovableProcesses(drives, out detail);
                            }
                        }

                        known.RemoveWhere(delegate(string v) { return !drives.Contains(v); });
                        lock (ReadOnlyLogged)
                            ReadOnlyLogged.RemoveWhere(delegate(string v) { return !drives.Contains(v); });
                    }
                    catch (Exception e) { Log.Write(S.Log.WatchLoopError(e.Message), "ERROR"); }
                    Thread.Sleep(2000);
                }
                }
                finally
                {
                    if (pipe != null)
                    {
                        try { pipe.Stop(); } catch { }
                        Log.Write(S.Log.PipeStopped);
                    }
                    behavior.Stop();
                }

                Log.Write(S.Log.WatchStopped);
                try { File.Delete(Paths.StopFlag); } catch { }
                try { mutex.ReleaseMutex(); } catch { }
                return 0;
            }
            finally
            {
                if (mutex != null) { try { mutex.Dispose(); } catch { } }
            }
        }

        private static void StartVolumeScan(HashSet<string> scanning, string vol)
        {
            lock (scanning)
            {
                if (scanning.Contains(vol)) return;
                scanning.Add(vol);
            }
            string volume = vol;
            Thread t = new Thread(delegate()
            {
                try { ScanAndClean(volume); }
                catch (Exception e) { Log.Write(S.Log.VolumeError(volume, e.Message), "ERROR"); }
                finally { lock (scanning) scanning.Remove(volume); }
            });
            t.IsBackground = true;
            t.Start();
        }

        // ------------------------------------------------------ kiểm kê thiết bị

        private static void StartDeviceCheck(int[] busy, HashSet<string> warnedBlocked)
        {
            if (busy == null) return;
            if (Interlocked.CompareExchange(ref busy[0], 1, 0) != 0) return;
            Thread t = new Thread(delegate()
            {
                try { CheckDevices(warnedBlocked); }
                catch (Exception e) { Log.Write(S.Log.DeviceScanError(e.Message), "WARN"); }
                finally { busy[0] = 0; }
            });
            t.IsBackground = true;
            t.Start();
        }

        // WMI chạy ở luồng riêng. Lần đầu chỉ lập kho, không báo cả máy là thiết bị mới.
        private static void CheckDevices(HashSet<string> warnedBlocked)
        {
            DeviceScanResult ds = Devices.Scan();
            if (ds == null) return;
            if (!string.IsNullOrEmpty(ds.Error))
            {
                Log.Write(S.Log.DeviceScanError(ds.Error), "WARN");
                return;
            }

            if (ds.FirstRun)
            {
                Log.Write(S.Log.DeviceBaseline(ds.Devices.Count));
                Events.Add(Events.KindDevice, "INFO", S.Events.DeviceBaseline(ds.Devices.Count), "");
            }

            foreach (DeviceInfo d in ds.NewDevices)
            {
                string ids = d.HardwareId;
                if (ids.Length == 0) ids = d.InstanceId;
                Log.Write(S.Log.DeviceNew(d.Name, d.DeviceClass));
                Events.Add(Events.KindDevice, "INFO", d.Name,
                           S.Events.DeviceNew(d.Name, d.DeviceClass, ids));

                if (d.CompositeSuspect)
                {
                    Log.Write(S.Log.DeviceComposite(d.Name), "WARN");
                    Events.Add(Events.KindDevice, "WARN", d.Name, S.Events.DeviceComposite(d.Name));
                }

                // Chỉ là heuristic: bàn phím mới + shell trong 20 giây. Không tự tắt thiết bị.
                if (d.Keyboard && ds.NewKeyboard && !Devices.IsInternalKeyboard(d))
                {
                    int pid;
                    string shell = Devices.RecentShellProcess(DateTime.Now.AddSeconds(-20), out pid);
                    if (shell.Length > 0)
                    {
                        Log.Write(S.Log.DeviceBadUsb(d.Name, shell), "WARN");
                        Events.Add(Events.KindDevice, "WARN", d.Name, S.Events.DeviceBadUsb(d.Name));
                    }
                }
            }

            HashSet<string> present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DeviceInfo d in ds.Devices)
            {
                present.Add(d.Key);
                if (d.Trust != DeviceTrust.Blocked) continue;
                bool warn = false;
                lock (warnedBlocked)
                {
                    if (!warnedBlocked.Contains(d.Key))
                    {
                        warnedBlocked.Add(d.Key);
                        warn = true;
                    }
                }
                if (warn)
                {
                    Log.Write(S.Log.DeviceBlocked(d.Name), "WARN");
                    Events.Add(Events.KindDevice, "WARN", d.Name, S.Events.DeviceBlocked(d.Name));
                }
            }
            lock (warnedBlocked)
                warnedBlocked.RemoveWhere(delegate(string k) { return !present.Contains(k); });
        }

        private static void StartRootCheck(HashSet<string> scanning, string vol)        {
            lock (scanning)
            {
                if (scanning.Contains(vol)) return;
                scanning.Add(vol);
            }
            string volume = vol;
            Thread t = new Thread(delegate()
            {
                try { CleanRoot(volume); }
                catch (Exception e) { Log.Write(S.Log.VolumeError(volume, e.Message), "ERROR"); }
                finally { lock (scanning) scanning.Remove(volume); }
            });
            t.IsBackground = true;
            t.Start();
        }

        // Chi quet phan goc o: du de bat shortcut/thu muc gia vua bi ghi lai.
        private static void CleanRoot(string volume)
        {
            if (SkipReadOnly(volume)) return;
            // GUI co the dang quet/cach ly cung o: bo qua vong nay thay vi
            // quet lai toan bo thu muc gia roi bao "khong tim thay tep".
            if (CleanLock.IsBusy()) return;
            UsbScanResult root = UsbScan.DetectRoot(volume);
            List<UsbItem> auto = new List<UsbItem>();
            foreach (UsbItem it in root.Items)
                if (it.High) auto.Add(it);
            if (auto.Count == 0) return;

            int failed = 0, stopped = 0;
            string batch = "";
            int done = 0;
            // Apply tra ve so tep da di chuyen, khong phai so muc da chon:
            // truoc day "auto.Count - failed" cho ra so am (-67).
            // UsbScan.Apply tu tam mo khoa ghi va bat lai trong finally.
            done = UsbScan.Apply(root, auto, out failed, out batch, out stopped, null);
            int unhidden = 0;
            if (done > 0)
            {
                unhidden = UsbScan.Unhide(volume, null);
                if (ReinfectWatch.Observe(volume, done, DateTime.Now)) ReportReinfection(volume);
            }
            Log.Write(S.Log.AutoScan(volume, root.Files, root.Items.Count, done, failed, unhidden, stopped));
        }

        private static void ScanAndClean(string volume)
        {
            if (SkipReadOnly(volume)) return;
            if (CleanLock.IsBusy()) return;
            UsbScanResult scan = UsbScan.Detect(volume, null, null);
            if (scan.Truncated)
                Log.Write(S.Log.WatchScanTruncated(volume), "WARN");

            // Chỉ tự cách ly dấu hiệu mạnh. Mục yếu để người dùng xem, tránh xoá nhầm.
            List<UsbItem> auto = new List<UsbItem>();
            int review = 0;
            foreach (UsbItem it in scan.Items)
            {
                if (it.High) auto.Add(it);
                else review++;
            }

            int done = 0, failed = 0, stopped = 0;
            string batch = "";
            if (auto.Count > 0)
            {
                done = UsbScan.Apply(scan, auto, out failed, out batch, out stopped, null);
                review += failed;
            }
            int unhidden = 0;
            if (done > 0)
            {
                unhidden = UsbScan.Unhide(volume, null);
                if (ReinfectWatch.Observe(volume, done, DateTime.Now)) ReportReinfection(volume);
            }
            Settings.SetDriveScan(volume, scan.Files, review);
            // Sự kiện có ID cho tray: một lần quét có thể sinh nhiều thông báo
            // trong cùng 5 giây, mốc thời gian trong settings.txt làm mất bớt.
            if (done > 0 || review > 0)
                Events.Add(Events.KindDrive, done > 0 ? "INFO" : "WARN", volume,
                           S.Events.DriveScan(volume, done, review));
            Log.Write(S.Log.AutoScan(volume, scan.Files, scan.Items.Count, done,
                                      review, unhidden, stopped));
        }

        // Cùng một ổ bị nhiễm lại vài lần trong vài phút: nguồn lây đang chạy trên
        // máy chứ không nằm trên ổ. Báo rõ thay vì lặng lẽ dọn tiếp mỗi 10 giây.
        public static void ReportReinfection(string volume)
        {
            bool afterFix = false;
            try
            {
                DateTime t = Settings.PcFixEventTime;
                afterFix = t != DateTime.MinValue
                           && (DateTime.Now - t).TotalMinutes < 60;
            }
            catch { }

            // Sửa máy rồi mà ổ vẫn nhiễm lại: đừng bảo người dùng sửa lại lần nữa.
            // Nguồn lây nằm ngoài các mục đã thấy (driver/rootkit hoặc tiến trình
            // được bảo vệ), chỉ quét offline mới xử lý được.
            if (afterFix)
            {
                Log.Write(S.Log.DriveReinfectedAfterFix(volume), "ERROR");
                try { Events.Add(Events.KindReinfect, "ERROR", volume, S.Events.DriveReinfected(volume)); }
                catch { }
            }
            else
            {
                Log.Write(S.Log.DriveReinfected(volume), "WARN");
                try { Events.Add(Events.KindReinfect, "WARN", volume, S.Events.DriveReinfected(volume)); }
                catch { }
            }
            HuntSource(volume);
        }

        private static int _hunting;
        private static DateTime _lastHunt = DateTime.MinValue;

        // Quét máy ngay khi phát hiện vòng lặp nhiễm lại, chỉ đọc và ghi nhật ký.
        // Nhờ vậy log có tên tiến trình/tác vụ/dịch vụ cụ thể, kể cả tác vụ ẩn
        // dưới \Microsoft\..., thay vì bắt người dùng tự đoán.
        private static void HuntSource(string volume)
        {
            if ((DateTime.Now - _lastHunt).TotalMinutes < 10) return;
            if (Interlocked.CompareExchange(ref _hunting, 1, 0) != 0) return;
            _lastHunt = DateTime.Now;

            Thread t = new Thread(delegate()
            {
                try
                {
                    List<PcItem> found = PcScan.Detect(null, null);
                    if (found.Count > 0)
                    {
                        Log.Write(S.Log.HuntFound(volume, found.Count), "WARN");
                        PcScan.LogFindings(found);
                    }
                    else Log.Write(S.Log.HuntClean(volume));
                }
                catch (Exception e)
                {
                    try { Log.Write(S.Log.HuntError(e.Message), "ERROR"); }
                    catch { }
                }
                finally { Interlocked.Exchange(ref _hunting, 0); }
            });
            t.IsBackground = true;
            try { t.Start(); }
            catch { Interlocked.Exchange(ref _hunting, 0); }
        }

        public static bool StartWatchNow()
        {
            if (!Native.IsAdmin()) return false;
            string output;
            return Native.Run(Native.SysExe("schtasks.exe"), "/Run /TN \"" + AppInfo.TaskName + "\"",
                              20000, out output) == 0;
        }

        // ------------------------------------------------------ bật/tắt từng phần

        private static bool RestoreOne(string[] item)
        {
            try
            {
                List<string[]> rows = LoadSnapshot();
                foreach (string[] p in rows)
                {
                    if (!string.Equals(p[0], item[0], StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(p[1], item[1], StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(p[2], item[2], StringComparison.Ordinal)) continue;

                    bool exists = p[3] == "1";
                    RegistryValueKind kind = (RegistryValueKind)int.Parse(p[4]);
                    string value = Encoding.UTF8.GetString(Convert.FromBase64String(p[5]));
                    if (!exists) return Reg.DeleteValue(Hive(item[0]), item[1], item[2]);
                    object v = value;
                    if (kind == RegistryValueKind.DWord) v = int.Parse(value);
                    return Reg.Write(Hive(item[0]), item[1], item[2], v, kind);
                }
                return true;
            }
            catch { return false; }
        }

        private static bool RestoreGroup(int from, int to)
        {
            bool ok = true;
            for (int i = from; i <= to; i++) ok &= RestoreOne(SnapshotItems[i]);
            return ok;
        }

        // Áp dụng đúng năm lựa chọn của người dùng, phần nào tắt thì trả về giá trị gốc.
        // Riêng Registry: tắt chỉ ngừng theo dõi, không trả lại khoá đã hỏng.
        public static bool Configure(bool autorun, bool ini, bool wsh, bool registry,
                                     bool guardOn, Action<string> log,
                                     bool writeProtect = false, bool blockExec = false)
        {
            if (!Native.IsAdmin())
            {
                if (log != null) log(S.Cli.AdminRequired);
                return false;
            }
            Paths.EnsureDirs();
            Paths.HardenRoot();
            EnsureSnapshot();
            bool ok = true;

            // Ghi lựa chọn trước khi sửa registry: vòng lặp nền đọc guard.cfg mỗi
            // 60 giây. Nếu ghi sau, nó có thể đọc bản cũ và áp lại đúng phần vừa
            // được trả về, khiến công tắc người dùng vừa tắt tự bật lên.
            // Luôn ghi, kể cả khi tắt hết: SaveConfig giữ bản cũ trong guard.last
            // và ghi guard.cfg toàn 0 để UI phản ánh đúng trạng thái hiện tại.
            SaveConfig(autorun, ini, wsh, registry, writeProtect, blockExec);

            if (autorun)
            {
                ok &= Keep(RegistryHive.LocalMachine, ExplorerHklm, "NoDriveTypeAutoRun", 255, RegistryValueKind.DWord);
                ok &= Keep(RegistryHive.LocalMachine, ExplorerHklm, "NoAutorun", 1, RegistryValueKind.DWord);
                ok &= Keep(RegistryHive.LocalMachine, AutoplayHklm, "DisableAutoplay", 1, RegistryValueKind.DWord);
                ok &= Keep(RegistryHive.CurrentUser, ExplorerHkcu, "NoDriveTypeAutoRun", 255, RegistryValueKind.DWord);
                ok &= Keep(RegistryHive.CurrentUser, AutoplayHkcu, "DisableAutoplay", 1, RegistryValueKind.DWord);
            }
            else ok &= RestoreGroup(0, 4);

            if (ini)
            {
                ok &= Keep(RegistryHive.LocalMachine, IniPath, "", "@SYS:DoesNotExist",
                           RegistryValueKind.String);
            }
            else ok &= RestoreGroup(5, 5);

            if (wsh)
            {
                ok &= Keep(RegistryHive.LocalMachine, WshPath, "Enabled", 0, RegistryValueKind.DWord);
                ok &= Keep(RegistryHive.LocalMachine, WshPath32, "Enabled", 0, RegistryValueKind.DWord);
            }
            else ok &= RestoreGroup(6, 7);

            if (registry) ok &= RegistryGuard.Ensure(true);
            if (Native.IsAdmin()) ok &= Policy.ApplyWriteProtect(writeProtect);

            string output = "";
            if (guardOn)
            {
                bool created = RegisterTask(out output);
                ok &= created;
                if (!created)
                    Log.Write(S.Log.GuardTaskCreateFailed((output ?? "").Trim()), "ERROR");
                if (created)
                {
                    try { File.Delete(Paths.StopFlag); } catch { }
                    Native.Run(Native.SysExe("schtasks.exe"), "/Run /TN \"" + AppInfo.TaskName + "\"",
                               20000, out output);
                    for (int i = 0; i < 10; i++)
                    {
                        if (HeartbeatAge() < 20) break;
                        Thread.Sleep(600);
                    }
                    StartTray();
                }
            }
            else
            {
                try { File.WriteAllText(Paths.StopFlag, "stop"); } catch { }
                Native.Run(Native.SysExe("schtasks.exe"), "/End /TN \"" + AppInfo.TaskName + "\"", 20000, out output);
                Native.Run(Native.SysExe("schtasks.exe"), "/Delete /TN \"" + AppInfo.TaskName + "\" /F", 20000, out output);
                StopTray();
                try { File.Delete(Paths.StopFlag); } catch { }
                try { File.Delete(Paths.Heartbeat); } catch { }
            }

            Log.Write(S.Log.SettingsApplied(autorun, ini, wsh, registry, guardOn, ok));
            if (log != null) log(ok ? S.Cli.Configured : S.Cli.ConfigurePartial);
            return ok;
        }
    }
}
