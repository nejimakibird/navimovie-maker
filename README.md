# NaviMovie-Maker

## 1. Overview

NaviMovie-Maker is a Windows desktop app for preparing online video URLs and local video/audio files for practical playback targets such as car navigation systems, USB/SD-capable DVD players, iPad/tablet devices, Android tablets, and local media players.

The app is centered around the `Conversion Queue`. You can use Simple Mode for a minimal drop-and-convert workflow, or normal mode for explicit download, convert, and copy operations.

Online metadata and downloads use `yt-dlp`. Conversion and audio processing use FFmpeg / `ffprobe`. NaviMovie-Maker can check, download, and configure those tools, but they remain separate external tools.

## 2. Features

- Simple Mode: drop URLs or local files and convert them with the selected preset.
- Normal mode: download only, download and convert, convert local files, or copy files.
- Search YouTube by keyword or load direct video URLs.
- Drag URLs into the source input or directly into the conversion queue.
- Add local video and audio files with the file picker or drag/drop.
- Convert using Car Navi MP4, DVD Player MPG, iPad/tablet, Android tablet, and audio presets.
- Per-item queue status, progress bars, overall progress, and logs.
- Audio correction, peak boost, numbering, and original-download retention.
- External tool checking, automatic setup support, manual tool paths, and tools-folder access.

## 3. Simple Mode

Simple Mode is a minimal workflow for converting dropped URLs or local files with the selected output preset. URL items are downloaded to a temporary location and then converted. Local files are converted directly. Final output files are written to the Converted Folder.

Simple Mode mainly exposes the output preset selector. It does not show the normal-mode operation choices such as download only, download and convert, convert only, or file copy.

During Simple Mode processing, items remain visible in the `Conversion Queue`. Progress is shown in the queue row, the overall progress display, and the log. Use the Simple Mode `キャンセル` button to stop active processing.

Simple Mode is designed not to leave temporary downloaded files, cache files, or intermediate files behind as normal results.

## 4. Normal Mode

Normal mode processes queue items from top to bottom using the selected operation mode.

| UI label | Internal mode | URL | Local files | Main use |
|---|---|---:|---:|---|
| ダウンロードのみ | `Download Only` | Supported | Unsupported | Download source files from URLs. |
| ダウンロードして変換 | `Download & Convert` | Supported | Supported | Download URLs and convert them; also convert supported local files in the queue. |
| 変換のみ | `Convert Only` | Unsupported | Supported | Convert local video/audio files. |
| ファイルコピー | `Copy Files` | Unsupported | Supported | Copy files to the selected output folder. |

Items that the selected mode cannot process remain visible as `対象外` unsupported queue items.

## 5. Adding URLs and Files

HTTP / HTTPS URLs can be dropped into the source input or directly into the `Conversion Queue`. Queue-dropped URLs appear immediately, then metadata loading starts through `yt-dlp`.

While metadata is loading, the item can show `読み込み中...` and `情報取得中...`. When metadata succeeds, the video title is used for display and output filename suggestions. If metadata fails, the item remains visible with a safe fallback title and warning status so processing can retry later.

YouTube single-video URLs, `youtu.be/...`, and Shorts URLs are treated as single videos. URLs such as `watch?v=...&list=...` are normalized to the single video when a video ID is present. Channel, playlist, handle, radio, and other broad URLs may be rejected or marked unsupported in normal operation to avoid unintended large downloads.

Supported local file extensions:

- Video: `.mp4`, `.m4v`, `.mov`, `.avi`, `.mpg`, `.mpeg`, `.wmv`, `.mkv`, `.webm`
- Audio: `.wav`, `.mp3`, `.m4a`, `.aac`, `.flac`, `.ogg`, `.wma`

Folders, missing paths, and unsupported extensions remain visible as unsupported queue items. Folder drops are not imported recursively.

## 6. Queue Status and Progress

The `状態` column shows status text with a progress bar. During conversion, NaviMovie-Maker shows percent, converted time / total duration, processing speed, and estimated remaining time when available. This helps confirm progress during long videos or high-resolution conversions.

Common statuses:

- `情報取得中...`: loading metadata for a dropped URL.
- `待機中`: ready and waiting.
- Processing / downloading / converting: active work is running.
- `対象外`: unsupported in the current mode, unsupported file type, broad URL, or similar.
- Warning / `注意`: metadata failed or the item needs attention but may still be processable.
- Completed / `完了`: processing succeeded.
- Failed / `失敗`: download, conversion, or copy failed.
- `Skipped`: skipped due to cancellation, mode mismatch, or an earlier failure.

The overall progress bar includes both completed items and the current item's partial progress.

## 7. Cancellation and Deletion

Active queue items cannot be removed while metadata loading, downloading, converting, processing, or canceling is still in progress. This keeps the visible queue state aligned with the underlying task. Use the `キャンセル` button to stop active processing.

