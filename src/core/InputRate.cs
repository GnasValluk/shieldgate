// ShieldGate - do toc do go theo tung thiet bi. Chi dem su kien va moc thoi
// gian; khong nhan, khong luu ma phim. Logic tach khoi P/Invoke de self-test.
using System;

namespace ShieldGate
{
    internal sealed class InputBurst
    {
        // Nhan da loc (VID/PID), khong phai duong dan raw co the chua serial.
        public string Device = "";
        // Duong dan raw chi de ghep voi kho thiet bi; khong ghi log, khong luu dia.
        public string Raw = "";
        public DateTime First;
        public DateTime Last;
        public int Events;
        public double PeakPerSecond;

        public double Seconds
        {
            get
            {
                double s = (Last - First).TotalSeconds;
                return s > 0 ? s : 0;
            }
        }
    }

    internal sealed class InputRate
    {
        // 50 bucket x 100 ms = 5 giay; cua so tinh toc do la 20 bucket = 2 giay.
        public const int Buckets = 50;
        public const int WindowBuckets = 20;
        // Nguong chi de phat hien burst; W22 moi quyet dinh co canh bao hay khong.
        public const int MinEvents = 30;
        public const double MinPerSecond = 15.0;

        private readonly string _device;
        private readonly long[] _counts = new long[Buckets];
        private readonly long[] _slots = new long[Buckets];
        private long _base;
        private bool _armed;
        private DateTime _first;
        private DateTime _last;
        private int _events;
        private double _peak;

        public InputRate(string device)
        {
            _device = device == null ? "" : device;
        }

        public bool Crossed
        {
            get { return _events >= MinEvents && _peak >= MinPerSecond; }
        }

        // when nen la gio he thong (DateTime.UtcNow). Dong ho lui hoac nghi qua
        // 5 giay deu bat dau cua so moi, tranh cong don su kien cu.
        public void Add(DateTime when)
        {
            long slot = when.Ticks / (TimeSpan.TicksPerMillisecond * 100);
            if (!_armed)
            {
                Clear();
                _armed = true;
                _base = slot;
                _first = when;
            }
            else
            {
                long delta = slot - _base;
                if (delta < 0)
                {
                    Clear();
                    _base = slot;
                    _first = when;
                }
                else if (delta >= Buckets)
                {
                    Clear();
                    _base = slot - Buckets + 1;
                    _first = when;
                }
            }

            int index = (int)((slot - _base) % Buckets);
            if (_slots[index] != slot)
            {
                _slots[index] = slot;
                _counts[index] = 0;
            }
            _counts[index]++;
            _events++;
            _last = when;

            double current = CountWindow(slot) * 1000.0 / (WindowBuckets * 100);
            if (current > _peak) _peak = current;
        }

        private long CountWindow(long slot)
        {
            long sum = 0;
            for (int i = 0; i < WindowBuckets; i++)
            {
                long s = slot - i;
                if (s < _base) break;
                int index = (int)((s - _base) % Buckets);
                if (_slots[index] == s) sum += _counts[index];
            }
            return sum;
        }

        public InputBurst Snapshot()
        {
            InputBurst burst = new InputBurst();
            burst.Device = _device;
            burst.First = _first;
            burst.Last = _last;
            burst.Events = _events;
            burst.PeakPerSecond = _peak;
            return burst;
        }

        public void Reset()
        {
            Clear();
            _armed = false;
        }

        private void Clear()
        {
            for (int i = 0; i < Buckets; i++)
            {
                _counts[i] = 0;
                _slots[i] = long.MinValue;
            }
            _events = 0;
            _peak = 0;
            _first = DateTime.MinValue;
            _last = DateTime.MinValue;
        }
    }
}
