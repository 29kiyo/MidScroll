namespace MidScroll
{
    // フェーズ2時点では既定値のみ。JSON永続化はフェーズ3で実装する。
    internal sealed class AppSettings
    {
        public int LongPressThresholdMs { get; set; } = 300;
        public int PollingIntervalMs { get; set; } = 30;
        public int DeadZonePixels { get; set; } = 10;
        public int MaxSpeedRangePixels { get; set; } = 200;
        public double MinWheelStepPerTick { get; set; } = 6.0;
        public double MaxWheelStepPerTick { get; set; } = 60.0;
        public double WheelSpeedMultiplier { get; set; } = 1.0;
        public bool Enabled { get; set; } = true;

        public static AppSettings Default => new AppSettings();
    }
}
