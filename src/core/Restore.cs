// ShieldGate - phuc hoi du lieu bi virus shortcut giau vao thu muc khong ten.
// Dung duong dan "\\?\" va API Win32 truc tiep, vi thu muc ten rong / ky tu vo hinh /
// co dau cach cuoi ten se hong voi System.IO tren .NET 4.0.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

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
                    switch (System.IO.Path.GetExtension(t.Name).ToLowerInvariant())
                    {
                        case ".exe": case ".scr": case ".com": case ".pif": case ".lnk":
                        case ".vbs": case ".vbe": case ".js": case ".jse": case ".wsf":
                        case ".hta": case ".bat": case ".cmd": case ".dll":
                            return true;
                    }
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
