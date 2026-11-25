// ShieldGate 3.0 - khung tin cho named pipe giữa GUI và tiến trình nền.
// Mỗi tin là 4 byte độ dài little-endian + UTF-8 JSON, tối đa 64 KiB.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShieldGate
{
    internal sealed class PipeRequest
    {
        public int Version;
        public string Id = "";
        public string Cmd = "";
        public Dictionary<string, object> Args = new Dictionary<string, object>();
        // Lỗi cú pháp: bên gọi vẫn trả lời được nếu đọc ra id.
        public string Error = "";
        public string ErrorMessage = "";
    }

    internal sealed class PipeClientInfo
    {
        public uint ProcessId;
        public uint SessionId;
        public string ImagePath = "";
        public string UserSid = "";
        public string Error = "";
        public bool Valid { get { return Error.Length == 0; } }
    }

    internal static class PipeProtocol
    {
        public const int Version = 1;
        public const int MaxMessage = 64 * 1024;
        public const string Name = "ShieldGate.Engine.v1";

        private static readonly string[] TopKeys = { "v", "id", "cmd", "args" };

        public static bool IsKnownCommand(string cmd)
        {
            return PipeCommands.IsKnown(cmd);
        }

        public static PipeRequest Parse(string text)
        {
            PipeRequest req = new PipeRequest();
            if (text == null)
            {
                req.Error = "bad-json";
                req.ErrorMessage = "empty message";
                return req;
            }

            Dictionary<string, object> root;
            try { root = Json.AsObject(Json.Parse(text)); }
            catch (Exception e)
            {
                req.Error = "bad-json";
                req.ErrorMessage = e.Message;
                return req;
            }
            if (root == null)
            {
                req.Error = "bad-json";
                req.ErrorMessage = "message is not an object";
                return req;
            }

            // Chỉ nhận đúng bốn khóa; khóa lạ là lỗi, không bỏ qua im lặng.
            foreach (string key in root.Keys)
            {
                bool known = false;
                for (int i = 0; i < TopKeys.Length; i++)
                    if (string.Equals(key, TopKeys[i], StringComparison.OrdinalIgnoreCase)) { known = true; break; }
                if (!known)
                {
                    req.Error = "bad-args";
                    req.ErrorMessage = "unknown key: " + key;
                    return req;
                }
            }

            req.Version = Json.Int(root, "v", -1);
            req.Id = Json.Str(root, "id", "");
            req.Cmd = Json.Str(root, "cmd", "").Trim();
            object args = Json.Get(root, "args");
            if (args != null)
            {
                req.Args = Json.AsObject(args);
                if (req.Args == null)
                {
                    req.Error = "bad-args";
                    req.ErrorMessage = "args must be an object";
                    return req;
                }
            }

            if (req.Version != Version)
            {
                req.Error = "bad-version";
                req.ErrorMessage = "expected " + Version.ToString();
                return req;
            }
            if (!ValidId(req.Id))
            {
                req.Error = "bad-args";
                req.ErrorMessage = "id must be 1-32 letters, digits, '-' or '_'";
                return req;
            }
            if (req.Cmd.Length == 0)
            {
                req.Error = "bad-args";
                req.ErrorMessage = "cmd is required";
                return req;
            }
            if (!PipeCommands.IsKnown(req.Cmd))
            {
                req.Error = "unknown-command";
                req.ErrorMessage = req.Cmd;
                return req;
            }
            return req;
        }

        private static bool ValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 32) return false;
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                       || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!ok) return false;
            }
            return true;
        }

        // Trả false khi client đóng (error rỗng) hoặc khung hỏng (error có mã).
        public static bool TryRead(Stream stream, out string text, out string error)
        {
            text = null;
            error = null;
            byte[] header = new byte[4];
            int got = ReadExact(stream, header, 4);
            if (got == 0) return false;
            if (got != 4)
            {
                error = "bad-frame";
                return false;
            }
            int length = BitConverter.ToInt32(header, 0);
            if (length <= 0 || length > MaxMessage)
            {
                error = "too-large";
                return false;
            }
            byte[] body = new byte[length];
            if (ReadExact(stream, body, length) != length)
            {
                error = "bad-frame";
                return false;
            }
            text = Encoding.UTF8.GetString(body);
            return true;
        }

        public static void Write(Stream stream, string text)
        {
            byte[] body = Encoding.UTF8.GetBytes(text ?? "");
            if (body.Length > MaxMessage)
                throw new InvalidOperationException("pipe message is too large");
            byte[] header = BitConverter.GetBytes(body.Length);
            stream.Write(header, 0, header.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        private static int ReadExact(Stream stream, byte[] buffer, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = stream.Read(buffer, total, count - total);
                if (n <= 0) break;
                total += n;
            }
            return total;
        }

        public static string Response(string id, Dictionary<string, object> result)
        {
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["v"] = Version;
            root["id"] = id ?? "";
            root["ok"] = true;
            root["result"] = result ?? new Dictionary<string, object>();
            return Json.Serialize(root, false);
        }

        public static string Error(string id, string code, string message)
        {
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["v"] = Version;
            root["id"] = id ?? "";
            root["ok"] = false;
            root["error"] = code ?? "failed";
            root["message"] = message ?? "";
            return Json.Serialize(root, false);
        }
    }
}
