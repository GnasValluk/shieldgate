// ShieldGate 2.0 - timeline su kien thiet bi / hanh vi. Tep TSV de doc bang
// bang tinh, xuat CSV hoac HTML khi nguoi dung can bao cao.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace ShieldGate
{
    internal sealed class EventEntry
    {
        public long Id;
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
        // Thông báo cho tray: quét ổ xong và ổ bị nhiễm lại. Tách khỏi behavior
        // để tray phân biệt bùng nổ ghi với nhiễm lại.
        public const string KindDrive = "drive";
        public const string KindReinfect = "reinfect";

        private static readonly object Gate = new object();
        private const long MaxBytes = 2 * 1024 * 1024;

        private static string IdFile { get { return Paths.EventsFile + ".id"; } }
        private static string LockFile { get { return Paths.EventsFile + ".lock"; } }

        public static void Add(string kind, string severity, string subject, string detail)
        {
            try
            {
                lock (Gate)
                {
                    Paths.EnsureDirs();
                    // Khoá tệp để hai tiến trình (GUI và bảo vệ nền) không cấp
                    // trùng ID hoặc ghi xen dòng.
                    using (FileStream held = AcquireLock())
                    {
                        Rotate();
                        long id = NextId();
                        string line = string.Join("\t", new string[]
                        {
                            id.ToString(CultureInfo.InvariantCulture),
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
            }
            catch { }
        }

        // Giữ khoá liên tiến trình ngắn hạn; hết 1 giây thì chấp nhận chạy chỉ
        // với khoá trong tiến trình thay vì treo thông báo.
        private static FileStream AcquireLock()
        {
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    return new FileStream(LockFile, FileMode.OpenOrCreate,
                                          FileAccess.ReadWrite, FileShare.None, 1);
                }
                catch { Thread.Sleep(10); }
            }
            return null;
        }

        // Bộ đếm nằm ngoài tệp sự kiện nên xoay tệp không làm mất con trỏ.
        // Mất tệp .id thì lấy ID lớn nhất còn trong events.tsv/.1 rồi +1.
        private static long NextId()
        {
            long id = 0;
            try
            {
                if (File.Exists(IdFile))
                {
                    string text = File.ReadAllText(IdFile).Trim();
                    long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
                }
            }
            catch { }
            if (id <= 0) id = Math.Max(MaxIdInFile(Paths.EventsFile + ".1"),
                                       MaxIdInFile(Paths.EventsFile));
            id++;
            try
            {
                File.WriteAllText(IdFile, id.ToString(CultureInfo.InvariantCulture),
                                  new UTF8Encoding(false));
            }
            catch { }
            return id;
        }

        // ID lớn nhất đang có; tray dùng lúc khởi động để bỏ qua sự kiện cũ.
        public static long MaxId
        {
            get
            {
                try
                {
                    return Math.Max(MaxIdInFile(Paths.EventsFile + ".1"),
                                    MaxIdInFile(Paths.EventsFile));
                }
                catch { return 0; }
            }
        }

        private static long MaxIdInFile(string path)
        {
            long max = 0;
            try
            {
                if (!File.Exists(path)) return 0;
                using (FileStream fs = new FileStream(path, FileMode.Open,
                                                       FileAccess.Read, FileShare.ReadWrite))
                {
                    long length = fs.Length;
                    long start = Math.Max(0, length - 256 * 1024);
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
                        string[] p = raw.TrimEnd('\r').Split('\t');
                        if (p.Length < 5) continue;
                        long id;
                        if (long.TryParse(p[0], NumberStyles.Integer,
                                          CultureInfo.InvariantCulture, out id) && id > max)
                            max = id;
                    }
                }
            }
            catch { }
            return max;
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
            List<EventEntry> list = ReadFile(Paths.EventsFile, 4 * 1024 * 1024);
            list.Reverse(); // moi nhat truoc
            if (max > 0 && list.Count > max) list = list.GetRange(0, max);
            return list;
        }

        // Su kien moi hon afterId, cu truoc. Doc ca events.tsv.1 de xoay tep
        // khong lam mat thong bao dang cho tray. Chi doc 512 KB cuoi moi tep:
        // tray hoi moi 5 giay, doc ca 4 MB hai lan moi nhip la lang phi.
        public static List<EventEntry> ReadSince(long afterId)
        {
            List<EventEntry> list = ReadFile(Paths.EventsFile + ".1", 512 * 1024);
            list.AddRange(ReadFile(Paths.EventsFile, 512 * 1024));
            List<EventEntry> result = new List<EventEntry>();
            foreach (EventEntry e in list)
                if (e.Id > afterId) result.Add(e);
            result.Sort(delegate(EventEntry a, EventEntry b)
            {
                return a.Id.CompareTo(b.Id);
            });
            return result;
        }

        private static List<EventEntry> ReadFile(string path, int maxBytes)
        {
            List<EventEntry> list = new List<EventEntry>();
            try
            {
                if (!File.Exists(path)) return list;
                using (FileStream fs = new FileStream(path, FileMode.Open,
                                                       FileAccess.Read, FileShare.ReadWrite))
                {
                    long length = fs.Length;
                    long start = Math.Max(0, length - maxBytes);
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
                        EventEntry e;
                        if (TryParse(line, out e)) list.Add(e);
                    }
                }
            }
            catch { }
            return list;
        }

        // Ho tro ca dinh dang moi (id, time, kind, severity, subject, detail)
        // va dong cu 4/5 cot (khong co id, co the khong co severity).
        private static bool TryParse(string line, out EventEntry e)
        {
            e = null;
            string[] p = line.Split('\t');
            if (p.Length < 3) return false;

            long id = 0;
            int off = 0;
            DateTime time;
            if (p.Length >= 5 && long.TryParse(p[0], NumberStyles.Integer,
                                               CultureInfo.InvariantCulture, out id)
                && TryTime(p[1], out time))
            {
                off = 1;
            }
            else if (!TryTime(p[0], out time))
            {
                return false;
            }

            e = new EventEntry();
            e.Id = id;
            e.Time = time;
            e.Kind = p[off + 1];
            string third = p[off + 2];
            if (p.Length >= off + 4 && IsSeverity(third))
            {
                e.Severity = third.ToUpperInvariant();
                e.Subject = p[off + 3];
                e.Detail = p.Length > off + 4 ? p[off + 4] : "";
            }
            else
            {
                // 4 cot cu: time, kind, subject, detail.
                e.Severity = "INFO";
                e.Subject = third;
                e.Detail = p.Length > off + 3 ? p[off + 3] : "";
            }
            return true;
        }

        private static bool TryTime(string text, out DateTime time)
        {
            return DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm:ss",
                                          CultureInfo.InvariantCulture,
                                          DateTimeStyles.None, out time);
        }

        private static bool IsSeverity(string text)
        {
            return string.Equals(text, "INFO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "WARN", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "ERROR", StringComparison.OrdinalIgnoreCase);
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
