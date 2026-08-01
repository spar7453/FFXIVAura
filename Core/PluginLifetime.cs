namespace FFXIVAura;

internal sealed class PluginLifetime : IDisposable
{
    private readonly record struct CleanupRegistration(
        Action Cleanup,
        string Description);

    private readonly Stack<CleanupRegistration> cleanups = [];
    private readonly Action<Exception, string> logError;
    private bool disposed;

    public PluginLifetime(Action<Exception, string> logError)
    {
        this.logError = logError ?? throw new ArgumentNullException(nameof(logError));
    }

    public int Count => this.cleanups.Count;

    public T Own<T>(T resource, string description)
        where T : IDisposable
    {
        ArgumentNullException.ThrowIfNull(resource);
        this.Register(resource.Dispose, description);
        return resource;
    }

    public void Register(Action cleanup, string description)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        if (this.disposed)
            throw new ObjectDisposedException(nameof(PluginLifetime));

        this.cleanups.Push(new CleanupRegistration(
            cleanup,
            string.IsNullOrWhiteSpace(description) ? "plugin resource" : description.Trim()));
    }

    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;
        while (this.cleanups.TryPop(out var registration))
        {
            try
            {
                registration.Cleanup();
            }
            catch (Exception ex)
            {
                try
                {
                    this.logError(ex, $"Failed to clean up {registration.Description}.");
                }
                catch
                {
                    // Cleanup must continue even if the logging adapter is already unavailable.
                }
            }
        }
    }
}
