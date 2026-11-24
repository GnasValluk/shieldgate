// ShieldGate - theo doi shell moi trong phien nguoi dung cho BadUSB.
// Poll 1 giay bang WMI (chi ten/pid/thoi diem) va Toolhelp (ten cha), khong
// can quyen quan tri, khong doc CommandLine. WMI loi thi bo qua vong do.
using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;

namespace ShieldGate
{
    internal static class ProcessWatch
    {
        private const int MaxShells = 20;
        private const uint Th32csSnapProcess = 0x00000002;
        private static readonly IntPtr InvalidHandle = new IntPtr(-1);

        private static readonly object Gate = new object();
        private static readonly List<ShellStart> _shells = new List<ShellStart>();
        private static readonly HashSet<int> _seen = new HashSet<int>();
        private static bool _primed;
        private static bool _running;
        private static Thread _thread;

        public static void Start()
        {
            lock (Gate)
            {
                if (_running) return;
                _running = true;
                _primed = false;
                _seen.Clear();
                _shells.Clear();
            }
            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Name = "ShieldGateProcessWatch";
            _thread.Start();
        }

        public static void Stop()
        {
            lock (Gate) _running = false;
        }

        public static void Reset()
        {
            lock (Gate)
            {
                _primed = false;
                _seen.Clear();
                _shells.Clear();
            }
        }

        // Tra ve cac shell da thay sau moc since. Ban sao de ben goi khong giu khoa.
        public static List<ShellStart> ShellsSince(DateTime since)
        {
            List<ShellStart> list = new List<ShellStart>();
            lock (Gate)
            {
                foreach (ShellStart s in _shells)
                    if (s.Created >= since) list.Add(s);
            }
            return list;
        }

        private static void Loop()
        {
            while (true)
            {
                lock (Gate) { if (!_running) return; }
                try { PollOnce(); }
                catch { }
                Thread.Sleep(1000);
            }
        }

        private static void PollOnce()
        {
            List<ShellStart> found = QueryShells();
            Dictionary<int, ProcNode> snapshot = Snapshot();
            DateTime now = DateTime.UtcNow;

            lock (Gate)
            {
                if (!_running) return;
                HashSet<int> current = new HashSet<int>();
                foreach (ShellStart s in found) current.Add(s.Pid);

                // Lan poll dau chi ghi nho shell dang co, khong bao shell cu la
                // "moi" khi tray vua khoi dong.
                if (!_primed)
                {
                    _primed = true;
                    foreach (int pid in current) _seen.Add(pid);
                    return;
                }

                foreach (ShellStart s in found)
                {
                    if (_seen.Contains(s.Pid)) continue;
                    ProcNode node;
                    int parent = 0;
                    if (snapshot.TryGetValue(s.Pid, out node)) parent = node.Parent;
                    s.ParentName = ParentName(parent, snapshot, out s.ExplorerAncestor);
                    _shells.Add(s);
                }

                _seen.Clear();
                foreach (int pid in current) _seen.Add(pid);

                DateTime cut = now.AddSeconds(-120);
                for (int i = _shells.Count - 1; i >= 0; i--)
                    if (_shells[i].Created < cut) _shells.RemoveAt(i);
                while (_shells.Count > MaxShells) _shells.RemoveAt(0);
            }
        }

        // Ten cha truc tiep; ExplorerAncestor = co explorer.exe trong 3 cap.
        private static string ParentName(int parentPid, Dictionary<int, ProcNode> snapshot,
                                         out bool explorer)
        {
            explorer = false;
            string direct = "";
            int pid = parentPid;
            for (int depth = 0; depth < 3 && pid > 0; depth++)
            {
                ProcNode node;
                if (!snapshot.TryGetValue(pid, out node)) break;
                if (depth == 0) direct = node.Name;
                if (string.Equals(node.Name, "explorer.exe", StringComparison.OrdinalIgnoreCase))
                {
                    explorer = true;
                    break;
                }
                pid = node.Parent;
            }
            return direct;
        }

        private static List<ShellStart> QueryShells()
        {
            List<ShellStart> list = new List<ShellStart>();
            ManagementObjectCollection col = null;
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect();
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope,
                    new ObjectQuery("SELECT ProcessId, ParentProcessId, Name, CreationDate " +
                        "FROM Win32_Process WHERE Name='cmd.exe' OR Name='powershell.exe' " +
                        "OR Name='pwsh.exe' OR Name='wscript.exe' OR Name='cscript.exe' " +
                        "OR Name='mshta.exe'"));
                searcher.Options.Timeout = TimeSpan.FromSeconds(5);
                col = searcher.Get();
                foreach (ManagementBaseObject o in col)
                {
                    ManagementObject mo = o as ManagementObject;
                    if (mo == null) continue;
                    try
                    {
                        ShellStart s = new ShellStart();
                        s.Pid = Convert.ToInt32(mo["ProcessId"]);
                        s.Name = Convert.ToString(mo["Name"]) ?? "";
                        try
                        {
                            // WMI tra gio dia phuong; doi ve UTC de cung moc voi
                            // burst (InputCollector dung DateTime.UtcNow).
                            s.Created = ManagementDateTimeConverter.ToDateTime(
                                Convert.ToString(mo["CreationDate"])).ToUniversalTime();
                        }
                        catch { s.Created = DateTime.UtcNow; }
                        if (s.Pid > 0 && s.Name.Length > 0) list.Add(s);
                    }
                    catch { }
                    finally { try { mo.Dispose(); } catch { } }
                }
            }
            catch { }
            finally { try { if (col != null) col.Dispose(); } catch { } }
            return list;
        }

        private static Dictionary<int, ProcNode> Snapshot()
        {
            Dictionary<int, ProcNode> map = new Dictionary<int, ProcNode>();
            IntPtr handle = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
            if (handle == InvalidHandle) return map;
            try
            {
                ProcessEntry32 entry = new ProcessEntry32();
                entry.Size = (uint)Marshal.SizeOf(typeof(ProcessEntry32));
                if (!Process32FirstW(handle, ref entry)) return map;
                do
                {
                    ProcNode node = new ProcNode();
                    node.Pid = (int)entry.ProcessId;
                    node.Parent = (int)entry.ParentProcessId;
                    node.Name = entry.ExeFile ?? "";
                    if (node.Pid > 0) map[node.Pid] = node;
                }
                while (Process32NextW(handle, ref entry));
            }
            catch { }
            finally { try { CloseHandle(handle); } catch { } }
            return map;
        }

        private struct ProcNode
        {
            public int Pid;
            public int Parent;
            public string Name;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry32
        {
            public uint Size;
            public uint Usage;
            public uint ProcessId;
            public IntPtr DefaultHeapId;
            public uint ModuleId;
            public uint Threads;
            public uint ParentProcessId;
            public int PriClassBase;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
