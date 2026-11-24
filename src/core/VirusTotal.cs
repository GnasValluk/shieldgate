// ShieldGate - tra ma bam SHA-256 tren VirusTotal (opt-in).
// Day la noi DUY NHAT trong chuong trinh gui du lieu ra mang. Chi gui ma bam
// trong URL; khong gui ten tep, duong dan hay noi dung. Khoa API chi nam
// trong header x-apikey va khong bao gio duoc ghi log.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ShieldGate
{
    internal sealed class VtResult
    {
        public bool Ran;       // da thuc su gui yeu cau
        public bool Ok;        // doc duoc last_analysis_stats
        public int Status;     // HTTP status; 0 = loi mang hoac timeout
        public int Malicious;
        public int Suspicious;
        public int Harmless;
        public int Undetected;
        public int Timeout;
        public int Unsupported;
        public int Total;
        public string Error = "";

        public bool Clean
        {
            get { return Ok && Malicious == 0 && Suspicious == 0; }
        }
    }

    // 4 yeu cau/phut. Tach khoi phan gui de self-test kiem tra bang dong ho gia
    // thay vi ngu that 60 giay.
    internal sealed class VtRateLimiter
    {
        private readonly object _gate = new object();
        private readonly List<DateTime> _taken = new List<DateTime>();

        public int Capacity = 4;
        public int WindowSeconds = 60;

        public bool TryTake(DateTime now)
        {
            lock (_gate)
            {
                _taken.RemoveAll(delegate(DateTime t)
                {
                    return (now - t).TotalSeconds >= WindowSeconds;
                });
                if (_taken.Count >= Capacity) return false;
                _taken.Add(now);
                return true;
            }
        }

        public void Wait()
        {
            while (!TryTake(DateTime.UtcNow)) Thread.Sleep(500);
        }
    }

    internal static class VirusTotal
    {
        public const string DefaultBaseUrl = "https://www.virustotal.com/api/v3/files/";

        // Self-test doi sang may chu gia loopback; ban that luon la DefaultBaseUrl.
        internal static string BaseUrl = DefaultBaseUrl;
        internal static readonly VtRateLimiter Rate = new VtRateLimiter();

        private const int RequestTimeoutMs = 15000;

        public static bool IsSha256(string text)
        {
            return IsHex64(text);
        }

        // Khoa VirusTotal cung la 64 ky tu hex; kiem tra truoc khi ghi tep.
        public static bool IsApiKey(string text)
        {
            return IsHex64(text);
        }

        private static bool IsHex64(string text)
        {
            if (text == null || text.Length != 64) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')
                        || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return true;
        }

        // Chi gui khi nguoi dung da bat trong trang Bao ve va co khoa. Tat cong
        // tac thi khong co loi goi mang nao, ke ca khi con khoa da luu.
        public static VtResult Query(string sha256)
        {
            VtResult r = new VtResult();
            if (!Settings.VirusTotalEnabled)
            {
                r.Error = S.Vt.Disabled;
                return r;
            }
            string key = VtKey.Load();
            if (key.Length == 0)
            {
                r.Error = S.Vt.NoKey;
                return r;
            }
            return Lookup(sha256, key);
        }

        // Chi tra khi co khoa va ma bam hop le; khong co truong hop nao khac
        // tu dong gui du lieu.
        public static VtResult Lookup(string sha256, string apiKey)
        {
            VtResult r = new VtResult();
            if (!IsSha256(sha256))
            {
                r.Error = S.Vt.BadHash;
                return r;
            }
            apiKey = apiKey == null ? "" : apiKey.Trim();
            if (apiKey.Length == 0 || apiKey.IndexOf('\r') >= 0 || apiKey.IndexOf('\n') >= 0)
            {
                r.Error = S.Vt.NoKey;
                return r;
            }

            string url = BaseUrl + sha256.ToLowerInvariant();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (attempt > 0) Thread.Sleep(1000);
                Rate.Wait();
                r = Request(url, apiKey);
                // Loi mang/timeout hoac 5xx moi thu lai mot lan; 4xx la ket luan.
                if (r.Status != 0 && r.Status < 500) break;
            }
            return r;
        }

        private static VtResult Request(string url, string apiKey)
        {
            VtResult r = new VtResult();
            r.Ran = true;
            try
            {
                // .NET 4.0 khong co ten Tls12; 3072 la gia tri cua no.
                try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; }
                catch { }

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.UserAgent = "ShieldGate/" + AppInfo.Version;
                req.Accept = "application/json";
                req.Headers["x-apikey"] = apiKey;
                req.Timeout = RequestTimeoutMs;
                req.ReadWriteTimeout = RequestTimeoutMs;
                // Khong theo redirect: header x-apikey se bi gui sang host khac.
                req.AllowAutoRedirect = false;
                req.Proxy = null;
                req.KeepAlive = false;

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    r.Status = (int)resp.StatusCode;
                    string body = ReadLimited(resp.GetResponseStream(), 512 * 1024);
                    if (r.Status == 200) Parse(body, r);
                    else r.Error = ErrorFor(r.Status);
                }
            }
            catch (WebException e)
            {
                HttpWebResponse resp = e.Response as HttpWebResponse;
                if (resp != null)
                {
                    try { r.Status = (int)resp.StatusCode; }
                    catch { r.Status = 0; }
                    r.Error = ErrorFor(r.Status);
                    try { resp.Close(); } catch { }
                }
                else
                {
                    r.Status = 0;
                    r.Error = S.Vt.Network;
                }
            }
            catch (Exception)
            {
                r.Status = 0;
                r.Error = S.Vt.Network;
            }
            return r;
        }

        private static string ErrorFor(int status)
        {
            if (status == 404) return S.Vt.NotFound;
            if (status == 401 || status == 403) return S.Vt.Auth;
            if (status == 429) return S.Vt.RateLimited;
            return S.Vt.HttpError(status);
        }

        private static void Parse(string json, VtResult r)
        {
            Match m = Regex.Match(json,
                "\"last_analysis_stats\"\\s*:\\s*\\{([^}]*)\\}", RegexOptions.Singleline);
            if (!m.Success)
            {
                r.Error = S.Vt.NoStats;
                return;
            }

            string body = m.Groups[1].Value;
            r.Malicious = Field(body, "malicious");
            r.Suspicious = Field(body, "suspicious");
            r.Harmless = Field(body, "harmless");
            r.Undetected = Field(body, "undetected");
            r.Timeout = Field(body, "timeout");
            r.Unsupported = Field(body, "type-unsupported");
            r.Total = r.Malicious + r.Suspicious + r.Harmless + r.Undetected
                    + r.Timeout + r.Unsupported;
            r.Ok = true;
        }

        private static int Field(string json, string name)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*(\\d+)");
            int value;
            return m.Success && int.TryParse(m.Groups[1].Value, out value) ? value : 0;
        }

        private static string ReadLimited(Stream stream, int maxBytes)
        {
            if (stream == null) return "";
            StringBuilder sb = new StringBuilder();
            byte[] buffer = new byte[8192];
            int total = 0;
            while (total < maxBytes)
            {
                int n;
                try { n = stream.Read(buffer, 0, buffer.Length); }
                catch { break; }
                if (n <= 0) break;
                int use = Math.Min(n, maxBytes - total);
                sb.Append(Encoding.UTF8.GetString(buffer, 0, use));
                total += use;
            }
            return sb.ToString();
        }
    }
}
