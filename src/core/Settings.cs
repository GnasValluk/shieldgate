// ShieldGate - thiết lập nhỏ: lần kiểm tra gần nhất của máy và từng ổ.
// Tệp key=value trong thư mục dữ liệu. Không chứa thông tin cá nhân.
// Giao diện và bảo vệ nền cùng ghi tệp này: mỗi lần ghi phải nạp lại bản trên đĩa
// và khoá liên tiến trình, nếu không bên này sẽ xoá thay đổi của bên kia.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace ShieldGate
{
    internal static class Settings
    {
        private const string MutexName = @"Global\ShieldGate_Settings_1";
        private static readonly object Gate = new object();
        private static Dictionary<string, string> _map;
        private static DateTime _stamp = DateTime.MinValue;

        private static DateTime Stamp()
        {
            try
            {
                return File.Exists(Paths.SettingsFile)
                    ? File.GetLastWriteTimeUtc(Paths.SettingsFile)
                    : DateTime.MinValue;
            }
            catch { return DateTime.MinValue; }
        }

        private static Dictionary<string, string> Load()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(Paths.SettingsFile))
                {
                    foreach (string raw in File.ReadAllLines(Paths.SettingsFile, Encoding.UTF8))
                    {
                        string line = raw.TrimStart('\uFEFF');
                        if (line.Length == 0 || line[0] == '#') continue;
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        map[line.Substring(0, eq)] = line.Substring(eq + 1);
                    }
                }
            }
            catch { }
            _stamp = Stamp();
            return map;
        }

        private static Dictionary<string, string> Map()
        {
            if (_map == null || Stamp() != _stamp) _map = Load();
            return _map;
        }

        public static string Get(string key, string fallback)
        {
            lock (Gate)
            {
                string v;
                return Map().TryGetValue(key, out v) ? v : fallback;
            }
        }

        public static void Set(string key, string value)
        {
            Dictionary<string, string> one = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            one[key] = value;
            SetMany(one);
        }

        public static void SetMany(IDictionary<string, string> values)
        {
            lock (Gate)
            {
                Mutex gate = null;
                try { gate = new Mutex(false, MutexName); }
                catch { }
                try
                {
                    try { if (gate != null) gate.WaitOne(5000); } catch { }
                    _map = Load();
                    foreach (KeyValuePair<string, string> kv in values)
                        _map[kv.Key] = Clean(kv.Value);
                    Flush();
                    _stamp = Stamp();
                }
                finally
                {
                    try { if (gate != null) gate.ReleaseMutex(); } catch { }
                    if (gate != null) { try { gate.Dispose(); } catch { } }
                }
            }
        }

        private static string Clean(string value)
        {
            return (value ?? "").Replace("\r", " ").Replace("\n", " ");
        }

        private static void Flush()
        {
            try
            {
                Paths.EnsureDirs();
                List<string> keys = new List<string>(_map.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);
                StringBuilder sb = new StringBuilder();
                sb.Append("# ShieldGate - settings\r\n");
                foreach (string k in keys) sb.Append(k).Append('=').Append(_map[k]).Append("\r\n");
                string tmp = Paths.SettingsFile + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(Paths.SettingsFile))
                {
                    try { File.Replace(tmp, Paths.SettingsFile, null); }
                    catch
                    {
                        File.Delete(Paths.SettingsFile);
                        File.Move(tmp, Paths.SettingsFile);
                    }
                }
                else File.Move(tmp, Paths.SettingsFile);
            }
            catch { }
        }

        // ------------------------------------------------------------- máy tính

        public static DateTime LastPcScanTime
        {
            get
            {
                DateTime t;
                return DateTime.TryParse(Get("pc.time", ""), out t) ? t : DateTime.MinValue;
            }
        }

        public static int LastPcFiles { get { return Int("pc.files"); } }
        public static int LastPcFound { get { return Int("pc.found"); } }

        public static void SetPcScan(int files, int found)
        {
            Dictionary<string, string> v = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            v["pc.time"] = DateTime.Now.ToString("o");
            v["pc.files"] = files.ToString();
            v["pc.found"] = found.ToString();
            SetMany(v);
        }

        // --------------------------------------------------------------- ổ USB

        public static void SetDriveScan(string drive, int files, int found)
        {
            string letter = Letter(drive);
            if (letter.Length == 0) return;
            Dictionary<string, string> v = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            v["usb." + letter + ".time"] = DateTime.Now.ToString("o");
            v["usb." + letter + ".files"] = files.ToString();
            v["usb." + letter + ".found"] = found.ToString();
            SetMany(v);
        }

        public static DateTime DriveScanTime(string drive)
        {
            DateTime t;
            return DateTime.TryParse(Get("usb." + Letter(drive) + ".time", ""), out t)
                ? t : DateTime.MinValue;
        }

        public static int DriveScanFiles(string drive) { return Int("usb." + Letter(drive) + ".files"); }
        public static int DriveScanFound(string drive) { return Int("usb." + Letter(drive) + ".found"); }

        // Sự kiện gần nhất của bảo vệ nền nằm trong events.tsv (có ID). Trước đây
        // mỗi loại sự kiện ghi thêm một mốc thời gian vào settings.txt; tray chỉ
        // so sánh mốc nên nhiều sự kiện trong cùng 5 giây bị mất. Đã bỏ để chỉ
        // còn một nguồn.

        // VirusTotal là opt-in: mặc định tắt. Công tắc và hộp thoại đồng ý nằm ở
        // trang Bảo vệ; khoá API không nằm trong tệp này.
        public static bool VirusTotalEnabled
        {
            get { return string.Equals(Get("vt.enabled", "0"), "1", StringComparison.Ordinal); }
        }

        public static void SetVirusTotalEnabled(bool on)
        {
            Set("vt.enabled", on ? "1" : "0");
        }

        public static bool VirusTotalConsent
        {
            get { return string.Equals(Get("vt.consent", "0"), "1", StringComparison.Ordinal); }
        }

        public static void SetVirusTotalConsent()
        {
            Set("vt.consent", "1");
        }

        // BadUSB là thiết lập của riêng bản tray: mặc định lấy từ rules.json,
        // người dùng đổi bằng công tắc thì ghi đè trong settings.txt.
        public static bool BadUsbEnabled
        {
            get
            {
                string fallback = Rules.Current.BadUsbEnabled ? "1" : "0";
                return string.Equals(Get("badusb.enabled", fallback), "1", StringComparison.Ordinal);
            }
        }

        public static void SetBadUsbEnabled(bool on)
        {
            Set("badusb.enabled", on ? "1" : "0");
        }

        // Lần sửa máy gần nhất, để phân biệt "chưa sửa" với "sửa rồi vẫn nhiễm lại".
        public static void SetPcFixEvent(int ok, int fail)
        {
            Dictionary<string, string> v = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            v["pcfix.time"] = DateTime.Now.ToString("o");
            v["pcfix.ok"] = ok.ToString();
            v["pcfix.fail"] = fail.ToString();
            SetMany(v);
        }

        public static DateTime PcFixEventTime
        {
            get
            {
                DateTime t;
                return DateTime.TryParse(Get("pcfix.time", ""), out t) ? t : DateTime.MinValue;
            }
        }

        public static int PcFixEventOk { get { return Int("pcfix.ok"); } }

        private static int Int(string key)
        {
            int n;
            return int.TryParse(Get(key, "0"), out n) ? n : 0;
        }

        private static string Letter(string drive)
        {
            if (string.IsNullOrEmpty(drive)) return "";
            char c = char.ToUpperInvariant(drive[0]);
            return (c >= 'A' && c <= 'Z') ? c.ToString() : "";
        }

        // ------------------------------------------------------------ hiển thị

        public static string When(DateTime t)
        {
            if (t == DateTime.MinValue) return "";
            TimeSpan age = DateTime.Now - t;
            if (age.TotalSeconds < 60 && age.TotalSeconds >= -60) return S.Overview.JustNow;
            if (t.Date == DateTime.Today) return S.Overview.Today(t.ToString("HH:mm"));
            if (t.Date == DateTime.Today.AddDays(-1)) return S.Overview.Yesterday(t.ToString("HH:mm"));
            return t.ToString("dd/MM/yyyy HH:mm");
        }
    }
}
