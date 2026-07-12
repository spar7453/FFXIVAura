using System.Numerics;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class AuraRenderingLayoutTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("StatusIconRenderGeometry crops transparent status padding", CropsTransparentStatusPadding),
        ("AuraLayoutPolicy flows every standard aura role continuously", FlowsEveryStandardAuraRoleContinuously),
        ("AuraFlowReorder finds the nearest wrapped slot", FindsNearestWrappedSlot),
        ("AuraFlowReorder moves tracked statuses in both directions", MovesTrackedStatusesInBothDirections),
    ];

    private static void CropsTransparentStatusPadding()
    {
        Vector(Vector2.Zero, StatusIconRenderGeometry.UvMin);
        Vector(new Vector2(1f, 0.875f), StatusIconRenderGeometry.UvMax);
        Equal(28f, 32f * StatusIconRenderGeometry.UvMax.Y);
    }

    private static void FlowsEveryStandardAuraRoleContinuously()
    {
        True(AuraLayoutPolicy.UsesContinuousFlow(IconWindowRole.PlayerBuffs), "player buffs should flow continuously");
        True(AuraLayoutPolicy.UsesContinuousFlow(IconWindowRole.TargetDebuffs), "target debuffs should flow continuously");
        True(AuraLayoutPolicy.UsesContinuousFlow(IconWindowRole.PartyBuffs), "party buffs should flow continuously");
        True(!AuraLayoutPolicy.UsesContinuousFlow(IconWindowRole.SkillCooldowns), "skill positions should remain explicit");
        True(!AuraLayoutPolicy.UsesContinuousFlow(IconWindowRole.PartyDefensives), "party cooldown boards use their own layout");
    }

    private static void FindsNearestWrappedSlot()
    {
        var areaSize = new Vector2(100f, 100f);
        Equal(0, AuraFlowReorder.FindNearestVisibleIndex(IconAlignment.Left, new Vector2(20f, 20f), 3, areaSize, 40f, 5f));
        Equal(1, AuraFlowReorder.FindNearestVisibleIndex(IconAlignment.Left, new Vector2(65f, 20f), 3, areaSize, 40f, 5f));
        Equal(2, AuraFlowReorder.FindNearestVisibleIndex(IconAlignment.Left, new Vector2(20f, 65f), 3, areaSize, 40f, 5f));
    }

    private static void MovesTrackedStatusesInBothDirections()
    {
        var trackedStatusIds = new List<uint> { 10, 20, 30 };

        True(AuraFlowReorder.MoveTrackedStatus(trackedStatusIds, 10, 30), "the first status should move to the last slot");
        Sequence([20u, 30u, 10u], trackedStatusIds);

        True(AuraFlowReorder.MoveTrackedStatus(trackedStatusIds, 10, 20), "the last status should move to the first slot");
        Sequence([10u, 20u, 30u], trackedStatusIds);
        True(!AuraFlowReorder.MoveTrackedStatus(trackedStatusIds, 99, 20), "an unknown source should not change the order");
    }
}
