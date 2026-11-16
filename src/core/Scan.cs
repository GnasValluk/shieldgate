// ShieldGate - lõi quét: tìm trước, người dùng chọn, rồi mới cách ly.
// Không tự sửa gì khi chưa được xác nhận. Mọi bước đều huỷ được.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Win32;

namespace ShieldGate
{
    internal enum UsbKind { Shortcut, Script, Autorun, RootExe, HiddenFolder, FakeFolder }

    internal sealed class UsbItem
    {
        public string Path;
        public string Rel;
        public UsbKind Kind;
        public List<string> Codes = new List<string>();
        public long Size;
        // Chi dung cho HiddenFolder: ten that cua thu muc khong ten (co ky tu vo hinh).
        public string FolderName;
        // Dấu hiệu đủ mạnh để bảo vệ nền tự cách ly mà không hỏi.
        public bool High;

        public string ReasonText { get { return S.Reason.Text(Codes); } }
    }

    internal sealed class UsbScanResult
    {
        public string Root;
        public string Drive;
        public List<UsbItem> Items = new List<UsbItem>();
        public List<string> Warnings = new List<string>();
        public int Files;
        public double Seconds;
        public bool Cancelled;
        public bool Truncated;
        public int Shortcuts;
        public int Scripts;
        public int Autoruns;
        public int RootExes;
        public int Folders;
    }

    internal static class UsbScan
    {
        private static readonly Regex ScriptCmdRe = new Regex(
            @"\.(vbs|vbe|js|jse|wsf|hta)\b", RegexOptions.IgnoreCase);

        // ------------------------------------------------------------ tiến trình

        private static IEnumerable<ManagementObject> ScriptProcesses()
        {
            ManagementObjectCollection col = null;
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect();
                ManagementObjectSearcher s = new ManagementObjectSearcher(scope, new ObjectQuery(
                    "SELECT ProcessId, Name, CommandLine FROM Win32_Process " +
                    "WHERE Name='wscript.exe' OR Name='cscript.exe' OR Name='mshta.exe'"));
                s.Options.Timeout = TimeSpan.FromSeconds(10);
                col = s.Get();
            }
            catch { }

            if (col == null) yield break;
            foreach (ManagementBaseObject o in col)
            {
                ManagementObject mo = o as ManagementObject;
                if (mo != null) yield return mo;
            }
        }

        public static int StopProcesses(string root)
        {
            int killed = 0;
            string low = root.ToLowerInvariant();
            try
            {
                foreach (ManagementObject mo in ScriptProcesses())
                {
                    try
                    {
                        string cmd = Convert.ToString(mo["CommandLine"]);
                        int pid = Convert.ToInt32(mo["ProcessId"]);
                        if (!string.IsNullOrEmpty(cmd) && cmd.ToLowerInvariant().Contains(low)
                            && ScriptCmdRe.IsMatch(cmd))
                        {
                            try
                            {
                                using (Process p = Process.GetProcessById(pid)) p.Kill();
                                killed++;
                                Log.Write(S.Log.ProcessStopped(pid, root), "WARN");
                            }
                            catch { }
                        }
                    }
                    catch { }
                    finally { try { mo.Dispose(); } catch { } }
                }
            }
            catch { }
            return killed;
        }

        // ---------------------------------------------------------------- quét

        public static UsbScanResult Detect(string root, Action<int, string> progress, Func<bool> cancelled)
        {
            return Detect(root, progress, cancelled, int.MaxValue);
        }

        // Quet nhanh chi o goc o (va cac thu muc con cap 1 de nhan dien thu muc gia).
        // Dung cho kiem tra dinh ky khi USB dang cam va cho quet goc cac o tren may.
        public static UsbScanResult DetectRoot(string root)
        {
            return Detect(root, null, null, 0);
        }

