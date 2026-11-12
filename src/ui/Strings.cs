// ShieldGate - toàn bộ chuỗi hiện ra với người dùng, hai ngôn ngữ Việt / Anh.
// Giao diện không được viết chuỗi thẳng; mọi câu chữ đi qua lớp này.
using System;

namespace ShieldGate
{
    internal static class S
    {
        private static string _lang = Bootstrap.LanguageVi;

        public static void SetLanguage(string lang)
        {
            _lang = Bootstrap.IsLanguage(lang) ? lang.ToLowerInvariant() : Bootstrap.LanguageVi;
        }

        public static string Language { get { return _lang; } }
        public static bool English { get { return _lang == Bootstrap.LanguageEn; } }

        private static string T(string vi, string en)
        {
            return English ? en : vi;
        }

        public static class App
        {
            public static string Name { get { return AppInfo.Name; } }
            public static string Tagline { get { return T("Chặn và dọn virus shortcut", "Block and clean shortcut viruses"); } }
            public static string WindowTitle { get { return AppInfo.Name; } }
        }

        public static class Nav
        {
            public static string Overview { get { return T("Tổng quan", "Overview"); } }
            public static string Usb { get { return T("Xử lý USB", "USB scan"); } }
            public static string Protect { get { return T("Bảo vệ", "Protection"); } }
            public static string Log { get { return T("Nhật ký", "Log"); } }
            public static string About { get { return T("Giới thiệu", "About"); } }
        }

        public static class Footer
        {
            public static string Line(string version)
            {
                return T("ShieldGate " + version + " · mã nguồn mở, giấy phép MIT",
                         "ShieldGate " + version + " · open source, MIT license");
            }
            public static string Privacy { get { return T("Không gửi dữ liệu ra ngoài", "No data leaves this computer"); } }
        }

        public static class Overview
        {
            public static string Title { get { return T("Tổng quan", "Overview"); } }
            public static string Refresh { get { return T("Kiểm tra lại", "Check again"); } }
            public static string StateSafe { get { return T("Máy tính đang được bảo vệ", "This computer is protected"); } }
            public static string StateAttentionFiles(int n) { return T("Cần chú ý " + n + " tệp", "Needs attention: " + n + " files"); }
            public static string StateScanning { get { return T("Đang kiểm tra máy tính", "Checking this computer"); } }
            public static string StateNeverTitle { get { return T("Chưa kiểm tra máy tính lần nào", "This computer has never been checked"); } }
            public static string SubNever { get { return T("Kiểm tra lần đầu để biết máy có bị nhiễm hay không.", "Run the first check to see whether it is infected."); } }
            public static string SubScanned(string when, int files)
            {
                return T("Lần kiểm tra gần nhất: " + when + " · " + files.ToString("#,0") + " tệp · không phát hiện gì",
                         "Last check: " + when + " · " + files.ToString("#,0") + " files · nothing found");
            }
            public static string SubScannedShort(string when)
            {
                return T("Lần kiểm tra gần nhất: " + when + " · không phát hiện gì",
                         "Last check: " + when + " · nothing found");
            }
            public static string SubAttention(int n)
            {
                return T(n + " mục khởi động đáng ngờ. Kiểm tra để xem chi tiết.",
                         n + " suspicious startup items. Check to see details.");
            }
            public static string SubAttentionUnseen(int n)
            {
                return T(n + " mục khởi động đáng ngờ từ lần kiểm tra trước. Kiểm tra lại để xem chi tiết.",
                         n + " suspicious startup items from the last check. Check again to see details.");
            }
            public static string SubScannedUnprotected(string when)
            {
                return T("Máy không phát hiện gì, nhưng chưa bật bảo vệ. Lần kiểm tra gần nhất: " + when + ".",
                         "Nothing found, but protection is off. Last check: " + when + ".");
            }
            public static string ActionProtect { get { return T("Bật bảo vệ", "Turn on protection"); } }
            public static string ActionScan { get { return T("Kiểm tra máy", "Check this computer"); } }
            public static string ActionSeeFiles(int n) { return T("Xem " + n + " tệp", "View " + n + " files"); }
            public static string ActionLog { get { return T("Xem nhật ký", "View log"); } }
            public static string Cancel { get { return T("Huỷ", "Cancel"); } }
            public static string JustNow { get { return T("vừa xong", "just now"); } }
            public static string Today(string time) { return T("hôm nay, " + time, "today, " + time); }
            public static string Yesterday(string time) { return T("hôm qua, " + time, "yesterday, " + time); }

