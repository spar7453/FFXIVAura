using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownRuntimeStoreTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownRuntimeStore reuses case-insensitive runtime states", ReusesCaseInsensitiveRuntimeStates),
        ("PartyCooldownRuntimeStore prunes states outside the live key set", PrunesStatesOutsideLiveKeySet),
        ("PartyCooldownRuntimeStore caches live keys for one frame", CachesLiveKeysForOneFrame),
        ("PartyCooldownRuntimeStore owns window row buffer lifetimes", OwnsWindowRowBufferLifetimes),
        ("PartyCooldownRuntimeStore resets all owned state", ResetsAllOwnedState),
    ];

    private static void ReusesCaseInsensitiveRuntimeStates()
    {
        var store = new PartyCooldownRuntimeStore();
        var first = store.GetOrCreateState(new PartyCooldownRuntimeKey("Member-A", "Ability-A"));
        var second = store.GetOrCreateState(new PartyCooldownRuntimeKey("member-a", "ability-a"));

        True(ReferenceEquals(first, second), "equivalent runtime keys should reuse one state instance");
        Equal(1, store.CreateDiagnostics().RuntimeStateCount);
    }

    private static void PrunesStatesOutsideLiveKeySet()
    {
        var store = new PartyCooldownRuntimeStore();
        var keepKey = new PartyCooldownRuntimeKey("member-a", "ability-a");
        var removeKey = new PartyCooldownRuntimeKey("member-b", "ability-b");
        var keptState = store.GetOrCreateState(keepKey);
        var removedState = store.GetOrCreateState(removeKey);

        store.BeginFrame();
        True(store.BeginLiveKeyCollection(), "the first collection in a frame should build live keys");
        store.MarkLiveKey(keepKey);
        store.CompleteLiveKeyCollection();
        store.PruneStates();

        Equal(1, store.CreateDiagnostics().RuntimeStateCount);
        True(ReferenceEquals(keptState, store.GetOrCreateState(keepKey)), "a live runtime state should be retained");
        True(
            !ReferenceEquals(removedState, store.GetOrCreateState(removeKey)),
            "a missing runtime state should be recreated after pruning");
    }

    private static void CachesLiveKeysForOneFrame()
    {
        var store = new PartyCooldownRuntimeStore();
        var key = new PartyCooldownRuntimeKey("member-a", "ability-a");
        store.GetOrCreateState(key);

        store.BeginFrame();
        store.PruneStates();
        Equal(1, store.CreateDiagnostics().RuntimeStateCount);
        True(store.BeginLiveKeyCollection(), "an invalidated frame cache should start collection");
        store.MarkLiveKey(key);
        store.CompleteLiveKeyCollection();

        True(!store.BeginLiveKeyCollection(), "completed live keys should be reused within the frame");
        Equal(1, store.CreateDiagnostics().LiveRuntimeKeyCount);

        store.BeginFrame();
        Equal(0, store.CreateDiagnostics().LiveRuntimeKeyCount);
        True(store.BeginLiveKeyCollection(), "a new frame should invalidate the previous live key cache");
    }

    private static void OwnsWindowRowBufferLifetimes()
    {
        var store = new PartyCooldownRuntimeStore();
        var first = store.GetOrCreateRowBuffer("Window-A");
        first.BeginFrame(1);
        first.GetMemberItems("member-a", 1);
        first.EndFrame();
        var same = store.GetOrCreateRowBuffer("window-a");
        store.GetOrCreateRowBuffer("window-b");

        True(ReferenceEquals(first, same), "window ids should reuse row buffers case-insensitively");
        var beforePrune = store.CreateDiagnostics();
        Equal(2, beforePrune.RowBufferCount);
        Equal(1, beforePrune.RowMemberBufferCount);

        store.PruneWindowBuffers(["WINDOW-A"]);
        Equal(1, store.CreateDiagnostics().RowBufferCount);
        store.RemoveWindowBuffer("window-a");
        Equal(0, store.CreateDiagnostics().RowBufferCount);
    }

    private static void ResetsAllOwnedState()
    {
        var store = new PartyCooldownRuntimeStore();
        var key = new PartyCooldownRuntimeKey("member-a", "ability-a");
        var state = store.GetOrCreateState(key);
        var rowBuffer = store.GetOrCreateRowBuffer("window-a");
        rowBuffer.BeginFrame(1);
        rowBuffer.GetMemberItems("member-a", 1);
        rowBuffer.EndFrame();
        store.BeginFrame();
        store.BeginLiveKeyCollection();
        store.MarkLiveKey(key);
        store.CompleteLiveKeyCollection();

        store.Reset();

        var diagnostics = store.CreateDiagnostics();
        Equal(0, diagnostics.RuntimeStateCount);
        Equal(0, diagnostics.RowBufferCount);
        Equal(0, diagnostics.RowMemberBufferCount);
        Equal(0, diagnostics.LiveRuntimeKeyCount);
        True(!ReferenceEquals(state, store.GetOrCreateState(key)), "reset should release runtime state instances");
        True(
            !ReferenceEquals(rowBuffer, store.GetOrCreateRowBuffer("window-a")),
            "reset should release window row buffers");
        True(store.BeginLiveKeyCollection(), "reset should invalidate the live key cache");
    }
}