        public static UsbScanResult Detect(string root, Action<int, string> progress,
                                           Func<bool> cancelled, int maxDepth)
        {
            DateTime started = DateTime.UtcNow;
            UsbScanResult result = new UsbScanResult();
            result.Root = root;
            result.Drive = root.Length >= 2 ? root.Substring(0, 2) : root;
            int seen = 0;
            WalkStats stats = new WalkStats();
            List<string> nameless = UsbRestore.FindNameless(root);
            List<TreeItem> rootExes = new List<TreeItem>();

            foreach (TreeItem f in Tree.Walk(root, stats, 150000, 300, maxDepth))
            {
                if (cancelled != null && cancelled())
                {
                    result.Cancelled = true;
                    break;
                }
                if (f.IsDir) continue;

                seen++;
                if (progress != null && (seen % 25 == 0)) progress(seen, f.Path);

                string low = f.Name.ToLowerInvariant();
                string ext = System.IO.Path.GetExtension(low);
                bool isLnk = ext == ".lnk";
                bool isScript = ShieldGate.Detect.ScriptExts.Contains(ext);
                bool isAutorun = low == "autorun.inf";
                bool isRootExe = f.Depth == 0 &&
                    (ext == ".exe" || ext == ".com" || ext == ".pif" || ext == ".scr") &&
                    (f.Attrs & (FileAttributes.Hidden | FileAttributes.System)) != 0;

                UsbItem item = null;

                if (isLnk)
                {
                    List<string> why;
                    if (ShieldGate.Detect.LnkVerdict(f.Path, root, f.Attrs, out why))
                    {
                        item = MakeItem(f, UsbKind.Shortcut);
                        item.Codes.AddRange(why);
                        item.High = Strong(why);
                    }
                }
                else if (isScript)
                {
                    List<string> why;
                    if (ShieldGate.Detect.ScriptVerdict(f.Path, f.Attrs, f.Depth, out why))
                    {
                        item = MakeItem(f, UsbKind.Script);
                        item.Codes.AddRange(why);
                        item.High = why.Contains("encoded-vbscript");
                    }
                }
                else if (isAutorun)
                {
                    if (ShieldGate.Detect.AutorunVerdict(f.Path))
                    {
                        item = MakeItem(f, UsbKind.Autorun);
                        item.Codes.Add("autorun-payload");
                        item.High = true;
                    }
                }
                else if (isRootExe)
                {
                    item = MakeItem(f, UsbKind.RootExe);
                    item.Codes.Add("hidden-root-exe");
                    item.High = true;
                }
                else if (f.Depth == 0 && (ext == ".exe" || ext == ".com" || ext == ".pif" || ext == ".scr"))
                {
                    result.Warnings.Add(f.Name);
                    rootExes.Add(f);
                }

                if (item != null)
                {
                    try { item.Size = new FileInfo(item.Path).Length; }
                    catch { }
                    result.Items.Add(item);
                    if (item.Kind == UsbKind.Shortcut) result.Shortcuts++;
                    else if (item.Kind == UsbKind.Script) result.Scripts++;
                    else if (item.Kind == UsbKind.Autorun) result.Autoruns++;
                    else result.RootExes++;
                }
            }

            // Thu muc khong ten: noi virus giau du lieu that cua nguoi dung.
            // Neu chi chua du lieu (khong co tep chay) thi co the tu chuyen ra goc o.
            foreach (string nm in nameless)
            {
                UsbItem fi = new UsbItem();
                fi.Path = System.IO.Path.Combine(root, nm);
                fi.Rel = S.Usb.NamelessLabel(UsbRestore.Display(nm));
                fi.Kind = UsbKind.HiddenFolder;
                fi.FolderName = nm;
                fi.Codes.Add("nameless-folder");
                fi.High = !UsbRestore.HasRiskyContent(fi.Path);
                result.Items.Add(fi);
                result.Folders++;
            }

            // Thu muc gia (vd "Docusment") khong can thu muc khong ten di kem.
            // Noi dung virus (payload) bi cach ly; du lieu nguoi dung duoc giu lai.
            foreach (string fk in UsbRestore.FindFakeDirs(root))
            {
                string full = System.IO.Path.Combine(root, fk);
                ShieldGate.Detect.FolderCheck fc = ShieldGate.Detect.CheckFolder(full, System.IO.Path.GetFileName(fk));
                UsbItem fd = new UsbItem();
                fd.Path = full;
                fd.Rel = fk;
                fd.Kind = UsbKind.FakeFolder;
                fd.FolderName = fk;
                fd.Codes.Add(fc != null && fc.Code.Length > 0 ? fc.Code : "fake-folder-dir");
                fd.High = fc != null && fc.Strong;
                result.Items.Add(fd);
                result.Folders++;
            }

            // Tep .exe gia thu muc ("Documents" co bieu tuong thu muc nhung la chuong trinh).
            bool infected = nameless.Count > 0;
            foreach (UsbItem x in result.Items)
                if (x.High) infected = true;
            foreach (TreeItem ex in rootExes)
            {
                string bn = System.IO.Path.GetFileNameWithoutExtension(ex.Name);
                bool sameAsDir = false, dirHidden = false;
                try
                {
                    string dp = System.IO.Path.Combine(root, bn);
                    sameAsDir = Directory.Exists(dp);
                    if (sameAsDir) dirHidden = (File.GetAttributes(dp) & FileAttributes.Hidden) != 0;
                }
                catch { }
                bool folderLike = UsbRestore.LooksLikeFolderName(bn);
                if ((sameAsDir && (dirHidden || infected)) || (infected && folderLike))
                {
                    UsbItem fx = MakeItem(ex, UsbKind.RootExe);
                    fx.Codes.Add("fake-folder-exe");
                    fx.High = true;
                    try { fx.Size = new FileInfo(fx.Path).Length; }
                    catch { }
                    result.Items.Add(fx);
                    result.RootExes++;
                    result.Warnings.Remove(ex.Name);
                }
            }

            result.Files = seen;
            result.Truncated = stats.Truncated;
            result.Seconds = Math.Round((DateTime.UtcNow - started).TotalSeconds, 1);
            if (progress != null) progress(seen, null);
            return result;
        }

        // Shortcut trỏ tới công cụ hệ thống hoặc mạo danh thư mục bị ẩn:
        // đây là hai dấu hiệu mạnh nhất của virus shortcut.
        private static bool Strong(List<string> why)
        {
            foreach (string r in why)
            {
                if (r.StartsWith("target=", StringComparison.OrdinalIgnoreCase)) return true;
                if (r.StartsWith("args=", StringComparison.OrdinalIgnoreCase)) return true;
                if (r.StartsWith("args-relative-exe=", StringComparison.OrdinalIgnoreCase)) return true;
                if (r.StartsWith("args-drive-exe=", StringComparison.OrdinalIgnoreCase)) return true;
                if (r == "shadows-hidden-folder") return true;
                if (r == "target-in-hidden-folder") return true;
                if (r == "target-in-fake-folder") return true;
            }
            return false;
        }

        private static UsbItem MakeItem(TreeItem f, UsbKind kind)
        {
            UsbItem item = new UsbItem();
            item.Path = f.Path;
            item.Rel = f.Rel;
            item.Kind = kind;
            return item;
        }

        // ------------------------------------------------------------- cách ly

        public static int Apply(UsbScanResult scan, IList<UsbItem> selected,
                                out int failed, out string batch, out int stopped, Action<int> progress)
        {
            failed = 0;
            stopped = StopProcesses(scan.Root);
            batch = Quarantine.NewBatch("usb_" + (scan.Drive.Length > 0 ? scan.Drive.Substring(0, 1) : "x"), "");
            int done = 0;
            List<string> folders = new List<string>();
            List<string> fakes = new List<string>();
            for (int i = 0; i < selected.Count; i++)
            {
                UsbItem it = selected[i];
                if (it.Kind == UsbKind.FakeFolder)
                {
                    fakes.Add(it.FolderName);
                    if (progress != null) progress(i + 1);
                    continue;
                }
                if (it.Kind == UsbKind.HiddenFolder)
                {
                    folders.Add(it.FolderName);
                    if (progress != null) progress(i + 1);
                    continue;
                }
                string err;
                if (Quarantine.Add(it.Path, scan.Root, batch, KindName(it.Kind), out err))
                    done++;
                else
                {
                    failed++;
                    Log.Write(S.Log.QuarantineFailed(it.Path, err), "ERROR");
                }
                if (progress != null) progress(i + 1);
            }

            // Thu muc gia: cach ly tung tep (khoi phuc duoc), roi go thu muc rong.
            foreach (string fk in fakes)
            {
                done += QuarantineTree(System.IO.Path.Combine(scan.Root, fk), scan.Root, batch, ref failed);
                UsbRestore.RemoveWrapper(scan.Root, fk);
            }

            // Tra du lieu tu thu muc khong ten ra goc o, roi xoa thu muc rong do.
            if (folders.Count > 0)
            {
                RestoreResult rr = UsbRestore.Restore(scan.Root, folders, null);
                done += rr.DirsRemoved;
                failed += rr.DirsLeft;
                if (rr.Moved > 0) done += SweepRestored(scan.Root, batch);
            }

            Log.Write(S.Log.QuarantineSummary(done, scan.Root, failed));
            return done;
        }

        public static int QuarantineTree(string dir, string root, string batch, ref int failed)
        {
            int n = 0;
            List<string> dirs = new List<string>();
            foreach (TreeItem t in Tree.Walk(dir))
            {
                if (t.IsDir) { dirs.Add(t.Path); continue; }
                string err;
                if (Quarantine.Add(t.Path, root, batch, "fake-folder", out err)) n++;
                else
                {
                    failed++;
                    Log.Write(S.Log.QuarantineFailed(t.Path, err), "ERROR");
                }
            }
            dirs.Sort(delegate(string a, string b) { return b.Length.CompareTo(a.Length); });
            foreach (string d in dirs)
            {
                try { File.SetAttributes(d, FileAttributes.Normal); Directory.Delete(d, false); }
                catch { }
            }
            bool removed = false;
            try { File.SetAttributes(dir, FileAttributes.Normal); Directory.Delete(dir, false); removed = true; }
            catch { }
            return n > 0 ? n : (removed ? 1 : 0);
        }

        // Sau khi du lieu ra ngoai, quet lai va cach ly nhung dau hieu manh (shortcut /
        // script / autorun) vua lo ra. Khong dong vao tep .exe de khong xoa nham phan mem that.
        private static int SweepRestored(string root, string batch)
        {
            int n = 0;
            UsbScanResult again = UsbScan.Detect(root, null, null);
            foreach (UsbItem it in again.Items)
            {
                if (!it.High) continue;
                if (it.Kind != UsbKind.Shortcut && it.Kind != UsbKind.Script && it.Kind != UsbKind.Autorun) continue;
                string err;
                if (Quarantine.Add(it.Path, root, batch, KindName(it.Kind), out err)) n++;
            }
            return n;
        }

        private static string KindName(UsbKind kind)
        {
            switch (kind)
            {
                case UsbKind.Shortcut: return "shortcut";
                case UsbKind.Script: return "script";
                case UsbKind.Autorun: return "autorun";
                default: return "exe-an";
            }
        }

        public static int Unhide(string root, Func<bool> cancelled)
        {
            int count = 0;
            foreach (TreeItem t in Tree.Walk(root))
            {
                if (cancelled != null && cancelled()) break;
                try
                {
                    if (!File.Exists(t.Path) && !Directory.Exists(t.Path)) continue;
                    if (Native.ClearHidden(t.Path)) count++;
                }
                catch { }
            }
            if (count > 0) Log.Write(S.Log.Unhidden(count, root));
            return count;
        }

        public static string DriveLabel(string root)
        {
            try
            {
                DriveInfo d = new DriveInfo(root.Substring(0, 1));
                string label = d.VolumeLabel;
                return string.IsNullOrEmpty(label) ? d.Name : d.Name + " — " + label;
            }
            catch { return root; }
        }

