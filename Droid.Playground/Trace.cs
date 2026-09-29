using Hexa.NET.ImGui;
using Num = System.Numerics;

namespace Droid.Playground
{
    // The last few seconds of a value, a sample a tick, to plot in a panel: the way to see how something moves over
    // time (a wobble, an overshoot) rather than where it is now.
    public sealed class Trace
    {
        private readonly float[] _values;
        private int _next;

        public Trace(int length = 300) => _values = new float[length];   // 300 ticks: 5 seconds

        public float Latest => _values[(_next + _values.Length - 1) % _values.Length];

        public void Add(float value)
        {
            _values[_next] = value;
            _next = (_next + 1) % _values.Length;
        }

        public void Clear()
        {
            System.Array.Clear(_values);
            _next = 0;
        }

        // A line graph of it, oldest on the left, between `min` and `max`, with its latest value written over it.
        public unsafe void Plot(string label, float min, float max, string format = "{0:F2}")
        {
            fixed (float* values = _values)
                ImGui.PlotLines(label, values, _values.Length, _next, string.Format(format, Latest), min, max, new Num.Vector2(0f, 60f));
        }
    }
}