The Delete key and delete button both protect active items. Waiting, unsupported, warning, completed, failed, and no-longer-active canceled items can be removed normally.

Queue reordering may also be disabled while processing is active.

## 8. Audio Correction

The `音声補正` column shows the audio correction setting applied to each queue item. Normal mode can apply per-item audio correction such as loudness normalization.

Peak boost is intended to raise quiet sources toward the selected target peak without lowering already-loud sources. Advanced pre-analysis gain adjustment similar to dedicated transcoding tools is not documented as an implemented feature yet.

## 9. Output Presets

Visible presets can be customized in Settings. Simple Mode and normal mode use the shared preset list.

### Car Navi MP4

- `Car Navi MP4 - Current Compatibility`
- `Car Navi MP4 - Standard`
- `Car Navi MP4 - Small Size`
- `Car Navi MP4 - High Quality`

Practical MP4 / H.264 / AAC presets for car navigation playback. They are not universal compatibility guarantees.

### DVD Player MPG

- `Portable DVD Player MPG - Small Size (MP2 audio)`
- `Portable DVD Player MPG - Standard (MP2 audio)`
- `Portable DVD Player MPG - High Quality (MP2 audio)`

These create `.mpg` files for USB/SD-capable DVD players. They do not author DVD-Video disc structures.

### iPad / Tablet

- `iPad / タブレット MP4 1080p 標準`: H.264 MP4 for current/common tablet devices.
- `iPad / タブレット MP4 720p 互換`: H.264 MP4 for older devices or smaller files.
- `iPad / タブレット HEVC 1080p 高圧縮`: HEVC MP4 for newer devices and smaller files.

### Android Tablet

- `Androidタブレット MP4 1080p 標準`: H.264 MP4 for current/common Android tablets.
- `Androidタブレット MP4 720p 互換`: H.264 MP4 for older devices or smaller files.
- `Androidタブレット HEVC 1080p 高圧縮`: HEVC MP4 for newer devices and smaller files.

H.264 MP4 presets are the safer compatibility choice. HEVC presets are high-compression presets for newer devices and may not play on older devices or apps.

### Audio Presets

Audio output presets include MP4 AAC, MP3, M4A AAC, WAV PCM, FLAC, OGG Vorbis, and WMA variants.

## 10. Folders and Output

Default folders are created under the user's Videos folder:

- Working Folder: `Videos\NaviMovie-Maker\work`
- Temporary Folder: `Videos\NaviMovie-Maker\temp`
- Converted Folder: `Videos\NaviMovie-Maker\converted`
- Local Video Folder: `Videos\NaviMovie-Maker\local`

Settings can change these folders. When `出力形式ごとにサブフォルダを作成` is enabled, output is grouped into preset or mode-specific subfolders.

Simple Mode writes final files to the Converted Folder. Normal mode can also use the session output folder selected with `出力先...`.

## 11. External Tools

NaviMovie-Maker uses:

- `yt-dlp.exe` for URL metadata and downloads.
- `ffmpeg.exe` for video/audio conversion and audio processing.
- `ffprobe.exe` for FFmpeg-related checks and media information.

The `ツール` menu provides:

- `外部ツール確認`: check configured paths, the app `tools` folder, then `PATH`.
- `外部ツールを自動取得`: download `yt-dlp.exe` and an FFmpeg essentials package, then place the executables in the `tools` folder.
- `tools フォルダを開く`: open the app tools folder.
- Settings: manually specify executable paths and download URLs.

Download availability depends on `yt-dlp` and the target site. Site changes, terms, regional limits, login requirements, or access restrictions may prevent downloads.

## 12. Notes and Limitations

- Users are responsible for complying with copyright law, video service terms, and rights-holder permissions.
- NaviMovie-Maker does not encourage saving, converting, or redistributing content without the necessary rights.
- Playback compatibility depends on device codec support, resolution, bitrate, file size limits, storage format, firmware, and playback app.
- Presets are practical starting points, not guarantees for every device.
- HEVC is high-compression but less broadly compatible than H.264.
- `yt-dlp`, FFmpeg, and `ffprobe` are external tools with their own behavior, licenses, and release cycles.
- Folder drag/drop does not recursively import files.
- Playlist batch download and full channel download are not implemented in normal operation.
- Hardware GPU encoding is not treated as a default implemented feature.
- Physical playback order on SD cards or USB drives is handled outside NaviMovie-Maker.

## 13. Development Status

Implemented:

- Simple Mode
- Queue-based normal workflow
- URL search, direct URL loading, and URL drag/drop
- Local file queueing
- Download, conversion, copy, retry, cancel, status, and logging
- Detailed queue progress, unsupported-item display, and warning display
- External tool checks and built-in setup support
- Folder, preset visibility, and basic conversion settings

Not implemented or future work:

- Pro Mode
- Playlist batch download
- Full channel download
- Advanced pre-analysis audio gain adjustment
