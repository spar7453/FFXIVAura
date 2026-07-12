namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private PartyListHeader GetPartyListHeader()
        => this.partyRosterReader.ReadPartyListHeader();

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
