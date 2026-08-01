using Dalamud.Game.Chat;
using Lumina.Text.ReadOnly;

namespace FFXIVAura;

internal sealed class PartyCooldownLogParser
{
    private readonly PartyCooldownCatalog partyCooldownCatalog;
    private readonly Dictionary<uint, int> actionParameterIndexByLogMessageId = new();

    public PartyCooldownLogParser(PartyCooldownCatalog partyCooldownCatalog)
    {
        this.partyCooldownCatalog = partyCooldownCatalog;
    }

    public bool IsCompletedActionUse(ILogMessage message)
    {
        if (!message.GameData.IsValid)
            return false;

        var templateText = message.GameData.Value.Text.ExtractText();
        return PartyCooldownLogMatcher.IsCompletedActionUseTemplate(templateText);
    }

    public PartyCooldownObservedAction ExtractObservedAction(ILogMessage message)
    {
        if (this.actionParameterIndexByLogMessageId.TryGetValue(message.LogMessageId, out var cachedIndex)
            && this.TryGetTrackedActionIdParameter(message, cachedIndex, out var cachedActionId))
        {
            return new PartyCooldownObservedAction(cachedActionId, string.Empty, "id", cachedIndex);
        }

        for (var index = 0; index < message.ParameterCount; index++)
        {
            if (this.TryGetTrackedActionIdParameter(message, index, out var actionId))
            {
                this.actionParameterIndexByLogMessageId[message.LogMessageId] = index;
                return new PartyCooldownObservedAction(actionId, string.Empty, "id", index);
            }
        }

        for (var index = 0; index < message.ParameterCount; index++)
        {
            if (!message.TryGetStringParameter(index, out var stringValue))
                continue;

            var actionName = PartyCooldownLogMatcher.NormalizeActionName(ExtractParameterText(stringValue));
            if (string.IsNullOrWhiteSpace(actionName))
                continue;

            if (this.partyCooldownCatalog.ContainsActionName(actionName))
                return new PartyCooldownObservedAction(0, actionName, "name", index);
        }

        return default;
    }

    public string DescribeParameters(ILogMessage message)
    {
        var parts = new List<string>(Math.Min(message.ParameterCount, 8));
        for (var index = 0; index < message.ParameterCount && parts.Count < 8; index++)
        {
            if (message.TryGetIntParameter(index, out var intValue))
            {
                parts.Add($"{index}=#{intValue}");
                continue;
            }

            if (message.TryGetStringParameter(index, out var stringValue))
            {
                var text = ExtractParameterText(stringValue);
                if (text.Length > 24)
                    text = $"{text[..24]}...";

                parts.Add($"{index}=\"{text}\"");
            }
        }

        return parts.Count == 0 ? "params: -" : $"params: {string.Join(", ", parts)}";
    }

    private bool TryGetTrackedActionIdParameter(ILogMessage message, int parameterIndex, out uint actionId)
    {
        actionId = 0;
        if (parameterIndex < 0
            || parameterIndex >= message.ParameterCount
            || !message.TryGetIntParameter(parameterIndex, out var value)
            || value <= 0)
        {
            return false;
        }

        actionId = (uint)value;
        return this.partyCooldownCatalog.ContainsActionId(actionId);
    }

    private static string ExtractParameterText(ReadOnlySeString value)
        => value.ExtractText().Trim();
}
