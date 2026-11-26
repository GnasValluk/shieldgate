// ShieldGate - phuc hoi du lieu bi virus shortcut giau vao thu muc khong ten.
// Dung duong dan "\\?\" va API Win32 truc tiep, vi thu muc ten rong / ky tu vo hinh /
// co dau cach cuoi ten se hong voi System.IO tren .NET 4.0.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ShieldGate
{
    internal sealed class DirEntry
    {
        public string Name;
        public bool IsDir;
        public uint Attr;
    }

    internal sealed class RestoreResult
    {
        public int Moved;        // so tep/thu muc da chuyen ra goc o
        public int Renamed;      // trong do bao nhieu muc bi doi ten vi trung ten
        public int Failed;       // so muc khong chuyen duoc
        public int DirsRemoved;  // thu muc khong ten da duoc xoa (da rong)
        public int DirsLeft;     // thu muc khong ten con lai (chua rong)
        public bool NoWrite;     // o chi doc / khong co quyen ghi: khong thu chuyen
        public List<string> Names = new List<string>();
    }

    internal static class WinPath
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct FindData
        {
            public uint Attr;
            public System.Runtime.InteropServices.ComTypes.FILETIME Created;
            public System.Runtime.InteropServices.ComTypes.FILETIME Accessed;
            public System.Runtime.InteropServices.ComTypes.FILETIME Written;
            public uint SizeHigh;
            public uint SizeLow;
            public uint Reserved0;
            public uint Reserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string Alt;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            public uint Low;
            public uint High;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AttributeData
        {
            public uint Attr;
            public FileTime Created;
            public FileTime Accessed;
            public FileTime Written;
            public uint SizeHigh;
            public uint SizeLow;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindFirstFileW(string name, out FindData data);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool FindNextFileW(IntPtr handle, out FindData data);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FindClose(IntPtr handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileExW(string from, string to, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool RemoveDirectoryW(string path);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFileAttributesW(string path);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetFileAttributesW(string path, uint attrs);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CopyFileW(string from, string to, bool failIfExists);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeleteFileW(string path);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateDirectoryW(string path, IntPtr security);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetFileAttributesExW(string name, int level, out AttributeData data);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFileW(string name, uint access, uint share,
                                                 IntPtr security, uint disposition,
                                                 uint flags, IntPtr template);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetFileSizeEx(IntPtr handle, out long size);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        private const uint GenericRead = 0x80000000;
        private const uint ShareAll = 0x00000001 | 0x00000002 | 0x00000004;
        private const uint OpenExisting = 3;

        private static readonly IntPtr InvalidHandle = new IntPtr(-1);
        private const uint InvalidAttrs = 0xFFFFFFFF;
        public const uint AttrDirectory = 0x10;
        public const uint AttrReparse = 0x400;

        public static string Ext(string path)
        {
            if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) return path;
            if (path.StartsWith(@"\\", StringComparison.Ordinal)) return @"\\?\UNC\" + path.Substring(2);
            try { if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path); }
            catch { }
            return @"\\?\" + path;
        }

        public static string Join(string dir, string name)
        {
            return dir.TrimEnd('\\') + "\\" + name;
        }

        // Thư mục cha, không dùng System.IO.Path vì nó từ chối đường dẫn > 260 ký tự.
        public static string Parent(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string p = path.TrimEnd('\\');
            int i = p.LastIndexOf('\\');
            return i > 4 ? p.Substring(0, i) : "";
        }

        public static List<DirEntry> List(string dirExt)
        {
            List<DirEntry> list = new List<DirEntry>();
            FindData d;
            IntPtr h = FindFirstFileW(Join(dirExt, "*"), out d);
            if (h == InvalidHandle) return list;
            try
            {
                do
                {
                    if (d.Name == "." || d.Name == "..") continue;
                    DirEntry e = new DirEntry();
                    e.Name = d.Name;
                    e.Attr = d.Attr;
                    e.IsDir = (d.Attr & AttrDirectory) != 0;
                    list.Add(e);
                } while (FindNextFileW(h, out d));
            }
            finally { FindClose(h); }
            return list;
        }

        public static bool Exists(string pathExt)
        {
            return GetFileAttributesW(pathExt) != InvalidAttrs;
        }

        public static bool IsDir(string pathExt)
        {
            uint a = GetFileAttributesW(pathExt);
            return a != InvalidAttrs && (a & AttrDirectory) != 0;
        }

        public static int LastError;

        public static bool Move(string fromExt, string toExt)
        {
            bool ok = MoveFileExW(fromExt, toExt, 0);
            LastError = ok ? 0 : Marshal.GetLastWin32Error();
            return ok;
        }

        // Tra ve 0 neu sao chep duoc, nguoc lai la ma loi Windows.
        public static int CopyFile(string fromExt, string toExt)
        {
            return CopyFileW(fromExt, toExt, true) ? 0 : Marshal.GetLastWin32Error();
        }

        public static bool DeleteFile(string pathExt)
        {
            SetNormal(pathExt);
            return DeleteFileW(pathExt);
        }

        public static bool CreateDir(string pathExt)
        {
            return CreateDirectoryW(pathExt, IntPtr.Zero);
        }

        // Tao ca cay thu muc. System.IO tu choi duong dan dai hon 260 ky tu,
        // ke ca khi da co tien to \\?\, nen phai tao tung cap bang Win32.
        public static bool CreateDirTree(string pathExt)
        {
            if (pathExt == null || pathExt.Length == 0) return false;
            if (Exists(pathExt)) return IsDir(pathExt);
            if (!pathExt.StartsWith(@"\\?\", StringComparison.Ordinal)) return false;
            for (int i = 4; i < pathExt.Length; i++)
            {
                if (pathExt[i] != '\\') continue;
                string part = pathExt.Substring(0, i);
                if (!Exists(part)) CreateDir(part);
            }
            if (!Exists(pathExt)) CreateDir(pathExt);
            return IsDir(pathExt);
        }

        // Mo tep chi de doc, ke ca duong dan dai hon 260 ky tu.
        public static FileStream OpenRead(string pathExt)
        {
            IntPtr h = CreateFileW(pathExt, GenericRead, ShareAll, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (h == InvalidHandle)
            {
                int code = Marshal.GetLastWin32Error();
                throw new IOException(ErrorText(code), code);
            }
            return new FileStream(new SafeFileHandle(h, true), FileAccess.Read);
        }

        // Kich thuoc tep; -1 neu khong doc duoc.
        public static long Size(string pathExt)
        {
            IntPtr h = CreateFileW(pathExt, GenericRead, ShareAll, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (h == InvalidHandle) return -1;
            try
            {
                long size;
                return GetFileSizeEx(h, out size) ? size : -1;
            }
            finally { CloseHandle(h); }
        }

        // Kich thuoc va thoi diem sua cuoi de lam khoa cache chu ky.
        // GetFileAttributesExW khong mo tep nen khong bi khoa chia se chan.
        public static bool Stat(string pathExt, out long size, out long writeTicks)
        {
            size = -1;
            writeTicks = 0;
            AttributeData d;
            if (!GetFileAttributesExW(pathExt, 0, out d)) return false;
            size = ((long)d.SizeHigh << 32) | d.SizeLow;
            writeTicks = ((long)d.Written.High << 32) | d.Written.Low;
            return true;
        }

        public static string ErrorText(int code)
        {
            try { return new Win32Exception(code).Message; }
            catch { return "Windows error " + code; }
        }

        public static bool RemoveDir(string pathExt)
        {
            return RemoveDirectoryW(pathExt);
        }

        public static void SetNormal(string pathExt)
        {
            uint a = GetFileAttributesW(pathExt);
            if (a == InvalidAttrs) return;
            uint n = a & ~(0x1u | 0x2u | 0x4u);   // readonly, hidden, system
            if (n != a) SetFileAttributesW(pathExt, n);
        }
    }

    internal static class UsbRestore
    {
        // ------------------------------------------------------------ nhan dien

        public static bool IsInvisible(char c)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c)) return true;
            switch (c)
            {
                case '\u00AD': case '\u115F': case '\u1160': case '\u180E':
                case '\u200B': case '\u200C': case '\u200D': case '\u200E': case '\u200F':
                case '\u2060': case '\u2800': case '\u3164': case '\uFEFF':
                    return true;
            }
            return false;
        }

        // Ten khong co ky tu nhin thay duoc, hoac ket thuc bang dau cach / dau cham.
        public static bool IsNameless(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            bool visible = false;
            for (int i = 0; i < name.Length; i++)
                if (!IsInvisible(name[i])) { visible = true; break; }
            if (!visible) return true;
            char last = name[name.Length - 1];
            return last == ' ' || last == '.';
        }

        public static string Codes(string name)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < name.Length && i < 8; i++)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append("U+" + ((int)name[i]).ToString("X4"));
            }
            return sb.ToString();
        }

        // Thu muc o goc o va thu muc con cap 1 (virus hay boc trong mot thu muc ten ngau nhien,
        // vd DATA_XXXXXXXX). Khoa la duong dan tuong doi so voi goc o.
        private static List<KeyValuePair<string, DirEntry>> Candidates(string root)
        {
            List<KeyValuePair<string, DirEntry>> list = new List<KeyValuePair<string, DirEntry>>();
            try
            {
                string rootExt = WinPath.Ext(root);
                foreach (DirEntry e in WinPath.List(rootExt))
                {
                    if (!e.IsDir || (e.Attr & WinPath.AttrReparse) != 0) continue;
                    if (Tree.SkipNames.Contains(e.Name)) continue;
                    list.Add(new KeyValuePair<string, DirEntry>(e.Name, e));
                    if (IsNameless(e.Name)) continue;
                    foreach (DirEntry c in WinPath.List(WinPath.Join(rootExt, e.Name)))
                        if (c.IsDir && (c.Attr & WinPath.AttrReparse) == 0)
                            list.Add(new KeyValuePair<string, DirEntry>(e.Name + "\\" + c.Name, c));
                }
            }
            catch { }
            return list;
        }

        // Tra ve duong dan tuong doi (vd "DATA_X\\<ten rong>") cua cac thu muc khong ten.
        public static List<string> FindNameless(string root)
        {
            List<string> found = new List<string>();
            foreach (KeyValuePair<string, DirEntry> kv in Candidates(root))
                if (IsNameless(kv.Value.Name)) found.Add(kv.Key);
            return found;
        }

        // Ten de hien thi: giu phan thu muc cha, ten rong thay bang ma ky tu.
        public static string Display(string rel)
        {
            int cut = rel.LastIndexOf('\\');
            if (cut < 0) return Codes(rel);
            return rel.Substring(0, cut + 1) + Codes(rel.Substring(cut + 1));
        }

        // Tep "payload" cua virus shortcut (1.exe, LMIGuardianDll.dll, adback.dat,
        // link.dat, .lnk gia, desktop.ini gia dang thu muc he thong). Tai lieu cua
        // nguoi dung bi virus doi ten Base64 KHONG nam trong danh sach nay.
        public static bool IsPayloadFile(string name)
        {
            string n = (name ?? "").Trim();
            if (n.Length == 0) return false;
            if (string.Equals(n, "desktop.ini", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(n, "autorun.inf", StringComparison.OrdinalIgnoreCase)) return true;
            return Rules.Current.IsPayloadExt(System.IO.Path.GetExtension(n));
        }

        // Virus đổi tên tài liệu thành Base64 của tên gốc (UTF-8). Chỉ nhận khi
        // giải mã ra một tên tài liệu hợp lệ: không có ký tự đường dẫn, không có
        // ký tự điều khiển, và đuôi nằm trong danh sách tài liệu (không phải payload).
        public static string DecodeName(string fileName)
        {
            string text = DecodeBase64Name(fileName, false);
            if (text != null) return text;
            // Virus có thể dùng Base64 URL-safe ('-' và '_' thay cho '+' và '/')
            // để tên tệp hợp lệ trên Windows; thử lại trước khi bỏ qua.
            return DecodeBase64Name(fileName, true);
        }

        private static string DecodeBase64Name(string fileName, bool urlSafe)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            string n = fileName.Trim();
            if (n.Length < 8 || n.Length > 400) return null;

            int pad = 0;
            bool sawPad = false;
            for (int i = 0; i < n.Length; i++)
            {
                char c = n[i];
                if (c == '=')
                {
                    sawPad = true;
                    if (++pad > 2) return null;
                    continue;
                }
                if (sawPad) return null;   // '=' chỉ được nằm ở cuối
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                       || (c >= '0' && c <= '9') || c == '+' || c == '/';
                if (urlSafe && (c == '-' || c == '_')) ok = true;
                if (!ok) return null;
            }
            if (n.Length % 4 == 1) return null;

            string b64 = n;
            if (urlSafe) b64 = b64.Replace('-', '+').Replace('_', '/');
            while (b64.Length % 4 != 0) b64 += "=";
            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(b64));
            }
            catch { return null; }

            if (text.Length < 3 || text.Length > 200) return null;
            if (text != text.Trim()) return null;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c < 32 || c == 127) return null;
                if ("<>:\"/\\|?*".IndexOf(c) >= 0) return null;
            }
            if (!Rules.Current.IsDocumentExt(System.IO.Path.GetExtension(text).ToLowerInvariant()))
                return null;
            return text;
        }

        // Thư mục người dùng chọn: quét chính nó. Gốc ổ: chỉ các thư mục giả
        // và không tên, tránh đổi tên tài liệu hợp lệ nằm rải rác trên ổ.
        private static List<string> Base64Dirs(string root)
        {
            string rootExt = WinPath.Ext(root);
            List<string> dirs = new List<string>();
            if (!UsbScan.IsDriveRoot(root)) dirs.Add(rootExt);
            else
            {
                foreach (string fk in FindFakeDirs(root)) dirs.Add(WinPath.Join(rootExt, fk));
                foreach (string nm in FindNameless(root)) dirs.Add(WinPath.Join(rootExt, nm));
            }
            return dirs;
        }

        // Xem trước cho pipe: đếm tên Base64 sẽ đổi, không ghi gì.
        public static int CountBase64(string root, Func<bool> cancelled)
        {
            int count = 0;
            foreach (string dir in Base64Dirs(root))
            {
                foreach (TreeItem t in Tree.Walk(dir))
                {
                    if (cancelled != null && cancelled()) return count;
                    if (t.IsDir) continue;
                    string name = System.IO.Path.GetFileName(t.Path);
                    if (IsPayloadFile(name)) continue;
                    if (DecodeName(name) == null) continue;
                    count++;
                }
            }
            return count;
        }

        // Đổi tên tài liệu Base64 trong thư mục giả/không tên về tên gốc.
        // Không ghi đè: tên đích đã có thì bỏ qua và đếm vào skipped.
        public static int RenameBase64(string root, Action<int, string> progress,
                                       Func<bool> cancelled, out int skipped)
        {
            skipped = 0;
            int renamed = 0;
            foreach (string dir in Base64Dirs(root))
            {
                int dirSkipped;
                renamed += RenameBase64In(dir, progress, cancelled, out dirSkipped);
                skipped += dirSkipped;
                if (cancelled != null && cancelled()) return renamed;
            }
            return renamed;
        }

        // Cùng quy tắc với RenameBase64 nhưng chỉ trong một thư mục đã xác định.
        // Dùng trước khi chuyển cả thư mục giả ra khỏi tên giả.
        public static int RenameBase64In(string dir, Action<int, string> progress,
                                         Func<bool> cancelled, out int skipped)
        {
            skipped = 0;
            int renamed = 0;
            foreach (TreeItem t in Tree.Walk(dir))
            {
                if (t.IsDir) continue;
                if (cancelled != null && cancelled()) return renamed;
                string name = System.IO.Path.GetFileName(t.Path);
                // Tệp đang mang đuôi chạy được thì không đổi tên: chỉ tài liệu.
                if (IsPayloadFile(name)) continue;
                string decoded = DecodeName(name);
                if (decoded == null) continue;
                string parent = WinPath.Parent(t.Path);
                if (parent.Length == 0) continue;
                string dest = WinPath.Join(parent, decoded);
                if (WinPath.Exists(dest)) { skipped++; continue; }
                if (WinPath.Move(t.Path, dest))
                {
                    renamed++;
                    Log.Write(S.Log.Base64Renamed(name, decoded), "INFO");
                    if (progress != null) progress(renamed, dest);
                }
                else skipped++;
            }
            return renamed;
        }

        // Thu muc khong ten chi chua du lieu nguoi dung (khong co tep chay, shortcut,
        // script) thi co the tu dong tra du lieu ra goc o ma khong so mat an toan.
        public static bool HasRiskyContent(string dir)
        {
            int seen = 0;
            try
            {
                foreach (TreeItem t in Tree.Walk(dir, null, 800, 5))
                {
                    if (t.IsDir) continue;
                    if (++seen > 800) return true;
                    if (IsPayloadFile(t.Name)) return true;
                }
            }
            catch { }
            return false;
        }

        // Thu muc gia ("Docsument", "Documents" ...) nam canh thu muc khong ten tren o da nhiem.
        // Ten go sai chinh ta (khong phai tu that) la dau hieu rieng; ten dung chi tinh khi
        // trong do co goi payload hoac desktop.ini gia dang thu muc he thong.
        public static List<string> FindFakeDirs(string root)
        {
            List<string> found = new List<string>();
            string rootExt = WinPath.Ext(root);
            foreach (KeyValuePair<string, DirEntry> kv in Candidates(root))
            {
                string name = kv.Value.Name;
                if (IsNameless(name) || !LooksLikeFolderName(name)) continue;
                string full = WinPath.Join(rootExt, kv.Key);
                Detect.FolderCheck check = Detect.CheckFolder(full, name);
                if (check != null && (check.Strong || check.Code == "lookalike-folder"
                                     || check.Code == "desktop-ini-clsid"))
                    found.Add(kv.Key);
            }
            return found;
        }

        // Tra ve true khi ten la ban go sai cua mot ten thu muc thong dung
        // (vd "Docusment" thay vi "Documents"), nhung khong phai chinh ten do.
        // Chi xet ten du dai va trung hai ky tu dau de tranh bao nham cac ten
        // that nhu "Proton" (gan voi "photos") hay "download".
        public static bool IsMisspelledFolder(string name)
        {
            string n = (name ?? "").Trim().ToLowerInvariant();
            if (n.Length < 8) return false;
            foreach (string w in FolderWords)
                if (n == w) return false;
            foreach (string w in FolderWords)
            {
                if (w.Length < 8) continue;
                if (n[0] != w[0] || n[1] != w[1]) continue;
                if (Distance(n, w) <= 2) return true;
            }
            return false;
        }

        // Xoa thu muc boc ngoai (phan cha cua duong dan tuong doi) neu da rong.
        public static void RemoveWrapper(string root, string rel)
        {
            int cut = rel.LastIndexOf('\\');
            if (cut <= 0) return;
            string wrap = WinPath.Join(WinPath.Ext(root), rel.Substring(0, cut));
            if (!WinPath.IsDir(wrap)) return;
            if (WinPath.List(wrap).Count > 0) return;
            WinPath.SetNormal(wrap);
            WinPath.RemoveDir(wrap);
        }

        // Chuyen ca cay thu muc gia (vd "Docusment") sang "Recovered\<ten>" o goc o.
        // Chi goi sau khi payload da duoc cach ly: neu con tep chay thi tu choi,
        // de khong mang ma doc sang noi khac. Khong ghi de: ten trung thi them (1).
        public static bool RecoverFakeFolder(string root, string rel, out int files,
                                             out int failed, out string dest, out string reason)
        {
            files = 0;
            failed = 0;
            dest = "";
            reason = "";
            if (!SafeRel(rel))
            {
                reason = S.Log.FakeFolderBadPath;
                return false;
            }

            string rootExt = WinPath.Ext(root);
            string dir = WinPath.Join(rootExt, rel);
            if (!WinPath.IsDir(dir))
            {
                reason = S.Log.FakeFolderGone;
                return false;
            }

            // Tuyệt đối không chuyển payload còn sót. Người gọi phải cách ly trước.
            // Kiểm tra trước khi đổi tên để lần từ chối không để lại thay đổi nào.
            foreach (TreeItem t in Tree.Walk(dir))
            {
                if (t.IsDir) continue;
                if (IsPayloadFile(t.Name))
                {
                    reason = S.Log.FakeFolderPayloadLeft(t.Name);
                    return false;
                }
            }

            // Đổi tên tài liệu Base64 trước khi chuyển, để người dùng nhận lại
            // tên gốc ngay trong thư mục Recovered.
            int skipped;
            RenameBase64In(dir, null, null, out skipped);

            foreach (TreeItem t in Tree.Walk(dir))
            {
                if (t.IsDir) continue;
                files++;
            }

            string rec = WinPath.Join(rootExt, "Recovered");
            if (!WinPath.IsDir(rec) && !WinPath.CreateDirTree(rec))
            {
                reason = S.Log.FakeFolderNoDest;
                failed = files;
                return false;
            }

            string name = System.IO.Path.GetFileName(rel);
            if (string.IsNullOrEmpty(name)) name = "fake-folder";
            // Hậu tố để lần quét sau không nhận lại thư mục đã phục hồi là thư mục
            // giả (tên gốc "Docusment" vẫn bị coi là gõ sai của "Documents").
            if (!name.EndsWith(" (recovered)", StringComparison.OrdinalIgnoreCase))
                name += " (recovered)";
            dest = WinPath.Join(rec, UniqueName(rec, name, true));

            WinPath.SetNormal(dir);
            if (!WinPath.Move(dir, dest))
            {
                int err = WinPath.LastError;
                WinPath.SetNormal(dir);
                if (!WinPath.Move(dir, dest))
                {
                    reason = WinPath.ErrorText(err);
                    failed = files;
                    dest = "";
                    return false;
                }
            }
            SetNormalTree(dest, 0);
            Log.Write(S.Log.FakeFolderRecovered(rel, dest, files), "INFO");
            return true;
        }

        private static bool SafeRel(string rel)
        {
            if (string.IsNullOrEmpty(rel) || Path.IsPathRooted(rel)) return false;
            if (rel.IndexOf(':') >= 0) return false;
            string[] parts = rel.Split('\\');
            foreach (string p in parts)
                if (p.Length == 0 || p == "." || p == "..") return false;
            return true;
        }

        // Tra ve thuoc tinh thong thuong cho ca cay vua chuyen, de tep khong
        // con an/he thong sau khi ra khoi thu muc gia.
        private static void SetNormalTree(string pathExt, int depth)
        {
            if (depth > 16) return;
            WinPath.SetNormal(pathExt);
            foreach (DirEntry e in WinPath.List(pathExt))
            {
                string p = WinPath.Join(pathExt, e.Name);
                WinPath.SetNormal(p);
                if (e.IsDir && (e.Attr & WinPath.AttrReparse) == 0)
                    SetNormalTree(p, depth + 1);
            }
        }

        private static readonly string[] FolderWords =
        {
            "documents", "document", "my documents", "folder", "new folder", "pictures",
            "photos", "images", "music", "videos", "downloads", "download", "files",
            "data", "backup", "tai lieu", "work"
        };

        // Tep .exe mang bieu tuong thu muc thuong dat ten giong thu muc that, hoac ten
        // thu muc go sai chinh ta (vd "Doscument", "Docsument").
        public static bool LooksLikeFolderName(string baseName)
        {
            string n = (baseName ?? "").Trim().ToLowerInvariant();
            if (n.Length < 4) return false;
            foreach (string w in FolderWords)
            {
                if (n == w) return true;
                if (w.Length >= 6 && n.Length >= 6 && Distance(n, w) <= 2) return true;
            }
            return false;
        }

        private static int Distance(string a, string b)
        {
            int[,] d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            return d[a.Length, b.Length];
        }

        // ------------------------------------------------------------- phuc hoi

        // Chuyen toan bo noi dung thu muc khong ten ra goc o roi xoa thu muc do.
        // Khong bao gio ghi de: trung ten thi gop thu muc, con tep thi them " (1)".
        // Chi xoa thu muc khong ten khi da rong.
        public static RestoreResult Restore(string root, IList<string> names, Func<bool> cancelled)
        {
            RestoreResult r = new RestoreResult();
            if (names != null && names.Count > 0 && !Native.CanWrite(root))
            {
                // O chi doc (USB co khoa ghi, dia CD, anh ISO): thu chuyen tung tep
                // chi lam day nhat ky bang loi Windows 5. Bao mot lan roi thoi.
                Log.Write(S.Log.DriveNotWritable(root), "WARN");
                r.NoWrite = true;
                r.Failed = names.Count;
                r.DirsLeft = names.Count;
                foreach (string name in names) r.Names.Add(name);
                return r;
            }
            string rootExt = WinPath.Ext(root);
            foreach (string name in names)
            {
                if (cancelled != null && cancelled()) break;
                string dir = WinPath.Join(rootExt, name);
                if (!WinPath.IsDir(dir)) continue;
                r.Names.Add(name);
                MoveChildren(dir, rootExt, r, 0);
                WinPath.SetNormal(dir);
                if (WinPath.RemoveDir(dir))
                {
                    r.DirsRemoved++;
                    RemoveWrapper(root, name);
                }
                else r.DirsLeft++;
            }
            if (r.Moved + r.DirsRemoved + r.Failed > 0)
                Log.Write(S.Log.Restored(r.Moved, r.DirsRemoved, root, r.Failed), r.Failed > 0 ? "WARN" : "INFO");
            return r;
        }

        // Chuyen mot muc. Tra ve 0 neu xong, nguoc lai la ma loi Windows.
        // Thu tu: doi cho -> bo thuoc tinh chi-doc/an/he thong roi doi cho lai ->
        // sao chep (tep) / tao thu muc va chuyen tung muc ben trong (thu muc).
        private static int MoveItem(string from, string to, DirEntry e, RestoreResult r, int depth)
        {
            if (WinPath.Move(from, to)) return 0;
            int err = WinPath.LastError;

            WinPath.SetNormal(from);
            if (WinPath.Move(from, to)) return 0;
            err = WinPath.LastError;

            if (!e.IsDir)
            {
                int ce = WinPath.CopyFile(from, to);
                if (ce != 0) return ce;
                if (WinPath.DeleteFile(from)) Log.Write(S.Log.RestoreCopied(e.Name, err), "WARN");
                else Log.Write(S.Log.RestoreOriginalLeft(e.Name, err), "WARN");
                return 0;
            }

            if ((e.Attr & WinPath.AttrReparse) == 0 && depth < 12 && WinPath.CreateDir(to))
            {
                MoveChildren(from, to, r, depth + 1);
                WinPath.SetNormal(from);
                WinPath.RemoveDir(from);
                return 0;
            }
            return err;
        }

        private static void MoveChildren(string srcExt, string dstExt, RestoreResult r, int depth)
        {
            if (depth > 12) return;
            foreach (DirEntry e in WinPath.List(srcExt))
            {
                string from = WinPath.Join(srcExt, e.Name);
                string to = WinPath.Join(dstExt, e.Name);

                if (WinPath.Exists(to))
                {
                    if (e.IsDir && (e.Attr & WinPath.AttrReparse) == 0 && WinPath.IsDir(to))
                    {
                        MoveChildren(from, to, r, depth + 1);
                        WinPath.SetNormal(from);
                        WinPath.RemoveDir(from);
                        continue;
                    }
                    to = WinPath.Join(dstExt, UniqueName(dstExt, e.Name, e.IsDir));
                    r.Renamed++;
                }

                int err = MoveItem(from, to, e, r, depth);
                if (err == 0) r.Moved++;
                else
                {
                    r.Failed++;
                    if (to != WinPath.Join(dstExt, e.Name)) r.Renamed--;
                    Log.Write(S.Log.RestoreMoveFailed(e.Name, err), "ERROR");
                }
            }
        }

        private static string UniqueName(string dirExt, string name, bool isDir)
        {
            if (!WinPath.Exists(WinPath.Join(dirExt, name))) return name;
            string stem = name, ext = "";
            int dot = name.LastIndexOf('.');
            if (!isDir && dot > 0)
            {
                stem = name.Substring(0, dot);
                ext = name.Substring(dot);
            }
            for (int i = 1; i < 1000; i++)
            {
                string cand = stem + " (" + i + ")" + ext;
                if (!WinPath.Exists(WinPath.Join(dirExt, cand))) return cand;
            }
            return stem + " (" + Guid.NewGuid().ToString("N").Substring(0, 6) + ")" + ext;
        }
    }
}
