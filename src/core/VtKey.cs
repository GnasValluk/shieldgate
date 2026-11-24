// ShieldGate - khoa API VirusTotal, ma hoa bang DPAPI cua nguoi dung hien tai.
// Khong ghi khoa vao settings.txt hay log; tep chi chua ciphertext.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ShieldGate
{
    internal static class VtKey
    {
        // Entropy co dinh chi de tranh giai ma nham boi ung dung khac cung user.
        private static readonly byte[] Entropy =
            Encoding.UTF8.GetBytes("ShieldGate.VirusTotal.v1");

        public static bool HasKey
        {
            get { return Load().Length > 0; }
        }

        public static bool Save(string key)
        {
            if (!VirusTotal.IsApiKey(key)) return false;
            try
            {
                Paths.EnsureDirs();
                byte[] plain = Encoding.UTF8.GetBytes(key.Trim());
                byte[] encrypted = ProtectedData.Protect(plain, Entropy,
                                                         DataProtectionScope.CurrentUser);
                File.WriteAllBytes(Paths.VtKeyFile, encrypted);
                return true;
            }
            catch { return false; }
        }

        // Tep hong, bi copy tu may khac, hoac khong phai cua user nay: tra rong
        // de tinh nang chi don gian la "chua co khoa", khong crash.
        public static string Load()
        {
            try
            {
                if (!File.Exists(Paths.VtKeyFile)) return "";
                byte[] encrypted = File.ReadAllBytes(Paths.VtKeyFile);
                byte[] plain = ProtectedData.Unprotect(encrypted, Entropy,
                                                       DataProtectionScope.CurrentUser);
                string key = Encoding.UTF8.GetString(plain).Trim();
                return VirusTotal.IsApiKey(key) ? key : "";
            }
            catch { return ""; }
        }

        public static void Delete()
        {
            try { File.Delete(Paths.VtKeyFile); }
            catch { }
        }
    }
}
