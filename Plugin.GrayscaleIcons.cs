namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private IDalamudTextureWrap? GetGrayscaleIconTexture(uint iconId)
    {
        if (iconId == 0)
            return null;

        lock (this.grayscaleIconLock)
        {
            if (this.grayscaleIconCache.TryGetValue(iconId, out var cached))
                return cached;

            if (this.grayscaleIconFailed.Contains(iconId) || !this.grayscaleIconPending.Add(iconId))
                return null;

            this.grayscaleIconQueue.Enqueue(iconId);
        }

        return null;
    }

    private void ProcessGrayscaleIconQueue(int maxPerFrame = 1)
    {
        for (var index = 0; index < maxPerFrame; index++)
        {
            uint iconId;
            lock (this.grayscaleIconLock)
            {
                if (this.grayscaleIconQueue.Count == 0)
                    return;

                iconId = this.grayscaleIconQueue.Dequeue();
                if (this.grayscaleIconCache.ContainsKey(iconId))
                {
                    this.grayscaleIconPending.Remove(iconId);
                    continue;
                }
            }

            this.CreateGrayscaleIconTexture(iconId);
        }
    }

    private void CreateGrayscaleIconTexture(uint iconId)
    {
        try
        {
            var lookup = new GameIconLookup(iconId, false, true, null);
            if (!TextureProvider.TryGetIconPath(in lookup, out var iconPath))
                throw new FileNotFoundException($"Icon path not found for {iconId}.");

            var tex = DataManager.GetFile<TexFile>(iconPath);
            if (tex is null)
                throw new FileNotFoundException($"Icon tex not found for {iconId}: {iconPath}");

            var data = tex.GetRgbaImageData();
            ConvertRgbaImageToGrayscale(data);
            var spec = RawImageSpecification.Rgba32(tex.Header.Width, tex.Header.Height);
            var texture = TextureProvider.CreateFromRaw(spec, data, $"FFXIVAura grayscale icon {iconId}");

            lock (this.grayscaleIconLock)
            {
                if (this.grayscaleIconCache.TryGetValue(iconId, out var oldTexture))
                    oldTexture.Dispose();

                this.grayscaleIconCache[iconId] = texture;
                this.grayscaleIconPending.Remove(iconId);
                this.grayscaleIconFailed.Remove(iconId);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to create grayscale icon {iconId}.");
            lock (this.grayscaleIconLock)
            {
                this.grayscaleIconPending.Remove(iconId);
                this.grayscaleIconFailed.Add(iconId);
            }
        }
    }

    private static void ConvertRgbaImageToGrayscale(byte[] data)
    {
        for (var offset = 0; offset + 3 < data.Length; offset += 4)
        {
            var gray = (byte)Math.Clamp((int)(data[offset] * 0.299f + data[offset + 1] * 0.587f + data[offset + 2] * 0.114f), 0, 255);
            data[offset] = gray;
            data[offset + 1] = gray;
            data[offset + 2] = gray;
        }
    }
}
