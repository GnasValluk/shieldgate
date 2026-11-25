// ShieldGate 3.0 - allow-list lệnh cho named pipe.
// Engine không tin kế hoạch: mỗi apply lặp lại kiểm tra ổ, thời hạn, SID/session
// và serial ổ; chỉ số thứ tự mục được nhận, không nhận đường dẫn từ client.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace ShieldGate
{
    internal sealed class PipePlan
    {
        public string Id;
        public string Kind;
        public string Drive;
        public string Root;
        public uint Serial;
        public string Sid;
        public uint Session;
        public DateTime CreatedUtc;
        public UsbScanResult Scan;
        public List<UsbItem> Items = new List<UsbItem>();
        public UnhideScope Scope;
        public int Count;
    }

    internal sealed class PipeJob
    {
        public string Id;
        public string Kind;
        public string Drive;
        public string Root;
        public string Sid;
        public uint Session;
        public DateTime StartedUtc;
        public string State = "running";
        public int Files;
        public int Items;
        public string PlanId = "";
        public UsbScanResult Scan;
        public volatile bool Cancel;
    }

    internal static class PipeCommands
    {
        public const int PlanTtlSeconds = 120;

        private static readonly HashSet<string> Known =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "status.get",
                "devices.list",
                "events.since",
                "job.start",
                "job.status",
                "job.cancel",
                "clean.plan",
                "clean.apply",
                "unhide.plan",
                "unhide.apply",
                "rename.plan",
                "rename.apply"
            };

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, PipePlan> Plans =
            new Dictionary<string, PipePlan>(StringComparer.Ordinal);
        private static PipeJob _job;

        public static bool IsKnown(string cmd)
        {
            return !string.IsNullOrEmpty(cmd) && Known.Contains(cmd);
        }

        // Phiên mới: kế hoạch cũ không còn giá trị. Job đang chạy bị huỷ ở điểm an toàn.
        public static void ClientConnected(PipeClientInfo info)
        {
            lock (Gate)
            {
                Plans.Clear();
                if (_job != null && _job.State == "running") _job.Cancel = true;
            }
        }

        // Ngắt kết nối: huỷ plan và job để không áp kế hoạch của phiên đã chết.
        public static void ClientDisconnected()
        {
            lock (Gate)
            {
                Plans.Clear();
                if (_job != null && _job.State == "running") _job.Cancel = true;
            }
        }

        public static string Handle(PipeRequest req, PipeClientInfo client)
        {
            try
            {
                switch (req.Cmd)
                {
                    case "status.get":
                        if (!Only(req)) return BadArgs(req);
                        return Status(req, client);
                    case "devices.list":
                        if (!Only(req)) return BadArgs(req);
                        return DevicesList(req);
                    case "events.since":
                        if (!Only(req, "after")) return BadArgs(req);
                        return EventsSince(req);
                    case "job.start":
                        if (!Only(req, "kind", "drive")) return BadArgs(req);
                        return JobStart(req, client);
                    case "job.status":
                        if (!Only(req, "job")) return BadArgs(req);
                        return JobStatus(req);
                    case "job.cancel":
                        if (!Only(req, "job")) return BadArgs(req);
                        return JobCancel(req);
                    case "clean.plan":
                        if (!Only(req, "drive", "job")) return BadArgs(req);
                        return CleanPlan(req, client);
                    case "clean.apply":
                        if (!Only(req, "plan", "ids", "confirm")) return BadArgs(req);
                        return CleanApply(req, client);
                    case "unhide.plan":
                        if (!Only(req, "drive", "scope")) return BadArgs(req);
                        return UnhidePlan(req, client);
                    case "unhide.apply":
                        if (!Only(req, "plan", "confirm")) return BadArgs(req);
                        return UnhideApply(req, client);
                    case "rename.plan":
                        if (!Only(req, "drive")) return BadArgs(req);
                        return RenamePlan(req, client);
                    case "rename.apply":
                        if (!Only(req, "plan", "confirm")) return BadArgs(req);
                        return RenameApply(req, client);
                    default:
                        return PipeProtocol.Error(req.Id, "unknown-command", req.Cmd);
                }
            }
            catch (Exception e)
            {
                return PipeProtocol.Error(req.Id, "failed", e.Message);
            }
        }

        private static bool Only(PipeRequest req, params string[] keys)
        {
            if (req.Args == null) return true;
            foreach (string key in req.Args.Keys)
            {
                bool found = false;
                for (int i = 0; i < keys.Length; i++)
                    if (string.Equals(key, keys[i], StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        private static string BadArgs(PipeRequest req)
        {
            return PipeProtocol.Error(req.Id, "bad-args", "unexpected argument");
        }

        private static string Ok(PipeRequest req, Dictionary<string, object> result)
        {
            return PipeProtocol.Response(req.Id, result);
        }

        // ------------------------------------------------------------- lệnh đọc

        private static string Status(PipeRequest req, PipeClientInfo client)
        {
            StatusInfo st = Guard.GetStatus();
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["version"] = AppInfo.Version;
            r["protocol"] = PipeProtocol.Version;
            r["admin"] = st.Admin;
            r["autorun"] = st.Autorun;
            r["ini"] = st.Ini;
            r["wsh"] = st.Wsh;
            r["registry"] = st.Registry;
            r["task"] = st.Task;
            r["running"] = st.Running;
            r["writeProtect"] = st.WriteProtect;
            r["blockExec"] = st.BlockExec;
            r["quarantineFiles"] = st.QuarantineFiles;
            r["quarantineBatches"] = st.QuarantineBatches;
            r["clientPid"] = client == null ? 0 : (int)client.ProcessId;
            r["clientSession"] = client == null ? 0 : (int)client.SessionId;
            return Ok(req, r);
        }

        private static string DevicesList(PipeRequest req)
        {
            DeviceScanResult scan = Devices.Scan();
            List<object> list = new List<object>();
            if (scan != null)
            {
                foreach (DeviceInfo d in scan.Devices)
                {
                    Dictionary<string, object> item = new Dictionary<string, object>();
                    item["key"] = d.Key;
                    item["name"] = d.Name;
                    item["class"] = d.DeviceClass;
                    item["vid"] = d.Vid;
                    item["pid"] = d.Pid;
                    item["serial"] = d.Serial;
                    item["trust"] = d.Trust.ToString().ToLowerInvariant();
                    item["removable"] = d.Removable;
                    item["keyboard"] = d.Keyboard;
                    item["composite"] = d.CompositeSuspect;
                    item["status"] = d.Status;
                    list.Add(item);
                }
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["devices"] = list;
            r["error"] = scan == null ? "scan failed" : scan.Error;
            return Ok(req, r);
        }

        private static string EventsSince(PipeRequest req)
        {
            int after = Json.Int(req.Args, "after", 0);
            if (after < 0) after = 0;
            List<EventEntry> entries = Events.ReadSince(after);
            List<object> list = new List<object>();
            long last = after;
            if (entries != null)
            {
                for (int i = 0; i < entries.Count && i < 200; i++)
                {
                    EventEntry e = entries[i];
                    Dictionary<string, object> item = new Dictionary<string, object>();
                    item["id"] = e.Id;
                    item["time"] = e.Time.ToString("yyyy-MM-dd HH:mm:ss");
                    item["kind"] = e.Kind;
                    item["severity"] = e.Severity;
                    item["subject"] = e.Subject;
                    item["detail"] = e.Detail;
                    list.Add(item);
                    if (e.Id > last) last = e.Id;
                }
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["events"] = list;
            r["last"] = last;
            return Ok(req, r);
        }

        // ------------------------------------------------------------ job quét

        private static string JobStart(PipeRequest req, PipeClientInfo client)
        {
            string kind = Json.Str(req.Args, "kind", "").Trim().ToLowerInvariant();
            if (kind != "scan") return BadArgs(req);
            string drive, root, code, message;
            if (!TryDrive(req, out drive, out root, out code, out message))
                return PipeProtocol.Error(req.Id, code, message);
            string reason;
            if (Repair.RefuseReason(root, false, out reason))
                return PipeProtocol.Error(req.Id, "refused", reason);

            PipeJob job = new PipeJob();
            job.Id = NewId("j");
            job.Kind = kind;
            job.Drive = drive;
            job.Root = root;
            job.Sid = client == null ? "" : client.UserSid;
            job.Session = client == null ? 0 : client.SessionId;
            job.StartedUtc = DateTime.UtcNow;

            lock (Gate)
            {
                if (_job != null && _job.State == "running")
                    return PipeProtocol.Error(req.Id, "busy", "a job is already running");
                _job = job;
            }

            Thread t = new Thread(delegate() { RunScanJob(job); });
            t.IsBackground = true;
            t.Name = "ShieldGate pipe job";
            t.Start();
            Log.Write(S.Log.PipeJob(job.Id, kind, drive, "started"), "INFO");

            Dictionary<string, object> r = new Dictionary<string, object>();
            r["job"] = job.Id;
            r["kind"] = kind;
            r["drive"] = drive;
            r["state"] = job.State;
            return Ok(req, r);
        }

        private static void RunScanJob(PipeJob job)
        {
            UsbScanResult scan = null;
            string error = "";
            try
            {
                scan = UsbScan.Detect(job.Root, delegate(int n, string current)
                {
                    job.Files = n;
                }, delegate() { return job.Cancel; });
            }
            catch (Exception e)
            {
                error = e.Message;
            }

            lock (Gate)
            {
                if (scan != null)
                {
                    job.Scan = scan;
                    job.Files = scan.Files;
                    job.Items = scan.Items.Count;
                }
                if (error.Length > 0)
                {
                    job.State = "failed";
                }
                else if (job.Cancel || (scan != null && scan.Cancelled))
                {
                    job.State = "cancelled";
                }
                else
                {
                    job.State = "done";
                    PipePlan plan = NewPlan("scan", job.Drive, job.Root, job.Sid, job.Session);
                    plan.Scan = scan;
                    plan.Items = scan == null ? new List<UsbItem>() : scan.Items;
                    plan.Count = plan.Items.Count;
                    StorePlanLocked(plan);
                    job.PlanId = plan.Id;
                }
            }
            Log.Write(S.Log.PipeJob(job.Id, job.Kind, job.Drive,
                error.Length > 0 ? "failed: " + error : job.State), "INFO");
        }

        private static string JobStatus(PipeRequest req)
        {
            string id = Json.Str(req.Args, "job", "");
            PipeJob job;
            lock (Gate)
            {
                job = _job;
                if (job == null || !string.Equals(job.Id, id, StringComparison.Ordinal))
                    return PipeProtocol.Error(req.Id, "not-found", "unknown job");
                Dictionary<string, object> r = new Dictionary<string, object>();
                r["job"] = job.Id;
                r["kind"] = job.Kind;
                r["drive"] = job.Drive;
                r["state"] = job.State;
                r["files"] = job.Files;
                r["items"] = job.Items;
                r["seconds"] = Math.Round((DateTime.UtcNow - job.StartedUtc).TotalSeconds, 1);
                r["plan"] = job.PlanId;
                List<object> warnings = new List<object>();
                if (job.Scan != null && job.Scan.Warnings != null)
                    foreach (string w in job.Scan.Warnings) warnings.Add(w);
                r["warnings"] = warnings;
                return Ok(req, r);
            }
        }

        private static string JobCancel(PipeRequest req)
        {
            string id = Json.Str(req.Args, "job", "");
            lock (Gate)
            {
                if (_job == null || !string.Equals(_job.Id, id, StringComparison.Ordinal))
                    return PipeProtocol.Error(req.Id, "not-found", "unknown job");
                _job.Cancel = true;
                Dictionary<string, object> r = new Dictionary<string, object>();
                r["job"] = _job.Id;
                r["state"] = _job.State;
                r["cancelled"] = true;
                return Ok(req, r);
            }
        }

        // ---------------------------------------------------------- plan / apply

        private static string CleanPlan(PipeRequest req, PipeClientInfo client)
        {
            string drive, root, code, message;
            if (!TryDrive(req, out drive, out root, out code, out message))
                return PipeProtocol.Error(req.Id, code, message);
            string reason;
            if (Repair.RefuseReason(root, false, out reason))
                return PipeProtocol.Error(req.Id, "refused", reason);

            UsbScanResult scan = null;
            string jobId = Json.Str(req.Args, "job", "");
            if (jobId.Length > 0)
            {
                lock (Gate)
                {
                    if (_job == null || !string.Equals(_job.Id, jobId, StringComparison.Ordinal)
                        || _job.State != "done" || _job.Scan == null
                        || !string.Equals(_job.Root, root, StringComparison.OrdinalIgnoreCase))
                        return PipeProtocol.Error(req.Id, "not-found", "scan job is not ready for this drive");
                    if (client != null
                        && (!string.Equals(_job.Sid, client.UserSid, StringComparison.OrdinalIgnoreCase)
                            || _job.Session != client.SessionId))
                        return PipeProtocol.Error(req.Id, "refused", "job belongs to another client");
                    scan = _job.Scan;
                }
            }
            if (scan == null)
                scan = UsbScan.Detect(root, null, null);

            PipePlan plan = NewPlan("clean", drive, root,
                client == null ? "" : client.UserSid,
                client == null ? 0 : client.SessionId);
            plan.Scan = scan;
            plan.Items = scan.Items;
            plan.Count = scan.Items.Count;

            Dictionary<string, object> r = new Dictionary<string, object>();
            List<object> items = new List<object>();
            lock (Gate)
            {
                StorePlanLocked(plan);
                for (int i = 0; i < plan.Items.Count; i++)
                {
                    UsbItem it = plan.Items[i];
                    Dictionary<string, object> item = new Dictionary<string, object>();
                    item["id"] = i + 1;
                    item["rel"] = it.Rel;
                    item["kind"] = KindText(it.Kind);
                    item["score"] = it.Score;
                    item["high"] = it.High;
                    item["flagOnly"] = it.FlagOnly;
                    item["size"] = it.Size;
                    List<object> codes = new List<object>();
                    foreach (string c in it.Codes) codes.Add(c);
                    item["codes"] = codes;
                    items.Add(item);
                }
            }
            r["plan"] = plan.Id;
            r["drive"] = drive;
            r["serial"] = (long)plan.Serial;
            r["files"] = scan.Files;
            r["items"] = items;
            List<object> warnings = new List<object>();
            foreach (string w in scan.Warnings) warnings.Add(w);
            r["warnings"] = warnings;
            Log.Write(S.Log.PipePlan(plan.Id, "clean", drive, plan.Count), "INFO");
            return Ok(req, r);
        }

        private static string CleanApply(PipeRequest req, PipeClientInfo client)
        {
            if (!Confirm(req)) return BadArgs(req);
            string drive, root, code, message;
            if (!TryDrive(req, out drive, out root, out code, out message))
                return PipeProtocol.Error(req.Id, code, message);

            // Lặp lại kiểm tra ghi trên ổ; plan không miễn trừ bất kỳ bước nào.
            string reason;
            if (Repair.RefuseReason(root, true, out reason))
                return PipeProtocol.Error(req.Id, "refused", reason);
            if (!Native.CanWrite(root))
                return PipeProtocol.Error(req.Id, "refused", "drive is read-only");

            PipePlan plan;
            List<UsbItem> selected = new List<UsbItem>();
            lock (Gate)
            {
                string problem;
                plan = FindPlan(req, "clean", root, client, out problem);
                if (plan == null) return PipeProtocol.Error(req.Id, problem, "plan is not usable");
                List<object> raw = Json.AsArray(Json.Get(req.Args, "ids"));
                if (raw == null || raw.Count == 0)
                    return PipeProtocol.Error(req.Id, "bad-args", "ids is required");
                for (int i = 0; i < raw.Count; i++)
                {
                    int index;
                    if (!TryIndex(raw[i], plan.Items.Count, out index))
                        return PipeProtocol.Error(req.Id, "bad-args", "ids must be 1..items");
                    UsbItem item = plan.Items[index - 1];
                    bool dup = false;
                    for (int j = 0; j < selected.Count; j++)
                        if (ReferenceEquals(selected[j], item)) { dup = true; break; }
                    if (!dup) selected.Add(item);
                }
                if (selected.Count == 0)
                    return PipeProtocol.Error(req.Id, "bad-args", "ids is empty");
                Plans.Remove(plan.Id);
            }

            int failed, stopped;
            string batch;
            int done = UsbScan.Apply(plan.Scan, selected, out failed, out batch, out stopped, null);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["plan"] = plan.Id;
            r["drive"] = drive;
            r["done"] = done;
            r["failed"] = failed;
            r["stopped"] = stopped;
            r["batch"] = batch;
            Log.Write(S.Log.PipeApplied(plan.Id, "clean", drive, done, failed), "INFO");
            return Ok(req, r);
        }

        private static string UnhidePlan(PipeRequest req, PipeClientInfo client)
        {
            string drive, root, code, message;
            if (!TryDrive(req, out drive, out root, out code, out message))
                return PipeProtocol.Error(req.Id, code, message);
            string reason;
            if (Repair.RefuseReason(root, false, out reason))
                return PipeProtocol.Error(req.Id, "refused", reason);
            UnhideScope scope;
            if (!TryScope(req, out scope))
                return PipeProtocol.Error(req.Id, "bad-args", "scope must be virus or all");

            int count = UsbScan.CountHidden(root, scope, null);
            PipePlan plan = NewPlan("unhide", drive, root,
                client == null ? "" : client.UserSid,
                client == null ? 0 : client.SessionId);
            plan.Scope = scope;
            plan.Count = count;
            lock (Gate) StorePlanLocked(plan);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["plan"] = plan.Id;
            r["drive"] = drive;
            r["serial"] = (long)plan.Serial;
            r["count"] = count;
            Log.Write(S.Log.PipePlan(plan.Id, "unhide", drive, count), "INFO");
            return Ok(req, r);
        }

        private static string UnhideApply(PipeRequest req, PipeClientInfo client)
        {
            if (!Confirm(req)) return BadArgs(req);
            string drive, root, code, message;
            if (!TryDrive(req, out drive, out root, out code, out message))
                return PipeProtocol.Error(req.Id, code, message);
            string reason;
            if (Repair.RefuseReason(root, true, out reason))
                return PipeProtocol.Error(req.Id, "refused", reason);
            if (!Native.CanWrite(root))
                return PipeProtocol.Error(req.Id, "refused", "drive is read-only");
            PipePlan plan;
            lock (Gate)
            {
                string problem;
                plan = FindPlan(req, "unhide", root, client, out problem);
                if (plan == null) return PipeProtocol.Error(req.Id, problem, "plan is not usable");
                Plans.Remove(plan.Id);
            }
            int done = UsbScan.Unhide(root, plan.Scope, null);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["plan"] = plan.Id;
            r["drive"] = drive;
            r["done"] = done;
            Log.Write(S.Log.PipeApplied(plan.Id, "unhide", drive, done, 0), "INFO");
            return Ok(req, r);
        }

        private static string RenamePlan(PipeRequest req, PipeClientInfo client)
        {
            string drive, root, code, message;
            if (!TryDrive(req, out drive, out root, out code, out message))
                return PipeProtocol.Error(req.Id, code, message);
            string reason;
            if (Repair.RefuseReason(root, false, out reason))
                return PipeProtocol.Error(req.Id, "refused", reason);

            int count = UsbRestore.CountBase64(root, null);
            PipePlan plan = NewPlan("rename", drive, root,
                client == null ? "" : client.UserSid,
                client == null ? 0 : client.SessionId);
            plan.Count = count;
            lock (Gate) StorePlanLocked(plan);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["plan"] = plan.Id;
            r["drive"] = drive;
            r["serial"] = (long)plan.Serial;
            r["count"] = count;
            Log.Write(S.Log.PipePlan(plan.Id, "rename", drive, count), "INFO");
            return Ok(req, r);
        }

        private static string RenameApply(PipeRequest req, PipeClientInfo client)
        {
            if (!Confirm(req)) return BadArgs(req);
            string drive, root, code, message;
            if (!TryDrive(req, out drive, out root, out code, out message))
                return PipeProtocol.Error(req.Id, code, message);
            string reason;
            if (Repair.RefuseReason(root, true, out reason))
                return PipeProtocol.Error(req.Id, "refused", reason);
            if (!Native.CanWrite(root))
                return PipeProtocol.Error(req.Id, "refused", "drive is read-only");
            PipePlan plan;
            lock (Gate)
            {
                string problem;
                plan = FindPlan(req, "rename", root, client, out problem);
                if (plan == null) return PipeProtocol.Error(req.Id, problem, "plan is not usable");
                Plans.Remove(plan.Id);
            }
            int skipped;
            int done = UsbRestore.RenameBase64(root, null, null, out skipped);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["plan"] = plan.Id;
            r["drive"] = drive;
            r["done"] = done;
            r["skipped"] = skipped;
            Log.Write(S.Log.PipeApplied(plan.Id, "rename", drive, done, skipped), "INFO");
            return Ok(req, r);
        }

        // ---------------------------------------------------------------- phụ trợ

        private static bool Confirm(PipeRequest req)
        {
            return Json.Bool(req.Args, "confirm", false);
        }

        private static bool TryDrive(PipeRequest req, out string drive, out string root,
                                     out string code, out string message)
        {
            drive = "";
            root = "";
            code = "";
            message = "";
            object value = Json.Get(req.Args, "drive");
            string text = value as string;
            if (text == null)
            {
                code = "bad-args";
                message = "drive is required";
                return false;
            }
            text = text.Trim().ToUpperInvariant();
            if (text.Length != 1 || text[0] < 'A' || text[0] > 'Z')
            {
                code = "bad-args";
                message = "drive must be one letter, e.g. E";
                return false;
            }
            drive = text;
            root = text + ":\\";
            return true;
        }

        private static bool TryScope(PipeRequest req, out UnhideScope scope)
        {
            scope = UnhideScope.VirusOnly;
            object value = Json.Get(req.Args, "scope");
            if (value == null) return true;
            string text = value as string;
            if (text == null) return false;
            text = text.Trim().ToLowerInvariant();
            if (text == "virus") { scope = UnhideScope.VirusOnly; return true; }
            if (text == "all") { scope = UnhideScope.AllHidden; return true; }
            return false;
        }

        private static bool TryIndex(object raw, int count, out int index)
        {
            index = 0;
            if (raw == null) return false;
            double d;
            try { d = Convert.ToDouble(raw, CultureInfo.InvariantCulture); }
            catch { return false; }
            if (d != Math.Floor(d) || d < 1 || d > count) return false;
            index = (int)d;
            return true;
        }

        private static string NewId(string prefix)
        {
            return prefix + Guid.NewGuid().ToString("N").Substring(0, 10);
        }

        private static PipePlan NewPlan(string kind, string drive, string root,
                                        string sid, uint session)
        {
            PipePlan plan = new PipePlan();
            plan.Id = NewId("p");
            plan.Kind = kind;
            plan.Drive = drive;
            plan.Root = root;
            plan.Sid = sid ?? "";
            plan.Session = session;
            plan.CreatedUtc = DateTime.UtcNow;
            try { plan.Serial = Native.VolumeSerial(root); }
            catch { plan.Serial = 0; }
            return plan;
        }

        // Plan mới thay plan cũ: không giữ nhiều kế hoạch cùng lúc.
        private static void StorePlanLocked(PipePlan plan)
        {
            Plans.Clear();
            Plans[plan.Id] = plan;
        }

        private static PipePlan FindPlan(PipeRequest req, string kind, string root,
                                         PipeClientInfo client, out string problem)
        {
            problem = "";
            string id = Json.Str(req.Args, "plan", "");
            PipePlan plan;
            if (id.Length == 0 || !Plans.TryGetValue(id, out plan))
            {
                problem = "not-found";
                return null;
            }
            if (!string.Equals(plan.Kind, kind, StringComparison.Ordinal))
            {
                problem = "refused";
                return null;
            }
            problem = PlanProblem(plan, client == null ? "" : client.UserSid,
                client == null ? 0 : client.SessionId, root, DateTime.UtcNow);
            return problem.Length == 0 ? plan : null;
        }

        // Hàm thuần để self-test kiểm tra hết hạn/SID/session/ổ/serial.
        internal static string PlanProblem(PipePlan plan, string sid, uint session,
                                           string root, DateTime now)
        {
            if (plan == null) return "not-found";
            if ((now - plan.CreatedUtc).TotalSeconds > PlanTtlSeconds) return "expired";
            if (!string.Equals(plan.Sid, sid ?? "", StringComparison.OrdinalIgnoreCase))
                return "refused";
            if (plan.Session != session) return "refused";
            if (!string.Equals(plan.Root, root, StringComparison.OrdinalIgnoreCase))
                return "refused";
            uint serial;
            try { serial = Native.VolumeSerial(root); }
            catch { serial = 0; }
            if (serial != 0 && plan.Serial != 0 && serial != plan.Serial)
                return "refused";
            return "";
        }

        private static string KindText(UsbKind kind)
        {
            switch (kind)
            {
                case UsbKind.Shortcut: return "shortcut";
                case UsbKind.Script: return "script";
                case UsbKind.Autorun: return "autorun";
                case UsbKind.RootExe: return "root-exe";
                case UsbKind.HiddenFolder: return "hidden-folder";
                case UsbKind.FakeFolder: return "fake-folder";
                default: return "other";
            }
        }
    }
}
