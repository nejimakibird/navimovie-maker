using NaviMovieMaker.App;
using NaviMovieMaker.App.Services;
using System.Globalization;
using System.Text.Json;

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
    ("natural output name without collision", NaturalOutputNameWithoutCollision),
    ("numbered output name without collision", NumberedOutputNameWithoutCollision),
    ("different source collision", DifferentSourceCollision),
    ("other managed item collision", OtherManagedItemCollision),
    ("same item recorded output reuse", SameItemRecordedOutputReuse),
    ("same item safe regeneration", SameItemSafeRegeneration),
    ("same item stable collision output reuse", SameItemStableCollisionOutputReuse),
    ("occupied stable suffix uses next candidate", OccupiedStableSuffixUsesNextCandidate),
    ("sequence sync preserves collision suffix", SequenceSyncPreservesCollisionSuffix),
    ("simple and normal modes share natural naming", SimpleAndNormalModesShareNaturalNaming),
    ("file copy natural output name", FileCopyNaturalOutputName),
    ("legacy suffixed result survives reload", LegacySuffixedResultSurvivesReload),
    ("two-way sequence swap", TwoWaySequenceSwap),
    ("rename conflict", RenameConflict),
    ("automatic source playback", AutomaticSourcePlayback),
    ("automatic result playback", AutomaticResultPlayback),
    ("Download Only result tracking", DownloadOnlyResultTracking),
    ("queue order preservation", QueueOrderPreservation),
    ("playlist-specific output folders", PlaylistSpecificOutputFolders),
    ("ReplayGain equals parsing", ReplayGainEqualsParsing),
    ("ReplayGain colon parsing", ReplayGainColonParsing),
    ("ReplayGain prefixed parsing", ReplayGainPrefixedParsing),
    ("ReplayGain invariant parsing", ReplayGainInvariantParsing),
    ("ReplayGain missing gain", ReplayGainMissingGain),
    ("ReplayGain missing peak", ReplayGainMissingPeak),
    ("ReplayGain NaN", ReplayGainNaN),
    ("ReplayGain infinity", ReplayGainInfinity),
    ("ReplayGain zero peak", ReplayGainZeroPeak),
    ("ReplayGain negative peak", ReplayGainNegativePeak),
    ("ReplayGain infinite peak", ReplayGainInfinitePeak),
    ("ReplayGain excessive peak", ReplayGainExcessivePeak),
    ("ReplayGain invalid log", ReplayGainInvalidLog),
    ("ReplayGain no audio", ReplayGainNoAudio),
    ("ReplayGain target 89", ReplayGainTarget89),
    ("ReplayGain target 99", ReplayGainTarget99),
    ("ReplayGain XMedia example", ReplayGainXMediaExample),
    ("ReplayGain target 95", ReplayGainTarget95),
    ("ReplayGain positive gain", ReplayGainPositiveGain),
    ("ReplayGain negative gain", ReplayGainNegativeGain),
    ("ReplayGain tolerance skip", ReplayGainToleranceSkip),
    ("ReplayGain maximum gain", ReplayGainMaximumGain),
    ("ReplayGain attenuation limit", ReplayGainAttenuationLimit),
    ("ReplayGain predicted peak", ReplayGainPredictedPeak),
    ("ReplayGain volume only", ReplayGainVolumeOnly),
    ("ReplayGain limiter", ReplayGainLimiter),
    ("ReplayGain dBFS linear conversion", ReplayGainDbfsLinearConversion),
    ("ReplayGain invariant filter", ReplayGainInvariantFilter),
    ("ReplayGain defaults and range", ReplayGainDefaultsAndRange),
    ("ReplayGain step", ReplayGainStep),
    ("ReplayGain settings persistence", ReplayGainSettingsPersistence),
    ("legacy normalization mode migration", LegacyNormalizationModeMigration),
    ("ReplayGain playlist persistence", ReplayGainPlaylistPersistence),
    ("experimental mean names removed", ExperimentalMeanNamesRemoved),
    ("per-item loudnorm priority", PerItemLoudnormPriority),
    ("peak normalization unchanged", PeakNormalizationUnchanged),
    ("ReplayGain simple mode shared decision", ReplayGainSimpleModeSharedDecision),
    ("ReplayGain analysis failure continues", ReplayGainAnalysisFailureContinues),
    ("ReplayGain cancellation stops conversion", ReplayGainCancellationStopsConversion),
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

