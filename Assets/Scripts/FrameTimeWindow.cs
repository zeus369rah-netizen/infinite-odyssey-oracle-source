using System;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Fixed managed float rings/scratch; pure reductions and no Unity Objects or native ownership.
    // Push allocates nothing; bounded sort runs only when a main-thread snapshot is requested.
    public struct FrameTimeSnapshot
    {
        public int Count, OverBudgetFrames;
        public double WindowSeconds, AverageMs, P95Ms, MinimumMs, MaximumMs, FramesPerSecond;
        public long IgnoredIntervals;
    }

    /// <summary>A fixed240-frame rolling window; reductions reuse a separate fixed scratch array.</summary>
    public sealed class FrameTimeWindow
    {
        public const int Capacity = 240;
        private readonly float[] frames = new float[Capacity], sorted = new float[Capacity];
        private int head, count;
        private long ignored;
        public int Count => count;

        public bool Push(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f || seconds > 5f)
            { ignored++; return false; }
            frames[head] = seconds * 1000f;
            head = (head + 1) % Capacity;
            if (count < Capacity) count++;
            return true;
        }

        public FrameTimeSnapshot Snapshot(double budgetMs = 0d)
        {
            var result = new FrameTimeSnapshot { Count = count, IgnoredIntervals = ignored };
            if (count == 0) return result;
            double total = 0d;
            bool compareBudget = !double.IsNaN(budgetMs) && !double.IsInfinity(budgetMs) && budgetMs > 0d;
            for (int i = 0; i < count; i++)
            {
                float value = frames[i];
                sorted[i] = value;
                total += value;
                if (compareBudget && value > budgetMs * 1.08d) result.OverBudgetFrames++;
            }
            Array.Sort(sorted, 0, count);
            result.WindowSeconds = total / 1000d;
            result.AverageMs = total / count;
            result.FramesPerSecond = 1000d / result.AverageMs;
            result.MinimumMs = sorted[0];
            result.MaximumMs = sorted[count - 1];
            result.P95Ms = sorted[Math.Max(0, (int)Math.Ceiling(count * .95d) - 1)];
            return result;
        }

        public void Clear() { head = count = 0; ignored = 0; }
    }
}
