// ShieldGate - cau hinh khoi dong: thu muc du lieu va ngon ngu, doc truoc khi nap ung dung.
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ShieldGate
{
    internal static class Bootstrap
    {
        public const string LanguageVi = "vi";
        public const string LanguageEn = "en";

        public static string Home { get; private set; }
        public static string Language { get; private set; }

        public static string SetupFile
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShieldGate");
                return Path.Combine(dir, "setup.ini");
            }
        }

        public static bool HasSetup
        {
            get
            {
                Load();
                return !string.IsNullOrEmpty(Home) && IsLanguage(Language);
            }
        }

        public static string DefaultLanguage
        {
            get
            {
                try
                {
                    if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == LanguageVi)
                        return LanguageVi;
                }
                catch { }
                return LanguageEn;
            }
        }

        public static string DefaultHome
        {
            get
            {
                string common = null, local = null;
                try
                {
                    common = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ShieldGate");
                }
                catch { }
                try
                {
                    local = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShieldGate");
                }
                catch { }

                if (!string.IsNullOrEmpty(common) && CanWrite(common)) return common;
                if (!string.IsNullOrEmpty(local)) return local;
                return Path.Combine(Path.GetTempPath(), "ShieldGate");
            }
        }

        public static bool IsLanguage(string value)
        {
            return string.Equals(value, LanguageVi, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, LanguageEn, StringComparison.OrdinalIgnoreCase);
        }

        public static void Load()
        {
            if (Home != null && Language != null) return;

            Home = "";
            Language = "";
            try
            {
                if (!File.Exists(SetupFile)) return;
                foreach (string raw in File.ReadAllLines(SetupFile, Encoding.UTF8))
                {
                    string line = raw.TrimStart('\uFEFF').Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    if (string.Equals(key, "home", StringComparison.OrdinalIgnoreCase)) Home = value;
                    else if (string.Equals(key, "language", StringComparison.OrdinalIgnoreCase)) Language = value;
                }
            }
            catch { }
        }

        public static void Save(string home, string language)
        {
            string dir = Path.GetDirectoryName(SetupFile);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            Home = (home ?? "").Trim();
            Language = IsLanguage(language) ? language.ToLowerInvariant() : DefaultLanguage;

            StringBuilder sb = new StringBuilder();
            sb.Append("# ShieldGate setup\r\n");
            sb.Append("home=").Append(Home.Replace("\r", "").Replace("\n", "")).Append("\r\n");
            sb.Append("language=").Append(Language).Append("\r\n");
            File.WriteAllText(SetupFile, sb.ToString(), new UTF8Encoding(false));
        }

        public static bool CanWrite(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".write-test");
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }
    }
}