            public static string Drives { get { return T("Ổ USB", "USB drives"); } }
            public static string DrivesEmpty { get { return T("Không có USB nào đang cắm", "No USB drive is plugged in"); } }
            public static string DrivesEmptyHint
            {
                get
                {
                    return T("Cắm USB vào, ShieldGate sẽ kiểm tra và dọn tự động nếu bảo vệ nền đang bật.",
                             "Plug in a USB drive. ShieldGate will check and clean it automatically when background protection is on.");
                }
            }
            public static string DriveSub(string fs, int files, string when)
            {
                return T(fs + " · " + files.ToString("#,0") + " tệp · kiểm tra " + when,
                         fs + " · " + files.ToString("#,0") + " files · checked " + when);
            }
            public static string DriveSubNew(string fs) { return T(fs + " · chưa kiểm tra", fs + " · not checked yet"); }
            public static string ChipSafe { get { return T("An toàn", "Safe"); } }
            public static string ChipAttention(int n) { return T(n + " tệp cần xử lý", n + " files need action"); }
            public static string ChipScanning { get { return T("Đang kiểm tra", "Checking"); } }
            public static string ChipUnreadable { get { return T("Không đọc được", "Unreadable"); } }
            public static string ChipNew { get { return T("Chưa kiểm tra", "Not checked"); } }

            public static string Protect { get { return T("Bảo vệ", "Protection"); } }
            public static string ProtectOk
            {
                get
                {
                    return T("AutoRun đã tắt · autorun.inf đã chặn · bảo vệ nền đang chạy",
                             "AutoRun is off · autorun.inf is blocked · background protection is running");
                }
            }
            public static string ProtectPartial(int n)
            {
                return T("Bảo vệ chưa đầy đủ — còn " + n + " mục chưa bật",
                         "Protection is incomplete — " + n + " items are off");
            }
            public static string ProtectOff { get { return T("Chưa bật bảo vệ", "Protection is off"); } }
            public static string ProtectAction { get { return T("Tuỳ chỉnh", "Customize"); } }

            public static string Quarantine { get { return T("Cách ly", "Quarantine"); } }
            public static string QuarantineCount(int n) { return T(n + " tệp đang cách ly", n + " files in quarantine"); }
            public static string QuarantineEmpty { get { return T("Chưa có tệp nào bị cách ly", "No files in quarantine"); } }
            public static string QuarantineAction { get { return T("Khôi phục tệp", "Restore files"); } }
        }

        public static class Usb
        {
            public static string Title { get { return T("Xử lý USB", "USB scan"); } }
            public static string Choose { get { return T("Chọn ổ", "Choose drive"); } }
            public static string NoDriveTitle { get { return T("Không tìm thấy USB nào", "No USB drive found"); } }
            public static string NoDriveHint { get { return T("Cắm USB vào rồi bấm Làm mới danh sách.", "Plug in a USB drive, then refresh the list."); } }
            public static string IdleTitle { get { return T("Sẵn sàng kiểm tra", "Ready to scan"); } }
            public static string IdleHint
            {
                get
                {
                    return T("Bấm Kiểm tra USB để tìm shortcut, script và autorun độc hại.",
                             "Click Scan USB to find malicious shortcuts, scripts and autorun files.");
                }
            }
            public static string Starting { get { return T("Bắt đầu xem các tệp…", "Looking at files…"); } }
            public static string ScanningCount(int files) { return T("Đã xem " + files.ToString("#,0") + " tệp", "Scanned " + files.ToString("#,0") + " files"); }
            public static string Refresh { get { return T("Làm mới danh sách", "Refresh list"); } }
            public static string Scan { get { return T("Kiểm tra USB", "Scan USB"); } }
            public static string Scanning(string drive) { return T("Đang kiểm tra " + drive, "Scanning " + drive); }
            public static string ScanningSub(int files, string path)
            {
                return T("Đã xem " + files.ToString("#,0") + " tệp · " + path,
                         "Scanned " + files.ToString("#,0") + " files · " + path);
            }
            public static string Vaccine { get { return T("Chặn autorun trên USB này", "Block autorun on this drive"); } }
            public static string VaccineHint
            {
                get
                {
                    return T("Tạo một thư mục tên autorun.inf ở gốc ổ, để virus không ghi được tệp autorun.",
                             "Create a folder named autorun.inf at the drive root so viruses cannot write an autorun file.");
                }
            }
            public static string VaccineDone(string drive) { return T("Đã chặn autorun trên " + drive + ".", "Autorun blocked on " + drive + "."); }
            public static string VaccineExists { get { return T("USB này đã được chặn autorun từ trước.", "This USB drive was already protected from autorun."); } }
            public static string ResultsTitle(int n) { return T(n + " tệp cần xử lý", n + " files need action"); }
            public static string ResultsNote
            {
                get
                {
                    return T("Bỏ chọn những tệp bạn muốn giữ. Tệp không chọn sẽ nằm nguyên tại chỗ.",
                             "Clear files you want to keep. Files you clear stay where they are.");
                }
            }
            public static string SelectAll { get { return T("Chọn tất cả", "Select all"); } }
            public static string SelectNone { get { return T("Bỏ chọn tất cả", "Clear selection"); } }
            public static string ListName { get { return T("Danh sách tệp cần xử lý", "Files that need action"); } }
            public static string PartialNote(int n)
            {
                return T(n + " tệp không cách ly được. Xem nhật ký để biết lý do.",
                         n + " files could not be quarantined. See the log for details.");
            }
            public static string Apply(int n) { return T("Cách ly " + n + " tệp", "Quarantine " + n + " files"); }
            public static string CleanTitle { get { return T("Không phát hiện gì", "Nothing found"); } }
            public static string CleanBody(int n)
            {
                return T("Đã kiểm tra " + n.ToString("#,0") + " tệp. Không có shortcut, script hay autorun độc hại.",
                         "Checked " + n.ToString("#,0") + " files. No malicious shortcuts, scripts or autorun files.");
            }
            public static string DoneTitle(int n) { return T("Đã cách ly " + n + " tệp", "Quarantined " + n + " files"); }
            public static string DoneBody(int unhidden)
            {
                return T("Đã bỏ thuộc tính ẩn cho " + unhidden + " mục. Tệp vẫn nằm trong vùng cách ly, khôi phục được bất cứ lúc nào.",
                         "Unhid " + unhidden + " items. Files are in quarantine and can be restored at any time.");
            }
            public static string DoneBodyNoUnhide
            {
                get
                {
                    return T("Tệp vẫn nằm trong vùng cách ly, khôi phục được bất cứ lúc nào.",
                             "Files are in quarantine and can be restored at any time.");
                }
            }
            public static string Undo { get { return T("Hoàn tác", "Undo"); } }
            public static string OpenLog { get { return T("Xem nhật ký", "View log"); } }
            public static string ScanCancelled { get { return T("Đã huỷ lần kiểm tra.", "Scan cancelled."); } }
            public static string UndoDone(int restored, int kept)
            {
                if (kept <= 0)
                    return T("Đã hoàn tác. " + restored + " tệp trở về vị trí cũ.",
                             "Undone. " + restored + " files returned to their original locations.");
                return T("Đã hoàn tác. " + restored + " tệp trở về vị trí cũ, " + kept + " tệp đã có sẵn nên được giữ nguyên.",
                         "Undone. " + restored + " files returned to their original locations; " + kept + " existing files were kept.");
            }
        }

