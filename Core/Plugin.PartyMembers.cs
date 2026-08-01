namespace FFXIVAura;

public sealed partial class Plugin
{
    private PartyListHeader GetPartyListHeader()
        => this.partyRosterReader.ReadPartyListHeader();
}
