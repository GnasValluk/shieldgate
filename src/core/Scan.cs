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
    internal enum UsbKind { Shortcut, Script, Autorun, RootExe, HiddenFolder, FakeFolder, Other }

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
        // Điểm rủi ro 0..10, tính từ trọng số trong rules.json.
        public int Score;
        // Chỉ gắn cờ (macro Office, DLL nghi vấn): không tự cách ly, không chọn sẵn.
        public bool FlagOnly;

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
        public int Other;
        // O chi doc (USB co khoa ghi, dia CD, anh ISO): chi xem duoc, khong sua duoc.
        public bool ReadOnly;
        // Gốc quét là ổ (G:\) hay thư mục người dùng chọn. Chế độ thư mục bỏ qua
        // các thao tác cấp ổ như tiêm autorun.inf.
        public bool IsDriveRoot;
        // Số tệp dữ liệu người dùng được giữ lại trong thư mục giả (không cách ly).
        public int KeptFiles;
        // Số tệp có tên là Base64 của một tên tài liệu: có thể khôi phục tên gốc.
        public int Base64Names;
        // Số thư mục giả còn lại trên ổ (kể cả thư mục chỉ còn tài liệu).
        public int FakeFolders;
    }

    // Kết quả xử lý thư mục giả: cách ly payload còn sót, đổi tên Base64,
    // chuyển cả cây sang Recovered và xoá tên thư mục giả.
    internal sealed class FakeRecoverResult
    {
        public int Folders;
        public int FilesMoved;
        public int Quarantined;
        public int Failed;
        public List<string> Destinations = new List<string>();
    }

    // Phạm vi gỡ thuộc tính ẩn. Chế độ tự động chỉ chạm mục mang dấu hiệu virus
    // (Hidden + System); nút "Hiện tệp" do người dùng bấm thì gỡ cả mục họ tự ẩn.
    internal enum UnhideScope
    {
        VirusOnly,
        AllHidden
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
                    "SELECT ProcessId, Name, CommandLine, ExecutablePath FROM Win32_Process " +
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

        // Gốc ổ (G: hoặc G:\) khác với một thư mục người dùng chọn trên ổ đó.
        public static bool IsDriveRoot(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string full;
            try { full = System.IO.Path.GetFullPath(path); }
            catch { return false; }
            full = full.TrimEnd('\\');
            return full.Length == 2 && full[1] == ':';
        }

        private static bool UnderAny(string rel, List<string> dirs)
        {
            foreach (string d in dirs)
            {
                if (rel.Length > d.Length
                    && rel.StartsWith(d, StringComparison.OrdinalIgnoreCase)
                    && rel[d.Length] == '\\')
                    return true;
            }
            return false;
        }

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
            result.ReadOnly = Native.IsReadOnlyVolume(root);
            result.IsDriveRoot = IsDriveRoot(root);
            int seen = 0;
            WalkStats stats = new WalkStats();
            RulesData rules = Rules.Current;
            ScanContext context = new ScanContext();
            context.Root = root;
            context.Rules = rules;
            context.Stats = stats;
            context.IsNtfs = Native.IsNtfsVolume(root);
            List<string> nameless = UsbRestore.FindNameless(root);
            List<string> fakeDirs = UsbRestore.FindFakeDirs(root);
            List<TreeItem> rootExes = new List<TreeItem>();
            List<string> dirs = new List<string>();

            foreach (TreeItem f in Tree.Walk(root, stats, rules.MaxFiles, rules.MaxSeconds, maxDepth))
            {
                if (cancelled != null && cancelled())
                {
                    result.Cancelled = true;
                    break;
                }
                if (f.IsDir)
                {
                    if (f.Depth <= 2 && dirs.Count < 400) dirs.Add(f.Path);
                    continue;
                }

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

                // Thư mục giả: đếm tài liệu người dùng còn lại và tên Base64 có
                // thể khôi phục, để UI báo rõ thay vì chỉ ghi log.
                if (fakeDirs.Count > 0 && UnderAny(f.Rel, fakeDirs))
                {
                    if (!UsbRestore.IsPayloadFile(f.Name)) result.KeptFiles++;
                    if (!UsbRestore.IsPayloadFile(f.Name)
                        && UsbRestore.DecodeName(f.Name) != null) result.Base64Names++;
                }

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
                    List<string> why;
                    if (ShieldGate.Detect.AutorunVerdict(f.Path, out why))
                    {
                        item = MakeItem(f, UsbKind.Autorun);
                        item.Codes.AddRange(why);
                        // Chi co payload that su moi du de bao ve nen tu cach ly;
                        // cau truc shellexecute/USEAUTOPLAY chi cong diem.
                        item.High = why.Contains("autorun-payload");
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
                    if (rules.IsRootDecoyExe(f.Name))
                    {
                        // Ten chung chung o goc o (usb.exe, update.exe): mot dau
                        // hieu, chua du de tu cach ly.
                        item = MakeItem(f, UsbKind.RootExe);
                        item.Codes.Add("root-decoy-exe");
                    }
                    else
                    {
                        result.Warnings.Add(f.Name);
                        rootExes.Add(f);
                    }
                }

                // Detector 2.0 chỉ chạy khi các luật 1.0 chưa kết luận, để không
                // thay đổi hành vi cũ và không nhân đôi cảnh báo.
                if (item == null && !f.IsDir)
                {
                    List<Finding> found = DetectorPipeline.InspectFile(context, f);
                    if (found.Count > 0)
                    {
                        item = MakeItem(f, UsbKind.Other);
                        int sum = 0;
                        bool strong = false;
                        bool flagOnly = true;
                        foreach (Finding fd in found)
                        {
                            string code = fd.Code;
                            if (!string.IsNullOrEmpty(fd.Detail)) code += "=" + fd.Detail;
                            if (!item.Codes.Contains(code)) item.Codes.Add(code);
                            sum += fd.Score;
                            if (fd.Strong) strong = true;
                            if (!fd.FlagOnly) flagOnly = false;
                        }
                        if (sum > 10) sum = 10;
                        item.Score = sum;
                        item.FlagOnly = flagOnly;
                        // Chỉ dấu hiệu mạnh mới đủ để bảo vệ nền tự cách ly.
                        item.High = strong && !flagOnly;
                    }
                }

                if (item != null)
                {
                    if (item.Score == 0) item.Score = Scoring.Evaluate(item.Codes, rules);
                    // AGENTS điều 7: dấu hiệu mạnh nhưng chưa đủ điểm thì chỉ cảnh báo,
                    // không cho bảo vệ nền tự cách ly. Ngưỡng lấy từ rules.json.
                    if (item.High && item.Score < rules.ScoreThreshold) item.High = false;
                    try { item.Size = new FileInfo(item.Path).Length; }
                    catch { }
                    result.Items.Add(item);
                    if (item.Kind == UsbKind.Shortcut) result.Shortcuts++;
                    else if (item.Kind == UsbKind.Script) result.Scripts++;
                    else if (item.Kind == UsbKind.Autorun) result.Autoruns++;
                    else if (item.Kind == UsbKind.Other) result.Other++;
                    else result.RootExes++;
                }
            }

            // Detector theo thư mục: DLL sideloading cần nhìn cả thư mục.
            if (!stats.Truncated && dirs.Count > 0)
            {
                HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (UsbItem it in result.Items) seenPaths.Add(it.Path);
                int inspected = 0;
                foreach (string dir in dirs)
                {
                    if (cancelled != null && cancelled()) { result.Cancelled = true; break; }
                    if (++inspected > 300) break;
                    List<Finding> found = DetectorPipeline.InspectDirectory(context, dir);
                    foreach (Finding fd in found)
                    {
                        string path = fd.Path;
                        if (string.IsNullOrEmpty(path)) continue;
                        if (seenPaths.Contains(path)) continue;
                        UsbItem di = new UsbItem();
                        di.Path = path;
                        di.Rel = path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                            ? path.Substring(root.Length).TrimStart('\\') : Path.GetFileName(path);
                        di.Kind = UsbKind.Other;
                        di.Codes.Add(fd.Code + (string.IsNullOrEmpty(fd.Detail) ? "" : "=" + fd.Detail));
                        di.Score = fd.Score;
                        di.FlagOnly = fd.FlagOnly;
                        // Thư mục giả có payload (thùng rác giả) đủ mạnh để bảo vệ
                        // nền xử lý: ApplyLocked chỉ cách ly payload bên trong.
                        di.High = fd.Strong && !fd.FlagOnly && fd.Score >= rules.ScoreThreshold;
                        try { di.Size = new FileInfo(path).Length; } catch { }
                        result.Items.Add(di);
                        result.Other++;
                        seenPaths.Add(path);
                    }
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
            foreach (string fk in fakeDirs)
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
                result.FakeFolders++;
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
                if (r.StartsWith("shadows-hidden-document=", StringComparison.OrdinalIgnoreCase)) return true;
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
            batch = "";
            stopped = 0;
            if (selected == null || selected.Count == 0) return 0;
            // GUI va bao ve nen co the cung cach ly mot thu muc gia: mot ben di
            // chuyen tep, ben kia bao loi "khong tim thay" hang tram lan. Chi mot
            // tien trinh duoc sua o tai mot thoi diem; ben kia cho roi lam tiep.
            if (!CleanLock.Enter(60000))
            {
                Log.Write(S.Log.CleanBusy(scan.Root), "WARN");
                return 0;
            }
            // Tam mo khoa ghi sau khi da giu CleanLock: chi mot tien trinh sua o
            // tai mot thoi diem, nen khong ai bat lai khoa giua luc cach ly.
            bool paused = Policy.BeginCleanPause();
            try { return ApplyLocked(scan, selected, out failed, out batch, out stopped, progress); }
            finally
            {
                if (paused) Policy.EndCleanPause();
                CleanLock.Exit();
            }
        }

        private static int ApplyLocked(UsbScanResult scan, IList<UsbItem> selected,
                                       out int failed, out string batch, out int stopped,
                                       Action<int> progress)
        {
            failed = 0;
            batch = "";
            stopped = 0;
            // O chi doc: dung truoc khi thu xoa / di chuyen, de khong ghi hang loat
            // loi Windows 5 vao nhat ky va khong tao thu muc cach ly rong.
            if (!Native.CanWrite(scan.Root))
            {
                Log.Write(S.Log.DriveNotWritable(scan.Root), "WARN");
                failed = selected.Count;
                return 0;
            }
            stopped = StopProcesses(scan.Root);
            batch = Quarantine.NewBatch("usb_" + (scan.Drive.Length > 0 ? scan.Drive.Substring(0, 1) : "x"), "");
            int done = 0;
            int kept = 0;
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
                // Detector theo thu muc (thung rac gia...) tra ve duong dan thu muc:
                // cach ly payload ben trong, giu tai lieu nguoi dung tai cho.
                if (it.Kind == UsbKind.Other && WinPath.IsDir(WinPath.Ext(it.Path)))
                {
                    int keptHere = 0;
                    done += QuarantineTree(it.Path, scan.Root, batch, ref failed, ref keptHere);
                    kept += keptHere;
                    if (progress != null) progress(i + 1);
                    continue;
                }
                // Macro/DLL chỉ gắn cờ: người dùng phải tự quyết định, không cách ly.
                if (it.FlagOnly)
                {
                    Log.Write(S.Log.FlagOnlyKept(it.Path), "INFO");
                    if (progress != null) progress(i + 1);
                    continue;
                }
                // Danh sach co the da cu: bao ve nen hoac nguoi dung vua xu ly tep nay
                // truoc khi bam nut. Bo qua, khong ghi loi "Could not find file".
                if (!WinPath.Exists(WinPath.Ext(it.Path)))
                {
                    if (progress != null) progress(i + 1);
                    continue;
                }
                string err;
                if (Quarantine.Add(it.Path, scan.Root, batch, KindName(it.Kind), out err))
                    done++;
                else if (err.Length > 0)
                {
                    failed++;
                    Log.Write(S.Log.QuarantineFailed(it.Path, err), "ERROR");
                }
                if (progress != null) progress(i + 1);
            }

            // Thu muc gia: chi cach ly payload (1.exe, dll, dat, shortcut...).
            // Tai lieu nguoi dung bi virus doi ten Base64 duoc giu lai tai cho,
            // khi do thu muc khong xoa duoc va van con nguyen.
            foreach (string fk in fakes)
            {
                int keptHere = 0;
                done += QuarantineTree(System.IO.Path.Combine(scan.Root, fk), scan.Root, batch,
                                       ref failed, ref keptHere);
                kept += keptHere;
                if (keptHere == 0) UsbRestore.RemoveWrapper(scan.Root, fk);
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
            if (kept > 0) Log.Write(S.Log.FakeFolderKept(kept, scan.Root), "INFO");

            // Không để lại vỏ batch rỗng: lượt quét sau chỉ còn tài liệu giữ lại
            // vẫn tạo batch, và mỗi vòng nền lại thêm một thư mục trống.
            if (batch.Length > 0 && !HasQuarantinedFile(batch))
            {
                Quarantine.RemoveBatch(batch);
                batch = "";
            }
            return done;
        }

        private static bool HasQuarantinedFile(string batch)
        {
            try
            {
                string files = System.IO.Path.Combine(batch, "files");
                return Directory.Exists(files)
                    && Directory.GetFiles(files, "*.sgq", SearchOption.AllDirectories).Length > 0;
            }
            catch { return true; }   // không đọc được: giữ lại, không xoá mù.
        }

        // Tra ve so tep payload da cach ly. kept = so tep du lieu nguoi dung giu lai.
        public static int QuarantineTree(string dir, string root, string batch,
                                         ref int failed, ref int kept)
        {
            int n = 0;
            kept = 0;
            if (!CleanLock.Enter(60000))
            {
                Log.Write(S.Log.CleanBusy(root), "WARN");
                return 0;
            }
            try
            {
                List<string> dirs = new List<string>();
                foreach (TreeItem t in Tree.Walk(dir))
                {
                    if (t.IsDir) { dirs.Add(t.Path); continue; }
                    // Tai lieu / anh / tep cua nguoi dung: khong dong vao.
                    if (!UsbRestore.IsPayloadFile(t.Name)) { kept++; continue; }
                    string err;
                    if (Quarantine.Add(t.Path, root, batch, "fake-folder", out err)) n++;
                    else if (err.Length > 0)
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
            finally { CleanLock.Exit(); }
        }

        // Xử lý thư mục giả còn tài liệu: cách ly payload còn sót, đổi tên Base64,
        // rồi chuyển cả cây sang Recovered\<tên> và xoá tên giả. Chỉ chạy khi người
        // dùng bấm; bảo vệ nền vẫn chỉ cách ly payload và giữ tài liệu tại chỗ.
        public static FakeRecoverResult RecoverFakeFolders(UsbScanResult scan,
                                                           IList<UsbItem> items,
                                                           Action<int, string> progress)
        {
            FakeRecoverResult r = new FakeRecoverResult();
            if (scan == null || items == null || items.Count == 0) return r;
            if (!Native.CanWrite(scan.Root))
            {
                Log.Write(S.Log.DriveNotWritable(scan.Root), "WARN");
                r.Failed = items.Count;
                return r;
            }
            if (!CleanLock.Enter(60000))
            {
                Log.Write(S.Log.CleanBusy(scan.Root), "WARN");
                return r;
            }
            bool paused = Policy.BeginCleanPause();
            try
            {
                // Dừng tiến trình đang chạy từ ổ để không bị khoá tệp khi chuyển.
                StopProcesses(scan.Root);
                string batch = Quarantine.NewBatch(
                    "usb_" + (scan.Drive.Length > 0 ? scan.Drive.Substring(0, 1) : "x"), "");
                int seen = 0;
                foreach (UsbItem it in items)
                {
                    if (it == null || it.Kind != UsbKind.FakeFolder) continue;
                    seen++;
                    string dir = System.IO.Path.Combine(scan.Root, it.FolderName);
                    int failedHere = 0, kept = 0;
                    int q = QuarantineTree(dir, scan.Root, batch, ref failedHere, ref kept);
                    r.Quarantined += q;
                    if (failedHere > 0)
                    {
                        r.Failed++;
                        Log.Write(S.Log.FakeFolderQuarantineFailed(it.FolderName), "WARN");
                        if (progress != null) progress(seen, dir);
                        continue;
                    }
                    // Không còn tài liệu: QuarantineTree đã xoá thư mục rỗng.
                    if (kept == 0)
                    {
                        if (progress != null) progress(seen, dir);
                        continue;
                    }
                    int files, moveFailed;
                    string dest, reason;
                    if (UsbRestore.RecoverFakeFolder(scan.Root, it.FolderName,
                            out files, out moveFailed, out dest, out reason))
                    {
                        r.Folders++;
                        r.FilesMoved += files;
                        if (dest.Length > 0) r.Destinations.Add(dest);
                        // Thư mục bọc ngoài (vd DATA_XXXX) chỉ bị xoá khi đã rỗng.
                        UsbRestore.RemoveWrapper(scan.Root, it.FolderName);
                    }
                    else
                    {
                        r.Failed++;
                        Log.Write(S.Log.FakeFolderRecoverFailed(it.FolderName, reason), "WARN");
                    }
                    if (progress != null) progress(seen, dir);
                }
                if (batch.Length > 0 && !HasQuarantinedFile(batch))
                {
                    Quarantine.RemoveBatch(batch);
                    batch = "";
                }
                if (seen > 0)
                    Log.Write(S.Log.FakeFolderRecoverSummary(r.Folders, r.FilesMoved,
                        r.Quarantined, r.Failed, scan.Root), r.Failed > 0 ? "WARN" : "INFO");
                return r;
            }
            finally
            {
                if (paused) Policy.EndCleanPause();
                CleanLock.Exit();
            }
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
                case UsbKind.Other: return "detector";
                default: return "exe-an";
            }
        }

        // Giữ nguyên thuộc tính hệ thống: desktop.ini thật, autorun.inf (được xử lý
        // riêng), thư mục hệ thống của ổ. Đổi thuộc tính các mục này dễ làm hỏng
        // hiển thị thư mục hoặc làm Explorer hiện lại thứ người dùng cố tình ẩn.
        private static bool KeepSystemAttributes(string name)
        {
            string n = name ?? "";
            if (n.Length == 0) return true;
            if (n[0] == '$') return true;
            if (string.Equals(n, "desktop.ini", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(n, "autorun.inf", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(n, "System Volume Information", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Chế độ tự động (sau khi cách ly): chỉ mục Hidden + System, dấu hiệu
        // virus đặt attrib +h +s hàng loạt. Mục người dùng tự ẩn chỉ có Hidden nên
        // được giữ nguyên.
        public static int Unhide(string root, Func<bool> cancelled)
        {
            return Unhide(root, UnhideScope.VirusOnly, cancelled);
        }

        public static int Unhide(string root, UnhideScope scope, Func<bool> cancelled)
        {
            int count = 0;
            if (!CleanLock.Enter(60000))
            {
                Log.Write(S.Log.CleanBusy(root), "WARN");
                return 0;
            }
            bool paused = Policy.BeginCleanPause();
            try
            {
                foreach (TreeItem t in Tree.Walk(root))
                {
                    if (cancelled != null && cancelled()) break;
                    try
                    {
                        if (!File.Exists(t.Path) && !Directory.Exists(t.Path)) continue;
                        if (KeepSystemAttributes(t.Name)) continue;
                        FileAttributes a = File.GetAttributes(t.Path);
                        if ((a & FileAttributes.Hidden) == 0) continue;
                        if (scope == UnhideScope.VirusOnly
                            && (a & FileAttributes.System) == 0) continue;
                        if (Native.ClearHidden(t.Path)) count++;
                    }
                    catch { }
                }
            }
            finally
            {
                if (paused) Policy.EndCleanPause();
                CleanLock.Exit();
            }
            if (count > 0) Log.Write(S.Log.Unhidden(count, root));
            return count;
        }

        // Xem trước cho pipe: đếm đúng những mục Unhide sẽ chạm, không đổi gì.
        public static int CountHidden(string root, UnhideScope scope, Func<bool> cancelled)
        {
            int count = 0;
            foreach (TreeItem t in Tree.Walk(root))
            {
                if (cancelled != null && cancelled()) break;
                try
                {
                    if (!File.Exists(t.Path) && !Directory.Exists(t.Path)) continue;
                    if (KeepSystemAttributes(t.Name)) continue;
                    FileAttributes a = File.GetAttributes(t.Path);
                    if ((a & FileAttributes.Hidden) == 0) continue;
                    if (scope == UnhideScope.VirusOnly
                        && (a & FileAttributes.System) == 0) continue;
                    count++;
                }
                catch { }
            }
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
        MountPoint, StartupFile, Task, Process, Service, Defender,
        DroppedFile, FakeProcess,
        DriveShortcut, DriveFakeFolder, DriveHiddenFolder, DriveExe,
        WmiConsumer, LnkAssoc, RegHealth
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
                            i.Data["path"] = Convert.ToString(mo["ExecutablePath"]) ?? "";
                            list.Add(i);
                        }
                    }
                    catch { }
                    finally { try { mo.Dispose(); } catch { } }
                }
            }
            catch { }

            ScanDropDirs(list);
            ScanPayloadTrees(list);
            ScanImpostors(list);
            ScanOddProcesses(list);
            if (stage != null) stage(S.Pc.StageServices);
            ScanServices(list);
            LinkProcessesToPayloads(list);

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

            // Registry: các khoá File Explorer bị virus phá hoại hoặc ghi đè.
            if (cancelled != null && cancelled()) return list;
            if (stage != null) stage(S.Pc.StageRegistry);
            ScanRegistry(list);

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
                    "SELECT ProcessId, Name, CommandLine, ExecutablePath FROM Win32_Process " +
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

        // Ban sao goi payload/thu muc gia trong cac thu muc nguoi dung ghi duoc
        // (AppData, Temp, ProgramData, Public). Virus thuong chep ca cay vao day
        // de chay lai sau khi USB rut ra.
        private static void ScanPayloadTrees(List<PcItem> findings)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PcItem x in findings)
            {
                string p = Str(x, "path");
                if (p.Length > 0) seen.Add(p);
                if (x.Kind == PcKind.DriveFakeFolder)
                {
                    string fr = Str(x, "root"), ff = Str(x, "folder");
                    if (fr.Length > 0 && ff.Length > 0)
                        seenDirs.Add(System.IO.Path.Combine(fr, ff));
                }
            }

            List<string> candidates = new List<string>(ShieldGate.Detect.DropDirs());
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (profile.Length > 0)
                candidates.Add(System.IO.Path.Combine(profile, "Downloads"));
            candidates.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            candidates.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            candidates.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
            candidates.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
            candidates.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));

            List<string> roots = new List<string>();
            foreach (string d in candidates)
            {
                string dd = (d ?? "").TrimEnd('\\');
                if (dd.Length == 0 || !Directory.Exists(dd)) continue;
                bool under = false;
                foreach (string r in roots)
                    if (ShieldGate.Detect.IsUnder(dd, r)) { under = true; break; }
                if (!under) roots.Add(dd);
            }

            foreach (string dir in roots)
            {
                List<string> fakeDirs = new List<string>();
                List<TreeItem> files = new List<TreeItem>();
                try
                {
                    foreach (TreeItem t in Tree.Walk(dir, null, 6000, 15, 4))
                    {
                        if (t.IsDir)
                        {
                            bool nested = false;
                            foreach (string fd in fakeDirs)
                                if (ShieldGate.Detect.IsUnder(t.Path, fd)) { nested = true; break; }
                            if (nested) continue;
                            ShieldGate.Detect.FolderCheck fc =
                                ShieldGate.Detect.CheckFolder(t.Path, t.Name);
                            if (fc == null || !fc.Strong) continue;
                            if (!seenDirs.Add(t.Path))
                            {
                                // Thu muc da duoc bao o lan quet truoc (cac thu muc
                                // nguoi dung long nhau); van ghi nho de bo qua tep ben trong.
                                fakeDirs.Add(t.Path);
                                continue;
                            }
                            PcItem i = New(PcKind.DriveFakeFolder, true, t.Name,
                                           t.Path + "  ->  " + fc.Code);
                            i.Data["root"] = System.IO.Path.GetDirectoryName(t.Path);
                            i.Data["folder"] = t.Name;
                            findings.Add(i);
                            fakeDirs.Add(t.Path);
                        }
                        else files.Add(t);
                    }
                }
                catch { }

                foreach (TreeItem t in files)
                {
                    if (seen.Contains(t.Path)) continue;
                    bool inFake = false;
                    foreach (string fd in fakeDirs)
                        if (ShieldGate.Detect.IsUnder(t.Path, fd)) { inFake = true; break; }
                    if (inFake) continue;
                    if (!ShieldGate.Detect.IsPayloadFile(t.Path)) continue;
                    PcItem i = New(PcKind.DroppedFile, true, t.Name,
                                   "payload-bundle  ->  " + t.Path);
                    i.Data["path"] = t.Path;
                    i.Data["root"] = System.IO.Path.GetDirectoryName(t.Path);
                    findings.Add(i);
                    seen.Add(t.Path);
                }
            }

            // Goc ho so nguoi dung: chi xem cac thu muc con truc tiep, vi
            // AppData/Desktop/Downloads da duoc quet o tren.
            if (profile.Length > 0)
            {
                try
                {
                    foreach (TreeItem t in Tree.Walk(profile, null, 2000, 10, 0))
                    {
                        if (!t.IsDir || !seenDirs.Add(t.Path)) continue;
                        ShieldGate.Detect.FolderCheck fc =
                            ShieldGate.Detect.CheckFolder(t.Path, t.Name);
                        if (fc == null || !fc.Strong) continue;
                        PcItem i = New(PcKind.DriveFakeFolder, true, t.Name,
                                       t.Path + "  ->  " + fc.Code);
                        i.Data["root"] = profile;
                        i.Data["folder"] = t.Name;
                        findings.Add(i);
                    }
                }
                catch { }
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
                        i.Data["path"] = path;
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
                        i.Data["path"] = path;
                        findings.Add(i);
                    }
                    catch { }
                    finally { try { mo.Dispose(); } catch { } }
                }
            }
            catch { }
        }

        // Dich vu/driver khong nam trong Windows hay Program Files: day la cach
        // thuong tru pho bien cua virus de tu khoi dong lai sau moi lan don.
        private static void ScanServices(List<PcItem> findings)
        {
            try
            {
                using (RegistryKey root = Registry.LocalMachine.OpenSubKey(
                           @"SYSTEM\CurrentControlSet\Services"))
                {
                    if (root == null) return;
                    string[] names;
                    try { names = root.GetSubKeyNames(); }
                    catch { return; }
                    foreach (string name in names)
                    {
                        try
                        {
                            using (RegistryKey k = root.OpenSubKey(name))
                            {
                                if (k == null) continue;
                                string image = Convert.ToString(k.GetValue("ImagePath")) ?? "";
                                if (image.Length == 0) continue;
                                int start = ToInt(k.GetValue("Start"), 3);
                                int type = ToInt(k.GetValue("Type"), 1);
                                if (!ServiceSuspicious(name, image, start, type)) continue;
                                string exe = ServiceImagePath(image);
                                PcItem i = New(PcKind.Service, true, name,
                                    image + "  [Start " + start + ", Type " + type + "]");
                                i.Data["name"] = name;
                                i.Data["image"] = exe;
                                findings.Add(i);

                                // Tep chay cua dich vu cung phai roi khoi may, nhung
                                // chi sau khi dich vu da dung (FixPass 0 truoc pass 3).
                                if (exe.Length > 0 && File.Exists(exe))
                                {
                                    bool dup = false;
                                    foreach (PcItem x in findings)
                                        if (string.Equals(Str(x, "path"), exe,
                                                          StringComparison.OrdinalIgnoreCase))
                                        { dup = true; break; }
                                    if (!dup)
                                    {
                                        PcItem f = New(PcKind.DroppedFile, true,
                                            System.IO.Path.GetFileName(exe),
                                            "service-image  ->  " + exe);
                                        f.Data["path"] = exe;
                                        f.Data["root"] = System.IO.Path.GetDirectoryName(exe);
                                        findings.Add(f);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        // Chi bao dich vu co ImagePath ra khoi Windows/Program Files. Dich vu lanh
        // manh thuong nam trong hai thu muc do; file .sys cua driver cung vay.
        internal static bool ServiceSuspicious(string name, string imagePath, int start, int type)
        {
            if (start == 4 || type <= 0) return false;
            string exe = ServiceImagePath(imagePath);
            if (exe.Length == 0) return false;
            string lower = exe.ToLowerInvariant();
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows)
                                      .TrimEnd('\\').ToLowerInvariant();
            if (win.Length > 0 && lower.StartsWith(win + "\\")) return false;
            if (Regex.IsMatch(lower, @"^[a-z]:\\program files( \(x86\))?\\")) return false;

            // Windows Defender dat engine trong ProgramData\Microsoft\Windows Defender.
            // Do la dich vu cua Windows, khong phai virus; nham o day se de xuat xoa
            // dung thanh phan bao ve cua may.
            if (lower.IndexOf(@"\windows defender\", StringComparison.Ordinal) >= 0) return false;
            string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
                                       .TrimEnd('\\').ToLowerInvariant();
            if (common.Length > 0 && lower.StartsWith(common + @"\microsoft\")) return false;

            if (Regex.IsMatch(lower,
                    @"\\(appdata|temp|programdata|users\\public|downloads|desktop|documents)\\"))
                return true;
            if (exe.IndexOf('\\') < 0) return true;
            // Duong dan la khac (D:\Apps, C:\Tools...): chi bao khi tep khong co
            // chu ky so, vi trinh cai dat hop le co the dat dich vu o day.
            try
            {
                if (!File.Exists(exe)) return true;
                return !ShieldGate.Detect.IsSigned(exe);
            }
            catch { return true; }
        }

        // ImagePath co the la "\??\C:\...", "%SystemRoot%\...", co ngoac kep va
        // tham so (svchost.exe -k netsvcs). Tra ve dung duong dan tep chay.
        internal static string ServiceImagePath(string image)
        {
            string s = (image ?? "").Trim();
            if (s.Length == 0) return "";
            s = Environment.ExpandEnvironmentVariables(s);
            if (s.StartsWith(@"\??\", StringComparison.Ordinal)) s = s.Substring(4);
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (s.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
                s = System.IO.Path.Combine(win, s.Substring(12));
            if (s.StartsWith(@"System32\", StringComparison.OrdinalIgnoreCase))
                s = System.IO.Path.Combine(win, s);
            if (s.StartsWith("\"", StringComparison.Ordinal))
            {
                int end = s.IndexOf('"', 1);
                if (end > 1) s = s.Substring(1, end - 1);
            }
            else
            {
                Match m = Regex.Match(s, @"^(.*?\.(exe|sys|dll))(\s|$)",
                                      RegexOptions.IgnoreCase);
                if (m.Success) s = m.Groups[1].Value;
                else
                {
                    int sp = s.IndexOf(' ');
                    if (sp > 0) s = s.Substring(0, sp);
                }
            }
            return s.Trim();
        }

        private static int ToInt(object value, int fallback)
        {
            try { return Convert.ToInt32(value); }
            catch { return fallback; }
        }

        // Tiến trình đang chạy từ chính thư mục chứa payload/thư mục giả. Đây là
        // ứng viên ghi lại shortcut sau mỗi lần dọn: nếu chỉ cách ly tệp, vòng
        // quét nền sẽ lặp mãi (nhật ký máy bệnh 05/11: 50 lần trong 27 phút).
        // Bộ lọc theo tên (wscript/mshta, tên hệ thống giả, tên số) bỏ sót exe
        // lạ hoặc exe có chữ ký bị lợi dụng, nên phải nối theo thư mục.
        private static void LinkProcessesToPayloads(List<PcItem> findings)
        {
            List<string> dirs = new List<string>();
            HashSet<int> known = new HashSet<int>();
            HashSet<string> knownFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PcItem x in findings)
            {
                if (x.Kind == PcKind.Process || x.Kind == PcKind.FakeProcess)
                {
                    int id;
                    if (x.Data.ContainsKey("pid") && int.TryParse(Convert.ToString(x.Data["pid"]), out id))
                        known.Add(id);
                }
                if (x.Kind == PcKind.DriveFakeFolder)
                {
                    string root = Str(x, "root"), folder = Str(x, "folder");
                    if (root.Length > 0 && folder.Length > 0)
                        dirs.Add(System.IO.Path.Combine(root, folder));
                    continue;
                }
                if (x.Kind == PcKind.Service)
                {
                    string img = Str(x, "image");
                    string sd = img.Length > 0 ? System.IO.Path.GetDirectoryName(img) : "";
                    if (!string.IsNullOrEmpty(sd)) dirs.Add(sd);
                    continue;
                }
                string p = Str(x, "path");
                if (p.Length == 0) continue;
                knownFiles.Add(p);
                // Chỉ nối theo tệp payload đã biết (tên/kích thước/hash). Nối theo
                // mọi tệp đáng ngờ sẽ kéo theo thư mục Desktop/Documents và báo
                // nhầm ứng dụng portable của người dùng.
                if (ShieldGate.Detect.IsPayloadFile(p))
                {
                    string d = System.IO.Path.GetDirectoryName(p);
                    if (!string.IsNullOrEmpty(d)) dirs.Add(d);
                }
            }
            if (dirs.Count == 0) return;

            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            int self = Process.GetCurrentProcess().Id;
            try
            {
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
                        int pid = Convert.ToInt32(mo["ProcessId"]);
                        if (pid == self || known.Contains(pid)) continue;
                        string dir = System.IO.Path.GetDirectoryName(path) ?? "";
                        bool hit = false;
                        foreach (string d in dirs)
                        {
                            if (string.Equals(dir, d, StringComparison.OrdinalIgnoreCase)
                                || ShieldGate.Detect.IsUnder(path, d)) { hit = true; break; }
                        }
                        if (!hit) continue;

                        PcItem i = New(PcKind.FakeProcess, true, Convert.ToString(mo["Name"]),
                                       path + "  [PID " + pid + "]  (chay tu thu muc co payload)");
                        i.Data["pid"] = pid;
                        i.Data["path"] = path;
                        findings.Add(i);
                        known.Add(pid);

                        // Tệp chạy của tiến trình cũng là mã độc: đưa vào danh sách
                        // để cách ly sau khi tiến trình đã dừng.
                        if (!knownFiles.Contains(path))
                        {
                            PcItem f = New(PcKind.DroppedFile, true, System.IO.Path.GetFileName(path),
                                           "process-image  ->  " + path);
                            f.Data["path"] = path;
                            f.Data["root"] = dir;
                            findings.Add(f);
                            knownFiles.Add(path);
                        }
                    }
                    catch { }
                    finally { try { mo.Dispose(); } catch { } }
                }
            }
            catch { }
        }


        // Gốc các ổ cố định và ổ rời: cùng cấu trúc shortcut/thư mục giả như USB,
        // nên dùng lại bộ dò của UsbScan thay vì viết luật riêng.
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

        // Registry: các khoá File Explorer bị hỏng hoặc bị ghi đè. Mỗi mục là một
        // lựa chọn riêng trong danh sách kết quả, người dùng tự quyết định sửa.
        private static void ScanRegistry(List<PcItem> findings)
        {
            // Ghi ảnh chụp cờ xem tệp ở lần quét đầu, rồi chỉ báo khi giá trị đổi.
            // Chạy dưới SYSTEM thì RegViewGuard tự bỏ qua vì HKCU ở đó là của SYSTEM.
            RegViewGuard.EnsureBaseline();
            foreach (RegIssue issue in RegistryGuard.Detect(true))
            {
                PcItem i = New(PcKind.RegHealth, issue.High, issue.Title,
                               RegistryGuard.Detail(issue));
                i.Data["hive"] = issue.Hive;
                i.Data["path"] = issue.Path;
                i.Data["name"] = issue.Name;
                i.Data["value"] = issue.Expected;
                i.Data["kind"] = issue.Kind;
                i.Data["delete"] = issue.Remove;
                findings.Add(i);
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
                    // Task Scheduler an cac tac vu trong \Microsoft\... va tac vu Hidden
                    // theo mac dinh, nen nguoi dung tim khong thay. Van quet, nhung chi
                    // bao khi lenh tro vao thu muc nguoi dung ghi duoc hoac script host.
                    bool microsoft = rel.StartsWith("Microsoft\\", StringComparison.OrdinalIgnoreCase);
                    XmlDocument doc = new XmlDocument();
                    doc.XmlResolver = null;
                    doc.Load(path);
                    string action;
                    if (!TaskSuspicious(doc, microsoft, out action)) continue;
                    string name = System.IO.Path.GetFileName(rel);
                    string tpath = "\\" + System.IO.Path.GetDirectoryName(rel);
                    string detail = "\\" + rel + "  ->  " + action;
                    if (TaskHidden(doc)) detail += "  [hidden]";
                    PcItem i = New(PcKind.Task, true, name, detail);
                    i.Data["name"] = name;
                    i.Data["path"] = tpath;
                    findings.Add(i);
                }
                catch { }
            }
        }

        // Tác vụ đã bị tắt thì không báo lại. Trước đây mỗi lần "Kiểm tra máy" lại
        // hiện đúng tác vụ vừa tắt và ghi "Đã sửa" thêm lần nữa, nên người dùng
        // tưởng virus tạo lại tác vụ.
        internal static bool TaskSuspicious(XmlDocument doc, out string action)
        {
            return TaskSuspicious(doc, false, out action);
        }

        // microsoftFolder: tac vu nam duoi \Microsoft\... Legit Windows tasks sometimes
        // use encoded PowerShell, so require a user-writable path or script host too.
        internal static bool TaskSuspicious(XmlDocument doc, bool microsoftFolder, out string action)
        {
            action = "";
            if (doc == null) return false;
            bool enabled = true;
            XmlNodeList en = doc.GetElementsByTagName("Enabled");
            if (en.Count > 0)
                enabled = !string.Equals(en[0].InnerText.Trim(), "false",
                                         StringComparison.OrdinalIgnoreCase);
            if (!enabled) return false;
            XmlNodeList cmds = doc.GetElementsByTagName("Command");
            XmlNodeList argsList = doc.GetElementsByTagName("Arguments");
            foreach (XmlNode n in cmds) action += n.InnerText + " ";
            foreach (XmlNode n in argsList) action += n.InnerText + " ";
            action = Environment.ExpandEnvironmentVariables(action).Trim();
            if (action.Length == 0) return false;
            ShieldGate.CommandCheck v = ShieldGate.Detect.CommandVerdict(action);
            if (v == null || !v.High) return false;
            return !microsoftFolder || TaskLooksUserControlled(action);
        }

        private static bool TaskLooksUserControlled(string action)
        {
            return Regex.IsMatch(action,
                       @"(?i)\\(appdata|temp|programdata|users\\public|downloads|desktop|documents)\\")
                || Regex.IsMatch(action, @"(?i)\b(mshta|wscript|cscript)(\.exe)?\b")
                || Regex.IsMatch(action, @"(?i)\.(vbs|vbe|js|jse|wsf|hta)\b")
                || Regex.IsMatch(action, @"(?i)(^|\\)\d{1,3}\.(exe|scr|com|pif)\b")
                || Regex.IsMatch(action, @"(?i)\battrib\s+[+\-][hsrc]");
        }

        private static bool TaskHidden(XmlDocument doc)
        {
            XmlNodeList n = doc.GetElementsByTagName("Hidden");
            return n.Count > 0
                && string.Equals(n[0].InnerText.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }

        // ----------------------------------------------------------------- sửa

        internal static bool BackupReg(RegistryHive hive, string path, out string error)
        {
            string file;
            return BackupReg(hive, path, out error, out file);
        }

        // Sao lưu một khoá trước khi sửa. Chỉ trả true khi reg.exe thoát 0 **và**
        // tệp .reg có thật: exit code 0 mà không có tệp cũng không phải bản sao.
        // Bên gọi phải dừng khi hàm này trả false, để không xoá mà không có đường lùi.
        internal static bool BackupReg(RegistryHive hive, string path, out string error, out string file)
        {
            error = "";
            file = "";
            string full = (hive == RegistryHive.LocalMachine ? "HKEY_LOCAL_MACHINE\\" : "HKEY_CURRENT_USER\\") + path;
            try
            {
                Paths.EnsureDirs();
                string safe = Regex.Replace(full, @"[\\/:*?""<>|]", "_");
                string dest = System.IO.Path.Combine(Paths.BakDir,
                    "reg_" + safe + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".reg");
                string output;
                int code = Native.Run(Native.SysExe("reg.exe"),
                    "export \"" + full + "\" \"" + dest + "\" /y", 20000, out output);
                bool written = false;
                try { written = File.Exists(dest) && new FileInfo(dest).Length > 0; }
                catch { }
                if (code == 0 && written)
                {
                    file = dest;
                    return true;
                }
                error = S.Err.RegBackup(full);
                Log.Write(S.Log.RegBackupFailed(full, code), "WARN");
            }
            catch (Exception e)
            {
                error = S.Err.RegBackup(full);
                Log.Write(S.Log.RegBackupFailed(full, -1) + " " + e.Message, "WARN");
            }
            return false;
        }

        public static void Fix(IList<PcItem> items, Action<PcItem, bool, string> done, Func<bool> cancelled,
                               out int okCount, out int failCount)
        {
            okCount = 0;
            failCount = 0;
            string batch = Quarantine.NewBatch("pc", "");
            // Dừng tiến trình trước, rồi mới xoá cơ chế khởi động và tệp. Nếu xoá
            // tác vụ trước, tiến trình đang chạy sẽ tạo lại ngay trong cùng lượt.
            List<PcItem> ordered = new List<PcItem>();
            for (int pass = 0; pass < 4; pass++)
                foreach (PcItem f in items)
                    if (f.Fixable && FixPass(f.Kind) == pass) ordered.Add(f);
            foreach (PcItem f in ordered)
            {
                if (cancelled != null && cancelled()) break;
                bool ok = false;
                string err = "";
                try
                {
                    RegistryHive hive = f.Data.ContainsKey("hive")
                        ? (RegistryHive)f.Data["hive"] : RegistryHive.LocalMachine;
                    switch (f.Kind)
                    {
                        case PcKind.RunValue:
                            // Không có bản sao khoá thì không xoá giá trị.
                            if (!BackupReg(hive, Str(f, "path"), out err)) break;
                            ok = Reg.DeleteValue(hive, Str(f, "path"), Str(f, "name"));
                            break;
                        case PcKind.WinlogonShell:
                        case PcKind.WinlogonUserinit:
                        case PcKind.AppInit:
                            if (!BackupReg(hive, Str(f, "path"), out err)) break;
                            ok = Reg.Write(hive, Str(f, "path"), Str(f, "name"), f.Data["value"],
                                           f.Data.ContainsKey("kind")
                                               ? (RegistryValueKind)f.Data["kind"] : RegistryValueKind.String);
                            break;
                        case PcKind.MountPoint:
                            if (!BackupReg(hive, Str(f, "path"), out err)) break;
                            ok = Reg.DeleteTree(hive, Str(f, "path"));
                            break;
                        case PcKind.StartupFile:
                            ok = Quarantine.Add(Str(f, "path"), Str(f, "root"), batch, "startup", out err);
                            break;
                        case PcKind.Task:
                            ok = RemoveTask(Str(f, "path"), Str(f, "name"), out err);
                            break;
                        case PcKind.Service:
                            ok = RemoveService(Str(f, "name"), out err);
                            break;
                        case PcKind.DroppedFile:
                            ok = Quarantine.Add(Str(f, "path"), Str(f, "root"), batch, "dropped", out err);
                            break;
                        case PcKind.DriveShortcut:
                        case PcKind.DriveExe:
                            // Ổ chỉ đọc: không thử từng tệp để khỏi ghi hàng loạt lỗi.
                            if (!Native.CanWrite(Str(f, "root")))
                                err = S.Err.DriveReadOnly(Str(f, "root"));
                            else
                                ok = Quarantine.Add(Str(f, "path"), Str(f, "root"), batch, "drive", out err);
                            break;
                        case PcKind.DriveFakeFolder:
                            string folder = Str(f, "folder");
                            string droot = Str(f, "root");
                            // Ổ chỉ đọc: đừng thử cách ly từng tệp (mỗi tệp sẽ là một lỗi
                            // Windows 5 trong nhật ký). Báo một lần cho cả mục.
                            if (!Native.CanWrite(droot))
                            {
                                err = S.Err.DriveReadOnly(droot);
                                break;
                            }
                            int innerFail = 0;
                            int innerKept = 0;
                            UsbScan.QuarantineTree(System.IO.Path.Combine(droot, folder), droot, batch,
                                                   ref innerFail, ref innerKept);
                            if (innerKept == 0) UsbRestore.RemoveWrapper(droot, folder);
                            ok = innerFail == 0;
                            if (innerKept > 0) Log.Write(S.Log.FakeFolderKept(innerKept, droot), "INFO");
                            if (innerFail > 0) err = S.Err.Unknown;
                            break;
                        case PcKind.DriveHiddenFolder:
                            RestoreResult rr = UsbRestore.Restore(Str(f, "root"),
                                new List<string> { Str(f, "folder") }, null);
                            ok = rr.DirsLeft == 0;
                            if (rr.NoWrite) err = S.Err.DriveReadOnly(Str(f, "root"));
                            else if (!ok) err = S.Err.Unknown;
                            break;
                        case PcKind.WmiConsumer:
                            ok = DeleteWmiConsumer(Str(f, "class"), Str(f, "name"));
                            break;
                        case PcKind.LnkAssoc:
                            if (!BackupReg(hive, Str(f, "path"), out err)) break;
                            ok = Reg.DeleteValue(hive, Str(f, "path"), Str(f, "name"));
                            if (!ok) ok = Reg.DeleteTree(hive, Str(f, "path"));
                            break;
                        case PcKind.RegHealth:
                            if (!BackupReg(hive, Str(f, "path"), out err)) break;
                            if (f.Data.ContainsKey("delete") && Convert.ToBoolean(f.Data["delete"]))
                                ok = Reg.DeleteValue(hive, Str(f, "path"), Str(f, "name"));
                            else
                                ok = Reg.Write(hive, Str(f, "path"), Str(f, "name"),
                                               f.Data["value"],
                                               f.Data.ContainsKey("kind")
                                                   ? (RegistryValueKind)f.Data["kind"]
                                                   : RegistryValueKind.String);
                            break;
                        case PcKind.Process:
                        case PcKind.FakeProcess:
                            ok = StopOne(f, out err);
                            break;
                    }
                }
                catch (Exception e) { ok = false; err = e.Message; }

                // Tệp đã biến mất (bảo vệ nền vừa xử lý) không phải lỗi cần báo.
                if (!ok && err.Length == 0
                    && (f.Kind == PcKind.StartupFile || f.Kind == PcKind.DroppedFile
                        || f.Kind == PcKind.DriveShortcut || f.Kind == PcKind.DriveExe))
                    ok = true;

                if (ok) { okCount++; Log.Write(S.Log.PcFixed(f.Kind.ToString(), f.Arg1), "WARN"); }
                else failCount++;
                if (done != null) done(f, ok, err);
            }
            try { Settings.SetPcFixEvent(okCount, failCount); }
            catch { }
        }

        // Thứ tự sửa: 0 tiến trình/dịch vụ, 1 tác vụ/WMI, 2 startup/registry,
        // 3 tệp. Tách hàm để self-test kiểm được mà không cần chạy schtasks.
        internal static int FixPass(PcKind kind)
        {
            switch (kind)
            {
                case PcKind.Process:
                case PcKind.FakeProcess:
                case PcKind.Service: return 0;
                case PcKind.Task:
                case PcKind.WmiConsumer: return 1;
                case PcKind.RunValue:
                case PcKind.WinlogonShell:
                case PcKind.WinlogonUserinit:
                case PcKind.AppInit:
                case PcKind.StartupFile:
                case PcKind.LnkAssoc:
                case PcKind.MountPoint: return 2;
                default: return 3;
            }
        }

        // Chỉ dừng đúng tiến trình đã quét: PID có thể bị hệ thống cấp lại cho
        // tiến trình khác giữa lúc quét và lúc sửa. Tên luôn kiểm được; đường dẫn
        // chỉ kiểm được khi tiến trình cùng kiến trúc (x86 đọc x64 sẽ ném lỗi).
        private static bool StopOne(PcItem f, out string error)
        {
            error = "";
            int pid;
            if (!f.Data.ContainsKey("pid")
                || !int.TryParse(Convert.ToString(f.Data["pid"]), out pid) || pid <= 0)
            {
                error = S.Err.Unknown;
                return false;
            }
            try
            {
                using (Process p = Process.GetProcessById(pid))
                {
                    string name = f.Arg1 ?? "";
                    if (name.Length > 0
                        && !string.Equals(p.ProcessName + ".exe", name, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(p.ProcessName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        error = S.Err.ProcessChanged(name);
                        return false;
                    }
                    string expected = Str(f, "path");
                    if (expected.Length > 0)
                    {
                        string actual = Native.ProcessImagePath(pid);
                        if (actual.Length > 0
                            && !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                        {
                            error = S.Err.ProcessChanged(name);
                            return false;
                        }
                    }
                    p.Kill();
                }
                return true;
            }
            catch (ArgumentException)
            {
                // Tiến trình đã tự thoát: không phải lỗi.
                return true;
            }
            catch (Exception e) { error = e.Message; return false; }
        }

        // Sao lưu XML rồi xoá hẳn tác vụ. Chỉ tắt (/DISABLE) thì tiến trình đang
        // chạy bật lại được và tác vụ vẫn nằm đó chờ người dùng bấm nhầm.
        // Không sao lưu được thì lùi về tắt, để vẫn còn đường lùi.
        internal static bool RemoveTask(string folder, string name, out string error)
        {
            error = "";
            string tn = (folder ?? "").TrimEnd('\\') + "\\" + (name ?? "");
            string output;
            string backup;
            if (BackupTask(tn, out backup, out output))
            {
                if (Native.Run(Native.SysExe("schtasks.exe"),
                               "/Delete /TN \"" + tn + "\" /F", 20000, out output) == 0)
                {
                    Log.Write(S.Log.TaskDeleted(tn), "WARN");
                    return true;
                }
                error = output;
                return false;
            }
            bool ok = Native.Run(Native.SysExe("schtasks.exe"),
                                 "/Change /TN \"" + tn + "\" /DISABLE", 20000, out output) == 0;
            if (!ok) error = output;
            else Log.Write(S.Log.TaskDisabled(tn), "WARN");
            return ok;
        }

        // Sao lưu khoá dịch vụ rồi dừng và xoá. Chỉ xoá khi đã có bản sao .reg;
        // dịch vụ đang chạy sẽ chặn xoá tệp nên phải dừng trước (FixPass 0).
        internal static bool RemoveService(string name, out string error)
        {
            error = "";
            if (string.IsNullOrEmpty(name)) { error = S.Err.Unknown; return false; }
            string key = @"SYSTEM\CurrentControlSet\Services\" + name;
            if (!BackupReg(RegistryHive.LocalMachine, key, out error)) return false;

            string output;
            Native.Run(Native.SysExe("sc.exe"), "stop \"" + name + "\"", 20000, out output);
            int code = Native.Run(Native.SysExe("sc.exe"), "delete \"" + name + "\"",
                                  20000, out output);
            if (code == 0)
            {
                Log.Write(S.Log.ServiceDeleted(name), "WARN");
                return true;
            }
            error = output;
            return false;
        }

        private static bool BackupTask(string taskName, out string file, out string error)
        {
            file = "";
            error = "";
            try
            {
                Paths.EnsureDirs();
                string xml;
                int code = Native.Run(Native.SysExe("schtasks.exe"),
                    "/Query /TN \"" + taskName + "\" /XML", 20000, out xml);
                if (code != 0 || string.IsNullOrEmpty(xml)
                    || xml.IndexOf("<Task", StringComparison.OrdinalIgnoreCase) < 0
                    || xml.Length > 1024 * 1024)
                    return false;
                string safe = Regex.Replace(taskName.Trim('\\'), @"[\\/:*?""<>|]", "_");
                string dest = System.IO.Path.Combine(Paths.BakDir,
                    "task_" + safe + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".xml");
                File.WriteAllText(dest, xml, new System.Text.UTF8Encoding(false));
                if (!File.Exists(dest) || new FileInfo(dest).Length == 0) return false;
                file = dest;
                return true;
            }
            catch (Exception e) { error = e.Message; return false; }
        }

        // Ghi từng mục ra nhật ký. Trước đây chỉ có "N mục đáng ngờ" nên nhật ký
        // gửi đi không cho biết tiến trình/tác vụ nào, không chẩn đoán được.
        public static void LogFindings(IList<PcItem> items)
        {
            if (items == null) return;
            foreach (PcItem i in items)
                Log.Write(S.Log.PcItem(i.Kind.ToString(), i.Arg1, Cut(i.Detail, 300)), "INFO");
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

        public static bool KeyExists(RegistryHive hive, string path)
        {
            try
            {
                using (RegistryKey k = Base(hive).OpenSubKey(path, false))
                    return k != null;
            }
            catch { return false; }
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
                    // Khóa chưa từng tồn tại thì coi như đã xóa xong: nếu trả về false
                    // thì mọi lần "tắt bảo vệ" đều bị báo lỗi, dù chẳng có gì để xóa.
                    if (k == null) return true;
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
                using (RegistryKey k = Base(hive).OpenSubKey(path, false))
                {
                    if (k == null) return true;
                }
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
