// ShieldGate - hệ thống thiết kế: màu, chữ, khoảng cách.
// Mọi giao diện mới phải lấy giá trị từ đây, không viết số màu / cỡ chữ thẳng trong form.
using System;
using System.Drawing;

namespace ShieldGate
{
    internal static class Theme
    {
        private static Color Hex(int v)
        {
            return Color.FromArgb((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
        }

        // Nền và đường kẻ
        public static readonly Color Canvas = Hex(0xF7F8FA);
        public static readonly Color Surface = Hex(0xFFFFFF);
        public static readonly Color Line = Hex(0xE3E6EB);
        public static readonly Color LineSoft = Hex(0xEDEFF3);
        public static readonly Color BorderStrong = Hex(0xD6DBE2);

        // Chữ
        public static readonly Color Ink = Hex(0x111827);
        public static readonly Color InkMuted = Hex(0x5B6472);
        public static readonly Color InkFaint = Hex(0x9AA3AF);

        // Màu nhấn và trạng thái
        public static readonly Color Accent = Hex(0x1F5E3D);
        public static readonly Color AccentHover = Hex(0x184D31);
        public static readonly Color AccentSoft = Hex(0xECF3EF);
        public static readonly Color Safe = Hex(0x1F7A45);
        public static readonly Color SafeSoft = Hex(0xEDF5F0);
        public static readonly Color Warn = Hex(0xA15C00);
        public static readonly Color WarnSoft = Hex(0xFAF3E6);
        public static readonly Color Danger = Hex(0x9B1C1C);
        public static readonly Color DangerSoft = Hex(0xFBEDED);
        public static readonly Color Hover = Hex(0xEFEFEF);
        public static readonly Color HoverSoft = Hex(0xF4F5F7);
        public static readonly Color Pressed = Hex(0xE7EAEF);
        public static readonly Color Disabled = Hex(0xC3C9D2);

        // Chữ - thang 6 bậc
        private static Font Make(string family, float size, FontStyle style)
        {
            try { return new Font(family, size, style, GraphicsUnit.Point); }
            catch { return new Font("Segoe UI", size, style, GraphicsUnit.Point); }
        }

        public static readonly Font H1 = Make("Segoe UI Semibold", 17f, FontStyle.Regular);
        public static readonly Font Hero = Make("Segoe UI Semibold", 15f, FontStyle.Regular);
        public static readonly Font Brand = Make("Segoe UI Semibold", 12.5f, FontStyle.Regular);
        public static readonly Font Section = Make("Segoe UI Semibold", 10f, FontStyle.Regular);
        public static readonly Font Body = Make("Segoe UI", 9.5f, FontStyle.Regular);
        public static readonly Font BodyStrong = Make("Segoe UI Semibold", 9.5f, FontStyle.Regular);
        public static readonly Font Small = Make("Segoe UI", 8.25f, FontStyle.Regular);
        public static readonly Font SmallStrong = Make("Segoe UI Semibold", 8.25f, FontStyle.Regular);
        public static readonly Font Tiny = Make("Segoe UI", 7.5f, FontStyle.Regular);
        public static readonly Font Mono = Make("Consolas", 9f, FontStyle.Regular);

        // Khoảng cách: lưới 8 (có 4 cho chi tiết nhỏ)
        public const int Space1 = 4;
        public const int Space2 = 8;
        public const int Space3 = 12;
        public const int Space4 = 16;
        public const int Space5 = 24;
        public const int Space6 = 32;

        public const int RailWidth = 232;
        public const int PagePad = 32;
        public const int ControlHeight = 38;
        public const int NavHeight = 38;
        public const int RowHeight = 56;
    }
}
