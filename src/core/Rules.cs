// ShieldGate 2.0 - luat va trong so tach khoi ma. Doc rules.json de cap nhat
// ten tep / duoi tep / nguong diem ma khong phai build lai.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ShieldGate
{
    internal sealed class RulesData
    {
        public const int CurrentVersion = 6;

        public int Version = CurrentVersion;
        public int ScoreThreshold = 3;
        public int MaxFiles = 150000;
        public int MaxSeconds = 300;
        public int BehaviorLnkBurst = 15;
        public int BehaviorWriteBurst = 400;
        public int BehaviorWindowSeconds = 10;
        // Cung o chi bao lai sau khoang nay: tranh toast 2 phut/lan khi o dang
        // bi ghi lien tuc (B8).
        public int BehaviorAlertSeconds = 600;

        // BadUSB chi la heuristic: toc do go bat thuong + thiet bi moi + shell
        // moi. Nguong nam trong rules.json de chinh duoc ma khong build lai.
        public bool BadUsbEnabled = true;
        public int BadUsbNewDeviceSeconds = 120;
        public int BadUsbMinEvents = 30;
        public int BadUsbEventsPerSecond = 15;
        public int BadUsbShellSeconds = 10;
        public int BadUsbAlertSeconds = 600;
        public bool BadUsbRequireExplorerParent = true;

        public readonly HashSet<string> ScriptExts =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> RootScriptExts =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> DoubleExtDecoys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> DangerousExts =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Tep "payload" cua virus shortcut nam trong thu muc gia. Chi nhung duoi
        // nay moi bi cach ly; tai lieu nguoi dung (doc/pdf/xls...) duoc giu lai.
        public readonly HashSet<string> PayloadExts =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> MacroExts =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> NtlmExts =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> SideloadDlls =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> AdsSkipStreams =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Ten tep va mau nhan dien (AGENTS điều 8): nằm trong rules.json, không
        // rải hằng số trong Detect.cs. Người dùng thêm mẫu mới mà không build lại.
        public readonly HashSet<string> DangerExes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> SystemNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> PayloadNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> PayloadHashes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<long> PayloadSizes = new List<long>();
        public readonly List<string> ScriptMarkers = new List<string>();
        public readonly List<string[]> HighPatterns = new List<string[]>();
        public readonly List<string[]> MedPatterns = new List<string[]>();
        // Bo duoi tep ma bo doc script 1.0 coi la script khi quet o. Hep hon
        // ScriptExts (dung cho cac detector 2.0) de giu hanh vi cu.
        public readonly HashSet<string> ScriptScanExts =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, int> _weights =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public int Weight(string code)
        {
            if (string.IsNullOrEmpty(code)) return 1;
            int cut = code.IndexOf('=');
            if (cut < 0) cut = code.IndexOf(':');
            string key = cut > 0 ? code.Substring(0, cut) : code;
            int w;
            if (_weights.TryGetValue(key, out w)) return w;
            return 1;
        }

        public void SetWeights(IDictionary<string, int> values)
        {
            _weights.Clear();
            foreach (KeyValuePair<string, int> kv in values) _weights[kv.Key] = kv.Value;
        }

        public void AddWeight(string key, int value)
        {
            _weights[key] = value;
        }

        public bool IsScriptExt(string ext)
        {
            return !string.IsNullOrEmpty(ext) && ScriptExts.Contains(ext);
        }

        public bool IsRootScriptExt(string ext)
        {
            return !string.IsNullOrEmpty(ext) && RootScriptExts.Contains(ext);
        }

        public bool IsDoubleExtDecoy(string ext)
        {
            return !string.IsNullOrEmpty(ext) && DoubleExtDecoys.Contains(ext);
        }

        public bool IsDangerousExt(string ext)
        {
            return !string.IsNullOrEmpty(ext) && DangerousExts.Contains(ext);
        }

        public bool IsPayloadExt(string ext)
        {
            return !string.IsNullOrEmpty(ext) && PayloadExts.Contains(ext);
        }

        // Duoi tep duoc tinh la "dang ngo" khi xet bung no ghi: .lnk, script va
        // payload. Tai lieu hang loat (.txt, .jpg...) khong duoc coi la dang ngo.
        public bool IsBurstRiskExt(string ext)
        {
            return string.Equals(ext, ".lnk", StringComparison.OrdinalIgnoreCase)
                   || IsScriptExt(ext) || IsPayloadExt(ext);
        }

        public bool IsMacroExt(string ext)
        {
            return !string.IsNullOrEmpty(ext) && MacroExts.Contains(ext);
        }

        public bool IsNtlmExt(string ext)
        {
            return !string.IsNullOrEmpty(ext) && NtlmExts.Contains(ext);
        }

        public bool IsSideloadDll(string name)
        {
            return !string.IsNullOrEmpty(name) && SideloadDlls.Contains(name);
        }

        public bool IsAdsSkipped(string stream)
        {
            return !string.IsNullOrEmpty(stream) && AdsSkipStreams.Contains(stream);
        }

        public bool IsDangerExe(string name)
        {
            return !string.IsNullOrEmpty(name) && DangerExes.Contains(name);
        }

        public bool IsSystemName(string name)
        {
            return !string.IsNullOrEmpty(name) && SystemNames.Contains(name);
        }

        public bool IsPayloadName(string name)
        {
            return !string.IsNullOrEmpty(name) && PayloadNames.Contains(name);
        }

        public bool IsPayloadHash(string hash)
        {
            return !string.IsNullOrEmpty(hash) && PayloadHashes.Contains(hash);
        }

        public bool IsScriptScanExt(string ext)
        {
            return !string.IsNullOrEmpty(ext) && ScriptScanExts.Contains(ext);
        }
    }

    internal static class Rules
    {
        private static readonly object Gate = new object();
        private static RulesData _current;
        private static bool _warnedBeside;

        public static RulesData Current
        {
            get
            {
                lock (Gate)
                {
                    if (_current == null) _current = Load();
                    return _current;
                }
            }
        }

        public static void Reload()
        {
            lock (Gate) _current = Load();
        }

        // Ghi đè rules.json trong thư mục dữ liệu bằng tệp người dùng chọn. Chỉ
        // nhận tệp parse được và nhỏ (luật không cần lớn); bản cũ giữ thành
        // rules.json.bak để quay lại nếu nhập nhầm.
        public static bool Import(string source, out string error)
        {
            error = "";
            try
            {
                if (string.IsNullOrEmpty(source) || !File.Exists(source))
                {
                    error = S.Cli.ImportRulesMissing(source ?? "");
                    return false;
                }
                if (new FileInfo(source).Length > 1024 * 1024)
                {
                    error = S.Cli.ImportRulesTooBig;
                    return false;
                }
                if (Json.AsObject(Json.ParseFile(source)) == null)
                {
                    error = S.Cli.ImportRulesInvalid(source);
                    return false;
                }

                Paths.EnsureDirs();
                string from = Path.GetFullPath(source);
                string dest = Path.GetFullPath(Paths.RulesFile);
                if (string.Equals(from, dest, StringComparison.OrdinalIgnoreCase))
                {
                    Reload();
                    return true;
                }
                if (File.Exists(dest))
                {
                    try { File.Copy(dest, dest + ".bak", true); }
                    catch { }
                }
                File.Copy(from, dest, true);
                Reload();
                return true;
            }
            catch (Exception e)
            {
                error = S.Cli.ImportRulesInvalid(e.Message);
                return false;
            }
        }

        public static string CandidatePath()
        {
            // Luật chỉ đọc từ thư mục dữ liệu. rules.json cạnh exe bị bỏ qua: bản
            // chạy từ USB thường nằm trên ổ do người khác quản lý, nên một tệp
            // cạnh exe có thể bị thay mà người dùng không biết.
            try
            {
                string exe = System.Reflection.Assembly.GetEntryAssembly().Location;
                string dir = Path.GetDirectoryName(exe);
                if (!string.IsNullOrEmpty(dir))
                {
                    string beside = Path.Combine(dir, "rules.json");
                    if (File.Exists(beside) && !_warnedBeside)
                    {
                        _warnedBeside = true;
                        Log.Write(S.Log.RulesBesideIgnored(beside), "WARN");
                    }
                }
            }
            catch { }
            return Paths.RulesFile;
        }

        private static RulesData Load()
        {
            RulesData data = Defaults();
            try
            {
                string path = CandidatePath();
                if (!File.Exists(path))
                {
                    // Chỉ tạo tệp mẫu trong thư mục dữ liệu, không ghi cạnh exe
                    // (USB có thể chỉ đọc hoặc do người khác quản lý).
                    try
                    {
                        Paths.EnsureDirs();
                        if (!File.Exists(Paths.RulesFile))
                            File.WriteAllText(Paths.RulesFile, DefaultJson, new UTF8Encoding(false));
                    }
                    catch { }
                    return data;
                }

                Dictionary<string, object> root = Json.AsObject(Json.ParseFile(path));
                if (root == null) throw new FormatException("rules.json phai la object");

                data.Version = Json.Int(root, "version", data.Version);
                if (data.Version < RulesData.CurrentVersion)
                {
                    // Tệp cũ thiếu khoá mới: giữ mặc định trong mã và chỉ thêm phần
                    // người dùng có, không ghi đè lại tệp của họ.
                    Log.Write(S.Log.RulesMerged(data.Version, RulesData.CurrentVersion), "INFO");
                    data.Version = RulesData.CurrentVersion;
                }
                int threshold = Json.Int(root, "scoreThreshold", data.ScoreThreshold);
                data.ScoreThreshold = threshold < 1 ? 1 : (threshold > 10 ? 10 : threshold);

                Dictionary<string, object> limits = Json.AsObject(Json.Get(root, "limits"));
                if (limits != null)
                {
                    data.MaxFiles = Math.Max(1000, Json.Int(limits, "maxFiles", data.MaxFiles));
                    data.MaxSeconds = Math.Max(10, Json.Int(limits, "maxSeconds", data.MaxSeconds));
                }

                Dictionary<string, object> behavior = Json.AsObject(Json.Get(root, "behavior"));
                if (behavior != null)
                {
                    data.BehaviorLnkBurst = Math.Max(3, Json.Int(behavior, "lnkBurst", data.BehaviorLnkBurst));
                    data.BehaviorWriteBurst = Math.Max(20, Json.Int(behavior, "writeBurst", data.BehaviorWriteBurst));
                    data.BehaviorWindowSeconds = Math.Max(3, Json.Int(behavior, "windowSeconds", data.BehaviorWindowSeconds));
                    data.BehaviorAlertSeconds = Math.Max(30, Json.Int(behavior, "alertSeconds", data.BehaviorAlertSeconds));
                }

                // Gia tri ngoai khoang hop ly bi kep lai: mot so 0 hay am se lam
                // canh bao BadUSB bat vo dieu kien hoac khong bao gio bat.
                Dictionary<string, object> badusb = Json.AsObject(Json.Get(root, "badusb"));
                if (badusb != null)
                {
                    data.BadUsbEnabled = Json.Bool(badusb, "enabled", data.BadUsbEnabled);
                    data.BadUsbNewDeviceSeconds = Clamp(
                        Json.Int(badusb, "newDeviceSeconds", data.BadUsbNewDeviceSeconds), 10, 3600);
                    data.BadUsbMinEvents = Clamp(
                        Json.Int(badusb, "minEvents", data.BadUsbMinEvents), 5, 10000);
                    data.BadUsbEventsPerSecond = Clamp(
                        Json.Int(badusb, "eventsPerSecond", data.BadUsbEventsPerSecond), 2, 10000);
                    data.BadUsbShellSeconds = Clamp(
                        Json.Int(badusb, "shellSeconds", data.BadUsbShellSeconds), 1, 120);
                    data.BadUsbAlertSeconds = Clamp(
                        Json.Int(badusb, "alertSeconds", data.BadUsbAlertSeconds), 30, 86400);
                    data.BadUsbRequireExplorerParent = Json.Bool(
                        badusb, "requireExplorerParent", data.BadUsbRequireExplorerParent);
                }

                MergeSet(data.ScriptExts, Json.StringList(root, "scriptExts"));
                MergeSet(data.RootScriptExts, Json.StringList(root, "rootScriptExts"));
                MergeSet(data.DoubleExtDecoys, Json.StringList(root, "doubleExtDecoys"));
                MergeSet(data.DangerousExts, Json.StringList(root, "dangerousExts"));
                MergeSet(data.PayloadExts, Json.StringList(root, "payloadExts"));
                MergeSet(data.MacroExts, Json.StringList(root, "officeMacroExts"));
                MergeSet(data.NtlmExts, Json.StringList(root, "ntlmExts"));
                MergeSet(data.SideloadDlls, Json.StringList(root, "sideloadDlls"));
                MergeSet(data.AdsSkipStreams, Json.StringList(root, "adsSkipStreams"));
                MergeNames(data.DangerExes, Json.StringList(root, "dangerExes"));
                MergeNames(data.SystemNames, Json.StringList(root, "systemNames"));
                MergeNames(data.PayloadNames, Json.StringList(root, "payloadNames"));
                MergeNames(data.PayloadHashes, Json.StringList(root, "payloadHashes"));
                MergeSizes(data.PayloadSizes, Json.AsArray(Json.Get(root, "payloadSizes")));
                MergeMarkers(data.ScriptMarkers, Json.StringList(root, "scriptMarkers"));
                MergePatterns(data.HighPatterns, Json.AsArray(Json.Get(root, "highPatterns")));
                MergePatterns(data.MedPatterns, Json.AsArray(Json.Get(root, "medPatterns")));
                MergeSet(data.ScriptScanExts, Json.StringList(root, "scriptScanExts"));

                Dictionary<string, object> weights = Json.AsObject(Json.Get(root, "weights"));
                if (weights != null)
                {
                    foreach (KeyValuePair<string, object> kv in weights)
                    {
                        int w;
                        try { w = Convert.ToInt32(kv.Value); }
                        catch { continue; }
                        // Ngoài 0..10 là lỗi gõ: kẹp lại thay vì để điểm lệch.
                        if (w < 0) w = 0;
                        if (w > 10) w = 10;
                        data.AddWeight(kv.Key, w);
                    }
                }
            }
            catch (Exception e)
            {
                try { Log.Write("rules.json loi, dung mac dinh: " + e.Message, "WARN"); }
                catch { }
            }
            return data;
        }

        // Luật người dùng chỉ thêm vào mặc định: tệp cũ hay tệp thiếu khoá mới
        // không làm mất đuôi mà bản này đã biết.
        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static void MergeSet(HashSet<string> set, List<string> values)
        {
            if (values == null) return;
            foreach (string v in values)
            {
                string s = (v ?? "").Trim();
                if (s.Length == 0) continue;
                if (s[0] != '.' && (s.IndexOf('.') < 0)) s = "." + s;
                set.Add(s);
            }
        }

        // Tên tệp/tiến trình giữ nguyên, không thêm dấu chấm như đuôi tệp.
        private static void MergeNames(HashSet<string> set, List<string> values)
        {
            if (values == null) return;
            foreach (string v in values)
            {
                string s = (v ?? "").Trim();
                if (s.Length > 0) set.Add(s);
            }
        }

        private static void MergeSizes(List<long> target, List<object> values)
        {
            if (values == null) return;
            foreach (object v in values)
            {
                long n;
                try { n = Convert.ToInt64(v); }
                catch { continue; }
                if (n <= 0 || n > 64L * 1024 * 1024) continue;
                if (!target.Contains(n)) target.Add(n);
            }
        }

        private static void MergeMarkers(List<string> target, List<string> values)
        {
            if (values == null) return;
            foreach (string v in values)
            {
                string s = (v ?? "").Trim().ToLowerInvariant();
                if (s.Length == 0 || target.Contains(s)) continue;
                target.Add(s);
            }
        }

        // Mẫu regex hỏng bị bỏ qua thay vì làm sập cả tệp luật.
        private static void MergePatterns(List<string[]> target, List<object> rows)
        {
            if (rows == null) return;
            foreach (object row in rows)
            {
                List<object> pair = Json.AsArray(row);
                if (pair == null || pair.Count < 2) continue;
                string pattern = Convert.ToString(pair[0]);
                string code = Convert.ToString(pair[1]);
                if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(code)) continue;
                if (pattern.Length > 500) continue;
                try { new Regex(pattern); }
                catch { continue; }
                target.Add(new string[] { pattern, code });
            }
        }

        private static RulesData Defaults()
        {
            RulesData d = new RulesData();
            d.BadUsbEnabled = true;
            d.BadUsbNewDeviceSeconds = 120;
            d.BadUsbMinEvents = 30;
            d.BadUsbEventsPerSecond = 15;
            d.BadUsbShellSeconds = 10;
            d.BadUsbAlertSeconds = 600;
            d.BadUsbRequireExplorerParent = true;
            MergeSet(d.ScriptExts, new List<string>(
                new string[] { ".vbs", ".vbe", ".js", ".jse", ".wsf", ".hta", ".ps1",
                               ".bat", ".cmd", ".scr", ".cpl" }));
            MergeSet(d.RootScriptExts, new List<string>(
                new string[] { ".vbs", ".vbe", ".js", ".jse", ".wsf", ".hta", ".ps1",
                               ".bat", ".cmd", ".scr", ".cpl", ".reg", ".msi", ".msp" }));
            MergeSet(d.DoubleExtDecoys, new List<string>(
                new string[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
                               ".txt", ".rtf", ".csv", ".jpg", ".jpeg", ".png", ".gif",
                               ".bmp", ".mp3", ".mp4", ".avi", ".mkv", ".mov", ".zip",
                               ".rar", ".7z", ".iso", ".html", ".htm", ".xml", ".json" }));
            MergeSet(d.DangerousExts, new List<string>(
                new string[] { ".exe", ".scr", ".com", ".pif", ".cpl", ".bat", ".cmd",
                               ".vbs", ".js", ".hta", ".ps1", ".msi", ".reg" }));
            // Duoi tep cua goi payload trong thu muc gia: 1.exe, LMIGuardianDll.dll,
            // adback.dat, link.dat, shortcut... Tai lieu nguoi dung khong nam o day.
            MergeSet(d.PayloadExts, new List<string>(
                new string[] { ".exe", ".scr", ".com", ".pif", ".cpl", ".dll", ".sys",
                               ".drv", ".ocx", ".bat", ".cmd", ".vbs", ".vbe", ".js",
                               ".jse", ".wsf", ".wsh", ".hta", ".ps1", ".msi", ".msp",
                               ".reg", ".lnk", ".url", ".scf", ".inf", ".dat", ".jar" }));
            MergeSet(d.MacroExts, new List<string>(
                new string[] { ".docm", ".xlsm", ".pptm", ".dotm", ".xltm", ".potm" }));
            MergeSet(d.NtlmExts, new List<string>(new string[] { ".url", ".scf", ".lnk" }));
            MergeSet(d.SideloadDlls, new List<string>(
                new string[] { "version.dll", "winmm.dll", "dbghelp.dll", "uxtheme.dll",
                               "profapi.dll", "wlbsctrl.dll", "cryptbase.dll", "dwmapi.dll",
                               "msimg32.dll", "ole32.dll", "secur32.dll", "userenv.dll",
                               "wtsapi32.dll", "netapi32.dll", "ntmarta.dll", "samcli.dll",
                               "sspicli.dll", "winsta.dll", "d3d9.dll", "d3dx9_43.dll" }));
            MergeSet(d.AdsSkipStreams, new List<string>(
                new string[] { "Zone.Identifier", "SmartScreen", "SummaryInformation", "{4c8cc155-6c1e-11d1-8e41-00c04fb9386d}" }));

            MergeNames(d.DangerExes, new List<string>(
                new string[] { "wscript.exe", "cscript.exe", "mshta.exe", "cmd.exe",
                               "powershell.exe", "pwsh.exe", "rundll32.exe", "regsvr32.exe",
                               "schtasks.exe", "bitsadmin.exe", "certutil.exe",
                               "msiexec.exe", "wmic.exe" }));
            MergeNames(d.SystemNames, new List<string>(
                new string[] { "svchost.exe", "csrss.exe", "lsass.exe", "services.exe",
                               "smss.exe", "winlogon.exe", "wininit.exe", "explorer.exe",
                               "spoolsv.exe", "taskhost.exe", "taskhostw.exe", "conhost.exe",
                               "dwm.exe", "ctfmon.exe", "lsm.exe", "userinit.exe",
                               "taskmgr.exe" }));
            MergeNames(d.PayloadNames, new List<string>(
                new string[] { "adback.dat", "link.dat", "opera_elf.dll" }));
            MergeNames(d.PayloadHashes, new List<string>(
                new string[] { "1668A64F5EFAC6B932FC725F33ECA219136EB49BE4115A731EFE877FFEBCD461",
                               "065DCE8809D18B824844A820F1AD9F9663752540F36B53F6162546089E3624A4",
                               "CF4D1B6917B4A26711E2A3C54D5466299C6D46DB446C215A70BD9389ECFF263E",
                               "B09FB3D4E84EC43C8E1EB39892F59B406E87AB5FA175713D75C045CD79D1CC67",
                               "E0DA42C12597D6D8620E7A4C477C4B94928F3CC34894FA0EFCCA2E3C02940931" }));
            d.PayloadSizes.AddRange(new long[] { 943, 3936, 177152, 303718, 1040920 });
            MergeSet(d.ScriptScanExts, new List<string>(
                new string[] { ".vbs", ".vbe", ".js", ".jse", ".wsf", ".hta" }));
            MergeMarkers(d.ScriptMarkers, new List<string>(
                new string[] { "wscript.shell", "createobject", "scripting.filesystemobject",
                               "shell.application", "attrib +", "-h -s", ".lnk", "wscript.exe",
                               "vbscript", "on error resume next" }));
            d.HighPatterns.AddRange(new string[][]
            {
                new string[] { @"\b(wscript|cscript|mshta)(\.exe)?\b", "wscript/mshta" },
                new string[] { @"\.vbe?\b|\.jse?\b", "script-file" },
                new string[] { @"powershell(\.exe)?.*(-enc|-encodedcommand)\b", "encoded-powershell" },
                new string[] { @"(bitsadmin|certutil).*(-urlcache|http)", "download-lolbin" },
                new string[] { @"regsvr32.*(/i|scrobj|http)", "regsvr32-remote" },
                new string[] { @"attrib\s+[+\-][hsrc]", "attrib-hiding" }
            });
            d.MedPatterns.AddRange(new string[][]
            {
                new string[] { @"powershell(\.exe)?.*(-windowstyle\s+hidden|-w\s+hidden|bypass)", "hidden-powershell" },
                new string[] { @"\\appdata\\local\\temp\\|\\windows\\temp\\", "temp-path" }
            });

            d.SetWeights(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                // 1.0
                { "target", 3 }, { "args", 3 }, { "args-relative-exe", 4 },
                { "args-drive-exe", 4 }, { "shadows-hidden-folder", 4 },
                { "target-in-hidden-folder", 4 }, { "target-in-fake-folder", 4 },
                { "target-odd-name", 2 }, { "named-after-drive", 3 },
                { "target-on-drive", 2 }, { "target-missing", 1 },
                { "unreadable-hidden", 2 }, { "markers", 2 }, { "encoded-vbscript", 4 },
                { "autorun-payload", 5 }, { "hidden-root-exe", 4 },
                { "fake-folder-exe", 5 }, { "nameless-folder", 4 },
                { "payload-bundle", 5 }, { "fake-folder-clsid", 5 },
                { "desktop-ini-clsid", 1 }, { "lookalike-folder", 1 },
                // 2.0
                { "double-extension", 5 }, { "rlo-name", 5 }, { "root-script", 2 },
                { "hidden-root-script", 4 }, { "ntlm-leak", 5 },
                { "alternate-data-stream", 2 }, { "hidden-ads", 4 },
                { "sideload-dll", 2 }, { "office-macro", 1 }, { "root-desktop-ini", 3 }
            });
            return d;
        }

        // Tệp mẫu: người dùng có thể sửa trực tiếp. Giữ đồng bộ với Defaults().
        public const string DefaultJson =