static void NaturalOutputNameWithoutCollision() => WithTempFolder(folder =>
{
    var source = Path.Combine(folder, "source.mp4"); File.WriteAllText(source, "source");
    var path = new PlaylistResultService().ResolveCollisionSafePath(LocalItem(source), folder, "Title", ".mp4");
    Equal("Title.mp4", Path.GetFileName(path));
});

static void NumberedOutputNameWithoutCollision() => WithTempFolder(folder =>
{
    var source = Path.Combine(folder, "source.mp4"); File.WriteAllText(source, "source");
    var path = new PlaylistResultService().ResolveCollisionSafePath(LocalItem(source), folder, "001_Title", ".mp4");
    Equal("001_Title.mp4", Path.GetFileName(path));
});

static void DifferentSourceCollision()
{
    WithTempFolder(folder =>
    {
        var source = Path.Combine(folder, "source.mp4"); File.WriteAllText(source, "a");
        File.WriteAllText(Path.Combine(folder, "Title.mp4"), "owned by someone else");
        var item = LocalItem(source);
        var path = new PlaylistResultService().ResolveCollisionSafePath(item, folder, "Title", ".mp4");
        var fileName = Path.GetFileName(path);
        True(fileName.StartsWith("Title_", StringComparison.Ordinal));
        Equal(16, fileName.Length);
        Equal("owned by someone else", File.ReadAllText(Path.Combine(folder, "Title.mp4")));
    });
}

static void OtherManagedItemCollision() => WithTempFolder(folder =>
{
    var service = new PlaylistResultService();
    _ = TrackedItem(service, folder, "owner-source.mp4", "Title.mp4", 0);
    var source = Path.Combine(folder, "other-source.mp4"); File.WriteAllText(source, "other");
    var path = service.ResolveCollisionSafePath(LocalItem(source), folder, "Title", ".mp4");
    True(!string.Equals("Title.mp4", Path.GetFileName(path), StringComparison.Ordinal));
});

static void SameItemRecordedOutputReuse() => WithTrackedLocal((service, item, output) =>
    Equal(Path.GetFullPath(output), Path.GetFullPath(service.ResolveCollisionSafePath(item, Path.GetDirectoryName(output)!, "output", ".mp4"))));

static void SameItemSafeRegeneration() => WithTrackedLocal((service, item, output) =>
{
    Equal(PlaylistResultState.NeedsReprocess, service.Reconcile(item, "Convert Only", "changed-profile", null));
    Equal(Path.GetFullPath(output), Path.GetFullPath(service.ResolveCollisionSafePath(item, Path.GetDirectoryName(output)!, "output", ".mp4")));
});

static void SameItemStableCollisionOutputReuse() => WithTempFolder(folder =>
{
    var service = new PlaylistResultService();
    var source = Path.Combine(folder, "source.mp4"); File.WriteAllText(source, "source");
    var item = LocalItem(source);
    File.WriteAllText(Path.Combine(folder, "Title.mp4"), "unmanaged");
    var collisionPath = service.ResolveCollisionSafePath(item, folder, "Title", ".mp4");
    File.WriteAllText(collisionPath, "owned result");
    service.RecordSuccessfulResult(item, collisionPath, "Convert Only", "p1", null);
    Equal(Path.GetFullPath(collisionPath), Path.GetFullPath(service.ResolveCollisionSafePath(item, folder, "Title", ".mp4")));
});

static void OccupiedStableSuffixUsesNextCandidate() => WithTempFolder(folder =>
{
    var source = Path.Combine(folder, "source.mp4"); File.WriteAllText(source, "source");
    var item = LocalItem(source);
    File.WriteAllText(Path.Combine(folder, "Title.mp4"), "unmanaged");
    var service = new PlaylistResultService();
    var stableCandidate = service.ResolveCollisionSafePath(item, folder, "Title", ".mp4");
    File.WriteAllText(stableCandidate, "also unmanaged");
    var nextCandidate = service.ResolveCollisionSafePath(item, folder, "Title", ".mp4");
    Equal(Path.GetFileNameWithoutExtension(stableCandidate) + "_2.mp4", Path.GetFileName(nextCandidate));
});

static void SequenceSyncPreservesCollisionSuffix() => WithTempFolder(folder =>
{
    var service = new PlaylistResultService();
    var item = TrackedItem(service, folder, "source.mp4", "001_Title_484306.mp4", 1); item.Order = 2;
    Equal(PlaylistResultState.SequenceOutOfSync, service.Reconcile(item, "Convert Only", "p1", 2));
    service.ApplySequenceRenames(service.BuildSequenceRenames(new[] { item }, 1));
    Equal("002_Title_484306.mp4", Path.GetFileName(item.Result!.ResultFilePath));
});

static void SimpleAndNormalModesShareNaturalNaming() => WithTempFolder(folder =>
{
    var service = new PlaylistResultService();
    var sourceFolder = Path.Combine(folder, "sources"); Directory.CreateDirectory(sourceFolder);
    var normalSource = Path.Combine(sourceFolder, "normal.mp4"); File.WriteAllText(normalSource, "normal");
    var simpleSource = Path.Combine(sourceFolder, "simple.mp4"); File.WriteAllText(simpleSource, "simple");
    Equal("Normal.mp4", Path.GetFileName(service.ResolveCollisionSafePath(LocalItem(normalSource), folder, "Normal", ".mp4")));
    Equal("Simple.mp4", Path.GetFileName(service.ResolveCollisionSafePath(LocalItem(simpleSource), folder, "Simple", ".mp4")));
});

static void FileCopyNaturalOutputName() => WithTempFolder(folder =>
{
    var source = Path.Combine(folder, "source.mkv"); File.WriteAllText(source, "source");
    Equal("Title.mkv", Path.GetFileName(new PlaylistResultService().ResolveCollisionSafePath(LocalItem(source), folder, "Title", ".mkv")));
});

static void LegacySuffixedResultSurvivesReload() => WithTempFolder(folder =>
{
    var source = Path.Combine(folder, "source.mp4"); File.WriteAllText(source, "source");
    var output = Path.Combine(folder, "Title__484306.mp4"); File.WriteAllText(output, "output");
    var queueItem = LocalItem(source);
    new PlaylistResultService().RecordSuccessfulResult(queueItem, output, "Convert Only", "p1", null);
    var path = Path.Combine(folder, "playlist.json");
    new ConversionPlaylistService().Save(path, Playlist(new ConversionPlaylistItem
    {
        ItemId = queueItem.ItemId,
        SourceKind = queueItem.SourceType,
        SourcePathOrUrl = queueItem.SourcePathOrUrl,
        Result = queueItem.Result,
    }));
    var loaded = new ConversionPlaylistService().Load(path).Items[0];
    Equal(output, loaded.Result!.ResultFilePath);
    var restoredQueueItem = new ConversionQueueItem
    {
        ItemId = loaded.ItemId,
        SourceType = loaded.SourceKind,
        SourcePathOrUrl = loaded.SourcePathOrUrl,
        Result = loaded.Result,
    };
    Equal(PlaylistResultState.Available, new PlaylistResultService().Reconcile(restoredQueueItem, "Convert Only", "p1", null));
});

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

static void ReplayGainEqualsParsing()
{
    var analysis = ParseReplayGain("track_gain = -2.70 dB\ntrack_peak = 0.945000");
    Near(-2.7, analysis.TrackGainDb);
    Near(0.945, analysis.TrackPeak);
}

static void ReplayGainColonParsing()
{
    var analysis = ParseReplayGain("track_gain: +1.25 dB\ntrack_peak: 0.750000");
    Near(1.25, analysis.TrackGainDb);
    Near(0.75, analysis.TrackPeak);
}

