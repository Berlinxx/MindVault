using System;

namespace mindvault.Srs
{
    /// <summary>
    /// Configurable pacing options for cram mode to allow dynamic difficulty.
    /// </summary>
    public class CramModeOptions
    {
        public TimeSpan InitialInterval { get; set; } = TimeSpan.Zero; // immediate due by default
        public double InitialEase { get; set; } = 2.5; // baseline ease
        public double EaseIncrement { get; set; } = 0.1; // growth per successful answer
        public double IntervalGrowthMultiplier { get; set; } = 1.0; // 1.0 keeps interval static unless randomization alters it
        public double IntervalRandomLow { get; set; } = 0.9; // lower bound for random factor
        public double IntervalRandomHigh { get; set; } = 1.2; // upper bound for random factor
        public double CorrectCooldownSeconds { get; set; } = 3; // cooldown after success
        public double FailCooldownSeconds { get; set; } = 10; // cooldown after failure
        public TimeSpan OnFailInterval { get; set; } = TimeSpan.FromMinutes(1); // interval after failure resets

        public static CramModeOptions Default => new();

        public static CramModeOptions Fast => new()
        {
            CorrectCooldownSeconds = 2,
            FailCooldownSeconds = 6,
            IntervalRandomLow = 0.85,
            IntervalRandomHigh = 1.15,
            IntervalGrowthMultiplier = 0.95,
            EaseIncrement = 0.08
        };

        public static CramModeOptions Moderate => new()
        {
            InitialInterval = TimeSpan.FromMinutes(1),
            IntervalGrowthMultiplier = 1.1,
            IntervalRandomLow = 0.9,
            IntervalRandomHigh = 1.25,
            EaseIncrement = 0.12
        };

        public static CramModeOptions Intensive => new()
        {
            CorrectCooldownSeconds = 1.5,
            FailCooldownSeconds = 8,
            IntervalGrowthMultiplier = 0.9,
            IntervalRandomLow = 0.8,
            IntervalRandomHigh = 1.2,
            EaseIncrement = 0.06,
            OnFailInterval = TimeSpan.FromSeconds(30)
        };
    }
}
