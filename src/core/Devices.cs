// ShieldGate 2.0 - kiem ke thiet bi ngoai vi va trang thai tin cay.
// WMI chi de doc; moi loi deu duoc nuot de vong lap bao ve khong chet.
using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ShieldGate
{
    internal enum DeviceTrust { Unknown = 0, Trusted = 1, Blocked = 2 }

    internal sealed class DeviceInfo
    {
        public string Key = "";
        public string InstanceId = "";
        public string Name = "";
        public string DeviceClass = "other";
        public string Vid = "";
        public string Pid = "";
        public string Serial = "";
        public List<string> Drives = new List<string>();
        public DateTime FirstSeen;
        public DateTime LastSeen;
        public DeviceTrust Trust = DeviceTrust.Unknown;
        public bool Removable;
        public bool Keyboard;
        public bool CompositeSuspect;
        public string Status = "";

        public string HardwareId
        {
            get
            {
                if (Vid.Length == 0 && Pid.Length == 0) return "";
                return (Vid.Length > 0 ? "VID_" + Vid : "") +
                       (Pid.Length > 0 ? (Vid.Length > 0 ? "&" : "") + "PID_" + Pid : "");
            }
        }
    }

    internal sealed class DeviceScanResult
    {
        public List<DeviceInfo> Devices = new List<DeviceInfo>();
        public List<DeviceInfo> NewDevices = new List<DeviceInfo>();
        public bool NewKeyboard;
        public bool FirstRun;
        public string Error = "";
    }

    internal sealed class DeviceRecord
    {
        public string Key = "";
        public string Name = "";
        public string DeviceClass = "";
        public DeviceTrust Trust = DeviceTrust.Unknown;
        public DateTime FirstSeen;
        public DateTime LastSeen;
    }

    internal static class DeviceStore
    {
        private static readonly object Gate = new object();
        private const string MutexName = @"Global\ShieldGate_Devices_2";
        private static DateTime _lastSave = DateTime.MinValue;

        public static bool HasAny()
        {
            try { return LoadMap().Count > 0; }
            catch { return false; }
        }

        public static void ApplyTrust(IList<DeviceInfo> devices)
        {
            if (devices == null) return;
            Dictionary<string, DeviceRecord> map = LoadMap();
            foreach (DeviceInfo d in devices)
            {
                DeviceRecord r;
                if (map.TryGetValue(d.Key, out r))
                {
                    d.Trust = r.Trust;
                    d.FirstSeen = r.FirstSeen;
                    d.LastSeen = r.LastSeen;
                }
            }
        }

        // Gop thiet bi vua thay vao kho, giu FirstSeen/Trust cua lan truoc.
        // created: khoa cua thiet bi lan dau xuat hien, de bao "thiet bi moi"
        // ma khong phai suy doan tu moc thoi gian.
        public static void Sync(IList<DeviceInfo> devices, out bool firstRun, out List<string> created)
        {
            firstRun = false;
            created = new List<string>();
            if (devices == null) return;
            lock (Gate)
            {
                Mutex gate = null;
                try { gate = new Mutex(false, MutexName); } catch { }
                try
                {
                    try { if (gate != null) gate.WaitOne(5000); } catch { }
                    Dictionary<string, DeviceRecord> map = LoadMap();
                    firstRun = map.Count == 0;
                    bool changed = false;
                    foreach (DeviceInfo d in devices)
                    {
                        DeviceRecord r;
                        if (map.TryGetValue(d.Key, out r))
                        {
                            if (!string.Equals(r.Name, d.Name, StringComparison.Ordinal) ||
                                !string.Equals(r.DeviceClass, d.DeviceClass, StringComparison.Ordinal))
                            {
                                r.Name = d.Name;
                                r.DeviceClass = d.DeviceClass;
                                changed = true;
                            }
                            r.LastSeen = DateTime.Now;
                            d.Trust = r.Trust;
                            d.FirstSeen = r.FirstSeen;
                            d.LastSeen = r.LastSeen;
                        }
                        else
                        {
                            r = new DeviceRecord();
                            r.Key = d.Key;
                            r.Name = d.Name;
                            r.DeviceClass = d.DeviceClass;
                            r.Trust = DeviceTrust.Unknown;
                            r.FirstSeen = DateTime.Now;
                            r.LastSeen = DateTime.Now;
                            map[d.Key] = r;
                            created.Add(d.Key);
                            d.FirstSeen = r.FirstSeen;
                            d.LastSeen = r.LastSeen;
                            changed = true;
                        }
                    }
                    // Không ghi đĩa mỗi 15 giây: chỉ khi có thay đổi hoặc 10 phút.
                    if (changed || (DateTime.Now - _lastSave).TotalMinutes >= 10)
                    {
                        if (SaveMap(map)) _lastSave = DateTime.Now;
                    }
                }
                finally
                {
                    try { if (gate != null) gate.ReleaseMutex(); } catch { }
                    if (gate != null) { try { gate.Dispose(); } catch { } }
                }
            }
        }

        public static bool SetTrust(string key, DeviceTrust trust)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (Gate)
            {
                Mutex gate = null;
                try { gate = new Mutex(false, MutexName); } catch { }
                try
                {
                    try { if (gate != null) gate.WaitOne(5000); } catch { }
                    Dictionary<string, DeviceRecord> map = LoadMap();
                    DeviceRecord r;
                    if (!map.TryGetValue(key, out r))
                    {
                        r = new DeviceRecord();
                        r.Key = key;
                        r.FirstSeen = DateTime.Now;
                        r.LastSeen = DateTime.Now;
                        map[key] = r;
                    }
                    r.Trust = trust;
                    r.LastSeen = DateTime.Now;
                    return SaveMap(map);
                }
                finally
                {
                    try { if (gate != null) gate.ReleaseMutex(); } catch { }
                    if (gate != null) { try { gate.Dispose(); } catch { } }
                }
            }
        }

        public static bool Forget(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (Gate)
            {
                Mutex gate = null;
                try { gate = new Mutex(false, MutexName); } catch { }
                try
                {
                    try { if (gate != null) gate.WaitOne(5000); } catch { }
                    Dictionary<string, DeviceRecord> map = LoadMap();
                    if (!map.Remove(key)) return true;
                    return SaveMap(map);
                }
                finally
                {
                    try { if (gate != null) gate.ReleaseMutex(); } catch { }
                    if (gate != null) { try { gate.Dispose(); } catch { } }
                }
            }
        }

        public static List<DeviceRecord> All()
        {
            List<DeviceRecord> list = new List<DeviceRecord>();
            try { list.AddRange(LoadMap().Values); }
            catch { }
            list.Sort(delegate(DeviceRecord a, DeviceRecord b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        private static Dictionary<string, DeviceRecord> LoadMap()
        {
            Dictionary<string, DeviceRecord> map =
                new Dictionary<string, DeviceRecord>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(Paths.DevicesFile)) return map;
                Dictionary<string, object> root = Json.AsObject(Json.ParseFile(Paths.DevicesFile));
                if (root == null) return map;
                List<object> arr = Json.AsArray(Json.Get(root, "devices"));
                if (arr == null) return map;
                foreach (object o in arr)
                {
                    Dictionary<string, object> d = Json.AsObject(o);
                    if (d == null) continue;
                    string key = Json.Str(d, "key", "");
                    if (key.Length == 0) continue;
                    DeviceRecord r = new DeviceRecord();
                    r.Key = key;
                    r.Name = Json.Str(d, "name", "");
                    r.DeviceClass = Json.Str(d, "class", "");
                    string trust = Json.Str(d, "trust", "unknown");
                    if (string.Equals(trust, "trusted", StringComparison.OrdinalIgnoreCase))
                        r.Trust = DeviceTrust.Trusted;
                    else if (string.Equals(trust, "blocked", StringComparison.OrdinalIgnoreCase))
                        r.Trust = DeviceTrust.Blocked;
                    DateTime t;
                    DateTime.TryParse(Json.Str(d, "first", ""), out t);
                    r.FirstSeen = t;
                    DateTime.TryParse(Json.Str(d, "last", ""), out t);
                    r.LastSeen = t;
                    map[key] = r;
                }
            }
            catch (Exception e)
            {
                try { Log.Write("devices.json loi, bo qua: " + e.Message, "WARN"); } catch { }
            }
            return map;
        }

        private static bool SaveMap(Dictionary<string, DeviceRecord> map)
        {
            try
            {
                Paths.EnsureDirs();
                Dictionary<string, object> root = new Dictionary<string, object>();
                root["version"] = 1;
                List<object> arr = new List<object>();
                foreach (DeviceRecord r in map.Values)
                {
                    Dictionary<string, object> d = new Dictionary<string, object>();
                    d["key"] = r.Key;
                    d["name"] = r.Name;
                    d["class"] = r.DeviceClass;
                    d["trust"] = TrustCode(r.Trust);
                    d["first"] = r.FirstSeen.ToString("o");
                    d["last"] = r.LastSeen.ToString("o");
                    arr.Add(d);
                }
                root["devices"] = arr;
                string text = Json.Serialize(root, true);
                string tmp = Paths.DevicesFile + ".tmp";
                File.WriteAllText(tmp, text, new UTF8Encoding(false));
                if (File.Exists(Paths.DevicesFile))
                {
                    try { File.Replace(tmp, Paths.DevicesFile, null); }
                    catch
                    {
                        File.Delete(Paths.DevicesFile);
                        File.Move(tmp, Paths.DevicesFile);
                    }
                }
                else File.Move(tmp, Paths.DevicesFile);
                return true;
            }
            catch (Exception e)
            {
                try { Log.Write("khong ghi duoc devices.json: " + e.Message, "WARN"); } catch { }
                return false;
            }
        }

        public static string TrustCode(DeviceTrust t)
        {
            if (t == DeviceTrust.Trusted) return "trusted";
            if (t == DeviceTrust.Blocked) return "blocked";
            return "unknown";
        }
    }

    internal static class Devices
    {
        private static readonly Regex VidRe = new Regex(@"VID_([0-9A-Fa-f]{4})", RegexOptions.Compiled);
        private static readonly Regex PidRe = new Regex(@"PID_([0-9A-Fa-f]{4})", RegexOptions.Compiled);

        // Quet hien tai. Khong dung Settings/Save ngoai DeviceStore.
        public static DeviceScanResult Scan()
        {
            DeviceScanResult result = new DeviceScanResult();
            try
            {
                List<DeviceInfo> found = Enumerate();
                MarkComposite(found);
                bool first;
                List<string> created;
                DeviceStore.Sync(found, out first, out created);
                result.FirstRun = first;

                // Lan quet dau chi lap kho, khong bao toan bo may la "thiet bi moi".
                HashSet<string> fresh = new HashSet<string>(created, StringComparer.OrdinalIgnoreCase);
                foreach (DeviceInfo d in found)
                {
                    result.Devices.Add(d);
                    if (!first && fresh.Contains(d.Key)) result.NewDevices.Add(d);
                }
                foreach (DeviceInfo d in result.NewDevices)
                {
                    if (d.Keyboard && !IsInternalKeyboard(d)) result.NewKeyboard = true;
                }
            }
            catch (Exception e)
            {
                result.Error = e.Message;
            }
            return result;
        }

        private static List<DeviceInfo> Enumerate()
        {
            List<DeviceInfo> list = new List<DeviceInfo>();
            Dictionary<string, List<string>> drives = RemovableDrivesByPnp();
            Dictionary<string, string[]> diskInfo = DiskDriveInfo();

            ManagementObjectCollection entities = Query(
                @"\\.\root\cimv2",
                "SELECT DeviceID, Name, PNPClass, Service, Status, Manufacturer FROM Win32_PnPEntity");
            if (entities == null) return list;

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (ManagementBaseObject o in entities)
                {
                    ManagementObject mo = o as ManagementObject;
                    if (mo == null) continue;
                    try
                    {
                        string id = Convert.ToString(mo["DeviceID"]);
                        string name = Convert.ToString(mo["Name"]);
                        string pnpClass = Convert.ToString(mo["PNPClass"]);
                        string service = Convert.ToString(mo["Service"]);
                        string status = Convert.ToString(mo["Status"]);
                        string manufacturer = Convert.ToString(mo["Manufacturer"]);
                        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name)) continue;

                        string cls = Classify(pnpClass, service, name, id);
                        if (cls == "other" || cls == "usb") continue;

                        string vid, pid;
                        ParseHardwareId(id, out vid, out pid);
                        string serial = ParseSerial(id);

                        // Chỉ giữ thiết bị ngoại vi thật; hub/controller USB bị bỏ qua.
                        DeviceInfo d = new DeviceInfo();
                        d.InstanceId = id;
                        d.Name = name;
                        d.DeviceClass = cls;
                        d.Vid = vid;
                        d.Pid = pid;
                        d.Serial = serial;
                        d.Status = status;
                        d.Removable = cls == "storage";
                        d.Keyboard = cls == "keyboard";

                        string[] disk;
                        if (diskInfo.TryGetValue(id, out disk))
                        {
                            if (string.IsNullOrEmpty(d.Name) || d.Name.Length < 3) d.Name = disk[0];
                            if (!string.IsNullOrEmpty(disk[1])) d.Serial = disk[1];
                            d.Removable = true;
                        }
                        if (d.Serial.Length == 0 && disk != null && disk.Length > 1) d.Serial = disk[1];

                        List<string> letters;
                        if (drives.TryGetValue(id, out letters)) d.Drives.AddRange(letters);
                        if (d.Drives.Count == 0) AddDrivesBySerial(d, drives);

                        d.Key = KeyFor(d);
                        if (!seen.Add(d.Key)) continue;
                        if (string.IsNullOrEmpty(d.Name)) d.Name = manufacturer;
                        list.Add(d);
                    }
                    catch { }
                    finally { try { mo.Dispose(); } catch { } }
                }
            }
            finally
            {
                try { entities.Dispose(); } catch { }
            }

            // Sap xep: o nho / ban phim truoc, de nguoi dung thay ngay.
            list.Sort(delegate(DeviceInfo a, DeviceInfo b)
            {
                int wa = Weight(a), wb = Weight(b);
                if (wa != wb) return wa.CompareTo(wb);
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            if (list.Count > 200) list = list.GetRange(0, 200);
            return list;
        }

        private static int Weight(DeviceInfo d)
        {
            if (d.Removable) return 0;
            if (d.Keyboard) return 1;
            if (d.DeviceClass == "mtp" || d.DeviceClass == "bluetooth") return 2;
            return 3;
        }

        public static string Classify(string pnpClass, string service, string name, string id)
        {
            string c = (pnpClass ?? "").ToLowerInvariant();
            string s = (service ?? "").ToLowerInvariant();
            string n = (name ?? "").ToLowerInvariant();
            string i = (id ?? "").ToLowerInvariant();

            if (c == "diskdrive" || s == "usbstor" || i.StartsWith("usbstor\\")) return "storage";
            if (c == "keyboard" || s == "kbdhid" || s == "kbdclass" ||
                (c == "hidclass" && n.IndexOf("keyboard", StringComparison.Ordinal) >= 0))
                return "keyboard";
            if (c == "mouse" || s == "mouhid" || s == "mouclass" ||
                (c == "hidclass" && n.IndexOf("mouse", StringComparison.Ordinal) >= 0))
                return "mouse";
            if (c == "hidclass" || c == "hid") return "hid";
            if (c == "bluetooth" || c == "bluetoothradio") return "bluetooth";
            if (c == "wpd" || c == "portabledevice" || c == "image" ||
                n.IndexOf("portable device", StringComparison.Ordinal) >= 0) return "mtp";
            if (c == "net") return "network";
            if (c == "usb") return "usb";
            return "other";
        }

        // VID/PID co the nam o bat ky dau chuoi PNPDeviceID.
        public static void ParseHardwareId(string id, out string vid, out string pid)
        {
            vid = "";
            pid = "";
            if (string.IsNullOrEmpty(id)) return;
            Match m = VidRe.Match(id);
            if (m.Success) vid = m.Groups[1].Value.ToUpperInvariant();
            m = PidRe.Match(id);
            if (m.Success) pid = m.Groups[1].Value.ToUpperInvariant();
        }

        public static string ParseSerial(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            // Phan sau dau \ cuoi cung la serial/instance; bo cac gia tri vo nghia.
            int cut = id.LastIndexOf('\\');
            if (cut < 0 || cut + 1 >= id.Length) return "";
            string s = id.Substring(cut + 1).Trim();
            if (s.Length == 0 || s == "0" || s.StartsWith("&", StringComparison.Ordinal)) return "";
            if (s.Length > 80) s = s.Substring(0, 80);
            return s;
        }

        public static string KeyFor(DeviceInfo d)
        {
            if (d == null) return "";
            if (d.Vid.Length > 0 && d.Pid.Length > 0 && d.Serial.Length > 0)
                return (d.Vid + ":" + d.Pid + ":" + d.Serial).ToUpperInvariant();
            return (d.InstanceId ?? "").ToUpperInvariant();
        }

        // Raw Input tra ve \\?\HID#VID_...#{GUID}; kho luu HID\VID_... . Chuan
        // hoa ca hai roi so, de ghep burst voi dung thiet bi trong trust store.
        public static bool MatchesRaw(DeviceInfo d, string rawName)
        {
            if (d == null || string.IsNullOrEmpty(rawName)) return false;
            string raw = NormalizeRaw(rawName);
            string id = NormalizeRaw(d.InstanceId);
            if (raw.Length > 0 && id.Length > 0)
            {
                if (string.Equals(raw, id, StringComparison.OrdinalIgnoreCase)) return true;
                if (raw.StartsWith(id + "\\", StringComparison.OrdinalIgnoreCase) ||
                    id.StartsWith(raw + "\\", StringComparison.OrdinalIgnoreCase)) return true;
            }

            // Du phong: cung VID/PID. Chi dung khi TrayApp da loc ban phim.
            string vid, pid;
            ParseHardwareId(rawName, out vid, out pid);
            return vid.Length > 0 && pid.Length > 0
                && string.Equals(vid, d.Vid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(pid, d.Pid, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeRaw(string value)
        {
            string s = (value ?? "").Trim();
            if (s.StartsWith(@"\\?\", StringComparison.Ordinal)) s = s.Substring(4);
            s = s.Replace('#', '\\');
            int brace = s.IndexOf('{');
            if (brace >= 0) s = s.Substring(0, brace);
            return s.TrimEnd('\\');
        }

        // Ban phim ngoai theo bus: USB, Bluetooth hoac HID co VID (USB HID).
        // Ban phim trong cua laptop thuong la ACPI/PS2, ROOT, SWD, hoac HID
        // khong co VID (I2C HID nhu MSFT0001/ELAN0001). Day la heuristic: mot
        // so laptop noi ban phim qua USB noi bo van bi coi la ngoai.
        public static bool IsInternalKeyboard(DeviceInfo d)
        {
            if (d == null) return false;
            return IsInternalKeyboard(d.InstanceId);
        }

        public static bool IsInternalKeyboard(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return true;
            string id = instanceId.Trim();
            if (StartsWithBus(id, "ACPI\\")) return true;
            if (StartsWithBus(id, "ROOT\\")) return true;
            if (StartsWithBus(id, "SWD\\")) return true;
            if (StartsWithBus(id, "BTHENUM\\")) return false;
            if (StartsWithBus(id, "BTHLEDEVICE\\")) return false;
            if (StartsWithBus(id, "BTH\\")) return false;
            if (StartsWithBus(id, "USB\\")) return false;
            if (StartsWithBus(id, "HID\\"))
                return id.IndexOf("VID_", StringComparison.OrdinalIgnoreCase) < 0;
            return true;
        }

        private static bool StartsWithBus(string id, string prefix)
        {
            return id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        // Cung VID/PID vua co o nho vua co ban phim: dau hieu composite/BadUSB.
        // Chi la heuristic; hub có nhiều thiết bị cùng hãng có thể trùng.
        public static void MarkComposite(IList<DeviceInfo> devices)
        {
            if (devices == null) return;
            Dictionary<string, bool> storage = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> keyboard = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (DeviceInfo d in devices)
            {
                if (d.Vid.Length == 0 || d.Pid.Length == 0) continue;
                string k = d.Vid + ":" + d.Pid;
                if (d.Removable) storage[k] = true;
                if (d.Keyboard && !IsInternalKeyboard(d)) keyboard[k] = true;
            }
            foreach (DeviceInfo d in devices)
            {
                if (d.Vid.Length == 0 || d.Pid.Length == 0) continue;
                string k = d.Vid + ":" + d.Pid;
                if (storage.ContainsKey(k) && keyboard.ContainsKey(k)) d.CompositeSuspect = true;
            }
        }

        // ---------------------------------------------------------- WMI phu tro

        private static ManagementObjectCollection Query(string scopePath, string wql)
        {
            try
            {
                ManagementScope scope = new ManagementScope(scopePath);
                scope.Connect();
                ManagementObjectSearcher s = new ManagementObjectSearcher(scope, new ObjectQuery(wql));
                s.Options.Timeout = TimeSpan.FromSeconds(12);
                return s.Get();
            }
            catch { return null; }
        }

        private static Dictionary<string, List<string>> RemovableDrivesByPnp()
        {
            Dictionary<string, List<string>> map =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            ManagementObjectCollection col = Query(@"\\.\root\cimv2",
                "SELECT DeviceID, PNPDeviceID FROM Win32_LogicalDisk WHERE DriveType=2");
            if (col == null) return map;
            try
            {
                foreach (ManagementBaseObject o in col)
                {
                    ManagementObject mo = o as ManagementObject;
                    if (mo == null) continue;
                    try
                    {
                        string letter = Convert.ToString(mo["DeviceID"]);
                        string pnp = Convert.ToString(mo["PNPDeviceID"]);
                        if (string.IsNullOrEmpty(letter) || string.IsNullOrEmpty(pnp)) continue;
                        List<string> list;
                        if (!map.TryGetValue(pnp, out list))
                        {
                            list = new List<string>();
                            map[pnp] = list;
                        }
                        if (!list.Contains(letter)) list.Add(letter);
                    }
                    catch { }
                    finally { try { mo.Dispose(); } catch { } }
                }
            }
            finally { try { col.Dispose(); } catch { } }
            return map;
        }

        private static Dictionary<string, string[]> DiskDriveInfo()
        {
            Dictionary<string, string[]> map =
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            ManagementObjectCollection col = Query(@"\\.\root\cimv2",
                "SELECT PNPDeviceID, Model, SerialNumber, InterfaceType, MediaType FROM Win32_DiskDrive");
            if (col == null) return map;
            try
            {
                foreach (ManagementBaseObject o in col)
                {
                    ManagementObject mo = o as ManagementObject;
                    if (mo == null) continue;
                    try
                    {
                        string pnp = Convert.ToString(mo["PNPDeviceID"]);
                        if (string.IsNullOrEmpty(pnp)) continue;
                        string model = Convert.ToString(mo["Model"]);
                        string serial = Convert.ToString(mo["SerialNumber"]);
                        string media = Convert.ToString(mo["MediaType"]);
                        string iface = Convert.ToString(mo["InterfaceType"]);
                        map[pnp] = new string[] { model, serial, media, iface };
                    }
                    catch { }
                    finally { try { mo.Dispose(); } catch { } }
                }
            }
            finally { try { col.Dispose(); } catch { } }
            return map;
        }

        private static void AddDrivesBySerial(DeviceInfo d, Dictionary<string, List<string>> drives)
        {
            if (string.IsNullOrEmpty(d.Serial)) return;
            foreach (KeyValuePair<string, List<string>> kv in drives)
            {
                if (kv.Key.IndexOf(d.Serial, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foreach (string letter in kv.Value)
                        if (!d.Drives.Contains(letter)) d.Drives.Add(letter);
                }
            }
        }

        // --------------------------------------------------- BadUSB muc 1

        // Ban phim moi vua xuat hien ma shell process chay ngay sau do: dang ngo.
        // Day chi la canh bao, khong tu tat thiet bi.
        public static string RecentShellProcess(DateTime since, out int pid)
        {
            pid = 0;
            string detail = "";
            ManagementObjectCollection col = Query(@"\\.\root\cimv2",
                "SELECT ProcessId, Name, CreationDate FROM Win32_Process " +
                "WHERE Name='cmd.exe' OR Name='powershell.exe' OR Name='pwsh.exe' " +
                "OR Name='wscript.exe' OR Name='cscript.exe' OR Name='mshta.exe'");
            if (col == null) return "";
            try
            {
                DateTime newest = since.AddSeconds(-1);
                foreach (ManagementBaseObject o in col)
                {
                    ManagementObject mo = o as ManagementObject;
                    if (mo == null) continue;
                    try
                    {
                        DateTime created;
                        string raw = Convert.ToString(mo["CreationDate"]);
                        try { created = ManagementDateTimeConverter.ToDateTime(raw); }
                        catch { continue; }
                        if (created < since.AddSeconds(-2)) continue;
                        if (created < newest) continue;
                        newest = created;
                        pid = Convert.ToInt32(mo["ProcessId"]);
                        // Chi ten image: khong giu CommandLine vi co the chua
                        // chuoi nguoi dung vua go (xem docs/design-badusb.md).
                        detail = Convert.ToString(mo["Name"]);
                    }
                    catch { }
                    finally { try { mo.Dispose(); } catch { } }
                }
            }
            finally { try { col.Dispose(); } catch { } }
            return detail.Trim();
        }
    }
}
