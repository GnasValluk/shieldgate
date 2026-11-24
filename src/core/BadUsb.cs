// ShieldGate - quyet dinh co canh bao BadUSB hay khong. Chi logic, khong
// P/Invoke: nhan burst da tong hop tu InputCollector va shell da tim thay.
// Day la heuristic, khong phai ket luan; khong bao gio tu tat thiet bi.
using System;
using System.Collections.Generic;

namespace ShieldGate
{
    internal sealed class ShellStart
    {
        public int Pid;
        public string Name = "";
        public string ParentName = "";
        public DateTime Created;
        // Co mot to tien la explorer.exe trong toi da 3 cap (ProcessWatch dien).
        public bool ExplorerAncestor;
    }

    internal sealed class BadUsbVerdict
    {
        public bool Alert;
        public string Reason = "";
        public int Score;
    }

    internal static class BadUsb
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, DateTime> _lastAlert =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        public static bool Enabled
        {
            get { return Settings.BadUsbEnabled; }
        }

        // Burst chi duoc danh gia khi da co du ca ba manh: thiet bi moi/chua tin
        // cay, toc do go bat thuong, va shell moi do explorer mo.
        public static BadUsbVerdict Evaluate(InputBurst burst, DeviceInfo device,
                                             ShellStart shell, DateTime now)
        {
            BadUsbVerdict verdict = new BadUsbVerdict();
            if (!Enabled) return verdict;
            if (burst == null || device == null) return verdict;
            if (device.Trust == DeviceTrust.Trusted) return verdict;

            RulesData rules = Rules.Current;
            if (!IsNew(device, now, rules.BadUsbNewDeviceSeconds)) return verdict;
            if (burst.Events < rules.BadUsbMinEvents) return verdict;
            if (burst.PeakPerSecond < rules.BadUsbEventsPerSecond) return verdict;
            if (shell == null) return verdict;
            // Shell phai roi vao cua so cua burst: khong tinh shell mo truoc do
            // qua lau hay sau khi da het thoi gian cho.
            if (shell.Created < burst.First.AddSeconds(-1)) return verdict;
            if (shell.Created > burst.Last.AddSeconds(rules.BadUsbShellSeconds)) return verdict;
            if (rules.BadUsbRequireExplorerParent && !shell.ExplorerAncestor) return verdict;

            lock (Gate)
            {
                DateTime last;
                if (_lastAlert.TryGetValue(device.Key, out last) &&
                    (now - last).TotalSeconds < rules.BadUsbAlertSeconds)
                    return verdict;
                _lastAlert[device.Key] = now;
            }

            verdict.Alert = true;
            verdict.Reason = "rate+new+shell+explorer";
            verdict.Score = 8;
            return verdict;
        }

        // Nguoi dung bam "Tin cay thiet bi": lan cam sau khong canh bao lai.
        public static void MarkTrusted(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (Gate) _lastAlert.Remove(key);
        }

        // Chi dung cho self-test: xoa lich su cooldown giua cac ca.
        public static void Reset()
        {
            lock (Gate) _lastAlert.Clear();
        }

        private static bool IsNew(DeviceInfo device, DateTime now, int seconds)
        {
            if (device.FirstSeen == DateTime.MinValue) return false;
            // FirstSeen trong devices.json la gio dia phuong; burst/shell la UTC.
            DateTime seen = device.FirstSeen;
            if (seen.Kind == DateTimeKind.Unspecified)
                seen = DateTime.SpecifyKind(seen, DateTimeKind.Local);
            seen = seen.ToUniversalTime();
            double age = (now - seen).TotalSeconds;
            // Cho phep lech dong ho nho; thiet bi la trong tuong lai xa thi bo qua.
            return age >= -5 && age <= seconds;
        }
    }
}
