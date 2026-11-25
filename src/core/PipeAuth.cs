// ShieldGate 3.0 - xác thực client của named pipe.
// Chỉ tiến trình có ảnh đã ghim (đường dẫn + SHA-256) mới được gửi lệnh.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ShieldGate
{
    internal static class PipeAuth
    {
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const uint TokenQuery = 0x0008;
        private const int TokenUser = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct SidAndAttributes
        {
            public IntPtr Sid;
            public uint Attributes;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNamedPipeClientProcessId(IntPtr pipe, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNamedPipeClientSessionId(IntPtr pipe, out uint sessionId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool GetNamedPipeClientComputerNameW(IntPtr pipe,
                                                                   StringBuilder name, uint length);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags,
                                                              StringBuilder name, ref int size);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info,
                                                       int length, out int needed);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ConvertSidToStringSidW(IntPtr sid, out IntPtr text);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        // Trả false nếu không lấy được đủ thông tin; info.Error nói lý do ngắn.
        public static bool TryIdentify(IntPtr pipeHandle, out PipeClientInfo info)
        {
            info = new PipeClientInfo();
            if (pipeHandle == IntPtr.Zero)
            {
                info.Error = "no-pipe-handle";
                return false;
            }

            uint pid, session;
            if (!GetNamedPipeClientProcessId(pipeHandle, out pid) || pid == 0)
            {
                info.Error = "no-client-pid";
                return false;
            }
            if (!GetNamedPipeClientSessionId(pipeHandle, out session))
            {
                info.Error = "no-client-session";
                return false;
            }
            info.ProcessId = pid;
            info.SessionId = session;
            // Session 0 là dịch vụ; GUI phải chạy trong phiên người dùng.
            if (session == 0)
            {
                info.Error = "service-session";
                return false;
            }

            // .NET 4.0 không đặt được PIPE_REJECT_REMOTE_CLIENTS; kiểm tra tên
            // máy client để chặn pipe qua mạng.
            StringBuilder computer = new StringBuilder(256);
            if (GetNamedPipeClientComputerNameW(pipeHandle, computer, (uint)(computer.Capacity * 2))
                && computer.Length > 0)
            {
                info.Error = "remote-client";
                return false;
            }

            IntPtr process = OpenProcess(ProcessQueryLimitedInformation, false, pid);
            if (process == IntPtr.Zero)
            {
                info.Error = "open-process";
                return false;
            }
            try
            {
                StringBuilder name = new StringBuilder(1024);
                int size = name.Capacity;
                if (!QueryFullProcessImageNameW(process, 0, name, ref size) || size <= 0)
                {
                    info.Error = "image-path";
                    return false;
                }
                try { info.ImagePath = Path.GetFullPath(name.ToString()); }
                catch { info.Error = "image-path"; return false; }

                string sid;
                if (!TryUserSid(process, out sid))
                {
                    info.Error = "token-user";
                    return false;
                }
                info.UserSid = sid;
                if (IsServiceSid(sid))
                {
                    info.Error = "service-sid";
                    return false;
                }
            }
            finally { CloseHandle(process); }
            return true;
        }

        private static bool TryUserSid(IntPtr process, out string sid)
        {
            sid = "";
            IntPtr token = IntPtr.Zero;
            IntPtr buffer = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(process, TokenQuery, out token) || token == IntPtr.Zero)
                    return false;
                int needed;
                GetTokenInformation(token, TokenUser, IntPtr.Zero, 0, out needed);
                if (needed <= 0 || needed > 4096) return false;
                buffer = Marshal.AllocHGlobal(needed);
                if (!GetTokenInformation(token, TokenUser, buffer, needed, out needed))
                    return false;
                SidAndAttributes user = (SidAndAttributes)Marshal.PtrToStructure(buffer, typeof(SidAndAttributes));
                if (user.Sid == IntPtr.Zero) return false;
                IntPtr text;
                if (!ConvertSidToStringSidW(user.Sid, out text) || text == IntPtr.Zero)
                    return false;
                try { sid = Marshal.PtrToStringUni(text); }
                finally { LocalFree(text); }
                return !string.IsNullOrEmpty(sid);
            }
            catch { return false; }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                if (token != IntPtr.Zero) CloseHandle(token);
            }
        }

        private static bool IsServiceSid(string sid)
        {
            // SYSTEM, LocalService, NetworkService, Service, Anonymous.
            return sid == "S-1-5-18" || sid == "S-1-5-19" || sid == "S-1-5-20"
                || sid == "S-1-5-6" || sid == "S-1-5-7";
        }

        // Đọc pin: dòng 1 đường dẫn đầy đủ, dòng 2 SHA-256.
        public static bool ReadPin(out string path, out string hash)
        {
            path = "";
            hash = "";
            try
            {
                if (!File.Exists(Paths.ClientPin)) return false;
                string[] lines = File.ReadAllLines(Paths.ClientPin, Encoding.UTF8);
                string p = "", h = "";
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim().TrimStart('\uFEFF');
                    if (line.Length == 0) continue;
                    if (p.Length == 0) { p = line; continue; }
                    h = line;
                    break;
                }
                if (p.Length == 0 || h.Length == 0) return false;
                path = p;
                hash = h;
                return true;
            }
            catch { return false; }
        }

        // So khớp đường dẫn + băm. Tách riêng để self-test kiểm tra mà không
        // cần tạo pipe thật.
        public static bool IsPinnedImage(string imagePath, string hash)
        {
            string wantPath, wantHash;
            if (!ReadPin(out wantPath, out wantHash)) return false;
            string full, fullWant, fullGuard;
            try
            {
                full = Path.GetFullPath(imagePath);
                fullWant = Path.GetFullPath(wantPath);
                fullGuard = Path.GetFullPath(Paths.GuardExe);
            }
            catch { return false; }
            // Chấp nhận đúng tệp đã ghim, hoặc bản đã cài trong bin nếu băm trùng.
            bool pathOk = string.Equals(full, fullWant, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(full, fullGuard, StringComparison.OrdinalIgnoreCase);
            if (!pathOk) return false;
            return string.Equals((hash ?? "").Trim(), wantHash.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTrusted(PipeClientInfo info)
        {
            if (info == null || !info.Valid || info.ImagePath.Length == 0) return false;
            string hash;
            try { hash = Native.Sha256(info.ImagePath); }
            catch { return false; }
            return IsPinnedImage(info.ImagePath, hash);
        }

        // Ghi pin sau khi --install đã đặt bản chạy nền. Ghi tạm rồi thay để
        // không để lại pin nửa vời nếu tiến trình bị kill.
        public static bool PinClient(string imagePath)
        {
            try
            {
                string full = Path.GetFullPath(imagePath);
                string hash = Native.Sha256(full);
                if (string.IsNullOrEmpty(hash)) return false;
                string temp = Paths.ClientPin + ".tmp";
                File.WriteAllText(temp, full + "\r\n" + hash + "\r\n", new UTF8Encoding(false));
                if (File.Exists(Paths.ClientPin))
                {
                    try { File.Replace(temp, Paths.ClientPin, null); }
                    catch
                    {
                        try { File.Delete(Paths.ClientPin); } catch { }
                        File.Move(temp, Paths.ClientPin);
                    }
                }
                else File.Move(temp, Paths.ClientPin);
                return true;
            }
            catch { return false; }
        }

        public static void ClearPin()
        {
            try { File.Delete(Paths.ClientPin); } catch { }
            try { File.Delete(Paths.ClientPin + ".tmp"); } catch { }
        }
    }
}