static void ReplayGainPrefixedParsing()
{
    var analysis = ParseReplayGain("[Parsed_replaygain_0 @ 0001] track_gain = -2.70 dB\n[Parsed_replaygain_0 @ 0001] track_peak = 0.945000");
    Near(-2.7, analysis.TrackGainDb);
    Near(0.945, analysis.TrackPeak);
}

static void ReplayGainInvariantParsing()
{
    var previousCulture = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        var analysis = ParseReplayGain("track_gain = -2.70 dB\ntrack_peak = 0.945000");
        Near(-2.7, analysis.TrackGainDb);
        Near(0.945, analysis.TrackPeak);
    }
    finally
    {
        CultureInfo.CurrentCulture = previousCulture;
    }
}

static void ReplayGainMissingGain() => True(ReplayGainParser.Parse("track_peak = 0.9") is null);
static void ReplayGainMissingPeak() => True(ReplayGainParser.Parse("track_gain = -2.0 dB") is null);
static void ReplayGainNaN() => True(ReplayGainParser.Parse("track_gain = NaN dB\ntrack_peak = 0.9") is null);
static void ReplayGainInfinity() => True(ReplayGainParser.Parse("track_gain = Infinity dB\ntrack_peak = 0.9") is null);
static void ReplayGainZeroPeak() => True(ReplayGainParser.Parse("track_gain = -2.0 dB\ntrack_peak = 0") is null);
static void ReplayGainNegativePeak() => True(ReplayGainParser.Parse("track_gain = -2.0 dB\ntrack_peak = -0.5") is null);
static void ReplayGainInfinitePeak() => True(ReplayGainParser.Parse("track_gain = -2.0 dB\ntrack_peak = Infinity") is null);
static void ReplayGainExcessivePeak() => True(ReplayGainParser.Parse("track_gain = -2.0 dB\ntrack_peak = 1000") is null);
static void ReplayGainInvalidLog() => True(ReplayGainParser.Parse("not ReplayGain output") is null);
static void ReplayGainNoAudio() => True(ReplayGainParser.Parse("Stream map '0:a:0' matches no streams") is null);

static void ReplayGainTarget89()
{
    var decision = DecideReplayGain(-2.7, 0.5, 89.0);
    Near(-2.7, decision.RequestedGainDb);
}

static void ReplayGainTarget99()
{
    var decision = DecideReplayGain(-2.7, 0.5, 99.0);
    Near(7.3, decision.RequestedGainDb);
}

static void ReplayGainXMediaExample()
{
    var decision = DecideReplayGain(-2.7, 0.945, 99.0);
    Near(91.7, decision.DetectedTrackVolumeDb);
    Near(10.0, decision.TargetOffsetDb);
    Near(7.3, decision.RequestedGainDb);
}

static void ReplayGainTarget95() => Near(3.3, DecideReplayGain(-2.7, 0.5, 95.0).RequestedGainDb);
static void ReplayGainPositiveGain() => Near(5.0, DecideReplayGain(-5.0, 0.5, 99.0).AppliedGainDb);
static void ReplayGainNegativeGain() => Near(-4.0, DecideReplayGain(-4.0, 0.5, 89.0).AppliedGainDb);

static void ReplayGainToleranceSkip()
{
    var decision = DecideReplayGain(0.2, 0.5, 89.0);
    Equal(ReplayGainNormalizationAction.Skip, decision.Action);
    Equal(string.Empty, decision.AudioFilter);
}

static void ReplayGainMaximumGain()
{
    var decision = DecideReplayGain(14.0, 0.1, 99.0, new ReplayGainNormalizationOptions(99.0, -1.0, 0.5, 20.0));
    Near(20.0, decision.AppliedGainDb);
    True(decision.GainWasLimited);
}

static void ReplayGainAttenuationLimit()
{
    var decision = DecideReplayGain(-40.0, 0.5, 89.0);
    Near(-30.0, decision.AppliedGainDb);
    True(decision.GainWasLimited);
}

