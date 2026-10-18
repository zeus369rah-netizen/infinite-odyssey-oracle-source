using System;

namespace OdysseyOracle.Diagnostics
{
    // # ARCHITECTURE SNAPSHOT
    // World distances are metres, frame intervals are seconds, receipts use milliseconds.
    // Pure scalar/array math owns no Unity objects or native resources. Receipt summarization
    // allocates one sorted scratch array after measurement; frame ingestion is caller-owned.
    [Serializable]
    public struct TourFrameSummary
    {
        public int Samples, RejectedIntervals;
        public double MeanMs, P50Ms, P95Ms, P99Ms, WorstMs;
        public double FractionOver16_67Ms, FractionOver33_33Ms;
    }

    public static class OracleNativeTourMath
    {
        public static TourFrameSummary Summarize(float[] seconds, int count)
        {
            if (seconds == null) throw new ArgumentNullException(nameof(seconds));
            if (count < 0 || count > seconds.Length) throw new ArgumentOutOfRangeException(nameof(count));
            var sorted = new float[count];
            int valid = 0, over60 = 0, over30 = 0;
            double total = 0d;
            for (int i = 0; i < count; i++)
            {
                float value = seconds[i];
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f) continue;
                sorted[valid++] = value;
                total += value;
                if (value > 1d / 60d) over60++;
                if (value > 1d / 30d) over30++;
            }
            if (valid == 0) return new TourFrameSummary { RejectedIntervals = count };
            Array.Sort(sorted, 0, valid);
            return new TourFrameSummary {
                Samples = valid, RejectedIntervals = count - valid, MeanMs = total * 1000d / valid,
                P50Ms = Rank(sorted, valid, .5d) * 1000d,
                P95Ms = Rank(sorted, valid, .95d) * 1000d,
                P99Ms = Rank(sorted, valid, .99d) * 1000d,
                WorstMs = sorted[valid - 1] * 1000d,
                FractionOver16_67Ms = (double)over60 / valid,
                FractionOver33_33Ms = (double)over30 / valid
            };
        }

        public static double LegDeadlineSeconds(double distanceMetres, double speedMetresPerSecond)
        {
            if (double.IsNaN(distanceMetres) || double.IsInfinity(distanceMetres) || distanceMetres < 0d)
                throw new ArgumentOutOfRangeException(nameof(distanceMetres));
            if (double.IsNaN(speedMetresPerSecond) || double.IsInfinity(speedMetresPerSecond) || speedMetresPerSecond <= 0d)
                throw new ArgumentOutOfRangeException(nameof(speedMetresPerSecond));
            return Math.Min(35d, Math.Max(2d, distanceMetres / speedMetresPerSecond * 1.8d + 1d));
        }

        private static float Rank(float[] sorted, int count, double quantile) =>
            sorted[Math.Max(0, Math.Min(count - 1, (int)Math.Ceiling(count * quantile) - 1))];
    }
}
