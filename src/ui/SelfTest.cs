// ShieldGate - tự kiểm tra lõi: dựng USB giả trong thư mục tạm rồi chạy đủ vòng.
// Không đụng tới USB thật, không đổi cấu hình máy. Dùng khi phát hành hoặc sau khi sửa lõi.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ShieldGate
{
    internal static class SelfTest
    {
        public static int Run()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "shieldgate-selftest-" + System.Diagnostics.Process.GetCurrentProcess().Id);
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch { }

            // Phải đặt trước lần đầu chạm tới Paths: thư mục dữ liệu tách khỏi bản thật.
            Environment.SetEnvironmentVariable("SHIELDGATE_HOME", root);

            int passed = 0;
            int total = 19;
            string firstError = "";

            Action<string, bool> check = delegate(string what, bool ok)
            {
                if (ok) passed++;
                else if (firstError.Length == 0) firstError = what;
                Console.WriteLine(S.Cli.SelftestStep(what, ok));
            };

            Console.WriteLine(S.Cli.SelftestTitle + " " + AppInfo.Version);
            Console.WriteLine(S.Cli.TempDir(root));

            string usb = Path.Combine(root, "usb");
            string script = Path.Combine(usb, "virus.vbs");
            string autorun = Path.Combine(usb, "autorun.inf");
            string badLink = Path.Combine(usb, "virus.lnk");
            string goodLink = Path.Combine(usb, "tai-lieu.lnk");
            string photo = Path.Combine(usb, "anh.jpg");

            try
            {
                Directory.CreateDirectory(usb);
                Directory.CreateDirectory(Path.Combine(usb, "Tai lieu"));

                File.WriteAllText(script,
                    "Set fso = CreateObject(\"Scripting.FileSystemObject\")\r\n" +
                    "Set sh = CreateObject(\"WScript.Shell\")\r\n" +
                    "sh.Run \"cmd.exe /c start virus.exe\", 0\r\n",
                    new UTF8Encoding(true));
                File.SetAttributes(script, FileAttributes.Hidden);

                File.WriteAllText(autorun,
                    "[autorun]\r\nopen=virus.exe\r\nshellexecute=virus.exe\r\n",
                    new UTF8Encoding(true));

                File.WriteAllText(photo, "anh thu", new UTF8Encoding(false));
                File.SetAttributes(photo, FileAttributes.Hidden);

                string wscript = Path.Combine(Environment.SystemDirectory, "wscript.exe");
                string notepad = Path.Combine(Environment.SystemDirectory, "notepad.exe");
                bool madeBad = MakeShortcut(badLink, wscript, "\"" + script + "\" //B");
                bool madeGood = MakeShortcut(goodLink, notepad, "");
                check(S.Selftest.FakeUsb, madeBad && madeGood);
            }
            catch (Exception e)
            {
                check(S.Selftest.FakeUsb, false);
                Console.WriteLine("  " + e.Message);
            }

            // 2. Nhận diện
            UsbScanResult result = UsbScan.Detect(usb, null, null);
            bool exactlyThree = result.Items.Count == 3;
            check(S.Selftest.Detected, exactlyThree);

            // 3. Shortcut sạch không bị bắt
            bool goodCaught = false;
            foreach (UsbItem i in result.Items)
                if (string.Equals(i.Path, goodLink, StringComparison.OrdinalIgnoreCase)) goodCaught = true;
            check(S.Selftest.NoFalsePositive, !goodCaught);

            // 4. Huỷ được lần kiểm tra
            UsbScanResult cancelled = UsbScan.Detect(usb, null, delegate { return true; });
            check(S.Selftest.Cancelled, cancelled.Cancelled);

            // 5. Cách ly
            int failed, stopped;
            string batch;
            int done = UsbScan.Apply(result, result.Items, out failed, out batch, out stopped, null);
            bool moved = !File.Exists(script) && !File.Exists(autorun) && !File.Exists(badLink);
            check(S.Selftest.Quarantined, done == 3 && failed == 0 && moved);

            // 6. Vùng cách ly có lần chạy
            bool hasBatch = Directory.Exists(batch) && Quarantine.ListBatches().Count > 0;
            check(S.Selftest.Batch, hasBatch);

            // 7. Khôi phục
            int restored, skipped, missing;
            Quarantine.RestoreBatch(batch, out restored, out skipped, out missing);
            bool back = File.Exists(script) && File.Exists(autorun) && File.Exists(badLink);
            check(S.Selftest.Restored, restored == 3 && missing == 0 && back);

            // 8. Xoá lần cách ly sau khi khôi phục đủ
            bool removed = missing == 0 && Quarantine.RemoveBatch(batch) && !Directory.Exists(batch);
            check(S.Selftest.BatchRemoved, removed);

            // 9. Bỏ thuộc tính ẩn
            int unhidden = UsbScan.Unhide(usb, null);
            bool visible = (File.GetAttributes(photo) & FileAttributes.Hidden) == 0;
            check(S.Selftest.Unhidden, unhidden > 0 && visible);

            // 9b. Thư mục không tên: chuyển dữ liệu ra, không ghi đè, xoá thư mục rỗng
            try
            {
                string usb2 = Path.Combine(root, "usb2");
                string ghost = Path.Combine(usb2, "\u00A0");
                Directory.CreateDirectory(Path.Combine(ghost, "Sub"));
                File.WriteAllText(Path.Combine(ghost, "a.txt"), "trong", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(ghost, "Sub", "b.txt"), "b", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(usb2, "a.txt"), "goc", new UTF8Encoding(false));
                File.SetAttributes(ghost, FileAttributes.Hidden | FileAttributes.System);
                List<string> found = UsbRestore.FindNameless(usb2);
                RestoreResult rr = UsbRestore.Restore(usb2, found, null);
                // Directory.Exists cắt khoảng trắng cuối tên nên luôn thấy thư mục cha;
                // kiểm tra bằng đường dẫn mở rộng của chính chương trình.
                bool ghostGone = !WinPath.Exists(WinPath.Ext(ghost));
                bool okRestore = found.Count == 1 && rr.Failed == 0 && rr.DirsRemoved == 1
                    && ghostGone
                    && File.ReadAllText(Path.Combine(usb2, "a.txt")) == "goc"
                    && File.Exists(Path.Combine(usb2, "a (1).txt"))
                    && File.Exists(Path.Combine(usb2, "Sub", "b.txt"));
                check(S.Selftest.RestoreNameless, okRestore);
            }
            catch (Exception e)
            {
                check(S.Selftest.RestoreNameless, false);
                Console.WriteLine("  " + e.Message);
            }

            // 9c. Cấu trúc virus thu thập được: LNK -> cmd /c "Docusment\1\1.exe"
            //     cùng thư mục giả mạo có desktop.ini CLSID và bộ payload.
            try
            {
                string usb3 = Path.Combine(root, "usb3");
                string fake = Path.Combine(usb3, "Docusment");
                Directory.CreateDirectory(Path.Combine(fake, "1"));
                Directory.CreateDirectory(Path.Combine(fake, "2"));
                File.WriteAllText(Path.Combine(fake, "desktop.ini"),
                    "[.ShellClassInfo]\r\nCLSID={88C6C381-2E85-11D0-94DE-444553540000}\r\n",
                    new UTF8Encoding(true));
                File.WriteAllBytes(Path.Combine(fake, "1", "1.exe"), new byte[2048]);
                File.WriteAllText(Path.Combine(fake, "1", "adback.dat"), "x");
                File.WriteAllText(Path.Combine(fake, "1", "link.dat"), "");
                File.WriteAllBytes(Path.Combine(fake, "1", "opera_elf.dll"), new byte[1024]);
                File.WriteAllBytes(Path.Combine(fake, "2", "icudtl.dat"), new byte[1024]);
                string driveLink = Path.Combine(usb3, "USB.lnk");
                string cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                bool madeDriveLink = MakeShortcut(driveLink, cmd,
                    "/q /c \"Docusment\\1\\1.exe 442 101\"");

                UsbScanResult fakeScan = UsbScan.Detect(usb3, null, null);
                bool sawShortcut = false, sawArgs = false, sawFake = false, fakeStrong = false;
                foreach (UsbItem it in fakeScan.Items)
                {
                    if (it.Kind == UsbKind.Shortcut)
                    {
                        sawShortcut = true;
                        if (it.Codes.Contains("args-relative-exe=Docusment\\1\\1.exe")) sawArgs = true;
                    }
                    if (it.Kind == UsbKind.FakeFolder && it.FolderName == "Docusment")
                    {
                        sawFake = true;
                        fakeStrong = it.High;
                    }
                }
                check(S.Selftest.FakeFolderDetected, madeDriveLink && sawShortcut && sawArgs && sawFake && fakeStrong);
            }
            catch (Exception e)
            {
                check(S.Selftest.FakeFolderDetected, false);
                Console.WriteLine("  " + e.Message);
            }

            // 9d. Lệnh cmd bọc payload bị bắt; shortcut hợp lệ không bị báo nhầm.
            try
            {
                CommandCheck bad = Detect.CommandVerdict(
                    "cmd.exe /q /c \"Docusment\\1\\1.exe 442 101\"");
                CommandCheck good = Detect.CommandVerdict(
                    "C:\\Users\\x\\AppData\\Local\\Discord\\Update.exe --processStart Discord.exe");
                check(S.Selftest.CommandVerdict, bad != null && bad.High && good == null);
            }
            catch (Exception e)
            {
                check(S.Selftest.CommandVerdict, false);
                Console.WriteLine("  " + e.Message);
            }

            // 9e. Sửa máy: cách ly thư mục giả + shortcut trên một gốc ổ giả.
            try
            {
                string pcroot = Path.Combine(root, "pcroot");
                string fake = Path.Combine(pcroot, "Docusment");
                Directory.CreateDirectory(Path.Combine(fake, "1"));
                File.WriteAllBytes(Path.Combine(fake, "1", "1.exe"), new byte[2048]);
                File.WriteAllText(Path.Combine(fake, "1", "adback.dat"), "x");
                File.WriteAllText(Path.Combine(fake, "1", "link.dat"), "");
                string lnk = Path.Combine(pcroot, "USB.lnk");
                File.WriteAllText(lnk, "dummy");

                List<PcItem> items = new List<PcItem>();
                PcItem folder = new PcItem();
                folder.Kind = PcKind.DriveFakeFolder;
                folder.High = true;
                folder.Arg1 = "Docusment";
                folder.Data["root"] = pcroot;
                folder.Data["folder"] = "Docusment";
                items.Add(folder);
                PcItem shortcut = new PcItem();
                shortcut.Kind = PcKind.DriveShortcut;
                shortcut.High = true;
                shortcut.Arg1 = "USB.lnk";
                shortcut.Data["path"] = lnk;
                shortcut.Data["root"] = pcroot;
                items.Add(shortcut);

                int ok, fail;
                PcScan.Fix(items, null, null, out ok, out fail);
                check(S.Selftest.PcFixWorks,
                      ok == 2 && fail == 0 && !Directory.Exists(fake) && !File.Exists(lnk));
            }
            catch (Exception e)
            {
                check(S.Selftest.PcFixWorks, false);
                Console.WriteLine("  " + e.Message);
            }

            // 10. Thiết lập lưu và đọc lại
            Settings.SetDriveScan("Z:\\", 123, 4);
            bool settingsOk = Settings.DriveScanFiles("Z:\\") == 123
                           && Settings.DriveScanFound("Z:\\") == 4
                           && Settings.DriveScanTime("Z:\\") > DateTime.Now.AddMinutes(-5);
            Log.Write(S.Log.SelfTest(usb));
            settingsOk = settingsOk && File.Exists(Paths.SettingsFile) && File.Exists(Paths.LogFile);
            check(S.Selftest.SettingsSaved, settingsOk);

            // 11. Tiến trình khác ghi thêm khóa thì lần ghi sau không xoá mất.
            bool merged = false;
            try
            {
                File.AppendAllText(Paths.SettingsFile, "other.process=1\r\n", new UTF8Encoding(false));
                Settings.Set("selftest.after", "2");
                merged = Settings.Get("other.process", "") == "1"
                      && Settings.Get("selftest.after", "") == "2"
                      && Settings.DriveScanFiles("Z:\\") == 123;
            }
            catch { }
            check(S.Selftest.SettingsMerge, merged);

            // 12. Lựa chọn bảo vệ được lưu và đọc lại đúng.
            bool configOk = false;
            try
            {
                Guard.SaveConfig(false, true, true);
                GuardConfig cfg = Guard.LoadConfig();
                configOk = !cfg.Autorun && cfg.Ini && cfg.Wsh;
            }
            catch { }
            check(S.Selftest.GuardConfig, configOk);

            // 13. Bản cách ly bị sửa thì không được ghi trả về ổ.
            bool refused = false;
            string tamper = Path.Combine(usb, "tamper.txt");
            string tamperBatch = "";
            try
            {
                File.WriteAllText(tamper, "ban goc", new UTF8Encoding(false));
                tamperBatch = Quarantine.NewBatch("test", "");
                string qerr;
                bool added = Quarantine.Add(tamper, usb, tamperBatch, "test", out qerr);
                bool edited = false;
                if (added)
                {
                    string[] qfiles = Directory.GetFiles(Path.Combine(tamperBatch, "files"),
                                                         "*.sgq", SearchOption.AllDirectories);
                    if (qfiles.Length == 1)
                    {
                        File.AppendAllText(qfiles[0], "x", new UTF8Encoding(false));
                        edited = true;
                    }
                }
                int tr, ts, tm;
                Quarantine.RestoreBatch(tamperBatch, out tr, out ts, out tm);
                refused = added && edited && tr == 0 && tm == 1 && !File.Exists(tamper);
            }
            catch { }
            if (tamperBatch.Length > 0) Quarantine.RemoveBatch(tamperBatch);
            check(S.Selftest.TamperRefused, refused);

            // 14. Chạm giới hạn số tệp thì báo đã cắt, không im lặng.
            bool truncated = false;
            try
            {
                WalkStats stats = new WalkStats();
                int walked = 0;
                foreach (TreeItem t in Tree.Walk(usb, stats, 2, 60)) walked++;
                truncated = stats.Truncated && walked <= 2;
            }
            catch { }
            check(S.Selftest.Truncation, truncated);

            // 15. Nhật ký đổi theo ngôn ngữ, không lẫn câu tiếng Việt khi chọn English.
            bool logEnglish = false;
            try
            {
                S.SetLanguage(Bootstrap.LanguageEn);
                logEnglish = S.Log.Title == "Log"
                          && S.Log.Quarantined("a", "b").StartsWith("Quarantined", StringComparison.Ordinal)
                          && S.Log.WatchStopped.IndexOf("dừng", StringComparison.Ordinal) < 0;
            }
            catch { }
            finally { S.SetLanguage(Bootstrap.LanguageVi); }
            check(S.Selftest.LogLanguage, logEnglish);

            try { Directory.Delete(root, true); }
            catch { }

            if (passed == total)
            {
                Console.WriteLine(S.Cli.SelftestPass(total));
                return 0;
            }
            Console.WriteLine(S.Cli.SelftestFail(passed, total, firstError));
            return 1;
        }

        // Tạo shortcut .lnk qua WScript.Shell, không cần thư viện ngoài.
        private static bool MakeShortcut(string path, string target, string arguments)
        {
            object shell = null;
            object link = null;
            try
            {
                Type type = Type.GetTypeFromProgID("WScript.Shell");
                if (type == null) return false;
                shell = Activator.CreateInstance(type);
                link = type.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod,
                    null, shell, new object[] { path });
                Type lt = link.GetType();
                lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { target });
                if (!string.IsNullOrEmpty(arguments))
                    lt.InvokeMember("Arguments", BindingFlags.SetProperty, null, link,
                        new object[] { arguments });
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
                return File.Exists(path);
            }
            catch { return false; }
            finally
            {
                if (link != null) { try { Marshal.FinalReleaseComObject(link); } catch { } }
                if (shell != null) { try { Marshal.FinalReleaseComObject(shell); } catch { } }
            }
        }
    }
}
