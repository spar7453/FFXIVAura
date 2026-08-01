namespace FFXIVAura;

internal readonly record struct PluginRuntimeBinding(
    Action Subscribe,
    Action Unsubscribe,
    string Description);

internal sealed class PluginRuntimeBindings : IDisposable
{
    private readonly PluginLifetime lifetime;

    public PluginRuntimeBindings(
        IEnumerable<PluginRuntimeBinding> bindings,
        Action<Exception, string> logError)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        this.lifetime = new PluginLifetime(logError);

        try
        {
            foreach (var binding in bindings)
            {
                ArgumentNullException.ThrowIfNull(binding.Subscribe);
                ArgumentNullException.ThrowIfNull(binding.Unsubscribe);

                this.lifetime.Register(binding.Unsubscribe, binding.Description);
                binding.Subscribe();
            }
        }
        catch
        {
            this.lifetime.Dispose();
            throw;
        }
    }

    public int Count => this.lifetime.Count;

    public void Dispose()
        => this.lifetime.Dispose();
}
