// ShieldGate 2.0 - chan doan va sua o an toan.
// Nguyen tac: moi thao tac ghi deu tu choi o he thong, co xem truoc, co sao luu,
// co xac nhan o giao dien. Boot sector chi doc de bao cao, khong bao gio ghi.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ShieldGate
{
    internal sealed class RepairInfo
    {
        public string Drive = "";
        public string Label = "";
        public string FileSystem = "";
        public long TotalBytes;
        public long FreeBytes;
        public bool Ready;
        public bool ReadOnly;
        public bool SystemDrive;
        public bool Dirty;
        public bool DirtyKnown;
        public string Problem = "";
    }

    internal sealed class ReadTestResult
    {
        public long Bytes;
        public int Files;
        public int Errors;
        public bool TimedOut;
        public string FirstError = "";
    }

    internal sealed class BootSectorInfo
    {
        public bool Read;
        public bool Signature55AA;
        public string FileSystem = "";
        public string Error = "";
    }

    internal sealed class BackupPlan
    {
        public bool Ok;
        public long Files;
        public long Bytes;
        public string Output = "";
        public string Error = "";
    }

    internal static class Repair
    {
        private const uint GenericRead = 0x80000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 3;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string name, uint access, uint share,
                                                         IntPtr security, uint disposition,
                                                         uint flags, IntPtr template);

        public static string NormalRoot(string drive)
        {
            if (string.IsNullOrEmpty(drive)) return "";
            string root = drive.Trim();
            try { root = Path.GetFullPath(root); }
            catch { }
            if (!root.EndsWith("\\")) root += "\\";
            return root;
        }

        // True khi phai tu choi moi thao tac ghi. Dung truoc chkdsk/backup.
        public static bool RefuseReason(string drive, out string reason)
        {
            reason = "";
            if (string.IsNullOrEmpty(drive) || drive.Trim().Length < 2)
            {
                reason = S.Repair.NoDrive;
                return true;
            }
            string root = NormalRoot(drive);
            if (root.Length < 3)
            {
                reason = S.Repair.InvalidDrive;
                return true;
            }
            if (string.Equals(root, NormalRoot(Paths.SystemDrive), StringComparison.OrdinalIgnoreCase))
            {
                reason = S.Repair.SystemDrive;
                return true;
            }
            try
            {
                DriveInfo di = new DriveInfo(root);
                if (!di.IsReady)
                {
                    reason = S.Repair.NotReady;
                    return true;
                }
                if (di.DriveType == DriveType.Network || di.DriveType == DriveType.CDRom)
                {
                    reason = S.Repair.UnsupportedDrive;
                    return true;
                }
            }
            catch (Exception e)
            {
                reason = e.Message;
                return true;
            }
            return false;
        }

        public static RepairInfo Inspect(string drive)
        {
            RepairInfo info = new RepairInfo();
            info.Drive = NormalRoot(drive);
            string reason;
            if (RefuseReason(drive, out reason)) info.Problem = reason;

            try
            {
                DriveInfo di = new DriveInfo(info.Drive);
                info.Ready = di.IsReady;
                if (info.Ready)
                {
                    info.Label = di.VolumeLabel;
                    info.FileSystem = di.DriveFormat;
                    info.TotalBytes = di.TotalSize;
                    info.FreeBytes = di.TotalFreeSpace;
                }
                info.SystemDrive = string.Equals(info.Drive, NormalRoot(Paths.SystemDrive),
                                                 StringComparison.OrdinalIgnoreCase);
                info.ReadOnly = Native.IsReadOnlyVolume(info.Drive);
            }
            catch (Exception e)
            {
                info.Problem = e.Message;
            }

            // fsutil dirty query chi doc co dirty bit; khong sua gi.
            try
            {
                string letter = info.Drive.Substring(0, 2);
                string output;
                int code = Native.Run("fsutil.exe", "dirty query " + letter, 15000, out output);
                if (code == 0 && output != null)
                {
                    string low = output.ToLowerInvariant();
                    if (low.IndexOf("not dirty", StringComparison.Ordinal) >= 0)
                    {
                        info.Dirty = false;
                        info.DirtyKnown = true;
                    }
                    else if (low.IndexOf("dirty", StringComparison.Ordinal) >= 0)
                    {
                        info.Dirty = true;
                        info.DirtyKnown = true;
                    }
                }
            }
            catch { }

            return info;
        }

        // Doc tuan tu, gioi han thoi gian va so byte; chi mo FileAccess.Read.
        public static ReadTestResult ReadTest(string drive, int seconds, long maxBytes)
        {
            ReadTestResult r = new ReadTestResult();
            if (seconds <= 0) seconds = 20;
            if (maxBytes <= 0) maxBytes = 64L * 1024 * 1024;
            DateTime end = DateTime.Now.AddSeconds(seconds);
            byte[] buffer = new byte[1024 * 1024];

            try
            {
                foreach (TreeItem item in Tree.Walk(NormalRoot(drive), null, 200000, seconds, 12))
                {
                    if (DateTime.Now >= end)
                    {
                        r.TimedOut = true;
                        break;
                    }
                    if (item.IsDir) continue;
                    try
                    {
                        using (FileStream fs = new FileStream(item.Path, FileMode.Open,
                                                              FileAccess.Read, FileShare.ReadWrite))
                        {
                            int n;
                            while ((n = fs.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                r.Bytes += n;
                                if (r.Bytes >= maxBytes || DateTime.Now >= end) break;
                            }
                        }
                        r.Files++;
                    }
                    catch (Exception e)
                    {
                        r.Errors++;
                        if (r.FirstError.Length == 0) r.FirstError = item.Path + ": " + e.Message;
                        if (r.Errors >= 25) break;
                    }
                    if (r.Bytes >= maxBytes || DateTime.Now >= end)
                    {
                        r.TimedOut = DateTime.Now >= end;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                r.Errors++;
                if (r.FirstError.Length == 0) r.FirstError = e.Message;
            }
            return r;
        }

        // Sao luu ca o vao thu muc repair. Chi xem truoc bang /L, khong ghi.
        public static BackupPlan PreviewBackup(string drive)
        {
            BackupPlan plan = new BackupPlan();
            string reason;
            if (RefuseReason(drive, out reason))
            {
                plan.Error = reason;
                return plan;
            }
            Paths.EnsureDirs();
            string dest = Path.Combine(Paths.RepairDir, "preview-" + DateTime.Now.ToString("HHmmss"));
            string output;
            try
            {
                string args = "\"" + NormalRoot(drive) + "\" \"" + dest +
                              "\" /L /E /COPY:DAT /R:0 /W:0 /XJ /BYTES /NP /NFL /NDL";
                int code = Native.Run("robocopy.exe", args, 120000, out output);
                plan.Output = output ?? "";
                plan.Ok = code >= 0 && code < 8;
                if (!plan.Ok) plan.Error = S.Repair.BackupExit(code);
                ParseRobocopy(plan, output);
                // Nhãn tổng kết của robocopy bị bản địa hoá ("Tệp", "Byte"...), nên trên
                // Windows tiếng Việt không đọc được khoá tiếng Anh. Đếm dự phòng để số
                // xem trước không hiện 0; chỉ đếm khi chưa có số nào.
                if (plan.Ok && plan.Files == 0 && plan.Bytes == 0)
                {
                    long files, bytes;
                    CountTree(drive, out files, out bytes);
                    plan.Files = files;
                    plan.Bytes = bytes;
                }
            }
            catch (Exception e)
            {
                plan.Error = e.Message;
            }
            return plan;
        }

        private static void ParseRobocopy(BackupPlan plan, string output)
        {
            if (string.IsNullOrEmpty(output)) return;
            foreach (string raw in output.Split('\n'))
            {
                string line = raw.Trim();
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string key = line.Substring(0, colon).Trim().ToLowerInvariant();
                string val = line.Substring(colon + 1).Trim().Replace(",", "");
                long n;
                if (!long.TryParse(val, out n)) continue;
                if (key == "files") plan.Files = n;
                else if (key == "bytes") plan.Bytes = n;
            }
        }

        // Đếm tệp/dung lượng trực tiếp, dùng khi không đọc được bản tổng kết robocopy.
        // Giới hạn thời gian và số mục để không treo giao diện trên ổ lớn.
        internal static void CountTree(string drive, out long files, out long bytes)
        {
            files = 0;
            bytes = 0;
            try
            {
                WalkStats stats = new WalkStats();
                foreach (TreeItem item in Tree.Walk(NormalRoot(drive), stats, 300000, 60, 64))
                {
                    if (item.IsDir) continue;
                    long size = item.Size;
                    if (size < 0) continue;
                    files++;
                    bytes += size;
                }
            }
            catch { }
        }

        // Sao luu that. Bat buoc dich nam trong repair\ va khong nam tren o nguon.
        public static bool Backup(string drive, out string dest, out string output)
        {
            dest = "";
            output = "";
            string reason;
            if (RefuseReason(drive, out reason))
            {
                output = reason;
                return false;
            }
            try
            {
                Paths.EnsureDirs();
                string root = NormalRoot(drive);
                string letter = root.Substring(0, 1).ToUpperInvariant();
                dest = Path.Combine(Paths.RepairDir,
                    "backup-" + letter + "-" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));

                string repairRoot = Path.GetFullPath(Paths.RepairDir)
                    .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(dest);
                if (!full.StartsWith(repairRoot, StringComparison.OrdinalIgnoreCase))
                {
                    output = S.Repair.BackupOutside;
                    return false;
                }
                if (full.Length > 2 && string.Equals(full.Substring(0, 2),
                    root.Substring(0, 2), StringComparison.OrdinalIgnoreCase))
                {
                    output = S.Repair.BackupOnSource;
                    return false;
                }

                string args = "\"" + root + "\" \"" + dest +
                              "\" /E /COPY:DAT /R:1 /W:1 /XJ /NP /NFL /NDL";
                int code = Native.Run("robocopy.exe", args, 6 * 60 * 60 * 1000, out output);
                bool ok = code >= 0 && code < 8;
                if (ok)
                {
                    File.WriteAllText(MarkerFile(drive), dest + "\r\n", new UTF8Encoding(false));
                    Events.Add(Events.KindRepair, "INFO", root, S.Events.BackupDone(dest));
                    Log.Write(S.Log.BackupDone(root, dest));
                }
                else Log.Write(S.Log.BackupFailed(root, code), "WARN");
                return ok;
            }
            catch (Exception e)
            {
                output = e.Message;
                Log.Write(S.Log.BackupFailed(dest, -1), "WARN");
                return false;
            }
        }

        private static string MarkerFile(string drive)
        {
            string letter = NormalRoot(drive).Substring(0, 1).ToUpperInvariant();
            return Path.Combine(Paths.RepairDir, "backup-" + letter + ".ok");
        }

        public static bool HasBackup(string drive)
        {
            try { return File.Exists(MarkerFile(drive)); }
            catch { return false; }
        }

        public static string LastBackup(string drive)
        {
            try
            {
                if (!File.Exists(MarkerFile(drive))) return "";
                return File.ReadAllText(MarkerFile(drive)).Trim();
            }
            catch { return ""; }
        }

        // chkdsk chi doc hoac /f. /f bat buoc da co ban sao luu va co quyen quan tri.
        public static int RunChkdsk(string drive, bool fix, Action<string> log)
        {
            string reason;
            if (RefuseReason(drive, out reason))
            {
                if (log != null) log(reason);
                return -1;
            }
            if (fix)
            {
                if (!Native.IsAdmin())
                {
                    if (log != null) log(S.Cli.AdminRequired);
                    return -1;
                }
                if (!HasBackup(drive))
                {
                    if (log != null) log(S.Repair.BackupRequired);
                    return -1;
                }
            }

            string letter = NormalRoot(drive).Substring(0, 2);
            string args = fix ? letter + " /f" : letter;
            string output;
            if (log != null) log(fix ? S.Repair.ChkdskFixRunning : S.Repair.ChkdskReadRunning);
            int code = Native.Run("chkdsk.exe", args,
                                  fix ? 6 * 60 * 60 * 1000 : 20 * 60 * 1000, out output);
            if (log != null && !string.IsNullOrEmpty(output))
            {
                string[] lines = output.Replace("\r", "").Split('\n');
                int start = Math.Max(0, lines.Length - 12);
                for (int i = start; i < lines.Length; i++)
                    if (lines[i].Trim().Length > 0) log("  " + lines[i].Trim());
            }
            Events.Add(Events.KindRepair, code == 0 ? "INFO" : "WARN", NormalRoot(drive),
                       (fix ? "chkdsk /f" : "chkdsk") + " -> " + code);
            Log.Write(S.Log.ChkdskDone(NormalRoot(drive), fix, code), code == 0 ? "INFO" : "WARN");
            return code;
        }

        // Chi doc 512 byte dau (VBR/MBR). Khong ghi, khong sua.
        public static BootSectorInfo ReadBootSector(string drive)
        {
            BootSectorInfo info = new BootSectorInfo();
            string root = NormalRoot(drive);
            if (root.Length < 3) { info.Error = S.Repair.InvalidDrive; return info; }
            try
            {
                string device = @"\\.\" + root.Substring(0, 2);
                using (SafeFileHandle handle = CreateFileW(device, GenericRead,
                           FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero))
                {
                    if (handle.IsInvalid)
                    {
                        info.Error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                        return info;
                    }
                    byte[] boot = new byte[512];
                    using (FileStream fs = new FileStream(handle, FileAccess.Read, 512, false))
                    {
                        int read = fs.Read(boot, 0, boot.Length);
                        if (read < 512)
                        {
                            info.Error = S.Repair.BootShort(read);
                            return info;
                        }
                    }
                    info.Read = true;
                    info.Signature55AA = boot[510] == 0x55 && boot[511] == 0xAA;
                    info.FileSystem = IdentifyFileSystem(boot);
                    return info;
                }
            }
            catch (Exception e)
            {
                info.Error = e.Message;
                return info;
            }
        }

        private static string IdentifyFileSystem(byte[] boot)
        {
            string at3 = Ascii(boot, 3, 8);
            if (at3.StartsWith("NTFS", StringComparison.OrdinalIgnoreCase)) return "NTFS";
            if (at3.StartsWith("EXFAT", StringComparison.OrdinalIgnoreCase)) return "exFAT";
            string at54 = Ascii(boot, 54, 5);
            if (at54.StartsWith("FAT", StringComparison.OrdinalIgnoreCase)) return "FAT";
            string at82 = Ascii(boot, 82, 5);
            if (at82.StartsWith("FAT32", StringComparison.OrdinalIgnoreCase)) return "FAT32";
            bool zero = true;
            for (int i = 0; i < boot.Length; i++)
                if (boot[i] != 0) { zero = false; break; }
            return zero ? S.Repair.BootEmpty : S.Repair.BootUnknown;
        }

        private static string Ascii(byte[] data, int offset, int count)
        {
            if (offset < 0 || offset + count > data.Length) return "";
            char[] chars = new char[count];
            for (int i = 0; i < count; i++)
            {
                byte b = data[offset + i];
                chars[i] = b >= 32 && b < 127 ? (char)b : ' ';
            }
            return new string(chars);
        }
    }
}
