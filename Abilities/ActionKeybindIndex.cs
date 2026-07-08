namespace FFXIVAura;

internal sealed class ActionKeybindIndex
{
    private readonly Dictionary<uint, string> keybinds = new();

    public int Count => this.keybinds.Count;

    public void Clear()
        => this.keybinds.Clear();

    public void Register(uint actionId, uint adjustedActionId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        this.Register(actionId, text);
        this.Register(adjustedActionId, text);
    }

    public string Find(uint baseActionId, uint displayActionId, uint adjustedBaseActionId, uint adjustedDisplayActionId)
    {
        return this.TryGet(baseActionId, out var text)
               || this.TryGet(displayActionId, out text)
               || this.TryGet(adjustedBaseActionId, out text)
               || this.TryGet(adjustedDisplayActionId, out text)
            ? text
            : string.Empty;
    }

    private void Register(uint actionId, string text)
    {
        if (actionId == 0)
            return;

        this.keybinds.TryAdd(actionId, text);
    }

    private bool TryGet(uint actionId, out string text)
    {
        if (actionId == 0)
        {
            text = string.Empty;
            return false;
        }

        return this.keybinds.TryGetValue(actionId, out text!);
    }
}
