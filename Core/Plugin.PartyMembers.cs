namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private const int PartyMemberSlotCount = 8;

    private readonly record struct PartyListHeader(
        int Length,
        bool IsAlliance,
        int PartyId)
    {
        public int PartySlotCount => Math.Clamp(this.Length, 0, PartyMemberSlotCount);
    }

    private PartyListHeader GetPartyListHeader()
    {
        try
        {
            return new PartyListHeader(
                Math.Max(0, PartyList.Length),
                PartyList.IsAlliance,
                (int)PartyList.PartyId);
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyListHeaderReadFailed:{ex.GetType().Name}");
            return default;
        }
    }

    private IPartyMember? TryCreatePartyMemberReference(int index)
    {
        try
        {
            var address = PartyList.GetPartyMemberAddress(index);
            if (address == IntPtr.Zero)
                return null;

            return PartyList.CreatePartyMemberReference(address);
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyMemberReferenceReadFailed:{index}:{ex.GetType().Name}");
            return null;
        }
    }
}