        public static string DriveFileSystem(string root)
        {
            try { return new DriveInfo(root.Substring(0, 1)).DriveFormat; }
            catch { return ""; }
        }
    }

    // ------------------------------------------------------------------ máy tính

    internal enum PcKind
    {
        RunValue, WinlogonShell, WinlogonUserinit, AppInit,
        MountPoint, StartupFile, Task, Process, Defender,
        DroppedFile, FakeProcess,
        DriveShortcut, DriveFakeFolder, DriveHiddenFolder, DriveExe,
        WmiConsumer, LnkAssoc
    }

    internal sealed class PcItem
    {
        public PcKind Kind;
        public bool High;
        public string Arg1;
        public string Detail;
        public Dictionary<string, object> Data = new Dictionary<string, object>();

        public bool Fixable { get { return Kind != PcKind.Defender; } }
    }

    internal static class PcScan
    {
        private static PcItem New(PcKind kind, bool high, string arg1, string detail)
        {
            PcItem i = new PcItem();
            i.Kind = kind;
            i.High = high;
            i.Arg1 = arg1;
            i.Detail = detail;
            return i;
        }

        public static List<PcItem> Detect(Action<string> stage, Func<bool> cancelled)
        {
            List<PcItem> list = new List<PcItem>();
            RegistryHive[] hives = { RegistryHive.CurrentUser, RegistryHive.LocalMachine };
            string[] runPaths =
            {
                @"Software\Microsoft\Windows\CurrentVersion\Run",
                @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
                @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run",
                @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\RunOnce",
                @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run"
            };

            if (stage != null) stage(S.Pc.StageStartup);
            foreach (RegistryHive hive in hives)
            {
                if (cancelled != null && cancelled()) return list;
                foreach (string path in runPaths)
                {
                    foreach (KeyValuePair<string, object> kv in Reg.Values(hive, path))
                    {
                        string cmd = Environment.ExpandEnvironmentVariables(Convert.ToString(kv.Value) ?? "");
                        ShieldGate.CommandCheck v = ShieldGate.Detect.CommandVerdict(cmd);
                        if (v == null) continue;
                        PcItem i = New(PcKind.RunValue, v.High, kv.Key, cmd);
                        i.Data["hive"] = hive;
                        i.Data["path"] = path;
                        i.Data["name"] = kv.Key;
                        list.Add(i);
                    }
                }
            }

            if (stage != null) stage(S.Pc.StageLogin);
            string wlPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
            object val;
            RegistryValueKind kind;
            if (Reg.Read(RegistryHive.LocalMachine, wlPath, "Shell", out val, out kind))
            {
                string shell = Convert.ToString(val);
                if (!Regex.IsMatch(shell, @"^\s*explorer\.exe(\s*,.*)?\s*$", RegexOptions.IgnoreCase))
                {
                    PcItem i = New(PcKind.WinlogonShell, true, shell, wlPath + @"\Shell");
                    i.Data["hive"] = RegistryHive.LocalMachine;
                    i.Data["path"] = wlPath;
                    i.Data["name"] = "Shell";
                    i.Data["value"] = "explorer.exe";
                    i.Data["kind"] = RegistryValueKind.String;
                    list.Add(i);
                }
            }
            if (Reg.Read(RegistryHive.LocalMachine, wlPath, "Userinit", out val, out kind))
            {
                string ui = Convert.ToString(val);
                if (ui.IndexOf("userinit.exe", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    PcItem i = New(PcKind.WinlogonUserinit, true, ui, wlPath + @"\Userinit");
                    i.Data["hive"] = RegistryHive.LocalMachine;
                    i.Data["path"] = wlPath;
                    i.Data["name"] = "Userinit";
                    i.Data["value"] = @"C:\Windows\system32\userinit.exe,";
                    i.Data["kind"] = RegistryValueKind.String;
                    list.Add(i);
                }
            }

            string appInit = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows";
            if (Reg.Read(RegistryHive.LocalMachine, appInit, "AppInit_DLLs", out val, out kind))
            {
                string dlls = Convert.ToString(val);
                if (!string.IsNullOrWhiteSpace(dlls))
                {
                    PcItem i = New(PcKind.AppInit, false, dlls, appInit + @"\AppInit_DLLs");
                    i.Data["hive"] = RegistryHive.LocalMachine;
                    i.Data["path"] = appInit;
                    i.Data["name"] = "AppInit_DLLs";
                    i.Data["value"] = "";
                    i.Data["kind"] = RegistryValueKind.String;
                    list.Add(i);
                }
            }

            if (cancelled != null && cancelled()) return list;
            if (stage != null) stage(S.Pc.StageDrives);
            string mp = @"Software\Microsoft\Windows\CurrentVersion\Explorer\MountPoints2";
            foreach (string child in Reg.SubKeys(RegistryHive.CurrentUser, mp))
            {
                string cmdPath = mp + "\\" + child + @"\shell\open\command";
                if (!Reg.Read(RegistryHive.CurrentUser, cmdPath, "", out val, out kind)) continue;
                string cmd = Convert.ToString(val);
                if (string.IsNullOrWhiteSpace(cmd)) continue;
                ShieldGate.CommandCheck v = ShieldGate.Detect.CommandVerdict(Environment.ExpandEnvironmentVariables(cmd));
                if (v == null) continue;
                PcItem i = New(PcKind.MountPoint, v.High, child, cmd);
                i.Data["hive"] = RegistryHive.CurrentUser;
                i.Data["path"] = mp + "\\" + child;
                list.Add(i);
            }

            List<string> startup = new List<string>();
            startup.Add(Environment.GetFolderPath(Environment.SpecialFolder.Startup));
            startup.Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup));
            foreach (string dir in startup)
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    ScanStartupDir(dir, list);

            if (stage != null) stage(S.Pc.StageTasks);
            ScanTasks(list);

            if (cancelled != null && cancelled()) return list;
            if (stage != null) stage(S.Pc.StageProcesses);
            try
            {
                foreach (ManagementObject mo in ScriptProcesses())
                {
                    try
                    {
                        int pid = Convert.ToInt32(mo["ProcessId"]);
                        string name = Convert.ToString(mo["Name"]);
                        string cmd = Convert.ToString(mo["CommandLine"]) ?? "";
                        ShieldGate.CommandCheck v = ShieldGate.Detect.CommandVerdict(cmd);
                        if (v != null && v.High)
                        {
                            PcItem i = New(PcKind.Process, true, name, cmd + "  [PID " + pid + "]");
                            i.Data["pid"] = pid;
                            list.Add(i);
                        }
                    }
                    catch { }
                    finally { try { mo.Dispose(); } catch { } }
                }
            }
            catch { }

            ScanDropDirs(list);
            ScanImpostors(list);
            ScanOddProcesses(list);

            // Cac o dia va thu muc nguoi dung: noi virus tha shortcut/thu muc gia
            // va giau du lieu. Quet goc o de tim ban sao tren may bi nhiem.
            if (cancelled != null && cancelled()) return list;
            if (stage != null) stage(S.Pc.StageDrives);
            ScanDriveRoots(list);
            ScanUserFolders(list);

            // WMI co the chay script moi khi cam USB ma khong co tien trinh thuong tru.
            if (cancelled != null && cancelled()) return list;
            if (stage != null) stage(S.Pc.StageWmi);
            ScanWmi(list);
            ScanLnkAssoc(list);

            if (stage != null) stage(S.Pc.StageDefender);
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Defender");
                scope.Connect();
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope,
                    new ObjectQuery("SELECT RealTimeProtectionEnabled FROM MSFT_MpComputerStatus"));
                searcher.Options.Timeout = TimeSpan.FromSeconds(10);
                foreach (ManagementObject mo in searcher.Get())
                {
                    try
                    {
                        bool rtp = Convert.ToBoolean(mo["RealTimeProtectionEnabled"]);
                        if (!rtp) list.Add(New(PcKind.Defender, true, "", "RealTimeProtectionEnabled = false"));
                    }
                    catch { }
                    finally { try { mo.Dispose(); } catch { } }
                }
            }
            catch { }

            list.Sort(delegate(PcItem a, PcItem b) { return (a.High ? 0 : 1).CompareTo(b.High ? 0 : 1); });
            return list;
        }

        private static IEnumerable<ManagementObject> ScriptProcesses()
        {
            ManagementObjectCollection col = null;
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect();
                ManagementObjectSearcher s = new ManagementObjectSearcher(scope, new ObjectQuery(
                    "SELECT ProcessId, Name, CommandLine FROM Win32_Process " +
                    "WHERE Name='wscript.exe' OR Name='cscript.exe' OR Name='mshta.exe'"));
                s.Options.Timeout = TimeSpan.FromSeconds(10);
                col = s.Get();
            }
            catch { }
            if (col == null) yield break;
            foreach (ManagementBaseObject o in col)
            {
                ManagementObject mo = o as ManagementObject;
                if (mo != null) yield return mo;
            }
        }

        // Tep .exe tha thang vao AppData / Temp / ProgramData: cach virus shortcut hay dung.
        private static void ScanDropDirs(List<PcItem> findings)
        {
            foreach (string dir in ShieldGate.Detect.DropDirs())
            {
                string[] files;
                try { files = Directory.GetFiles(dir); }
                catch { continue; }
                foreach (string path in files)
                {
                    try
                    {
                        string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                        if (ext != ".exe" && ext != ".scr" && ext != ".com" && ext != ".pif") continue;
                        string leaf = System.IO.Path.GetFileName(path).ToLowerInvariant();
                        FileAttributes a = File.GetAttributes(path);
                        bool hid = (a & (FileAttributes.Hidden | FileAttributes.System)) != 0;
                        string why = null;
                        if (ShieldGate.Detect.SystemNames.Contains(leaf)) why = "fake-system-name";
                        else if (hid && !ShieldGate.Detect.IsSigned(path)) why = "hidden-unsigned-exe";
                        if (why == null) continue;
                        PcItem i = New(PcKind.DroppedFile, true, System.IO.Path.GetFileName(path), why + "  ->  " + path);
                        i.Data["path"] = path;
                        i.Data["root"] = dir;
                        findings.Add(i);
                    }
                    catch { }
                }
            }
        }

        // Tien trinh mang ten tien trinh he thong (svchost.exe...) nhung chay ngoai thu muc Windows.
        private static void ScanImpostors(List<PcItem> findings)
        {
            try
            {
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                List<string> conds = new List<string>();
                foreach (string n in ShieldGate.Detect.SystemNames) conds.Add("Name='" + n + "'");
                ManagementScope scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect();
                ManagementObjectSearcher s = new ManagementObjectSearcher(scope, new ObjectQuery(
                    "SELECT ProcessId, Name, ExecutablePath FROM Win32_Process WHERE " +
                    string.Join(" OR ", conds.ToArray())));
                s.Options.Timeout = TimeSpan.FromSeconds(10);
                foreach (ManagementBaseObject o in s.Get())
                {
                    ManagementObject mo = o as ManagementObject;
                    if (mo == null) continue;
                    try
                    {
                        string path = Convert.ToString(mo["ExecutablePath"]);
                        if (string.IsNullOrEmpty(path)) continue;
                        if (path.StartsWith(win + "\\", StringComparison.OrdinalIgnoreCase)) continue;
                        int pid = Convert.ToInt32(mo["ProcessId"]);
                        PcItem i = New(PcKind.FakeProcess, true, Convert.ToString(mo["Name"]),
                                       path + "  [PID " + pid + "]");
                        i.Data["pid"] = pid;
                        findings.Add(i);
                    }
                    catch { }
                    finally { try { mo.Dispose(); } catch { } }
                }
            }
            catch { }
        }

        // Tien trinh chay tu USB hoac tu thu muc nguoi dung voi ten dang so (1.exe,
        // 2.exe...) va khong co chu ky: dau hieu payload cua virus shortcut.
        private static void ScanOddProcesses(List<PcItem> findings)
        {
            try
            {
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                ManagementScope scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect();
                ManagementObjectSearcher s = new ManagementObjectSearcher(scope, new ObjectQuery(
                    "SELECT ProcessId, Name, ExecutablePath FROM Win32_Process"));
                s.Options.Timeout = TimeSpan.FromSeconds(10);
                foreach (ManagementBaseObject o in s.Get())
                {
                    ManagementObject mo = o as ManagementObject;
                    if (mo == null) continue;
                    try
                    {
                        string path = Convert.ToString(mo["ExecutablePath"]);
                        if (string.IsNullOrEmpty(path)) continue;
                        if (path.StartsWith(win + "\\", StringComparison.OrdinalIgnoreCase)) continue;
                        string leaf = System.IO.Path.GetFileName(path).ToLowerInvariant();
                        bool odd = Regex.IsMatch(leaf, @"^\d{1,3}\.(exe|scr|com|pif)$");
                        bool removable = false;
                        try
                        {
                            if (path.Length > 2 && path[1] == ':')
                                removable = new DriveInfo(path.Substring(0, 1)).DriveType == DriveType.Removable;
                        }
                        catch { }
                        if (!odd && !removable) continue;
                        // Ung dung di dong co chu ky hop le (ke ca tren USB) khong phai
                        // payload; ngoai le la tep trung ten/hash cua mau da thu thap.
                        if (!odd && !ShieldGate.Detect.IsPayloadFile(path)
                            && ShieldGate.Detect.IsSigned(path)) continue;
                        int pid = Convert.ToInt32(mo["ProcessId"]);
                        PcItem i = New(PcKind.FakeProcess, true, Convert.ToString(mo["Name"]),
                                       path + "  [PID " + pid + "]" +
                                       (removable ? " (chay tu USB)" : " (ten dang so)"));
                        i.Data["pid"] = pid;
                        findings.Add(i);
                    }
                    catch { }
                    finally { try { mo.Dispose(); } catch { } }
                }
            }
            catch { }
        }

        // Goc cac o dia: tim shortcut, thu muc gia, thu muc khong ten va tep chay gia dang
        // ma virus shortcut de lai tren may bi nhiem (ke ca o he thong).
        private static void ScanDriveRoots(List<PcItem> findings)
        {
            try
            {
                foreach (DriveInfo d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!d.IsReady) continue;
                        if (d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Removable) continue;
                        string root = d.RootDirectory.FullName;
                        UsbScanResult r = UsbScan.DetectRoot(root);
                        foreach (UsbItem it in r.Items)
                        {
                            if (it.Kind == UsbKind.FakeFolder)
                            {
                                // Tren o he thong, chi bao thu muc gia khi co bang chung
                                // manh (payload/clsid). Ten chi hoi giong thu muc that
                                // (vd "download") khong du de de xuat xoa ca cay thu muc.
                                if (!it.High) continue;
                                PcItem i = New(PcKind.DriveFakeFolder, it.High, it.FolderName,
                                               it.Path + "  ->  " + string.Join("; ", it.Codes.ToArray()));
                                i.Data["root"] = root;
                                i.Data["folder"] = it.FolderName;
                                findings.Add(i);
                            }
                            else if (it.Kind == UsbKind.HiddenFolder)
                            {
                                PcItem i = New(PcKind.DriveHiddenFolder, true,
                                               S.Usb.NamelessLabel(UsbRestore.Display(it.FolderName)),
                                               it.Path);
                                i.Data["root"] = root;
                                i.Data["folder"] = it.FolderName;
                                findings.Add(i);
                            }
                            else if (it.Kind == UsbKind.Shortcut && it.High)
                            {
                                PcItem i = New(PcKind.DriveShortcut, true, System.IO.Path.GetFileName(it.Path),
                                               it.Path + "  ->  " + string.Join("; ", it.Codes.ToArray()));
                                i.Data["path"] = it.Path;
                                i.Data["root"] = root;
                                findings.Add(i);
                            }
                            else if (it.Kind == UsbKind.RootExe && it.High)
                            {
                                PcItem i = New(PcKind.DriveExe, true, System.IO.Path.GetFileName(it.Path),
                                               it.Path + "  ->  " + string.Join("; ", it.Codes.ToArray()));
                                i.Data["path"] = it.Path;
                                i.Data["root"] = root;
                                findings.Add(i);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        // Desktop / Documents / Downloads ...: virus tha shortcut va script tai day
        // de nguoi dung bam nham, hoac de tu chay lai sau khi khoi dong.
        private static void ScanUserFolders(List<PcItem> findings)
        {
            List<string> dirs = new List<string>();
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            AddDir(dirs, profile);
            AddDir(dirs, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            AddDir(dirs, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            AddDir(dirs, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
            AddDir(dirs, Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
            AddDir(dirs, Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            if (profile.Length > 0) AddDir(dirs, System.IO.Path.Combine(profile, "Downloads"));
            string pub = Environment.GetEnvironmentVariable("PUBLIC");
            if (!string.IsNullOrEmpty(pub)) AddDir(dirs, System.IO.Path.Combine(pub, "Desktop"));

            foreach (string dir in dirs)
            {
                string[] files;
                try { files = Directory.GetFiles(dir); }
                catch { continue; }
                foreach (string path in files)
                {
                    try
                    {
                        string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                        if (ext != ".lnk" && !ShieldGate.Detect.ScriptExts.Contains(ext)) continue;
                        FileAttributes attrs = File.GetAttributes(path);
                        List<string> why;
                        bool bad = ext == ".lnk"
                            ? ShieldGate.Detect.LnkVerdict(path, dir, attrs, out why)
                            : ShieldGate.Detect.ScriptVerdict(path, attrs, 0, out why);
                        if (!bad) continue;
                        PcItem i = New(ext == ".lnk" ? PcKind.DriveShortcut : PcKind.DroppedFile,
                                       true, System.IO.Path.GetFileName(path),
                                       path + "  ->  " + string.Join("; ", why.ToArray()));
                        i.Data["path"] = path;
                        i.Data["root"] = dir;
                        findings.Add(i);
                    }
                    catch { }
                }
            }
        }

        private static void AddDir(List<string> dirs, string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            foreach (string e in dirs)
                if (string.Equals(e, dir, StringComparison.OrdinalIgnoreCase)) return;
            dirs.Add(dir);
        }

        // WMI permanent event subscription: cach virus chay lai moi khi cam USB
        // ma khong can tien trinh hay muc khoi dong nao.
        private static void ScanWmi(List<PcItem> findings)
        {
            string[] classes = { "CommandLineEventConsumer", "ActiveScriptEventConsumer" };
            ManagementScope scope = null;
            try
            {
                scope = new ManagementScope(@"\\.\root\subscription");
                scope.Connect();
            }
            catch { return; }

            foreach (string cls in classes)
            {
                try
                {
                    ManagementObjectSearcher s = new ManagementObjectSearcher(scope,
                        new ObjectQuery("SELECT * FROM " + cls));
                    s.Options.Timeout = TimeSpan.FromSeconds(10);
                    foreach (ManagementBaseObject o in s.Get())
                    {
                        ManagementObject mo = o as ManagementObject;
                        if (mo == null) continue;
                        try
                        {
                            string name = Convert.ToString(mo["Name"]);
                            string action = cls == "CommandLineEventConsumer"
                                ? (Convert.ToString(mo["ExecutablePath"]) + " " +
                                   Convert.ToString(mo["CommandLineTemplate"])).Trim()
                                : Convert.ToString(mo["ScriptText"]);
                            ShieldGate.CommandCheck v = ShieldGate.Detect.CommandVerdict(action);
                            bool high = cls == "ActiveScriptEventConsumer" || (v != null && v.High);
                            PcItem i = New(PcKind.WmiConsumer, high, name,
                                           cls + "  ->  " + Cut(action, 300));
                            i.Data["class"] = cls;
                            i.Data["name"] = name;
                            findings.Add(i);
                        }
                        catch { }
                        finally { try { mo.Dispose(); } catch { } }
                    }
                }
                catch { }
            }
        }

        // Virus doi chuong trinh mo tep .lnk de moi shortcut deu chay ma doc.
        private static void ScanLnkAssoc(List<PcItem> findings)
        {
            object val;
            RegistryValueKind kind;
            string ext = @"Software\Classes\.lnk";
            if (Reg.Read(RegistryHive.CurrentUser, ext, "", out val, out kind))
            {
                string prog = Convert.ToString(val);
                if (!string.IsNullOrEmpty(prog)
                    && !string.Equals(prog.Trim(), "lnkfile", StringComparison.OrdinalIgnoreCase))
                {
                    PcItem i = New(PcKind.LnkAssoc, true, ".lnk", ext + "  ->  " + prog);
                    i.Data["hive"] = RegistryHive.CurrentUser;
                    i.Data["path"] = ext;
                    i.Data["name"] = "";
                    findings.Add(i);
                }
            }

            string uc = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.lnk\UserChoice";
            if (Reg.Read(RegistryHive.CurrentUser, uc, "ProgId", out val, out kind))
            {
                string prog = Convert.ToString(val);
                if (!string.IsNullOrEmpty(prog)
                    && !string.Equals(prog.Trim(), "lnkfile", StringComparison.OrdinalIgnoreCase))
                {
                    PcItem i = New(PcKind.LnkAssoc, true, ".lnk", uc + "  ->  " + prog);
                    i.Data["hive"] = RegistryHive.CurrentUser;
                    i.Data["path"] = uc;
                    i.Data["name"] = "ProgId";
                    findings.Add(i);
                }
            }
        }

        private static string Cut(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length > n ? s.Substring(0, n) + "…" : s;
        }


        private static void ScanStartupDir(string folder, List<PcItem> findings)
        {
            try
            {
                foreach (string path in Directory.GetFileSystemEntries(folder))
                {
                    try { if (Directory.Exists(path)) continue; }
                    catch { continue; }
                    string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                    if (!ShieldGate.Detect.ScriptExts.Contains(ext) && ext != ".lnk") continue;
                    FileAttributes attrs = File.GetAttributes(path);
                    List<string> why;
                    bool bad = ext == ".lnk"
                        ? ShieldGate.Detect.LnkVerdict(path, folder, attrs, out why)
                        : ShieldGate.Detect.ScriptVerdict(path, attrs, 0, out why);
                    if (!bad) continue;
                    PcItem i = New(PcKind.StartupFile, true, System.IO.Path.GetFileName(path),
                                   string.Join("; ", why.ToArray()) + "  ->  " + path);
                    i.Data["path"] = path;
                    i.Data["root"] = folder;
                    findings.Add(i);
                }
            }
            catch { }
        }

        private static void ScanTasks(List<PcItem> findings)
        {
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string tasksDir = System.IO.Path.Combine(win, @"System32\Tasks");
            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
            {
                string sysnative = System.IO.Path.Combine(win, @"Sysnative\Tasks");
                if (Directory.Exists(sysnative)) tasksDir = sysnative;
            }
            if (!Directory.Exists(tasksDir)) return;
            WalkTasks(tasksDir, tasksDir, findings, 0);
        }

        private static void WalkTasks(string tasksRoot, string dir, List<PcItem> findings, int depth)
        {
            if (depth > 8) return;
            string[] entries;
            try { entries = Directory.GetFileSystemEntries(dir); }
            catch { return; }
            foreach (string path in entries)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        WalkTasks(tasksRoot, path, findings, depth + 1);
                        continue;
                    }
                    string rel = path.Substring(tasksRoot.Length).TrimStart('\\');
                    if (rel.StartsWith("Microsoft\\", StringComparison.OrdinalIgnoreCase)) continue;
                    XmlDocument doc = new XmlDocument();
                    doc.XmlResolver = null;
                    doc.Load(path);
                    XmlNodeList cmds = doc.GetElementsByTagName("Command");
                    XmlNodeList argsList = doc.GetElementsByTagName("Arguments");
                    string action = "";
                    foreach (XmlNode n in cmds) action += n.InnerText + " ";
                    foreach (XmlNode n in argsList) action += n.InnerText + " ";
                    action = Environment.ExpandEnvironmentVariables(action).Trim();
                    if (action.Length == 0) continue;
                    ShieldGate.CommandCheck v = ShieldGate.Detect.CommandVerdict(action);
                    if (v == null || !v.High) continue;
                    string name = System.IO.Path.GetFileName(rel);
                    string tpath = "\\" + System.IO.Path.GetDirectoryName(rel);
                    PcItem i = New(PcKind.Task, true, name, "\\" + rel + "  ->  " + action);
                    i.Data["name"] = name;
                    i.Data["path"] = tpath;
                    findings.Add(i);
                }
                catch { }
            }
        }

        // ----------------------------------------------------------------- sửa

        private static void BackupReg(RegistryHive hive, string path)
        {
            if (!Native.IsAdmin()) return;
            Paths.EnsureDirs();
            string full = (hive == RegistryHive.LocalMachine ? "HKEY_LOCAL_MACHINE\\" : "HKEY_CURRENT_USER\\") + path;
            string safe = Regex.Replace(full, @"[\\/:*?""<>|]", "_");
            string file = System.IO.Path.Combine(Paths.BakDir,
                "reg_" + safe + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".reg");
            string output;
            Native.Run("reg.exe", "export \"" + full + "\" \"" + file + "\" /y", 20000, out output);
        }

        public static void Fix(IList<PcItem> items, Action<PcItem, bool, string> done, Func<bool> cancelled,
                               out int okCount, out int failCount)
        {
            okCount = 0;
            failCount = 0;
            string batch = Quarantine.NewBatch("pc", "");
            foreach (PcItem f in items)
            {
                if (cancelled != null && cancelled()) break;
                if (!f.Fixable) continue;
                bool ok = false;
                string err = "";
                try
                {
                    RegistryHive hive = f.Data.ContainsKey("hive")
                        ? (RegistryHive)f.Data["hive"] : RegistryHive.LocalMachine;
                    switch (f.Kind)
                    {
                        case PcKind.RunValue:
                            BackupReg(hive, Str(f, "path"));
                            ok = Reg.DeleteValue(hive, Str(f, "path"), Str(f, "name"));
                            break;
                        case PcKind.WinlogonShell:
                        case PcKind.WinlogonUserinit:
                        case PcKind.AppInit:
                            BackupReg(hive, Str(f, "path"));
                            ok = Reg.Write(hive, Str(f, "path"), Str(f, "name"), f.Data["value"],
                                           f.Data.ContainsKey("kind")
                                               ? (RegistryValueKind)f.Data["kind"] : RegistryValueKind.String);
                            break;
                        case PcKind.MountPoint:
                            BackupReg(hive, Str(f, "path"));
                            ok = Reg.DeleteTree(hive, Str(f, "path"));
                            break;
                        case PcKind.StartupFile:
                            ok = Quarantine.Add(Str(f, "path"), Str(f, "root"), batch, "startup", out err);
                            break;
                        case PcKind.Task:
                            string tn = Str(f, "path").TrimEnd('\\') + "\\" + Str(f, "name");
                            string outp;
                            ok = Native.Run("schtasks.exe", "/Change /TN \"" + tn + "\" /DISABLE", 20000, out outp) == 0;
                            err = outp;
                            break;
                        case PcKind.DroppedFile:
                            ok = Quarantine.Add(Str(f, "path"), Str(f, "root"), batch, "dropped", out err);
                            break;
                        case PcKind.DriveShortcut:
                        case PcKind.DriveExe:
                            ok = Quarantine.Add(Str(f, "path"), Str(f, "root"), batch, "drive", out err);
                            break;
                        case PcKind.DriveFakeFolder:
                            string folder = Str(f, "folder");
                            string droot = Str(f, "root");
                            int innerFail = 0;
                            UsbScan.QuarantineTree(System.IO.Path.Combine(droot, folder), droot, batch, ref innerFail);
                            UsbRestore.RemoveWrapper(droot, folder);
                            ok = innerFail == 0;
                            if (innerFail > 0) err = S.Err.Unknown;
                            break;
                        case PcKind.DriveHiddenFolder:
                            RestoreResult rr = UsbRestore.Restore(Str(f, "root"),
                                new List<string> { Str(f, "folder") }, null);
                            ok = rr.DirsLeft == 0;
                            if (!ok) err = S.Err.Unknown;
                            break;
                        case PcKind.WmiConsumer:
                            ok = DeleteWmiConsumer(Str(f, "class"), Str(f, "name"));
                            break;
                        case PcKind.LnkAssoc:
                            BackupReg(hive, Str(f, "path"));
                            ok = Reg.DeleteValue(hive, Str(f, "path"), Str(f, "name"));
                            if (!ok) ok = Reg.DeleteTree(hive, Str(f, "path"));
                            break;
                        case PcKind.Process:
                        case PcKind.FakeProcess:
                            try
                            {
                                using (Process p = Process.GetProcessById(Convert.ToInt32(f.Data["pid"])))
                                    p.Kill();
                                ok = true;
                            }
                            catch (Exception e) { err = e.Message; }
                            break;
                    }
                }
                catch (Exception e) { ok = false; err = e.Message; }

                if (ok) { okCount++; Log.Write(S.Log.PcFixed(f.Kind.ToString(), f.Arg1), "WARN"); }
                else failCount++;
                if (done != null) done(f, ok, err);
            }
        }

        private static string Str(PcItem f, string key)
        {
            return f.Data.ContainsKey(key) ? Convert.ToString(f.Data[key]) : "";
        }

        // Xoá một WMI consumer cùng các binding trỏ tới nó. Đây là cách virus
        // chạy lại theo sự kiện (cắm USB, đăng nhập) mà không cần tệp khởi động.
        private static bool DeleteWmiConsumer(string cls, string name)
        {
            if (string.IsNullOrEmpty(cls) || string.IsNullOrEmpty(name)) return false;
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\subscription");
                scope.Connect();

                ManagementObjectSearcher bs = new ManagementObjectSearcher(scope,
                    new ObjectQuery("SELECT * FROM __FilterToConsumerBinding"));
                bs.Options.Timeout = TimeSpan.FromSeconds(10);
                foreach (ManagementBaseObject o in bs.Get())
                {
                    ManagementObject mo = o as ManagementObject;
                    if (mo == null) continue;
                    try
                    {
                        string consumer = Convert.ToString(mo["Consumer"]);
                        if (!string.IsNullOrEmpty(consumer)
                            && consumer.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0
                            && consumer.IndexOf(cls, StringComparison.OrdinalIgnoreCase) >= 0)
                            mo.Delete();
                    }
                    catch { }
                    finally { try { mo.Dispose(); } catch { } }
                }

                string safe = name.Replace("'", "\\'");
                ManagementObjectSearcher s = new ManagementObjectSearcher(scope,
                    new ObjectQuery("SELECT * FROM " + cls + " WHERE Name='" + safe + "'"));
                s.Options.Timeout = TimeSpan.FromSeconds(10);
                bool ok = false;
                foreach (ManagementBaseObject o in s.Get())
                {
                    ManagementObject mo = o as ManagementObject;
                    if (mo == null) continue;
                    try { mo.Delete(); ok = true; }
                    catch { }
                    finally { try { mo.Dispose(); } catch { } }
                }
                return ok;
            }
            catch { return false; }
        }
    }

    // ------------------------------------------------------------------ registry

    internal static class Reg
    {
        public static RegistryKey Base(RegistryHive hive)
        {
            return RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        }

        public static bool Read(RegistryHive hive, string path, string name,
                                out object value, out RegistryValueKind kind)
        {
            value = null;
            kind = RegistryValueKind.Unknown;
            try
            {
                using (RegistryKey k = Base(hive).OpenSubKey(path, false))
                {
                    if (k == null) return false;
                    object v = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (v == null) return false;
                    kind = k.GetValueKind(name);
                    value = v;
                    return true;
                }
            }
            catch { return false; }
        }

        public static bool Write(RegistryHive hive, string path, string name,
                                 object value, RegistryValueKind kind)
        {
            try
            {
                using (RegistryKey k = Base(hive).CreateSubKey(path))
                    k.SetValue(name, value, kind);
                return true;
            }
            catch { return false; }
        }

        public static bool DeleteValue(RegistryHive hive, string path, string name)
        {
            try
            {
                using (RegistryKey k = Base(hive).OpenSubKey(path, true))
                {
                    if (k == null) return false;
                    k.DeleteValue(name, false);
                }
                return true;
            }
            catch { return false; }
        }

        public static bool DeleteTree(RegistryHive hive, string path)
        {
            try
            {
                Base(hive).DeleteSubKeyTree(path, false);
                return true;
            }
            catch { return false; }
        }

        public static List<string> SubKeys(RegistryHive hive, string path)
        {
            List<string> list = new List<string>();
            try
            {
                using (RegistryKey k = Base(hive).OpenSubKey(path, false))
                {
                    if (k == null) return list;
                    list.AddRange(k.GetSubKeyNames());
                }
            }
            catch { }
            return list;
        }

        public static List<KeyValuePair<string, object>> Values(RegistryHive hive, string path)
        {
            List<KeyValuePair<string, object>> list = new List<KeyValuePair<string, object>>();
            try
            {
                using (RegistryKey k = Base(hive).OpenSubKey(path, false))
                {
                    if (k == null) return list;
                    foreach (string name in k.GetValueNames())
                    {
                        object v = null;
                        try { v = k.GetValue(name); }
                        catch { }
                        list.Add(new KeyValuePair<string, object>(name, v));
                    }
                }
            }
            catch { }
            return list;
        }
    }
}
