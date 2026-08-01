using Lumina.Data.Files;

namespace FFXIVAura;

internal readonly record struct IconTextureDiagnostics(
    int GrayscaleCacheCount,
    int GrayscaleQueueCount,
    int GrayscalePendingCount,
    int GrayscaleFailedCount);

internal sealed class IconTextureService : IDisposable
{
    private readonly ITextureProvider textureProvider;
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    private readonly Dictionary<uint, IDalamudTextureWrap> grayscaleCache = new();
    private readonly Queue<uint> grayscaleQueue = new();
    private readonly HashSet<uint> grayscalePending = [];
    private readonly HashSet<uint> grayscaleFailed = [];
    private readonly object syncRoot = new();
    private bool disposed;

    public IconTextureService(
        ITextureProvider textureProvider,
        IDataManager dataManager,
        IPluginLog log)
    {
        this.textureProvider = textureProvider;
        this.dataManager = dataManager;
        this.log = log;
    }

    public IDalamudTextureWrap GetIcon(uint iconId)
    {
        var lookup = new GameIconLookup(iconId, false, true, null);
        return this.textureProvider.GetFromGameIcon(in lookup).GetWrapOrEmpty();
    }

    public IDalamudTextureWrap? GetGrayscaleIcon(uint iconId)
    {
        if (iconId == 0 || this.disposed)
            return null;

        lock (this.syncRoot)
        {
            if (this.grayscaleCache.TryGetValue(iconId, out var cached))
                return cached;

            if (this.grayscaleFailed.Contains(iconId) || !this.grayscalePending.Add(iconId))
                return null;

            this.grayscaleQueue.Enqueue(iconId);
        }

        return null;
    }

    public int ProcessGrayscaleQueue(int maxPerFrame = 1)
    {
        if (this.disposed)
            return 0;

        var processed = 0;
        for (var index = 0; index < maxPerFrame; index++)
        {
            uint iconId;
            lock (this.syncRoot)
            {
                if (this.grayscaleQueue.Count == 0)
                    return processed;

                iconId = this.grayscaleQueue.Dequeue();
                if (this.grayscaleCache.ContainsKey(iconId))
                {
                    this.grayscalePending.Remove(iconId);
                    continue;
                }
            }

            this.CreateGrayscaleIcon(iconId);
            processed++;
        }

        return processed;
    }

    public IconTextureDiagnostics CreateDiagnostics()
    {
        lock (this.syncRoot)
        {
            return new IconTextureDiagnostics(
                this.grayscaleCache.Count,
                this.grayscaleQueue.Count,
                this.grayscalePending.Count,
                this.grayscaleFailed.Count);
        }
    }

    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;
        lock (this.syncRoot)
        {
            foreach (var texture in this.grayscaleCache.Values)
                texture.Dispose();

            this.grayscaleCache.Clear();
            this.grayscaleQueue.Clear();
            this.grayscalePending.Clear();
            this.grayscaleFailed.Clear();
        }
    }

    internal static void ConvertRgbaImageToGrayscale(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        for (var offset = 0; offset + 3 < data.Length; offset += 4)
        {
            var gray = (byte)Math.Clamp(
                (int)(data[offset] * 0.299f + data[offset + 1] * 0.587f + data[offset + 2] * 0.114f),
                0,
                255);
            data[offset] = gray;
            data[offset + 1] = gray;
            data[offset + 2] = gray;
        }
    }

    private void CreateGrayscaleIcon(uint iconId)
    {
        try
        {
            var lookup = new GameIconLookup(iconId, false, true, null);
            if (!this.textureProvider.TryGetIconPath(in lookup, out var iconPath))
                throw new FileNotFoundException($"Icon path not found for {iconId}.");

            var textureFile = this.dataManager.GetFile<TexFile>(iconPath);
            if (textureFile is null)
                throw new FileNotFoundException($"Icon tex not found for {iconId}: {iconPath}");

            var data = textureFile.GetRgbaImageData();
            ConvertRgbaImageToGrayscale(data);
            var specification = RawImageSpecification.Rgba32(
                textureFile.Header.Width,
                textureFile.Header.Height);
            var texture = this.textureProvider.CreateFromRaw(
                specification,
                data,
                $"FFXIVAura grayscale icon {iconId}");

            lock (this.syncRoot)
            {
                if (this.disposed)
                {
                    // The service was disposed while this texture was being created; caching it
                    // now would leak it past the cache cleanup in Dispose.
                    texture.Dispose();
                    return;
                }

                if (this.grayscaleCache.TryGetValue(iconId, out var oldTexture))
                    oldTexture.Dispose();

                this.grayscaleCache[iconId] = texture;
                this.grayscalePending.Remove(iconId);
                this.grayscaleFailed.Remove(iconId);
            }
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, $"Failed to create grayscale icon {iconId}.");
            lock (this.syncRoot)
            {
                this.grayscalePending.Remove(iconId);
                this.grayscaleFailed.Add(iconId);
            }
        }
    }
}
