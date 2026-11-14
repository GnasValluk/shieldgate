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
                if (target.Length > 0 && target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
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
                if (r.StartsWith("target=") || r.StartsWith("args=") || r == "shadows-hidden-folder")
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