@"{
  ""version"": 6,
  ""scoreThreshold"": 3,
  ""limits"": { ""maxFiles"": 150000, ""maxSeconds"": 300 },
  ""behavior"": { ""lnkBurst"": 15, ""writeBurst"": 400, ""windowSeconds"": 10, ""alertSeconds"": 600 },
  ""badusb"": { ""enabled"": true, ""newDeviceSeconds"": 120, ""minEvents"": 30, ""eventsPerSecond"": 15, ""shellSeconds"": 10, ""alertSeconds"": 600, ""requireExplorerParent"": true },
  ""scriptExts"": [""vbs"", ""vbe"", ""js"", ""jse"", ""wsf"", ""hta"", ""ps1"", ""bat"", ""cmd"", ""scr"", ""cpl""],
  ""rootScriptExts"": [""vbs"", ""vbe"", ""js"", ""jse"", ""wsf"", ""hta"", ""ps1"", ""bat"", ""cmd"", ""scr"", ""cpl"", ""reg"", ""msi"", ""msp""],
  ""doubleExtDecoys"": [""pdf"", ""doc"", ""docx"", ""xls"", ""xlsx"", ""ppt"", ""pptx"", ""txt"", ""rtf"", ""csv"", ""jpg"", ""jpeg"", ""png"", ""gif"", ""bmp"", ""mp3"", ""mp4"", ""avi"", ""mkv"", ""mov"", ""zip"", ""rar"", ""7z"", ""iso"", ""html"", ""htm"", ""xml"", ""json""],
  ""dangerousExts"": [""exe"", ""scr"", ""com"", ""pif"", ""cpl"", ""bat"", ""cmd"", ""vbs"", ""js"", ""hta"", ""ps1"", ""msi"", ""reg""],
  ""payloadExts"": [""exe"", ""scr"", ""com"", ""pif"", ""cpl"", ""dll"", ""sys"", ""drv"", ""ocx"", ""bat"", ""cmd"", ""vbs"", ""vbe"", ""js"", ""jse"", ""wsf"", ""wsh"", ""hta"", ""ps1"", ""msi"", ""msp"", ""reg"", ""lnk"", ""url"", ""scf"", ""inf"", ""dat"", ""jar""],
  ""officeMacroExts"": [""docm"", ""xlsm"", ""pptm"", ""dotm"", ""xltm"", ""potm""],
  ""ntlmExts"": [""url"", ""scf"", ""lnk""],
  ""sideloadDlls"": [""version.dll"", ""winmm.dll"", ""dbghelp.dll"", ""uxtheme.dll"", ""profapi.dll"", ""wlbsctrl.dll"", ""cryptbase.dll"", ""dwmapi.dll"", ""msimg32.dll"", ""ole32.dll"", ""secur32.dll"", ""userenv.dll"", ""wtsapi32.dll"", ""netapi32.dll"", ""ntmarta.dll"", ""samcli.dll"", ""sspicli.dll"", ""winsta.dll"", ""d3d9.dll"", ""d3dx9_43.dll""],
  ""adsSkipStreams"": [""Zone.Identifier"", ""SmartScreen"", ""SummaryInformation"", ""{4c8cc155-6c1e-11d1-8e41-00c04fb9386d}""],
  ""dangerExes"": [""wscript.exe"", ""cscript.exe"", ""mshta.exe"", ""cmd.exe"", ""powershell.exe"", ""pwsh.exe"", ""rundll32.exe"", ""regsvr32.exe"", ""schtasks.exe"", ""bitsadmin.exe"", ""certutil.exe"", ""msiexec.exe"", ""wmic.exe""],
  ""systemNames"": [""svchost.exe"", ""csrss.exe"", ""lsass.exe"", ""services.exe"", ""smss.exe"", ""winlogon.exe"", ""wininit.exe"", ""explorer.exe"", ""spoolsv.exe"", ""taskhost.exe"", ""taskhostw.exe"", ""conhost.exe"", ""dwm.exe"", ""ctfmon.exe"", ""lsm.exe"", ""userinit.exe"", ""taskmgr.exe""],
  ""payloadNames"": [""adback.dat"", ""link.dat"", ""opera_elf.dll""],
  ""payloadHashes"": [""1668A64F5EFAC6B932FC725F33ECA219136EB49BE4115A731EFE877FFEBCD461"", ""065DCE8809D18B824844A820F1AD9F9663752540F36B53F6162546089E3624A4"", ""CF4D1B6917B4A26711E2A3C54D5466299C6D46DB446C215A70BD9389ECFF263E"", ""B09FB3D4E84EC43C8E1EB39892F59B406E87AB5FA175713D75C045CD79D1CC67"", ""E0DA42C12597D6D8620E7A4C477C4B94928F3CC34894FA0EFCCA2E3C02940931""],
  ""payloadSizes"": [943, 3936, 177152, 303718, 1040920],
  ""scriptScanExts"": [""vbs"", ""vbe"", ""js"", ""jse"", ""wsf"", ""hta""],
  ""scriptMarkers"": [""wscript.shell"", ""createobject"", ""scripting.filesystemobject"", ""shell.application"", ""attrib +"", ""-h -s"", "".lnk"", ""wscript.exe"", ""vbscript"", ""on error resume next""],
  ""highPatterns"": [[""\\b(wscript|cscript|mshta)(\\.exe)?\\b"", ""wscript/mshta""], [""\\.vbe?\\b|\\.jse?\\b"", ""script-file""], [""powershell(\\.exe)?.*(-enc|-encodedcommand)\\b"", ""encoded-powershell""], [""(bitsadmin|certutil).*(-urlcache|http)"", ""download-lolbin""], [""regsvr32.*(/i|scrobj|http)"", ""regsvr32-remote""], [""attrib\\s+[+\\-][hsrc]"", ""attrib-hiding""]],
  ""medPatterns"": [[""powershell(\\.exe)?.*(-windowstyle\\s+hidden|-w\\s+hidden|bypass)"", ""hidden-powershell""], [""\\\\appdata\\\\local\\\\temp\\\\|\\\\windows\\\\temp\\\\"", ""temp-path""]],
  ""weights"": {
    ""target"": 3, ""args"": 3, ""args-relative-exe"": 4, ""args-drive-exe"": 4,
    ""shadows-hidden-folder"": 4, ""target-in-hidden-folder"": 4, ""target-in-fake-folder"": 4,
    ""target-odd-name"": 2, ""named-after-drive"": 3, ""target-on-drive"": 2,
    ""target-missing"": 1, ""unreadable-hidden"": 2, ""markers"": 2, ""encoded-vbscript"": 4,
    ""autorun-payload"": 5, ""hidden-root-exe"": 4, ""fake-folder-exe"": 5,
    ""nameless-folder"": 4, ""payload-bundle"": 5, ""fake-folder-clsid"": 5,
    ""desktop-ini-clsid"": 1, ""lookalike-folder"": 1,
    ""double-extension"": 5, ""rlo-name"": 5, ""root-script"": 2, ""hidden-root-script"": 4,
    ""ntlm-leak"": 5, ""alternate-data-stream"": 2, ""hidden-ads"": 4,
    ""sideload-dll"": 2, ""office-macro"": 1, ""root-desktop-ini"": 3
  }
}
";
    }
}
