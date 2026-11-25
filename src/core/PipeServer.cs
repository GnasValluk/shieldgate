// ShieldGate 3.0 - named pipe server. Một client tại một thời điểm, ACL chỉ
// SYSTEM + Administrators + Interactive; client phải qua PipeAuth.
using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace ShieldGate
{
    internal sealed class PipeServer
    {
        private readonly string _name;
        private readonly Func<PipeClientInfo, bool> _validator;
        private readonly object _gate = new object();
        private Thread _thread;
        private NamedPipeServerStream _listener;
        private volatile bool _stopping;

        public PipeServer(string name, Func<PipeClientInfo, bool> validator)
        {
            _name = string.IsNullOrEmpty(name) ? PipeProtocol.Name : name;
            _validator = validator;
        }

        public void Start()
        {
            if (_thread != null) return;
            _listener = CreatePipe();
            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Name = "ShieldGate pipe";
            _thread.Start();
        }

        public void Stop()
        {
            _stopping = true;
            NamedPipeServerStream listener;
            lock (_gate)
            {
                listener = _listener;
                _listener = null;
            }
            if (listener != null)
            {
                try { listener.Dispose(); } catch { }
            }
            if (_thread != null)
            {
                try { _thread.Join(2000); } catch { }
                _thread = null;
            }
        }

        internal static PipeSecurity BuildSecurity()
        {
            PipeSecurity security = new PipeSecurity();
            SecurityIdentifier system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            SecurityIdentifier admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            SecurityIdentifier interactive = new SecurityIdentifier(WellKnownSidType.InteractiveSid, null);
            SecurityIdentifier network = new SecurityIdentifier(WellKnownSidType.NetworkSid, null);
            SecurityIdentifier anonymous = new SecurityIdentifier(WellKnownSidType.AnonymousSid, null);

            security.AddAccessRule(new PipeAccessRule(system, PipeAccessRights.FullControl,
                                                      AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(admins, PipeAccessRights.FullControl,
                                                      AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(interactive, PipeAccessRights.ReadWrite,
                                                      AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(network, PipeAccessRights.FullControl,
                                                      AccessControlType.Deny));
            security.AddAccessRule(new PipeAccessRule(anonymous, PipeAccessRights.FullControl,
                                                      AccessControlType.Deny));
            return security;
        }

        private NamedPipeServerStream CreatePipe()
        {
            // PIPE_REJECT_REMOTE_CLIENTS không có trong PipeOptions của .NET 4.0.
            // ACL ở trên đã chặn Network; server còn kiểm tra tên máy client.
            return new NamedPipeServerStream(_name, PipeDirection.InOut, 1,
                PipeTransmissionMode.Message, PipeOptions.WriteThrough,
                PipeProtocol.MaxMessage, PipeProtocol.MaxMessage, BuildSecurity(),
                HandleInheritability.None);
        }

        private void Loop()
        {
            while (!_stopping)
            {
                NamedPipeServerStream listener;
                lock (_gate) listener = _listener;
                if (listener == null) break;
                try
                {
                    listener.WaitForConnection();
                    Handle(listener);
                }
                catch (Exception e)
                {
                    if (!_stopping)
                    {
                        Log.Write(S.Log.PipeError(e.Message), "WARN");
                        Thread.Sleep(200);
                    }
                }
                finally
                {
                    try { listener.Dispose(); } catch { }
                    lock (_gate)
                    {
                        if (!_stopping)
                        {
                            try { _listener = CreatePipe(); }
                            catch (Exception e)
                            {
                                Log.Write(S.Log.PipeError(e.Message), "ERROR");
                                _listener = null;
                            }
                        }
                    }
                }
            }
        }

        private void Handle(NamedPipeServerStream stream)
        {
            PipeClientInfo info;
            if (!PipeAuth.TryIdentify(stream.SafePipeHandle.DangerousGetHandle(), out info))
            {
                Log.Write(S.Log.PipeClientDenied(info.Error), "WARN");
                return;
            }

            bool ok = _validator == null ? PipeAuth.IsTrusted(info) : _validator(info);
            if (!ok)
            {
                Log.Write(S.Log.PipeClientDenied("not pinned"), "WARN");
                return;
            }

            string image = info.ImagePath;
            try { image = System.IO.Path.GetFileName(image); } catch { }
            Log.Write(S.Log.PipeClientAccepted(info.ProcessId, info.SessionId, image), "INFO");

            PipeCommands.ClientConnected(info);
            try
            {
                while (!_stopping)
                {
                    string text, error;
                    if (!PipeProtocol.TryRead(stream, out text, out error))
                    {
                        if (error != null)
                        {
                            try
                            {
                                PipeProtocol.Write(stream, PipeProtocol.Error("", error, "bad frame"));
                            }
                            catch { }
                        }
                        break;
                    }

                    PipeRequest req = PipeProtocol.Parse(text);
                    string response = req.Error.Length > 0
                        ? PipeProtocol.Error(req.Id, req.Error, req.ErrorMessage)
                        : PipeCommands.Handle(req, info);
                    PipeProtocol.Write(stream, response);
                }
            }
            finally
            {
                PipeCommands.ClientDisconnected();
            }
        }
    }
}
