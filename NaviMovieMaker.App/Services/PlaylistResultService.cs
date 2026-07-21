using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NaviMovieMaker.App.Services;

public sealed class PlaylistResultService
{
    private static readonly Regex SequencePrefixPattern = new(@"^(?<number>\d{3})_", RegexOptions.Compiled);

    public PlaylistSourceIdentity CreateSourceIdentity(ConversionQueueItem item)
    {
        if (string.Equals(item.SourceType, "LocalFile", StringComparison.OrdinalIgnoreCase))
        {
            var path = Path.GetFullPath(item.SourcePathOrUrl);
            var file = new FileInfo(path);
            return new PlaylistSourceIdentity
            {
                Kind = "LocalFile",
                Location = path,
                FileSize = file.Exists ? file.Length : null,
                LastWriteTimeUtc = file.Exists ? file.LastWriteTimeUtc : null,
            };
        }

        return new PlaylistSourceIdentity
        {
            Kind = "Url",
            Location = NormalizeUrl(item.SourcePathOrUrl),
        };
    }

    public void RecordSuccessfulResult(
        ConversionQueueItem item,
        string resultPath,
        string operationMode,
        string profileId,
        int? sequenceNumber)
    {
        var fullPath = Path.GetFullPath(resultPath);
        var file = new FileInfo(fullPath);
        if (!file.Exists) throw new FileNotFoundException("処理結果ファイルが見つかりません。", fullPath);
        item.Result = new PlaylistResultRecord
        {
            SourceIdentity = CreateSourceIdentity(item),
            OperationMode = operationMode,
            ProfileId = profileId,
            ResultFilePath = fullPath,
            ResultFileName = file.Name,
            FileSize = file.Length,
            LastWriteTimeUtc = file.LastWriteTimeUtc,
            GeneratedAtUtc = DateTime.UtcNow,
            SequenceNumber = sequenceNumber,
        };
        ApplyResultPath(item);
        SetState(item, PlaylistResultState.Available, "追跡済みの処理結果を利用できます。");
    }

