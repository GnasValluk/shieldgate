// ShieldGate - đếm số lần cùng một ổ bị nhiễm lại trong thời gian ngắn.
// Virus đang chạy trên máy sẽ ghi lại shortcut/payload sau mỗi lần dọn; nhật ký
// máy bệnh 05/11 có 50 lần trong 27 phút mà không có cảnh báo nào. Phát hiện
// vòng lặp để báo người dùng quét máy, thay vì lặng lẽ dọn tiếp.
using System;
using System.Collections.Generic;

namespace ShieldGate
{
    internal static class ReinfectWatch
    {
        private sealed class Hits
        {
            public readonly List<DateTime> When = new List<DateTime>();
            public DateTime LastAlert = DateTime.MinValue;
        }

        private static readonly Dictionary<string, Hits> Map =
            new Dictionary<string, Hits>(StringComparer.OrdinalIgnoreCase);
        private static readonly object Gate = new object();

        public const int WindowMinutes = 10;
        public const int Threshold = 3;

        // Trả true khi ổ vừa chạm ngưỡng nhiễm lại và chưa báo trong 10 phút.
        // Gọi sau mỗi lượt cách ly có di chuyển tệp (cleaned > 0).
        public static bool Observe(string volume, int cleaned, DateTime now)
        {
            if (string.IsNullOrEmpty(volume) || cleaned <= 0) return false;
            lock (Gate)
            {
                Hits h;
                if (!Map.TryGetValue(volume, out h))
                {
                    h = new Hits();
                    Map[volume] = h;
                }
                h.When.Add(now);
                DateTime cutoff = now.AddMinutes(-WindowMinutes);
                h.When.RemoveAll(delegate(DateTime t) { return t < cutoff; });
                if (h.When.Count < Threshold) return false;
                if (h.LastAlert != DateTime.MinValue
                    && (now - h.LastAlert).TotalMinutes < WindowMinutes)
                    return false;
                h.When.Clear();
                h.LastAlert = now;
                return true;
            }
        }

        // Chỉ dùng cho self-test: xoá trạng thái giữa các lần chạy.
        internal static void Reset()
        {
            lock (Gate) Map.Clear();
        }
    }
}
