// ShieldGate 2.0 - chinh sach bao ve cong: chan ghi o USB va chan chay tep tu o roi.
// Chan ghi dung khoa StorageDevicePolicies cua Windows; truoc khi bat luon luu
// gia tri cu de tat la tra lai dung trang thai, khong de lai thay doi chet.
// Chan chay khong dung driver: chi theo doi tien trinh dang chay tu o roi va dung
// tien trinh do. Day la lop phong thu theo thoi diem, khong phai chan tuyet doi.
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            lock (Gate)
            {
                if (!Native.IsAdmin()) return false;
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
                        // Không tự xoá để tránh mở khoá ngoài ý muốn.
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
                    string exe = "";
                    try { exe = p.MainModule.FileName; }
                    catch { continue; }
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
