using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class AuraStatusGroupTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("AuraStatusGroups groups exact same names and preserves tracked order", GroupsExactNamesAndPreservesOrder),
        ("AuraStatusGroups keeps different icons and categories separate", KeepsDifferentIconsAndCategoriesSeparate),
        ("AuraStatusGroups keeps exact aliases as separate icons", KeepsExactAliasesSeparate),
        ("AuraStatusGroupStateBuilder combines aliases and keeps active identity", CombinesAliasesAndKeepsActiveIdentity),
        ("AuraStatusGroupStateBuilder keeps a stable missing identity", KeepsStableMissingIdentity),
        ("AuraStatusGroupCacheEntry invalidates changed index generations", InvalidatesChangedIndexGenerations),
    ];

    private static void GroupsExactNamesAndPreservesOrder()
    {
        var definitions = new Dictionary<uint, AuraStatusDefinition>
        {
            [82] = new("천하무적", 100, 1),
            [1302] = new("천하무적", 100, 1),
            [2287] = new("진 천하무적", 200, 1),
            [2794] = new("진 천하무적", 200, 1),
        };
        var idsByGroup = new Dictionary<AuraStatusGroupKey, IReadOnlyList<uint>>
        {
            [definitions[82].GroupKey] = new uint[] { 82, 1302 },
            [definitions[2287].GroupKey] = new uint[] { 2287, 2794 },
        };

        var groups = AuraStatusGroups.Build(
            new uint[] { 1302, 82, 2287 },
            Array.Empty<uint>(),
            id => definitions[id],
            key => idsByGroup.TryGetValue(key, out var ids) ? ids : Array.Empty<uint>());

        Equal(2, groups.Count);
        Equal(1302u, groups[0].StatusId);
        Equal("천하무적", groups[0].Name);
        Sequence([82u, 1302u], groups[0].MemberStatusIds);
        Equal(2287u, groups[1].StatusId);
        Sequence([2287u, 2794u], groups[1].MemberStatusIds);
    }

    private static void KeepsDifferentIconsAndCategoriesSeparate()
    {
        var definitions = new Dictionary<uint, AuraStatusDefinition>
        {
            [1] = new("감전", 100, 2),
            [2] = new("감전", 200, 2),
            [3] = new("감전", 100, 1),
        };

        var groups = AuraStatusGroups.Build(
            new uint[] { 1, 2, 3 },
            Array.Empty<uint>(),
            id => definitions[id],
            _ => Array.Empty<uint>());

        Equal(3, groups.Count);
        Equal(1u, groups[0].StatusId);
        Equal(2u, groups[1].StatusId);
        Equal(3u, groups[2].StatusId);
    }

    private static void KeepsExactAliasesSeparate()
    {
        var definitions = new Dictionary<uint, AuraStatusDefinition>
        {
            [82] = new("천하무적", 100, 1),
            [1302] = new("천하무적", 100, 1),
        };
        var key = definitions[82].GroupKey;

        var groups = AuraStatusGroups.Build(
            new uint[] { 82, 1302 },
            new uint[] { 82, 1302 },
            id => definitions[id],
            _ => new uint[] { 82, 1302 });

        Equal(2, groups.Count);
        True(groups[0].IsExact, "first exact status should stay separate");
        True(groups[1].IsExact, "second exact status should stay separate");
        Sequence([82u], groups[0].MemberStatusIds);
        Sequence([1302u], groups[1].MemberStatusIds);
        True(groups.All(group => group.Key == key), "exact entries should retain their shared identity metadata");
    }

    private static void CombinesAliasesAndKeepsActiveIdentity()
    {
        var key = AuraStatusGroupKey.Create("천하무적", 100, 1);
        var group = new AuraStatusGroup(82, "천하무적", 100, key, false, new uint[] { 82, 1302 });
        var builder = new AuraStatusGroupStateBuilder(group);
        builder.Add(new AuraStatusGroupActiveState(1302, 100, 4f, 1, 2, 0, false));
        builder.Add(new AuraStatusGroupActiveState(82, 100, 8f, 2, 3, 1, true));

        var state = builder.Build();

        Equal(82u, state.StatusId);
        Equal(82u, state.ActiveStatusId);
        Equal(82u, state.TooltipStatusId);
        Equal(100u, state.IconId);
        Near(8f, state.Remaining);
        Equal(5, state.Count);
        Equal(1, state.OwnCount);
        True(state.Present, "one active alias should make the group active");
        True(state.FromSelf, "self ownership should aggregate across aliases");
    }

    private static void KeepsStableMissingIdentity()
    {
        var key = AuraStatusGroupKey.Create("천하무적", 100, 1);
        var group = new AuraStatusGroup(1302, "천하무적", 100, key, false, new uint[] { 82, 1302 });
        var state = new AuraStatusGroupStateBuilder(group).Build();

        Equal(1302u, state.StatusId);
        Equal(0u, state.ActiveStatusId);
        Equal(1302u, state.TooltipStatusId);
        Equal(100u, state.IconId);
        True(!state.Present, "a group without active aliases should be missing");
    }

    private static void InvalidatesChangedIndexGenerations()
    {
        var key = AuraStatusGroupKey.Create("천하무적", 100, 1);
        var group = new AuraStatusGroup(82, "천하무적", 100, key, false, new uint[] { 82, 1302 });
        var cache = new AuraStatusGroupCacheEntry(new uint[] { 82 }, Array.Empty<uint>(), 1, new[] { group });

        True(cache.Matches(new uint[] { 82 }, Array.Empty<uint>(), 1), "unchanged tracked IDs and generation should reuse cache");
        True(!cache.Matches(new uint[] { 82 }, Array.Empty<uint>(), 2), "new status index generation should rebuild groups");
        True(!cache.Matches(new uint[] { 1302 }, Array.Empty<uint>(), 1), "changed representative ID should rebuild groups");
        True(!cache.Matches(new uint[] { 82 }, new uint[] { 82 }, 1), "changed exact tracking mode should rebuild groups");
    }
}
