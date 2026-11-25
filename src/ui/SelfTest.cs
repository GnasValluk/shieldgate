// ShieldGate - tự kiểm tra lõi: dựng USB giả trong thư mục tạm rồi chạy đủ vòng.
// Không đụng tới USB thật, không đổi cấu hình máy. Dùng khi phát hành hoặc sau khi sửa lõi.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
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

            // Self-test chạy trên thư mục tạm; không được tạm mở WriteProtect thật.
            Policy.PauseAllowed = false;

            int passed = 0;
            int total = 80;
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

            // 12d. Tắt hết phải ghi guard.cfg toàn 0 để công tắc Registry không
            //      tự bật lại sau khi làm mới; lựa chọn cũ vẫn nằm ở guard.last.
            bool offSticks = false;
            try
            {
                Guard.SaveConfig(true, true, true, true);
                Guard.SaveConfig(false, false, false, false);
                GuardConfig off = Guard.LoadConfig();
                GuardConfig restoredCfg = Guard.EnableConfig();
                offSticks = !off.Autorun && !off.Ini && !off.Wsh && !off.Registry
                            && restoredCfg.Autorun && restoredCfg.Ini && restoredCfg.Registry;
                File.Delete(Paths.GuardConfig);
                File.Delete(Paths.GuardLast);
            }
            catch { }
            check(S.Selftest.GuardOffSticks, offSticks);

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
                    "\"scriptExts\": [\"abc\"], \"dangerExes\": [\"evil.exe\"], " +
                    "\"payloadSizes\": [12345], \"scriptMarkers\": [\"BadMarker\"], " +
                    "\"highPatterns\": [[\"evil\", \"evil-code\"]] }", new UTF8Encoding(false));
                Rules.Reload();
                RulesData d = Rules.Current;
                rulesCustom = d.ScoreThreshold == 7 && d.Weight("double-extension") == 9
                           && d.Weight("target") == 10
                           && d.IsScriptExt(".abc") && d.IsScriptExt(".vbs")
                           && d.IsDangerousExt(".exe") && d.IsPayloadExt(".dll")
                           && d.IsDangerExe("evil.exe") && d.IsDangerExe("cmd.exe")
                           && d.PayloadSizes.Contains(12345)
                           && d.ScriptMarkers.Contains("badmarker")
                           && d.HighPatterns.Count == 7
                           && d.IsScriptScanExt(".vbs") && !d.IsScriptScanExt(".bat")
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

            // 35b. Tên Base64: chỉ giải mã tài liệu hợp lệ; đổi tên trong thư mục
            //      người dùng chọn, không ghi đè và không chạm payload.
            bool base64Ok = false;
            string b64Root = Path.Combine(usb, "Docsument");
            try
            {
                Directory.CreateDirectory(b64Root);
                File.WriteAllText(Path.Combine(b64Root, "1.exe"), "MZ", new UTF8Encoding(false));
                string good = Convert.ToBase64String(Encoding.UTF8.GetBytes("bao cao.docx"));
                File.WriteAllText(Path.Combine(b64Root, good), "data", new UTF8Encoding(false));

                bool decodeOk = UsbRestore.DecodeName(good) == "bao cao.docx"
                    && UsbRestore.DecodeName("tai-lieu.docx") == null
                    && UsbRestore.DecodeName(Convert.ToBase64String(
                           Encoding.UTF8.GetBytes("virus.exe"))) == null
                    && UsbRestore.DecodeName(Convert.ToBase64String(
                           Encoding.UTF8.GetBytes("..\\x.docx"))) == null;

                int b64Skipped;
                int renamed = UsbRestore.RenameBase64(b64Root, null, null, out b64Skipped);
                bool b64Moved = renamed == 1 && b64Skipped == 0
                    && File.Exists(Path.Combine(b64Root, "bao cao.docx"))
                    && !File.Exists(Path.Combine(b64Root, good));

                string clash = Convert.ToBase64String(Encoding.UTF8.GetBytes("trung ten.pdf"));
                File.WriteAllText(Path.Combine(b64Root, clash), "a", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(b64Root, "trung ten.pdf"), "b", new UTF8Encoding(false));
                int skipped2;
                int renamed2 = UsbRestore.RenameBase64(b64Root, null, null, out skipped2);

                base64Ok = decodeOk && b64Moved && renamed2 == 0 && skipped2 == 1
                    && File.Exists(Path.Combine(b64Root, "trung ten.pdf"));
            }
            catch { }
            check(S.Selftest.Base64Name, base64Ok);

            // 35c. Quét một thư mục con thay vì cả ổ: vẫn nhận diện được và đếm
            //      đúng tài liệu giữ lại trong thư mục giả.
            bool folderScanOk = false;
            try
            {
                string subDir = Path.Combine(usb, "Tai lieu", "sub");
                string fake = Path.Combine(subDir, "Doscument");
                Directory.CreateDirectory(fake);
                File.WriteAllText(Path.Combine(fake, "1.exe"), "MZ", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(fake, "a.docx"), "data", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(subDir, "run.vbs"),
                    "CreateObject(\"WScript.Shell\").Run \"cmd.exe\"\r\n", new UTF8Encoding(false));

                UsbScanResult subScan = UsbScan.Detect(subDir, null, null);
                folderScanOk = !subScan.IsDriveRoot
                    && string.Equals(subScan.Root, subDir, StringComparison.OrdinalIgnoreCase)
                    && subScan.Items.Count >= 1
                    && subScan.KeptFiles == 1;
            }
            catch { }
            check(S.Selftest.FolderScan, folderScanOk);

            // 35d. Khung tin pipe: đọc/ghi đúng, từ chối phiên bản, lệnh lạ,
            //      khóa lạ, args sai kiểu và khung quá lớn.
            bool pipeProtocolOk = false;
            try
            {
                string frame;
                string frameError;
                using (MemoryStream ms = new MemoryStream())
                {
                    PipeProtocol.Write(ms, "{\"v\":1,\"id\":\"a1\",\"cmd\":\"status.get\",\"args\":{}}");
                    ms.Position = 0;
                    pipeProtocolOk = PipeProtocol.TryRead(ms, out frame, out frameError)
                        && frameError == null && frame.IndexOf("status.get") >= 0;
                }

                PipeRequest good = PipeProtocol.Parse(
                    "{\"v\":1,\"id\":\"a1\",\"cmd\":\"status.get\",\"args\":{}}");
                PipeRequest badVersion = PipeProtocol.Parse(
                    "{\"v\":2,\"id\":\"a1\",\"cmd\":\"status.get\",\"args\":{}}");
                PipeRequest unknown = PipeProtocol.Parse(
                    "{\"v\":1,\"id\":\"a1\",\"cmd\":\"nope\",\"args\":{}}");
                PipeRequest extra = PipeProtocol.Parse(
                    "{\"v\":1,\"id\":\"a1\",\"cmd\":\"status.get\",\"args\":{},\"x\":1}");
                PipeRequest badArgs = PipeProtocol.Parse(
                    "{\"v\":1,\"id\":\"a1\",\"cmd\":\"status.get\",\"args\":[]}");

                byte[] big = BitConverter.GetBytes(PipeProtocol.MaxMessage + 1);
                using (MemoryStream ms = new MemoryStream(big))
                {
                    PipeProtocol.TryRead(ms, out frame, out frameError);
                }

                pipeProtocolOk = pipeProtocolOk
                    && good.Error.Length == 0
                    && badVersion.Error == "bad-version"
                    && unknown.Error == "unknown-command"
                    && extra.Error == "bad-args"
                    && badArgs.Error == "bad-args"
                    && frameError == "too-large";
            }
            catch { }
            check(S.Selftest.PipeProtocol, pipeProtocolOk);

            // 35e. Pin client: đường dẫn + SHA-256 phải khớp; xoá pin thì từ chối.
            bool pipePinOk = false;
            try
            {
                Paths.EnsureDirs();
                string pinExe = Path.Combine(root, "pipe-client-test.exe");
                File.WriteAllText(pinExe, "pin", new UTF8Encoding(false));
                string hash = Native.Sha256(pinExe);
                string wantPath, wantHash;
                bool written = PipeAuth.PinClient(pinExe);
                bool read = PipeAuth.ReadPin(out wantPath, out wantHash);
                bool match = PipeAuth.IsPinnedImage(pinExe, hash);
                bool wrongPath = PipeAuth.IsPinnedImage(pinExe + ".other", hash);
                bool wrongHash = PipeAuth.IsPinnedImage(pinExe, new string('0', 64));
                PipeAuth.ClearPin();
                pipePinOk = written && read && match && !wrongPath && !wrongHash
                    && !File.Exists(Paths.ClientPin);
            }
            catch { }
            check(S.Selftest.PipePin, pipePinOk);

            // 35f. Roundtrip thật qua named pipe: server + client trong tiến trình
            //      test, validator cho qua vì đây không phải tiến trình GUI.
            bool pipeRoundtripOk = false;
            string pipeName = "ShieldGate.SelfTest." + System.Diagnostics.Process.GetCurrentProcess().Id
                            + "." + Guid.NewGuid().ToString("N");
            PipeServer pipeServer = null;
            PipeClientInfo clientSeen = null;
            try
            {
                pipeServer = new PipeServer(pipeName, delegate(PipeClientInfo info)
                {
                    clientSeen = info;
                    return true;
                });
                pipeServer.Start();
                string error;
                string response = PipeClient.Send(pipeName, "status.get",
                    new Dictionary<string, object>(), 5000, out error);
                pipeRoundtripOk = response != null && PipeClient.IsOk(response)
                    && response.IndexOf("\"version\":\"" + AppInfo.Version + "\"") >= 0
                    // Xác thực thật: PID → ảnh, session, SID; client nội bộ không
                    // bị nhận nhầm là remote.
                    && clientSeen != null && clientSeen.Valid && clientSeen.ImagePath.Length > 0
                    && clientSeen.SessionId == (uint)System.Diagnostics.Process.GetCurrentProcess().SessionId;

                // Cùng một phiên phải gửi được nhiều lệnh và sống tiếp sau lỗi:
                // plan/apply của GUI dựa vào điều này.
                using (PipeSession session = new PipeSession(pipeName))
                {
                    string sessionError;
                    if (session.Connect(5000, out sessionError))
                    {
                        string first = session.Send("status.get",
                            new Dictionary<string, object>(), 5000, out sessionError);
                        string second = session.Send("status.get",
                            new Dictionary<string, object>(), 5000, out sessionError);
                        Dictionary<string, object> bad = new Dictionary<string, object>();
                        bad["drive"] = "C";
                        string refusedPlan = session.Send("clean.plan", bad, 5000, out sessionError);
                        pipeRoundtripOk = pipeRoundtripOk
                            && PipeClient.IsOk(first) && PipeClient.IsOk(second)
                            && refusedPlan != null && !PipeClient.IsOk(refusedPlan)
                            && refusedPlan.IndexOf("\"error\":\"refused\"") >= 0;
                    }
                    else pipeRoundtripOk = false;
                }
            }
            catch { }
            finally
            {
                if (pipeServer != null)
                {
                    try { pipeServer.Stop(); } catch { }
                }
            }
            check(S.Selftest.PipeRoundtrip, pipeRoundtripOk);

            // 35h. ACL thật của pipe: SYSTEM + Administrators + Interactive được
            //      phép, Network/Anonymous bị từ chối, không có Everyone.
            bool pipeAclOk = false;
            try
            {
                PipeSecurity security = PipeServer.BuildSecurity();
                AuthorizationRuleCollection rules = security.GetAccessRules(
                    true, true, typeof(SecurityIdentifier));
                bool system = false, admins = false, interactive = false;
                bool networkDeny = false, anonymousDeny = false, everyoneAllow = false;
                foreach (AuthorizationRule rule in rules)
                {
                    PipeAccessRule pipeRule = rule as PipeAccessRule;
                    if (pipeRule == null) continue;
                    string sid = pipeRule.IdentityReference.Value;
                    bool allow = pipeRule.AccessControlType == AccessControlType.Allow;
                    if (allow && sid == "S-1-5-18") system = true;
                    if (allow && sid == "S-1-5-32-544") admins = true;
                    if (allow && sid == "S-1-5-4") interactive = true;
                    if (!allow && sid == "S-1-5-2") networkDeny = true;
                    if (!allow && sid == "S-1-5-7") anonymousDeny = true;
                    if (allow && (sid == "S-1-1-0" || sid == "S-1-5-2" || sid == "S-1-5-7"))
                        everyoneAllow = true;
                }
                pipeAclOk = system && admins && interactive
                    && networkDeny && anonymousDeny && !everyoneAllow;
            }
            catch { }
            check(S.Selftest.PipeAcl, pipeAclOk);

            // 35g. Plan/apply: chỉ nhận một ký tự ổ, từ chối ổ hệ thống và đường
            //      dẫn; thiếu confirm là bad-args; plan hết hạn/sai SID/session/ổ
            //      hoặc serial bị từ chối. Không ghi gì lên ổ thật.
            bool pipePlanOk = false;
            try
            {
                Func<string, string> err = delegate(string text)
                {
                    try { return Json.Str(Json.AsObject(Json.Parse(text)), "error", ""); }
                    catch { return "parse"; }
                };
                Func<string, Dictionary<string, object>, PipeRequest> mk =
                    delegate(string cmd, Dictionary<string, object> a)
                {
                    PipeRequest r = new PipeRequest();
                    r.Version = 1;
                    r.Id = "t1";
                    r.Cmd = cmd;
                    r.Args = a;
                    return r;
                };

                Dictionary<string, object> a1 = new Dictionary<string, object>();
                a1["drive"] = "C";
                string systemDrive = err(PipeCommands.Handle(mk("clean.plan", a1), null));

                Dictionary<string, object> a2 = new Dictionary<string, object>();
                a2["drive"] = "E:\\sub";
                string subPath = err(PipeCommands.Handle(mk("clean.plan", a2), null));

                Dictionary<string, object> a3 = new Dictionary<string, object>();
                a3["drive"] = "\\\\server\\share";
                string uncPath = err(PipeCommands.Handle(mk("clean.plan", a3), null));

                Dictionary<string, object> a4 = new Dictionary<string, object>();
                a4["drive"] = "";
                string emptyDrive = err(PipeCommands.Handle(mk("clean.plan", a4), null));

                Dictionary<string, object> a5 = new Dictionary<string, object>();
                a5["plan"] = "nope";
                string noConfirm = err(PipeCommands.Handle(mk("clean.apply", a5), null));

                Dictionary<string, object> j1 = new Dictionary<string, object>();
                j1["kind"] = "scan";
                j1["drive"] = "C";
                string jobSystem = err(PipeCommands.Handle(mk("job.start", j1), null));
                Dictionary<string, object> j2 = new Dictionary<string, object>();
                j2["job"] = "nope";
                string jobMissing = err(PipeCommands.Handle(mk("job.status", j2), null));

                PipePlan plan = new PipePlan();
                plan.Id = "p-test";
                plan.Kind = "clean";
                plan.Drive = "Q";
                plan.Root = root;
                plan.Serial = Native.VolumeSerial(root);
                plan.Sid = "S-1-5-21-1";
                plan.Session = 7;
                plan.CreatedUtc = DateTime.UtcNow;

                DateTime now = DateTime.UtcNow;
                string okPlan = PipeCommands.PlanProblem(plan, plan.Sid, 7, root, now);
                plan.CreatedUtc = now.AddSeconds(-(PipeCommands.PlanTtlSeconds + 5));
                string expired = PipeCommands.PlanProblem(plan, plan.Sid, 7, root, now);
                plan.CreatedUtc = now;
                string badSid = PipeCommands.PlanProblem(plan, "S-1-5-21-2", 7, root, now);
                string badSession = PipeCommands.PlanProblem(plan, plan.Sid, 8, root, now);
                string badRoot = PipeCommands.PlanProblem(plan, plan.Sid, 7,
                    Path.Combine(root, "other"), now);
                string missingPlan = PipeCommands.PlanProblem(null, plan.Sid, 7, root, now);
                string badSerial = "";
                if (plan.Serial != 0)
                {
                    plan.Serial = plan.Serial + 1;
                    badSerial = PipeCommands.PlanProblem(plan, plan.Sid, 7, root, now);
                    plan.Serial = plan.Serial - 1;
                }

                pipePlanOk = systemDrive == "refused"
                    && subPath == "bad-args"
                    && uncPath == "bad-args"
                    && emptyDrive == "bad-args"
                    && noConfirm == "bad-args"
                    && okPlan.Length == 0
                    && expired == "expired"
                    && badSid == "refused"
                    && badSession == "refused"
                    && badRoot == "refused"
                    && missingPlan == "not-found"
                    && jobSystem == "refused"
                    && jobMissing == "not-found"
                    && (plan.Serial == 0 || badSerial == "refused");
            }
            catch { }
            check(S.Selftest.PipePlan, pipePlanOk);

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

            // 48. Chữ ký số: tệp văn bản không có chữ ký là Unsigned; một tệp hệ
            //     thống có chữ ký phải xác minh được qua WinVerifyTrust, không mạng.
            bool signatureOk = false;
            try
            {
                string plain = Path.Combine(root, "unsigned.txt");
                File.WriteAllText(plain, "khong co chu ky", new UTF8Encoding(false));
                bool unsigned = Signature.Verify(plain) == SignatureStatus.Unsigned
                             && !Detect.IsSigned(plain);

                // Bỏ qua phần tệp hệ thống nếu máy không có tệp nào trong danh sách.
                string signedPath = "";
                string[] candidates = new string[]
                {
                    Path.Combine(Environment.SystemDirectory, "notepad.exe"),
                    Native.SysExe("cmd.exe"),
                    Native.ShellExe("explorer.exe")
                };
                foreach (string candidate in candidates)
                {
                    if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                    {
                        signedPath = candidate;
                        break;
                    }
                }
                bool signedOk = signedPath.Length == 0
                             || (Signature.Verify(signedPath) == SignatureStatus.Signed
                                 && Detect.IsSigned(signedPath));
                signatureOk = unsigned && signedOk;
            }
            catch { }
            check(S.Selftest.SignatureVerify, signatureOk);

            // 49. Đường dẫn tiến trình: đọc được tiến trình hiện tại (32-bit trên
            //     Windows 64-bit) và PID hệ thống không ném lỗi.
            bool processPathOk = false;
            try
            {
                int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                string actual = Native.ProcessImagePath(pid);
                string expected = Assembly.GetEntryAssembly().Location;
                bool same = actual.Length > 0
                         && string.Equals(Path.GetFullPath(actual), Path.GetFullPath(expected),
                                          StringComparison.OrdinalIgnoreCase);
                Native.ProcessImagePath(4); // PID hệ thống: phải trả rỗng, không ném.
                processPathOk = same;
            }
            catch { }
            check(S.Selftest.ProcessPath, processPathOk);

            // 50. Bàn phím trong/ngoài theo bus: ACPI, ROOT, SWD và HID không VID
            //     là trong; USB, USB HID, Bluetooth và composite là ngoài.
            bool keyboardBusOk = false;
            try
            {
                bool acpiOk = Devices.IsInternalKeyboard(@"ACPI\PNP0303\4&2A1B3C4D&0");
                bool rootOk = Devices.IsInternalKeyboard(@"ROOT\RDP_KBD\0000");
                bool swdOk = Devices.IsInternalKeyboard(@"SWD\MMDEVAPI\{0.0.0.00000000}.{1}");
                bool i2cOk = Devices.IsInternalKeyboard(@"HID\MSFT0001&Col01\5&1A2B3C4D&0&0000");
                bool usbOk = !Devices.IsInternalKeyboard(@"USB\VID_046D&PID_C31C\5&1A2B3C4D&0&1");
                bool usbHidOk = !Devices.IsInternalKeyboard(@"HID\VID_046D&PID_C31C&MI_00\7&2B3C4D5E&0&0000");
                bool btOk = !Devices.IsInternalKeyboard(
                    @"BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001000F_PID&0000\8&3C4D5E6F&0&1");
                bool compositeOk = !Devices.IsInternalKeyboard(@"USB\VID_1234&PID_5678&MI_00\6&4D5E6F70&0&0000");
                keyboardBusOk = acpiOk && rootOk && swdOk && i2cOk
                             && usbOk && usbHidOk && btOk && compositeOk;
            }
            catch { }
            check(S.Selftest.KeyboardBus, keyboardBusOk);

            // 51. Ngưỡng điểm có hiệu lực: cùng tệp .pdf.exe là High ở ngưỡng mặc
            //     định nhưng chỉ còn cảnh báo khi rules.json nâng ngưỡng lên 10.
            bool scoreGateOk = false;
            try
            {
                string gate = Path.Combine(root, "gate");
                Directory.CreateDirectory(gate);
                File.WriteAllText(Path.Combine(gate, "baocao.pdf.exe"), "MZ",
                                  new UTF8Encoding(false));
                UsbScanResult normal = UsbScan.Detect(gate, null, null, 0);
                bool highAtDefault = normal.Items.Count == 1 && normal.Items[0].High
                                  && normal.Items[0].Score >= Rules.Current.ScoreThreshold;

                File.WriteAllText(Paths.RulesFile,
                    "{ \"scoreThreshold\": 10, \"weights\": { \"double-extension\": 5 } }",
                    new UTF8Encoding(false));
                Rules.Reload();
                UsbScanResult strict = UsbScan.Detect(gate, null, null, 0);
                bool lowAtStrict = strict.Items.Count == 1 && !strict.Items[0].High
                                && strict.Items[0].Score == 5;
                scoreGateOk = highAtDefault && lowAtStrict;
            }
            catch { }
            finally
            {
                try
                {
                    File.WriteAllText(Paths.RulesFile, Rules.DefaultJson,
                                      new UTF8Encoding(false));
                    Rules.Reload();
                }
                catch { }
            }
            check(S.Selftest.ScoreGate, scoreGateOk);

            // 52. Cây sâu hơn 300 ký tự: Tree.Walk dùng WinPath nên vẫn quét tới
            //     tệp cuối thay vì bỏ cả nhánh vì PathTooLongException.
            bool deepWalkOk = false;
            List<string> deepDirs = new List<string>();
            string deepSeed = "";
            string deepFile = "";
            try
            {
                string deepRoot = Path.Combine(root, "deepscan");
                Directory.CreateDirectory(deepRoot);
                deepDirs.Add(deepRoot);
                string deepDir = deepRoot;
                while (deepDir.Length < 310)
                {
                    deepDir = Path.Combine(deepDir, "0123456789");
                    deepDirs.Add(deepDir);
                }
                bool built = WinPath.CreateDirTree(WinPath.Ext(deepDir));
                deepSeed = Path.Combine(root, "deep-seed.bin");
                File.WriteAllText(deepSeed, "MZ", new UTF8Encoding(false));
                deepFile = Path.Combine(deepDir, "baocao.pdf.exe");
                bool made = built
                         && WinPath.CopyFile(WinPath.Ext(deepSeed), WinPath.Ext(deepFile)) == 0;

                UsbScanResult deep = UsbScan.Detect(deepRoot, null, null, 100);
                bool found = false;
                foreach (UsbItem it in deep.Items)
                {
                    if (!string.Equals(Path.GetFileName(it.Path), "baocao.pdf.exe",
                                       StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (string code in it.Codes)
                        if (code.StartsWith("double-extension", StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    break;
                }
                deepWalkOk = made && found;
            }
            catch { }
            if (deepFile.Length > 0) { try { WinPath.DeleteFile(WinPath.Ext(deepFile)); } catch { } }
            for (int i = deepDirs.Count - 1; i >= 0; i--)
                WinPath.RemoveDir(WinPath.Ext(deepDirs[i]));
            if (deepSeed.Length > 0) { try { File.Delete(deepSeed); } catch { } }
            check(S.Selftest.DeepWalk, deepWalkOk);

            // 53. Chu ky: tep khong phai PE duoc tra loi ngay va vao cache, khong
            //     goi WinVerifyTrust moi lan quet lai.
            bool sigCacheOk = false;
            try
            {
                string plain = Path.Combine(root, "sig-cache.txt");
                File.WriteAllText(plain, "khong phai PE", new UTF8Encoding(false));
                int before = Signature.CacheCount;
                SignatureStatus first = Signature.Verify(plain);
                SignatureStatus second = Signature.Verify(plain);
                sigCacheOk = first == SignatureStatus.Unsigned
                          && second == SignatureStatus.Unsigned
                          && Signature.CacheCount > before;
            }
            catch { }
            check(S.Selftest.SignatureCache, sigCacheOk);

            // 54. Macro: ten vbaProject.bin chi nam trong 1 MB cuoi (central
            //     directory). Ban cu doc 8 MB dau nen bo sot tep lon.
            bool macroTailOk = false;
            string macroFile = Path.Combine(root, "tail.docx");
            try
            {
                byte[] big = new byte[9 * 1024 * 1024];
                byte[] macroMarker = Encoding.ASCII.GetBytes("vbaProject.bin");
                Array.Copy(macroMarker, 0, big, big.Length - 4096, macroMarker.Length);
                File.WriteAllBytes(macroFile, big);

                ScanContext ctx = new ScanContext();
                ctx.Rules = Rules.Current;
                TreeItem ti = new TreeItem();
                ti.Path = macroFile;
                ti.Name = "tail.docx";
                ti.IsDir = false;
                ti.Depth = 1;
                foreach (Finding fd in DetectorPipeline.InspectFile(ctx, ti))
                    if (fd.Code == "office-macro" && fd.FlagOnly) macroTailOk = true;
            }
            catch { }
            try { File.Delete(macroFile); } catch { }
            check(S.Selftest.MacroTail, macroTailOk);

            // 55. ADS: tren NTFS thi thay stream an, tren FAT32/exFAT thi bo qua
            //     truoc khi goi FindFirstStreamW cho tung tep.
            bool adsGateOk = false;
            string adsHost = Path.Combine(root, "ads-host.txt");
            try
            {
                File.WriteAllText(adsHost, "host", new UTF8Encoding(false));
                bool made = false;
                try
                {
                    File.WriteAllText(adsHost + ":evil.exe", "MZ", new UTF8Encoding(false));
                    made = true;
                }
                catch { }

                ScanContext ctx = new ScanContext();
                ctx.Rules = Rules.Current;
                TreeItem ti = new TreeItem();
                ti.Path = adsHost;
                ti.Name = "ads-host.txt";
                ti.IsDir = false;
                ti.Depth = 1;
                ti.Attrs = FileAttributes.Normal;

                ctx.IsNtfs = true;
                bool on = false;
                foreach (Finding fd in DetectorPipeline.InspectFile(ctx, ti))
                    if (fd.Code == "hidden-ads") on = true;
                ctx.IsNtfs = false;
                bool off = false;
                foreach (Finding fd in DetectorPipeline.InspectFile(ctx, ti))
                    if (fd.Code == "hidden-ads") off = true;

                adsGateOk = made ? (on && !off) : !off;
            }
            catch { }
            try { File.Delete(adsHost + ":evil.exe"); } catch { }
            try { File.Delete(adsHost); } catch { }
            check(S.Selftest.AdsNtfs, adsGateOk);

            // 56. WriteProtect: ghi chú wp-pending còn hạn thì giữ khoá đang mở;
            //     hết hạn hoặc hỏng thì phải bật lại. Không chạm registry.
            bool wpPendingOk = false;
            try
            {
                DateTime now = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
                string future = "pid=1\r\nuntil=" +
                    now.AddMinutes(5).ToString("o", CultureInfo.InvariantCulture) + "\r\n";
                string past = "pid=1\r\nuntil=" +
                    now.AddMinutes(-1).ToString("o", CultureInfo.InvariantCulture) + "\r\n";
                wpPendingOk = !Policy.PendingExpired(future, now)
                           && Policy.PendingExpired(past, now)
                           && Policy.PendingExpired("rac", now)
                           && Policy.PendingExpired("pid=1\r\n", now)
                           // Chỉ chặn chiều bật lại; người dùng tắt thì vẫn đi qua.
                           && Policy.BlockedByPause(true, false, true)
                           && !Policy.BlockedByPause(false, false, true)
                           && !Policy.BlockedByPause(true, true, true);
            }
            catch { }
            check(S.Selftest.WriteProtectPending, wpPendingOk);

            // 57. Quét lại thông minh: 300 .txt không kích hoạt, 30 .lnk thì có;
            //     cùng ổ chỉ báo lại sau 10 phút.
            bool smartRescanOk = false;
            try
            {
                RulesData r = Rules.Current;
                DateTime now = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
                smartRescanOk = !BehaviorMonitor.ShouldAlert(300, 0, r)
                             && BehaviorMonitor.ShouldAlert(30, 30, r)
                             && BehaviorMonitor.ShouldAlert(500, 1, r)
                             && !BehaviorMonitor.ShouldAlert(399, 14, r)
                             && !BehaviorMonitor.CooldownElapsed(now.AddMinutes(-2), now,
                                                                 r.BehaviorAlertSeconds)
                             && BehaviorMonitor.CooldownElapsed(now.AddMinutes(-11), now,
                                                                r.BehaviorAlertSeconds)
                             && r.IsBurstRiskExt(".lnk") && r.IsBurstRiskExt(".exe")
                             && r.IsBurstRiskExt(".vbs") && !r.IsBurstRiskExt(".txt")
                             && !r.IsBurstRiskExt(".jpg");
            }
            catch { }
            check(S.Selftest.SmartRescan, smartRescanOk);

            // 58. chkdsk nhận câu trả lời qua stdin. Không chạy chkdsk trong
            //     self-test; dùng findstr để chứng minh dữ liệu vào tới tiến trình con.
            bool chkdskStdinOk = false;
            try
            {
                string stdinOut;
                int stdinCode = Native.Run(Native.SysExe("findstr.exe"), "w15",
                                           10000, "w15-stdin\r\n", out stdinOut);
                chkdskStdinOk = stdinCode == 0 && stdinOut != null
                             && stdinOut.IndexOf("w15-stdin", StringComparison.Ordinal) >= 0;
            }
            catch { }
            check(S.Selftest.ChkdskStdin, chkdskStdinOk);

            // 59. Hàng đợi sự kiện: ID tăng dần, đọc từ ID cũ chỉ trả mục mới,
            //     xoay tệp vẫn giữ con trỏ, dòng 4 cột cũ vẫn đọc được.
            bool eventQueueOk = false;
            try
            {
                Paths.EnsureDirs();
                Events.Add("selftest", "INFO", "w16-a", "one");
                Events.Add("selftest", "INFO", "w16-b", "two");
                Events.Add("selftest", "INFO", "w16-c", "three");

                EventEntry a = null, b = null, c = null;
                foreach (EventEntry e in Events.ReadSince(0))
                {
                    if (e.Subject == "w16-a") a = e;
                    else if (e.Subject == "w16-b") b = e;
                    else if (e.Subject == "w16-c") c = e;
                }
                bool ordered = a != null && b != null && c != null
                            && a.Id > 0 && b.Id > a.Id && c.Id > b.Id;

                bool sinceOk = false;
                if (ordered)
                {
                    bool sawA = false, sawB = false, sawC = false;
                    foreach (EventEntry e in Events.ReadSince(a.Id))
                    {
                        if (e.Subject == "w16-a") sawA = true;
                        else if (e.Subject == "w16-b") sawB = true;
                        else if (e.Subject == "w16-c") sawC = true;
                    }
                    sinceOk = !sawA && sawB && sawC;
                }

                // Xoay tệp: chuyển tệp hiện tại sang .1 rồi ghi tiếp.
                bool rotateOk = false;
                if (ordered)
                {
                    string old = Paths.EventsFile + ".1";
                    try { File.Delete(old); } catch { }
                    File.Move(Paths.EventsFile, old);
                    Events.Add("selftest", "INFO", "w16-d", "four");
                    foreach (EventEntry e in Events.ReadSince(c.Id))
                        if (e.Subject == "w16-d" && e.Id > c.Id) rotateOk = true;
                }

                // Dòng cũ 4 cột: có và không có cột severity.
                File.AppendAllText(Paths.EventsFile,
                    "2026-11-22 12:00:00\tselftest\tlegacy-four-subject\tlegacy-four-detail\r\n" +
                    "2026-11-22 12:00:01\tselftest\tWARN\tlegacy-four-warn\r\n",
                    new UTF8Encoding(false));
                bool legacyOk = false;
                bool legacyPlain = false, legacyWarn = false;
                foreach (EventEntry e in Events.Read(50))
                {
                    if (e.Subject == "legacy-four-subject"
                        && e.Detail == "legacy-four-detail" && e.Severity == "INFO")
                        legacyPlain = true;
                    if (e.Subject == "legacy-four-warn"
                        && e.Severity == "WARN" && e.Detail.Length == 0)
                        legacyWarn = true;
                }
                legacyOk = legacyPlain && legacyWarn;

                eventQueueOk = ordered && sinceOk && rotateOk && legacyOk
                            && Events.MaxId >= (c == null ? 0 : c.Id);
            }
            catch { }
            check(S.Selftest.EventQueue, eventQueueOk);

            // 60. VirusTotal: chỉ gửi hash trong URL + x-apikey, không có tên tệp;
            //     4 yêu cầu/phút; hash hoặc khoá sai thì không gửi gì.
            bool vtOk = false;
            TcpListener vtServer = null;
            string vtRequest = "";
            try
            {
                VtRateLimiter limiter = new VtRateLimiter();
                DateTime t0 = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
                bool rateOk = limiter.TryTake(t0) && limiter.TryTake(t0)
                           && limiter.TryTake(t0) && limiter.TryTake(t0)
                           && !limiter.TryTake(t0)
                           && !limiter.TryTake(t0.AddSeconds(59))
                           && limiter.TryTake(t0.AddSeconds(61));

                vtServer = new TcpListener(IPAddress.Loopback, 0);
                vtServer.Start();
                int port = ((IPEndPoint)vtServer.LocalEndpoint).Port;
                Thread server = new Thread(delegate()
                {
                    try
                    {
                        using (TcpClient client = vtServer.AcceptTcpClient())
                        using (NetworkStream stream = client.GetStream())
                        {
                            StringBuilder req = new StringBuilder();
                            int state = 0;
                            while (true)
                            {
                                int b = stream.ReadByte();
                                if (b < 0) break;
                                req.Append((char)b);
                                if (b == '\r' && (state == 0 || state == 2)) state++;
                                else if (b == '\n' && (state == 1 || state == 3)) state++;
                                else state = 0;
                                if (state == 4) break;
                            }
                            vtRequest = req.ToString();

                            string body = "{\"data\":{\"attributes\":{\"last_analysis_stats\":" +
                                          "{\"harmless\":60,\"malicious\":3,\"suspicious\":2," +
                                          "\"undetected\":5,\"timeout\":0,\"type-unsupported\":0}}}}";
                            byte[] bytes = Encoding.UTF8.GetBytes(body);
                            string head = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n" +
                                          "Content-Length: " + bytes.Length +
                                          "\r\nConnection: close\r\n\r\n";
                            byte[] headBytes = Encoding.ASCII.GetBytes(head);
                            stream.Write(headBytes, 0, headBytes.Length);
                            stream.Write(bytes, 0, bytes.Length);
                        }
                    }
                    catch { }
                });
                server.IsBackground = true;
                server.Start();

                string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
                VirusTotal.BaseUrl = "http://127.0.0.1:" + port + "/api/v3/files/";
                VtResult got = VirusTotal.Lookup(hash, "test-key");
                server.Join(5000);

                bool requestOk = vtRequest.IndexOf("GET /api/v3/files/" + hash + " HTTP/1.1",
                                                   StringComparison.Ordinal) >= 0;
                bool headerOk = vtRequest.IndexOf("x-apikey: test-key",
                                                  StringComparison.OrdinalIgnoreCase) >= 0;
                int keyCount = 0;
                for (int i = vtRequest.IndexOf("test-key", StringComparison.Ordinal); i >= 0;
                     i = vtRequest.IndexOf("test-key", i + 8, StringComparison.Ordinal))
                    keyCount++;
                bool statsOk = got.Ran && got.Ok && got.Malicious == 3 && got.Total == 70
                            && !got.Clean;

                VtResult badHash = VirusTotal.Lookup("not-a-hash", "test-key");
                VtResult noKey = VirusTotal.Lookup(hash, "");
                bool guarded = !badHash.Ran && badHash.Error.Length > 0
                            && !noKey.Ran && noKey.Error.Length > 0;

                vtOk = rateOk && requestOk && headerOk && keyCount == 1 && statsOk && guarded;
            }
            catch { }
            finally
            {
                try { VirusTotal.BaseUrl = VirusTotal.DefaultBaseUrl; } catch { }
                if (vtServer != null) { try { vtServer.Stop(); } catch { } }
            }
            check(S.Selftest.VirusTotal, vtOk);

            // 61. Khoá VirusTotal: DPAPI round-trip, không lộ trong settings/log,
            //     tệp hỏng trả rỗng, công tắc tắt thì Query không gửi gì.
            bool vtKeyOk = false;
            try
            {
                string key = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
                bool saved = VtKey.Save(key);
                string loaded = VtKey.Load();
                byte[] raw = File.ReadAllBytes(Paths.VtKeyFile);
                bool encrypted = Encoding.ASCII.GetString(raw).IndexOf(key,
                    StringComparison.Ordinal) < 0;

                Settings.SetVirusTotalEnabled(false);
                VtResult off = VirusTotal.Query(key);
                bool gated = !off.Ran && off.Error.Length > 0;

                Settings.SetVirusTotalEnabled(true);
                bool enabled = Settings.VirusTotalEnabled;
                Settings.SetVirusTotalConsent();
                bool consent = Settings.VirusTotalConsent;

                string settingsText = "";
                try { settingsText = File.ReadAllText(Paths.SettingsFile, Encoding.UTF8); }
                catch { }
                bool noLeak = settingsText.IndexOf(key, StringComparison.Ordinal) < 0;

                File.WriteAllBytes(Paths.VtKeyFile, new byte[] { 1, 2, 3, 4, 5 });
                bool corruptSafe = VtKey.Load().Length == 0;

                bool listed = Uninstaller.DataEntries(true).Contains(Paths.VtKeyFile)
                           && Uninstaller.DataEntries(false).Contains(Paths.VtKeyFile);

                VtKey.Delete();
                bool deleted = !VtKey.HasKey;

                vtKeyOk = saved && loaded == key && encrypted && gated && enabled
                       && consent && noLeak && corruptSafe && listed && deleted;
            }
            catch { }
            check(S.Selftest.VirusTotalKey, vtKeyOk);

            // 62. W19: JSON của --scan --json đọc lại được (kể cả dấu " và \),
            //     và --help liệt kê đủ lệnh mới.
            bool cliJsonOk = false;
            try
            {
                UsbScanResult sample = new UsbScanResult();
                sample.Files = 3;
                sample.Seconds = 0.25;
                UsbItem item = new UsbItem();
                item.Path = "E:\\a\"b\\x.lnk";
                item.Rel = "a\"b\\x.lnk";
                item.Kind = UsbKind.Shortcut;
                item.Score = 7;
                item.High = true;
                item.Size = 42;
                item.Codes.Add("target-on-drive");
                sample.Items.Add(item);

                Dictionary<string, object> map =
                    Json.AsObject(Json.Parse(ScanJson.Text("E:\\", sample)));
                List<object> findings = Json.AsArray(Json.Get(map, "findings"));
                Dictionary<string, object> first = findings != null && findings.Count == 1
                    ? Json.AsObject(findings[0]) : null;
                bool parsed = map != null && first != null
                    && Json.Str(map, "drive", "") == "E:\\"
                    && Json.Str(first, "relative", "") == item.Rel
                    && Json.Str(first, "path", "") == item.Path
                    && Json.Str(first, "kind", "") == "shortcut"
                    && Json.Str(first, "reason", "").Length > 0;

                // Chuoi thoat phai nam tren mot dong; khong duoc lot newline that.
                bool escaped = Json.Quote("a\"b\\c\n").IndexOf('\n') < 0;

                string help = S.Cli.Help;
                bool helpOk = help.IndexOf("--scan", StringComparison.Ordinal) >= 0
                           && help.IndexOf("--vt", StringComparison.Ordinal) >= 0
                           && help.IndexOf("--json", StringComparison.Ordinal) >= 0
                           && help.IndexOf("--version", StringComparison.Ordinal) >= 0
                           && help.IndexOf("--help", StringComparison.Ordinal) >= 0;
                cliJsonOk = parsed && escaped && helpOk;
            }
            catch { }
            check(S.Selftest.CliJson, cliJsonOk);

            // 63. W21: bộ đếm tốc độ gõ chỉ đếm sự kiện; người gõ 6/giây không
            //     vượt ngưỡng, burst 50/giây thì vượt; nghỉ > 5 giây hoặc đồng hồ
            //     lùi đều bắt đầu cửa sổ mới.
            bool inputOk = false;
            try
            {
                DateTime t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

                InputRate human = new InputRate("VID_1234&PID_0001");
                for (int i = 0; i < 12; i++) human.Add(t0.AddMilliseconds(i * 166));
                InputBurst hb = human.Snapshot();
                bool humanOk = !human.Crossed && hb.Events == 12
                            && hb.PeakPerSecond < InputRate.MinPerSecond;

                InputRate fast = new InputRate("VID_1234&PID_0001");
                for (int i = 0; i < 100; i++) fast.Add(t0.AddMilliseconds(i * 20));
                InputBurst fb = fast.Snapshot();
                bool fastOk = fast.Crossed && fb.Events == 100
                           && fb.PeakPerSecond >= InputRate.MinPerSecond
                           && fb.Seconds > 1.5 && fb.Seconds < 2.5;

                InputRate gap = new InputRate("x");
                for (int i = 0; i < 40; i++) gap.Add(t0.AddMilliseconds(i * 20));
                gap.Add(t0.AddSeconds(10));
                bool gapOk = gap.Snapshot().Events == 1 && !gap.Crossed;

                InputRate rewind = new InputRate("x");
                rewind.Add(t0.AddSeconds(10));
                rewind.Add(t0);
                bool backOk = rewind.Snapshot().Events == 1 && !rewind.Crossed;

                inputOk = humanOk && fastOk && gapOk && backOk;
            }
            catch { }
            check(S.Selftest.InputRate, inputOk);

            // 64. W22: chỉ cảnh báo khi đủ bốn mảnh; thiết bị tin cậy, shell sai
            //     cha/cửa sổ, gõ chậm, thiết bị cũ, công tắc tắt đều không báo.
            bool badUsbOk = false;
            try
            {
                DateTime t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
                DeviceInfo dev = new DeviceInfo();
                dev.Key = "VID:1234:PID:0001:SN";
                dev.Name = "Test keyboard";
                dev.Keyboard = true;
                dev.Trust = DeviceTrust.Unknown;
                dev.FirstSeen = t0.AddSeconds(-5);

                InputBurst burst = new InputBurst();
                burst.Device = "VID_1234&PID_0001";
                burst.First = t0;
                burst.Last = t0.AddSeconds(1.9);
                burst.Events = 100;
                burst.PeakPerSecond = 50;

                ShellStart shell = new ShellStart();
                shell.Name = "cmd.exe";
                shell.ParentName = "explorer.exe";
                shell.Created = t0.AddSeconds(2);
                shell.ExplorerAncestor = true;

                Settings.SetBadUsbEnabled(true);
                BadUsb.Reset();
                bool alert = BadUsb.Evaluate(burst, dev, shell, t0.AddSeconds(3)).Alert;
                bool cooldown = !BadUsb.Evaluate(burst, dev, shell, t0.AddSeconds(4)).Alert;

                dev.Trust = DeviceTrust.Trusted;
                BadUsb.Reset();
                bool trusted = !BadUsb.Evaluate(burst, dev, shell, t0.AddSeconds(3)).Alert;
                dev.Trust = DeviceTrust.Unknown;

                shell.ExplorerAncestor = false;
                BadUsb.Reset();
                bool noExplorer = !BadUsb.Evaluate(burst, dev, shell, t0.AddSeconds(3)).Alert;
                shell.ExplorerAncestor = true;

                shell.Created = t0.AddSeconds(-30);
                BadUsb.Reset();
                bool oldShell = !BadUsb.Evaluate(burst, dev, shell, t0.AddSeconds(3)).Alert;

                shell.Created = t0.AddSeconds(15);
                BadUsb.Reset();
                bool lateShell = !BadUsb.Evaluate(burst, dev, shell, t0.AddSeconds(16)).Alert;

                InputBurst slow = new InputBurst();
                slow.Device = burst.Device;
                slow.First = t0;
                slow.Last = t0.AddSeconds(1.9);
                slow.Events = 12;
                slow.PeakPerSecond = 6;
                shell.Created = t0.AddSeconds(2);
                BadUsb.Reset();
                bool slowOk = !BadUsb.Evaluate(slow, dev, shell, t0.AddSeconds(3)).Alert;

                dev.FirstSeen = t0.AddSeconds(-300);
                BadUsb.Reset();
                bool oldDevice = !BadUsb.Evaluate(burst, dev, shell, t0.AddSeconds(3)).Alert;
                dev.FirstSeen = t0.AddSeconds(-5);

                Settings.SetBadUsbEnabled(false);
                BadUsb.Reset();
                bool disabled = !BadUsb.Evaluate(burst, dev, shell, t0.AddSeconds(3)).Alert;
                Settings.SetBadUsbEnabled(true);

                // Raw Input tra ve \\?\HID#...#{GUID}; phai ghep duoc voi
                // InstanceId trong devices.json truoc khi xet trust store.
                DeviceInfo rawDev = new DeviceInfo();
                rawDev.InstanceId = @"HID\VID_1234&PID_0001\7&ABC&0&0000";
                rawDev.Vid = "1234";
                rawDev.Pid = "0001";
                bool rawOk = Devices.MatchesRaw(rawDev,
                    @"\\?\HID#VID_1234&PID_0001#7&ABC&0&0000" +
                    "#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}");

                badUsbOk = alert && cooldown && trusted && noExplorer && oldShell
                        && lateShell && slowOk && oldDevice && disabled && rawOk;
            }
            catch { }
            check(S.Selftest.BadUsb, badUsbOk);

            // 65. W22: mục badusb trong rules.json được đọc và kẹp giá trị.
            bool badUsbCfg = false;
            try
            {
                Paths.EnsureDirs();
                File.WriteAllText(Paths.RulesFile,
                    "{ \"version\": 6, \"badusb\": { \"enabled\": true, " +
                    "\"newDeviceSeconds\": 999999, \"minEvents\": -5, " +
                    "\"eventsPerSecond\": 99999, \"shellSeconds\": 0, " +
                    "\"alertSeconds\": 1, \"requireExplorerParent\": false } }",
                    new UTF8Encoding(false));
                Rules.Reload();
                RulesData d = Rules.Current;
                badUsbCfg = d.Version == 6 && d.BadUsbEnabled
                         && d.BadUsbNewDeviceSeconds == 3600
                         && d.BadUsbMinEvents == 5
                         && d.BadUsbEventsPerSecond == 10000
                         && d.BadUsbShellSeconds == 1
                         && d.BadUsbAlertSeconds == 30
                         && !d.BadUsbRequireExplorerParent;
            }
            catch { }
            finally
            {
                try
                {
                    File.WriteAllText(Paths.RulesFile, Rules.DefaultJson, new UTF8Encoding(false));
                    Rules.Reload();
                }
                catch { }
            }
            check(S.Selftest.BadUsbConfig, badUsbCfg);

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
