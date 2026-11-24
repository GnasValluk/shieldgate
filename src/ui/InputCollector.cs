// ShieldGate - nhan Raw Input trong tien trinh tray de dem toc do go.
// Chi doc phan header cua goi tin (handle + loai thiet bi); khong doc, khong
// luu ma phim. Khong dung hook ban phim. W21 chi do, chua canh bao.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShieldGate
{
    internal sealed class InputCollector : IDisposable
    {
        private const int WmInput = 0x00FF;
        private const int WmInputDeviceChange = 0x00FE;
        private const int RidInput = 0x10000003;
        private const int RidiDeviceName = 0x20000007;
        private const int RimTypeKeyboard = 1;
        private const int RidevRemove = 0x00000001;
        private const int RidevInputSink = 0x00000100;
        private const int RidevDevNotify = 0x00002000;
        private const int GidcRemoval = 2;
        // Mot dong log cho moi thiet bi moi 10 phut; chi de do tren may that.
        private const int CooldownSeconds = 600;

        private readonly Dictionary<string, DeviceState> _states =
            new Dictionary<string, DeviceState>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<IntPtr, string> _names =
            new Dictionary<IntPtr, string>();

        private RawWindow _window;
        private bool _started;

        public event Action<InputBurst> Burst;

        public bool Start()
        {
            if (_started) return true;
            try
            {
                _window = new RawWindow(this);
                if (!_window.Register())
                {
                    _window.Dispose();
                    _window = null;
                    return false;
                }
                _started = true;
                return true;
            }
            catch
            {
                if (_window != null)
                {
                    try { _window.Dispose(); } catch { }
                    _window = null;
                }
                return false;
            }
        }

        public void Stop()
        {
            _started = false;
            if (_window != null)
            {
                try { _window.Dispose(); } catch { }
                _window = null;
            }
            _states.Clear();
            _names.Clear();
        }

        public void Dispose()
        {
            Stop();
        }

        private void OnRawInput(IntPtr lParam)
        {
            uint header = (uint)Marshal.SizeOf(typeof(RawInputHeader));
            uint size = 0;
            GetRawInputData(lParam, RidInput, IntPtr.Zero, ref size, header);
            if (size == 0 || size > 4096) return;

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(lParam, RidInput, buffer, ref size, header) != size)
                    return;
                RawInputHeader info = (RawInputHeader)Marshal.PtrToStructure(
                    buffer, typeof(RawInputHeader));
                // Chi quan tam ban phim vat ly; hDevice = 0 la input tong hop
                // (RDP, automation, ban phim tren man hinh).
                if (info.Type != RimTypeKeyboard || info.Device == IntPtr.Zero) return;
                OnKey(info.Device);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private void OnKey(IntPtr device)
        {
            DateTime now = DateTime.UtcNow;
            string raw;
            if (!_names.TryGetValue(device, out raw))
            {
                raw = DeviceName(device);
                _names[device] = raw;
            }

            string key = raw.Length > 0 ? raw : "h:" + device.ToString();
            DeviceState state;
            if (!_states.TryGetValue(key, out state))
            {
                state = new DeviceState();
                state.Rate = new InputRate(Label(raw));
                _states[key] = state;
            }

            state.Rate.Add(now);
            if (!state.Rate.Crossed) return;
            if (state.LastBurst != DateTime.MinValue &&
                (now - state.LastBurst).TotalSeconds < CooldownSeconds)
            {
                // Bỏ burst trong thời gian chờ để bộ đếm không phình to.
                state.Rate.Reset();
                return;
            }

            state.LastBurst = now;
            InputBurst burst = state.Rate.Snapshot();
            burst.Raw = raw;
            state.Rate.Reset();
            try
            {
                if (Burst != null) Burst(burst);
            }
            catch { }
        }

        private void OnDeviceChange(IntPtr wParam, IntPtr lParam)
        {
            if (wParam.ToInt64() != GidcRemoval) return;
            string raw;
            if (!_names.TryGetValue(lParam, out raw)) return;
            _names.Remove(lParam);
            if (raw.Length > 0) _states.Remove(raw);
        }

        private static string DeviceName(IntPtr device)
        {
            uint size = 0;
            GetRawInputDeviceInfo(device, RidiDeviceName, IntPtr.Zero, ref size);
            if (size == 0 || size > 4096) return "";

            IntPtr buffer = Marshal.AllocHGlobal((int)size * 2);
            try
            {
                uint got = GetRawInputDeviceInfo(device, RidiDeviceName, buffer, ref size);
                if (got == 0 || got == uint.MaxValue) return "";
                return Marshal.PtrToStringUni(buffer) ?? "";
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        // Chi giu VID/PID de ghi log; duong dan raw co the chua serial.
        private static string Label(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "HID";
            int v = raw.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);
            int p = raw.IndexOf("PID_", StringComparison.OrdinalIgnoreCase);
            if (v >= 0 && raw.Length >= v + 8)
            {
                string vid = raw.Substring(v + 4, 4);
                if (p >= 0 && raw.Length >= p + 8)
                    return "VID_" + vid + "&PID_" + raw.Substring(p + 4, 4);
                return "VID_" + vid;
            }
            return "HID";
        }

        private sealed class DeviceState
        {
            public InputRate Rate;
            public DateTime LastBurst = DateTime.MinValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputDevice
        {
            public ushort UsagePage;
            public ushort Usage;
            public int Flags;
            public IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputHeader
        {
            public int Type;
            public int Size;
            public IntPtr Device;
            public IntPtr WParam;
        }

        private sealed class RawWindow : NativeWindow
        {
            private readonly InputCollector _owner;
            private bool _registered;

            public RawWindow(InputCollector owner)
            {
                _owner = owner;
                CreateParams cp = new CreateParams();
                cp.Caption = "ShieldGateRawInput";
                cp.ClassName = "STATIC";
                cp.Parent = (IntPtr)(-3); // HWND_MESSAGE
                cp.Style = 0;
                CreateHandle(cp);
            }

            public bool Register()
            {
                RawInputDevice[] devices = new RawInputDevice[1];
                devices[0].UsagePage = 0x01;
                devices[0].Usage = 0x06; // keyboard
                devices[0].Flags = RidevInputSink | RidevDevNotify;
                devices[0].Target = Handle;
                _registered = RegisterRawInputDevices(devices, 1,
                    (uint)Marshal.SizeOf(typeof(RawInputDevice)));
                return _registered;
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WmInput)
                {
                    _owner.OnRawInput(m.LParam);
                    base.WndProc(ref m);
                    return;
                }
                if (m.Msg == WmInputDeviceChange)
                {
                    _owner.OnDeviceChange(m.WParam, m.LParam);
                    base.WndProc(ref m);
                    return;
                }
                base.WndProc(ref m);
            }

            public void Dispose()
            {
                if (_registered)
                {
                    RawInputDevice[] devices = new RawInputDevice[1];
                    devices[0].UsagePage = 0x01;
                    devices[0].Usage = 0x06;
                    devices[0].Flags = RidevRemove;
                    devices[0].Target = IntPtr.Zero;
                    try
                    {
                        RegisterRawInputDevices(devices, 1,
                            (uint)Marshal.SizeOf(typeof(RawInputDevice)));
                    }
                    catch { }
                    _registered = false;
                }
                if (Handle != IntPtr.Zero) DestroyHandle();
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(
            RawInputDevice[] devices, uint count, uint size);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(
            IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetRawInputDeviceInfo(
            IntPtr device, uint command, IntPtr data, ref uint size);
    }
}
