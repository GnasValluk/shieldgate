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
        public static readonly HashSet<string> DangerExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "wscript.exe", "cscript.exe", "mshta.exe", "cmd.exe", "powershell.exe",
            "pwsh.exe", "rundll32.exe", "regsvr32.exe", "schtasks.exe", "bitsadmin.exe",
            "certutil.exe", "msiexec.exe", "wmic.exe"
        };

        public static readonly HashSet<string> ScriptExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".vbs", ".vbe", ".js", ".jse", ".wsf", ".hta" };

        private static readonly Regex ArgsRe = new Regex(
            @"(-e:vbscript|-e:jscript|//e:vbscript|//e:jscript|\.vbs\b|\.vbe\b|\.jse?\b|mshta|https?://|powershell.*(-enc|-windowstyle\s+hidden|-w\s+hidden)|\bcmd(\.exe)?\s*/c\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AutorunRe = new Regex(@"\[autorun\]", RegexOptions.IgnoreCase);
        private static readonly Regex AutorunPayloadRe = new Regex(
            @"(open|shellexecute|action)\s*=.*(\.exe|\.vbs|\.vbe|\.bat|\.cmd|\.com|\.pif|\.scr|\.lnk|wscript|cscript|mshta|cmd|powershell)",
            RegexOptions.IgnoreCase);

        private static readonly string[] ScriptMarkers =
        {
            "wscript.shell", "createobject", "scripting.filesystemobject",
            "shell.application", "attrib +", "-h -s", ".lnk", "wscript.exe",
            "vbscript", "on error resume next"
        };

        private static readonly string[][] HighPatterns =
        {
            new[] { @"\b(wscript|cscript|mshta)(\.exe)?\b", "wscript/mshta" },
            new[] { @"\.vbe?\b|\.jse?\b", "script-file" },
            new[] { @"powershell(\.exe)?.*(-enc|-encodedcommand)\b", "encoded-powershell" },
            new[] { @"(bitsadmin|certutil).*(-urlcache|http)", "download-lolbin" },
            new[] { @"regsvr32.*(/i|scrobj|http)", "regsvr32-remote" },
            new[] { @"attrib\s+[+\-][hsrc]", "attrib-hiding" }
        };

        private static readonly string[][] MedPatterns =
        {
            new[] { @"powershell(\.exe)?.*(-windowstyle\s+hidden|-w\s+hidden|bypass)", "hidden-powershell" },
            new[] { @"\\appdata\\local\\temp\\|\\windows\\temp\\", "temp-path" }
        };

        // ---------------------------------------------------- cau truc virus shortcut
        // Mau thu thap 2026: <nhan o>.lnk -> cmd /c "Docusment\1\1.exe ..."
        // Docusment la thu muc gia mao "Documents", ben trong la goi chuong trinh
        // (1.exe ky so cua Opera, opera_elf.dll, adback.dat, link.dat, icudtl.dat).
        // Cac ten tep duoi day chi co trong goi payload nay, khong phai tep he thong.
        public static readonly HashSet<string> PayloadNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "adback.dat", "link.dat", "opera_elf.dll"
        };

        // SHA256 cua mau da thu thap: van nhan ra khi virus doi ten tep.
        private static readonly Dictionary<string, string> PayloadHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "1668A64F5EFAC6B932FC725F33ECA219136EB49BE4115A731EFE877FFEBCD461", "1.exe" },
            { "065DCE8809D18B824844A820F1AD9F9663752540F36B53F6162546089E3624A4", "opera_elf.dll" },
            { "CF4D1B6917B4A26711E2A3C54D5466299C6D46DB446C215A70BD9389ECFF263E", "icudtl.dat" },
            { "B09FB3D4E84EC43C8E1EB39892F59B406E87AB5FA175713D75C045CD79D1CC67", "adback.dat" },
            { "E0DA42C12597D6D8620E7A4C477C4B94928F3CC34894FA0EFCCA2E3C02940931", "shortcut.lnk" }
        };

        private static readonly long[] PayloadSizes = { 943, 3936, 177152, 303718, 1040920 };

        // Tep thuoc goi payload: ten da biet, hoac dung kich thuoc + hash cua mau.
        public static bool IsPayloadFile(string path)
        {
            try
            {
                FileInfo fi = new FileInfo(path);
                if (fi.Length > 10 * 1024 * 1024) return false;
                if (PayloadNames.Contains(fi.Name)) return true;
                foreach (long size in PayloadSizes)
                    if (fi.Length == size)
                    {
                        string hash = Native.Sha256(path);
                        if (hash.Length > 0 && PayloadHashes.ContainsKey(hash)) return true;
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
        public static readonly HashSet<string> SystemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "svchost.exe", "csrss.exe", "lsass.exe", "services.exe", "smss.exe", "winlogon.exe",
            "wininit.exe", "explorer.exe", "spoolsv.exe", "taskhost.exe", "taskhostw.exe",
            "conhost.exe", "dwm.exe", "ctfmon.exe", "lsm.exe", "userinit.exe", "taskmgr.exe"
        };

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

        // Chi kiem tra tep co chu ky so hay khong (khong xac minh chuoi tin cay).
        public static bool IsSigned(string path)
        {
            try
            {
                System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(path);
                return true;
            }
            catch { return false; }
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

        private static bool IsUnder(string path, string root)
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
