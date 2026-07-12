namespace FFXIVAura;

internal enum PerformanceProfileSection
{
    Overlay,
    FrameModel,
    Positioning,
    Cooldown,
    AbilityIcon,
    AuraIcon,
    AuraScan,
    KeybindRebuild,
    TooltipRendering,
    GrayscaleProcessing,
    ConfigSave,
    AbilityCandidateBuild,
    AuraIndexBuild,
    AuraSearch,
    PartyRoster,
    PartyRows,
    Count,
}

internal readonly record struct PerformanceProfileSectionSnapshot(
    PerformanceProfileSection Section,
    string Label,
    double LastMilliseconds,
    double AverageMilliseconds,
    double RecentAverageMilliseconds,
    double MaxMilliseconds,
    DateTime MaxOccurredAtUtc,
    int LastCallCount,
    long TotalCallCount,
    int SampleFrameCount);

internal readonly record struct PerformanceProfileWindowSnapshot(
    string Id,
    string Label,
    double LastMilliseconds,
    double AverageMilliseconds,
    double RecentAverageMilliseconds,
    double MaxMilliseconds,
    DateTime MaxOccurredAtUtc,
    int LastCallCount,
    long TotalCallCount,
    int SampleFrameCount);
