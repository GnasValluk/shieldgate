// ShieldGate - thiết lập nhỏ: lần kiểm tra gần nhất của máy và từng ổ.
// Tệp key=value trong thư mục dữ liệu. Không chứa thông tin cá nhân.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShieldGate
{
    internal static class Settings
    {
        private static readonly object Gate = new object();
        private static Dictionary<string, string> _map;

        private static Dictionary<string, string> Map()
        {
            if (_map != null) return _map;
            _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
                        _map[line.Substring(0, eq)] = line.Substring(eq + 1);
                    }
                }
            }
            catch { }
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
            lock (Gate)
            {
                Map()[key] = (value ?? "").Replace("\r", " ").Replace("\n", " ");
                Flush();
            }
        }

        private static void Flush()
        {
            try
            {
                Paths.EnsureDirs();
                List<string> keys = new List<string>(Map().Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);
                StringBuilder sb = new StringBuilder();
                sb.Append("# ShieldGate - settings\r\n");
                foreach (string k in keys) sb.Append(k).Append('=').Append(Map()[k]).Append("\r\n");
                string tmp = Paths.SettingsFile + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(Paths.SettingsFile)) File.Delete(Paths.SettingsFile);
                File.Move(tmp, Paths.SettingsFile);
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
            Set("pc.time", DateTime.Now.ToString("o"));
            Set("pc.files", files.ToString());
            Set("pc.found", found.ToString());
        }

        // --------------------------------------------------------------- ổ USB

        public static void SetDriveScan(string drive, int files, int found)
        {
            string letter = Letter(drive);
            if (letter.Length == 0) return;
            Set("usb." + letter + ".time", DateTime.Now.ToString("o"));
            Set("usb." + letter + ".files", files.ToString());
            Set("usb." + letter + ".found", found.ToString());
        }

        public static DateTime DriveScanTime(string drive)
        {
            DateTime t;
            return DateTime.TryParse(Get("usb." + Letter(drive) + ".time", ""), out t)
                ? t : DateTime.MinValue;
        }

        public static int DriveScanFiles(string drive) { return Int("usb." + Letter(drive) + ".files"); }
        public static int DriveScanFound(string drive) { return Int("usb." + Letter(drive) + ".found"); }

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