        public static class Reason
        {
            public static string Text(System.Collections.Generic.IList<string> codes)
            {
                if (codes == null || codes.Count == 0) return "";
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (string code in codes)
                {
                    string line = One(code);
                    if (line.Length == 0) continue;
                    if (sb.Length > 0) sb.Append(" · ");
                    sb.Append(line);
                }
                return sb.ToString();
            }

            private static string One(string code)
            {
                if (code == null) return "";
                if (code.StartsWith("target=", StringComparison.Ordinal)) return DangerTarget(code.Substring(7));
                if (code.StartsWith("args=", StringComparison.Ordinal)) return Args(code.Substring(5));
                if (code == "target-missing") return TargetMissing;
                if (code == "unreadable-hidden") return UnreadableHidden;
                if (code == "shadows-hidden-folder") return ShadowsHiddenFolder;
                if (code.StartsWith("markers=", StringComparison.Ordinal)) return ScriptMarkers;
                if (code == "encoded-vbscript") return EncodedVbs;
                if (code == "autorun-payload") return AutorunPayload;
                if (code == "hidden-root-exe") return HiddenRootExe;
                return "";
            }

            public static string DangerTarget(string leaf)
            {
                return T("Shortcut mở " + leaf + " — công cụ chạy script mà virus shortcut thường dùng.",
                         "Shortcut opens " + leaf + " — a script runner commonly used by shortcut viruses.");
            }
            public static string Args(string args) { return T("Shortcut truyền lệnh đáng ngờ: " + args, "Shortcut passes a suspicious command: " + args); }
            public static string TargetMissing { get { return T("Shortcut trỏ tới tệp không còn tồn tại.", "Shortcut points to a file that no longer exists."); } }
            public static string UnreadableHidden { get { return T("Shortcut bị ẩn và không đọc được nội dung.", "Shortcut is hidden and its contents cannot be read."); } }
            public static string ShadowsHiddenFolder { get { return T("Shortcut cùng tên với một thư mục đang bị ẩn.", "Shortcut has the same name as a hidden folder."); } }
            public static string ScriptMarkers { get { return T("Script chứa lệnh tạo shortcut và ẩn tệp — dấu hiệu của virus shortcut.", "Script creates shortcuts and hides files — a sign of a shortcut virus."); } }
            public static string EncodedVbs { get { return T("Script VBScript đã bị mã hoá để tránh bị đọc.", "VBScript file is encoded to avoid inspection."); } }
            public static string AutorunPayload { get { return T("Tệp autorun sẽ tự chạy một chương trình khi cắm USB.", "The autorun file will launch a program when the USB drive is plugged in."); } }
            public static string HiddenRootExe { get { return T("Tệp chạy bị ẩn ở gốc ổ — vị trí virus thường dùng.", "A hidden executable at the drive root — a location viruses often use."); } }
        }

