using NaviMovieMaker.App;
using NaviMovieMaker.App.Services;

var tests = new (string Name, Action Body)[]
{
    ("stable ItemId save/load", StableItemIdSaveLoad),
    ("old playlist migration", OldPlaylistMigration),
    ("available result detection", AvailableResult),
    ("missing result", MissingResult),
    ("modified result", ModifiedResult),
    ("profile/source mismatch", ProfileAndSourceMismatch),
    ("sequence out of sync", SequenceOutOfSync),
    ("same result skipped", SameResultSkipped),
    ("different source collision", DifferentSourceCollision),
    ("two-way sequence swap", TwoWaySequenceSwap),
    ("rename conflict", RenameConflict),
    ("automatic source playback", AutomaticSourcePlayback),
    ("automatic result playback", AutomaticResultPlayback),
    ("Download Only result tracking", DownloadOnlyResultTracking),
    ("queue order preservation", QueueOrderPreservation),
    ("playlist-specific output folders", PlaylistSpecificOutputFolders),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add($"FAIL {test.Name}: {ex.Message}"); }
}
foreach (var failure in failures) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;

static void StableItemIdSaveLoad()
{
    WithTempFolder(folder =>
    {
        var path = Path.Combine(folder, "playlist.json");
        var id = Guid.NewGuid().ToString("N");
        var playlist = Playlist(new ConversionPlaylistItem { ItemId = id, SourceKind = "OnlineVideo", SourcePathOrUrl = "https://example.com/a" });
        var service = new ConversionPlaylistService();
        service.Save(path, playlist);
        Equal(id, service.Load(path).Items[0].ItemId);
    });
}

static void OldPlaylistMigration()
{
    WithTempFolder(folder =>
    {
        var path = Path.Combine(folder, "old.json");
        File.WriteAllText(path, "{\"formatVersion\":1,\"items\":[{\"sourceKind\":\"OnlineVideo\",\"sourcePathOrUrl\":\"https://example.com/a\"}]}");
        var loaded = new ConversionPlaylistService().Load(path);
        True(!string.IsNullOrWhiteSpace(loaded.Items[0].ItemId));
        Equal(ConversionPlaylist.CurrentFormatVersion, loaded.FormatVersion);
    });
}

static void AvailableResult() => WithTrackedLocal((service, item, _) =>
    Equal(PlaylistResultState.Available, service.Reconcile(item, "Convert Only", "p1", null)));

static void MissingResult() => WithTrackedLocal((service, item, output) =>
{
    File.Delete(output);
    Equal(PlaylistResultState.Missing, service.Reconcile(item, "Convert Only", "p1", null));
});

static void ModifiedResult() => WithTrackedLocal((service, item, output) =>
{
    File.AppendAllText(output, "changed");
    Equal(PlaylistResultState.Modified, service.Reconcile(item, "Convert Only", "p1", null));
});

static void ProfileAndSourceMismatch() => WithTrackedLocal((service, item, _) =>
{
    Equal(PlaylistResultState.NeedsReprocess, service.Reconcile(item, "Convert Only", "p2", null));
    item.Result!.ProfileId = "p1";
    File.AppendAllText(item.SourcePathOrUrl, "source change");
    Equal(PlaylistResultState.NeedsReprocess, service.Reconcile(item, "Convert Only", "p1", null));
});

static void SequenceOutOfSync() => WithTrackedLocal((service, item, _) =>
    Equal(PlaylistResultState.SequenceOutOfSync, service.Reconcile(item, "Convert Only", "p1", 2)), "001_output.mp4");

static void SameResultSkipped() => WithTrackedLocal((service, item, _) =>
{
    var state = service.Reconcile(item, "Convert Only", "p1", null);
    True(state is PlaylistResultState.Available or PlaylistResultState.SequenceOutOfSync);
});

static void DifferentSourceCollision()
{
    WithTempFolder(folder =>
    {
        var source = Path.Combine(folder, "source.mp4"); File.WriteAllText(source, "a");
        File.WriteAllText(Path.Combine(folder, "Title.mp4"), "owned by someone else");
        var item = LocalItem(source);
        var path = new PlaylistResultService().ResolveCollisionSafePath(item, folder, "Title", ".mp4");
        True(Path.GetFileName(path).StartsWith("Title__", StringComparison.Ordinal));
    });
}

static void TwoWaySequenceSwap()
{
    WithTempFolder(folder =>
    {
        var service = new PlaylistResultService();
        var a = TrackedItem(service, folder, "a-source.mp4", "001_A.mp4", 1); a.Order = 2;
        var b = TrackedItem(service, folder, "b-source.mp4", "002_B.mp4", 2); b.Order = 1;
        service.Reconcile(a, "Convert Only", "p1", 2);
        service.Reconcile(b, "Convert Only", "p1", 1);
        var renames = service.BuildSequenceRenames(new[] { a, b }, 1);
        service.ApplySequenceRenames(renames);
        True(File.Exists(Path.Combine(folder, "002_A.mp4")));
        True(File.Exists(Path.Combine(folder, "001_B.mp4")));
    });
}

