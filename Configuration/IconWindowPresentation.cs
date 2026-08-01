namespace FFXIVAura;

internal readonly record struct IconWindowRoleOption(
    IconWindowRole Role,
    string Label);

internal readonly record struct IconDisplayConditionOption(
    IconDisplayCondition Condition,
    string Label);

internal static class IconWindowPresentation
{
    public static IReadOnlyList<IconWindowRoleOption> RoleOptions { get; } =
    [
        new(IconWindowRole.SkillCooldowns, "스킬"),
        new(IconWindowRole.PlayerBuffs, "내 버프"),
        new(IconWindowRole.TargetDebuffs, "대상 디버프"),
        new(IconWindowRole.PartyBuffs, "파티 버프"),
        new(IconWindowRole.PartyDefensives, "파티 생존기"),
        new(IconWindowRole.PartyHealingCooldowns, "파티 힐쿨"),
        new(IconWindowRole.PartySynergies, "파티 딜 시너지"),
    ];

    private static IReadOnlyList<IconDisplayConditionOption> SkillDisplayConditionOptions { get; } =
    [
        new(IconDisplayCondition.Always, "항상"),
        new(IconDisplayCondition.InCombat, "전투 중"),
        new(IconDisplayCondition.OutOfCombat, "비전투"),
        new(IconDisplayCondition.CoolingOnly, "쿨/불가"),
        new(IconDisplayCondition.ReadyOnly, "사용 가능"),
    ];

    private static IReadOnlyList<IconDisplayConditionOption> AuraDisplayConditionOptions { get; } =
    [
        new(IconDisplayCondition.Always, "항상"),
        new(IconDisplayCondition.InCombat, "전투 중"),
        new(IconDisplayCondition.OutOfCombat, "비전투"),
        new(IconDisplayCondition.CoolingOnly, "활성"),
        new(IconDisplayCondition.ReadyOnly, "없음"),
    ];

    private static IReadOnlyList<IconDisplayConditionOption> PartyCooldownDisplayConditionOptions { get; } =
    [
        new(IconDisplayCondition.Always, "항상"),
        new(IconDisplayCondition.InCombat, "전투 중"),
        new(IconDisplayCondition.OutOfCombat, "비전투"),
        new(IconDisplayCondition.CoolingOnly, "사용 중/쿨"),
        new(IconDisplayCondition.ReadyOnly, "사용 가능"),
    ];

    public static string GetDisplayName(IconWindowConfig window)
        => string.IsNullOrWhiteSpace(window.Name) ? window.Id : window.Name;

    public static IReadOnlyList<IconDisplayConditionOption> GetDisplayConditionOptions(
        IconWindowRole role)
    {
        if (role == IconWindowRole.SkillCooldowns)
            return SkillDisplayConditionOptions;

        return IconWindowRoles.IsPartyCooldownRole(role)
            ? PartyCooldownDisplayConditionOptions
            : AuraDisplayConditionOptions;
    }
}