        public static class Protect
        {
            public static string Title { get { return T("Bảo vệ", "Protection"); } }
            public static string Intro
            {
                get
                {
                    return T("Bật một lần. ShieldGate giữ các thiết lập này và kiểm tra USB mỗi khi cắm vào.",
                             "Turn it on once. ShieldGate keeps these settings and checks every USB drive when plugged in.");
                }
            }
            public static string SwitchAutorun { get { return T("Tắt tự chạy khi cắm USB", "Turn off AutoRun for USB drives"); } }
            public static string SwitchAutorunDetail
            {
                get
                {
                    return T("Windows không tự mở chương trình trên USB hay đĩa khi bạn cắm vào.",
                             "Windows will not open programs from USB drives automatically.");
                }
            }
            public static string SwitchIni { get { return T("Chặn tệp autorun.inf", "Block autorun.inf"); } }
            public static string SwitchIniDetail
            {
                get
                {
                    return T("USB không thể tạo tệp autorun.inf, kể cả khi đã bị nhiễm.",
                             "USB drives cannot create autorun.inf, even if already infected.");
                }
            }
            public static string SwitchWsh { get { return T("Chặn Windows Script Host", "Block Windows Script Host"); } }
            public static string SwitchWshDetail
            {
                get
                {
                    return T("Chặn script .vbs và .js. Chỉ bật nếu máy không dùng phần mềm cần VBScript.",
                             "Block .vbs and .js scripts. Only enable if this computer does not need VBScript.");
                }
            }
            public static string SwitchGuard { get { return T("Kiểm tra USB tự động", "Check USB drives automatically"); } }
            public static string SwitchGuardDetail
            {
                get
                {
                    return T("Chạy nền và kiểm tra mỗi ổ USB ngay khi cắm vào.",
                             "Run in the background and check every USB drive as soon as it is plugged in.");
                }
            }
            public static string On { get { return T("Bật bảo vệ", "Turn on protection"); } }
            public static string Off { get { return T("Tắt bảo vệ", "Turn off protection"); } }
            public static string Tech { get { return T("Chi tiết kỹ thuật", "Technical details"); } }
            public static string BackupNote
            {
                get
                {
                    return T("Thay đổi được sao lưu. Tắt bảo vệ sẽ trả Windows về đúng cấu hình trước khi bật.",
                             "Changes are backed up. Turning protection off restores the previous Windows configuration.");
                }
            }
            public static string AdminOnly { get { return T("Cần quyền quản trị để bật bảo vệ.", "Administrator rights are required to turn on protection."); } }
            public static string Reload { get { return T("Chạy lại với quyền quản trị", "Restart as administrator"); } }
            public static string Applying { get { return T("Đang áp dụng thiết lập…", "Applying settings…"); } }
            public static string TechHide { get { return T("Ẩn chi tiết kỹ thuật", "Hide technical details"); } }
        }

        public static class Restore
        {
            public static string Title { get { return T("Khôi phục tệp", "Restore files"); } }
            public static string Intro { get { return T("Tệp bị cách ly vẫn còn nguyên, chỉ bị chuyển khỏi vị trí cũ.", "Quarantined files are intact, only moved from their original location."); } }
            public static string Empty { get { return T("Chưa có tệp nào bị cách ly.", "No files in quarantine."); } }
            public static string Action { get { return T("Khôi phục lần này", "Restore this batch"); } }
            public static string OpenFolder { get { return T("Mở thư mục cách ly", "Open quarantine folder"); } }
            public static string Done(int restored, int kept)
            {
                return T("Đã khôi phục " + restored + " tệp. " + kept + " tệp đã có sẵn nên được giữ nguyên.",
                         "Restored " + restored + " files. " + kept + " files already existed and were kept.");
            }
            public static string NothingSelected { get { return T("Chọn một lần cách ly trong danh sách.", "Select a quarantine batch."); } }
            public static string Missing(int n) { return T(n + " tệp không còn trong vùng cách ly.", n + " files are no longer in quarantine."); }
            public static string Summary(int count, string size) { return T(count + " tệp · " + size, count + " files · " + size); }
        }

        public static class Pc
        {
            public static string Title { get { return T("Kiểm tra máy tính", "Computer check"); } }
            public static string Results(int n) { return T(n + " mục cần xử lý", n + " items need action"); }
            public static string SeverityHigh { get { return T("Nghiêm trọng", "Severe"); } }
            public static string SeverityMedium { get { return T("Cần chú ý", "Needs attention"); } }
            public static string Fix(int n) { return T("Sửa " + n + " mục", "Fix " + n + " items"); }
            public static string FixNote
            {
                get
                {
                    return T("Cấu hình được sao lưu trước khi sửa. Tệp được cách ly, không xoá.",
                             "Configuration is backed up before fixing. Files are quarantined, not deleted.");
                }
            }
            public static string StageStartup { get { return T("Đang xem mục khởi động", "Checking startup items"); } }
            public static string StageLogin { get { return T("Đang xem quá trình đăng nhập", "Checking sign-in"); } }
            public static string StageDrives { get { return T("Đang xem ổ đĩa đã cắm", "Checking plugged-in drives"); } }
            public static string StageTasks { get { return T("Đang xem tác vụ hẹn giờ", "Checking scheduled tasks"); } }
            public static string StageProcesses { get { return T("Đang xem tiến trình đang chạy", "Checking running processes"); } }
            public static string StageDefender { get { return T("Đang xem bảo vệ của Windows", "Checking Windows protection"); } }
            public static string Clean { get { return T("Không phát hiện gì bất thường", "Nothing unusual found"); } }
            public static string CleanBody
            {
                get
                {
                    return T("Đã kiểm tra mục khởi động, tác vụ hẹn giờ, tiến trình đang chạy và các ổ đĩa.",
                             "Checked startup items, scheduled tasks, running processes and drives.");
                }
            }
            public static string Done(int fixedCount, int manual)
            {
                if (manual <= 0) return T("Đã sửa " + fixedCount + " mục.", "Fixed " + fixedCount + " items.");
                return T("Đã sửa " + fixedCount + " mục. " + manual + " mục cần làm tay theo hướng dẫn.",
                         "Fixed " + fixedCount + " items. " + manual + " items need manual action.");
            }
            public static string Manual(string what) { return T("Cần làm tay: " + what, "Manual action: " + what); }
            public static string Fixing { get { return T("Đang sửa các mục đã chọn…", "Fixing selected items…"); } }

            public static string RunValue(string name) { return T("Chương trình tự chạy khi đăng nhập: " + name, "Program that starts at sign-in: " + name); }
            public static string WinlogonShell { get { return T("Màn hình đăng nhập bị đổi chương trình khởi chạy", "Sign-in screen launches a different program"); } }
            public static string WinlogonUserinit { get { return T("Quá trình đăng nhập bị chèn thêm chương trình", "Sign-in process has an extra program"); } }
            public static string AppInit { get { return T("Thư viện lạ được nạp vào mọi chương trình", "An unknown library is loaded into every program"); } }
            public static string MountPoint(string drive) { return T("Ổ " + drive + " tự chạy chương trình khi cắm vào", "Drive " + drive + " launches a program when plugged in"); }
            public static string StartupFile(string name) { return T("Tệp trong thư mục khởi động: " + name, "File in the startup folder: " + name); }
            public static string Task(string name) { return T("Tác vụ hẹn giờ đáng ngờ: " + name, "Suspicious scheduled task: " + name); }
            public static string Process(string name) { return T("Script đang chạy: " + name, "Script is running: " + name); }
            public static string Defender { get { return T("Bảo vệ thời gian thực của Windows đang tắt", "Windows real-time protection is off"); } }
        }

        public static class Log
        {
            public static string Title { get { return T("Nhật ký", "Log"); } }
            public static string All { get { return T("Tất cả", "All"); } }
            public static string ErrorsOnly { get { return T("Chỉ lỗi", "Errors only"); } }
            public static string Export { get { return T("Xuất nhật ký", "Export log"); } }
            public static string Empty { get { return T("Chưa có hoạt động nào.", "No activity yet."); } }
            public static string NoErrors { get { return T("Không có lỗi nào được ghi.", "No errors were logged."); } }
            public static string Refresh { get { return T("Làm mới", "Refresh"); } }
            public static string ExportTitle { get { return T("Lưu nhật ký", "Save log"); } }
            public static string FilterName { get { return T("Tệp văn bản", "Text file"); } }
            public static string Exported(string path) { return T("Đã lưu nhật ký vào " + path + ".", "Log saved to " + path + "."); }
            public static string OpenFolder { get { return T("Mở thư mục nhật ký", "Open log folder"); } }
        }

        public static class About
        {
            public static string Title { get { return T("Giới thiệu", "About"); } }
            public static string Version(string v) { return T("Phiên bản " + v, "Version " + v); }
            public static string License { get { return T("Mã nguồn mở, giấy phép MIT", "Open source, MIT license"); } }
            public static string Source { get { return T("Mã nguồn", "Source code"); } }
            public static string Data { get { return T("Dữ liệu", "Data"); } }
            public static string Hash { get { return T("SHA256 của tệp đang chạy", "SHA256 of the running file"); } }
            public static string NoNetwork { get { return T("Không gửi dữ liệu ra ngoài. Không có telemetry.", "No data leaves this computer. No telemetry."); } }
            public static string Update { get { return T("Kiểm tra phiên bản mới", "Check for updates"); } }
            public static string SourceSoon { get { return T("Sẽ công bố cùng bản phát hành đầu tiên.", "Will be published with the first release."); } }
            public static string UpdateSoon { get { return T("Tính năng này sẽ bật khi có bản phát hành.", "This feature will be enabled with a release."); } }
            public static string OpenFolder { get { return T("Mở thư mục", "Open folder"); } }
        }

