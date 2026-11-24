// ShieldGate 2.0 - detector dong nhu plugin. Moi kieu kiem tra la mot lop rieng,
// tra Finding co diem; Scan.cs chi ghep ket qua, khong biet chi tiet tung luat.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace ShieldGate
{
    internal sealed class Finding
    {
        public string Code = "";
        public string Detail = "";
        public int Score;
        public bool Strong;
        public bool FlagOnly;
        public string Path = "";

        public Finding(string code, string detail, int score)
        {
            Code = code;
            Detail = detail ?? "";
            Score = score;
        }
    }

    internal sealed class ScanContext
    {
        public string Root = "";
        public RulesData Rules;
        public WalkStats Stats;
        // ADS chi co tren NTFS; tinh mot lan cho ca lan quet de detector bo qua
        // nhanh tren FAT32/exFAT thay vi goi FindFirstStreamW cho tung tep.
        public bool IsNtfs;
    }

    internal interface IDetector
    {
        string Name { get; }
        void Inspect(ScanContext context, TreeItem file, List<Finding> output);
    }

    internal interface IDirectoryDetector
    {
        string Name { get; }
        void Inspect(ScanContext context, string directory, List<Finding> output);
    }

    internal static class DetectorPipeline
    {
        private static readonly IDetector[] PerFile =
        {
            new DoubleExtensionDetector(),
            new RloNameDetector(),
            new RootScriptDetector(),
            new NtlmLeakDetector(),
            new DesktopIniDetector(),
            new AdsDetector(),
            new OfficeMacroDetector()
        };

        private static readonly IDirectoryDetector[] PerDirectory =
        {
            new SideloadDirectoryDetector()
        };

        public static List<Finding> InspectFile(ScanContext context, TreeItem file)
        {
            List<Finding> found = new List<Finding>();
            foreach (IDetector d in PerFile)
            {
                try { d.Inspect(context, file, found); }
                catch { }
            }
            return found;
        }

        public static List<Finding> InspectDirectory(ScanContext context, string directory)
        {
            List<Finding> found = new List<Finding>();
            foreach (IDirectoryDetector d in PerDirectory)
            {
                try { d.Inspect(context, directory, found); }
                catch { }
            }
            return found;
        }
    }

    internal static class Scoring
    {
        public static int Evaluate(IList<string> codes, RulesData rules)
        {
            int total = 0;
            if (codes == null) return 0;
            foreach (string c in codes) total += rules.Weight(c);
            if (total > 10) total = 10;
            return total;
        }
    }

    // ------------------------------------------------------- ten tep / duong dan

    // "report.pdf.exe": duoi thu hai la dinh dang tai lieu, duoi cuoi la tep chay.
    internal sealed class DoubleExtensionDetector : IDetector
    {
        public string Name { get { return "double-extension"; } }

        public void Inspect(ScanContext c, TreeItem f, List<Finding> output)
        {
            if (f.IsDir) return;
            string ext = Path.GetExtension(f.Name);
            if (!c.Rules.IsDangerousExt(ext)) return;
            string without = Path.GetFileNameWithoutExtension(f.Name);
            string decoy = Path.GetExtension(without);
            if (!c.Rules.IsDoubleExtDecoy(decoy)) return;
            Finding x = new Finding("double-extension", f.Name, c.Rules.Weight("double-extension"));
            x.Strong = true;
            output.Add(x);
        }
    }

    // Ky tu RLO U+202E dao nguoc ten tep: "innocuous.exe" hien ra thanh "exe.exe".
    internal sealed class RloNameDetector : IDetector
    {
        public string Name { get { return "rlo-name"; } }

        public void Inspect(ScanContext c, TreeItem f, List<Finding> output)
        {
            string name = f.Name;
            bool bad = false;
            foreach (char ch in name)
            {
                if (ch == '\u202E' || ch == '\u202D' || ch == '\u2066' ||
                    ch == '\u2067' || ch == '\u2068' || ch == '\u2069')
                {
                    bad = true;
                    break;
                }
            }
            if (!bad) return;
            Finding x = new Finding("rlo-name", f.Name, c.Rules.Weight("rlo-name"));
            x.Strong = true;
            output.Add(x);
        }
    }

    // Script nam ngay goc o: nguoi dung bam nham la chay, la vi tri virus hay dung.
    internal sealed class RootScriptDetector : IDetector
    {
        public string Name { get { return "root-script"; } }

        public void Inspect(ScanContext c, TreeItem f, List<Finding> output)
        {
            if (f.IsDir || f.Depth != 0) return;
            string ext = Path.GetExtension(f.Name);
            if (!c.Rules.IsRootScriptExt(ext)) return;
            bool hidden = (f.Attrs & (FileAttributes.Hidden | FileAttributes.System)) != 0;
            string code = hidden ? "hidden-root-script" : "root-script";
            Finding x = new Finding(code, f.Name, c.Rules.Weight(code));
            x.Strong = hidden && x.Score >= c.Rules.ScoreThreshold;
            output.Add(x);
        }
    }

    // .url / .scf tro icon ve \\may\chia-se: Windows gui NTLM hash toi may do.
    internal sealed class NtlmLeakDetector : IDetector
    {
        private static readonly Regex UncRe = new Regex(
            @"(?im)^\s*(IconFile|URL|WorkingDirectory|IconIndex)\s*=.*\\\\[^\\\s]",
            RegexOptions.Compiled);

        public string Name { get { return "ntlm-leak"; } }

        public void Inspect(ScanContext c, TreeItem f, List<Finding> output)
        {
            if (f.IsDir) return;
            string ext = Path.GetExtension(f.Name);
            if (!c.Rules.IsNtlmExt(ext)) return;
            // .lnk da co bo doc rieng trong Detect.cs; tranh bao hai lan.
            if (string.Equals(ext, ".lnk", StringComparison.OrdinalIgnoreCase)) return;
            string text = Detect.Snippet(f.Path);
            if (text.Length == 0 || !UncRe.IsMatch(text)) return;
            Finding x = new Finding("ntlm-leak", f.Name, c.Rules.Weight("ntlm-leak"));
            x.Strong = true;
            output.Add(x);
        }
    }

    // desktop.ini o goc o co CLSID: thu muc gay anh huong toan o, thuong de gia dang.
    internal sealed class DesktopIniDetector : IDetector
    {
        public string Name { get { return "root-desktop-ini"; } }

        public void Inspect(ScanContext c, TreeItem f, List<Finding> output)
        {
            if (f.IsDir || f.Depth != 0) return;
            if (!string.Equals(f.Name, "desktop.ini", StringComparison.OrdinalIgnoreCase)) return;
            string text = Detect.Snippet(f.Path);
            if (text.IndexOf("CLSID", StringComparison.OrdinalIgnoreCase) < 0) return;
            Finding x = new Finding("root-desktop-ini", f.Name, c.Rules.Weight("root-desktop-ini"));
            output.Add(x);
        }
    }

    // Alternate data stream: mau an trong file "sach". Zone.Identifier la cua Windows
    // nen duoc bo qua; stream khac bi an/hidden thi dang ngo hon.
    internal sealed class AdsDetector : IDetector
    {
        public string Name { get { return "ads"; } }

        public void Inspect(ScanContext c, TreeItem f, List<Finding> output)
        {
            if (!c.IsNtfs) return;
            if (f.IsDir || f.Depth > 3) return;
            if (f.Size > 64L * 1024 * 1024) return;
            List<string> streams = Streams.List(f.Path);
            if (streams.Count == 0) return;
            bool hidden = (f.Attrs & (FileAttributes.Hidden | FileAttributes.System)) != 0;
            int seen = 0;
            foreach (string raw in streams)
            {
                string name = Streams.Name(raw);
                if (name.Length == 0) continue;
                if (c.Rules.IsAdsSkipped(name)) continue;
                bool payloadName = Regex.IsMatch(name,
                    @"(?i)\.(exe|dll|scr|ps1|vbs|js|bat|cmd|hta|com|pif)$");
                string code = (hidden || payloadName) ? "hidden-ads" : "alternate-data-stream";
                Finding x = new Finding(code, f.Name + " :" + name,
                                        c.Rules.Weight(code));
                output.Add(x);
                if (++seen >= 3) break;
            }
        }
    }

    // Office co macro: chi gan co, khong bao gio tu cach ly. File .docm/.xlsm/...
    // la macro-enabled theo thiet ke; OOXML thuong chua vbaProject.bin la bat thuong.
    internal sealed class OfficeMacroDetector : IDetector
    {
        public string Name { get { return "office-macro"; } }

        public void Inspect(ScanContext c, TreeItem f, List<Finding> output)
        {
            if (f.IsDir) return;
            string ext = Path.GetExtension(f.Name);
            bool macroExt = c.Rules.IsMacroExt(ext);
            bool ooxml = string.Equals(ext, ".docx", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(ext, ".pptx", StringComparison.OrdinalIgnoreCase);
            if (!macroExt && !ooxml) return;
            if (ooxml && !HasVbaProject(f.Path)) return;

            string code = macroExt ? "office-macro" : "office-macro";
            Finding x = new Finding(code, f.Name,
                                    macroExt ? c.Rules.Weight("office-macro") : 3);
            x.FlagOnly = true;
            output.Add(x);
        }

        // Doc 1 MB cuoi tep: central directory cua ZIP nam o cuoi nen ten
        // "vbaProject.bin" o day. Doc duoi re hon doc 8 MB dau va khong con
        // bo qua tep lon hon 32 MB.
        private static bool HasVbaProject(string path)
        {
            try
            {
                using (FileStream fs = WinPath.OpenRead(WinPath.Ext(path)))
                {
                    if (fs.Length <= 0) return false;
                    int take = (int)Math.Min(fs.Length, 1024L * 1024);
                    fs.Seek(-take, SeekOrigin.End);
                    byte[] buf = new byte[take];
                    int n = fs.Read(buf, 0, take);
                    if (n <= 0) return false;
                    string text = Encoding.ASCII.GetString(buf, 0, n);
                    return text.IndexOf("vbaProject.bin", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch { return false; }
        }
    }

    // DLL bi sideload: ten DLL hay bi gia mao nam canh mot EXE co chu ky.
    // Chi gan co (FlagOnly) vi nhieu ung dung portable co DLL khong ky hop le.
    internal sealed class SideloadDirectoryDetector : IDirectoryDetector
    {
        public string Name { get { return "sideload-dll"; } }

        public void Inspect(ScanContext c, string directory, List<Finding> output)
        {
            string[] files;
            try { files = Directory.GetFiles(directory); }
            catch { return; }
            if (files.Length == 0 || files.Length > 200) return;

            bool hasSignedExe = false;
            List<string> candidates = new List<string>();
            foreach (string path in files)
            {
                string name = Path.GetFileName(path);
                string ext = Path.GetExtension(name);
                if (string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (!hasSignedExe && Detect.IsSigned(path)) hasSignedExe = true;
                }
                else if (string.Equals(ext, ".dll", StringComparison.OrdinalIgnoreCase)
                         && c.Rules.IsSideloadDll(name))
                    candidates.Add(path);
            }
            if (!hasSignedExe || candidates.Count == 0) return;

            // Chi can mot DLL ung vien moi thu muc, tranh lam ngap danh sach.
            foreach (string dll in candidates)
            {
                if (Detect.IsSigned(dll)) continue;
                Finding x = new Finding("sideload-dll", Path.GetFileName(dll),
                                        c.Rules.Weight("sideload-dll"));
                x.Path = dll;
                x.FlagOnly = true;
                output.Add(x);
                break;
            }
        }
    }

    // ---------------------------------------------------------- ADS Win32

    internal static class Streams
    {
        private const int InvalidHandle = -1;
        private const int FindStreamInfoStandard = 0;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Win32FindStreamData
        {
            public long StreamSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)]
            public string StreamName;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindFirstStreamW(string fileName, int infoLevel,
                                                       IntPtr findStreamData, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool FindNextStreamW(IntPtr findStreamHandle, IntPtr findStreamData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FindClose(IntPtr handle);

        public static List<string> List(string path)
        {
            List<string> names = new List<string>();
            IntPtr buffer = IntPtr.Zero;
            IntPtr handle = IntPtr.Zero;
            try
            {
                buffer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Win32FindStreamData)));
                handle = FindFirstStreamW(WinPath.Ext(path), FindStreamInfoStandard, buffer, 0);
                if (handle == new IntPtr(InvalidHandle)) return names;
                for (int i = 0; i < 16; i++)
                {
                    Win32FindStreamData data = (Win32FindStreamData)Marshal.PtrToStructure(
                        buffer, typeof(Win32FindStreamData));
                    if (!string.IsNullOrEmpty(data.StreamName)) names.Add(data.StreamName);
                    if (!FindNextStreamW(handle, buffer)) break;
                }
            }
            catch { }
            finally
            {
                if (handle != IntPtr.Zero && handle != new IntPtr(InvalidHandle))
                {
                    try { FindClose(handle); } catch { }
                }
                if (buffer != IntPtr.Zero) { try { Marshal.FreeHGlobal(buffer); } catch { } }
            }
            return names;
        }

        // ":ten:$DATA" -> "ten"; ":{$DATA}" (stream mac dinh) -> "".
        public static string Name(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.Length < 2 || raw[0] != ':') return "";
            int end = raw.LastIndexOf(':');
            if (end <= 1) return "";
            string name = raw.Substring(1, end - 1);
            if (name.Length == 0) return "";
            return name;
        }
    }
}