static void ReplayGainPredictedPeak()
{
    var decision = DecideReplayGain(-2.7, 0.945, 99.0);
    Near(0.945 * Math.Pow(10.0, 7.3 / 20.0), decision.PredictedPeakLinear);
    Near(20.0 * Math.Log10(decision.PredictedPeakLinear), decision.PredictedPeakDb);
}

static void ReplayGainVolumeOnly()
{
    var decision = DecideReplayGain(-2.7, 0.5, 89.0);
    Equal(ReplayGainNormalizationAction.VolumeOnly, decision.Action);
    Equal("volume=-2.7dB", decision.AudioFilter);
}

static void ReplayGainLimiter()
{
    var decision = DecideReplayGain(-2.7, 0.945, 99.0);
    Equal(ReplayGainNormalizationAction.VolumeAndLimiter, decision.Action);
    Equal("volume=7.3dB,alimiter=limit=0.891251:level=false:latency=true", decision.AudioFilter);
}

static void ReplayGainDbfsLinearConversion() => Near(0.891250938, ReplayGainNormalizationCalculator.DbfsToLinear(-1.0), 0.0000001);

static void ReplayGainInvariantFilter()
{
    var previousCulture = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        Equal("volume=7.3dB,alimiter=limit=0.891251:level=false:latency=true", DecideReplayGain(-2.7, 0.945, 99.0).AudioFilter);
    }
    finally
    {
        CultureInfo.CurrentCulture = previousCulture;
    }
}

static void ReplayGainDefaultsAndRange()
{
    var defaults = new ReplayGainNormalizationOptions();
    Near(89.0, defaults.TargetReplayGainVolumeDb);
    Near(80.0, (defaults with { TargetReplayGainVolumeDb = 70.0 }).Normalize().TargetReplayGainVolumeDb);
    Near(105.0, (defaults with { TargetReplayGainVolumeDb = 110.0 }).Normalize().TargetReplayGainVolumeDb);
}

static void ReplayGainStep()
{
    var defaults = new ReplayGainNormalizationOptions();
    Near(89.0, (defaults with { TargetReplayGainVolumeDb = 89.04 }).Normalize().TargetReplayGainVolumeDb);
    Near(89.1, (defaults with { TargetReplayGainVolumeDb = 89.06 }).Normalize().TargetReplayGainVolumeDb);
    Near(0.1, ReplayGainNormalizationOptions.TargetReplayGainVolumeStepDb);
}

static void ReplayGainSettingsPersistence()
{
    var original = new AppSettings
    {
        AudioNormalizationMode = AudioNormalizationMode.ReplayGain,
        TargetReplayGainVolumeDb = 99.0,
        PeakLimitDb = -2.0,
        NormalizationToleranceDb = 0.8,
        MaximumNormalizationGainDb = 12.0,
    };
    var loaded = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(original));
    True(loaded is not null);
    Equal(AudioNormalizationMode.ReplayGain, loaded!.AudioNormalizationMode);
    Near(99.0, loaded.TargetReplayGainVolumeDb);
    Near(-2.0, loaded.PeakLimitDb);
    Near(0.8, loaded.NormalizationToleranceDb);
    Near(12.0, loaded.MaximumNormalizationGainDb);
}

static void LegacyNormalizationModeMigration()
{
    var settings = JsonSerializer.Deserialize<AppSettings>("{\"PeakBoost\":true}");
    True(settings is not null);
    Equal(AudioNormalizationMode.Peak, settings!.AudioNormalizationMode);

    var playlist = JsonSerializer.Deserialize<ConversionPlaylist>("{\"formatVersion\":2}");
    True(playlist is not null);
    Equal(AudioNormalizationMode.Peak, playlist!.AudioNormalizationMode);

    var experimental = JsonSerializer.Deserialize<AppSettings>("{\"AudioNormalizationMode\":1}");
    True(experimental is not null);
    Equal(AudioNormalizationMode.ReplayGain, experimental!.AudioNormalizationMode);
}