        public static class Setup
        {
            public static string Title { get { return T("Thiết lập ShieldGate", "Set up ShieldGate"); } }
            public static string Welcome { get { return T("Chào mừng đến với ShieldGate", "Welcome to ShieldGate"); } }
            public static string Intro
            {
                get
                {
                    return T("Chọn nơi lưu nhật ký, tệp cách ly và bản sao lưu, cùng ngôn ngữ hiển thị.",
                             "Choose where ShieldGate stores logs, quarantined files and backups, and the display language.");
                }
            }
            public static string DataLabel { get { return T("Thư mục dữ liệu", "Data folder"); } }
            public static string DataHint
            {
                get
                {
                    return T("Có thể dùng thư mục mặc định hoặc chọn một thư mục khác trên ổ cố định của máy.",
                             "Use the default folder or choose another folder on a local fixed drive.");
                }
            }
            public static string Browse { get { return T("Chọn thư mục…", "Browse…"); } }
            public static string LanguageLabel { get { return T("Ngôn ngữ", "Language"); } }
            public static string LanguageVi { get { return "Tiếng Việt"; } }
            public static string LanguageEn { get { return "English"; } }
            public static string Continue { get { return T("Tiếp tục", "Continue"); } }
            public static string ChooseFolder { get { return T("Chọn thư mục dữ liệu", "Choose data folder"); } }
            public static string InvalidPath
            {
                get
                {
                    return T("Không ghi được vào thư mục này. Chọn thư mục khác rồi thử lại.",
                             "ShieldGate cannot write to this folder. Choose another folder and try again.");
                }
            }
            public static string SaveFailed
            {
                get
                {
                    return T("Không lưu được thiết lập. Kiểm tra quyền ghi vào thư mục dữ liệu.",
                             "Could not save settings. Check write access to the data folder.");
                }
            }
            public static string DefaultNote
            {
                get
                {
                    return T("Mặc định: C:\\ProgramData\\ShieldGate", "Default: C:\\ProgramData\\ShieldGate");
                }
            }
        }

        public static class Err
        {
            public static string Admin { get { return T("Cần quyền quản trị để sửa máy.", "Administrator rights are required to change this computer."); } }
            public static string AdminAction { get { return T("Chạy lại với quyền quản trị", "Restart as administrator"); } }
            public static string Locked(string path)
            {
                return T("Không đọc được " + path + " — có thể chương trình khác đang mở tệp này.",
                         "Could not read " + path + " — another program may have it open.");
            }
            public static string ReadOnly { get { return T("USB đang ở chế độ chỉ đọc. Bỏ khoá ghi trên USB rồi thử lại.", "The USB drive is read-only. Remove the write lock and try again."); } }
            public static string Space(int files, string need)
            {
                return T("Không đủ dung lượng để cách ly " + files + " tệp. Cần thêm " + need + ".",
                         "Not enough space to quarantine " + files + " files. Need " + need + " more.");
            }
            public static string Busy { get { return T("Đang có một lần kiểm tra khác chạy. Đợi lần đó xong rồi thử lại.", "Another scan is running. Wait for it to finish and try again."); } }
            public static string Unplugged(string drive) { return T("Ổ " + drive + " đã được rút ra trong lúc kiểm tra.", "Drive " + drive + " was removed during the scan."); }
            public static string Unknown { get { return T("Đã xảy ra lỗi không mong muốn.", "An unexpected error occurred."); } }
            public static string UnknownAction { get { return T("Mở nhật ký", "Open log"); } }
            public static string Partial(int n) { return T(n + " tệp không xử lý được. Xem nhật ký để biết lý do.", n + " files could not be processed. See the log for details."); }
            public static string AutrunConflict
            {
                get
                {
                    return T("USB đã có tệp autorun.inf. Xử lý tệp đó trước rồi chặn autorun.",
                             "The USB drive already has an autorun.inf file. Handle that file first, then block autorun.");
                }
            }
        }

        public static class Dlg
        {
            public static string QuarantineTitle(int n) { return T("Cách ly " + n + " tệp?", "Quarantine " + n + " files?"); }
            public static string QuarantineBody(string list)
            {
                return T(list + "\n\nTệp được chuyển vào vùng cách ly, không bị xoá. Bạn khôi phục lại bất cứ lúc nào.",
                         list + "\n\nFiles are moved to quarantine, not deleted. You can restore them at any time.");
            }
            public static string QuarantineOk(int n) { return T("Cách ly " + n + " tệp", "Quarantine " + n + " files"); }
            public static string Cancel { get { return T("Huỷ", "Cancel"); } }
            public static string FixTitle(int n) { return T("Sửa " + n + " mục?", "Fix " + n + " items?"); }
            public static string FixBody(string list)
            {
                return T("Cấu hình hiện tại được sao lưu trước khi sửa.\n\n" + list,
                         "The current configuration is backed up before fixing.\n\n" + list);
            }
            public static string FixOk(int n) { return T("Sửa " + n + " mục", "Fix " + n + " items"); }
            public static string ProtectOnTitle { get { return T("Bật bảo vệ?", "Turn on protection?"); } }
            public static string ProtectOnBody(int changes)
            {
                return T("ShieldGate sẽ thay đổi " + changes + " thiết lập của Windows. Mọi thay đổi được sao lưu và hoàn tác được.",
                         "ShieldGate will change " + changes + " Windows settings. Changes are backed up and can be undone.");
            }
            public static string ProtectOnOk { get { return T("Bật bảo vệ", "Turn on protection"); } }
            public static string ProtectOffTitle { get { return T("Tắt bảo vệ?", "Turn off protection?"); } }
            public static string ProtectOffBody
            {
                get
                {
                    return T("Windows sẽ trở về cấu hình trước khi bật. Tệp đã cách ly và nhật ký vẫn được giữ.",
                             "Windows will return to the configuration from before protection was turned on. Quarantined files and logs are kept.");
                }
            }
            public static string ProtectOffOk { get { return T("Tắt bảo vệ", "Turn off protection"); } }
            public static string Keep { get { return T("Giữ nguyên", "Keep"); } }
            public static string CloseRunningTitle { get { return T("Đang kiểm tra", "Scan in progress"); } }
            public static string CloseRunningBody { get { return T("ShieldGate đang kiểm tra. Huỷ lần kiểm tra này và thoát?", "ShieldGate is scanning. Cancel this scan and exit?"); } }
            public static string CloseRunningOk { get { return T("Huỷ và thoát", "Cancel and exit"); } }
            public static string Stay { get { return T("Ở lại", "Stay"); } }
            public static string UndoTitle(int files) { return T("Hoàn tác lần chạy này?", "Undo this run?"); }
            public static string UndoBody(int files)
            {
                return T(files + " tệp sẽ trở về vị trí cũ. Các thay đổi cấu hình sẽ được phục hồi.",
                         files + " files will return to their original locations. Configuration changes will be restored.");
            }
            public static string UndoOk { get { return T("Hoàn tác", "Undo"); } }
            public static string Close { get { return T("Đóng", "Close"); } }
            public static string RestoreWarnTitle { get { return T("Khôi phục tệp đã cách ly?", "Restore quarantined files?"); } }
            public static string RestoreWarnBody
            {
                get
                {
                    return T("Tệp này đã bị cách ly vì có dấu hiệu độc hại. Chỉ khôi phục nếu bạn chắc đó là tệp sạch.",
                             "This file was quarantined because it looked malicious. Only restore it if you are sure it is safe.");
                }
            }
            public static string RestoreOk { get { return T("Khôi phục", "Restore"); } }
        }

