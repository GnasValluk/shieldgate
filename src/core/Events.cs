// ShieldGate 2.0 - timeline su kien thiet bi / hanh vi. Tep TSV de doc bang
// bang tinh, xuat CSV hoac HTML khi nguoi dung can bao cao.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ShieldGate
{
    internal sealed class EventEntry
    {
        public DateTime Time;
        public string Kind = "";
        public string Severity = "INFO";
        public string Subject = "";
        public string Detail = "";

        public string TimeText { get { return Time.ToString("dd/MM/yyyy HH:mm:ss"); } }
    }

    internal static class Events
    {
        public const string KindDevice = "device";
        public const string KindScan = "scan";
        public const string KindBehavior = "behavior";
        public const string KindProtect = "protect";
        public const string KindRepair = "repair";

        private static readonly object Gate = new object();
        private const long MaxBytes = 2 * 1024 * 1024;

        public static void Add(string kind, string severity, string subject, string detail)
        {
            try
            {
                lock (Gate)
                {
                    Paths.EnsureDirs();
                    Rotate();
                    string line = string.Join("\t", new string[]
                    {
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                        Clean(kind), Clean(severity).ToUpperInvariant(),
                        Clean(subject), Clean(detail)
                    });
                    using (FileStream fs = new FileStream(Paths.EventsFile, FileMode.Append,
                                                          FileAccess.Write, FileShare.ReadWrite))
                    using (StreamWriter w = new StreamWriter(fs, new UTF8Encoding(false)))
                        w.Write(line + "\r\n");
                }
            }
            catch { }
        }

        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        }

        private static void Rotate()
        {
            try
            {
                FileInfo fi = new FileInfo(Paths.EventsFile);
                if (!fi.Exists || fi.Length < MaxBytes) return;
                string old = Paths.EventsFile + ".1";
                try { File.Delete(old); } catch { }
                File.Move(Paths.EventsFile, old);
            }
            catch { }
        }

        // max <= 0: doc toan bo (toi da 4 MB de khong ngon RAM).
        public static List<EventEntry> Read(int max)
        {
            List<EventEntry> list = new List<EventEntry>();
            try
            {
                if (!File.Exists(Paths.EventsFile)) return list;
                using (FileStream fs = new FileStream(Paths.EventsFile, FileMode.Open,
                                                       FileAccess.Read, FileShare.ReadWrite))
                {
                    long length = fs.Length;
                    long start = Math.Max(0, length - 4 * 1024 * 1024);
                    fs.Seek(start, SeekOrigin.Begin);
                    byte[] buffer = new byte[length - start];
                    int read = 0;
                    while (read < buffer.Length)
                    {
                        int n = fs.Read(buffer, read, buffer.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    string text = Encoding.UTF8.GetString(buffer, 0, read);
                    if (start > 0)
                    {
                        int cut = text.IndexOf('\n');
                        text = cut >= 0 ? text.Substring(cut + 1) : "";
                    }
                    foreach (string raw in text.Split('\n'))
                    {
                        string line = raw.TrimEnd('\r');
                        if (line.Length == 0) continue;
                        string[] p = line.Split('\t');
                        if (p.Length < 3) continue;
                        EventEntry e = new EventEntry();
                        DateTime t;
                        if (!DateTime.TryParseExact(p[0], "yyyy-MM-dd HH:mm:ss",
                                                   CultureInfo.InvariantCulture,
                                                   DateTimeStyles.None, out t))
                            continue;
                        e.Time = t;
                        e.Kind = p[1];
                        e.Severity = p[2];
                        e.Subject = p.Length > 3 ? p[3] : "";
                        e.Detail = p.Length > 4 ? p[4] : "";
                        list.Add(e);
                    }
                }
            }
            catch { }
            list.Reverse(); // moi nhat truoc
            if (max > 0 && list.Count > max) list = list.GetRange(0, max);
            return list;
        }

        public static bool ExportCsv(string path)
        {
            try
            {
                List<EventEntry> list = Read(0);
                list.Reverse(); // CSV theo thoi gian tang dan
                StringBuilder sb = new StringBuilder();
                sb.Append("Time,Kind,Severity,Subject,Detail\r\n");
                foreach (EventEntry e in list)
                {
                    sb.Append(Csv(e.Time.ToString("yyyy-MM-dd HH:mm:ss"))).Append(',')
                      .Append(Csv(e.Kind)).Append(',')
                      .Append(Csv(e.Severity)).Append(',')
                      .Append(Csv(e.Subject)).Append(',')
                      .Append(Csv(e.Detail)).Append("\r\n");
                }
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                return true;
            }
            catch { return false; }
        }

        public static bool ExportHtml(string path)
        {
            try
            {
                List<EventEntry> list = Read(0);
                list.Reverse();
                StringBuilder sb = new StringBuilder();
                sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\">")
                  .Append("<title>ShieldGate - Events</title><style>")
                  .Append("body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#1b1b1f}")
                  .Append("h1{font-size:20px}table{border-collapse:collapse;width:100%}")
                  .Append("th,td{border:1px solid #d8d8de;padding:6px 8px;text-align:left;font-size:13px}")
                  .Append("th{background:#f2f2f6}tr:nth-child(even){background:#fafafc}")
                  .Append(".WARN{color:#9a5b00}.ERROR{color:#b00020}")
                  .Append("</style></head><body><h1>ShieldGate ")
                  .Append(Html(AppInfo.Version)).Append(" - event timeline</h1>")
                  .Append("<p>").Append(Html(DateTime.Now.ToString("yyyy-MM-dd HH:mm"))).Append("</p>")
                  .Append("<table><thead><tr><th>Time</th><th>Kind</th><th>Level</th>")
                  .Append("<th>Subject</th><th>Detail</th></tr></thead><tbody>");
                foreach (EventEntry e in list)
                {
                    sb.Append("<tr><td>").Append(Html(e.TimeText)).Append("</td><td>")
                      .Append(Html(e.Kind)).Append("</td><td class=\"")
                      .Append(Html(e.Severity)).Append("\">").Append(Html(e.Severity))
                      .Append("</td><td>").Append(Html(e.Subject)).Append("</td><td>")
                      .Append(Html(e.Detail)).Append("</td></tr>");
                }
                sb.Append("</tbody></table></body></html>");
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                return true;
            }
            catch { return false; }
        }

        private static string Csv(string s)
        {
            string v = s ?? "";
            // CSV injection: Excel/Sheets thực thi ô mở đầu bằng = + - @ (và tab,
            // CR). Thêm nháy đơn để bảng tính coi là chữ. Tệp TSV nội bộ không đổi.
            if (v.Length > 0)
            {
                char c = v[0];
                if (c == '=' || c == '+' || c == '-' || c == '@' || c == '\t' || c == '\r')
                    v = "'" + v;
            }
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        }

        private static string Html(string s)
        {
            return (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;")
                            .Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}
