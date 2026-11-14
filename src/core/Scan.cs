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
    internal enum UsbKind { Shortcut, Script, Autorun, RootExe }

    internal sealed class UsbItem
    {
        public string Path;
        public string Rel;
        public UsbKind Kind;
        public List<string> Codes = new List<string>();
        public long Size;
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
            DateTime started = DateTime.UtcNow;
            UsbScanResult result = new UsbScanResult();
            result.Root = root;
            result.Drive = root.Length >= 2 ? root.Substring(0, 2) : root;
            int seen = 0;
            WalkStats stats = new WalkStats();

            foreach (TreeItem f in Tree.Walk(root, stats))
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
                if (r == "shadows-hidden-folder") return true;
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
            for (int i = 0; i < selected.Count; i++)
            {
                UsbItem it = selected[i];
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
            Log.Write(S.Log.QuarantineSummary(done, scan.Root, failed));
            return done;
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
        MountPoint, StartupFile, Task, Process, Defender
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
                        case PcKind.Process:
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