        public static class Tech
        {
            public static string Autorun
            {
                get
                {
                    return "HKLM\\…\\Policies\\Explorer: NoDriveTypeAutoRun = 255, NoAutorun = 1\r\n" +
                           "HKLM\\…\\Explorer\\AutoplayHandlers: DisableAutoplay = 1";
                }
            }
            public static string Ini { get { return "HKLM\\…\\IniFileMapping\\Autorun.inf = @SYS:DoesNotExist"; } }
            public static string Wsh { get { return "HKLM\\…\\Windows Script Host\\Settings: Enabled = 0"; } }
            public static string Guard
            {
                get
                {
                    return T("Tác vụ lập lịch “ShieldGate USB Guard”, chạy bằng SYSTEM khi khởi động",
                             "Scheduled task “ShieldGate USB Guard”, runs as SYSTEM at startup");
                }
            }
        }

        // Chỉ dùng cho phần tự kiểm tra trên dòng lệnh.
        public static class Selftest
        {
            public static string FakeUsb { get { return T("tạo được USB giả và shortcut thử", "create a fake USB drive and test shortcuts"); } }
            public static string Detected { get { return T("phát hiện đúng 3 mục độc hại", "detect exactly 3 malicious items"); } }
            public static string NoFalsePositive { get { return T("shortcut sạch không bị báo nhầm", "clean shortcut is not reported"); } }
            public static string Cancelled { get { return T("huỷ được lần kiểm tra", "scan can be cancelled"); } }
            public static string Quarantined { get { return T("cách ly 3 tệp, tệp rời khỏi USB", "quarantine 3 files and remove them from the drive"); } }
            public static string Batch { get { return T("vùng cách ly ghi nhận lần chạy", "quarantine records the batch"); } }
            public static string Restored { get { return T("khôi phục tệp về đúng chỗ", "restore files to their original locations"); } }
            public static string BatchRemoved { get { return T("xoá lần cách ly sau khi khôi phục", "remove the batch after restoring"); } }
            public static string Unhidden { get { return T("bỏ thuộc tính ẩn cho tệp và thư mục", "unhide files and folders"); } }
            public static string SettingsSaved { get { return T("thiết lập và nhật ký ghi đúng chỗ", "settings and log are written to the data folder"); } }
        }

