using System;
using System.Collections.Generic;
using System.Linq;

namespace LinkDrop
{
    internal enum TransferDirection
    {
        Incoming,
        Outgoing
    }

    internal enum TransferState
    {
        Waiting,
        Transferring,
        Verifying,
        Completed,
        Failed
    }

    internal sealed class TransferItem
    {
        private readonly object _gate = new object();
        public readonly long Id;
        public readonly TransferDirection Direction;
        public readonly DateTime StartedAt;
        private string _name;
        private long _size;
        private long _bytes;
        private TransferState _state;
        private string _error;
        private string _savedPath;
        private double _speed;
        private string _mime;

        public TransferItem(long id, TransferDirection direction, string name, long size, string mime)
        {
            Id = id;
            Direction = direction;
            StartedAt = DateTime.Now;
            _name = name;
            _size = size;
            _mime = mime ?? "";
            _state = TransferState.Waiting;
        }

        public string Name { get { lock (_gate) return _name; } }
        public long Size { get { lock (_gate) return _size; } }
        public long Bytes { get { lock (_gate) return _bytes; } }
        public TransferState State { get { lock (_gate) return _state; } }
        public string Mime { get { lock (_gate) return _mime; } }

        public void SetState(TransferState state)
        {
            lock (_gate) _state = state;
        }

        public void SetSize(long size)
        {
            lock (_gate) _size = size;
        }

        public void SetProgress(long bytes, double speed)
        {
            lock (_gate)
            {
                _bytes = bytes;
                _speed = speed;
            }
        }

        public void Complete(string savedPath, long size)
        {
            lock (_gate)
            {
                _state = TransferState.Completed;
                _savedPath = savedPath;
                _bytes = size;
                _speed = 0;
            }
        }

        public void Fail(string message)
        {
            lock (_gate)
            {
                _state = TransferState.Failed;
                _error = message;
                _speed = 0;
            }
        }

        public TransferSnapshot Snapshot()
        {
            lock (_gate)
            {
                TransferSnapshot snapshot = new TransferSnapshot();
                snapshot.Id = Id;
                snapshot.Direction = Direction;
                snapshot.Name = _name;
                snapshot.Size = _size;
                snapshot.Bytes = _bytes;
                snapshot.State = _state;
                snapshot.Error = _error;
                snapshot.SavedPath = _savedPath;
                snapshot.Speed = _speed;
                snapshot.StartedAt = StartedAt;
                snapshot.Mime = _mime;
                return snapshot;
            }
        }
    }

    internal sealed class TransferSnapshot
    {
        public long Id;
        public TransferDirection Direction;
        public string Name;
        public long Size;
        public long Bytes;
        public TransferState State;
        public string Error;
        public string SavedPath;
        public double Speed;
        public DateTime StartedAt;
        public string Mime;

        public double Progress
        {
            get
            {
                if (Size <= 0) return State == TransferState.Completed ? 1 : 0;
                return Math.Max(0, Math.Min(1, Bytes / (double)Size));
            }
        }
    }

    internal sealed class TransferManager
    {
        private readonly object _gate = new object();
        private readonly Dictionary<long, TransferItem> _items = new Dictionary<long, TransferItem>();
        private readonly List<long> _order = new List<long>();

        public TransferItem Add(long id, TransferDirection direction, string name, long size, string mime)
        {
            TransferItem item = new TransferItem(id, direction, name, size, mime);
            lock (_gate)
            {
                _items[id] = item;
                _order.Add(id);
                Trim();
            }
            return item;
        }

        public List<TransferSnapshot> Snapshots()
        {
            lock (_gate)
            {
                List<TransferSnapshot> result = new List<TransferSnapshot>();
                for (int i = _order.Count - 1; i >= 0; i--)
                {
                    TransferItem item;
                    if (_items.TryGetValue(_order[i], out item)) result.Add(item.Snapshot());
                }
                return result;
            }
        }

        private void Trim()
        {
            while (_order.Count > 200)
            {
                long oldest = _order[0];
                _order.RemoveAt(0);
                _items.Remove(oldest);
            }
        }
    }
}
