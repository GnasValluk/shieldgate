// ShieldGate 6.1 - nhan dien shortcut / script / autorun doc hai.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ShieldGate
{
    internal sealed class LnkInfo
    {
        public string Target;
        public string Args;
        public string Icon;
        public string WorkDir;
    }

    internal sealed class CommandCheck
    {
        public bool High;
        public List<string> Reasons = new List<string>();
    }

    internal static class Detect
    {
        // Ten tep / duoi tep / mau nhan dien doc tu rules.json (Rules.Current).
        // Nho vay nguoi dung them mau moi ma khong phai build lai; xem AGENTS.md
        // dieu 8. Khong con hang so nhan dien nam trong tep nay.
        public static HashSet<string> DangerExes
        {
            get { return Rules.Current.DangerExes; }
        }

        public static HashSet<string> ScriptExts
        {
            get { return Rules.Current.ScriptScanExts; }
        }

        private static readonly Regex ArgsRe = new Regex(
            @"(-e:vbscript|-e:jscript|//e:vbscript|//e:jscript|\.vbs\b|\.vbe\b|\.jse?\b|mshta|https?://|powershell.*(-enc|-windowstyle\s+hidden|-w\s+hidden)|\bcmd(\.exe)?\s*/c\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AutorunRe = new Regex(@"\[autorun\]", RegexOptions.IgnoreCase);
        private static readonly Regex AutorunPayloadRe = new Regex(
            @"(open|shellexecute|action)\s*=.*(\.exe|\.vbs|\.vbe|\.bat|\.cmd|\.com|\.pif|\.scr|\.lnk|wscript|cscript|mshta|cmd|powershell)",
            RegexOptions.IgnoreCase);

        private static List<string> ScriptMarkers
        {
            get { return Rules.Current.ScriptMarkers; }
        }

        private static List<string[]> HighPatterns
        {
            get { return Rules.Current.HighPatterns; }
        }

        private static List<string[]> MedPatterns
        {
            get { return Rules.Current.MedPatterns; }
        }

        // Ten tep / hash / kich thuoc cua goi payload doc tu rules.json.
        public static HashSet<string> PayloadNames
        {
            get { return Rules.Current.PayloadNames; }
        }

        // Tep thuoc goi payload: ten da biet, hoac dung kich thuoc + hash cua mau.
        public static bool IsPayloadFile(string path)
        {
            try
            {
                FileInfo fi = new FileInfo(path);
                if (fi.Length > 10 * 1024 * 1024) return false;
                RulesData rules = Rules.Current;
                if (rules.IsPayloadName(fi.Name)) return true;
                foreach (long size in rules.PayloadSizes)
                    if (fi.Length == size)
                    {
                        string hash = Native.Sha256(path);
                        if (hash.Length > 0 && rules.IsPayloadHash(hash)) return true;
                        break;
                    }
            }
            catch { }
            return false;
        }

        // desktop.ini co CLSID bien thu muc thanh mot "shell folder" de gia dang thu muc
        // he thong (bieu tuong Documents/Internet Explorer) va giau noi dung ben trong.
        public static string DesktopIniClsid(string dir)
        {
            try
            {
                if (dir.StartsWith(@"\\?\UNC\", StringComparison.Ordinal)) dir = @"\\" + dir.Substring(8);
                else if (dir.StartsWith(@"\\?\", StringComparison.Ordinal)) dir = dir.Substring(4);
                string ini = Path.Combine(dir, "desktop.ini");
                if (!File.Exists(ini)) return "";
                byte[] buf = new byte[8192];
                int len;
                using (FileStream fs = new FileStream(ini, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    len = fs.Read(buf, 0, buf.Length);
                if (len <= 0) return "";
                // desktop.ini cua Windows la UTF-16; mot so virus ghi UTF-16 khong BOM.
                string text = Encoding.UTF8.GetString(buf, 0, len) + "\n" +
                              Encoding.Unicode.GetString(buf, 0, len & ~1);
                Match m = Regex.Match(text,
                    @"(?im)CLSID\s*=\s*(\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\})");
                return m.Success ? m.Groups[1].Value.ToLowerInvariant() : "";
            }
            catch { return ""; }
        }

        // Goi payload nam rai trong nhieu thu muc con (1\, 2\...).
        // Tra ve true khi thay cap tep dac trung hoac tep trung hash mau.
        public static bool HasPayloadBundle(string dir)
        {
            bool adback = false, linkdat = false, operaElf = false, icudtl = false;
            bool oddExe = false, dll = false, dat = false;
            int seen = 0;
            try
            {
                foreach (TreeItem t in Tree.Walk(dir, null, 600, 5))
                {
                    if (t.IsDir) continue;
                    if (++seen > 600) break;
                    string leaf = t.Name.ToLowerInvariant();
                    string ext = Path.GetExtension(leaf);
                    if (leaf == "adback.dat") adback = true;
                    else if (leaf == "link.dat") linkdat = true;
                    else if (leaf == "opera_elf.dll") operaElf = true;
                    else if (leaf == "icudtl.dat") icudtl = true;
                    if (ext == ".dll") dll = true;
                    else if (ext == ".dat") dat = true;
                    else if ((ext == ".exe" || ext == ".scr" || ext == ".com" || ext == ".pif")
                             && Regex.IsMatch(Path.GetFileNameWithoutExtension(leaf), @"^\d{1,3}$"))
                        oddExe = true;
                    if (IsPayloadFile(t.Path)) return true;
                }
            }
            catch { }
            if (adback && linkdat) return true;
            if (operaElf && icudtl) return true;
            return oddExe && dll && dat;
        }

        // Cham diem mot thu muc o goc o (hoac cap 1) xem co phai thu muc gia cua virus.
        public sealed class FolderCheck
        {
            public string Code = "";
            public bool Strong;
            public string Clsid = "";
            public bool Payload;
        }

        public static FolderCheck CheckFolder(string dir, string name)
        {
            FolderCheck c = new FolderCheck();
            bool misspelled = UsbRestore.IsMisspelledFolder(name);
            string clsid = DesktopIniClsid(dir);
            c.Clsid = clsid;

            bool payload = false;
            // Chi quet noi dung khi ten da dang ngo, de khong quet ca thu muc lon.
            if (misspelled || clsid.Length > 0) payload = HasPayloadBundle(dir);
            c.Payload = payload;

            if (payload)
            {
                c.Code = "payload-bundle";
                c.Strong = true;
                return c;
            }
            if (clsid.Length > 0 && misspelled)
            {
                c.Code = "fake-folder-clsid";
                c.Strong = true;
                return c;
            }
            if (clsid.Length > 0)
            {
                c.Code = "desktop-ini-clsid";
                c.Strong = false;
                return c;
            }
            if (misspelled)
            {
                c.Code = "lookalike-folder";
                c.Strong = false;
                return c;
            }
            return null;
        }

        // Ten tien trinh he thong: chay ngoai thu muc Windows la gia mao.
        public static HashSet<string> SystemNames
        {
            get { return Rules.Current.SystemNames; }
        }

        // Thu muc nguoi dung ghi duoc, noi virus hay tha tep chay.
        public static List<string> DropDirs()
        {
            List<string> dirs = new List<string>();
            string[] raw =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                System.IO.Path.GetTempPath(),
                Environment.GetEnvironmentVariable("PUBLIC")
            };
            foreach (string d in raw)
            {
                if (string.IsNullOrEmpty(d)) continue;
                string t = d.TrimEnd('\\');
                bool dup = false;
                foreach (string e in dirs)
                    if (string.Equals(e, t, StringComparison.OrdinalIgnoreCase)) dup = true;
                if (!dup && Directory.Exists(t)) dirs.Add(t);
            }
            return dirs;
        }

        // Chu ky phai xac minh duoc bang WinVerifyTrust, khong chi co blob chu ky.
        // Chu ky gia, bi thu hoi hoac chuoi tin cay dut deu tinh la khong ky.
        public static bool IsSigned(string path)
        {
            return Signature.Verify(path) == SignatureStatus.Signed;
        }

        private static string ExePathOf(string cmd)
        {
            string c = (cmd ?? "").Trim();
            if (c.Length == 0) return "";
            string p;
            if (c[0] == '"')
            {
                int e = c.IndexOf('"', 1);
                p = e > 1 ? c.Substring(1, e - 1) : c.Substring(1);
            }
            else
            {
                Match m = Regex.Match(c, @"^(.+?\.(exe|scr|com|pif))(\s|$)", RegexOptions.IgnoreCase);
                p = m.Success ? m.Groups[1].Value : c.Split(' ')[0];
            }
            return Environment.ExpandEnvironmentVariables(p.Trim());
        }

        // Lenh khoi dong tro vao tep chay co vi tri / ten dang ngo, du khong co script.
        public static string ExeLocationReason(string cmd)
        {
            try
            {
                string exe = ExePathOf(cmd);
                if (exe.Length < 4) return null;
                string ext = Path.GetExtension(exe).ToLowerInvariant();
                if (ext != ".exe" && ext != ".scr" && ext != ".com" && ext != ".pif") return null;
                string leaf = Path.GetFileName(exe);
                if (Regex.IsMatch(leaf, @"^\d{1,3}\.(exe|scr|com|pif)$")) return "odd-exe-name";
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                bool inWin = win.Length > 0 && exe.StartsWith(win + "\\", StringComparison.OrdinalIgnoreCase);
                if (SystemNames.Contains(leaf) && !inWin) return "fake-system-name";

                string low = exe.ToLowerInvariant();
                if (low.Contains("\\$recycle.bin\\") || low.Contains("\\recycler\\")) return "run-from-recycler";

                if (exe.Length > 2 && exe[1] == ':')
                {
                    try
                    {
                        if (new DriveInfo(exe.Substring(0, 1)).DriveType == DriveType.Removable)
                            return "run-from-removable";
                    }
                    catch { }
                }

                string dir = (Path.GetDirectoryName(exe) ?? "").TrimEnd('\\');
                foreach (string d in DropDirs())
                    if (string.Equals(d, dir, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(exe) && !IsSigned(exe)) return "unsigned-exe-in-drop-folder";
                        break;
                    }
            }
            catch { }
            return null;
        }

        // Tep chay nam trong tham so (cmd /c "...", action cua task, gia tri Run).
        // Chi bao khi duong dan dang ngo: tuong doi co ca thu muc, ten dang so,
        // trung ten/hash payload, hoac nam trong thu muc nguoi dung ghi duoc,
        // o di dong, thu muc an hay thu muc gia. Tep trong Program Files/Windows
        // va ung dung co chu ky trong AppData duoc bo qua de tranh bao nham.
        public static string EmbeddedExeReason(string value)
        {
            try
            {
                string exe = Environment.ExpandEnvironmentVariables((value ?? "").Trim().Trim('"', '\''));
                if (exe.Length < 4) return null;
                string leaf = Path.GetFileName(exe);
                if (PayloadNames.Contains(leaf)) return "payload-file";
                if (Regex.IsMatch(leaf, @"^\d{1,3}\.(exe|scr|com|pif)$")) return "odd-exe-name";

                if (!Path.IsPathRooted(exe))
                {
                    if (exe.IndexOf('\\') < 0 && exe.IndexOf('/') < 0) return null;
                    return "relative-exe-arg";
                }

                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                if (win.Length > 0 && exe.StartsWith(win + "\\", StringComparison.OrdinalIgnoreCase))
                    return null;
                if (!File.Exists(exe)) return null;

                if (IsPayloadFile(exe)) return "payload-file";
                if (exe.Length > 2 && exe[1] == ':')
                {
                    try
                    {
                        if (new DriveInfo(exe.Substring(0, 1)).DriveType == DriveType.Removable)
                            return "run-from-removable";
                    }
                    catch { }
                }

                string dir = Path.GetDirectoryName(exe);
                if (string.IsNullOrEmpty(dir)) return null;
                try
                {
                    if ((File.GetAttributes(dir) & FileAttributes.Hidden) != 0)
                        return "exe-in-hidden-folder";
                }
                catch { }
                FolderCheck fc = CheckFolder(dir, Path.GetFileName(dir));
                if (fc != null && fc.Strong) return "exe-in-fake-folder";

                foreach (string d in DropDirs())
                    if (string.Equals(d.TrimEnd('\\'), dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                        return IsSigned(exe) ? null : "unsigned-exe-in-drop-folder";
            }
            catch { }
            return null;
        }

        private static Encoding Cp1252()
        {
            try { return Encoding.GetEncoding(1252); }
            catch { return Encoding.ASCII; }
        }

        private static string ReadCounted(byte[] d, ref int pos, bool uni)
        {
            if (pos + 2 > d.Length) return "";
            int n = BitConverter.ToUInt16(d, pos);
            pos += 2;
            int bytes = uni ? n * 2 : n;
            if (pos + bytes > d.Length) bytes = Math.Max(0, d.Length - pos);
            string s = (uni ? Encoding.Unicode : Cp1252()).GetString(d, pos, bytes);
            pos += bytes;
            return s;
        }

        private static string ReadCStr(byte[] d, int off, bool uni)
        {
            if (off <= 0 || off >= d.Length) return "";
            if (uni)
            {
                int end = off;
                while (end + 1 < d.Length && !(d[end] == 0 && d[end + 1] == 0)) end += 2;
                return Encoding.Unicode.GetString(d, off, end - off);
            }
            int z = Array.IndexOf(d, (byte)0, off);
            if (z < 0) z = d.Length;
            return Cp1252().GetString(d, off, z - off);
        }

        public static LnkInfo ReadLnk(string path)
        {
            byte[] d;
            try
            {
                FileInfo fi = new FileInfo(path);
                if (fi.Length > 1024 * 1024) return null;
                d = File.ReadAllBytes(path);
            }
            catch { return null; }
            if (d.Length < 0x4C) return null;
            if (BitConverter.ToUInt32(d, 0) != 0x4C) return null;
            try
            {
                uint flags = BitConverter.ToUInt32(d, 0x14);
                int pos = 0x4C;
                if ((flags & 0x01) != 0)
                    pos += 2 + BitConverter.ToUInt16(d, pos);

                string target = "";
                if ((flags & 0x02) != 0 && pos + 0x1C <= d.Length)
                {
                    int li = pos;
                    uint liSize = BitConverter.ToUInt32(d, li);
                    uint hdr = BitConverter.ToUInt32(d, li + 4);
                    uint liFlags = BitConverter.ToUInt32(d, li + 8);
                    string basePath = "", suffix = "";
                    if ((liFlags & 0x01) != 0)
                    {
                        uint off = BitConverter.ToUInt32(d, li + 16);
                        uint uoff = hdr >= 0x24 ? BitConverter.ToUInt32(d, li + 28) : 0;
                        if (uoff != 0) basePath = ReadCStr(d, li + (int)uoff, true);
                        if (basePath.Length == 0 && off != 0)
                            basePath = ReadCStr(d, li + (int)off, false);
                    }
                    uint cps = BitConverter.ToUInt32(d, li + 24);
                    uint ucps = hdr >= 0x24 ? BitConverter.ToUInt32(d, li + 32) : 0;
                    if (ucps != 0) suffix = ReadCStr(d, li + (int)ucps, true);
                    if (suffix.Length == 0 && cps != 0)
                        suffix = ReadCStr(d, li + (int)cps, false);
                    target = basePath + suffix;
                    if (liSize == 0) return null;
                    pos = li + (int)liSize;
                }

                bool uni = (flags & 0x80) != 0;
                LnkInfo info = new LnkInfo();
                if ((flags & 0x04) != 0) ReadCounted(d, ref pos, uni);
                if ((flags & 0x08) != 0) ReadCounted(d, ref pos, uni);
                if ((flags & 0x10) != 0) info.WorkDir = ReadCounted(d, ref pos, uni);
                if ((flags & 0x20) != 0) info.Args = ReadCounted(d, ref pos, uni);
                if ((flags & 0x40) != 0) info.Icon = ReadCounted(d, ref pos, uni);
                info.Target = target;
                return info;
            }
            catch { return null; }
        }

        public static bool IsUnder(string path, string root)
        {
            string r = (root ?? "").TrimEnd('\\');
            if (r.Length == 0 || string.IsNullOrEmpty(path)) return false;
            if (string.Equals(path.TrimEnd('\\'), r, StringComparison.OrdinalIgnoreCase)) return true;
            return path.StartsWith(r + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private static string Shorten(string s)
        {
            return s.Length > 120 ? s.Substring(0, 120) : s;
        }

        public static bool LnkVerdict(string path, string root, FileAttributes attrs,
                                      out List<string> reasons)
        {
            reasons = new List<string>();
            LnkInfo info = ReadLnk(path);
            if (info != null)
            {
                string target = (info.Target ?? "").Trim();
                string leaf = target.Length > 0 ? Path.GetFileName(target).ToLowerInvariant() : "";
                if (leaf.Length > 0 && DangerExes.Contains(leaf))
                    reasons.Add("target=" + leaf);
                string args = info.Args ?? "";
                if (args.Length > 0 && ArgsRe.IsMatch(args))
                    reasons.Add("args=" + (args.Length > 120 ? args.Substring(0, 120) : args));

                // Lenh chay mot tep .exe/.bat/.vbs bang duong dan tuong doi tren chinh o.
                // Mau "Docusment" dung: /q /c "Docusment\1\1.exe 442 101".
                Match pm = Regex.Match(args,
                    @"(?i)([^\s""']+\.(exe|scr|com|pif|bat|cmd|vbs|vbe|js|jse|wsf|hta))");
                if (pm.Success)
                {
                    string payload = pm.Groups[1].Value.Trim('"');
                    // Chi coi la dang ngo khi duong dan tuong doi co ca thu muc
                    // ("Docusment\1\1.exe"). Tham so kieu "--processStart Discord.exe"
                    // la cach viet binh thuong cua shortcut hop le.
                    bool inFolder = payload.IndexOf('\\') >= 0 || payload.IndexOf('/') >= 0;
                    if (!Path.IsPathRooted(payload) && inFolder)
                        reasons.Add("args-relative-exe=" + Shorten(payload));
                    else if (IsUnder(payload, root))
                        reasons.Add("args-drive-exe=" + Shorten(payload));
                }

                if (Regex.IsMatch(leaf, @"^\d{1,3}\.(exe|scr|com|pif)$"))
                    reasons.Add("target-odd-name");

                // Shortcut dat ten giong nhan o dia: dau hieu kinh dien cua virus USB.
                try
                {
                    string limbName = Path.GetFileNameWithoutExtension(path);
                    string pathRoot = Path.GetPathRoot(root);
                    if (pathRoot != null && root.TrimEnd('\\').Length <= pathRoot.TrimEnd('\\').Length
                        && pathRoot.Length >= 2)
                    {
                        string label = new DriveInfo(pathRoot.Substring(0, 1)).VolumeLabel;
                        if (!string.IsNullOrEmpty(label)
                            && string.Equals(limbName, label, StringComparison.OrdinalIgnoreCase))
                            reasons.Add("named-after-drive");
                    }
                }
                catch { }

                if (target.Length > 0 && IsUnder(target, root))
                {
                    reasons.Add("target-on-drive");
                    try
                    {
                        string dir = Path.GetDirectoryName(target);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            if ((File.GetAttributes(dir) & FileAttributes.Hidden) != 0)
                                reasons.Add("target-in-hidden-folder");
                            else
                            {
                                Detect.FolderCheck fc = Detect.CheckFolder(dir, Path.GetFileName(dir));
                                if (fc != null && fc.Strong) reasons.Add("target-in-fake-folder");
                            }
                        }
                    }
                    catch { }
                }

                if (target.Length > 0 && IsUnder(target, root))
                {
                    string expanded = Environment.ExpandEnvironmentVariables(target);
                    if (!File.Exists(expanded) && !Directory.Exists(expanded))
                        reasons.Add("target-missing");
                }
            }
            else if ((attrs & (FileAttributes.Hidden | FileAttributes.System)) != 0)
            {
                reasons.Add("unreadable-hidden");
            }

            string baseName = Path.GetFileNameWithoutExtension(path);
            string sibling = Path.Combine(Path.GetDirectoryName(path), baseName);
            if (Directory.Exists(sibling))
            {
                try
                {
                    if ((File.GetAttributes(sibling) & FileAttributes.Hidden) != 0)
                        reasons.Add("shadows-hidden-folder");
                }
                catch { }
            }

            int score = 0;
            foreach (string r in reasons)
            {
                if (r.StartsWith("target=") || r.StartsWith("args=") || r == "shadows-hidden-folder"
                    || r.StartsWith("args-relative-exe=") || r.StartsWith("args-drive-exe=")
                    || r == "target-in-hidden-folder" || r == "target-in-fake-folder")
                    score += 3;
                else score += 2;
            }
            return score >= 3;
        }

        public static bool ScriptVerdict(string path, FileAttributes attrs, int depth,
                                         out List<string> reasons)
        {
            reasons = new List<string>();
            string low = Snippet(path).ToLowerInvariant();
            string ext = Path.GetExtension(path).ToLowerInvariant();
            bool hidden = (attrs & (FileAttributes.Hidden | FileAttributes.System)) != 0;
            List<string> hits = new List<string>();
            foreach (string m in ScriptMarkers)
                if (low.Contains(m)) hits.Add(m);

            if (hits.Count >= 3)
                reasons.Add("markers=" + string.Join(",", hits.ToArray()));
            else if (hits.Count >= 2 && (hidden || depth <= 1))
                reasons.Add("markers=" + string.Join(",", hits.ToArray()));
            if (ext == ".vbe" && low.Contains("#@~^"))
                reasons.Add("encoded-vbscript");
            return reasons.Count > 0;
        }

        public static string Snippet(string path)
        {
            try
            {
                byte[] buf = new byte[8192];
                int n;
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    n = fs.Read(buf, 0, buf.Length);
                if (n <= 0) return "";
                byte[] raw = new byte[n];
                Array.Copy(buf, raw, n);
                return Cp1252().GetString(raw) + "\n" + Encoding.Unicode.GetString(raw);
            }
            catch { return ""; }
        }

        public static bool AutorunVerdict(string path)
        {
            string text = Snippet(path);
            return AutorunRe.IsMatch(text) && AutorunPayloadRe.IsMatch(text);
        }

        public static CommandCheck CommandVerdict(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return null;
            CommandCheck c = new CommandCheck();
            string loc = ExeLocationReason(cmd);
            if (loc != null)
            {
                c.Reasons.Add(loc);
                c.High = true;
            }

            // Lenh boc tep chay khac trong tham so: cmd /c "Docusment\1\1.exe",
            // action cua task hoac gia tri Run tro vao AppData/Temp/USB.
            string primary = ExePathOf(cmd);
            List<string> candidates = new List<string>();
            List<string> quoted = new List<string>();
            foreach (Match m in Regex.Matches(cmd, "\"([^\"]+\\.(exe|scr|com|pif))\""))
            {
                quoted.Add(m.Groups[1].Value);
                candidates.Add(m.Groups[1].Value);
            }
            foreach (Match m in Regex.Matches(cmd, @"(?i)([^\s""']+\.(exe|scr|com|pif))"))
            {
                string part = m.Groups[1].Value;
                bool insideQuoted = false;
                foreach (string q in quoted)
                    if (q.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        insideQuoted = true;
                        break;
                    }
                if (!insideQuoted) candidates.Add(part);
            }
            foreach (string raw in candidates)
            {
                string arg = raw.Trim('"', '\'');
                if (arg.Length < 4) continue;
                // Bo qua chinh lenh va cac manh cua duong dan co dau cach
                // (vd "C:\Program Files\App\app.exe" bi cat thanh "Files\App\app.exe").
                if (primary.Length > 0
                    && (string.Equals(arg, primary, StringComparison.OrdinalIgnoreCase)
                        || primary.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;
                string reason = EmbeddedExeReason(arg);
                if (reason != null && !c.Reasons.Contains(reason))
                    c.Reasons.Add(reason);
                if (reason != null) c.High = true;
            }

            foreach (string[] p in HighPatterns)
                if (Regex.IsMatch(cmd, p[0], RegexOptions.IgnoreCase))
                {
                    c.Reasons.Add(p[1]);
                    c.High = true;
                }
            List<string> med = new List<string>();
            foreach (string[] p in MedPatterns)
                if (Regex.IsMatch(cmd, p[0], RegexOptions.IgnoreCase)) med.Add(p[1]);
            if (!c.High && med.Count < 2) return null;
            c.Reasons.AddRange(med);
            return c;
        }
    }
}