    public PlaylistResultState Reconcile(
        ConversionQueueItem item,
        string operationMode,
        string profileId,
        int? expectedSequenceNumber)
    {
        var result = item.Result;
        if (result is null)
        {
            return SetState(item, PlaylistResultState.Unprocessed, "追跡された処理結果がありません。");
        }

        if (!IdentityEquals(result.SourceIdentity, CreateSourceIdentity(item))
            || !string.Equals(result.OperationMode, operationMode, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(result.ProfileId, profileId, StringComparison.OrdinalIgnoreCase))
        {
            return SetState(item, PlaylistResultState.NeedsReprocess, "ソース、処理モード、またはプロファイルが変更されています。");
        }

        if (!File.Exists(result.ResultFilePath))
        {
            return SetState(item, PlaylistResultState.Missing, "追跡された出力ファイルが見つかりません。");
        }

        var file = new FileInfo(result.ResultFilePath);
        if (file.Length != result.FileSize || file.LastWriteTimeUtc != result.LastWriteTimeUtc)
        {
            return SetState(item, PlaylistResultState.Modified, "出力ファイルのサイズまたは更新日時が変更されています。");
        }

        ApplyResultPath(item);
        if (expectedSequenceNumber is not null && !HasSequencePrefix(file.Name, expectedSequenceNumber.Value))
        {
            return SetState(item, PlaylistResultState.SequenceOutOfSync, "出力ファイルの連番が現在のキュー順と一致しません。");
        }

        return SetState(item, PlaylistResultState.Available, "追跡済みの処理結果を利用できます。");
    }

    public string ResolveCollisionSafePath(ConversionQueueItem item, string folder, string desiredStem, string extension)
    {
        var desiredPath = Path.Combine(folder, desiredStem + extension);
        if (!File.Exists(desiredPath) || CanReuseRecordedResultPath(item, desiredPath)) return desiredPath;

        var suffix = GetIdentitySuffix(CreateSourceIdentity(item));
        var distinctStem = $"{desiredStem}_{suffix}";
        var distinctPath = Path.Combine(folder, distinctStem + extension);
        if (!File.Exists(distinctPath) || CanReuseRecordedResultPath(item, distinctPath)) return distinctPath;

        for (var index = 2; ; index++)
        {
            var nextPath = Path.Combine(folder, $"{distinctStem}_{index}{extension}");
            if (!File.Exists(nextPath) || CanReuseRecordedResultPath(item, nextPath)) return nextPath;
        }
    }

    public string GetStableCollisionSuffix(ConversionQueueItem item)
    {
        var itemSuffix = item.ItemId[..Math.Min(6, item.ItemId.Length)];
        return $"{GetIdentitySuffix(CreateSourceIdentity(item))}_{itemSuffix}";
    }

    public IReadOnlyList<SequenceRename> BuildSequenceRenames(IEnumerable<ConversionQueueItem> items, int sequenceStart)
    {
        var renames = new List<SequenceRename>();
        foreach (var item in items.OrderBy(static item => item.Order))
        {
            if (item.ResultState != PlaylistResultState.SequenceOutOfSync || item.Result is null) continue;
            var expected = sequenceStart + item.Order - 1;
            var oldPath = item.Result.ResultFilePath;
            var directory = Path.GetDirectoryName(oldPath)!;
            var name = Path.GetFileName(oldPath);
            var stemWithoutPrefix = SequencePrefixPattern.Replace(Path.GetFileNameWithoutExtension(name), string.Empty);
            var newPath = Path.Combine(directory, $"{expected:000}_{stemWithoutPrefix}{Path.GetExtension(name)}");
            renames.Add(new SequenceRename(item, oldPath, newPath, expected));
        }
        return renames;
    }

    public void ApplySequenceRenames(IReadOnlyList<SequenceRename> renames)
    {
        if (renames.Count == 0) return;
        var sourcePaths = renames.Select(static rename => Path.GetFullPath(rename.OldPath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var rename in renames)
        {
            if (File.Exists(rename.NewPath) && !sourcePaths.Contains(Path.GetFullPath(rename.NewPath)))
            {
                SetState(rename.Item, PlaylistResultState.NameConflict, $"同期先に別のファイルがあります: {rename.NewPath}");
                throw new IOException($"同期先に別のファイルがあります: {rename.NewPath}");
            }
        }

        var staged = new List<(SequenceRename Rename, string TemporaryPath)>();
        var completed = new List<SequenceRename>();
        try
        {
            foreach (var rename in renames)
            {
                var temporaryPath = Path.Combine(Path.GetDirectoryName(rename.OldPath)!, $".{Guid.NewGuid():N}.nmm-rename.tmp");
                File.Move(rename.OldPath, temporaryPath);
                staged.Add((rename, temporaryPath));
            }
            foreach (var entry in staged)
            {
                File.Move(entry.TemporaryPath, entry.Rename.NewPath);
                completed.Add(entry.Rename);
                UpdateAfterRename(entry.Rename);
            }
        }
        catch
        {
            foreach (var rename in completed.AsEnumerable().Reverse())
            {
                try { if (File.Exists(rename.NewPath) && !File.Exists(rename.OldPath)) File.Move(rename.NewPath, rename.OldPath); } catch { }
            }
            foreach (var entry in staged.Where(static entry => File.Exists(entry.TemporaryPath)))
            {
                try { if (!File.Exists(entry.Rename.OldPath)) File.Move(entry.TemporaryPath, entry.Rename.OldPath); } catch { }
            }
            throw;
        }
    }

    private static void UpdateAfterRename(SequenceRename rename)
    {
        var result = rename.Item.Result!;
        var file = new FileInfo(rename.NewPath);
        result.ResultFilePath = file.FullName;
        result.ResultFileName = file.Name;
        result.SequenceNumber = rename.SequenceNumber;
        result.FileSize = file.Length;
        result.LastWriteTimeUtc = file.LastWriteTimeUtc;
        rename.Item.ConvertedFilePath = file.FullName;
        rename.Item.ResultState = PlaylistResultState.Available;
        rename.Item.ResultStateReason = "連番を同期しました。";
    }

    private static PlaylistResultState SetState(ConversionQueueItem item, PlaylistResultState state, string reason)
    {
        item.ResultState = state;
        item.ResultStateReason = reason;
        return state;
    }

    private static void ApplyResultPath(ConversionQueueItem item)
    {
        if (item.Result is null) return;
        if (string.Equals(item.Result.OperationMode, "Download Only", StringComparison.OrdinalIgnoreCase))
            item.DownloadedFilePath = item.Result.ResultFilePath;
        else
            item.ConvertedFilePath = item.Result.ResultFilePath;
    }

    private static bool IdentityEquals(PlaylistSourceIdentity left, PlaylistSourceIdentity right) =>
        string.Equals(left.Kind, right.Kind, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Location, right.Location, StringComparison.OrdinalIgnoreCase)
        && left.FileSize == right.FileSize
        && left.LastWriteTimeUtc == right.LastWriteTimeUtc;

    public bool CanReuseRecordedResultPath(ConversionQueueItem item, string candidatePath)
    {
        var result = item.Result;
        if (result is null
            || !string.Equals(
                Path.GetFullPath(result.ResultFilePath),
                Path.GetFullPath(candidatePath),
                StringComparison.OrdinalIgnoreCase)
            || !IdentityEquals(result.SourceIdentity, CreateSourceIdentity(item)))
        {
            return false;
        }

        var file = new FileInfo(candidatePath);
        return file.Exists
            && file.Length == result.FileSize
            && file.LastWriteTimeUtc == result.LastWriteTimeUtc;
    }

    private static bool HasSequencePrefix(string fileName, int expected) =>
        SequencePrefixPattern.Match(fileName) is { Success: true } match
        && int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
        && number == expected;

    private static string NormalizeUrl(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)) return value.Trim();
        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        return builder.Uri.AbsoluteUri;
    }

    private static string GetIdentitySuffix(PlaylistSourceIdentity identity)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{identity.Kind}|{identity.Location}|{identity.FileSize}|{identity.LastWriteTimeUtc:O}"));
        return Convert.ToHexString(bytes)[..6].ToLowerInvariant();
    }

}

public sealed record SequenceRename(ConversionQueueItem Item, string OldPath, string NewPath, int SequenceNumber);
