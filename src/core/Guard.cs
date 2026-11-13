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
        public bool Task;
        public bool Running;
        public int QuarantineFiles;
        public int QuarantineBatches;

        // Ba thiết lập chính đang có hiệu lực: chặn autorun, chặn autorun.inf, chạy nền.
        public bool FullyProtected { get { return Autorun && Ini && Running; } }

        public int EnabledCount
        {
            get
            {
                int n = 0;
                if (Autorun) n++;
                if (Ini) n++;
                if (Wsh) n++;
                if (Running) n++;
                return n;
            }
        }
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

        // ---------------------------------------------------------- chính sách

        public static bool ApplyMachinePolicy(bool? blockWsh)
        {
            bool ok = true;
            ok &= Reg.Write(RegistryHive.LocalMachine, ExplorerHklm, "NoDriveTypeAutoRun", 255, RegistryValueKind.DWord);
            ok &= Reg.Write(RegistryHive.LocalMachine, ExplorerHklm, "NoAutorun", 1, RegistryValueKind.DWord);
            ok &= Reg.Write(RegistryHive.LocalMachine, AutoplayHklm, "DisableAutoplay", 1, RegistryValueKind.DWord);
            ok &= Reg.Write(RegistryHive.LocalMachine, IniPath, "", "@SYS:DoesNotExist", RegistryValueKind.String);
            ok &= Reg.Write(RegistryHive.CurrentUser, ExplorerHkcu, "NoDriveTypeAutoRun", 255, RegistryValueKind.DWord);
            ok &= Reg.Write(RegistryHive.CurrentUser, AutoplayHkcu, "DisableAutoplay", 1, RegistryValueKind.DWord);
            if (blockWsh == true)
            {
                ok &= Reg.Write(RegistryHive.LocalMachine, WshPath, "Enabled", 0, RegistryValueKind.DWord);
                ok &= Reg.Write(RegistryHive.LocalMachine, WshPath32, "Enabled", 0, RegistryValueKind.DWord);
            }
            else if (blockWsh == false)
            {
                ok &= Reg.Write(RegistryHive.LocalMachine, WshPath, "Enabled", 1, RegistryValueKind.DWord);
                ok &= Reg.Write(RegistryHive.LocalMachine, WshPath32, "Enabled", 1, RegistryValueKind.DWord);
            }
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
            Native.Run("schtasks.exe", "/End /TN \"" + LegacyTaskName + "\"", 10000, out output);
            Native.Run("schtasks.exe", "/Delete /TN \"" + LegacyTaskName + "\" /F", 10000, out output);
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
            return Native.Run("schtasks.exe", "/Query /TN \"" + AppInfo.TaskName + "\"", 15000, out output) == 0;
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
                            if (!IsSamePath(p.MainModule.FileName, Paths.GuardExe)) continue;
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
        // Không khoá được thì vẫn chạy, nhưng ghi cảnh báo vào nhật ký.
        private static void HardenBinDir()
        {
            try
            {
                Paths.EnsureDirs();
                string output;
                Native.Run("icacls.exe", "\"" + Paths.BinDir + "\" /inheritance:r " +
                    "/grant:r \"*S-1-5-18:(OI)(CI)F\" \"*S-1-5-32-544:(OI)(CI)F\" " +
                    "\"*S-1-5-32-545:(OI)(CI)RX\"",
                    20000, out output);
            }
            catch { }
        }

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
                    HardenBinDir();
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
            Native.Run("schtasks.exe", "/End /TN \"" + AppInfo.TaskName + "\"", 20000, out output);
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
            string xmlPath = Path.Combine(Paths.Root, "guard_task.xml");
            try { File.WriteAllText(xmlPath, xml, new UnicodeEncoding(false, true)); }
            catch (Exception e) { output = "không ghi được tệp tác vụ: " + e.Message; return false; }

            int code = Native.Run("schtasks.exe",
                "/Create /XML \"" + xmlPath + "\" /TN \"" + AppInfo.TaskName + "\" /F",
                30000, out output);
            if (code == 0) return true;

            // Một số máy không nhận tệp XML (khác biệt quyền tài khoản SYSTEM):
            // thử tạo bằng tham số dòng lệnh rồi mới báo lỗi.
            string xmlError = output;
            string simple = "/Create /TN \"" + AppInfo.TaskName + "\" /SC ONSTART /RU SYSTEM /RL HIGHEST /TR \"\\\""
                            + Paths.GuardExe + "\\\" --watch\" /F";
            int simpleCode = Native.Run("schtasks.exe", simple, 30000, out output);
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
            return Native.Run("schtasks.exe", "/Query /TN \"" + AppInfo.TrayTaskName + "\"", 15000, out output) == 0;
        }

        private static bool RegisterTrayTask(out string output)
        {
            output = "";
            string sid = CurrentUserSid();
            if (sid.Length == 0) { output = "không xác định được tài khoản người dùng"; return false; }

            string xml = TrayTaskXml.Replace("{0}", XmlEscape(Paths.GuardExe)).Replace("{1}", sid);
            string xmlPath = Path.Combine(Paths.Root, "tray_task.xml");
            try { File.WriteAllText(xmlPath, xml, new UnicodeEncoding(false, true)); }
            catch (Exception e) { output = "không ghi được tệp tác vụ: " + e.Message; return false; }

            return Native.Run("schtasks.exe",
                "/Create /XML \"" + xmlPath + "\" /TN \"" + AppInfo.TrayTaskName + "\" /F",
                30000, out output) == 0;
        }

        private static void StartTray()
        {
            string output;
            if (!RegisterTrayTask(out output))
            {
                Log.Write("Không tạo được biểu tượng khay: " + (output ?? "").Trim(), "WARN");
                return;
            }
            Native.Run("schtasks.exe", "/Run /TN \"" + AppInfo.TrayTaskName + "\"", 20000, out output);
        }

        private static void StopTray()
        {
            string output;
            Native.Run("schtasks.exe", "/End /TN \"" + AppInfo.TrayTaskName + "\"", 20000, out output);
            Native.Run("schtasks.exe", "/Delete /TN \"" + AppInfo.TrayTaskName + "\" /F", 20000, out output);
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
                    Log.Write("Không tạo được biểu tượng khay: " + (created ?? "").Trim(), "WARN");
                    return;
                }
            }
            string output;
            Native.Run("schtasks.exe", "/Run /TN \"" + AppInfo.TrayTaskName + "\"", 20000, out output);
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

            string output;
            bool taskOk = RegisterTask(out output);
            if (log != null) log(taskOk ? S.Cli.TaskCreated : S.Cli.TaskFailed);
            if (!taskOk && output != null && output.Trim().Length > 0)
                Log.Write("Không tạo được tác vụ: " + output.Trim(), "ERROR");

            try { File.Delete(Paths.StopFlag); }
            catch { }
            if (taskOk)
            {
                Native.Run("schtasks.exe", "/Run /TN \"" + AppInfo.TaskName + "\"", 20000, out output);
                bool alive = false;
                for (int i = 0; i < 16; i++)
                {
                    if (HeartbeatAge() < 20) { alive = true; break; }
                    Thread.Sleep(1000);
                }
                if (log != null) log(alive ? S.Cli.GuardStarted : S.Cli.GuardStarting);
            }

            Log.Write("Bật bảo vệ (chặn script=" + blockWsh + ", tác vụ=" + taskOk + ")");
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
            Native.Run("schtasks.exe", "/End /TN \"" + AppInfo.TaskName + "\"", 20000, out output);
            bool deleted = Native.Run("schtasks.exe", "/Delete /TN \"" + AppInfo.TaskName + "\" /F",
                                      20000, out output) == 0;
            StopTray();
            if (log != null) log(deleted ? S.Cli.TaskRemoved : S.Cli.TaskMissing);

            bool restored = RestoreFromSnapshot();
            if (log != null) log(restored ? S.Cli.PolicyRestored : S.Cli.PolicyRestoreFailed);

            try { File.Delete(Paths.StopFlag); } catch { }
            try { File.Delete(Paths.Heartbeat); } catch { }
            if (!IsSamePath(SelfPath(), Paths.GuardExe))
            {
                try { File.Delete(Paths.GuardExe); } catch { }
                try { File.Delete(Paths.GuardHash); } catch { }
            }
            if (log != null) log(S.Cli.ProtectOffDone);
            Log.Write("Tắt bảo vệ");
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
                Log.Write("Chặn autorun: " + drive);
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
                    Log.Write("Bảo vệ nền đã chạy ở tiến trình khác, thoát.", "WARN");
                    return 0;
                }
            }
            catch { }

            try
            {
                if (!Native.IsAdmin())
                {
                    Log.Write("Bảo vệ nền cần quyền quản trị, thoát.", "ERROR");
                    return 1;
                }
                if (IsSamePath(SelfPath(), Paths.GuardExe) && !VerifyGuardBinary())
                {
                    Log.Write("Tệp chạy nền không khớp dấu vết đã cài. Không chạy.", "ERROR");
                    return 2;
                }

                Paths.EnsureDirs();
                try { File.Delete(Paths.StopFlag); } catch { }
                Log.Write("Bảo vệ nền bắt đầu (v" + AppInfo.Version + ")");
                WriteHeartbeat();

                HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> scanning = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                DateTime lastPolicy = DateTime.MinValue;
                DateTime lastBeat = DateTime.MinValue;

                while (true)
                {
                    if (File.Exists(Paths.StopFlag)) break;
                    try
                    {
                        DateTime now = DateTime.Now;
                        if ((now - lastPolicy).TotalSeconds >= 60)
                        {
                            ApplyMachinePolicy(null);
                            lastPolicy = now;
                        }
                        if ((now - lastBeat).TotalSeconds >= 4)
                        {
                            WriteHeartbeat();
                            lastBeat = now;
                        }

                        List<string> drives = Native.RemovableDrives();
                        foreach (string vol in drives)
                        {
                            if (known.Contains(vol)) continue;
                            known.Add(vol);
                            lock (scanning)
                            {
                                if (scanning.Contains(vol)) continue;
                                scanning.Add(vol);
                            }
                            string volume = vol;
                            Thread t = new Thread(delegate()
                            {
                                try { ScanAndClean(volume); }
                                catch (Exception e) { Log.Write("Lỗi khi xử lý " + volume + ": " + e.Message, "ERROR"); }
                                finally { lock (scanning) scanning.Remove(volume); }
                            });
                            t.IsBackground = true;
                            t.Start();
                        }
                        known.RemoveWhere(delegate(string v) { return !drives.Contains(v); });
                    }
                    catch (Exception e) { Log.Write("Lỗi bảo vệ nền: " + e.Message, "ERROR"); }
                    Thread.Sleep(2000);
                }

                Log.Write("Bảo vệ nền dừng theo yêu cầu");
                try { File.Delete(Paths.StopFlag); } catch { }
                try { mutex.ReleaseMutex(); } catch { }
                return 0;
            }
            finally
            {
                if (mutex != null) { try { mutex.Dispose(); } catch { } }
            }
        }

        private static void ScanAndClean(string volume)
        {
            UsbScanResult scan = UsbScan.Detect(volume, null, null);
            int done = 0, failed = 0, stopped = 0;
            string batch = "";
            if (scan.Items.Count > 0)
            {
                UsbScan.Apply(scan, scan.Items, out failed, out batch, out stopped, null);
                done = scan.Items.Count - failed;
            }
            int unhidden = 0;
            if (done > 0) unhidden = UsbScan.Unhide(volume, null);
            Settings.SetDriveScan(volume, scan.Files, scan.Items.Count);
            Log.Write("Tự động kiểm tra " + volume + ": " + scan.Files + " tệp, "
                      + scan.Items.Count + " mục, cách ly " + done
                      + ", bỏ ẩn " + unhidden + ", dừng tiến trình " + stopped);
        }

        public static bool StartWatchNow()
        {
            if (!Native.IsAdmin()) return false;
            string output;
            return Native.Run("schtasks.exe", "/Run /TN \"" + AppInfo.TaskName + "\"",
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

        // Áp dụng đúng bốn lựa chọn của người dùng, phần nào tắt thì trả về giá trị gốc.
        public static bool Configure(bool autorun, bool ini, bool wsh, bool guardOn, Action<string> log)
        {
            if (!Native.IsAdmin())
            {
                if (log != null) log(S.Cli.AdminRequired);
                return false;
            }
            Paths.EnsureDirs();
            EnsureSnapshot();
            bool ok = true;

            if (autorun)
            {
                ok &= Reg.Write(RegistryHive.LocalMachine, ExplorerHklm, "NoDriveTypeAutoRun", 255, RegistryValueKind.DWord);
                ok &= Reg.Write(RegistryHive.LocalMachine, ExplorerHklm, "NoAutorun", 1, RegistryValueKind.DWord);
                ok &= Reg.Write(RegistryHive.LocalMachine, AutoplayHklm, "DisableAutoplay", 1, RegistryValueKind.DWord);
                ok &= Reg.Write(RegistryHive.CurrentUser, ExplorerHkcu, "NoDriveTypeAutoRun", 255, RegistryValueKind.DWord);
                ok &= Reg.Write(RegistryHive.CurrentUser, AutoplayHkcu, "DisableAutoplay", 1, RegistryValueKind.DWord);
            }
            else ok &= RestoreGroup(0, 4);

            if (ini)
            {
                ok &= Reg.Write(RegistryHive.LocalMachine, IniPath, "", "@SYS:DoesNotExist",
                                RegistryValueKind.String);
            }
            else ok &= RestoreGroup(5, 5);

            if (wsh)
            {
                ok &= Reg.Write(RegistryHive.LocalMachine, WshPath, "Enabled", 0, RegistryValueKind.DWord);
                ok &= Reg.Write(RegistryHive.LocalMachine, WshPath32, "Enabled", 0, RegistryValueKind.DWord);
            }
            else ok &= RestoreGroup(6, 7);

            string output = "";
            if (guardOn)
            {
                bool created = RegisterTask(out output);
                ok &= created;
                if (!created)
                    Log.Write("Không tạo được tác vụ bảo vệ: " + (output ?? "").Trim(), "ERROR");
                if (created)
                {
                    try { File.Delete(Paths.StopFlag); } catch { }
                    Native.Run("schtasks.exe", "/Run /TN \"" + AppInfo.TaskName + "\"",
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
                Native.Run("schtasks.exe", "/End /TN \"" + AppInfo.TaskName + "\"", 20000, out output);
                Native.Run("schtasks.exe", "/Delete /TN \"" + AppInfo.TaskName + "\" /F", 20000, out output);
                StopTray();
                try { File.Delete(Paths.StopFlag); } catch { }
                try { File.Delete(Paths.Heartbeat); } catch { }
            }

            Log.Write("Thiết lập bảo vệ: chặn tự chạy=" + autorun + ", chặn autorun.inf=" + ini
                      + ", chặn script=" + wsh + ", chạy nền=" + guardOn + " (ok=" + ok + ")");
            if (log != null) log(ok ? S.Cli.Configured : S.Cli.ConfigurePartial);
            return ok;
        }
    }
}
