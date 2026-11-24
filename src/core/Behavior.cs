// ShieldGate 2.0 - giam sat hanh vi tren o roi: mot lan ghi hang tram tep hoac
// mot loat .lnk/script trong vai giay la dau hieu virus dang lay lan.
// Chi bao de bao ve nen quet lai; khong tu xoa tep nao o day.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ShieldGate
{
    internal sealed class BehaviorMonitor : IDisposable
    {
        private sealed class Hit
        {
            public DateTime When;
            public string Drive;
            public string Name;
        }

        private readonly object _gate = new object();
        private readonly Dictionary<string, FileSystemWatcher> _watchers =
            new Dictionary<string, FileSystemWatcher>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Hit> _hits = new List<Hit>();
        private readonly Dictionary<string, DateTime> _lastAlert =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Timer _timer;
        private bool _stopped;

        public BehaviorMonitor()
        {
            _timer = new Timer(Tick, null, 2000, 2000);
        }

        // Bao mot lan cho moi o trong khoang 10 phut (cau hinh) de khong dong tin nhan.
        public event Action<string> Suspicious;

        public void Update(IList<string> drives)
        {
            if (_stopped) return;
            HashSet<string> want = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (drives != null)
                foreach (string d in drives)
                    if (!string.IsNullOrEmpty(d)) want.Add(d);

            lock (_gate)
            {
                List<string> remove = new List<string>();
                foreach (KeyValuePair<string, FileSystemWatcher> kv in _watchers)
                    if (!want.Contains(kv.Key)) remove.Add(kv.Key);
                foreach (string key in remove)
                {
                    try { _watchers[key].Dispose(); } catch { }
                    _watchers.Remove(key);
                }

                foreach (string drive in want)
                {
                    if (_watchers.ContainsKey(drive)) continue;
                    try { _watchers[drive] = CreateWatcher(drive); }
                    catch { }
                }
            }
        }

        private FileSystemWatcher CreateWatcher(string drive)
        {
            FileSystemWatcher w = new FileSystemWatcher(drive);
            w.IncludeSubdirectories = true;
            w.InternalBufferSize = 64 * 1024;
            w.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
            string captured = drive;
            w.Created += delegate(object s, FileSystemEventArgs e) { Add(captured, e.FullPath); };
            w.Renamed += delegate(object s, RenamedEventArgs e) { Add(captured, e.FullPath); };
            w.Error += delegate(object s, ErrorEventArgs e)
            {
                Log.Write(S.Log.BehaviorWatcherError(captured), "WARN");
            };
            w.EnableRaisingEvents = true;
            return w;
        }

        private void Add(string drive, string path)
        {
            if (_stopped || string.IsNullOrEmpty(path)) return;
            try
            {
                lock (_gate)
                {
                    Hit h = new Hit();
                    h.When = DateTime.Now;
                    h.Drive = drive;
                    h.Name = Path.GetFileName(path);
                    _hits.Add(h);
                    // Giới hạn bộ nhớ khi ổ bị ghi liên tục.
                    if (_hits.Count > 4000) _hits.RemoveRange(0, 2000);
                }
            }
            catch { }
        }

        private void Tick(object state)
        {
            if (_stopped) return;
            try
            {
                RulesData rules = Rules.Current;
                int window = Math.Max(3, rules.BehaviorWindowSeconds);
                DateTime cutoff = DateTime.Now.AddSeconds(-window);
                Dictionary<string, int> total = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, int> risky = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                lock (_gate)
                {
                    int keep = 0;
                    for (int i = 0; i < _hits.Count; i++)
                    {
                        Hit h = _hits[i];
                        if (h.When < cutoff) continue;
                        if (keep != i) _hits[keep] = h;
                        keep++;

                        int n;
                        total.TryGetValue(h.Drive, out n);
                        total[h.Drive] = n + 1;

                        string ext = "";
                        try { ext = Path.GetExtension(h.Name); }
                        catch { }
                        if (rules.IsBurstRiskExt(ext))
                        {
                            risky.TryGetValue(h.Drive, out n);
                            risky[h.Drive] = n + 1;
                        }
                    }
                    if (keep < _hits.Count) _hits.RemoveRange(keep, _hits.Count - keep);
                }

                foreach (KeyValuePair<string, int> kv in total)
                {
                    int n;
                    risky.TryGetValue(kv.Key, out n);
                    if (!ShouldAlert(kv.Value, n, rules)) continue;

                    DateTime last;
                    if (_lastAlert.TryGetValue(kv.Key, out last) &&
                        !CooldownElapsed(last, DateTime.Now, rules.BehaviorAlertSeconds)) continue;
                    _lastAlert[kv.Key] = DateTime.Now;

                    string detail = S.Events.BehaviorBurst(kv.Value, n, window);
                    Log.Write(S.Log.BehaviorBurst(kv.Key, kv.Value, n), "WARN");
                    Events.Add(Events.KindBehavior, "WARN", kv.Key, detail);

                    Action<string> handler = Suspicious;
                    if (handler != null)
                    {
                        try { handler(kv.Key); }
                        catch { }
                    }
                }
            }
            catch (Exception e)
            {
                try { Log.Write(S.Log.BehaviorWatcherError(e.Message), "WARN"); }
                catch { }
            }
        }

        // Thuan tuy de self-test: tai lieu hang loat khong duoc kich hoat quet,
        // chi .lnk/script/payload moi dang ngo.
        internal static bool ShouldAlert(int total, int risky, RulesData rules)
        {
            if (rules == null) return false;
            return risky >= rules.BehaviorLnkBurst ||
                   (risky > 0 && total >= rules.BehaviorWriteBurst);
        }

        internal static bool CooldownElapsed(DateTime last, DateTime now, int seconds)
        {
            return (now - last).TotalSeconds >= seconds;
        }

        public void Stop()
        {
            if (_stopped) return;
            _stopped = true;
            try { _timer.Dispose(); } catch { }
            lock (_gate)
            {
                foreach (FileSystemWatcher w in _watchers.Values)
                {
                    try { w.EnableRaisingEvents = false; } catch { }
                    try { w.Dispose(); } catch { }
                }
                _watchers.Clear();
                _hits.Clear();
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