static void RenameConflict()
{
    WithTempFolder(folder =>
    {
        var service = new PlaylistResultService();
        var item = TrackedItem(service, folder, "source.mp4", "001_A.mp4", 1); item.Order = 2;
        service.Reconcile(item, "Convert Only", "p1", 2);
        File.WriteAllText(Path.Combine(folder, "002_A.mp4"), "unrelated");
        Throws<IOException>(() => service.ApplySequenceRenames(service.BuildSequenceRenames(new[] { item }, 1)));
        Equal(PlaylistResultState.NameConflict, item.ResultState);
    });
}

static void AutomaticSourcePlayback()
{
    var item = new ConversionQueueItem { SourceType = "OnlineVideo", SourcePathOrUrl = "https://example.com/a", ResultState = PlaylistResultState.Unprocessed };
    True(!new[] { item }.All(static value => value.ResultState == PlaylistResultState.Available));
}

static void AutomaticResultPlayback() => WithTrackedLocal((service, item, _) =>
{
    service.Reconcile(item, "Convert Only", "p1", null);
    True(new[] { item }.All(static value => value.ResultState == PlaylistResultState.Available));
});

static void DownloadOnlyResultTracking() => WithTempFolder(folder =>
{
    var output = Path.Combine(folder, "download.mp4"); File.WriteAllText(output, "download");
    var item = new ConversionQueueItem { SourceType = "OnlineVideo", SourcePathOrUrl = "https://example.com/video" };
    var service = new PlaylistResultService();
    service.RecordSuccessfulResult(item, output, "Download Only", "download-profile", null);
    Equal(output, item.DownloadedFilePath);
    Equal(1, new PlaybackPlaylistBuilder().BuildConverted(new[] { item }).Entries.Count);
});

static void QueueOrderPreservation() => WithTempFolder(folder =>
{
    var first = Path.Combine(folder, "first.mp4"); var second = Path.Combine(folder, "second.mp4");
    File.WriteAllText(first, "1"); File.WriteAllText(second, "2");
    var a = LocalItem(first); a.Order = 2;
    var b = LocalItem(second); b.Order = 1;
    var entries = new PlaybackPlaylistBuilder().BuildSource(new[] { a, b }).Entries;
    Equal(second, entries[0]); Equal(first, entries[1]);
});

static void PlaylistSpecificOutputFolders() => WithTempFolder(folder =>
{
    var service = new ConversionPlaylistService();
    var firstPath = Path.Combine(folder, "first.json");
    var secondPath = Path.Combine(folder, "second.json");
    var firstOutput = Path.Combine(folder, "output-a");
    var secondOutput = Path.Combine(folder, "output-b");
    service.Save(firstPath, new ConversionPlaylist { OutputFolder = firstOutput });
    service.Save(secondPath, new ConversionPlaylist { OutputFolder = secondOutput });
    Equal(firstOutput, ConversionPlaylistService.ResolveOutputFolder(service.Load(firstPath), "global"));
    Equal(secondOutput, ConversionPlaylistService.ResolveOutputFolder(service.Load(secondPath), "global"));

    var missingSavedFolder = Path.Combine(folder, "does-not-exist");
    Equal(missingSavedFolder, ConversionPlaylistService.ResolveOutputFolder(
        new ConversionPlaylist { OutputFolder = missingSavedFolder }, "global"));
    Equal("global", ConversionPlaylistService.ResolveOutputFolder(new ConversionPlaylist(), "global"));
});

static ConversionPlaylist Playlist(params ConversionPlaylistItem[] items) => new() { Items = items.ToList() };
static ConversionQueueItem LocalItem(string path) => new() { SourceType = "LocalFile", SourcePathOrUrl = path, Title = "Title" };

static ConversionQueueItem TrackedItem(PlaylistResultService service, string folder, string sourceName, string outputName, int sequence)
{
    var source = Path.Combine(folder, sourceName); var output = Path.Combine(folder, outputName);
    File.WriteAllText(source, sourceName); File.WriteAllText(output, outputName);
    var item = LocalItem(source);
    service.RecordSuccessfulResult(item, output, "Convert Only", "p1", sequence);
    return item;
}

static void WithTrackedLocal(Action<PlaylistResultService, ConversionQueueItem, string> test, string outputName = "output.mp4")
{
    WithTempFolder(folder =>
    {
        var service = new PlaylistResultService();
        var item = TrackedItem(service, folder, "source.mp4", outputName, outputName.StartsWith("001_") ? 1 : 0);
        test(service, item, Path.Combine(folder, outputName));
    });
}

static void WithTempFolder(Action<string> action)
{
    var folder = Path.Combine(Path.GetTempPath(), "NaviMovieMaker.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    try { action(folder); } finally { try { Directory.Delete(folder, true); } catch { } }
}

static void True(bool value, string message = "Expected true") { if (!value) throw new Exception(message); }
static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; actual {actual}"); }
static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }
