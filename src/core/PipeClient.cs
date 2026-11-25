// ShieldGate 3.0 - client named pipe dùng bởi GUI và self-test.
// Một phiên giữ kết nối cho plan/apply và job polling; Send() một lần tự đóng.
using System;
using System.Collections.Generic;
using System.IO.Pipes;

namespace ShieldGate
{
    internal sealed class PipeSession : IDisposable
    {
        private readonly string _name;
        private NamedPipeClientStream _pipe;

        public PipeSession(string name)
        {
            _name = string.IsNullOrEmpty(name) ? PipeProtocol.Name : name;
        }

        public bool Connect(int timeoutMs, out string error)
        {
            error = "";
            try
            {
                if (_pipe != null) return true;
                _pipe = new NamedPipeClientStream(".", _name, PipeDirection.InOut, PipeOptions.None);
                _pipe.Connect(timeoutMs);
                _pipe.ReadMode = PipeTransmissionMode.Message;
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                Dispose();
                return false;
            }
        }

        // Trả null khi lỗi; error nói lý do ngắn. Kết nối vẫn dùng được cho lệnh sau
        // nếu lỗi ở mức giao thức, nhưng bị đóng khi khung hỏng.
        public string Send(string cmd, Dictionary<string, object> args, int timeoutMs,
                           out string error)
        {
            error = "";
            if (_pipe == null)
            {
                error = "not-connected";
                return null;
            }
            try
            {
                string id = Guid.NewGuid().ToString("N").Substring(0, 12);
                Dictionary<string, object> root = new Dictionary<string, object>();
                root["v"] = PipeProtocol.Version;
                root["id"] = id;
                root["cmd"] = cmd;
                root["args"] = args ?? new Dictionary<string, object>();
                PipeProtocol.Write(_pipe, Json.Serialize(root, false));

                string text, frameError;
                if (!PipeProtocol.TryRead(_pipe, out text, out frameError))
                {
                    error = frameError == null ? "closed" : frameError;
                    Dispose();
                    return null;
                }
                return text;
            }
            catch (Exception e)
            {
                error = e.Message;
                Dispose();
                return null;
            }
        }

        public void Dispose()
        {
            if (_pipe == null) return;
            try { _pipe.Dispose(); } catch { }
            _pipe = null;
        }
    }

    internal static class PipeClient
    {
        public static string Send(string pipeName, string cmd,
                                  Dictionary<string, object> args, int timeoutMs,
                                  out string error)
        {
            using (PipeSession session = new PipeSession(pipeName))
            {
                if (!session.Connect(timeoutMs, out error)) return null;
                return session.Send(cmd, args, timeoutMs, out error);
            }
        }

        public static bool IsOk(string response)
        {
            if (string.IsNullOrEmpty(response)) return false;
            try
            {
                Dictionary<string, object> root = Json.AsObject(Json.Parse(response));
                return root != null && Json.Bool(root, "ok", false);
            }
            catch { return false; }
        }
    }
}
