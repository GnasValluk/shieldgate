// ShieldGate 2.0 - JSON toi thieu: doc/ghi cau hinh va luat ma khong can thu vien ngoai.
// Chi ho tro kieu JSON co ban; cho phep // va /* */ de nguoi dung chu thich rules.json.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ShieldGate
{
    internal static class Json
    {
        // ------------------------------------------------------------- doc

        public static object ParseFile(string path)
        {
            return Parse(File.ReadAllText(path, Encoding.UTF8));
        }

        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("JSON rong");
            int i = 0;
            object value = ParseValue(text, ref i);
            Skip(text, ref i);
            if (i < text.Length) throw new FormatException("JSON thua ky tu tai vi tri " + i);
            return value;
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c) || c == '\uFEFF') { i++; continue; }
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
                {
                    i += 2;
                    while (i < s.Length && s[i] != '\n') i++;
                    continue;
                }
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/')) i++;
                    if (i + 1 < s.Length) i += 2;
                    continue;
                }
                break;
            }
        }

        private static object ParseValue(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON ket thuc som");
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't' || c == 'f') return ParseBool(s, ref i);
            if (c == 'n') return ParseNull(s, ref i);
            return ParseNumber(s, ref i);
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            Dictionary<string, object> map =
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            i++; // {
            Skip(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return map; }
            while (true)
            {
                Skip(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("JSON can ten khoa");
                string key = ParseString(s, ref i);
                Skip(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("JSON can dau :");
                i++;
                map[key] = ParseValue(s, ref i);
                Skip(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return map; }
                throw new FormatException("JSON thieu , hoac }");
            }
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            List<object> list = new List<object>();
            i++; // [
            Skip(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(ParseValue(s, ref i));
                Skip(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return list; }
                throw new FormatException("JSON thieu , hoac ]");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            i++; // "
            StringBuilder sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("JSON \\u thieu ky tu");
                        int code;
                        if (!int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber,
                                          CultureInfo.InvariantCulture, out code))
                            throw new FormatException("JSON \\u sai");
                        sb.Append((char)code);
                        i += 4;
                        break;
                    default: throw new FormatException("JSON escape la: \\" + e);
                }
            }
            throw new FormatException("JSON chuoi khong dong");
        }

        private static bool ParseBool(string s, ref int i)
        {
            if (Starts(s, i, "true")) { i += 4; return true; }
            if (Starts(s, i, "false")) { i += 5; return false; }
            throw new FormatException("JSON bool sai");
        }

        private static object ParseNull(string s, ref int i)
        {
            if (Starts(s, i, "null")) { i += 4; return null; }
            throw new FormatException("JSON null sai");
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length)
            {
                char c = s[i];
                if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' ||
                    c == '+' || c == '-') i++;
                else break;
            }
            string raw = s.Substring(start, i - start);
            double d;
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                throw new FormatException("JSON so sai: " + raw);
            return d;
        }

        private static bool Starts(string s, int i, string what)
        {
            return i + what.Length <= s.Length &&
                   string.CompareOrdinal(s, i, what, 0, what.Length) == 0;
        }

        // ------------------------------------------------------------- truy van

        public static Dictionary<string, object> AsObject(object value)
        {
            return value as Dictionary<string, object>;
        }

        public static List<object> AsArray(object value)
        {
            return value as List<object>;
        }

        public static object Get(Dictionary<string, object> map, string key)
        {
            object v;
            return map != null && map.TryGetValue(key, out v) ? v : null;
        }

        public static string Str(Dictionary<string, object> map, string key, string fallback)
        {
            object v = Get(map, key);
            return v == null ? fallback : Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static int Int(Dictionary<string, object> map, string key, int fallback)
        {
            object v = Get(map, key);
            if (v == null) return fallback;
            try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        public static bool Bool(Dictionary<string, object> map, string key, bool fallback)
        {
            object v = Get(map, key);
            if (v == null) return fallback;
            try { return Convert.ToBoolean(v, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        public static List<string> StringList(Dictionary<string, object> map, string key)
        {
            List<string> list = new List<string>();
            List<object> arr = AsArray(Get(map, key));
            if (arr == null) return list;
            foreach (object o in arr)
            {
                string s = Convert.ToString(o, CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(s)) list.Add(s);
            }
            return list;
        }

        // ------------------------------------------------------------- ghi

        public static string Escape(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        public static string Serialize(object value, bool pretty)
        {
            StringBuilder sb = new StringBuilder();
            Write(sb, value, pretty, 0);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object value, bool pretty, int depth)
        {
            if (value == null) { sb.Append("null"); return; }
            string s = value as string;
            if (s != null) { sb.Append('"').Append(Escape(s)).Append('"'); return; }
            if (value is bool) { sb.Append((bool)value ? "true" : "false"); return; }
            if (value is int || value is long || value is short || value is byte)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }
            if (value is double || value is float || value is decimal)
            {
                double d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (d == Math.Floor(d) && Math.Abs(d) < 1e15)
                    sb.Append(((long)d).ToString(CultureInfo.InvariantCulture));
                else sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            Dictionary<string, object> map = value as Dictionary<string, object>;
            if (map != null)
            {
                if (map.Count == 0) { sb.Append("{}"); return; }
                sb.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, object> kv in map)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    if (pretty) NewLine(sb, depth + 1);
                    sb.Append('"').Append(Escape(kv.Key)).Append('"').Append(':');
                    if (pretty) sb.Append(' ');
                    Write(sb, kv.Value, pretty, depth + 1);
                }
                if (pretty) NewLine(sb, depth);
                sb.Append('}');
                return;
            }
            System.Collections.IEnumerable seq = value as System.Collections.IEnumerable;
            if (seq != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (object o in seq)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    if (pretty) NewLine(sb, depth + 1);
                    Write(sb, o, pretty, depth + 1);
                }
                if (pretty) NewLine(sb, depth);
                sb.Append(']');
                return;
            }
            sb.Append('"').Append(Escape(Convert.ToString(value, CultureInfo.InvariantCulture))).Append('"');
        }

        private static void NewLine(StringBuilder sb, int depth)
        {
            sb.Append("\r\n");
            for (int i = 0; i < depth; i++) sb.Append("  ");
        }
    }
}
