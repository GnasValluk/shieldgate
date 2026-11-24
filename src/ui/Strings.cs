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
            public static string Tagline { get { return T("Bảo vệ các cổng kết nối", "Protect your connection ports"); } }
            public static string WindowTitle { get { return AppInfo.Name; } }
        }

        public static class Nav
        {
            public static string Overview { get { return T("Tổng quan", "Overview"); } }
            public static string Usb { get { return T("Xử lý USB", "USB scan"); } }
            public static string Protect { get { return T("Bảo vệ", "Protection"); } }
            public static string Devices { get { return T("Thiết bị", "Devices"); } }
            public static string Repair { get { return T("Sửa ổ", "Drive repair"); } }
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
        }

        public static class Overview
        {
            public static string Title { get { return T("Tổng quan", "Overview"); } }
            public static string Refresh { get { return T("Kiểm tra lại", "Check again"); } }
            public static string StateSafe { get { return T("Máy tính đang được bảo vệ", "This computer is protected"); } }
            public static string StateAttentionFiles(int n) { return T("Cần chú ý " + n + " tệp", "Needs attention: " + n + " files"); }
            public static string StateScanning { get { return T("Đang kiểm tra máy tính", "Checking this computer"); } }
            public static string StateStopping { get { return T("Đang dừng lần kiểm tra…", "Stopping the check…"); } }
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
            public static string QuarantineAction { get { return T("Quản lý cách ly", "Manage quarantine"); } }
            public static string QuarantineClean { get { return T("Dọn sạch", "Clean up"); } }
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
            public static string ScanFolderButton { get { return T("Kiểm tra thư mục", "Scan folder"); } }
            public static string ScanFolder { get { return T("Quét thư mục…", "Scan a folder…"); } }
            public static string ChooseFolderHint
            {
                get { return T("Chọn thư mục cần kiểm tra (không chọn ổ hệ thống).",
                               "Choose the folder to scan (not the system drive)."); }
            }
            public static string FolderSystemRefused
            {
                get
                {
                    return T("Không xử lý thư mục trên ổ hệ thống. Hãy dùng Kiểm tra máy cho phần này.",
                             "Folders on the system drive are not processed. Use the computer check for that.");
                }
            }
            public static string FakeFolderKeptWarning(int n)
            {
                return T("Còn " + n + " tệp dữ liệu trong thư mục giả (đã giữ lại, không cách ly).",
                         n + " user files remain in the fake folder (kept, not quarantined).");
            }
            public static string RenameBase64(int n)
            {
                return T("Khôi phục tên gốc (" + n + ")", "Restore original names (" + n + ")");
            }
            public static string RenameBase64Hint
            {
                get
                {
                    return T("Đổi tên tài liệu bị virus mã hoá Base64 về tên gốc. Không ghi đè tệp đã có.",
                             "Rename documents the virus encoded as Base64 back to their original names. Existing files are never overwritten.");
                }
            }
            public static string RenameDone(int done, int skipped)
            {
                return T("Đã khôi phục tên cho " + done + " tệp."
                         + (skipped > 0 ? " Bỏ qua " + skipped + " tệp vì tên đích đã tồn tại." : ""),
                         "Restored names for " + done + " files."
                         + (skipped > 0 ? " Skipped " + skipped + " because the target name already exists." : ""));
            }
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
            public static string Unhide { get { return T("Hiện tệp bị ẩn", "Unhide files"); } }
            public static string UnhideHint
            {
                get
                {
                    return T("Chuyển dữ liệu ra khỏi thư mục không tên và bỏ thuộc tính ẩn cho mọi tệp, thư mục trên ổ. Dùng khi virus đã giấu dữ liệu của bạn.",
                             "Move data out of nameless folders and clear the hidden attribute on every file and folder on the drive. Use this when a virus hid your data.");
                }
            }
            public static string Unhidden(int n)
            {
                return T("Đã bỏ thuộc tính ẩn cho " + n + " mục. Thao tác này không hoàn tác tự động.",
                         "Unhid " + n + " items. This action is not undone automatically.");
            }
            public static string NamelessLabel(string codes)
            {
                return T("[Thư mục không tên " + codes + "]", "[Nameless folder " + codes + "]");
            }
            public static string UnhiddenNone { get { return T("Không có mục nào đang bị ẩn.", "No hidden items were found."); } }
            public static string Truncated(int files)
            {
                return T("Đã xem " + files.ToString("#,0") + " tệp và dừng ở giới hạn an toàn. Một phần ổ chưa được kiểm tra.",
                         "Scanned " + files.ToString("#,0") + " files and stopped at the safety limit. Part of the drive was not checked.");
            }
            public static string RootExeWarning(int n)
            {
                return T("Có " + n + " tệp chạy bị ẩn ở gốc ổ. Cách ly tệp đó rồi kiểm tra lại USB trên máy sạch.",
                         n + " hidden executable(s) sit at the drive root. Quarantine them and check the drive on a clean computer.");
            }
            public static string ReadOnlyWarning(string drive)
            {
                return T("Ổ " + drive + " đang ở chế độ chỉ đọc. Vẫn kiểm tra được nhưng không cách ly được; bỏ khoá ghi trên USB rồi kiểm tra lại.",
                         "Drive " + drive + " is read-only. It can be scanned but files cannot be quarantined; remove the write lock and scan again.");
            }
            public static string NothingChanged
            {
                get
                {
                    return T("Không có gì để thay đổi: các tệp đã được xử lý trước đó hoặc không còn ở đó.",
                             "Nothing to change: the files were already handled or are no longer there.");
                }
            }
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
            public static string ShowFlagged(int n)
            {
                return T("Hiện " + n + " mục chỉ gắn cờ", "Show " + n + " flagged items");
            }
            public static string HideFlagged { get { return T("Ẩn mục chỉ gắn cờ", "Hide flagged items"); } }
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
                if (code.StartsWith("args-relative-exe=", StringComparison.Ordinal)) return RelativeExe(code.Substring(18));
                if (code.StartsWith("args-drive-exe=", StringComparison.Ordinal)) return DriveExeArg(code.Substring(15));
                if (code == "target-missing") return TargetMissing;
                if (code == "target-on-drive") return TargetOnDrive;
                if (code == "target-odd-name") return TargetOddName;
                if (code == "target-in-hidden-folder") return TargetInHidden;
                if (code == "target-in-fake-folder") return TargetInFake;
                if (code == "named-after-drive") return NamedAfterDrive;
                if (code == "unreadable-hidden") return UnreadableHidden;
                if (code == "shadows-hidden-folder") return ShadowsHiddenFolder;
                if (code.StartsWith("markers=", StringComparison.Ordinal)) return ScriptMarkers;
                if (code == "encoded-vbscript") return EncodedVbs;
                if (code == "autorun-payload") return AutorunPayload;
                if (code == "hidden-root-exe") return HiddenRootExe;
                if (code == "nameless-folder") return NamelessFolder;
                if (code == "fake-folder-exe") return FakeFolderExe;
                if (code == "fake-folder-dir") return FakeFolderDir;
                if (code == "lookalike-folder") return LookalikeFolder;
                if (code == "desktop-ini-clsid") return DesktopIniClsid;
                if (code == "fake-folder-clsid") return FakeFolderClsid;
                if (code == "payload-bundle") return PayloadBundle;
                if (code.StartsWith("double-extension=", StringComparison.Ordinal)) return DoubleExtension(code.Substring(17));
                if (code.StartsWith("rlo-name=", StringComparison.Ordinal)) return RloName;
                if (code.StartsWith("root-script=", StringComparison.Ordinal)) return RootScript;
                if (code.StartsWith("hidden-root-script=", StringComparison.Ordinal)) return HiddenRootScript;
                if (code.StartsWith("ntlm-leak=", StringComparison.Ordinal)) return NtlmLeak;
                if (code.StartsWith("root-desktop-ini=", StringComparison.Ordinal)) return RootDesktopIni;
                if (code.StartsWith("alternate-data-stream=", StringComparison.Ordinal)) return AlternateDataStream(code.Substring(22));
                if (code.StartsWith("hidden-ads=", StringComparison.Ordinal)) return HiddenAds(code.Substring(11));
                if (code.StartsWith("sideload-dll=", StringComparison.Ordinal)) return SideloadDll(code.Substring(13));
                if (code.StartsWith("office-macro=", StringComparison.Ordinal)) return OfficeMacro(code.Substring(13));
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
            public static string NamelessFolder { get { return T("Thư mục không tên: virus giấu dữ liệu của bạn ở đây. Dữ liệu sẽ được chuyển ra gốc ổ rồi thư mục này bị xoá.", "Nameless folder: the virus hides your data here. The data will be moved to the drive root and this folder removed."); } }
            public static string FakeFolderDir { get { return T("Thư mục giả mạo tên thư mục thông dụng (vd \"Docusment\" thay cho \"Documents\"). Nội dung sẽ được cách ly, khôi phục được.", "Fake folder with a misspelled common folder name (e.g. \"Docusment\" instead of \"Documents\"). Its contents will be quarantined and can be restored."); } }
            public static string FakeFolderExe { get { return T("Tệp chạy mang tên thư mục (bấm vào là chạy virus), không phải thư mục thật.", "An executable posing as a folder (opening it runs the virus), not a real folder."); } }
            public static string HiddenRootExe { get { return T("Tệp chạy bị ẩn ở gốc ổ — vị trí virus thường dùng.", "A hidden executable at the drive root — a location viruses often use."); } }
            public static string RelativeExe(string payload) { return T("Shortcut chạy tệp bằng đường dẫn tương đối: " + payload, "Shortcut runs a file by a relative path: " + payload); }
            public static string DriveExeArg(string payload) { return T("Shortcut chạy tệp nằm trên chính ổ này: " + payload, "Shortcut runs a file on this drive: " + payload); }
            public static string TargetOnDrive { get { return T("Shortcut trỏ tới tệp trên chính ổ này.", "Shortcut points to a file on this drive."); } }
            public static string TargetOddName { get { return T("Shortcut trỏ tới tệp chạy có tên dạng số (1.exe, 2.exe…).", "Shortcut points to an executable with a numeric name (1.exe, 2.exe…)."); } }
            public static string TargetInHidden { get { return T("Shortcut trỏ vào một thư mục đang bị ẩn.", "Shortcut points into a hidden folder."); } }
            public static string TargetInFake { get { return T("Shortcut trỏ vào thư mục giả mạo của virus.", "Shortcut points into the virus's fake folder."); } }
            public static string NamedAfterDrive { get { return T("Shortcut mang đúng tên (nhãn) của ổ đĩa — chiêu cũ để bạn tưởng là biểu tượng ổ.", "Shortcut is named exactly like the drive label — an old trick to look like the drive icon."); } }
            public static string LookalikeFolder { get { return T("Tên thư mục gõ sai gần giống thư mục thông dụng (vd \"Docusment\"). Kiểm tra kỹ trước khi cách ly.", "Folder name is a near-miss of a common folder name (e.g. \"Docusment\"). Check before quarantining."); } }
            public static string DesktopIniClsid { get { return T("Thư mục có desktop.ini đặt CLSID để giả dạng thư mục hệ thống.", "Folder has a desktop.ini CLSID that makes it pose as a system folder."); } }
            public static string FakeFolderClsid { get { return T("Thư mục giả dạng thư mục hệ thống bằng desktop.ini và có tên gõ sai. Nội dung sẽ được cách ly.", "Folder poses as a system folder via desktop.ini and has a misspelled name. Its contents will be quarantined."); } }
            public static string PayloadBundle { get { return T("Thư mục chứa đúng bộ tệp của virus shortcut (1.exe, opera_elf.dll, adback.dat, link.dat, icudtl.dat). Nội dung sẽ được cách ly.", "Folder contains the shortcut virus payload set (1.exe, opera_elf.dll, adback.dat, link.dat, icudtl.dat). Its contents will be quarantined."); } }

            // ---------------------------------------------------------- 2.0
            public static string DoubleExtension(string name)
            {
                return T("Tên tệp có hai đuôi (" + name + "): đuôi đầu đánh lừa, đuôi cuối là tệp chạy.",
                         "File has a double extension (" + name + "): the first looks harmless, the last one is executable.");
            }
            public static string RloName { get { return T("Tên tệp chứa ký tự đảo chiều U+202E để hiển thị sai đuôi thật.", "File name contains the U+202E right-to-left override so the real extension looks different."); } }
            public static string RootScript { get { return T("Script nằm ngay gốc ổ: bấm vào là chạy, vị trí virus hay dùng.", "Script sits at the drive root: one click runs it, a common virus location."); } }
            public static string HiddenRootScript { get { return T("Script bị ẩn ở gốc ổ — dấu hiệu mạnh của mã độc lây qua USB.", "A hidden script at the drive root — a strong sign of USB-borne malware."); } }
            public static string NtlmLeak { get { return T("Tệp trỏ biểu tượng về đường dẫn mạng \\\\... có thể làm lộ hash đăng nhập Windows (NTLM).", "File points its icon to a network path \\\\... which can leak the Windows login hash (NTLM)."); } }
            public static string RootDesktopIni { get { return T("desktop.ini ở gốc ổ đặt CLSID để thay đổi cách Windows hiển thị cả ổ.", "desktop.ini at the drive root sets a CLSID that changes how Windows displays the whole drive."); } }
            public static string AlternateDataStream(string stream)
            {
                return T("Tệp có luồng dữ liệu ẩn (:" + stream + ") — nơi giấu nội dung ngoài phần nhìn thấy.",
                         "File has a hidden data stream (:" + stream + ") — a place to stash content outside the visible file.");
            }
            public static string HiddenAds(string stream)
            {
                return T("Luồng ẩn (:" + stream + ") trên tệp ẩn hoặc mang tên tệp chạy — rất đáng ngờ.",
                         "Hidden stream (:" + stream + ") on a hidden file or one with an executable name — highly suspicious.");
            }
            public static string SideloadDll(string name)
            {
                return T("DLL " + name + " không có chữ ký nằm cạnh EXE có chữ ký — kiểu DLL sideloading. Chỉ gắn cờ.",
                         "Unsigned DLL " + name + " sits beside a signed EXE — a DLL sideloading pattern. Flagged only.");
            }
            public static string OfficeMacro(string name)
            {
                return T("Tài liệu Office có macro (" + name + "). Chỉ gắn cờ, không cách ly; chỉ mở nếu bạn tin nguồn.",
                         "Office document contains macros (" + name + "). Flagged only, not quarantined; open it only if you trust the source.");
            }
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
            public static string SwitchRegistry { get { return T("Bảo vệ Registry của File Explorer", "Protect File Explorer registry keys"); } }
            public static string SwitchRegistryDetail
            {
                get
                {
                    return T("Tự sửa khoá mở thư mục, mở .exe và hiện tệp ẩn nếu virus phá hoại. Tắt chỉ ngừng kiểm tra.",
                             "Repair folder, .exe and hidden-file keys if a virus damages them. Turning off only stops checking.");
                }
            }
            public static string SwitchWriteProtect { get { return T("Chặn ghi ổ USB", "Block writes to USB drives"); } }
            public static string SwitchWriteProtectDetail
            {
                get
                {
                    return T("Bật khoá WriteProtect toàn máy cho ổ nhớ USB. Tắt sẽ trả lại giá trị cũ. Có thể ảnh hưởng ổ gắn ngoài.",
                             "Enable the machine-wide WriteProtect policy for USB storage. Turning it off restores the previous value. May affect external drives.");
                }
            }
            public static string SwitchBlockExec { get { return T("Chặn chạy tệp từ ổ rời", "Block programs running from removable drives"); } }
            public static string SwitchBlockExecDetail
            {
                get
                {
                    return T("Theo dõi tiến trình đang chạy từ ổ rời và dừng tiến trình đó. Đây là lớp theo thời điểm, không phải chặn tuyệt đối.",
                             "Watch processes running from removable drives and stop them. This is a point-in-time layer, not an absolute block.");
                }
            }
            public static string BadUsbSwitch { get { return T("Cảnh báo BadUSB", "Warn about BadUSB"); } }
            public static string BadUsbSwitchDetail
            {
                get
                {
                    return T("Khi biểu tượng khay chạy: cảnh báo nếu bàn phím mới gõ rất nhanh rồi mở cmd/PowerShell từ Explorer. Chỉ đếm sự kiện, không đọc phím; chỉ cảnh báo, không tắt thiết bị.",
                             "While the tray icon runs: warn if a new keyboard types very fast and then opens cmd/PowerShell from Explorer. Counts events only, never key codes; warning only, never disables a device.");
                }
            }
            public static string VtTitle { get { return T("VirusTotal (tuỳ chọn)", "VirusTotal (optional)"); } }
            public static string VtSwitch { get { return T("Tra cứu VirusTotal", "Look up VirusTotal"); } }
            public static string VtSwitchDetail
            {
                get
                {
                    return T("Chỉ gửi SHA-256 của tệp khi bạn bấm tra cứu. Không gửi tên tệp, đường dẫn hay nội dung. Mặc định tắt.",
                             "Sends only the file's SHA-256 when you ask for a lookup. No file names, paths or contents. Off by default.");
                }
            }
            public static string VtKeyLabel { get { return T("Khoá API VirusTotal", "VirusTotal API key"); } }
            public static string VtKeySaved
            {
                get { return T("Đã lưu khoá, mã hoá bằng DPAPI của tài khoản Windows này.", "Key saved, encrypted with DPAPI for this Windows account."); }
            }
            public static string VtNoKey { get { return T("Chưa có khoá. Tra cứu sẽ không chạy.", "No key saved. Lookups will not run."); } }
            public static string VtSave { get { return T("Lưu khoá", "Save key"); } }
            public static string VtClear { get { return T("Xoá khoá", "Remove key"); } }
            public static string VtSaved { get { return T("Đã lưu khoá.", "Key saved."); } }
            public static string VtBadKey { get { return T("Khoá phải là 64 ký tự hex.", "The key must be 64 hex characters."); } }
            public static string VtSaveFailed { get { return T("Không lưu được khoá.", "Could not save the key."); } }
            public static string VtCleared { get { return T("Đã xoá khoá.", "Key removed."); } }
            public static string VtEnabled
            {
                get { return T("Đã bật tra cứu. Kết quả chỉ để tham khảo.", "Lookup is on. Results are advisory only."); }
            }
            public static string VtDisabled
            {
                get { return T("Đã tắt tra cứu; không có dữ liệu nào được gửi.", "Lookup is off; nothing is sent."); }
            }
            public static string VtOnNoKey
            {
                get { return T("Đã bật nhưng chưa có khoá; chưa có gì được gửi.", "Enabled, but no key yet; nothing is sent."); }
            }
            public static string On { get { return T("Bật bảo vệ", "Turn on protection"); } }
            public static string Off { get { return T("Tắt bảo vệ", "Turn off protection"); } }
            public static string Tech { get { return T("Chi tiết kỹ thuật", "Technical details"); } }
            public static string BackupNote
            {
                get
                {
                    return T("Thay đổi được sao lưu. Tắt bảo vệ sẽ trả Windows về đúng cấu hình trước khi bật; riêng Registry đã sửa thì giữ nguyên.",
                             "Changes are backed up. Turning protection off restores the previous Windows configuration; repaired registry keys are kept.");
                }
            }
            public static string AdminOnly { get { return T("Cần quyền quản trị để bật bảo vệ.", "Administrator rights are required to turn on protection."); } }
            public static string Reload { get { return T("Chạy lại với quyền quản trị", "Restart as administrator"); } }
            public static string Applying { get { return T("Đang áp dụng thiết lập…", "Applying settings…"); } }
            public static string TechHide { get { return T("Ẩn chi tiết kỹ thuật", "Hide technical details"); } }
        }

        public static class Devices
        {
            public static string Title { get { return T("Thiết bị ngoại vi", "Peripheral devices"); } }
            public static string Intro
            {
                get
                {
                    return T("Danh sách ổ, bàn phím, chuột, điện thoại và Bluetooth đang kết nối. Đánh dấu tin cậy để biết thiết bị quen; chặn chỉ là cảnh báo, không tự tắt thiết bị.",
                             "Drives, keyboards, mice, phones and Bluetooth devices currently connected. Mark a device as trusted to recognise it; blocking only warns, it never disables the device.");
                }
            }
            public static string Refresh { get { return T("Quét lại", "Scan again"); } }
            public static string Scanning { get { return T("Đang kiểm tra thiết bị…", "Checking devices…"); } }
            public static string ConnectedTitle { get { return T("Thiết bị đang kết nối", "Connected devices"); } }
            public static string CompositeChip { get { return T("Nghi composite", "Composite?"); } }
            public static string Empty { get { return T("Chưa thấy thiết bị ngoại vi nào.", "No peripheral device found."); } }
            public static string ColumnName { get { return T("Tên", "Name"); } }
            public static string ColumnType { get { return T("Loại", "Type"); } }
            public static string ColumnDrives { get { return T("Ổ", "Drive"); } }
            public static string ColumnTrust { get { return T("Tin cậy", "Trust"); } }
            public static string TypeStorage { get { return T("Ổ nhớ", "Storage"); } }
            public static string TypeKeyboard { get { return T("Bàn phím", "Keyboard"); } }
            public static string TypeMouse { get { return T("Chuột", "Mouse"); } }
            public static string TypeHid { get { return T("HID", "HID"); } }
            public static string TypeBluetooth { get { return T("Bluetooth", "Bluetooth"); } }
            public static string TypePhone { get { return T("Điện thoại", "Phone"); } }
            public static string TypeNetwork { get { return T("Mạng", "Network"); } }
            public static string TypeOther { get { return T("Khác", "Other"); } }
            public static string Trusted { get { return T("Tin cậy", "Trusted"); } }
            public static string Unknown { get { return T("Chưa rõ", "Unknown"); } }
            public static string Blocked { get { return T("Đã chặn", "Blocked"); } }
            public static string TrustName(DeviceTrust trust)
            {
                if (trust == DeviceTrust.Trusted) return Trusted;
                if (trust == DeviceTrust.Blocked) return Blocked;
                return Unknown;
            }
            public static string TypeName(string cls)
            {
                if (cls == "storage") return TypeStorage;
                if (cls == "keyboard") return TypeKeyboard;
                if (cls == "mouse") return TypeMouse;
                if (cls == "hid") return TypeHid;
                if (cls == "bluetooth") return TypeBluetooth;
                if (cls == "mtp") return TypePhone;
                if (cls == "network") return TypeNetwork;
                return TypeOther;
            }
            public static string MarkTrusted { get { return T("Tin cậy", "Trust"); } }
            public static string MarkUnknown { get { return T("Bỏ đánh dấu", "Clear mark"); } }
            public static string MarkBlocked { get { return T("Chặn", "Block"); } }
            public static string Forget { get { return T("Quên thiết bị", "Forget device"); } }
            public static string NoSelection { get { return T("Chọn một thiết bị trong danh sách.", "Select a device in the list."); } }
            public static string TrustedDone(string name) { return T("Đã đánh dấu tin cậy: " + name, "Marked as trusted: " + name); }
            public static string UnknownDone(string name) { return T("Đã bỏ đánh dấu: " + name, "Mark cleared: " + name); }
            public static string BlockedDone(string name) { return T("Đã đánh dấu chặn: " + name, "Marked as blocked: " + name); }
            public static string ForgottenDone(string name) { return T("Đã quên thiết bị: " + name, "Device forgotten: " + name); }
            public static string ActionFailed { get { return T("Không ghi được trạng thái thiết bị. Kiểm tra quyền và nhật ký.", "Could not save the device state. Check permissions and the log."); } }
            public static string BlockedNotice(string name)
            {
                return T("Cảnh báo: thiết bị đã chặn đang kết nối — " + name,
                         "Warning: a blocked device is connected — " + name);
            }
            public static string CompositeNotice(string name)
            {
                return T("Cùng một thiết bị vừa có ổ nhớ vừa có bàn phím (dấu hiệu composite/BadUSB): " + name,
                         "One device exposes both storage and a keyboard (possible composite/BadUSB): " + name);
            }
            public static string NewNotice(string name)
            {
                return T("Thiết bị mới: " + name, "New device: " + name);
            }

            public static string EventsTitle { get { return T("Dòng thời gian", "Timeline"); } }
            public static string EventsEmpty { get { return T("Chưa có sự kiện nào.", "No events yet."); } }
            public static string ExportCsv { get { return T("Xuất CSV", "Export CSV"); } }
            public static string ExportHtml { get { return T("Xuất HTML", "Export HTML"); } }
            public static string Exported(string path) { return T("Đã xuất: " + path, "Exported: " + path); }
            public static string ExportFailed { get { return T("Không xuất được tệp sự kiện.", "Could not export the event file."); } }
            public static string ColumnTime { get { return T("Thời gian", "Time"); } }
            public static string ColumnKind { get { return T("Loại", "Kind"); } }
            public static string ColumnLevel { get { return T("Mức", "Level"); } }
            public static string ColumnSubject { get { return T("Đối tượng", "Subject"); } }
            public static string ColumnDetail { get { return T("Chi tiết", "Detail"); } }
            public static string Count(int n) { return T(n + " thiết bị", n + " devices"); }
            // Dải thông báo luôn có chữ: tránh khoảng trống lớn giữa tiêu đề và danh sách.
            public static string AllClear
            {
                get { return T("Chưa thấy dấu hiệu bất thường ở các thiết bị đang kết nối.",
                             "No unusual sign on the connected devices."); }
            }
            public static string EventCount(int n) { return T(n + " sự kiện gần nhất", n + " most recent events"); }
            public static string AdminHint { get { return T("Cần quyền quản trị để đọc đầy đủ thông tin thiết bị.", "Administrator rights are needed to read full device details."); } }
            public static string DisabledHint { get { return T("Chặn không tắt thiết bị. Muốn tắt, dùng Device Manager của Windows.", "Blocking does not disable a device. To disable it, use Windows Device Manager."); } }
        }

        public static class Repair
        {
            public static string Title { get { return T("Sửa ổ", "Drive repair"); } }
            public static string Intro
            {
                get
                {
                    return T("Chẩn đoán, đọc thử và sửa lỗi ổ rời. Mọi thao tác ghi đều từ chối ổ hệ thống; sửa bằng chkdsk cần sao lưu trước và xác nhận hai lần.",
                             "Diagnose, read-test and repair removable drives. Every write refuses the system drive; chkdsk repair needs a backup first and two confirmations.");
                }
            }
            public static string ChooseDrive { get { return T("Chọn ổ", "Choose drive"); } }
            public static string Refresh { get { return T("Làm mới", "Refresh"); } }
            public static string NoDrives { get { return T("Không có ổ rời nào.", "No removable drive."); } }
            public static string EmptyHint { get { return T("Chọn ổ rồi bấm Chẩn đoán để xem thông tin.", "Choose a drive, then run Diagnose to see details."); } }
            public static string DetailsTitle { get { return T("Thông tin ổ", "Drive details"); } }
            public static string GroupCheck { get { return T("Kiểm tra", "Check"); } }
            public static string GroupBackup { get { return T("Sao lưu", "Backup"); } }
            public static string GroupRepair { get { return T("Sửa chữa", "Repair"); } }
            public static string DiagnoseHint { get { return T("Đọc thông tin ổ, không ghi gì lên ổ.", "Read drive details without writing to it."); } }
            public static string ReadTestHint { get { return T("Đọc thử vài tệp trong giới hạn thời gian, không sửa tệp.", "Read a sample within a time limit; no file is changed."); } }
            public static string BootHint { get { return T("Chỉ đọc 512 byte đầu tiên của ổ.", "Read only the first 512 bytes of the drive."); } }
            public static string BackupHint { get { return T("Sao chép dữ liệu vào thư mục sửa chữa của ShieldGate.", "Copy data into ShieldGate's repair folder."); } }
            public static string OpenFolderHint { get { return T("Mở thư mục chứa các bản sao lưu.", "Open the folder that holds backups."); } }
            public static string ChkdskReadHint { get { return T("Kiểm tra hệ tệp, không sửa gì.", "Check the file system without changing it."); } }
            public static string ChkdskFixHint { get { return T("Sửa lỗi hệ tệp. Cần sao lưu trước và xác nhận hai lần.", "Repair the file system. A backup and two confirmations are required."); } }
            public static string DefenderHint { get { return T("Quét ổ bằng Windows Defender (MpCmdRun).", "Scan the drive with Windows Defender (MpCmdRun)."); } }
            public static string PreviewHint { get { return T("Đếm tệp và dung lượng sẽ sao lưu, không ghi gì.", "Count the files and size a backup would copy; nothing is written."); } }
            // Gợi ý khi rê chuột: dòng đầu là tên chức năng, dòng sau là giải thích.
            public static string HoverHint(string name, string hint) { return name + "\r\n" + hint; }
            public static string IconHint { get { return T("Rê chuột để xem tên", "Hover for names"); } }
            public static string NoProblem { get { return T("Không phát hiện vấn đề trên ổ.", "No problem detected on the drive."); } }
            public static string OutputEmpty { get { return T("Chưa có kết quả. Các thao tác sẽ ghi vào đây.", "No output yet. Actions will write here."); } }
            public static string ClearOutput { get { return T("Xoá kết quả", "Clear output"); } }
            public static string DefenderShort { get { return T("Quét Defender", "Defender scan"); } }
            public static string Diagnose { get { return T("Chẩn đoán", "Diagnose"); } }
            public static string Diagnosing { get { return T("Đang chẩn đoán…", "Diagnosing…"); } }
            public static string ReadTest { get { return T("Đọc thử", "Read test"); } }
            public static string ReadTesting { get { return T("Đang đọc thử (có giới hạn thời gian)…", "Read test running (time limited)…"); } }
            public static string Backup { get { return T("Sao lưu", "Back up"); } }
            public static string Preview { get { return T("Xem trước sao lưu", "Preview backup"); } }
            public static string PreviewFirst { get { return T("Hãy xem trước sao lưu để biết số tệp và dung lượng trước khi chạy.", "Preview the backup first to see the file count and size before running it."); } }
            public static string BackingUp { get { return T("Đang sao lưu…", "Backing up…"); } }
            public static string ChkdskRead { get { return T("Kiểm tra ổ (chỉ đọc)", "Check drive (read-only)"); } }
            public static string ChkdskFix { get { return T("Sửa lỗi ổ (chkdsk /f)", "Repair drive (chkdsk /f)"); } }
            public static string BootSector { get { return T("Xem boot sector", "Read boot sector"); } }
            public static string OpenFolder { get { return T("Mở thư mục sửa chữa", "Open repair folder"); } }
            public static string Defender { get { return T("Quét bằng Windows Defender", "Scan with Windows Defender"); } }
            public static string DefenderRunning { get { return T("Đang quét bằng Windows Defender…", "Windows Defender scan running…"); } }
            public static string DefenderMissing { get { return T("Không tìm thấy Windows Defender (MpCmdRun.exe) trên máy này.", "Windows Defender (MpCmdRun.exe) was not found on this computer."); } }
            public static string NoPath { get { return T("Chưa chọn đường dẫn để quét.", "No path selected to scan."); } }
            public static string DefenderExit(int code) { return T("Windows Defender trả mã " + code + ".", "Windows Defender returned code " + code + "."); }
            public static string DefenderClean { get { return T("Windows Defender: không phát hiện gì.", "Windows Defender: nothing found."); } }
            public static string DefenderFound { get { return T("Windows Defender phát hiện mối nguy. Xem lịch sử bảo vệ của Windows.", "Windows Defender found a threat. Check Windows protection history."); } }
            public static string NoDrive { get { return T("Chưa chọn ổ.", "No drive selected."); } }
            public static string InvalidDrive { get { return T("Đường dẫn ổ không hợp lệ.", "Invalid drive path."); } }
            public static string SystemDrive { get { return T("Từ chối: đây là ổ hệ thống. Không bao giờ ghi lên ổ hệ thống.", "Refused: this is the system drive. ShieldGate never writes to the system drive."); } }
            public static string NotReady { get { return T("Ổ chưa sẵn sàng hoặc đã rút.", "The drive is not ready or was removed."); } }
            public static string UnsupportedDrive { get { return T("Không hỗ trợ ổ mạng hoặc ổ quang.", "Network and optical drives are not supported."); } }
            public static string BackupRequired { get { return T("Phải sao lưu ổ trước khi chạy chkdsk /f.", "Back up the drive before running chkdsk /f."); } }
            public static string NotRemovable { get { return T("Chỉ sao lưu và sửa ổ rời (USB). Ổ cố định chỉ được chẩn đoán đọc.", "Only removable drives can be backed up or repaired. Fixed drives are read-only diagnosis."); } }
            public static string BackupOtherVolume { get { return T("Bản sao lưu thuộc ổ khác (serial không khớp). Hãy sao lưu lại ổ này.", "The backup belongs to another volume (serial mismatch). Back up this drive again."); } }
            public static string BackupExpired { get { return T("Bản sao lưu đã quá 24 giờ. Hãy sao lưu lại trước khi sửa.", "The backup is older than 24 hours. Back up again before repairing."); } }
            public static string BackupMissing { get { return T("Không tìm thấy thư mục sao lưu. Hãy sao lưu lại.", "The backup folder is missing. Back up again."); } }
            public static string BackupNoSpace(long need, long free)
            {
                return T("Không đủ chỗ cho bản sao lưu: cần " + Size(need) + ", chỉ còn " + Size(free) + ".",
                         "Not enough space for the backup: " + Size(need) + " needed, " + Size(free) + " free.");
            }
            public static string BackupExit(int code) { return T("robocopy trả mã " + code + ".", "robocopy returned code " + code + "."); }
            public static string BackupOutside { get { return T("Thư mục đích phải nằm trong thư mục sửa chữa của ShieldGate.", "The destination must be inside the ShieldGate repair folder."); } }
            public static string BackupOnSource { get { return T("Không sao lưu ổ vào chính nó.", "Refusing to back up a drive onto itself."); } }
            public static string BackupPreviewDone(long files, long bytes)
            {
                return T("Xem trước: " + files.ToString("#,0") + " tệp · " + Size(bytes),
                         "Preview: " + files.ToString("#,0") + " files · " + Size(bytes));
            }
            public static string BackupDone(string path) { return T("Đã sao lưu vào " + path, "Backed up to " + path); }
            public static string BackupFailed(string detail) { return T("Sao lưu thất bại: " + detail, "Backup failed: " + detail); }
            public static string ChkdskReadRunning { get { return T("Đang chạy chkdsk chế độ chỉ đọc…", "Running chkdsk in read-only mode…"); } }
            public static string ChkdskFixRunning { get { return T("Đang chạy chkdsk /f…", "Running chkdsk /f…"); } }
            public static string ChkdskDone(int code) { return T("chkdsk xong, mã " + code + ".", "chkdsk finished with code " + code + "."); }
            public static string BootReading { get { return T("Đang đọc boot sector (chỉ đọc)…", "Reading boot sector (read-only)…"); } }
            public static string BootReport(string fs, bool signature)
            {
                return T("Hệ tệp nhận ra: " + fs + " · chữ ký 55AA: " + (signature ? "có" : "không"),
                         "Detected file system: " + fs + " · 55AA signature: " + (signature ? "yes" : "no"));
            }
            public static string BootShort(int read) { return T("Chỉ đọc được " + read + "/512 byte.", "Only " + read + "/512 bytes could be read."); }
            public static string BootEmpty { get { return T("Vùng đọc được toàn số 0 (có thể trống hoặc hỏng).", "The read area is all zeroes (empty or damaged)."); } }
            public static string BootUnknown { get { return T("Không nhận ra hệ tệp.", "Unknown file system."); } }
            public static string SystemDriveLabel { get { return T("Ổ hệ thống", "System drive"); } }
            public static string Ready { get { return T("Sẵn sàng", "Ready"); } }
            public static string NotReadyLabel { get { return T("Chưa sẵn sàng", "Not ready"); } }
            public static string ReadOnlyLabel { get { return T("Chỉ đọc", "Read-only"); } }
            public static string WritableLabel { get { return T("Ghi được", "Writable"); } }
            public static string DirtyLabel { get { return T("Có cờ dirty", "Dirty bit set"); } }
            public static string CleanLabel { get { return T("Không có cờ dirty", "No dirty bit"); } }
            public static string DirtyUnknown { get { return T("Không đọc được cờ dirty", "Dirty bit unknown"); } }
            public static string FreeOf(long free, long total)
            {
                return T("Trống " + Size(free) + " / " + Size(total), Size(free) + " free of " + Size(total));
            }
            public static string Info(string label, string value) { return label + ": " + value; }
            public static string InfoDrive { get { return T("Ổ", "Drive"); } }
            public static string InfoLabel { get { return T("Nhãn", "Label"); } }
            public static string InfoFileSystem { get { return T("Hệ tệp", "File system"); } }
            public static string InfoStatus { get { return T("Trạng thái", "Status"); } }
            public static string InfoBoot { get { return T("Boot sector", "Boot sector"); } }
            public static string ProblemLabel { get { return T("Vấn đề", "Problem"); } }
            public static string InfoBackup { get { return T("Sao lưu", "Backup"); } }
            public static string InfoCapacity { get { return T("Dung lượng", "Capacity"); } }
            public static string BootReadFailed(string detail) { return T("Không đọc được boot sector: " + detail, "Could not read the boot sector: " + detail); }
            public static string OutputTitle { get { return T("Kết quả", "Output"); } }
            public static string ReadDone(int files, long bytes, int errors, bool timedOut)
            {
                return T("Đọc " + files + " tệp · " + Size(bytes) + " · lỗi " + errors
                         + (timedOut ? " · dừng theo giới hạn thời gian" : ""),
                         "Read " + files + " files · " + Size(bytes) + " · errors " + errors
                         + (timedOut ? " · stopped at the time limit" : ""));
            }
            public static string Size(long bytes)
            {
                if (bytes < 1024) return bytes + " B";
                double kb = bytes / 1024.0;
                if (kb < 1024) return kb.ToString("0.0") + " KB";
                double mb = kb / 1024.0;
                if (mb < 1024) return mb.ToString("0.0") + " MB";
                return (mb / 1024.0).ToString("0.0") + " GB";
            }
            public static string LastBackup(string path) { return T("Bản sao lưu gần nhất: " + path, "Last backup: " + path); }
            public static string NoBackup { get { return T("Chưa có bản sao lưu cho ổ này.", "No backup for this drive yet."); } }
            public static string RunFinished { get { return T("Xong.", "Finished."); } }
        }

        public static class Restore
        {
            public static string Title { get { return T("Vùng cách ly", "Quarantine"); } }
            public static string Intro { get { return T("Tệp bị cách ly vẫn còn nguyên. Bạn có thể khôi phục hoặc xoá vĩnh viễn.", "Quarantined files are kept intact. You can restore or delete them."); } }
            public static string Empty { get { return T("Chưa có tệp nào bị cách ly.", "No files in quarantine."); } }
            public static string Action { get { return T("Khôi phục lần này", "Restore this batch"); } }
            public static string Clean { get { return T("Xoá vĩnh viễn", "Delete permanently"); } }
            public static string CleanAll { get { return T("Dọn sạch tất cả", "Clean up all"); } }
            public static string CleanDone(int files)
            {
                return T("Đã xoá vĩnh viễn " + files + " tệp khỏi vùng cách ly.",
                         "Permanently deleted " + files + " files from quarantine.");
            }
            public static string CleanAllDone(int batches, int files)
            {
                return T("Đã dọn sạch " + batches + " lần cách ly (" + files + " tệp).",
                         "Cleaned up " + batches + " quarantine batches (" + files + " files).");
            }
            public static string CleanFailed { get { return T("Không xoá được toàn bộ tệp trong vùng cách ly. Xem nhật ký để biết chi tiết.", "Could not delete every quarantined file. See the log for details."); } }
            public static string OpenFolder { get { return T("Mở thư mục cách ly", "Open quarantine folder"); } }
            public static string Done(int restored, int kept)
            {
                return T("Đã khôi phục " + restored + " tệp. " + kept + " tệp đã có sẵn nên được giữ nguyên.",
                         "Restored " + restored + " files. " + kept + " files already existed and were kept.");
            }
            public static string NothingSelected { get { return T("Chọn một lần cách ly trong danh sách.", "Select a quarantine batch."); } }
            public static string Missing(int n) { return T(n + " tệp không còn trong vùng cách ly.", n + " files are no longer in quarantine."); }
            public static string Refused(int n)
            {
                return T(n + " tệp bị từ chối vì đường dẫn không an toàn (xem nhật ký).",
                         n + " files were refused because their path is unsafe (see the log).");
            }
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
            public static string StageServices { get { return T("Đang xem dịch vụ và driver", "Checking services and drivers"); } }
            public static string StageWmi { get { return T("Đang xem đăng ký sự kiện WMI", "Checking WMI event subscriptions"); } }
            public static string StageDefender { get { return T("Đang xem bảo vệ của Windows", "Checking Windows protection"); } }
            public static string StageRegistry { get { return T("Đang xem Registry của File Explorer", "Checking the File Explorer registry"); } }
            public static string Clean { get { return T("Không phát hiện gì bất thường", "Nothing unusual found"); } }
            public static string CleanBody
            {
                get
                {
                    return T("Đã kiểm tra mục khởi động, tác vụ hẹn giờ, tiến trình đang chạy, các ổ đĩa và Registry.",
                             "Checked startup items, scheduled tasks, running processes, drives and the registry.");
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
            public static string DroppedFile(string name) { return T("Tệp chạy đáng ngờ trong thư mục người dùng: " + name, "Suspicious executable in a user folder: " + name); }
            public static string FakeProcess(string name) { return T("Tiến trình mạo danh hệ thống: " + name, "Process impersonating a system process: " + name); }
            public static string StartupFile(string name) { return T("Tệp trong thư mục khởi động: " + name, "File in the startup folder: " + name); }
            public static string Task(string name) { return T("Tác vụ hẹn giờ đáng ngờ: " + name, "Suspicious scheduled task: " + name); }
            public static string Process(string name) { return T("Script đang chạy: " + name, "Script is running: " + name); }
            public static string Service(string name) { return T("Dịch vụ/driver đáng ngờ: " + name, "Suspicious service/driver: " + name); }
            public static string DriveShortcut(string name) { return T("Shortcut đáng ngờ trên ổ đĩa hoặc thư mục người dùng: " + name, "Suspicious shortcut on a drive or user folder: " + name); }
            public static string DriveFakeFolder(string name) { return T("Thư mục giả mạo của virus trên ổ đĩa: " + name, "Virus fake folder on a drive: " + name); }
            public static string DriveHiddenFolder(string name) { return T("Thư mục không tên giấu dữ liệu trên ổ đĩa: " + name, "Nameless folder hiding data on a drive: " + name); }
            public static string DriveExe(string name) { return T("Tệp chạy giả dạng thư mục ở gốc ổ: " + name, "Executable posing as a folder at a drive root: " + name); }
            public static string WmiConsumer(string name) { return T("Đăng ký sự kiện WMI có thể tự chạy mã độc: " + name, "WMI event subscription that can run malicious code: " + name); }
            public static string LnkAssoc(string name) { return T("Kiểu tệp .lnk bị đổi để mọi shortcut chạy mã lạ: " + name, "The .lnk file type was changed so every shortcut runs foreign code: " + name); }
            public static string RegHealth(string name) { return T("Registry bị phá hoại: " + name, "Damaged registry setting: " + name); }
            public static string Defender { get { return T("Bảo vệ thời gian thực của Windows đang tắt", "Windows real-time protection is off"); } }
        }

        // Tên ngắn của từng lỗi Registry, dùng chung cho màn hình quét máy và nhật ký.
        public static class Registry
        {
            public static string ExeAssoc { get { return T("Liên kết tệp .exe bị đổi", "The .exe file association was changed"); } }
            public static string ExeCommand { get { return T("Lệnh mở tệp .exe bị đổi", "The command that opens .exe files was changed"); } }
            public static string FolderCommand { get { return T("Lệnh mở thư mục (Folder) bị đổi", "The command that opens folders was changed"); } }
            public static string FolderDelegate { get { return T("Thiếu mã DelegateExecute khi mở thư mục", "The folder DelegateExecute code is missing"); } }
            public static string ShowHidden { get { return T("Không bật được chế độ hiện tệp ẩn", "The show-hidden-files setting cannot be enabled"); } }
            public static string NoFolderOptions { get { return T("Tuỳ chọn thư mục (Folder Options) bị khoá", "Folder Options is locked"); } }
            public static string NoContextMenu { get { return T("Menu chuột phải bị khoá", "The right-click menu is locked"); } }
            public static string NoTaskMgr { get { return T("Task Manager bị khoá", "Task Manager is locked"); } }
            public static string NoRegistryTools { get { return T("Registry Editor (regedit) bị khoá", "Registry Editor (regedit) is locked"); } }
            public static string Missing { get { return T("(không có)", "(missing)"); } }
        }

        public static class Log
        {
            public static string Title { get { return T("Nhật ký", "Log"); } }
            public static string All { get { return T("Tất cả", "All"); } }
            public static string Warnings { get { return T("Cảnh báo", "Warnings"); } }
            public static string ErrorsOnly { get { return T("Chỉ lỗi", "Errors only"); } }
            public static string Export { get { return T("Xuất nhật ký", "Export log"); } }
            public static string Empty { get { return T("Chưa có hoạt động nào.", "No activity yet."); } }
            public static string NoErrors { get { return T("Không có lỗi nào được ghi.", "No errors were logged."); } }
            public static string NoWarnings { get { return T("Không có cảnh báo nào được ghi.", "No warnings were logged."); } }
            public static string Newer { get { return T("Mới hơn", "Newer"); } }
            public static string Older { get { return T("Cũ hơn", "Older"); } }
            public static string Page(int page, int pages, int lines)
            {
                return T("Trang " + page + "/" + pages + " · " + lines + " dòng",
                         "Page " + page + "/" + pages + " · " + lines + " lines");
            }
            public static string PageEmpty { get { return T("Không có dòng nào", "No lines"); } }
            public static string Refresh { get { return T("Làm mới", "Refresh"); } }
            public static string ExportTitle { get { return T("Lưu nhật ký", "Save log"); } }
            public static string FilterName { get { return T("Tệp văn bản", "Text file"); } }
            public static string Exported(string path) { return T("Đã lưu nhật ký vào " + path + ".", "Log saved to " + path + "."); }
            public static string OpenFolder { get { return T("Mở thư mục nhật ký", "Open log folder"); } }

            // Nội dung ghi vào shieldgate.log, cũng theo ngôn ngữ đang chọn.
            public static string Quarantined(string path, string reason)
            {
                return T("Đã cách ly: " + path + " | " + reason,
                         "Quarantined: " + path + " | " + reason);
            }
            public static string QuarantineChanged(string path)
            {
                return T("Bản cách ly đã thay đổi, không khôi phục: " + path,
                         "Quarantined copy changed, not restoring: " + path);
            }
            public static string Restored(string path)
            {
                return T("Đã khôi phục: " + path, "Restored: " + path);
            }
            public static string ConfigSaveFailed(string detail)
            {
                return T("Không ghi được lựa chọn bảo vệ: " + detail,
                         "Could not save protection choices: " + detail);
            }
            public static string UninstallSkip(string path)
            {
                return T("Không xoá thư mục không có dấu hiệu của ShieldGate: " + path,
                         "Not deleting a folder without ShieldGate's marker: " + path);
            }
            public static string TrayCreateFailed(string detail)
            {
                return T("Không tạo được biểu tượng khay: " + detail,
                         "Could not create the tray icon: " + detail);
            }
            public static string TaskCreateFailed(string detail)
            {
                return T("Không tạo được tác vụ: " + detail,
                         "Could not create the task: " + detail);
            }
            public static string ProtectionOn(bool blockScript, bool task)
            {
                return T("Bật bảo vệ (chặn script=" + blockScript + ", tác vụ=" + task + ")",
                         "Protection on (block scripts=" + blockScript + ", task=" + task + ")");
            }
            public static string ProtectionOff { get { return T("Tắt bảo vệ", "Protection off"); } }
            public static string AutorunBlocked(string drive)
            {
                return T("Chặn autorun: " + drive, "Autorun blocked: " + drive);
            }
            public static string WatchAlreadyRunning
            {
                get
                {
                    return T("Bảo vệ nền đã chạy ở tiến trình khác, thoát.",
                             "Background protection is already running in another process; exiting.");
                }
            }
            public static string WatchNeedsAdmin
            {
                get
                {
                    return T("Bảo vệ nền cần quyền quản trị, thoát.",
                             "Background protection needs administrator rights; exiting.");
                }
            }
            public static string WatchFingerprintMismatch
            {
                get
                {
                    return T("Tệp chạy nền không khớp dấu vết đã cài. Không chạy.",
                             "The background binary does not match the installed fingerprint; not running.");
                }
            }
            public static string WatchStarted(string version)
            {
                return T("Bảo vệ nền bắt đầu (v" + version + ")",
                         "Background protection started (v" + version + ")");
            }
            public static string VolumeError(string volume, string detail)
            {
                return T("Lỗi khi xử lý " + volume + ": " + detail,
                         "Error while handling " + volume + ": " + detail);
            }
            public static string WatchLoopError(string detail)
            {
                return T("Lỗi bảo vệ nền: " + detail,
                         "Background protection error: " + detail);
            }
            public static string WatchStopped
            {
                get { return T("Bảo vệ nền dừng theo yêu cầu", "Background protection stopped on request"); }
            }
            public static string WatchScanTruncated(string volume)
            {
                return T("Kiểm tra " + volume + " chưa xong (giới hạn thời gian/số tệp)",
                         "Scan of " + volume + " did not finish (time/file limit)");
            }
            public static string AutoScan(string volume, int files, int items, int cleaned,
                                           int review, int unhidden, int stopped)
            {
                return T("Tự động kiểm tra " + volume + ": " + files + " tệp, " + items
                         + " mục, cách ly " + cleaned + ", cần xem " + review
                         + ", bỏ ẩn " + unhidden + ", dừng tiến trình " + stopped,
                         "Automatic scan of " + volume + ": " + files + " files, " + items
                         + " items, quarantined " + cleaned + ", needs review " + review
                         + ", unhid " + unhidden + ", stopped processes " + stopped);
            }
            public static string GuardTaskCreateFailed(string detail)
            {
                return T("Không tạo được tác vụ bảo vệ: " + detail,
                         "Could not create the protection task: " + detail);
            }
            public static string DriveReinfected(string volume)
            {
                return T("Ổ " + volume + " vừa bị nhiễm lại nhiều lần trong ít phút. "
                         + "Mã độc có thể đang chạy trên máy: mở ShieldGate và bấm Kiểm tra máy.",
                         "Drive " + volume + " was re-infected several times within minutes. "
                         + "Malware may be running on this computer: open ShieldGate and run Check this PC.");
            }
            public static string PcItem(string kind, string name, string detail)
            {
                return T("Mục cần xem: " + kind + " | " + name + " | " + detail,
                         "Finding: " + kind + " | " + name + " | " + detail);
            }
            public static string TaskDeleted(string task)
            {
                return T("Đã sao lưu XML và xoá tác vụ: " + task,
                         "Backed up the XML and deleted the task: " + task);
            }
            public static string TaskDisabled(string task)
            {
                return T("Không sao lưu được XML nên chỉ tắt tác vụ: " + task,
                         "Could not back up the XML, so the task was disabled only: " + task);
            }
            public static string ServiceDeleted(string name)
            {
                return T("Đã sao lưu Registry, dừng và xoá dịch vụ/driver: " + name,
                         "Backed up the registry, stopped and deleted the service/driver: " + name);
            }
            public static string DriveReinfectedAfterFix(string volume)
            {
                return T("Đã sửa máy nhưng ổ " + volume + " vẫn bị nhiễm lại. Nguồn lây có thể là "
                         + "driver/rootkit hoặc tiến trình được bảo vệ: hãy quét Microsoft Defender Offline.",
                         "The PC was fixed but drive " + volume + " is still getting re-infected. "
                         + "The source may be a driver/rootkit or a protected process: run Microsoft Defender Offline.");
            }
            public static string HuntFound(string volume, int count)
            {
                return T("Nhiễm lại " + volume + ": quét nguồn lây trên máy thấy " + count + " mục.",
                         "Re-infection on " + volume + ": the source hunt found " + count + " item(s).");
            }
            public static string HuntClean(string volume)
            {
                return T("Nhiễm lại " + volume + ": chưa thấy mục nào trên máy. Nguồn có thể là "
                         + "tiến trình ẩn hoặc driver chưa nhận diện.",
                         "Re-infection on " + volume + ": no known item was found on the PC. "
                         + "The source may be a hidden process or an unidentified driver.");
            }
            public static string HuntError(string detail)
            {
                return T("Không quét được nguồn lây: " + detail,
                         "The source hunt failed: " + detail);
            }
            public static string SettingsApplied(bool autorun, bool ini, bool wsh, bool registry, bool guard, bool ok)
            {
                return T("Thiết lập bảo vệ: chặn tự chạy=" + autorun + ", chặn autorun.inf=" + ini
                         + ", chặn script=" + wsh + ", bảo vệ Registry=" + registry
                         + ", chạy nền=" + guard + " (ok=" + ok + ")",
                         "Protection settings: AutoRun block=" + autorun + ", autorun.inf block=" + ini
                         + ", script block=" + wsh + ", registry protection=" + registry
                         + ", background=" + guard + " (ok=" + ok + ")");
            }
            public static string RegistryRepaired(int count)
            {
                return T("Đã tự sửa " + count + " khoá Registry bị phá hoại.",
                         "Automatically repaired " + count + " damaged registry keys.");
            }
            public static string RegistryRepairFailed(string what)
            {
                return T("Không sửa được khoá Registry: " + what,
                         "Could not repair the registry key: " + what);
            }
            public static string ProcessStopped(int pid, string root)
            {
                return T("Dừng tiến trình " + pid + " chạy từ " + root,
                         "Stopped process " + pid + " running from " + root);
            }
            public static string QuarantineFailed(string path, string detail)
            {
                return T("Không cách ly được " + path + ": " + detail,
                         "Could not quarantine " + path + ": " + detail);
            }
            public static string QuarantineSummary(int done, string root, int failed)
            {
                return T("Cách ly " + done + " tệp từ " + root + " (lỗi " + failed + ")",
                         "Quarantined " + done + " files from " + root + " (" + failed + " errors)");
            }
            public static string FakeFolderKept(int kept, string root)
            {
                return T("Giữ lại " + kept + " tệp dữ liệu trong thư mục giả trên " + root
                         + " (chỉ cách ly tệp chạy được).",
                         "Kept " + kept + " user files in the fake folder on " + root
                         + " (only executable payloads were quarantined).");
            }
            public static string Base64Renamed(string from, string to)
            {
                return T("Khôi phục tên Base64: " + from + " → " + to,
                         "Restored Base64 name: " + from + " -> " + to);
            }
            public static string CleanBusy(string root)
            {
                return T("Một tiến trình khác đang xử lý " + root + "; bỏ qua lượt này.",
                         "Another ShieldGate process is already cleaning " + root
                         + "; skipping this pass.");
            }
            public static string FlagOnlyKept(string path)
            {
                return T("Giữ nguyên (chỉ gắn cờ): " + path,
                         "Kept (flagged only): " + path);
            }
            public static string DriveNotWritable(string root)
            {
                return T("Ổ " + root + " chỉ đọc hoặc không có quyền ghi; không thay đổi gì.",
                         "Drive " + root + " is read-only or not writable; nothing was changed.");
            }
            public static string RestoreCopied(string name, int code)
            {
                return T("\"" + name + "\" bị từ chối khi đổi chỗ (lỗi " + code + "), đã chuyển bằng cách sao chép rồi xoá bản gốc.",
                         "\"" + name + "\" was refused a direct move (error " + code + "); moved by copying and deleting the original.");
            }
            public static string RestoreOriginalLeft(string name, int code)
            {
                return T("\"" + name + "\" đã được sao chép ra gốc ổ nhưng không xoá được bản gốc (lỗi đổi chỗ " + code + "). Bản gốc còn trong thư mục không tên.",
                         "\"" + name + "\" was copied to the drive root but the original could not be deleted (move error " + code + "). The original remains in the nameless folder.");
            }
            public static string RestoreMoveFailed(string name, int code)
            {
                return T("Không chuyển được \"" + name + "\" ra gốc ổ (mã lỗi Windows " + code + ").",
                         "Could not move \"" + name + "\" to the drive root (Windows error " + code + ").");
            }
            public static string Restored(int moved, int removed, string root, int failed)
            {
                return T("Đã chuyển " + moved + " mục ra gốc " + root + ", xoá " + removed + " thư mục không tên" + (failed > 0 ? ", " + failed + " mục lỗi" : "") + ".",
                         "Moved " + moved + " item(s) to the root of " + root + ", removed " + removed + " nameless folder(s)" + (failed > 0 ? ", " + failed + " failed" : "") + ".");
            }
            public static string Unhidden(int count, string root)
            {
                return T("Bỏ thuộc tính ẩn cho " + count + " mục trên " + root,
                         "Unhid " + count + " items on " + root);
            }
            public static string RegBackupFailed(string key, int code)
            {
                return T("Không sao lưu được khoá " + key + " (reg export exit " + code + "); giữ nguyên, chưa sửa.",
                         "Could not back up " + key + " (reg export exit " + code + "); left unchanged.");
            }
            public static string RulesBesideIgnored(string path)
            {
                return T("Bỏ qua " + path + " cạnh tệp chạy; luật chỉ đọc từ thư mục dữ liệu.",
                         "Ignoring " + path + " beside the executable; rules are read only from the data folder.");
            }
            public static string RulesMerged(int fileVersion, int current)
            {
                return T("rules.json phiên bản " + fileVersion + " thiếu khoá mới; đã bổ sung mặc định của bản "
                         + current + ".",
                         "rules.json version " + fileVersion + " is older; defaults for version "
                         + current + " were merged in.");
            }
            public static string PcFixed(string kind, string arg)
            {
                return T("Đã sửa: " + kind + " " + arg, "Fixed: " + kind + " " + arg);
            }
            public static string PcFixError(string detail)
            {
                return T("Lỗi khi sửa máy: " + detail, "Error while fixing this computer: " + detail);
            }
            public static string RestoreBatch(int restored, string batch, int missing, int refused)
            {
                return T("Khôi phục " + restored + " tệp từ " + batch + " (thiếu " + missing
                       + ", từ chối " + refused + ")",
                         "Restored " + restored + " files from " + batch + " (" + missing
                       + " missing, " + refused + " refused)");
            }
            public static string RestoreRefused(string path, string reason)
            {
                return T("Từ chối khôi phục: " + path + " (" + reason + ")",
                         "Refused to restore: " + path + " (" + reason + ")");
            }
            // Ly do tu choi, ghep vao dong nhat ky o tren.
            public static string RestoreOutside { get { return T("nguồn nằm ngoài lần cách ly", "source is outside the batch"); } }
            public static string RestoreSystemFolder { get { return T("đích nằm trong thư mục hệ thống", "target is inside a system folder"); } }
            public static string RestoreAppFolder { get { return T("đích nằm trong vùng cách ly hoặc thư mục chương trình", "target is inside quarantine or the program folder"); } }
            public static string RestoreBadPath { get { return T("đường dẫn đích không hợp lệ", "target path is invalid"); } }
            public static string QuarantineCleaned(int files, string batch)
            {
                return T("Xoá vĩnh viễn " + files + " tệp cách ly trong " + batch,
                         "Permanently deleted " + files + " quarantined files from " + batch);
            }
            public static string QuarantineCleanedAll(int batches, int files)
            {
                return T("Dọn sạch vùng cách ly: " + batches + " lần, " + files + " tệp",
                         "Quarantine cleaned: " + batches + " batches, " + files + " files");
            }
            public static string QuarantineCleanError(string batch)
            {
                return T("Không xoá được vùng cách ly: " + batch,
                         "Could not delete quarantine: " + batch);
            }
            public static string LogExported(string path)
            {
                return T("Xuất nhật ký: " + path, "Log exported: " + path);
            }
            public static string LogExportFailed(string detail)
            {
                return T("Không xuất được nhật ký: " + detail,
                         "Could not export the log: " + detail);
            }
            public static string PcScanError(string detail)
            {
                return T("Lỗi khi kiểm tra máy: " + detail,
                         "Error while scanning this computer: " + detail);
            }
            public static string PcScanFound(int count)
            {
                return T("Kiểm tra máy: " + count + " mục đáng ngờ",
                         "Computer scan: " + count + " suspicious items");
            }
            public static string PcScanClean
            {
                get { return T("Kiểm tra máy: không phát hiện gì", "Computer scan: nothing found"); }
            }
            public static string CommandLineError(string detail)
            {
                return T("Lỗi dòng lệnh: " + detail, "Command line error: " + detail);
            }
            public static string Open(string version, string language)
            {
                return T("Mở ShieldGate " + version + " (" + language + ")",
                         "Open ShieldGate " + version + " (" + language + ")");
            }
            public static string UnhandledError(string detail)
            {
                return T("Lỗi không xử lý: " + detail, "Unhandled error: " + detail);
            }
            public static string ProtectionApplyError(string detail)
            {
                return T("Lỗi khi áp dụng bảo vệ: " + detail,
                         "Error while applying protection: " + detail);
            }
            public static string SelfTest(string usb)
            {
                return T("Tự kiểm tra chạy trên " + usb, "Self-test ran on " + usb);
            }
            public static string UsbScanError(string drive, string detail)
            {
                return T("Lỗi khi kiểm tra " + drive + ": " + detail,
                         "Error while scanning " + drive + ": " + detail);
            }
            public static string UsbScanDone(string drive, int files, int items, double seconds)
            {
                return T("Kiểm tra " + drive + ": " + files + " tệp, phát hiện " + items
                         + " mục trong " + seconds + " giây",
                         "Scanned " + drive + ": " + files + " files, found " + items
                         + " items in " + seconds + " seconds");
            }
            public static string QuarantineError(string detail)
            {
                return T("Lỗi khi cách ly: " + detail, "Error while quarantining: " + detail);
            }
            public static string UndoError(string detail)
            {
                return T("Lỗi khi hoàn tác: " + detail, "Error while undoing: " + detail);
            }
            public static string UnhideError(string detail)
            {
                return T("Lỗi khi bỏ ẩn: " + detail, "Error while unhiding: " + detail);
            }

            public static string WriteProtect(bool on, bool ok)
            {
                return T((on ? "Bật" : "Tắt") + " chặn ghi ổ USB (ok=" + ok + ")",
                         (on ? "Enabled" : "Disabled") + " USB write protection (ok=" + ok + ")");
            }
            public static string WriteProtectRecovered
            {
                get
                {
                    return T("Đã bật lại chặn ghi USB sau khi tạm mở để cách ly",
                             "USB write protection was restored after a temporary cleaning pause");
                }
            }
            public static string WriteProtectRecoverFailed
            {
                get
                {
                    return T("Không bật lại được chặn ghi USB sau khi tạm mở",
                             "USB write protection could not be restored after a temporary pause");
                }
            }
            public static string PolicyError(string detail)
            {
                return T("Lỗi chính sách bảo vệ: " + detail, "Protection policy error: " + detail);
            }
            public static string BehaviorBurst(string drive, int total, int risky)
            {
                return T("Bùng nổ ghi trên " + drive + ": " + total + " mục, " + risky + " .lnk/script/payload",
                         "Write burst on " + drive + ": " + total + " items, " + risky +
                         " .lnk/script/payload files");
            }
            public static string BehaviorWatcherError(string detail)
            {
                return T("Lỗi giám sát hành vi: " + detail, "Behaviour monitor error: " + detail);
            }
            public static string DeviceScanError(string detail)
            {
                return T("Không kiểm kê được thiết bị: " + detail,
                         "Could not inventory devices: " + detail);
            }
            public static string DeviceNew(string name, string cls)
            {
                return T("Thiết bị mới: " + name + " (" + cls + ")",
                         "New device: " + name + " (" + cls + ")");
            }
            public static string DeviceBlocked(string name)
            {
                return T("Thiết bị đã chặn đang kết nối: " + name,
                         "Blocked device is connected: " + name);
            }
            public static string DeviceBadUsb(string name, string detail)
            {
                return T("Bàn phím mới kèm tiến trình shell: " + name + " | " + detail,
                         "New keyboard with a shell process: " + name + " | " + detail);
            }
            public static string DeviceComposite(string name)
            {
                return T("Nghi composite/BadUSB (vừa ổ vừa bàn phím): " + name,
                         "Composite/BadUSB suspect (storage and keyboard): " + name);
            }
            public static string BadUsbRate(string device, int events, double seconds, double peak)
            {
                return T("BadUSB (chỉ đo): " + device + " " + events + " sự kiện trong "
                         + seconds.ToString("0.0") + " giây, đỉnh " + peak.ToString("0") + "/giây",
                         "BadUSB (measure only): " + device + " " + events + " events in "
                         + seconds.ToString("0.0") + " s, peak " + peak.ToString("0") + "/s");
            }
            public static string BadUsbUnavailable
            {
                get
                {
                    return T("Không đăng ký được Raw Input; không đo tốc độ gõ.",
                             "Could not register Raw Input; typing rate is not measured.");
                }
            }
            public static string DeviceBaseline(int count)
            {
                return T("Lập kho thiết bị lần đầu: " + count + " thiết bị",
                         "Device inventory baseline: " + count + " devices");
            }
            public static string DeviceTrustChanged(string name, string trust)
            {
                return T("Đổi trạng thái tin cậy: " + name + " -> " + trust,
                         "Device trust changed: " + name + " -> " + trust);
            }
            public static string ChkdskDone(string drive, bool fix, int code)
            {
                return T("chkdsk " + drive + (fix ? " /f" : " (chỉ đọc)") + " -> " + code,
                         "chkdsk " + drive + (fix ? " /f" : " (read-only)") + " -> " + code);
            }
            public static string BackupDone(string drive, string dest)
            {
                return T("Sao lưu " + drive + " vào " + dest,
                         "Backed up " + drive + " to " + dest);
            }
            public static string BackupFailed(string drive, int code)
            {
                return T("Sao lưu " + drive + " thất bại (mã " + code + ")",
                         "Backup of " + drive + " failed (code " + code + ")");
            }
            public static string DefenderScan(string path, int code)
            {
                return T("Windows Defender quét " + path + " -> " + code,
                         "Windows Defender scanned " + path + " -> " + code);
            }
        }

        // Chi tiết ngắn ghi vào events.tsv; không chứa nội dung tệp.
        public static class Events
        {
            public static string ExecBlocked { get { return T("Dừng tiến trình chạy từ ổ rời", "Stopped a process running from a removable drive"); } }
            public static string BehaviorBurst(int total, int risky, int window)
            {
                return T("Trong " + window + " giây: " + total + " mục mới, " + risky +
                         " tệp .lnk/script/payload",
                         "Within " + window + " seconds: " + total + " new items, " + risky +
                         " .lnk/script/payload files");
            }
            public static string DefenderClean { get { return T("Windows Defender: không phát hiện gì", "Windows Defender: nothing found"); } }
            public static string DefenderThreat { get { return T("Windows Defender: phát hiện mối nguy", "Windows Defender: threat found"); } }
            public static string BackupDone(string dest) { return T("Sao lưu vào " + dest, "Backed up to " + dest); }
            public static string DeviceNew(string name, string cls, string ids)
            {
                return T("Thiết bị mới " + name + " · " + cls + " · " + ids,
                         "New device " + name + " · " + cls + " · " + ids);
            }
            public static string DeviceBlocked(string name) { return T("Thiết bị đã chặn: " + name, "Blocked device: " + name); }
            public static string DeviceBadUsb(string name) { return T("Nghi BadUSB: bàn phím mới + shell", "BadUSB suspect: new keyboard + shell"); }
            public static string BadUsbAlert(string device, string ids, int events, double seconds,
                                             string shell, string parent)
            {
                string who = parent.Length > 0 ? shell + " (" + parent + ")" : shell;
                return T("Nghi BadUSB: " + device + " (" + ids + ") gõ " + events +
                         " sự kiện trong " + seconds.ToString("0.0") + " giây rồi mở " + who +
                         ". Đây là dấu hiệu, không phải kết luận; không tắt thiết bị.",
                         "BadUSB suspect: " + device + " (" + ids + ") sent " + events +
                         " events in " + seconds.ToString("0.0") + " s then opened " + who +
                         ". This is a sign, not a verdict; no device was disabled.");
            }
            public static string DeviceComposite(string name) { return T("Nghi composite: " + name, "Composite suspect: " + name); }
            public static string DeviceBaseline(int count) { return T("Kho thiết bị ban đầu: " + count, "Initial device inventory: " + count); }
            public static string DeviceTrust(string name, string trust) { return T(name + " -> " + trust, name + " -> " + trust); }
            public static string DriveReinfected(string volume)
            {
                return T("Ổ " + volume + " bị nhiễm lại liên tục: nguồn lây đang chạy trên máy",
                         "Drive " + volume + " keeps getting re-infected: the source is running on this computer");
            }
            public static string DriveScan(string volume, int cleaned, int review)
            {
                if (cleaned > 0 && review > 0)
                    return S.Tray.CleanedReview(volume, cleaned, review);
                if (cleaned > 0) return S.Tray.Cleaned(volume, cleaned);
                return S.Tray.Attention(review);
            }
        }

        public static class Vt
        {
            public static string BadHash
            {
                get { return T("mã băm không hợp lệ", "invalid hash"); }
            }
            public static string NoKey
            {
                get { return T("chưa có khoá VirusTotal", "no VirusTotal API key"); }
            }
            public static string NotFound
            {
                get { return T("VirusTotal không có mẫu này", "VirusTotal does not know this file"); }
            }
            public static string Auth
            {
                get { return T("khoá VirusTotal bị từ chối", "VirusTotal rejected the API key"); }
            }
            public static string RateLimited
            {
                get { return T("VirusTotal giới hạn 4 yêu cầu/phút, thử lại sau", "VirusTotal allows 4 requests per minute; try again later"); }
            }
            public static string HttpError(int status)
            {
                return T("VirusTotal trả về HTTP " + status, "VirusTotal returned HTTP " + status);
            }
            public static string Network
            {
                get { return T("không kết nối được VirusTotal", "could not reach VirusTotal"); }
            }
            public static string NoStats
            {
                get { return T("VirusTotal trả về dữ liệu không đọc được", "VirusTotal returned data that could not be read"); }
            }
            public static string Disabled
            {
                get { return T("VirusTotal đang tắt", "VirusTotal is turned off"); }
            }
        }

        public static class About
        {
            public static string Title { get { return T("Giới thiệu", "About"); } }
            public static string Version(string v) { return T("Phiên bản " + v, "Version " + v); }
            public static string License { get { return T("Mã nguồn mở, giấy phép MIT", "Open source, MIT license"); } }
            public static string Source { get { return T("Mã nguồn", "Source code"); } }
            public static string Data { get { return T("Dữ liệu", "Data"); } }
            public static string Hash { get { return T("SHA256 của tệp đang chạy", "SHA256 of the running file"); } }
            public static string SourceSoon { get { return T("Mã nguồn đầy đủ đi kèm bản phát hành này.", "The full source code ships with this release."); } }
            public static string OpenFolder { get { return T("Mở thư mục", "Open folder"); } }

            // Gỡ cài đặt
            public static string RemoveTitle { get { return T("Gỡ cài đặt", "Uninstall"); } }
            public static string RemoveNote
            {
                get
                {
                    return T("Tắt bảo vệ và xoá toàn bộ dữ liệu, nhật ký, tệp cách ly cùng tệp chạy ShieldGate khỏi máy. Không thể hoàn tác.",
                             "Turns off protection and removes all ShieldGate data, logs, quarantined files and the app file from this computer. This cannot be undone.");
                }
            }
            public static string RemoveAction { get { return T("Gỡ ShieldGate khỏi máy", "Remove ShieldGate from this computer"); } }
            public static string RemoveWorking { get { return T("Đang gỡ ShieldGate…", "Removing ShieldGate…"); } }
            public static string RemoveDone { get { return T("Đã gỡ ShieldGate khỏi máy. Ứng dụng sẽ đóng.", "ShieldGate has been removed from this computer. The app will now close."); } }
            public static string RemoveDoneKept
            {
                get
                {
                    return T("Đã gỡ ShieldGate khỏi máy. Thư mục cách ly và bản sao lưu ổ vẫn còn trong thư mục dữ liệu để bạn tự xử lý. Ứng dụng sẽ đóng.",
                             "ShieldGate has been removed. Quarantine and drive backups are still in the data folder for you to handle. The app will now close.");
                }
            }
            public static string RemoveCleanupPending
            {
                get
                {
                    return T("Đã tắt bảo vệ. Một số tệp còn lại sẽ được dọn nốt sau khi ứng dụng đóng.",
                             "Protection is off. The remaining files will be cleaned up after the app closes.");
                }
            }
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
            public static string DriveReadOnly(string drive)
            {
                return T("Ổ " + drive + " đang ở chế độ chỉ đọc hoặc không có quyền ghi. Bỏ khoá ghi rồi thử lại.",
                         "Drive " + drive + " is read-only or not writable. Remove the write lock and try again.");
            }
            public static string Space(int files, string need)
            {
                return T("Không đủ dung lượng để cách ly " + files + " tệp. Cần thêm " + need + ".",
                         "Not enough space to quarantine " + files + " files. Need " + need + " more.");
            }
            public static string Busy { get { return T("Đang có một lần kiểm tra khác chạy. Đợi lần đó xong rồi thử lại.", "Another scan is running. Wait for it to finish and try again."); } }
            public static string Unplugged(string drive) { return T("Ổ " + drive + " đã được rút ra trong lúc kiểm tra.", "Drive " + drive + " was removed during the scan."); }
            public static string Unknown { get { return T("Đã xảy ra lỗi không mong muốn.", "An unexpected error occurred."); } }
            public static string RegBackup(string key)
            {
                return T("Không sao lưu được khoá " + key + " nên chưa sửa gì. Xem nhật ký.",
                         "Could not back up " + key + ", so nothing was changed. See the log.");
            }
            public static string UnknownAction { get { return T("Mở nhật ký", "Open log"); } }
            public static string ProcessChanged(string name)
            {
                return T("Tiến trình " + name + " đã đổi hoặc thoát sau khi quét. Quét máy lại rồi sửa.",
                         name + " changed or exited after the scan. Run Check this PC again before fixing.");
            }            public static string Partial(int n) { return T(n + " tệp không xử lý được. Xem nhật ký để biết lý do.", n + " files could not be processed. See the log for details."); }
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
            public static string VtTitle { get { return T("Bật VirusTotal?", "Turn on VirusTotal?"); } }
            public static string VtBody
            {
                get
                {
                    return T("Khi bật, ShieldGate chỉ gửi mã băm SHA-256 của tệp tới virustotal.com và nhận số lượng hãng phát hiện. Không gửi tên tệp, đường dẫn hay nội dung. Khoá API được lưu mã hoá bằng DPAPI cho tài khoản Windows này và có thể xoá bất cứ lúc nào.\n\nKết quả chỉ để tham khảo; ShieldGate không tự cách ly dựa trên VirusTotal.",
                             "When enabled, ShieldGate sends only the file's SHA-256 hash to virustotal.com and receives the number of engines that flagged it. It does not send file names, paths or contents. The API key is stored with DPAPI for this Windows account and can be removed at any time.\n\nResults are advisory only; ShieldGate never quarantines based on VirusTotal.");
                }
            }
            public static string VtOk { get { return T("Bật VirusTotal", "Turn on VirusTotal"); } }
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
            public static string CleanTitle(int n) { return T("Xoá vĩnh viễn " + n + " tệp?", "Delete " + n + " files permanently?"); }
            public static string CleanBody
            {
                get
                {
                    return T("Tệp sẽ bị xoá khỏi vùng cách ly và không thể khôi phục. Chỉ xoá khi bạn chắc không cần chúng nữa.",
                             "The files will be deleted from quarantine and cannot be restored. Only delete them if you are sure you no longer need them.");
                }
            }
            public static string CleanAllTitle { get { return T("Dọn sạch vùng cách ly?", "Clean up all quarantine?"); } }
            public static string CleanAllBody
            {
                get
                {
                    return T("Toàn bộ tệp đang cách ly sẽ bị xoá vĩnh viễn và không thể khôi phục.",
                             "Every quarantined file will be deleted permanently and cannot be restored.");
                }
            }
            public static string CleanOk { get { return T("Xoá vĩnh viễn", "Delete permanently"); } }
            public static string UninstallTitle { get { return T("Gỡ ShieldGate khỏi máy?", "Remove ShieldGate from this computer?"); } }
            public static string UninstallBody
            {
                get
                {
                    return T("ShieldGate sẽ tắt bảo vệ, xoá tác vụ hẹn giờ, khôi phục thiết lập Windows đã thay đổi, rồi xoá nhật ký, luật, kho thiết bị và tệp chạy ShieldGate.\n\nMặc định giữ lại thư mục cách ly và bản sao lưu ổ; bỏ tích ô bên dưới nếu muốn xoá cả hai.",
                             "ShieldGate will turn off protection, remove its scheduled tasks, restore the changed Windows settings, then delete its logs, rules, device store and the ShieldGate app file.\n\nQuarantine and drive backups are kept by default; clear the box below to delete them too.");
                }
            }
            public static string UninstallKeep { get { return T("Giữ cách ly và bản sao lưu ổ", "Keep quarantine and drive backups"); } }
            public static string UninstallKeepDetail(long quarantine, long repair)
            {
                return T("Cách ly " + Repair.Size(quarantine) + " · Bản sao lưu ổ " + Repair.Size(repair)
                       + " · Vẫn xoá nhật ký và cấu hình.",
                         "Quarantine " + Repair.Size(quarantine) + " · Drive backups " + Repair.Size(repair)
                       + " · Logs and settings are still removed.");
            }
            public static string UninstallOk { get { return T("Gỡ cài đặt", "Uninstall"); } }
            public static string UnhideTitle { get { return T("Hiện tệp bị ẩn?", "Unhide files?"); } }
            public static string UnhideBody
            {
                get
                {
                    return T("Dữ liệu trong thư mục không tên (nếu có) sẽ được chuyển ra gốc ổ rồi thư mục đó bị xoá; mọi tệp và thư mục sẽ bỏ thuộc tính ẩn. Không ghi đè: trùng tên thì thêm (1). Không xoá dữ liệu của bạn.",
                             "Data inside nameless folders (if any) will be moved to the drive root and the empty folder removed; every file and folder will lose the hidden attribute. Nothing is overwritten: name clashes get (1). Your data is not deleted.");
                }
            }
            public static string UnhideOk { get { return T("Hiện tệp", "Unhide files"); } }
            public static string RenameTitle(int n)
            {
                return T("Khôi phục tên gốc cho " + n + " tệp?", "Restore original names for " + n + " files?");
            }
            public static string RenameBody
            {
                get
                {
                    return T("Chỉ đổi tên tài liệu nằm trong thư mục giả hoặc thư mục không tên, khi tên hiện tại là Base64 của một tên tài liệu hợp lệ. Không ghi đè: nếu tên gốc đã có thì bỏ qua. Không xoá dữ liệu.",
                             "Only documents inside fake or nameless folders are renamed, and only when the current name is Base64 of a valid document name. Nothing is overwritten: if the original name already exists, the file is skipped. No data is deleted.");
                }
            }
            public static string RenameOk(int n) { return T("Đổi tên " + n + " tệp", "Rename " + n + " files"); }
            public static string BackupTitle { get { return T("Sao lưu toàn bộ ổ?", "Back up the entire drive?"); } }
            public static string BackupBody(string drive, long files, long bytes)
            {
                return T("Sao lưu " + drive + " (" + files.ToString("#,0") + " tệp, " + S.Repair.Size(bytes)
                         + ") vào thư mục sửa chữa. Không xoá gì trên ổ nguồn.",
                         "Back up " + drive + " (" + files.ToString("#,0") + " files, " + S.Repair.Size(bytes)
                         + ") into the repair folder. Nothing is deleted from the source drive.");
            }
            public static string BackupOk { get { return T("Sao lưu", "Back up"); } }
            public static string ChkdskReadTitle { get { return T("Kiểm tra ổ ở chế độ chỉ đọc?", "Check the drive in read-only mode?"); } }
            public static string ChkdskReadBody(string drive)
            {
                return T("chkdsk " + drive + " chỉ đọc và báo lỗi, không sửa gì. Có thể mất vài phút.",
                         "chkdsk " + drive + " will only read and report; nothing is repaired. This can take a few minutes.");
            }
            public static string ChkdskReadOk { get { return T("Kiểm tra", "Check"); } }
            public static string ChkdskFixTitle { get { return T("Sửa lỗi ổ bằng chkdsk /f?", "Repair the drive with chkdsk /f?"); } }
            public static string ChkdskFixBody(string drive)
            {
                return T("chkdsk " + drive + " /f GHI lên ổ và có thể thay đổi dữ liệu. Chỉ tiếp tục khi đã sao lưu và hiểu rủi ro.",
                         "chkdsk " + drive + " /f WRITES to the drive and may change data. Continue only after a backup and if you understand the risk.");
            }
            public static string ChkdskFixOk { get { return T("Tiếp tục", "Continue"); } }
            public static string ChkdskFixAgainTitle { get { return T("Xác nhận lần hai", "Confirm again"); } }
            public static string ChkdskFixAgainBody(string drive)
            {
                return T("Đây là lần xác nhận cuối. Chạy chkdsk /f trên " + drive + "?",
                         "This is the final confirmation. Run chkdsk /f on " + drive + "?");
            }
            public static string ChkdskFixAgainOk { get { return T("Chạy chkdsk /f", "Run chkdsk /f"); } }
            public static string DefenderTitle { get { return T("Quét bằng Windows Defender?", "Scan with Windows Defender?"); } }
            public static string DefenderBody(string drive)
            {
                return T("Gọi Windows Defender quét " + drive + ". Quá trình có thể lâu và Defender có thể tự xử lý mối nguy theo chính sách của máy.",
                         "Ask Windows Defender to scan " + drive + ". This can take a while and Defender may act on threats according to this computer's policy.");
            }
            public static string DefenderOk { get { return T("Quét", "Scan"); } }
            public static string ForgetTitle { get { return T("Quên thiết bị?", "Forget this device?"); } }
            public static string ForgetBody(string name)
            {
                return T("Xoá trạng thái tin cậy của " + name + ". Lần sau cắm vào, thiết bị sẽ được coi là mới.",
                         "Remove the trust state of " + name + ". The next time it is connected it will be treated as new.");
            }
            public static string ForgetOk { get { return T("Quên", "Forget"); } }
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
            public static string Registry
            {
                get
                {
                    return T("HKCR\\Folder\\shell\\open\\command: DelegateExecute = {11dbb47c-…}, mở bằng Explorer.exe; kiểm tra .exe, hiện tệp ẩn",
                             "HKCR\\Folder\\shell\\open\\command: DelegateExecute = {11dbb47c-…}, opens with Explorer.exe; checks .exe and hidden-file setting");
                }
            }
            public static string Guard
            {
                get
                {
                    return T("Tác vụ lập lịch “ShieldGate USB Guard”, chạy bằng SYSTEM khi khởi động",
                             "Scheduled task “ShieldGate USB Guard”, runs as SYSTEM at startup");
                }
            }
            public static string WriteProtect
            {
                get
                {
                    return T("HKLM\\SYSTEM\\…\\StorageDevicePolicies: WriteProtect = 1 (tuỳ chọn)",
                             "HKLM\\SYSTEM\\…\\StorageDevicePolicies: WriteProtect = 1 (opt-in)");
                }
            }
            public static string BlockExec
            {
                get
                {
                    return T("Theo dõi Win32_Process và dừng tiến trình có tệp chạy trên ổ rời (tuỳ chọn)",
                             "Watches Win32_Process and stops processes whose executable is on a removable drive (opt-in)");
                }
            }
        }

        // Chỉ dùng cho phần tự kiểm tra trên dòng lệnh.
        public static class Selftest
        {
            public static string RestoreNameless { get { return T("chuyển dữ liệu ra khỏi thư mục không tên, không ghi đè", "move data out of a nameless folder without overwriting"); } }
            public static string FakeFolderDetected { get { return T("nhận ra cấu trúc virus: shortcut cmd + thư mục Docusment + payload", "recognise the virus structure: cmd shortcut + Docusment folder + payload"); } }
            public static string CommandVerdict { get { return T("bắt lệnh cmd bọc payload, không báo nhầm shortcut hợp lệ", "flag a cmd command wrapping a payload without false-positive shortcuts"); } }
            public static string PcFixWorks { get { return T("cách ly thư mục giả và shortcut khi sửa máy", "quarantine the fake folder and shortcut during machine fix"); } }
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
            public static string SettingsMerge { get { return T("giữ thay đổi do tiến trình khác ghi", "keep changes written by another process"); } }
            public static string GuardConfig { get { return T("lưu và đọc lại lựa chọn bảo vệ", "save and reload protection choices"); } }
            public static string ProtectKeepsChoice { get { return T("bật bảo vệ giữ nguyên mục người dùng đã tắt", "turning protection on keeps items the user turned off"); } }
            public static string ProtectionStaysOn { get { return T("còn một lớp bảo vệ là vẫn tính đang bảo vệ", "one active layer still counts as protection on"); } }
            public static string GuardOffSticks { get { return T("tắt hết ghi guard.cfg toàn 0, vẫn nhớ lựa chọn cũ", "turning everything off writes an all-zero guard.cfg and still remembers the old choices"); } }
            public static string TamperRefused { get { return T("không khôi phục bản cách ly đã bị sửa", "refuse to restore a modified quarantined file"); } }
            public static string Truncation { get { return T("báo đúng khi quét bị giới hạn", "report when a scan is truncated"); } }
            public static string LogLanguage { get { return T("nhật ký đổi theo ngôn ngữ", "log text follows the chosen language"); } }
            public static string RegDeleteMissing { get { return T("xoá khoá đăng ký chưa tồn tại không báo lỗi", "deleting a registry key that never existed does not fail"); } }
            public static string RegistryScan { get { return T("quét Registry File Explorer không gây lỗi", "the File Explorer registry check runs without errors"); } }
            public static string LongPathQuarantine { get { return T("cách ly và khôi phục tệp có đường dẫn dài hơn 260 ký tự", "quarantine and restore a file whose path is longer than 260 characters"); } }
            public static string JsonRoundTrip { get { return T("đọc và ghi lại JSON có chú thích, mảng và ký tự thoát", "parse and re-serialise JSON with comments, arrays and escapes"); } }
            public static string JsonReject { get { return T("từ chối JSON hỏng thay vì đọc sai", "reject broken JSON instead of misreading it"); } }
            public static string RulesDefaults { get { return T("luật mặc định có ngưỡng và trọng số đúng", "default rules carry the expected threshold and weights"); } }
            public static string RulesCustom { get { return T("rules.json bổ sung luật, không xoá đuôi mặc định", "rules.json adds rules and keeps default extensions"); } }
            public static string RulesImport { get { return T("nhập rules.json có xác nhận, giữ bản cũ và bỏ tệp cạnh exe", "confirmed rules import keeps a backup and ignores a file beside the executable"); } }
            public static string RulesCorrupt { get { return T("rules.json hỏng thì dùng lại mặc định", "a corrupt rules.json falls back to the defaults"); } }
            public static string Scoring { get { return T("cộng điểm theo trọng số và chặn ở 10", "sum scores by weight and cap at 10"); } }
            public static string DetectorPipeline { get { return T("detector 2.0 bắt đuôi kép, RLO, NTLM, desktop.ini, script gốc và chỉ gắn cờ macro", "2.0 detectors catch double extension, RLO, NTLM, desktop.ini, root scripts and only flag macros"); } }
            public static string EventTimeline { get { return T("ghi, đọc và xuất dòng thời gian", "write, read and export the event timeline"); } }
            public static string DeviceTrustStore { get { return T("lưu, đọc và quên trạng thái tin cậy theo khoá ổn định", "store, read and forget device trust by stable key"); } }
            public static string RepairGuards { get { return T("sửa ổ từ chối ổ hệ thống và đường dẫn không hợp lệ", "drive repair refuses the system drive and invalid paths"); } }
            public static string RepairReadTest { get { return T("đọc thử chỉ đọc trên thư mục tạm", "read-only test reads files in the temporary folder"); } }
            public static string RepairBackupMarker { get { return T("marker sao lưu phải khớp serial và còn hiệu lực", "the backup marker must match the volume serial and still be valid"); } }
            public static string RepairBackupExpiry { get { return T("marker sao lưu quá 24 giờ bị từ chối", "a backup marker older than 24 hours is refused"); } }
            public static string SysExe { get { return T("công cụ hệ thống dùng đường dẫn tuyệt đối", "system tools resolve to absolute paths"); } }
            public static string UninstallOwnership { get { return T("gỡ cài đặt chỉ xoá thư mục có dấu hiệu sở hữu và giữ cách ly khi được yêu cầu", "uninstall only removes owned folders and keeps quarantine on request"); } }
            public static string RestoreGuard { get { return T("khôi phục từ chối nguồn ngoài lần cách ly và đích trong thư mục hệ thống", "restore refuses sources outside the batch and targets in system folders"); } }
            public static string RepairPreviewCount { get { return T("đếm tệp dự phòng cho xem trước sao lưu", "fallback file count for the backup preview"); } }
            public static string FakeFolderKeepsDocs { get { return T("thư mục giả: chỉ cách ly tệp chạy được, giữ tài liệu người dùng", "fake folder: quarantine only executable payloads and keep user documents"); } }
            public static string Base64Name { get { return T("khôi phục tên Base64: chỉ nhận tài liệu hợp lệ, không ghi đè, không chạm payload", "Base64 names: only valid documents, no overwrite, payload untouched"); } }
            public static string FolderScan { get { return T("quét một thư mục con: nhận diện tệp và đếm tài liệu giữ lại", "scan a subfolder: detects files and counts kept documents"); } }
            public static string MissingIsNotError { get { return T("tệp đã biến mất không bị tính là lỗi cách ly", "a file that already vanished is not counted as a quarantine error"); } }
            public static string CleanLockExclusive { get { return T("chỉ một luồng được dọn một ổ tại một thời điểm", "only one thread may clean a drive at a time"); } }
            public static string EmptyBatchRemoved { get { return T("không để lại vỏ cách ly rỗng khi không di chuyển được tệp nào", "no empty quarantine batch is left when nothing was moved"); } }
            public static string RegExportChecked { get { return T("reg export thất bại thì không sửa khoá đăng ký", "a failed reg export blocks any registry change"); } }
            public static string CsvInjection { get { return T("xuất CSV vô hiệu hoá ô bắt đầu bằng = + - @", "CSV export neutralises cells that start with = + - @"); } }
            public static string UnhideScope { get { return T("chỉ gỡ ẩn mục Hidden+System, giữ desktop.ini và mục người dùng tự ẩn", "only unhide Hidden+System items and keep desktop.ini and user-hidden items"); } }
            public static string TaskState
            {
                get
                {
                    return T("tác vụ đã tắt không báo lại, tác vụ bật và lệnh đáng ngờ thì báo",
                             "a disabled task is not reported again; an enabled task with a risky command is");
                }
            }
            public static string ReinfectLoop
            {
                get
                {
                    return T("báo nhiễm lại khi cùng một ổ bị dọn 3 lần trong 10 phút, không báo lặp",
                             "report re-infection when one drive is cleaned 3 times in 10 minutes, without repeats");
                }
            }
            public static string TaskMicrosoft
            {
                get
                {
                    return T("tác vụ dưới \\Microsoft\\... chỉ báo khi lệnh trỏ vào thư mục người dùng hoặc script host",
                             "a task under \\Microsoft\\... is reported only when its command targets a user folder or a script host");
                }
            }
            public static string ServicePath
            {
                get
                {
                    return T("dịch vụ/driver ngoài Windows và Program Files bị báo, đường dẫn hệ thống và Windows Defender được bỏ qua",
                             "a service/driver outside Windows and Program Files is reported; system paths and Windows Defender are skipped");
                }
            }
            public static string FixOrder
            {
                get
                {
                    return T("sửa theo thứ tự: tiến trình, cơ chế khởi động, rồi tệp",
                             "fix order: processes, then startup mechanisms, then files");
                }
            }
            public static string SignatureVerify
            {
                get
                {
                    return T("tệp chưa ký là Unsigned, tệp hệ thống có chữ ký hợp lệ là Signed",
                             "an unsigned file is Unsigned; a signed system file is Signed");
                }
            }
            public static string ProcessPath
            {
                get
                {
                    return T("đọc được đường dẫn tiến trình 64-bit, PID hệ thống không gây lỗi",
                             "read a 64-bit process path; the system PID does not throw");
                }
            }
            public static string KeyboardBus
            {
                get
                {
                    return T("phân loại bàn phím trong/ngoài theo bus ACPI, USB, Bluetooth, ROOT, SWD và composite",
                             "classify internal/external keyboards by ACPI, USB, Bluetooth, ROOT, SWD and composite bus");
                }
            }
            public static string ScoreGate
            {
                get
                {
                    return T("dấu hiệu mạnh nhưng dưới ngưỡng điểm thì không còn là mục High",
                             "a strong signal below the score threshold is no longer High");
                }
            }
            public static string DeepWalk
            {
                get
                {
                    return T("cây sâu hơn 300 ký tự vẫn được quét tới tệp cuối",
                             "a tree deeper than 300 characters is still scanned to the last file");
                }
            }
            public static string SignatureCache
            {
                get
                {
                    return T("tệp không phải PE không gọi WinVerifyTrust và được nhớ trong cache",
                             "a non-PE file skips WinVerifyTrust and is remembered in the cache");
                }
            }
            public static string MacroTail
            {
                get
                {
                    return T("macro trong 1 MB cuối của tệp lớn vẫn được nhận ra",
                             "a macro in the last 1 MB of a large file is still found");
                }
            }
            public static string AdsNtfs
            {
                get
                {
                    return T("chỉ dò luồng ẩn trên NTFS, bỏ qua trên FAT32/exFAT",
                             "hidden streams are only probed on NTFS, skipped on FAT32/exFAT");
                }
            }
            public static string WriteProtectPending
            {
                get
                {
                    return T("ghi chú mở khoá hết hạn hoặc hỏng thì tự bật lại chặn ghi",
                             "an expired or corrupt unlock note restores write protection");
                }
            }
            public static string SmartRescan
            {
                get
                {
                    return T("300 tệp .txt không kích hoạt quét, 30 .lnk thì quét; báo lại sau 10 phút",
                             "300 .txt files do not trigger a scan, 30 .lnk files do; alerts repeat after 10 minutes");
                }
            }
            public static string ChkdskStdin
            {
                get
                {
                    return T("chkdsk nhận câu trả lời dismount qua stdin thay vì chờ vô hạn",
                             "chkdsk receives the dismount answer on stdin instead of waiting forever");
                }
            }
            public static string EventQueue
            {
                get
                {
                    return T("hàng đợi sự kiện có ID: đọc từ ID cũ chỉ trả mục mới, xoay tệp giữ con trỏ, dòng cũ vẫn đọc được",
                             "event queue with IDs: reading from an old ID returns only new entries, rotation keeps the cursor, legacy rows still parse");
                }
            }
            public static string VirusTotal
            {
                get
                {
                    return T("VirusTotal chỉ nhận hash trong URL + x-apikey, 4 yêu cầu/phút; hash/khoá sai thì không gửi",
                             "VirusTotal gets only the hash in the URL + x-apikey, 4 requests/minute; a bad hash or key sends nothing");
                }
            }
            public static string VirusTotalKey
            {
                get
                {
                    return T("khoá VirusTotal mã hoá DPAPI, không nằm trong settings/log; tắt công tắc thì không gửi",
                             "VirusTotal key is DPAPI-encrypted and absent from settings/log; switching off sends nothing");
                }
            }
            public static string CliJson
            {
                get
                {
                    return T("--scan --json đọc lại được, thoát ký tự đúng; --help liệt kê --scan/--vt/--json/--version",
                             "--scan --json parses back with correct escaping; --help lists --scan/--vt/--json/--version");
                }
            }
            public static string InputRate
            {
                get
                {
                    return T("bộ đếm tốc độ gõ chỉ đếm sự kiện, không đọc mã phím; burst 100 sự kiện/2 giây vượt ngưỡng, gõ 6/giây thì không",
                             "typing-rate counter counts events only, never key codes; a 100-event/2s burst crosses the threshold, 6/s does not");
                }
            }
            public static string BadUsb
            {
                get
                {
                    return T("BadUSB chỉ báo khi đủ: thiết bị mới/chưa tin cậy + gõ nhanh + shell trong cửa sổ + cha explorer; thiết bị tin cậy, shell ngoài cửa sổ, công tắc tắt hoặc cooldown thì không báo",
                             "BadUSB warns only when all hold: new/untrusted device + fast typing + shell in the window + explorer parent; trusted device, late shell, disabled switch or cooldown do not warn");
                }
            }
            public static string BadUsbConfig
            {
                get
                {
                    return T("rules.json có mục badusb phiên bản 6; giá trị âm/quá lớn bị kẹp về khoảng an toàn",
                             "rules.json has a version-6 badusb section; negative or huge values are clamped to a safe range");
                }
            }
        }

        // Biểu tượng khay hệ thống của bản chạy nền.
        public static class Tray
        {
            public static string TooltipOn
            {
                get { return T("ShieldGate — đang bảo vệ máy", "ShieldGate — protecting this computer"); }
            }
            public static string TooltipOff
            {
                get { return T("ShieldGate — bảo vệ nền đã dừng", "ShieldGate — background protection has stopped"); }
            }
            public static string StatusOn { get { return T("Bảo vệ đang chạy", "Protection is running"); } }
            public static string StatusOff { get { return T("Bảo vệ đã dừng", "Protection has stopped"); } }
            public static string Open { get { return T("Mở ShieldGate", "Open ShieldGate"); } }
            public static string Exit { get { return T("Ẩn biểu tượng", "Hide icon"); } }
            public static string Balloon
            {
                get
                {
                    return T("Bảo vệ nền đang chạy. Nhấn đúp biểu tượng để mở ShieldGate.",
                             "Background protection is running. Double-click the icon to open ShieldGate.");
                }
            }
            public static string AlreadyOpen { get { return T("ShieldGate đang mở.", "ShieldGate is already open."); } }
            public static string DeviceNew(string name)
            {
                return T("Thiết bị mới vừa kết nối: " + name, "A new device was connected: " + name);
            }
            public static string Behavior(string drive)
            {
                return T("Ghi bất thường trên " + drive + ". Đang kiểm tra lại.",
                         "Unusual write activity on " + drive + ". Checking the drive again.");
            }
            public static string Attention(int n)
            {
                return T("Có " + n + " mục trên USB cần bạn xem lại. Nhấn để mở ShieldGate.",
                         n + " item(s) on a USB drive need your review. Click to open ShieldGate.");
            }
            public static string Cleaned(string drive, int n)
            {
                return T("Đã cách ly " + n + " mục trên ổ " + drive + ".",
                         "Quarantined " + n + " item(s) on " + drive + ".");
            }
            public static string CleanedReview(string drive, int cleaned, int review)
            {
                return T("Đã cách ly " + cleaned + " mục trên ổ " + drive + ", còn " + review + " mục cần xem lại.",
                         "Quarantined " + cleaned + " item(s) on " + drive + "; " + review + " item(s) still need review.");
            }
            public static string Reinfected(string drive)
            {
                return T("Ổ " + drive + " liên tục bị nhiễm lại. Mã độc đang chạy trên máy — mở ShieldGate và bấm Kiểm tra máy.",
                         drive + " keeps getting re-infected. Malware is running on this computer — open ShieldGate and run Check this PC.");
            }
            public static string ReinfectedAfterFix(string drive)
            {
                return T("Đã sửa máy nhưng ổ " + drive + " vẫn nhiễm lại. Nguồn lây có thể là driver/rootkit — hãy quét Microsoft Defender Offline.",
                         "The PC was fixed but drive " + drive + " is still re-infected. The source may be a driver/rootkit — run Microsoft Defender Offline.");
            }
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
            public static string UninstallDone { get { return T("Đã gỡ ShieldGate và xoá dữ liệu của nó.", "ShieldGate removed and its data deleted."); } }
            public static string UninstallPending { get { return T("Đã tắt bảo vệ. Phần tệp còn lại sẽ được dọn sau khi tiến trình thoát.", "Protection turned off. The remaining files will be cleaned up after this process exits."); } }
            public static string UninstallKept
            {
                get
                {
                    return T("Đã gỡ ShieldGate. Cách ly và bản sao lưu ổ vẫn còn trong thư mục dữ liệu.",
                             "ShieldGate removed. Quarantine and drive backups are still in the data folder.");
                }
            }
            public static string ProtectOffDone { get { return T("Đã tắt bảo vệ. Tệp cách ly và nhật ký vẫn được giữ.", "Protection turned off. Quarantined files and logs are kept."); } }
            public static string NoDriveSelected { get { return T("Chưa chọn ổ đĩa.", "No drive selected."); } }
            public static string StatusTitle { get { return T("Trạng thái ShieldGate", "ShieldGate status"); } }
            public static string StatusProtect(bool on) { return T("Bảo vệ: " + (on ? "đang bật" : "đang tắt"), "Protection: " + (on ? "on" : "off")); }
            public static string StatusAutorun(bool on) { return T("Chặn tự chạy: " + (on ? "bật" : "tắt"), "AutoRun block: " + (on ? "on" : "off")); }
            public static string StatusIni(bool on) { return T("Chặn autorun.inf: " + (on ? "bật" : "tắt"), "autorun.inf block: " + (on ? "on" : "off")); }
            public static string StatusWsh(bool on) { return T("Chặn Windows Script Host: " + (on ? "bật" : "tắt"), "Windows Script Host block: " + (on ? "on" : "off")); }
            public static string StatusRegistry(bool on) { return T("Bảo vệ Registry: " + (on ? "bật" : "tắt"), "Registry protection: " + (on ? "on" : "off")); }
            public static string StatusGuard(bool on) { return T("Chạy nền: " + (on ? "đang chạy" : "không chạy"), "Background: " + (on ? "running" : "not running")); }
            public static string StatusAdmin(bool on) { return T("Quyền quản trị: " + (on ? "có" : "không"), "Administrator rights: " + (on ? "yes" : "no")); }
            public static string StatusQuarantine(int files, int batches)
            {
                return T("Tệp đang cách ly: " + files + " trong " + batches + " lần",
                         "Files in quarantine: " + files + " in " + batches + " batches");
            }
            public static string ScanUsage { get { return T("Cách dùng: ShieldGate.exe --scan E:\\ hoặc --scan \"D:\\thu muc\"", "Usage: ShieldGate.exe --scan E:\\ or --scan \"D:\\folder\""); } }
            public static string DevicesUsage { get { return T("Cách dùng: ShieldGate.exe --devices", "Usage: ShieldGate.exe --devices"); } }
            public static string EventsUsage { get { return T("Cách dùng: ShieldGate.exe --events [số dòng]", "Usage: ShieldGate.exe --events [count]"); } }
            public static string RepairUsage { get { return T("Cách dùng: ShieldGate.exe --repair E:\\", "Usage: ShieldGate.exe --repair E:\\"); } }
            public static string DefenderUsage { get { return T("Cách dùng: ShieldGate.exe --defender <tệp hoặc thư mục>", "Usage: ShieldGate.exe --defender <file or folder>"); } }
            public static string ImportRulesUsage
            {
                get { return T("Cách dùng: ShieldGate.exe --import-rules <rules.json> --yes",
                               "Usage: ShieldGate.exe --import-rules <rules.json> --yes"); }
            }
            public static string ImportRulesConfirm(string path)
            {
                return T("Nhập luật từ " + path + " sẽ thay tệp luật trong thư mục dữ liệu (bản cũ giữ ở rules.json.bak). Thêm --yes để xác nhận.",
                         "Importing " + path + " replaces the rules file in the data folder (the old one is kept as rules.json.bak). Add --yes to confirm.");
            }
            public static string ImportRulesDone(string path)
            {
                return T("Đã nhập luật vào " + path + ".", "Rules imported into " + path + ".");
            }
            public static string ImportRulesMissing(string path)
            {
                return T("Không thấy tệp luật: " + path, "Rules file not found: " + path);
            }
            public static string ImportRulesTooBig
            {
                get { return T("Tệp luật quá lớn (giới hạn 1 MB).", "Rules file is too large (1 MB limit)."); }
            }
            public static string ImportRulesInvalid(string detail)
            {
                return T("Tệp luật không đọc được: " + detail, "Could not read the rules file: " + detail);
            }
            public static string DevicesTitle { get { return T("Thiết bị ngoại vi:", "Peripheral devices:"); } }
            public static string NoDevices { get { return T("Không thấy thiết bị ngoại vi nào.", "No peripheral device found."); } }
            public static string DeviceLine(string name, string type, string trust, string drives)
            {
                return "  " + name + " · " + type + " · " + trust + (drives.Length > 0 ? " · " + drives : "");
            }
            public static string EventsTitle { get { return T("Sự kiện gần nhất:", "Recent events:"); } }
            public static string NoEvents { get { return T("Chưa có sự kiện nào.", "No events yet."); } }
            public static string EventLine(string time, string kind, string level, string subject, string detail)
            {
                return "  " + time + " [" + level + "] " + kind + " · " + subject
                       + (detail.Length > 0 ? " · " + detail : "");
            }
            public static string RepairSummary(string drive, string fs, long free, long total, bool readOnly, bool system)
            {
                return T(drive + ": " + (fs.Length > 0 ? fs : "?") + " · trống " + S.Repair.Size(free) + "/" + S.Repair.Size(total)
                         + (readOnly ? " · chỉ đọc" : "") + (system ? " · Ổ HỆ THỐNG (từ chối ghi)" : ""),
                         drive + ": " + (fs.Length > 0 ? fs : "?") + " · free " + S.Repair.Size(free) + "/" + S.Repair.Size(total)
                         + (readOnly ? " · read-only" : "") + (system ? " · SYSTEM DRIVE (writes refused)" : ""));
            }
            public static string RepairProblem(string detail) { return T("Vấn đề: " + detail, "Problem: " + detail); }
            public static string DefenderResult(bool clean, int code)
            {
                return T(clean ? "Windows Defender: không phát hiện gì." : "Windows Defender trả mã " + code + ".",
                         clean ? "Windows Defender: nothing found." : "Windows Defender returned code " + code + ".");
            }
            public static string StatusWriteProtect(bool on) { return T("Chặn ghi USB: " + (on ? "bật" : "tắt"), "USB write protection: " + (on ? "on" : "off")); }
            public static string StatusBlockExec(bool on) { return T("Chặn chạy từ ổ rời: " + (on ? "bật" : "tắt"), "Removable execution block: " + (on ? "on" : "off")); }
            public static string VtUsage { get { return T("Cách dùng: ShieldGate.exe --vt <tệp>", "Usage: ShieldGate.exe --vt <file>"); } }
            public static string VtMissing(string path) { return T("Không thấy tệp: " + path, "File not found: " + path); }
            public static string VtOff
            {
                get
                {
                    return T("VirusTotal đang tắt. Bật trong trang Bảo vệ và lưu khoá API trước.",
                             "VirusTotal is off. Turn it on in the Protect page and save an API key first.");
                }
            }
            public static string VtHashFailed(string path)
            {
                return T("Không đọc được tệp để băm: " + path, "Could not read the file to hash: " + path);
            }
            public static string VtHash(string hash) { return T("SHA-256: " + hash, "SHA-256: " + hash); }
            public static string VtResult(int malicious, int suspicious, int total)
            {
                return T("VirusTotal: " + malicious + " ác ý, " + suspicious + " đáng ngờ trong " + total + " hãng. Chỉ để tham khảo.",
                         "VirusTotal: " + malicious + " malicious, " + suspicious + " suspicious out of " + total + " engines. Advisory only.");
            }
            public static string Help
            {
                get
                {
                    return T(
                        "ShieldGate " + AppInfo.Version + " — bảo vệ thiết bị ngoại vi\n" +
                        "\n" +
                        "  ShieldGate.exe                      mở giao diện\n" +
                        "  --install | --uninstall [--keep-data|--purge-data]\n" +
                        "  --watch | --tray | --status\n" +
                        "  --scan <ổ hoặc thư mục> [--json]    quét, --json cho script\n" +
                        "  --repair <ổ>                        xem và sửa lỗi ổ\n" +
                        "  --defender <tệp hoặc thư mục>       nhờ Windows Defender quét\n" +
                        "  --devices | --events [số dòng]\n" +
                        "  --vt <tệp>                          tra SHA-256 trên VirusTotal (phải bật)\n" +
                        "  --import-rules <rules.json> --yes\n" +
                        "  --selftest | --version | --help\n" +
                        "\n" +
                        "Mã thoát: 0 bình thường, 1 lỗi, 2 có mục cần xử lý (--scan, --vt).\n" +
                        "Không có lời gọi mạng nào trừ khi bạn bật VirusTotal và chạy --vt.",
                        "ShieldGate " + AppInfo.Version + " — peripheral device protection\n" +
                        "\n" +
                        "  ShieldGate.exe                      open the window\n" +
                        "  --install | --uninstall [--keep-data|--purge-data]\n" +
                        "  --watch | --tray | --status\n" +
                        "  --scan <drive or folder> [--json]   scan; --json for scripts\n" +
                        "  --repair <drive>                    inspect and repair a drive\n" +
                        "  --defender <file or folder>         ask Windows Defender to scan\n" +
                        "  --devices | --events [count]\n" +
                        "  --vt <file>                         look up the SHA-256 on VirusTotal (must be on)\n" +
                        "  --import-rules <rules.json> --yes\n" +
                        "  --selftest | --version | --help\n" +
                        "\n" +
                        "Exit codes: 0 normal, 1 error, 2 items need attention (--scan, --vt).\n" +
                        "No network call happens unless you turn on VirusTotal and run --vt.");
                }
            }
            public static string Usage
            {
                get
                {
                    return "ShieldGate.exe --watch | --status | --install | --uninstall [--keep-data|--purge-data] | --scan <drive or folder> [--json] | --devices | --events [n] | --repair <drive> | --defender <path> | --vt <file> | --import-rules <rules.json> --yes | --selftest | --version | --help";
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
            public static string ScanTruncated(int files)
            {
                return T("Đã xem " + files.ToString("#,0") + " tệp và dừng ở giới hạn an toàn; một phần ổ chưa được kiểm tra.",
                         "Scanned " + files.ToString("#,0") + " files and stopped at the safety limit; part of the drive was not checked.");
            }
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
