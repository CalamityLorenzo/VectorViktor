using System;
using System.Collections.Generic;

namespace World.Core.Animation
{
    // A value at a moment, and how it's reached from the key before it.
    public readonly record struct Key<T>(float Time, T Value, Ease Ease);

    // One value of a part (where it is, how it's turned, or its size) keyed over time. Before its first key it
    // holds that key's value, and after its last, the last's. Keys go in in time order; two at the same time
    // make a jump, the value arriving at the first and leaving from the second.
    public sealed class Channel<T> where T : struct
    {
        private readonly List<Key<T>> _keys = new List<Key<T>>();
        private readonly Func<T, T, float, T> _lerp;

        public Channel(Func<T, T, float, T> lerp) => _lerp = lerp;

        public IReadOnlyList<Key<T>> Keys => _keys;
        public bool IsEmpty => _keys.Count == 0;
        public float Duration => _keys.Count == 0 ? 0f : _keys[^1].Time;

        public void Add(float time, T value, Ease ease)
        {
            if (!float.IsFinite(time) || time < 0f)
                throw new ArgumentOutOfRangeException(nameof(time), time, "A key's time must be zero or more.");
            if (_keys.Count > 0 && time < _keys[^1].Time)
                throw new ArgumentException($"Keys go in in time order: {time} comes before the last key's {_keys[^1].Time}.", nameof(time));
            _keys.Add(new Key<T>(time, value, ease));
        }

        public T Sample(float time)
        {
            if (_keys.Count == 0)
                throw new InvalidOperationException("A channel with no keys has no value.");
            if (time <= _keys[0].Time)
                return _keys[0].Value;
            if (time >= _keys[^1].Time)
                return _keys[^1].Value;

            // The last key at or before `time`, so the one after it is later still
            int low = 0, high = _keys.Count - 1;
            while (high - low > 1)
            {
                var middle = (low + high) / 2;
                if (_keys[middle].Time <= time)
                    low = middle;
                else
                    high = middle;
            }
            var (from, to) = (_keys[low], _keys[high]);
            var along = (time - from.Time) / (to.Time - from.Time);
            return _lerp(from.Value, to.Value, to.Ease.Apply(along));
        }
    }
}
