// ShieldGate - điểm vào của bộ tự kiểm tra chạy trên dòng lệnh.
// Không gắn manifest quản trị: các phép kiểm tra lõi chạy được cả khi không nâng quyền.
using System;

namespace ShieldGate
{
    internal static class TestMain
    {
        private static int Main()
        {
            return SelfTest.Run();
        }
    }
}