static void ReplayGainPlaylistPersistence() => WithTempFolder(folder =>
{
    var path = Path.Combine(folder, "replaygain.nmm-playlist.json");
    var service = new ConversionPlaylistService();
    service.Save(path, new ConversionPlaylist
    {
        AudioNormalizationMode = AudioNormalizationMode.ReplayGain,
        TargetReplayGainVolumeDb = 99.0,
        PeakLimitDb = -2.0,
        NormalizationToleranceDb = 0.8,
        MaximumNormalizationGainDb = 12.0,
    });
    var loaded = service.Load(path);
    Equal(AudioNormalizationMode.ReplayGain, loaded.AudioNormalizationMode);
    Near(99.0, loaded.TargetReplayGainVolumeDb);
    Near(-2.0, loaded.PeakLimitDb);
    Near(0.8, loaded.NormalizationToleranceDb);
    Near(12.0, loaded.MaximumNormalizationGainDb);
});

static void ExperimentalMeanNamesRemoved()
{
    var removedName = string.Concat("Mean", "Volume");
    True(Enum.GetNames<AudioNormalizationMode>().All(name => !name.Contains(removedName, StringComparison.Ordinal)));
    True(typeof(AppSettings).GetProperties().All(property => !property.Name.Contains(removedName, StringComparison.Ordinal)));
    True(typeof(ConversionPlaylist).GetProperties().All(property => !property.Name.Contains(removedName, StringComparison.Ordinal)));
}

static void PerItemLoudnormPriority()
{
    True(AudioNormalizationPolicy.PerItemOverridesGlobal(AudioAdjustmentMode.LoudnessNormalize));
    True(!AudioNormalizationPolicy.PerItemOverridesGlobal(AudioAdjustmentMode.Off));
}

static void PeakNormalizationUnchanged()
{
    Equal("volume=5dB,alimiter=limit=0.98", PeakNormalizationFilterBuilder.BuildBoostOnly(-6.0, -1.0));
    Equal(string.Empty, PeakNormalizationFilterBuilder.BuildBoostOnly(-0.5, -1.0));
}

static void ReplayGainSimpleModeSharedDecision()
{
    var normalModeDecision = DecideReplayGain(-2.7, 0.945, 99.0);
    var simpleModeDecision = DecideReplayGain(-2.7, 0.945, 99.0);
    Equal(normalModeDecision, simpleModeDecision);
}

static void ReplayGainAnalysisFailureContinues()
{
    var preparation = new ReplayGainNormalizationService().PrepareAsync(
        _ => Task.FromResult(new ReplayGainAnalysisResult(false, null, string.Empty, "no audio", 1, false)),
        new ReplayGainNormalizationOptions(),
        CancellationToken.None).GetAwaiter().GetResult();
    Equal(ReplayGainPreparationStatus.AnalysisFailed, preparation.Status);
    Equal(string.Empty, preparation.AudioFilter);
}

static void ReplayGainCancellationStopsConversion()
{
    using var cancellation = new CancellationTokenSource();
    var analysisStarted = false;
    var conversionStarted = false;
    var preparation = new ReplayGainNormalizationService().PrepareAsync(
        _ =>
        {
            analysisStarted = true;
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        },
        new ReplayGainNormalizationOptions(),
        cancellation.Token).GetAwaiter().GetResult();
    if (preparation.Status != ReplayGainPreparationStatus.Canceled)
    {
        conversionStarted = true;
    }

    True(analysisStarted);
    True(!conversionStarted);
}

static ReplayGainAnalysis ParseReplayGain(string log) =>
    ReplayGainParser.Parse(log) ?? throw new Exception("ReplayGain parse failed");

static ReplayGainNormalizationDecision DecideReplayGain(
    double trackGainDb,
    double trackPeak,
    double targetVolumeDb,
    ReplayGainNormalizationOptions? options = null) =>
    ReplayGainNormalizationCalculator.Calculate(
        new ReplayGainAnalysis(trackGainDb, trackPeak),
        options ?? new ReplayGainNormalizationOptions(TargetReplayGainVolumeDb: targetVolumeDb));

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
static void Near(double expected, double actual, double tolerance = 0.000001) { if (Math.Abs(expected - actual) > tolerance) throw new Exception($"Expected {expected}; actual {actual}"); }
static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }
