// ShieldGate - khoa dung chung giua GUI va tien trinh bao ve nen.
// Log thuc dia 03/11/2026: hai tien trinh cung quet va cach ly mot thu muc gia,
// ben nay di chuyen tep con ben kia bao "khong tim thay tep" hang tram lan,
// bo dem ra so am va moi lan chay lai tao them mot vong cach ly moi.
// Dung tep khoa trong thu muc du lieu (moi tien trinh deu dung chung Paths.Root)
// thay cho named pipe: he dieu hanh tu nha khoa khi tien trinh bi ket thuc.
using System;
using System.IO;
using System.Threading;

namespace ShieldGate
{
    internal static class CleanLock
    {
        // Apply goi QuarantineTree trong cung luong nen khoa phai vao lai duoc.
        [ThreadStatic] private static int _depth;
        [ThreadStatic] private static FileStream _held;

        public static string FilePath
        {
            get { return Path.Combine(Paths.Root, "clean.lock"); }
        }

        // Tra ve false khi het thoi gian cho: ben goi phai bo qua vong nay,
        // khong duoc chay tiep roi bao loi "khong tim thay tep".
        public static bool Enter(int timeoutMs)
        {
            if (_depth > 0) { _depth++; return true; }
            DateTime until = DateTime.UtcNow.AddMilliseconds(timeoutMs < 0 ? 0 : timeoutMs);
            while (true)
            {
                try
                {
                    _held = new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                                           FileShare.None, 1);
                    _depth = 1;
                    return true;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch
                {
                    // Khong tao duoc tep khoa (o chi doc, quyen la): van chay,
                    // chi mat tinh nang tuan tu hoa chu khong lam chet ung dung.
                    _held = null;
                    _depth = 1;
                    return true;
                }
                if (DateTime.UtcNow >= until) return false;
                Thread.Sleep(120);
            }
        }

        public static void Exit()
        {
            if (_depth <= 0) return;
            _depth--;
            if (_depth > 0) return;
            try { if (_held != null) _held.Dispose(); }
            catch { }
            _held = null;
        }

        // Kiem tra nhanh truoc khi quet de khong quet lai ca o chi de roi bo qua.
        public static bool IsBusy()
        {
            if (_depth > 0) return false;
            try
            {
                using (FileStream fs = new FileStream(FilePath, FileMode.OpenOrCreate,
                                                      FileAccess.ReadWrite, FileShare.None, 1)) { }
                return false;
            }
            catch (IOException) { return true; }
            catch { return false; }
        }
    }
}
