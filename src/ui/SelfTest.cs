// ShieldGate - tự kiểm tra lõi: dựng USB giả trong thư mục tạm rồi chạy đủ vòng.
// Không đụng tới USB thật, không đổi cấu hình máy. Dùng khi phát hành hoặc sau khi sửa lõi.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

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
            int total = 54;
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

            // 9. Bỏ thuộc tính ẩn (nút người dùng bấm: gỡ cả mục họ tự ẩn)
            int unhidden = UsbScan.Unhide(usb, UnhideScope.AllHidden, null);
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
                Guard.SaveConfig(false, true, true, true);
                GuardConfig cfg = Guard.LoadConfig();
                configOk = !cfg.Autorun && cfg.Ini && cfg.Wsh && cfg.Registry;
            }
            catch { }
            check(S.Selftest.GuardConfig, configOk);

            // 12b. Bật bảo vệ dùng đúng lựa chọn đã lưu: người dùng đã tắt chặn
            //      AutoRun thì lần bật sau không được bật lại mục đó.
            bool keepChoice = false;
            try
            {
                Guard.SaveConfig(false, true, true, true);
                GuardConfig enable = Guard.EnableConfig();
                keepChoice = !enable.Autorun && enable.Ini && enable.Registry;
                File.Delete(Paths.GuardConfig);
                GuardConfig fresh = Guard.EnableConfig();
                keepChoice = keepChoice && fresh.Autorun && fresh.Ini && fresh.Registry;
            }
            catch { }
            check(S.Selftest.ProtectKeepsChoice, keepChoice);

            // 12c. Còn một lớp đang chạy thì vẫn là đang bảo vệ, kể cả khi người
            //      dùng chủ động tắt chặn AutoRun.
            StatusInfo partial = new StatusInfo();
            partial.Running = true;
            partial.Ini = true;
            partial.Registry = true;
            StatusInfo nothing = new StatusInfo();
            check(S.Selftest.ProtectionStaysOn,
                  partial.ProtectionOn && !partial.FullyProtected && !nothing.ProtectionOn);

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

            // 16. Khoá đăng ký chưa từng tồn tại: xoá phải coi là thành công, nếu không
            //     mỗi lần tắt bảo vệ đều ghi "(ok=False)" dù chẳng có gì để xoá.
            bool regMissing = false;
            try
            {
                string testKey = @"Software\ShieldGateSelfTest\khong-ton-tai";
                regMissing = Reg.DeleteValue(RegistryHive.CurrentUser, testKey, "x")
                          && Reg.DeleteTree(RegistryHive.CurrentUser, testKey);
            }
            catch { }
            check(S.Selftest.RegDeleteMissing, regMissing);

            // 16b. Bộ kiểm tra Registry chạy được, không ném lỗi khi máy thiếu khoá.
            bool regScan = false;
            try { regScan = RegistryGuard.Detect() != null; }
            catch { }
            check(S.Selftest.RegistryScan, regScan);

            // 17. Tệp nằm ở đường dẫn dài hơn 260 ký tự: vẫn cách ly và khôi phục được.
            bool longOk = false;
            string longBatch = "";
            List<string> longDirs = new List<string>();
            string longSeed = "";
            try
            {
                string longDir = Path.Combine(root, "long");
                longDirs.Add(longDir);
                while (longDir.Length < 270)
                {
                    longDir = Path.Combine(longDir, "0123456789");
                    longDirs.Add(longDir);
                }
                string longFile = Path.Combine(longDir, new string('a', 40) + ".txt");
                longSeed = Path.Combine(root, "long-seed.txt");
                File.WriteAllText(longSeed, "du lieu dai", new UTF8Encoding(false));

                bool built = WinPath.CreateDirTree(WinPath.Ext(longDir))
                          && WinPath.CopyFile(WinPath.Ext(longSeed), WinPath.Ext(longFile)) == 0;
                longBatch = Quarantine.NewBatch("longpath", "");
                string lerr;
                bool added = built && Quarantine.Add(longFile, root, longBatch, "longpath", out lerr);
                bool gone = added && !WinPath.Exists(WinPath.Ext(longFile));

                int lr, ls, lm;
                Quarantine.RestoreBatch(longBatch, out lr, out ls, out lm);
                bool backLong = WinPath.Exists(WinPath.Ext(longFile));
                string text = "";
                if (backLong)
                {
                    using (StreamReader rd = new StreamReader(
                               WinPath.OpenRead(WinPath.Ext(longFile)), Encoding.UTF8))
                        text = rd.ReadToEnd();
                }
                longOk = built && added && gone && lr == 1 && lm == 0 && backLong && text == "du lieu dai";
                if (backLong) WinPath.DeleteFile(WinPath.Ext(longFile));
            }
            catch { }
            if (longBatch.Length > 0) Quarantine.RemoveBatch(longBatch);
            // Directory.Delete không xoá được cây dài; dọn từng cấp bằng Win32.
            for (int i = longDirs.Count - 1; i >= 0; i--)
                WinPath.RemoveDir(WinPath.Ext(longDirs[i]));
            if (longSeed.Length > 0) { try { File.Delete(longSeed); } catch { } }
            check(S.Selftest.LongPathQuarantine, longOk);

            // 18. JSON: chú thích, mảng, số, null và ký tự thoát; ghi lại rồi đọc lần nữa.
            bool jsonOk = false;
            try
            {
                object parsed = Json.Parse("// chu thich\r\n{ \"a\": [1, true, null, \"x\\ny\"], \"b\": 2.5 }");
                Dictionary<string, object> obj = Json.AsObject(parsed);
                List<object> arr = Json.AsArray(Json.Get(obj, "a"));
                string round = Json.Serialize(obj, false);
                Dictionary<string, object> again = Json.AsObject(Json.Parse(round));
                jsonOk = arr != null && arr.Count == 4 && arr[2] == null
                      && arr[1] is bool && (bool)arr[1]
                      && arr[3] as string == "x\ny"
                      && Json.Str(again, "b", "") == "2.5";
            }
            catch { }
            check(S.Selftest.JsonRoundTrip, jsonOk);

            // 19. JSON hỏng phải ném lỗi, không được trả về nửa chừng.
            bool jsonReject = false;
            try { Json.Parse("{ \"a\": [1, }"); }
            catch { jsonReject = true; }
            check(S.Selftest.JsonReject, jsonReject);

            // 20. Luật mặc định: ngưỡng, trọng số và bộ đuôi tệp.
            bool rulesDefault = false;
            try
            {
                RulesData d = Rules.Current;
                rulesDefault = d.ScoreThreshold == 3 && d.Weight("double-extension") == 5
                            && d.IsScriptExt(".vbs") && d.IsDangerousExt(".exe")
                            && d.BehaviorWindowSeconds >= 3;
            }
            catch { }
            check(S.Selftest.RulesDefaults, rulesDefault);

            // 21. rules.json bổ sung ngưỡng/trọng số/đuôi tệp, không xoá mặc định.
            bool rulesCustom = false;
            try
            {
                Paths.EnsureDirs();
                File.WriteAllText(Paths.RulesFile,
                    "{ \"scoreThreshold\": 7, \"weights\": { \"double-extension\": 9, \"target\": 99 }, " +
                    "\"scriptExts\": [\"abc\"] }", new UTF8Encoding(false));
                Rules.Reload();
                RulesData d = Rules.Current;
                rulesCustom = d.ScoreThreshold == 7 && d.Weight("double-extension") == 9
                           && d.Weight("target") == 10
                           && d.IsScriptExt(".abc") && d.IsScriptExt(".vbs")
                           && d.IsDangerousExt(".exe") && d.IsPayloadExt(".dll")
                           && d.Version == RulesData.CurrentVersion;
            }
            catch { }
            check(S.Selftest.RulesCustom, rulesCustom);

            // 22. Tệp luật hỏng: quay về mặc định, không ném lỗi ra ngoài.
            bool rulesCorrupt = false;
            try
            {
                File.WriteAllText(Paths.RulesFile, "{ khong phai json ",
                                  new UTF8Encoding(false));
                Rules.Reload();
                RulesData d = Rules.Current;
                rulesCorrupt = d.ScoreThreshold == 3 && d.Weight("double-extension") == 5
                            && d.IsScriptExt(".vbs");
            }
            catch { }
            check(S.Selftest.RulesCorrupt, rulesCorrupt);
            try
            {
                File.WriteAllText(Paths.RulesFile, Rules.DefaultJson, new UTF8Encoding(false));
                Rules.Reload();
            }
            catch { }

            // 23. Điểm cộng theo trọng số và luôn chặn ở 10.
            bool scoringOk = false;
            try
            {
                RulesData d = Rules.Current;
                int two = Scoring.Evaluate(new List<string>(new string[] { "target", "args" }), d);
                int many = Scoring.Evaluate(new List<string>(new string[]
                    { "target", "args", "args-relative-exe", "autorun-payload", "ntlm-leak" }), d);
                scoringOk = two == 6 && many == 10 && Scoring.Evaluate(null, d) == 0;
            }
            catch { }
            check(S.Selftest.Scoring, scoringOk);

            // 24. Detector 2.0 trên ổ giả riêng: đuôi kép, RLO, NTLM, desktop.ini,
            //     script gốc và macro chỉ gắn cờ.
            bool pipelineOk = false;
            try
            {
                string detect = Path.Combine(root, "detect");
                Directory.CreateDirectory(detect);
                File.WriteAllText(Path.Combine(detect, "baocao.pdf.exe"), "MZ",
                                  new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(detect, "tailieu\u202Etxt.exe"), "MZ",
                                  new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(detect, "leak.url"),
                    "[InternetShortcut]\r\nURL=http://example.invalid/\r\n" +
                    "IconFile=\\\\evil.invalid\\share\\icon.ico\r\n",
                    new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(detect, "desktop.ini"),
                    "[.ShellClassInfo]\r\nCLSID={88C6C381-2E85-11D0-94DE-444553540000}\r\n",
                    new UTF8Encoding(true));
                File.SetAttributes(Path.Combine(detect, "desktop.ini"),
                    FileAttributes.Hidden | FileAttributes.System);
                File.WriteAllText(Path.Combine(detect, "fix.reg"),
                    "Windows Registry Editor Version 5.00\r\n", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(detect, "hopdong.docm"), "x",
                                  new UTF8Encoding(false));

                UsbScanResult dr = UsbScan.Detect(detect, null, null);
                bool doubleExt = false, rlo = false, ntlm = false;
                bool desktop = false, macroFlag = false, rootScript = false;
                foreach (UsbItem it in dr.Items)
                {
                    if (HasCode(it, "double-extension")) doubleExt = it.High;
                    if (HasCode(it, "rlo-name")) rlo = it.High;
                    if (HasCode(it, "ntlm-leak")) ntlm = it.High;
                    if (HasCode(it, "root-desktop-ini")) desktop = true;
                    if (HasCode(it, "root-script")) rootScript = true;
                    if (HasCode(it, "office-macro")) macroFlag = it.FlagOnly && !it.High;
                }
                pipelineOk = doubleExt && rlo && ntlm && desktop && rootScript && macroFlag;
            }
            catch (Exception e)
            {
                Console.WriteLine("  " + e.Message);
            }
            check(S.Selftest.DetectorPipeline, pipelineOk);

            // 25. Dòng thời gian: ghi, đọc lại và xuất CSV/HTML.
            bool eventsOk = false;
            try
            {
                ShieldGate.Events.Add("selftest", "INFO", "selftest-subject", "selftest-detail");
                List<EventEntry> list = ShieldGate.Events.Read(20);
                bool seen = false;
                foreach (EventEntry e in list)
                {
                    if (e.Kind == "selftest" && e.Severity == "INFO"
                        && e.Subject == "selftest-subject" && e.Detail == "selftest-detail")
                        seen = true;
                }
                string csv = Path.Combine(root, "events.csv");
                string html = Path.Combine(root, "events.html");
                bool exported = ShieldGate.Events.ExportCsv(csv)
                             && ShieldGate.Events.ExportHtml(html)
                             && File.ReadAllText(csv).IndexOf("selftest-subject",
                                    StringComparison.Ordinal) >= 0
                             && File.ReadAllText(html).IndexOf("selftest-subject",
                                    StringComparison.Ordinal) >= 0;
                eventsOk = seen && exported;
            }
            catch { }
            check(S.Selftest.EventTimeline, eventsOk);

            // 26. Kho thiết bị: ghi/đọc/xoá theo khoá ổn định, không theo ký tự ổ.
            bool trustOk = false;
            string trustKey = "selftest:vid_0000&pid_0000:serial";
            try
            {
                bool set = DeviceStore.SetTrust(trustKey, DeviceTrust.Blocked);
                bool found = false;
                foreach (DeviceRecord r in DeviceStore.All())
                    if (string.Equals(r.Key, trustKey, StringComparison.OrdinalIgnoreCase)
                        && r.Trust == DeviceTrust.Blocked) found = true;
                bool forgot = DeviceStore.Forget(trustKey);
                bool gone = true;
                foreach (DeviceRecord r in DeviceStore.All())
                    if (string.Equals(r.Key, trustKey, StringComparison.OrdinalIgnoreCase)) gone = false;
                trustOk = set && found && forgot && gone;
            }
            catch { try { DeviceStore.Forget(trustKey); } catch { } }
            check(S.Selftest.DeviceTrustStore, trustOk);

            // 27. Công cụ hệ thống: luôn phân giải ra đường dẫn tuyệt đối, kể cả
            //     khi nơi gọi chỉ truyền tên trần (CreateProcess tìm trong thư
            //     mục chứa ShieldGate trước).
            bool sysExeOk = false;
            try
            {
                // schtasks/reg/cmd deu co tren moi Windows ho tro; powershell.exe
                // nam o System32\WindowsPowerShell\v1.0 nen kiem rieng.
                bool rooted = true;
                string[] names = new string[] { "cmd.exe", "schtasks.exe", "reg.exe",
                                                "icacls.exe", "powershell.exe" };
                foreach (string name in names)
                {
                    string full = Native.SysExe(name);
                    if (!Path.IsPathRooted(full) || !File.Exists(full)) rooted = false;
                }
                // Tên trần vẫn phải chạy được: Native.Run tự phân giải trước khi
                // CreateProcess tìm trong thư mục hiện tại.
                string bareTool = "cmd.exe";
                string exeOut;
                int exeCode = Native.Run(bareTool, "/c exit 0", 15000, out exeOut);
                sysExeOk = rooted && exeCode == 0;
            }
            catch { }
            check(S.Selftest.SysExe, sysExeOk);

            // 28. Sửa ổ: từ chối ổ hệ thống và đường dẫn không dùng được.
            bool repairGuards = false;
            try
            {
                string reason;
                bool system = Repair.RefuseReason(Paths.SystemDrive, out reason)
                           && reason.Length > 0;
                bool empty = Repair.RefuseReason("", out reason);
                bool badPath = Repair.RefuseReason("Z:\\shieldgate-selftest-missing", out reason);
                repairGuards = system && empty && badPath;
            }
            catch { }
            check(S.Selftest.RepairGuards, repairGuards);

            // 29. Đọc thử chỉ đọc trên thư mục tạm; không ghi, không xoá.
            bool readOk = false;
            try
            {
                ReadTestResult r = Repair.ReadTest(usb, 5, 8L * 1024 * 1024);
                readOk = r.Bytes > 0 && r.Errors == 0;
            }
            catch { }
            check(S.Selftest.RepairReadTest, readOk);

            // 30. Marker sao lưu phải gắn đúng volume: serial khớp, bản sao còn,
            //     và marker 2.0 chỉ theo chữ ổ không còn được chấp nhận.
            bool markerOk = false;
            string backupDest = Path.Combine(Paths.RepairDir, "selftest-backup");
            string driveLetter = Paths.SystemDrive.Substring(0, 1).ToUpperInvariant();
            uint volumeSerial = Native.VolumeSerial(Paths.SystemDrive);
            string serialText = volumeSerial.ToString("X8", CultureInfo.InvariantCulture);
            long volumeBytes = 0;
            try { volumeBytes = new DriveInfo(Paths.SystemDrive).TotalSize; }
            catch { }
            string marker = Path.Combine(Paths.RepairDir,
                "backup-" + driveLetter + "-" + serialText + ".ok");
            string legacyMarker = Path.Combine(Paths.RepairDir, "backup-" + driveLetter + ".ok");
            try
            {
                Paths.EnsureDirs();
                if (Directory.Exists(backupDest)) Directory.Delete(backupDest, true);
                Directory.CreateDirectory(backupDest);
                File.WriteAllText(Path.Combine(backupDest, "sample.txt"), "x");
                if (File.Exists(marker)) File.Delete(marker);
                if (File.Exists(legacyMarker)) File.Delete(legacyMarker);

                bool before = !Repair.HasBackup(Paths.SystemDrive);
                string when = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss",
                                                    CultureInfo.InvariantCulture);
                File.WriteAllText(marker, BackupMarkerText(Paths.SystemDrive, backupDest,
                    serialText, volumeBytes, when), new UTF8Encoding(false));
                bool after = Repair.HasBackup(Paths.SystemDrive);

                File.WriteAllText(marker, BackupMarkerText(Paths.SystemDrive, backupDest,
                    (volumeSerial + 1).ToString("X8", CultureInfo.InvariantCulture),
                    volumeBytes, when), new UTF8Encoding(false));
                bool wrongVolume = !Repair.HasBackup(Paths.SystemDrive);

                // Marker 2.0 chỉ có chữ ổ, không có serial: phải bị bỏ qua.
                File.Delete(marker);
                File.WriteAllText(legacyMarker, "x", new UTF8Encoding(false));
                bool legacyIgnored = !Repair.HasBackup(Paths.SystemDrive);

                markerOk = before && after && wrongVolume && legacyIgnored;
            }
            catch { }
            finally
            {
                try { if (File.Exists(marker)) File.Delete(marker); } catch { }
                try { if (File.Exists(legacyMarker)) File.Delete(legacyMarker); } catch { }
                try { if (Directory.Exists(backupDest)) Directory.Delete(backupDest, true); }
                catch { }
            }
            check(S.Selftest.RepairBackupMarker, markerOk);

            // 31. Marker quá 24 giờ không còn hiệu lực: phải sao lưu lại.
            bool expiryOk = false;
            try
            {
                Paths.EnsureDirs();
                Directory.CreateDirectory(backupDest);
                File.WriteAllText(Path.Combine(backupDest, "sample.txt"), "x");
                string oldWhen = DateTime.Now.AddHours(-25).ToString("yyyy-MM-ddTHH:mm:ss",
                    CultureInfo.InvariantCulture);
                File.WriteAllText(marker, BackupMarkerText(Paths.SystemDrive, backupDest,
                    serialText, volumeBytes, oldWhen), new UTF8Encoding(false));
                string expiryReason;
                bool expired = !Repair.CheckBackup(Paths.SystemDrive, out expiryReason);
                expiryOk = expired
                        && string.Equals(expiryReason, S.Repair.BackupExpired, StringComparison.Ordinal);
            }
            catch { }
            finally
            {
                try { if (File.Exists(marker)) File.Delete(marker); } catch { }
                try { if (Directory.Exists(backupDest)) Directory.Delete(backupDest, true); }
                catch { }
            }
            check(S.Selftest.RepairBackupExpiry, expiryOk);

            // 32. Xem trước sao lưu: đếm dự phòng bằng Tree.Walk khi không đọc
            // được bản tổng kết robocopy (Windows bản địa hoá).
            bool countOk = false;
            try
            {
                long files, bytes;
                Repair.CountTree(usb, out files, out bytes);
                countOk = files >= 3 && bytes > 0;
            }
            catch { }
            check(S.Selftest.RepairPreviewCount, countOk);

            // 33. Gỡ cài đặt: chỉ xoá thư mục có dấu hiệu sở hữu (marker hoặc tệp
            //     đặc trưng của bản 2.0), và giữ cách ly + bản sao lưu khi được yêu
            //     cầu. Dùng thư mục lồng trong sandbox, không đụng dữ liệu thật.
            bool uninstallOk = false;
            string sub = Path.Combine(root, "uninstall-test");
            try
            {
                Paths.Configure(sub);
                Paths.EnsureDirs();
                // Bản 2.0 chưa có marker: xoá marker để kiểm nhận diện bằng chữ ký.
                foreach (string dir in new string[] { Paths.LogDir, Paths.QuarDir, Paths.RepairDir })
                {
                    try { File.Delete(Paths.MarkerIn(dir)); }
                    catch { }
                }
                File.WriteAllText(Paths.LogFile, "x");
                string quarBatch = Path.Combine(Paths.QuarDir, "usb_20260101_000000");
                Directory.CreateDirectory(quarBatch);
                File.WriteAllText(Path.Combine(quarBatch, "manifest.csv"), "x");
                Directory.CreateDirectory(Path.Combine(Paths.RepairDir, "backup-E-20260101_000000"));
                string foreign = Path.Combine(sub, "userdata");
                Directory.CreateDirectory(foreign);
                File.WriteAllText(Path.Combine(foreign, "keep.txt"), "x");

                bool owned = Uninstaller.Owned(Paths.LogDir) && Uninstaller.Owned(Paths.QuarDir)
                          && Uninstaller.Owned(Paths.RepairDir) && !Uninstaller.Owned(foreign);
                bool keepExcludes = !Uninstaller.DataEntries(true).Contains(Paths.QuarDir)
                                 && !Uninstaller.DataEntries(true).Contains(Paths.RepairDir);
                bool purgeIncludes = Uninstaller.DataEntries(false).Contains(Paths.QuarDir)
                                  && Uninstaller.DataEntries(false).Contains(Paths.RepairDir);

                bool kept = Uninstaller.RemoveData(true, false);
                bool quarStill = Directory.Exists(quarBatch);
                bool repairStill = Directory.Exists(Paths.RepairDir);
                bool logGone = !File.Exists(Paths.LogFile);

                bool purged = Uninstaller.RemoveData(false, false);
                bool quarGone = !Directory.Exists(Paths.QuarDir);
                bool foreignKept = File.Exists(Path.Combine(foreign, "keep.txt"));

                uninstallOk = owned && keepExcludes && purgeIncludes && kept && quarStill
                           && repairStill && logGone && purged && quarGone && foreignKept;
            }
            catch { }
            finally
            {
                try { if (Directory.Exists(sub)) Directory.Delete(sub, true); }
                catch { }
                Paths.Configure(root);
            }
            check(S.Selftest.UninstallOwnership, uninstallOk);

            // 34. Manifest là dữ liệu không tin được: dòng trỏ nguồn ra ngoài lần
            //     cách ly hoặc đích vào thư mục hệ thống phải bị từ chối, không ghi.
            bool restoreGuard = false;
            string guardBatch = Path.Combine(Paths.QuarDir, "usb_20260101_010101");
            string guardFiles = Path.Combine(guardBatch, "files");
            string okSource = Path.Combine(guardFiles, "good.txt");
            string okTarget = Path.Combine(usb, "restore-ok.txt");
            string escapedTarget = Path.Combine(usb, "restore-outside.txt");
            string outside = Path.Combine(root, "outside.txt");
            string systemTarget = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "shieldgate-selftest-escape.txt");
            try
            {
                Paths.EnsureDirs();
                Directory.CreateDirectory(guardFiles);
                File.WriteAllText(okSource, "payload");
                File.WriteAllText(outside, "outside");
                string header = "Time,OriginalPath,QuarantinePath,Reason,Size,SHA256\r\n";
                File.WriteAllText(Path.Combine(guardBatch, "manifest.csv"),
                    header
                    + Quarantine.Csv("2026-01-01 01:01:01") + "," + Quarantine.Csv(okTarget) + ","
                    + Quarantine.Csv(okSource) + ",test,7,\r\n"
                    + Quarantine.Csv("2026-01-01 01:01:02") + "," + Quarantine.Csv(escapedTarget) + ","
                    + Quarantine.Csv(Path.Combine(guardBatch, "..", "..", "outside.txt")) + ",test,7,\r\n"
                    + Quarantine.Csv("2026-01-01 01:01:03") + "," + Quarantine.Csv(systemTarget) + ","
                    + Quarantine.Csv(okSource) + ",test,7,\r\n",
                    new UTF8Encoding(false));

                int rr, rs, rm, rf;
                Quarantine.RestoreBatch(guardBatch, out rr, out rs, out rm, out rf);
                restoreGuard = rr == 1 && rf == 2 && File.Exists(okTarget)
                            && !File.Exists(escapedTarget) && !File.Exists(systemTarget);
            }
            catch { }
            finally
            {
                try { Quarantine.RemoveBatch(guardBatch); } catch { }
                try { if (File.Exists(okTarget)) File.Delete(okTarget); } catch { }
                try { if (File.Exists(outside)) File.Delete(outside); } catch { }
            }
            check(S.Selftest.RestoreGuard, restoreGuard);

            // 35. Thư mục giả: chỉ cách ly tệp chạy được. Tài liệu người dùng bị
            //     virus đổi tên Base64 phải được giữ lại (log 03/11 đã thu gom
            //     hơn 600 tài liệu thật chỉ vì nằm trong thư mục "Docusment").
            bool fakeKeep = false;
            string fakeRoot = Path.Combine(usb, "Docusment");
            string fakeBatch = "";
            try
            {
                string deep = Path.Combine(fakeRoot, "3", "22AB820634F924A0");
                Directory.CreateDirectory(deep);
                string payload = Path.Combine(deep, "1.exe");
                string payloadDat = Path.Combine(deep, "adback.dat");
                // "cuong_do cuong.docx" mã hoá Base64: tài liệu thật, không phải mã độc.
                string doc = Path.Combine(deep, "Y3VvbmdfxJHhu5cgY8aw4budbmcuZG9jeA==");
                File.WriteAllText(payload, "MZ", new UTF8Encoding(false));
                File.WriteAllText(payloadDat, "x", new UTF8Encoding(false));
                File.WriteAllText(doc, "tai lieu", new UTF8Encoding(false));

                fakeBatch = Quarantine.NewBatch("test", "");
                int fakeFail = 0, fakeKept = 0;
                UsbScan.QuarantineTree(fakeRoot, usb, fakeBatch, ref fakeFail, ref fakeKept);
                fakeKeep = fakeFail == 0 && fakeKept == 1
                        && !File.Exists(payload) && !File.Exists(payloadDat)
                        && File.Exists(doc) && Directory.Exists(deep);
            }
            catch { }
            finally
            {
                if (fakeBatch.Length > 0) { try { Quarantine.RemoveBatch(fakeBatch); } catch { } }
                try { if (Directory.Exists(fakeRoot)) Directory.Delete(fakeRoot, true); }
                catch { }
            }
            check(S.Selftest.FakeFolderKeepsDocs, fakeKeep);

            // 36. Tệp đã biến mất không phải lỗi: phải trả error rỗng để bên gọi
            //     bỏ qua thay vì ghi ERROR và trừ vào bộ đếm (log có 286 lỗi giả).
            bool goneOk = false;
            string goneBatch = "";
            try
            {
                goneBatch = Quarantine.NewBatch("test", "");
                string goneErr;
                bool goneAdded = Quarantine.Add(Path.Combine(usb, "khong-con.txt"), usb,
                                                goneBatch, "test", out goneErr);
                goneOk = !goneAdded && goneErr.Length == 0;
            }
            catch { }
            finally
            {
                if (goneBatch.Length > 0) { try { Quarantine.RemoveBatch(goneBatch); } catch { } }
            }
            check(S.Selftest.MissingIsNotError, goneOk);

            // 37. Khoá dọn ổ: luồng khác không vào được khi đang giữ, cùng luồng
            //     vào lại được (Apply gọi QuarantineTree), nhả ra thì vào lại được.
            bool lockOk = false;
            bool lockHeld = false;
            try
            {
                lockHeld = CleanLock.Enter(1000);
                int other = -1;
                System.Threading.Thread t = new System.Threading.Thread(delegate()
                {
                    bool got = CleanLock.Enter(0);
                    other = got ? 1 : 0;
                    if (got) CleanLock.Exit();
                });
                t.IsBackground = true;
                t.Start();
                t.Join(5000);
                bool blocked = lockHeld && other == 0;
                bool reentrant = lockHeld && CleanLock.Enter(0);
                if (reentrant) CleanLock.Exit();
                if (lockHeld) { CleanLock.Exit(); lockHeld = false; }
                bool freeAgain = CleanLock.Enter(2000);
                if (freeAgain) CleanLock.Exit();
                lockOk = blocked && reentrant && freeAgain && !CleanLock.IsBusy();
            }
            catch { }
            finally
            {
                if (lockHeld) { try { CleanLock.Exit(); } catch { } }
            }
            check(S.Selftest.CleanLockExclusive, lockOk);

            // 38. Lượt cách ly không di chuyển được tệp nào thì không để lại vỏ
            //     batch rỗng: vòng quét nền 10 giây/lần sẽ sinh hàng trăm thư mục.
            bool emptyBatchOk = false;
            string docsRoot = Path.Combine(usb, "Dosc2");
            try
            {
                Directory.CreateDirectory(docsRoot);
                string keepDoc = Path.Combine(docsRoot, "baocao.docx");
                File.WriteAllText(keepDoc, "tai lieu", new UTF8Encoding(false));

                UsbScanResult isEmpty = new UsbScanResult();
                isEmpty.Root = usb;
                isEmpty.Drive = usb;
                UsbItem folderItem = new UsbItem();
                folderItem.Kind = UsbKind.FakeFolder;
                folderItem.Path = docsRoot;
                folderItem.FolderName = "Dosc2";
                folderItem.High = true;
                List<UsbItem> oneFolder = new List<UsbItem>();
                oneFolder.Add(folderItem);

                int emptyFailed, emptyStopped;
                string emptyBatch;
                int movedFiles = UsbScan.Apply(isEmpty, oneFolder, out emptyFailed,
                                               out emptyBatch, out emptyStopped, null);
                emptyBatchOk = movedFiles == 0 && emptyBatch.Length == 0
                            && emptyFailed == 0 && File.Exists(keepDoc);
            }
            catch { }
            finally
            {
                try { if (Directory.Exists(docsRoot)) Directory.Delete(docsRoot, true); }
                catch { }
            }
            check(S.Selftest.EmptyBatchRemoved, emptyBatchOk);

            // 39. CSV injection: ô mở đầu bằng = + - @ phải bị vô hiệu hoá khi xuất
            //     để Excel không chạy công thức từ tên tệp do virus đặt.
            bool csvGuard = false;
            try
            {
                ShieldGate.Events.Add("selftest", "INFO", "=cmd|' /C calc'!A0", "csvdetail");
                string csvPath = Path.Combine(root, "events-inject.csv");
                if (ShieldGate.Events.ExportCsv(csvPath))
                {
                    string text = File.ReadAllText(csvPath);
                    csvGuard = text.IndexOf("\"'=cmd", StringComparison.Ordinal) >= 0
                            && text.IndexOf("\"=cmd", StringComparison.Ordinal) < 0;
                }
            }
            catch { }
            check(S.Selftest.CsvInjection, csvGuard);

            // 40. reg export: khoá không tồn tại thì không có bản sao, phải trả
            //     false để Fix dừng thay vì xoá mà không có đường lùi.
            bool regBackupOk = false;
            string regBak = "";
            try
            {
                string bogusError;
                string bogusFile;
                bool bogus = PcScan.BackupReg(RegistryHive.CurrentUser,
                    @"Control Panel\ShieldGateNoSuchKey", out bogusError, out bogusFile);
                string realError;
                string realFile;
                bool real = PcScan.BackupReg(RegistryHive.CurrentUser,
                    @"Control Panel\Desktop", out realError, out realFile);
                regBak = realFile;
                regBackupOk = !bogus && bogusError.Length > 0
                           && real && realError.Length == 0
                           && File.Exists(realFile) && new FileInfo(realFile).Length > 0;
            }
            catch { }
            finally
            {
                try { if (regBak.Length > 0 && File.Exists(regBak)) File.Delete(regBak); }
                catch { }
            }
            check(S.Selftest.RegExportChecked, regBackupOk);

            // 41. Phạm vi Unhide: chế độ tự động chỉ gỡ Hidden+System (dấu hiệu
            //     virus), giữ desktop.ini, mục người dùng tự ẩn và thư mục hệ thống.
            bool scopeOk = false;
            string scopeDir = Path.Combine(root, "unhide-scope");
            try
            {
                Directory.CreateDirectory(scopeDir);
                string recycle = Path.Combine(scopeDir, "$RECYCLE.BIN");
                Directory.CreateDirectory(recycle);
                string deskIni = Path.Combine(scopeDir, "desktop.ini");
                string mine = Path.Combine(scopeDir, "mine.docx");
                string worm = Path.Combine(scopeDir, "worm.doc");
                File.WriteAllText(deskIni, "[.ShellClassInfo]", new UTF8Encoding(false));
                File.WriteAllText(mine, "nguoi dung", new UTF8Encoding(false));
                File.WriteAllText(worm, "virus", new UTF8Encoding(false));
                File.SetAttributes(deskIni, FileAttributes.Hidden | FileAttributes.System);
                File.SetAttributes(mine, FileAttributes.Hidden);
                File.SetAttributes(worm, FileAttributes.Hidden | FileAttributes.System);
                File.SetAttributes(recycle, FileAttributes.Hidden | FileAttributes.System);

                int autoUnhide = UsbScan.Unhide(scopeDir, null);
                bool deskKept = (File.GetAttributes(deskIni) & FileAttributes.Hidden) != 0;
                bool mineKept = (File.GetAttributes(mine) & FileAttributes.Hidden) != 0;
                bool wormCleared = (File.GetAttributes(worm) & FileAttributes.Hidden) == 0;
                bool recycleKept = (File.GetAttributes(recycle) & FileAttributes.Hidden) != 0;
                scopeOk = autoUnhide == 1 && deskKept && mineKept && wormCleared && recycleKept;
            }
            catch { }
            finally
            {
                try
                {
                    if (Directory.Exists(scopeDir))
                    {
                        foreach (string f in Directory.GetFiles(scopeDir, "*", SearchOption.AllDirectories))
                        {
                            try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                        }
                        foreach (string d in Directory.GetDirectories(scopeDir, "*", SearchOption.AllDirectories))
                        {
                            try { File.SetAttributes(d, FileAttributes.Directory); } catch { }
                        }
                        Directory.Delete(scopeDir, true);
                    }
                }
                catch { }
            }
            check(S.Selftest.UnhideScope, scopeOk);

            // 42. Nhập luật có xác nhận: bỏ qua tệp cạnh exe, giữ bản cũ, tệp v2
            //     thiếu khoá mới vẫn có mặc định, trọng số ngoài 0..10 bị kẹp.
            bool rulesImport = false;
            string importSrc = Path.Combine(root, "rules-import.json");
            string besideRules = Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "rules.json");
            try
            {
                Paths.EnsureDirs();
                File.WriteAllText(besideRules,
                    "{ \"scoreThreshold\": 9, \"scriptExts\": [\"besideonly\"] }",
                    new UTF8Encoding(false));
                File.WriteAllText(importSrc,
                    "{ \"scoreThreshold\": 8, \"weights\": { \"target\": 99 }, " +
                    "\"scriptExts\": [\"xyz\"] }", new UTF8Encoding(false));

                string importError;
                bool imported = Rules.Import(importSrc, out importError);
                string missingError;
                bool rejected = !Rules.Import(Path.Combine(root, "khong-co.json"), out missingError);
                RulesData d = Rules.Current;
                rulesImport = imported && importError.Length == 0
                           && rejected && missingError.Length > 0
                           && d.Version == RulesData.CurrentVersion
                           && d.ScoreThreshold == 8
                           && d.IsScriptExt(".xyz") && d.IsScriptExt(".vbs")
                           && !d.IsScriptExt(".besideonly")
                           && d.IsPayloadExt(".dll")
                           && d.Weight("target") == 10
                           && File.Exists(Paths.RulesFile + ".bak")
                           && string.Equals(Rules.CandidatePath(), Paths.RulesFile,
                                            StringComparison.OrdinalIgnoreCase);
            }
            catch { }
            finally
            {
                try { File.Delete(besideRules); } catch { }
                try { File.Delete(importSrc); } catch { }
                try
                {
                    File.WriteAllText(Paths.RulesFile, Rules.DefaultJson, new UTF8Encoding(false));
                    Rules.Reload();
                }
                catch { }
            }
            check(S.Selftest.RulesImport, rulesImport);

            // 43. Tác vụ: chỉ báo khi tác vụ đang bật và lệnh nguy hiểm; tác vụ đã
            //     tắt không được báo lại (người dùng tưởng virus tạo lại tác vụ).
            bool taskState = false;
            try
            {
                System.Xml.XmlDocument risky = new System.Xml.XmlDocument();
                risky.LoadXml("<Task><Actions><Exec><Command>wscript.exe</Command>"
                    + "<Arguments>\"C:\\Users\\Public\\a.vbs\"</Arguments></Exec></Actions>"
                    + "<Settings><Enabled>true</Enabled></Settings></Task>");
                System.Xml.XmlDocument off = new System.Xml.XmlDocument();
                off.LoadXml("<Task><Actions><Exec><Command>wscript.exe</Command>"
                    + "<Arguments>\"C:\\Users\\Public\\a.vbs\"</Arguments></Exec></Actions>"
                    + "<Settings><Enabled>false</Enabled></Settings></Task>");
                System.Xml.XmlDocument clean = new System.Xml.XmlDocument();
                clean.LoadXml("<Task><Actions><Exec><Command>C:\\Windows\\System32\\notepad.exe"
                    + "</Command></Exec></Actions><Settings><Enabled>true</Enabled>"
                    + "</Settings></Task>");
                string action;
                bool hit = PcScan.TaskSuspicious(risky, out action) && action.Length > 0;
                bool skipOff = !PcScan.TaskSuspicious(off, out action);
                bool skipClean = !PcScan.TaskSuspicious(clean, out action);
                taskState = hit && skipOff && skipClean;
            }
            catch { }
            check(S.Selftest.TaskState, taskState);

            // 44. Ổ bị dọn 3 lần trong 10 phút mới báo nhiễm lại, báo một lần rồi
            //     im; ổ khác đếm riêng; lượt không cách ly tệp nào thì không đếm.
            bool reinfectOk = false;
            try
            {
                ReinfectWatch.Reset();
                DateTime t0 = new DateTime(2026, 11, 5, 2, 31, 0);
                bool first = ReinfectWatch.Observe("F:", 1, t0);
                bool second = ReinfectWatch.Observe("F:", 1, t0.AddSeconds(35));
                bool third = ReinfectWatch.Observe("F:", 1, t0.AddSeconds(70));
                bool throttled = ReinfectWatch.Observe("F:", 1, t0.AddSeconds(105));
                bool other = ReinfectWatch.Observe("D:", 1, t0.AddSeconds(110));
                bool noMove = ReinfectWatch.Observe("F:", 0, t0.AddSeconds(120));
                ReinfectWatch.Reset();
                bool again1 = ReinfectWatch.Observe("F:", 1, t0.AddMinutes(20));
                bool again2 = ReinfectWatch.Observe("F:", 1, t0.AddMinutes(21));
                bool again3 = ReinfectWatch.Observe("F:", 1, t0.AddMinutes(22));
                reinfectOk = !first && !second && third && !throttled && !other
                          && !noMove && !again1 && !again2 && again3;
            }
            catch { }
            finally { ReinfectWatch.Reset(); }
            check(S.Selftest.ReinfectLoop, reinfectOk);

            // 45. Tác vụ ẩn dưới \Microsoft\...: PowerShell mã hoá của Windows không
            //     bị báo, nhưng lệnh trỏ vào ProgramData hoặc script host thì báo.
            bool taskMicrosoft = false;
            try
            {
                System.Xml.XmlDocument encoded = new System.Xml.XmlDocument();
                encoded.LoadXml("<Task><Actions><Exec><Command>powershell.exe</Command>"
                    + "<Arguments>-EncodedCommand AAAA</Arguments></Exec></Actions>"
                    + "<Settings><Enabled>true</Enabled></Settings></Task>");
                System.Xml.XmlDocument drop = new System.Xml.XmlDocument();
                drop.LoadXml("<Task><Actions><Exec><Command>cmd.exe</Command>"
                    + "<Arguments>/c \"C:\\ProgramData\\LMIGuardianUrX\\1.exe\"</Arguments>"
                    + "</Exec></Actions><Settings><Enabled>true</Enabled></Settings></Task>");
                string action;
                bool normalSees = PcScan.TaskSuspicious(encoded, out action);
                bool microsoftSkips = !PcScan.TaskSuspicious(encoded, true, out action);
                bool microsoftSeesDrop = PcScan.TaskSuspicious(drop, true, out action);
                taskMicrosoft = normalSees && microsoftSkips && microsoftSeesDrop;
            }
            catch { }
            check(S.Selftest.TaskMicrosoft, taskMicrosoft);

            // 46. Dịch vụ/driver: đường dẫn Windows và Program Files được bỏ qua,
            //     ProgramData/Public hoặc đường dẫn trần bị báo; đọc đúng \??\ và
            //     \SystemRoot\.
            bool servicePath = false;
            try
            {
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                bool svchost = !PcScan.ServiceSuspicious("x",
                    @"C:\Windows\System32\svchost.exe -k netsvcs", 2, 0x20);
                bool programFiles = !PcScan.ServiceSuspicious("x",
                    @"""C:\Program Files\App\svc.exe"" -run", 2, 0x10);
                bool disabled = !PcScan.ServiceSuspicious("x",
                    @"C:\ProgramData\LMIGuardianUrX\1.exe", 4, 0x10);
                bool programData = PcScan.ServiceSuspicious("x",
                    @"\??\C:\ProgramData\LMIGuardianUrX\1.exe", 2, 0x10);
                bool publicDir = PcScan.ServiceSuspicious("x",
                    @"C:\Users\Public\svc.exe", 3, 0x10);
                bool bare = PcScan.ServiceSuspicious("x", "adback.dat", 2, 0x10);
                bool defender = !PcScan.ServiceSuspicious("WinDefend",
                    @"""C:\ProgramData\Microsoft\Windows Defender\Platform\4.18.26100.4-0\MsMpEng.exe""",
                    2, 0x10);
                string sysRoot = PcScan.ServiceImagePath(
                    @"\SystemRoot\System32\drivers\evil.sys");
                servicePath = svchost && programFiles && disabled && programData && publicDir
                           && bare && defender
                           && string.Equals(sysRoot,
                                  System.IO.Path.Combine(win, @"System32\drivers\evil.sys"),
                                  StringComparison.OrdinalIgnoreCase);
            }
            catch { }
            check(S.Selftest.ServicePath, servicePath);

            // 47. Thứ tự sửa: tiến trình/dịch vụ trước, cơ chế khởi động, rồi tệp —
            //     để nguồn lây không kịp tạo lại thứ vừa xoá.
            bool fixOrder = PcScan.FixPass(PcKind.Process) == 0
                         && PcScan.FixPass(PcKind.FakeProcess) == 0
                         && PcScan.FixPass(PcKind.Service) == 0
                         && PcScan.FixPass(PcKind.Task) == 1
                         && PcScan.FixPass(PcKind.WmiConsumer) == 1
                         && PcScan.FixPass(PcKind.RunValue) == 2
                         && PcScan.FixPass(PcKind.StartupFile) == 2
                         && PcScan.FixPass(PcKind.DroppedFile) == 3
                         && PcScan.FixPass(PcKind.Process) < PcScan.FixPass(PcKind.Task)
                         && PcScan.FixPass(PcKind.Task) < PcScan.FixPass(PcKind.RunValue)
                         && PcScan.FixPass(PcKind.RunValue) < PcScan.FixPass(PcKind.DroppedFile);
            check(S.Selftest.FixOrder, fixOrder);

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

        // Marker sao lưu đúng định dạng Repair đọc (xem Repair.FindMarker).
        private static string BackupMarkerText(string source, string dest, string serial,
                                               long volumeBytes, string when)
        {
            return "state=complete\r\n"
                 + "volumeSerial=" + serial + "\r\n"
                 + "volumeBytes=" + volumeBytes.ToString(CultureInfo.InvariantCulture) + "\r\n"
                 + "source=" + source + "\r\n"
                 + "dest=" + dest + "\r\n"
                 + "when=" + when + "\r\n"
                 + "files=1\r\n"
                 + "bytes=1\r\n";
        }

        // Mã có thể kèm chi tiết dạng "code=detail"; so khớp cả hai dạng.
        private static bool HasCode(UsbItem item, string code)
        {
            foreach (string c in item.Codes)
            {
                if (string.Equals(c, code, StringComparison.Ordinal)) return true;
                if (c.StartsWith(code + "=", StringComparison.Ordinal)) return true;
            }
            return false;
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
