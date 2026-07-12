namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private bool TryFindPartyCooldownMemberByOwnedObjectName(
        string sourceName,
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        out PartyCooldownMemberSnapshot member,
        out string detail,
        out PartyCooldownIgnoredLogReason ignoredReason)
    {
        var normalizedSourceName = PartyCooldownLogMatcher.NormalizeActorName(sourceName);
        if (normalizedSourceName.Length == 0)
        {
            member = default;
            detail = "소환수/객체 이름이 비어 있습니다.";
            ignoredReason = PartyCooldownIgnoredLogReason.OwnerNotFound;
            return false;
        }

        if (!this.TryResolvePartyCooldownOwnedObjectOwner(
                normalizedSourceName,
                displayMembers,
                out var ownerMatch,
                out detail))
        {
            member = default;
            ignoredReason = PartyCooldownIgnoredLogReason.OwnerNotFound;
            return false;
        }

        return TryApplyPartyCooldownOwnedObjectOwnerMatch(
            ownerMatch,
            displayMembers,
            out member,
            out detail,
            out ignoredReason);
    }

    private bool TryResolvePartyCooldownOwnedObjectOwner(
        string normalizedSourceName,
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        out PartyCooldownOwnedObjectOwnerMatch ownerMatch,
        out string detail)
    {
        var matchingOwnerEntityIds = this.partyCooldownOwnedObjectOwnerIdsBuffer;
        var partyMemberEntityIds = this.partyCooldownOwnedObjectPartyEntityIdsBuffer;
        matchingOwnerEntityIds.Clear();
        partyMemberEntityIds.Clear();

        try
        {
            AddPartyCooldownMemberEntityIds(displayMembers, partyMemberEntityIds);
            AddMatchingPartyCooldownOwnedObjectOwnerIds(normalizedSourceName, matchingOwnerEntityIds);
            ownerMatch = PartyCooldownOwnedObjectOwnerResolver.Resolve(
                ObjectTable.LocalPlayer?.EntityId ?? 0,
                matchingOwnerEntityIds,
                partyMemberEntityIds);
            detail = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            ownerMatch = default;
            detail = $"소환수/객체 목록을 읽지 못했습니다: {ex.GetType().Name}";
            this.SetBugDiagnosticEvent($"partyCooldownOwnedObjectReadFailed:{ex.GetType().Name}");
            return false;
        }
        finally
        {
            matchingOwnerEntityIds.Clear();
            partyMemberEntityIds.Clear();
        }
    }

    private static void AddPartyCooldownMemberEntityIds(
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        ISet<uint> partyMemberEntityIds)
    {
        foreach (var candidate in displayMembers)
        {
            if (IsValidPartyCooldownEntityId(candidate.EntityId))
                partyMemberEntityIds.Add(candidate.EntityId);
        }
    }

    private static void AddMatchingPartyCooldownOwnedObjectOwnerIds(
        string normalizedSourceName,
        ISet<uint> matchingOwnerEntityIds)
    {
        foreach (var gameObject in ObjectTable)
        {
            if (TryGetMatchingPartyCooldownOwnedObjectOwnerId(
                    gameObject,
                    normalizedSourceName,
                    out var ownerEntityId))
            {
                matchingOwnerEntityIds.Add(ownerEntityId);
            }
        }
    }

    private static bool TryGetMatchingPartyCooldownOwnedObjectOwnerId(
        IGameObject? gameObject,
        string normalizedSourceName,
        out uint ownerEntityId)
    {
        ownerEntityId = gameObject?.OwnerId ?? 0;
        if (!IsValidPartyCooldownEntityId(ownerEntityId))
            return false;

        var objectName = PartyCooldownLogMatcher.NormalizeActorName(gameObject!.Name.ToString());
        return string.Equals(normalizedSourceName, objectName, StringComparison.Ordinal);
    }

    private static bool TryApplyPartyCooldownOwnedObjectOwnerMatch(
        PartyCooldownOwnedObjectOwnerMatch ownerMatch,
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        out PartyCooldownMemberSnapshot member,
        out string detail,
        out PartyCooldownIgnoredLogReason ignoredReason)
    {
        if (ownerMatch.Kind == PartyCooldownOwnedObjectOwnerMatchKind.LocalPlayer)
        {
            member = default;
            ignoredReason = PartyCooldownIgnoredLogReason.LocalPlayerExcluded;
            detail = "로컬 플레이어의 소환수/객체 행동이라 제외했습니다.";
            return false;
        }

        if (ownerMatch.Kind == PartyCooldownOwnedObjectOwnerMatchKind.PartyMember
            && TryFindPartyCooldownMemberByEntityId(displayMembers, ownerMatch.OwnerEntityId, out member))
        {
            ignoredReason = PartyCooldownIgnoredLogReason.None;
            detail = "소환수/객체 소유자 매칭";
            return true;
        }

        member = default;
        if (ownerMatch.Kind == PartyCooldownOwnedObjectOwnerMatchKind.Ambiguous)
        {
            ignoredReason = PartyCooldownIgnoredLogReason.Ambiguous;
            detail = "같은 이름의 소환수/객체 소유 파티원이 여러 명이라 추적하지 않았습니다.";
            return false;
        }

        ignoredReason = PartyCooldownIgnoredLogReason.OwnerNotFound;
        detail = string.Empty;
        return false;
    }
}
