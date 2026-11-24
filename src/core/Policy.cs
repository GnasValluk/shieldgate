// ShieldGate 2.0 - chinh sach bao ve cong: chan ghi o USB va chan chay tep tu o roi.
// Chan ghi dung khoa StorageDevicePolicies cua Windows; truoc khi bat luon luu
// gia tri cu de tat la tra lai dung trang thai, khong de lai thay doi chet.
// Chan chay khong dung driver: chi theo doi tien trinh dang chay tu o roi va dung
// tien trinh do. Day la lop phong thu theo thoi diem, khong phai chan tuyet doi.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace ShieldGate
{
    internal static class Policy
    {
        public const string StoragePolicies =
            @"SYSTEM\CurrentControlSet\Control\StorageDevicePolicies";

        private static readonly object Gate = new object();

        private static string BackupFile
        {
            get { return Path.Combine(Paths.RepairDir, "write-protect.txt"); }
        }

        // Ghi chu "dang tam mo khoa de cach ly": co han 10 phut va PID cua tien
        // trinh da mo. Neu ung dung bi kill, lan khoi dong sau (hoac vong lap bao
        // ve) doc tep nay de bat khoa lai thay vi de ho may khong duoc bao ve.
        private static string PendingFile
        {
            get { return Path.Combine(Paths.RepairDir, "wp-pending.txt"); }
        }

        public const int PauseMinutes = 10;

        // Self-test tat tam mo khoa: no chay tren thu muc tam nhung neu may dang
        // bat WriteProtect thi khong duoc doi registry.
        internal static bool PauseAllowed = true;

        public static bool WriteProtectActive
        {
            get
            {
                object value;
                RegistryValueKind kind;
                return Reg.Read(RegistryHive.LocalMachine, StoragePolicies, "WriteProtect",
                                out value, out kind)
                       && ToInt(value) == 1;
            }
        }

        // Bat/tat chan ghi toan cuc. Bat lan dau ghi lai gia tri cu; tat tra lai.
        // Khong co quyen quan tri thi tra false va khong doi registry.
        public static bool ApplyWriteProtect(bool on)
        {
            return ApplyWriteProtect(on, false);
        }

        private static bool ApplyWriteProtect(bool on, bool force)
        {
            lock (Gate)
            {
                if (!Native.IsAdmin()) return false;
                // Trong 10 phut tam mo de cach ly, vong lap bao ve khong duoc bat
                // khoa lai giua luc; chi Begin/End/Recover moi duoc doi trang thai.
                // Chieu tat van cho phep: nguoi dung tat cong tac thi phai ton trong.
                if (BlockedByPause(on, force, PendingUnexpired())) return true;
                try
                {
                    object value;
                    RegistryValueKind kind;
                    bool exists = Reg.Read(RegistryHive.LocalMachine, StoragePolicies,
                                           "WriteProtect", out value, out kind);
                    int current = exists ? ToInt(value) : -1;

                    if (on)
                    {
                        if (current == 1) return true;
                        if (!File.Exists(BackupFile)) SaveBackup(exists, exists ? ToInt(value) : 0);
                        bool ok = Reg.Write(RegistryHive.LocalMachine, StoragePolicies, "WriteProtect",
                                            1, RegistryValueKind.DWord);
                        Log.Write(S.Log.WriteProtect(true, ok), ok ? "INFO" : "WARN");
                        return ok;
                    }

                    if (!File.Exists(BackupFile))
                    {
                        // Không có bản sao: khoá này có thể do người dùng/Windows bật.
                        // Không tự xoá để tránh mở khoá ngoài ý muốn. Nhưng lệnh tắt
                        // của người dùng phải huỷ ghi chú tạm mở, nếu không vòng
                        // EndCleanPause sẽ bật khoá lại sau khi họ đã tắt.
                        if (!force)
                        {
                            try { File.Delete(PendingFile); } catch { }
                        }
                        return true;
                    }

                    bool hadValue;
                    int oldValue;
                    if (!LoadBackup(out hadValue, out oldValue)) return false;
                    bool restored;
                    if (hadValue)
                        restored = Reg.Write(RegistryHive.LocalMachine, StoragePolicies,
                                             "WriteProtect", oldValue, RegistryValueKind.DWord);
                    else
                        restored = Reg.DeleteValue(RegistryHive.LocalMachine, StoragePolicies,
                                                   "WriteProtect");
                    if (restored)
                    {
                        try { File.Delete(BackupFile); } catch { }
                        // Nguoi dung chu dong tat: huy ghi chu tam mo, neu khong
                        // EndCleanPause se bat lai khoa ngoai y muon.
                        if (!force)
                        {
                            try { File.Delete(PendingFile); } catch { }
                        }
                        Log.Write(S.Log.WriteProtect(false, true));
                    }
                    else Log.Write(S.Log.WriteProtect(false, false), "WARN");
                    return restored;
                }
                catch (Exception e)
                {
                    Log.Write(S.Log.PolicyError(e.Message), "WARN");
                    return false;
                }
            }
        }

        // ------------------------------------------------- WriteProtect ↔ làm sạch

        // Khoa do ShieldGate bat (co backup) thi moi tam mo duoc; khoa do nguoi
        // dung/Windows bat thi phai tat tay, khong tu y mo.
        public static bool CanPauseWriteProtect
        {
            get
            {
                try
                {
                    return Native.IsAdmin() && WriteProtectActive && File.Exists(BackupFile);
                }
                catch { return false; }
            }
        }

        // Dang tam mo de cach ly (con han): UI van hien cong tac la bat thay vi
        // nhay sang tat roi bat lai sau 10 phut.
        public static bool PendingPause
        {
            get { return PendingUnexpired(); }
        }

        // Goi truoc khi cach ly/khoi phuc tren USB. Tra true khi da tam mo khoa
        // (nguoi goi phai goi EndCleanPause trong finally).
        public static bool BeginCleanPause()
        {
            lock (Gate)
            {
                if (!PauseAllowed) return false;
                if (!CanPauseWriteProtect) return false;
                try
                {
                    Paths.EnsureDirs();
                    int pid = 0;
                    try { pid = Process.GetCurrentProcess().Id; }
                    catch { }
                    File.WriteAllText(PendingFile,
                        "pid=" + pid + "\r\n" +
                        "until=" + DateTime.UtcNow.AddMinutes(PauseMinutes)
                                           .ToString("o", CultureInfo.InvariantCulture) + "\r\n",
                        new UTF8Encoding(false));
                }
                catch { return false; }

                if (ApplyWriteProtect(false, true)) return true;
                // Khong tat duoc thi xoa ghi chu de khong ai tuong dang mo khoa.
                try { File.Delete(PendingFile); } catch { }
                return false;
            }
        }

        // Goi sau khi xong: bat lai khoa va xoa ghi chu. Tra true khi khoa da bat.
        public static bool EndCleanPause()
        {
            lock (Gate)
            {
                bool had;
                try { had = File.Exists(PendingFile); }
                catch { had = false; }
                if (!had) return true;
                if (!ApplyWriteProtect(true, true)) return false;
                try { File.Delete(PendingFile); } catch { }
                Log.Write(S.Log.WriteProtectRecovered);
                return true;
            }
        }

        // Goi luc khoi dong va moi vong lap bao ve. Chi bat lai khi ghi chu da het
        // han, bi hong, hoac tien trinh da mo khoa khong con song.
        public static void RecoverPending()
        {
            lock (Gate)
            {
                try
                {
                    if (!File.Exists(PendingFile)) return;
                    string text = File.ReadAllText(PendingFile);
                    int pid;
                    DateTime until;
                    bool parsed = ParsePending(text, out pid, out until);
                    bool expired = !parsed || until == DateTime.MinValue ||
                                   DateTime.UtcNow >= until;
                    if (!expired && pid > 0 && IsProcessAlive(pid)) return;

                    if (ApplyWriteProtect(true, true))
                    {
                        try { File.Delete(PendingFile); } catch { }
                        Log.Write(S.Log.WriteProtectRecovered);
                    }
                    else Log.Write(S.Log.WriteProtectRecoverFailed, "WARN");
                }
                catch (Exception e)
                {
                    Log.Write(S.Log.PolicyError(e.Message), "WARN");
                }
            }
        }

        // Thuan tuy de self-test kiem tra huong chan: chi chan chieu bat lai.
        internal static bool BlockedByPause(bool on, bool force, bool pending)
        {
            return !force && on && pending;
        }

        // Thuan tuy de self-test kiem tra han va du lieu hong: true = nen bat khoa.
        internal static bool PendingExpired(string content, DateTime nowUtc)
        {
            int pid;
            DateTime until;
            if (!ParsePending(content, out pid, out until)) return true;
            return until == DateTime.MinValue || nowUtc >= until;
        }

        private static bool PendingUnexpired()
        {
            try
            {
                if (!File.Exists(PendingFile)) return false;
                return !PendingExpired(File.ReadAllText(PendingFile), DateTime.UtcNow);
            }
            catch { return false; }
        }

        private static bool ParsePending(string text, out int pid, out DateTime until)
        {
            pid = 0;
            until = DateTime.MinValue;
            if (string.IsNullOrEmpty(text)) return false;
            string[] lines = text.Split(new string[] { "\r\n", "\n" },
                                        StringSplitOptions.None);
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                if (string.Equals(key, "pid", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(val, out pid);
                else if (string.Equals(key, "until", StringComparison.OrdinalIgnoreCase))
                    DateTime.TryParse(val, CultureInfo.InvariantCulture,
                                      DateTimeStyles.RoundtripKind, out until);
            }
            return pid > 0 || until != DateTime.MinValue;
        }

        private static bool IsProcessAlive(int pid)
        {
            try
            {
                using (Process p = Process.GetProcessById(pid))
                    return !p.HasExited;
            }
            catch { return false; }
        }

        private static void SaveBackup(bool exists, int value)
        {
            Paths.EnsureDirs();
            File.WriteAllText(BackupFile,
                "exists=" + (exists ? "1" : "0") + "\r\nvalue=" + value + "\r\n",
                new UTF8Encoding(false));
        }

        private static bool LoadBackup(out bool exists, out int value)
        {
            exists = false;
            value = 0;
            try
            {
                foreach (string raw in File.ReadAllLines(BackupFile))
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    if (string.Equals(key, "exists", StringComparison.OrdinalIgnoreCase))
                        exists = val == "1";
                    else if (string.Equals(key, "value", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(val, out value);
                }
                return true;
            }
            catch { return false; }
        }

        private static int ToInt(object value)
        {
            try { return Convert.ToInt32(value); }
            catch { return 0; }
        }

        // ------------------------------------------------- chan chay tu o roi

        public static bool IsRemovableRoot(string root, IList<string> drives)
        {
            if (string.IsNullOrEmpty(root) || drives == null) return false;
            foreach (string d in drives)
            {
                if (string.IsNullOrEmpty(d)) continue;
                if (string.Equals(Normalize(d), Normalize(root), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string s = path.Trim();
            if (s.Length == 2 && s[1] == ':') s += "\\";
            return s;
        }

        // Dung tien trinh co tep chay nam tren o roi. Bo qua chinh ShieldGate,
        // tien trinh he thong va o he thong. Tra ve so tien trinh da dung.
        public static int StopRemovableProcesses(IList<string> drives, out string detail)
        {
            detail = "";
            if (drives == null || drives.Count == 0) return 0;
            if (!Native.IsAdmin()) return 0;

            List<string> killed = new List<string>();
            int self = 0;
            try { self = Process.GetCurrentProcess().Id; }
            catch { }

            Process[] all = null;
            try { all = Process.GetProcesses(); }
            catch { return 0; }

            foreach (Process p in all)
            {
                if (p == null) continue;
                try
                {
                    if (p.Id == self) continue;
                    string exe = Native.ProcessImagePath(p.Id);
                    if (string.IsNullOrEmpty(exe)) continue;

                    string root = "";
                    try { root = Path.GetPathRoot(exe); }
                    catch { }
                    if (root.Length == 0) continue;
                    if (string.Equals(Normalize(root), Normalize(Paths.SystemDrive),
                                      StringComparison.OrdinalIgnoreCase)) continue;
                    if (!IsRemovableRoot(root, drives)) continue;

                    string name = "";
                    try { name = p.ProcessName + " (" + p.Id + ")"; }
                    catch { name = p.Id.ToString(); }
                    try
                    {
                        p.Kill();
                        killed.Add(name + " @ " + root);
                        Log.Write(S.Log.ProcessStopped(p.Id, root), "WARN");
                    }
                    catch { }
                }
                finally { try { p.Dispose(); } catch { } }
            }

            if (killed.Count > 0)
            {
                detail = string.Join("; ", killed.ToArray());
                Events.Add(Events.KindProtect, "WARN", S.Events.ExecBlocked,
                           killed.Count + ": " + detail);
            }
            return killed.Count;
        }
    }
}
