namespace FFXIVAura;

internal static class PerformanceProfileConfigPolicy
{
    public const int MinRecordIntervalSeconds =
        PerformanceProfileRecordingCoordinator.MinRecordIntervalSeconds;
    public const int MaxRecordIntervalSeconds =
        PerformanceProfileRecordingCoordinator.MaxRecordIntervalSeconds;
    public const int DefaultRecordIntervalSeconds = 1;
    public const int MinFileMegabytes = 1;
    public const int MaxFileMegabytes = 1024;
    public const int DefaultFileMegabytes = 64;

    public static int ClampRecordInterval(int value)
        => Math.Clamp(value, MinRecordIntervalSeconds, MaxRecordIntervalSeconds);

    public static int NormalizeRecordInterval(int value)
        => value <= 0 ? DefaultRecordIntervalSeconds : ClampRecordInterval(value);

    public static int ClampMaxFileMegabytes(int value)
        => Math.Clamp(value, MinFileMegabytes, MaxFileMegabytes);

    public static int NormalizeMaxFileMegabytes(int value)
        => value <= 0 ? DefaultFileMegabytes : ClampMaxFileMegabytes(value);
}
