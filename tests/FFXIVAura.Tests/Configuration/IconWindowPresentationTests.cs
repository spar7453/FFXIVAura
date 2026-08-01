using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class IconWindowPresentationTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("IconWindowPresentation resolves display-name fallbacks", ResolvesDisplayNameFallbacks),
        ("IconWindowPresentation covers every window role", CoversEveryWindowRole),
        ("IconWindowPresentation provides role-specific condition labels", ProvidesRoleSpecificConditionLabels),
    ];

    private static void ResolvesDisplayNameFallbacks()
    {
        Equal(
            "win1",
            IconWindowPresentation.GetDisplayName(new IconWindowConfig
            {
                Id = "win1",
                Name = " ",
            }));
        Equal(
            "내 버프",
            IconWindowPresentation.GetDisplayName(new IconWindowConfig
            {
                Id = "win1",
                Name = "내 버프",
            }));
    }

    private static void CoversEveryWindowRole()
    {
        var options = IconWindowPresentation.RoleOptions;

        Sequence(
            Enum.GetValues<IconWindowRole>(),
            options.Select(option => option.Role).ToArray());
        True(
            options.All(option => !string.IsNullOrWhiteSpace(option.Label)),
            "every window role should have a visible label");
    }

    private static void ProvidesRoleSpecificConditionLabels()
    {
        var skillOptions = IconWindowPresentation.GetDisplayConditionOptions(
            IconWindowRole.SkillCooldowns);
        var auraOptions = IconWindowPresentation.GetDisplayConditionOptions(
            IconWindowRole.PlayerBuffs);
        var partyOptions = IconWindowPresentation.GetDisplayConditionOptions(
            IconWindowRole.PartyDefensives);

        Sequence(
            Enum.GetValues<IconDisplayCondition>(),
            skillOptions.Select(option => option.Condition).ToArray());
        Equal("쿨/불가", skillOptions.Single(option =>
            option.Condition == IconDisplayCondition.CoolingOnly).Label);
        Equal("사용 가능", skillOptions.Single(option =>
            option.Condition == IconDisplayCondition.ReadyOnly).Label);
        Equal("활성", auraOptions.Single(option =>
            option.Condition == IconDisplayCondition.CoolingOnly).Label);
        Equal("없음", auraOptions.Single(option =>
            option.Condition == IconDisplayCondition.ReadyOnly).Label);
        Equal("사용 중/쿨", partyOptions.Single(option =>
            option.Condition == IconDisplayCondition.CoolingOnly).Label);
        Equal("사용 가능", partyOptions.Single(option =>
            option.Condition == IconDisplayCondition.ReadyOnly).Label);
        Sequence(
            Enum.GetValues<IconDisplayCondition>(),
            partyOptions.Select(option => option.Condition).ToArray());

        foreach (var role in Enum.GetValues<IconWindowRole>().Where(IconWindowRoles.IsPartyCooldownRole))
        {
            Sequence(
                partyOptions,
                IconWindowPresentation.GetDisplayConditionOptions(role));
        }
    }
}
