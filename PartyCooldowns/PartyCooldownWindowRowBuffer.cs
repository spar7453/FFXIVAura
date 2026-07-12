namespace FFXIVAura;

internal sealed class PartyCooldownWindowRowBuffer
{
    private readonly Dictionary<string, List<PartyCooldownDisplayItem>> itemsByMember =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> liveMemberKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> staleMemberKeys = [];

    public List<PartyCooldownMemberRow> Rows { get; } = [];

    public int MemberBufferCount => this.itemsByMember.Count;

    public void BeginFrame(int memberCapacity)
    {
        this.Rows.Clear();
        this.Rows.EnsureCapacity(Math.Max(0, memberCapacity));
        this.liveMemberKeys.Clear();
    }

    public List<PartyCooldownDisplayItem> GetMemberItems(string memberKey, int itemCapacity)
    {
        this.liveMemberKeys.Add(memberKey);
        if (!this.itemsByMember.TryGetValue(memberKey, out var items))
        {
            items = new List<PartyCooldownDisplayItem>(Math.Max(0, itemCapacity));
            this.itemsByMember[memberKey] = items;
        }
        else
        {
            items.Clear();
            items.EnsureCapacity(Math.Max(0, itemCapacity));
        }

        return items;
    }

    public void EndFrame()
    {
        this.staleMemberKeys.Clear();
        foreach (var memberKey in this.itemsByMember.Keys)
        {
            if (!this.liveMemberKeys.Contains(memberKey))
                this.staleMemberKeys.Add(memberKey);
        }

        foreach (var memberKey in this.staleMemberKeys)
            this.itemsByMember.Remove(memberKey);
    }
}
