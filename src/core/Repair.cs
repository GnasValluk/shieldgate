// ShieldGate 2.0 - chan doan va sua o an toan.
// Nguyen tac: moi thao tac ghi deu tu choi o he thong, co xem truoc, co sao luu,
// co xac nhan o giao dien. Boot sector chi doc de bao cao, khong bao gio ghi.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
            return RefuseReason(drive, false, out reason);
        }

        // write = true: thao tac ghi chi cho o roi. O co dinh phu (D:, E: trong
        // may) van duoc chan doan chi doc nhung khong duoc sao luu/chkdsk /f: day
        // thuong la o du lieu chinh, khong phai thiet bi ngoai vi.
        public static bool RefuseReason(string drive, bool write, out string reason)
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
                if (write && di.DriveType != DriveType.Removable)
                {
                    reason = S.Repair.NotRemovable;
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
                int code = Native.Run(Native.SysExe("fsutil.exe"), "dirty query " + letter, 15000, out output);
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
            if (RefuseReason(drive, true, out reason))
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
                              "\" /L /E /COPY:DAT /R:0 /W:0 /XJ" + SkipDirs + " /BYTES /NP /NFL /NDL";
                int code = Native.Run(Native.SysExe("robocopy.exe"), args, 120000, out output);
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
            if (RefuseReason(drive, true, out reason))
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

                string repairRoot = SafeRepairRoot();
                string full = Path.GetFullPath(dest);
                if (repairRoot.Length == 0 ||
                    !full.StartsWith(repairRoot, StringComparison.OrdinalIgnoreCase))
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

                // Uoc luong truoc khi chep: robocopy het cho se dung giua duong va
                // de lai ban sao do, trong khi marker chi duoc ghi khi xong.
                long files, bytes;
                CountTree(root, out files, out bytes);
                long need, free;
                if (!HasRoomFor(bytes, out need, out free))
                {
                    output = S.Repair.BackupNoSpace(need, free);
                    return false;
                }

                string args = "\"" + root + "\" \"" + dest +
                              "\" /E /COPY:DAT /R:1 /W:1 /XJ" + SkipDirs + " /NP /NFL /NDL";
                int code = Native.Run(Native.SysExe("robocopy.exe"), args, 6 * 60 * 60 * 1000, out output);
                bool ok = code >= 0 && code < 8;
                // O trong hop le thi dich cung rong; chi khi nguon co du lieu ma
                // dich khong co gi moi la chep hong.
                if (ok && bytes > 0 && !HasAnyEntry(dest))
                {
                    ok = false;
                    output = S.Repair.BackupMissing;
                }
                if (ok)
                {
                    if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);
                    uint serial = Native.VolumeSerial(root);
                    long size = 0;
                    try { size = new DriveInfo(root).TotalSize; } catch { }
                    StringBuilder marker = new StringBuilder();
                    marker.Append("state=complete\r\n");
                    marker.Append("volumeSerial=")
                          .Append(serial.ToString("X8", CultureInfo.InvariantCulture)).Append("\r\n");
                    marker.Append("volumeBytes=")
                          .Append(size.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
                    marker.Append("source=").Append(root).Append("\r\n");
                    marker.Append("dest=").Append(dest).Append("\r\n");
                    marker.Append("when=")
                          .Append(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss",
                                                        CultureInfo.InvariantCulture)).Append("\r\n");
                    marker.Append("files=")
                          .Append(files.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
                    marker.Append("bytes=")
                          .Append(bytes.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
                    File.WriteAllText(MarkerFile(drive, serial), marker.ToString(),
                                      new UTF8Encoding(false));
                    // Marker 2.0 chi theo chu o khong con hieu luc; xoa de khoi gay nham.
                    try { File.Delete(Path.Combine(Paths.RepairDir, "backup-" + letter + ".ok")); }
                    catch { }
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

        // Du cho ban sao luu: giu lai 10% va 100 MB cho nhat ky/he thong.
        private static bool HasRoomFor(long bytes, out long need, out long free)
        {
            need = 0;
            free = 0;
            try
            {
                string root = Path.GetPathRoot(Paths.RepairDir);
                DriveInfo d = new DriveInfo(root);
                free = d.AvailableFreeSpace;
                need = bytes + bytes / 10 + 100L * 1024 * 1024;
                return free >= need;
            }
            catch { return true; }
        }

        // ------------------------------------------------ marker sao lưu theo ổ

        // Marker 2.0 (backup-E.ok) chỉ gắn theo chữ ổ: USB khác cắm vào cùng E:
        // vẫn được coi là đã sao lưu. Marker mới ghi serial + dung lượng volume và
        // chỉ có hiệu lực 24 giờ; marker cũ bị bỏ qua để buộc sao lưu lại.
        private const int MarkerMaxAgeHours = 24;

        // Hai thư mục này luôn gây lỗi truy cập trên NTFS; không loại trừ thì
        // robocopy trả mã >= 8 và marker không bao giờ được ghi.
        private const string SkipDirs = " /XD \"System Volume Information\" \"$RECYCLE.BIN\"";

        private static string MarkerFile(string drive, uint serial)
        {
            string letter = NormalRoot(drive).Substring(0, 1).ToUpperInvariant();
            return Path.Combine(Paths.RepairDir,
                "backup-" + letter + "-" + serial.ToString("X8", CultureInfo.InvariantCulture) + ".ok");
        }

        private static string MarkerValue(string text, string key)
        {
            if (string.IsNullOrEmpty(text)) return "";
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                if (string.Equals(line.Substring(0, eq).Trim(), key,
                                  StringComparison.OrdinalIgnoreCase))
                    return line.Substring(eq + 1).Trim();
            }
            return "";
        }

        private static string SafeRepairRoot()
        {
            try
            {
                return Path.GetFullPath(Paths.RepairDir)
                    .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            }
            catch { return ""; }
        }

        private static bool IsInsideRepair(string path, string repairRoot)
        {
            if (string.IsNullOrEmpty(path) || repairRoot.Length == 0) return false;
            try
            {
                string full = Path.GetFullPath(path);
                return full.StartsWith(repairRoot, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(full.TrimEnd(Path.DirectorySeparatorChar),
                                      repairRoot.TrimEnd(Path.DirectorySeparatorChar),
                                      StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool HasAnyEntry(string dir)
        {
            try { return Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length > 0; }
            catch { return false; }
        }

        // Tim marker hop le cua dung volume dang cam; tra ve duong dan ban sao.
        private static string FindMarker(string drive, out string reason, out DateTime when)
        {
            reason = S.Repair.BackupRequired;
            when = DateTime.MinValue;
            string root = NormalRoot(drive);
            if (root.Length < 3)
            {
                reason = S.Repair.InvalidDrive;
                return "";
            }
            uint serial = Native.VolumeSerial(root);
            if (serial == 0)
            {
                reason = S.Repair.NotReady;
                return "";
            }
            long size = 0;
            try { size = new DriveInfo(root).TotalSize; } catch { }

            string[] files;
            try
            {
                files = Directory.GetFiles(Paths.RepairDir,
                    "backup-" + root.Substring(0, 1).ToUpperInvariant() + "-*.ok");
            }
            catch { return ""; }

            string repairRoot = SafeRepairRoot();
            string serialText = serial.ToString("X8", CultureInfo.InvariantCulture);
            foreach (string file in files)
            {
                string text;
                try { text = File.ReadAllText(file); } catch { continue; }
                if (!string.Equals(MarkerValue(text, "state"), "complete",
                                   StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.Equals(MarkerValue(text, "volumeSerial"), serialText,
                                   StringComparison.OrdinalIgnoreCase))
                {
                    reason = S.Repair.BackupOtherVolume;
                    continue;
                }
                string sizeText = MarkerValue(text, "volumeBytes");
                if (sizeText.Length > 0 && size > 0 &&
                    !string.Equals(sizeText, size.ToString(CultureInfo.InvariantCulture),
                                   StringComparison.Ordinal))
                {
                    reason = S.Repair.BackupOtherVolume;
                    continue;
                }

                DateTime made;
                if (!DateTime.TryParse(MarkerValue(text, "when"), CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out made))
                {
                    reason = S.Repair.BackupMissing;
                    continue;
                }
                if ((DateTime.Now - made).TotalHours > MarkerMaxAgeHours)
                {
                    reason = S.Repair.BackupExpired;
                    continue;
                }

                string dest = MarkerValue(text, "dest");
                if (!IsInsideRepair(dest, repairRoot) || !HasAnyEntry(dest))
                {
                    reason = S.Repair.BackupMissing;
                    continue;
                }

                when = made;
                reason = "";
                return dest;
            }
            return "";
        }

        // True khi co marker hop le: dung volume, con han, ban sao con ton tai.
        public static bool CheckBackup(string drive, out string reason)
        {
            DateTime when;
            return FindMarker(drive, out reason, out when).Length > 0;
        }

        public static bool HasBackup(string drive)
        {
            string reason;
            return CheckBackup(drive, out reason);
        }

        public static string LastBackup(string drive)
        {
            string reason;
            DateTime when;
            return FindMarker(drive, out reason, out when);
        }

        // chkdsk chi doc hoac /f. /f bat buoc da co ban sao luu va co quyen quan tri.
        public static int RunChkdsk(string drive, bool fix, Action<string> log)
        {
            string reason;
            if (RefuseReason(drive, fix, out reason))
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
            // chkdsk /f hoi "force a dismount? (Y/N)" khi o con handle mo. Khong
            // redirect stdin thi tien trinh cho vo han (truoc day toi 6 gio).
            // Tra loi Y san; dong stdin de lan hoi sau nhan EOF thay vi treo.
            int code = Native.Run(Native.SysExe("chkdsk.exe"), args,
                                  fix ? 6 * 60 * 60 * 1000 : 20 * 60 * 1000,
                                  fix ? "Y\r\n" : null, out output);
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
