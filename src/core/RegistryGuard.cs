// ShieldGate - kiểm tra và sửa các khoá Registry quan trọng của File Explorer.
// Virus shortcut hay phá các khoá mở thư mục, mở .exe và chế độ hiện tệp ẩn.
// Mọi lần sửa đều được reg.exe sao lưu trước (xem PcScan.BackupReg).
using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace ShieldGate
{
    internal sealed class RegIssue
    {
        public RegistryHive Hive;
        public string Path;
        public string Name;
        public object Expected;
        public RegistryValueKind Kind;
        public bool Remove;
        public bool High;
        public string Code;
        public string Actual;

        public string Title { get { return RegistryGuard.Title(Code); } }
    }

    internal static class RegistryGuard
    {
        // GUID Windows dùng để mở thư mục bằng File Explorer (DelegateExecute).
        public const string FolderDelegate = "{11dbb47c-a525-400b-9e80-a54615a090c0}";

        private const string Classes = @"SOFTWARE\Classes\";
        private const string ExeCommand = "\"%1\" %*";

        // Bảo vệ nền có thể gặp lại đúng một khoá bị phá nhiều lần. Chỉ sao lưu
        // lần đầu mỗi khoá trong một phiên chạy để backup không phình ra.
        private static readonly HashSet<string> BackedUp =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private sealed class Check
        {
            public RegistryHive Hive;
            public string Path;
            public string Name;
            public object Expected;
            public RegistryValueKind Kind;
            public bool Remove;   // true: giá trị này phải không tồn tại
            public bool High;     // đủ chắc để bảo vệ nền tự sửa
            public bool Win10;    // chỉ kiểm tra từ Windows 10
            public bool User;     // bản ghi đè HKCU: thiếu giá trị là bình thường
            public string Code;
        }

        private static Check C(RegistryHive hive, string path, string name, object expected,
                               RegistryValueKind kind, bool high, bool remove, bool win10, string code)
        {
            Check c = new Check();
            c.Hive = hive;
            c.Path = path;
            c.Name = name;
            c.Expected = expected;
            c.Kind = kind;
            c.High = high;
            c.Remove = remove;
            c.Win10 = win10;
            c.Code = code;
            return c;
        }

        private static bool IsWin10OrLater()
        {
            // Không dùng Environment.OSVersion: bản build thiếu manifest sẽ báo 6.x.
            object value;
            RegistryValueKind kind;
            if (Reg.Read(RegistryHive.LocalMachine,
                         @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                         "CurrentMajorVersionNumber", out value, out kind))
                return ToInt(value) >= 10;
            return false;
        }

        // Danh sách kiểm tra cố định, chỉ các khoá ảnh hưởng trực tiếp tới File Explorer.
        private static List<Check> Checks()
        {
            List<Check> list = new List<Check>();

            // Liên kết tệp chạy: virus đổi .exe sang tệp khác là cách phổ biến để tự chạy.
            list.Add(C(RegistryHive.LocalMachine, Classes + ".exe", "", "exefile",
                       RegistryValueKind.String, true, false, false, "exe-assoc"));
            list.Add(C(RegistryHive.LocalMachine, Classes + @"exefile\shell\open\command", "", ExeCommand,
                       RegistryValueKind.String, true, false, false, "exe-command"));

            // Mở thư mục bằng File Explorer. Từ Windows 10, DelegateExecute phải có
            // đúng GUID; thiếu giá trị này là đúng kiểu hỏng trong ảnh người dùng gửi.
            list.Add(C(RegistryHive.LocalMachine, Classes + @"Folder\shell\open\command", "",
                       @"%SystemRoot%\Explorer.exe", RegistryValueKind.ExpandString,
                       true, false, true, "folder-command"));
            list.Add(C(RegistryHive.LocalMachine, Classes + @"Folder\shell\open\command", "DelegateExecute",
                       FolderDelegate, RegistryValueKind.String, true, false, true, "folder-delegate"));

            // Virus hay đặt CheckedValue = 0 để nút "hiện tệp ẩn" không có tác dụng.
            list.Add(C(RegistryHive.LocalMachine,
                       @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\Folder\Hidden\SHOWALL",
                       "CheckedValue", 1, RegistryValueKind.DWord, true, false, false, "show-hidden"));

            // Policy khoá chức năng sửa chữa. Chỉ báo để người dùng chọn, không tự xoá,
            // vì máy công ty có thể đặt các policy này có chủ ý.
            string pol = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
            list.Add(C(RegistryHive.CurrentUser, pol, "NoFolderOptions", 0,
                       RegistryValueKind.DWord, false, true, false, "no-folder-options"));
            list.Add(C(RegistryHive.CurrentUser, pol, "NoViewContextMenu", 0,
                       RegistryValueKind.DWord, false, true, false, "no-context-menu"));
            string sys = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
            list.Add(C(RegistryHive.CurrentUser, sys, "DisableTaskMgr", 0,
                       RegistryValueKind.DWord, false, true, false, "no-taskmgr"));
            list.Add(C(RegistryHive.CurrentUser, sys, "DisableRegistryTools", 0,
                       RegistryValueKind.DWord, false, true, false, "no-regedit"));

            // Khoá HKCU\Software\Classes ghi đè HKLM. Nếu virus để lại bản ghi đè hỏng,
            // sửa HKLM thôi không đủ: phải sửa/xoá cả bản của người dùng.
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                Check c = list[i];
                if (c.Hive != RegistryHive.LocalMachine) continue;
                if (!c.Path.StartsWith(Classes, StringComparison.OrdinalIgnoreCase)) continue;
                string userPath = @"Software\Classes\" + c.Path.Substring(Classes.Length);
                if (!Reg.KeyExists(RegistryHive.CurrentUser, userPath)) continue;

                Check u = C(RegistryHive.CurrentUser, userPath, c.Name, c.Expected,
                            c.Kind, c.High, c.Remove, c.Win10, c.Code);
                u.User = true;
                list.Add(u);
            }
            return list;
        }

        public static List<RegIssue> Detect()
        {
            List<RegIssue> found = new List<RegIssue>();
            bool win10 = IsWin10OrLater();
            foreach (Check c in Checks())
            {
                if (c.Win10 && !win10) continue;
                object value;
                RegistryValueKind kind;
                bool exists = Reg.Read(c.Hive, c.Path, c.Name, out value, out kind);

                if (c.Remove)
                {
                    if (exists && ToInt(value) != 0) found.Add(Make(c, Convert.ToString(value)));
                    continue;
                }
                if (!exists)
                {
                    // Khoá HKCU chỉ ghi đè khi có giá trị; thiếu thì Windows dùng HKLM.
                    if (!c.User) found.Add(Make(c, ""));
                    continue;
                }
                if (!Matches(c, value)) found.Add(Make(c, Convert.ToString(value)));
            }
            return found;
        }

        private static RegIssue Make(Check c, string actual)
        {
            RegIssue i = new RegIssue();
            i.Hive = c.Hive;
            i.Path = c.Path;
            i.Name = c.Name;
            i.Expected = c.Expected;
            i.Kind = c.Kind;
            i.Remove = c.Remove;
            i.High = c.High;
            i.Code = c.Code;
            i.Actual = actual;
            return i;
        }

        private static int ToInt(object value)
        {
            try { return Convert.ToInt32(value); }
            catch { return 0; }
        }

        private static bool Matches(Check c, object value)
        {
            if (c.Kind == RegistryValueKind.DWord) return ToInt(value) == ToInt(c.Expected);
            string actual = (Convert.ToString(value) ?? "").Trim();
            string expected = (Convert.ToString(c.Expected) ?? "").Trim();
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        // Chỉ tự sửa các mục đủ chắc (High). Mục yếu để người dùng quyết định ở lần
        // kiểm tra máy, tránh ghi đè lựa chọn hợp lệ của người dùng.
        public static bool Ensure(bool repair)
        {
            List<RegIssue> issues = Detect();
            if (issues.Count == 0) return true;

            bool ok = true;
            int fixedCount = 0;
            foreach (RegIssue i in issues)
            {
                if (!i.High) continue;
                if (!repair) { ok = false; continue; }
                string backupKey = i.Hive + "|" + i.Path + "|" + i.Name;
                bool first;
                lock (BackedUp) first = BackedUp.Add(backupKey);
                if (first) PcScan.BackupReg(i.Hive, i.Path);
                if (Write(i)) fixedCount++;
                else
                {
                    ok = false;
                    Log.Write(S.Log.RegistryRepairFailed(i.Title), "ERROR");
                }
            }
            if (fixedCount > 0) Log.Write(S.Log.RegistryRepaired(fixedCount), "WARN");
            return ok;
        }

        public static bool Write(RegIssue i)
        {
            try
            {
                if (i.Remove) return Reg.DeleteValue(i.Hive, i.Path, i.Name);
                return Reg.Write(i.Hive, i.Path, i.Name, i.Expected, i.Kind);
            }
            catch { return false; }
        }

        public static string Title(string code)
        {
            switch (code)
            {
                case "exe-assoc": return S.Registry.ExeAssoc;
                case "exe-command": return S.Registry.ExeCommand;
                case "folder-command": return S.Registry.FolderCommand;
                case "folder-delegate": return S.Registry.FolderDelegate;
                case "show-hidden": return S.Registry.ShowHidden;
                case "no-folder-options": return S.Registry.NoFolderOptions;
                case "no-context-menu": return S.Registry.NoContextMenu;
                case "no-taskmgr": return S.Registry.NoTaskMgr;
                case "no-regedit": return S.Registry.NoRegistryTools;
                default: return code;
            }
        }

        // Mô tả kỹ thuật cho danh sách kết quả: khoá, giá trị hiện tại và giá trị đúng.
        public static string Detail(RegIssue i)
        {
            string path = i.Path + "\\" + (i.Name.Length == 0 ? "(Default)" : i.Name);
            if (i.Remove) return path + " = " + i.Actual;
            string actual = i.Actual.Length == 0 ? S.Registry.Missing : i.Actual;
            return path + " = " + actual + "  ->  " + Convert.ToString(i.Expected);
        }
    }
}