        // Chỉ dùng cho dòng lệnh, không hiện trong cửa sổ.
        public static class Cli
        {
            public static string AdminRequired
            {
                get
                {
                    return T("Cần quyền quản trị. Chuột phải tệp rồi chọn Run as administrator.",
                             "Administrator rights are required. Right-click the file and choose Run as administrator.");
                }
            }
            public static string SnapshotSaved { get { return T("Đã sao lưu cấu hình hiện tại.", "Current configuration backed up."); } }
            public static string PolicyApplied { get { return T("Đã tắt AutoRun và chặn tệp autorun.inf.", "AutoRun disabled and autorun.inf blocked."); } }
            public static string PolicyPartial { get { return T("Chỉ áp dụng được một phần. Kiểm tra quyền quản trị.", "Only partially applied. Check administrator rights."); } }
            public static string TaskCreated { get { return T("Đã tạo tác vụ bảo vệ chạy khi khởi động.", "Protection task created to run at startup."); } }
            public static string TaskFailed { get { return T("Không tạo được tác vụ bảo vệ.", "Could not create the protection task."); } }
            public static string GuardStarted { get { return T("Bảo vệ nền đang chạy.", "Background protection is running."); } }
            public static string GuardStarting { get { return T("Đã cài nhưng bảo vệ nền chưa gửi tín hiệu. Thử lại hoặc khởi động lại máy.", "Installed, but background protection has not reported in. Try again or restart the computer."); } }
            public static string TaskRemoved { get { return T("Đã xoá tác vụ bảo vệ.", "Protection task removed."); } }
            public static string TaskMissing { get { return T("Không có tác vụ bảo vệ để xoá.", "There is no protection task to remove."); } }
            public static string PolicyRestored { get { return T("Đã phục hồi cấu hình trước khi bật.", "Restored the configuration from before protection was turned on."); } }
            public static string PolicyRestoreFailed { get { return T("Không phục hồi được cấu hình. Kiểm tra lại thiết lập AutoRun.", "Could not restore the configuration. Check the AutoRun settings."); } }
            public static string ProtectOffDone { get { return T("Đã tắt bảo vệ. Tệp cách ly và nhật ký vẫn được giữ.", "Protection turned off. Quarantined files and logs are kept."); } }
            public static string NoDriveSelected { get { return T("Chưa chọn ổ đĩa.", "No drive selected."); } }
            public static string StatusTitle { get { return T("Trạng thái ShieldGate", "ShieldGate status"); } }
            public static string StatusProtect(bool on) { return T("Bảo vệ: " + (on ? "đang bật" : "đang tắt"), "Protection: " + (on ? "on" : "off")); }
            public static string StatusAutorun(bool on) { return T("Chặn tự chạy: " + (on ? "bật" : "tắt"), "AutoRun block: " + (on ? "on" : "off")); }
            public static string StatusIni(bool on) { return T("Chặn autorun.inf: " + (on ? "bật" : "tắt"), "autorun.inf block: " + (on ? "on" : "off")); }
            public static string StatusWsh(bool on) { return T("Chặn Windows Script Host: " + (on ? "bật" : "tắt"), "Windows Script Host block: " + (on ? "on" : "off")); }
            public static string StatusGuard(bool on) { return T("Chạy nền: " + (on ? "đang chạy" : "không chạy"), "Background: " + (on ? "running" : "not running")); }
            public static string StatusAdmin(bool on) { return T("Quyền quản trị: " + (on ? "có" : "không"), "Administrator rights: " + (on ? "yes" : "no")); }
            public static string StatusQuarantine(int files, int batches)
            {
                return T("Tệp đang cách ly: " + files + " trong " + batches + " lần",
                         "Files in quarantine: " + files + " in " + batches + " batches");
            }
            public static string ScanUsage { get { return T("Cách dùng: ShieldGate.exe --scan E:\\", "Usage: ShieldGate.exe --scan E:\\"); } }
            public static string Usage
            {
                get
                {
                    return "ShieldGate.exe --watch | --status | --install | --uninstall | --scan <drive> | --selftest";
                }
            }
            public static string TempDir(string path) { return T("Thư mục tạm: " + path, "Temporary folder: " + path); }
            public static string ScanSummary(string drive, int files, int found, double seconds)
            {
                return T(drive + ": xem " + files.ToString("#,0") + " tệp trong " + seconds.ToString("0.0").Replace('.', ',') + " giây, phát hiện " + found + " mục.",
                         drive + ": checked " + files.ToString("#,0") + " files in " + seconds.ToString("0.0") + " seconds, found " + found + " items.");
            }
            public static string ScanItem(string rel, string reason) { return "  " + rel + " — " + reason; }
            public static string ScanClean { get { return T("Không phát hiện gì.", "Nothing found."); } }
            public static string WatchStarted { get { return T("Bảo vệ nền đang chạy. Đóng cửa sổ này để dừng.", "Background protection is running. Close this window to stop."); } }
            public static string UnknownArg { get { return T("Tham số không hợp lệ.", "Invalid argument."); } }
            public static string SelftestTitle { get { return T("Tự kiểm tra", "Self-test"); } }
            public static string SelftestPass(int total) { return T("Đạt " + total + "/" + total + " phép kiểm tra.", "Passed " + total + "/" + total + " checks."); }
            public static string SelftestFail(int passed, int total, string detail)
            {
                return T("Đạt " + passed + "/" + total + ". Lỗi: " + detail, "Passed " + passed + "/" + total + ". Failure: " + detail);
            }
            public static string SelftestStep(string what, bool ok)
            {
                return T((ok ? "  đạt: " : "  lỗi: ") + what, (ok ? "  pass: " : "  fail: ") + what);
            }
            public static string Configured { get { return T("Đã áp dụng thiết lập bảo vệ.", "Protection settings applied."); } }
            public static string ConfigurePartial { get { return T("Một phần thiết lập chưa áp dụng được. Kiểm tra quyền quản trị.", "Some settings could not be applied. Check administrator rights."); } }
            public static string AlreadyRunning { get { return T("ShieldGate đang mở.", "ShieldGate is already open."); } }
            public static string ConsoleTitle { get { return "ShieldGate"; } }
        }
    }
}
