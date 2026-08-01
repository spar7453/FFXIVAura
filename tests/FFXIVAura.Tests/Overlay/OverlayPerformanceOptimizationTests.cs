using System.Numerics;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class OverlayPerformanceOptimizationTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("IconRenderText formats cached cooldown and charge values", FormatsIconText),
        ("CooldownCoverGeometry returns bounded arc indices", ReturnsBoundedArcIndices),
        ("VisibleAbilityLayoutState detects layout changes", DetectsVisibleAbilityLayoutChanges),
        ("OverlayFrameBuffers clears and reuses lists", ClearsOverlayFrameBuffers),
        ("CooldownFrameKey compares ability ids without allocating normalized strings", ComparesCooldownFrameKeys),
        ("SkillLayoutScopeKey compares window and job without scoped strings", ComparesSkillLayoutScopeKeys),
        ("JobLevelKey compares job codes without normalized strings", ComparesJobLevelKeys),
        ("Party cooldown definition scope compares jobs without scoped strings", ComparesPartyCooldownDefinitionScopes),
        ("IconTextureService preserves alpha while converting grayscale", ConvertsIconPixelsToGrayscale),
    ];

    private static void FormatsIconText()
    {
        Equal("0", IconRenderText.FormatCooldown(float.NaN));
        Equal("0", IconRenderText.FormatCooldown(-3));
        Equal("4", IconRenderText.FormatCooldown(3.1f));
        Equal("3", IconRenderText.FormatCharge(3));
        Equal("1001", IconRenderText.FormatCharge(1001));
    }

    private static void ReturnsBoundedArcIndices()
    {
        Equal(0, CooldownCoverGeometry.GetStartIndex(-1));
        Equal(32, CooldownCoverGeometry.GetStartIndex(0.5f));
        Equal(63, CooldownCoverGeometry.GetStartIndex(0.99f));
        Equal(CooldownCoverGeometry.SegmentCount, CooldownCoverGeometry.GetStartIndex(2));
        Equal(CooldownCoverGeometry.SegmentCount + 1, CooldownCoverGeometry.UnitCirclePoints.Length);
    }

    private static void DetectsVisibleAbilityLayoutChanges()
    {
        AbilityDefinition[] visible =
        [
            new() { Id = "first" },
            new() { Id = "second" },
        ];
        var state = VisibleAbilityLayoutState.Create(
            100,
            visible,
            IconAlignment.Left,
            new Vector2(300, 100),
            40,
            3,
            autoAlignSuppressed: false,
            hasSavedPositions: true);

        True(state.Matches(100, visible, IconAlignment.Left, new Vector2(300, 100), 40, 3, false, true), "same layout should match");
        True(!state.Matches(90, visible, IconAlignment.Left, new Vector2(300, 100), 40, 3, false, true), "level change should invalidate");
        True(!state.Matches(100, visible.Reverse().ToArray(), IconAlignment.Left, new Vector2(300, 100), 40, 3, false, true), "order change should invalidate");
        True(!state.Matches(100, visible, IconAlignment.Right, new Vector2(300, 100), 40, 3, false, true), "alignment change should invalidate");
        True(!state.Matches(100, visible, IconAlignment.Left, new Vector2(300, 100), 40, 3, true, true), "suppression change should invalidate");
    }

    private static void ClearsOverlayFrameBuffers()
    {
        var buffers = new OverlayFrameBuffers();
        var layoutAbilities = buffers.LayoutAbilities;
        buffers.LayoutAbilities.Add(new AbilityDefinition { Id = "ability" });
        buffers.LayoutAuras.Add(default);

        buffers.Clear();

        Equal(0, buffers.LayoutAbilities.Count);
        Equal(0, buffers.LayoutAuras.Count);
        True(ReferenceEquals(layoutAbilities, buffers.LayoutAbilities), "clear should retain the reusable list");
    }

    private static void ComparesCooldownFrameKeys()
    {
        var first = new CooldownFrameKey("VPR-action", 123);
        var equivalent = new CooldownFrameKey("vpr-ACTION", 123);
        var differentAction = new CooldownFrameKey("VPR-action", 124);

        True(first == equivalent, "ability ids should compare case-insensitively");
        Equal(first.GetHashCode(), equivalent.GetHashCode());
        True(first != differentAction, "action id should remain part of the cache key");

        var values = new Dictionary<CooldownFrameKey, int> { [first] = 7 };
        True(values.TryGetValue(equivalent, out var value), "equivalent key should hit the dictionary cache");
        Equal(7, value);
    }

    private static void ComparesSkillLayoutScopeKeys()
    {
        var first = new SkillLayoutScopeKey("Window-1", "VPR");
        var equivalent = new SkillLayoutScopeKey("window-1", "vpr");
        var otherWindow = new SkillLayoutScopeKey("Window-2", "VPR");

        True(first == equivalent, "scope keys should compare case-insensitively");
        Equal(first.GetHashCode(), equivalent.GetHashCode());
        True(first.BelongsToWindow("WINDOW-1"), "scope should identify its window without parsing");
        True(first != otherWindow, "window id should remain part of the scope");
    }

    private static void ComparesJobLevelKeys()
    {
        var values = new Dictionary<JobLevelKey, int>
        {
            [new JobLevelKey("VPR", 100)] = 9,
        };

        True(values.TryGetValue(new JobLevelKey(" vpr ", 100), out var value), "job lookup should ignore case and surrounding whitespace");
        Equal(9, value);
        True(!values.ContainsKey(new JobLevelKey("VPR", 90)), "level should remain part of the cache key");
    }

    private static void ComparesPartyCooldownDefinitionScopes()
    {
        var first = new PartyCooldownDefinitionScopeKey(PartyCooldownCategory.Defensive, "PLD", 100);
        var equivalent = new PartyCooldownDefinitionScopeKey(PartyCooldownCategory.Defensive, " pld ", 100);
        var otherCategory = new PartyCooldownDefinitionScopeKey(PartyCooldownCategory.Healing, "PLD", 100);

        True(first.Equals(equivalent), "party cooldown scope jobs should ignore case and surrounding whitespace");
        Equal(first.GetHashCode(), equivalent.GetHashCode());
        True(!first.Equals(otherCategory), "category should remain part of the cache key");
    }

    private static void ConvertsIconPixelsToGrayscale()
    {
        byte[] pixels = [255, 0, 0, 123, 0, 255, 0, 231];

        IconTextureService.ConvertRgbaImageToGrayscale(pixels);

        Sequence<byte>([76, 76, 76, 123, 149, 149, 149, 231], pixels);
    }
}
