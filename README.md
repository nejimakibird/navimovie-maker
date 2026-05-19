# NaviMovie-Maker

## 1. Overview

NaviMovie-Maker is a Windows desktop app for preparing online video URLs and local video/audio files for practical playback targets such as car navigation systems, USB/SD-capable DVD players, and local media players.

The current app uses a queue-based workflow. Add URLs or local files to the `Conversion Queue`, choose an operation mode and output preset, then run the queue from top to bottom.

Online metadata and downloads use `yt-dlp`. Conversion and audio processing use FFmpeg / `ffprobe`. NaviMovie-Maker can help check, download, and configure those tools, but they remain separate external tools.

Simple Mode is planned, but it is not implemented yet. This README documents the currently implemented queue workflow.

## 2. Features

- Search YouTube by keyword or load a direct video URL.
- Drag and drop URLs into the source input or directly into the conversion queue.
- Add local video and audio files with the file picker or drag/drop.
- Process queue items with download-only, download-and-convert, convert-only, or file-copy modes.
- Convert using Car Navi MP4, DVD Player MPG, and audio-oriented presets.
- Choose aspect handling, numbering, original download retention, peak boost, and per-item audio normalization.
- Reorder, remove, clear, retry failed items, and cancel active queue work.
- Configure working, temporary, converted, and local video folders.
- Check and automatically fetch required external tools.
- Review logs and per-item status feedback.

## 3. Requirements and tool setup

NaviMovie-Maker depends on these external executables:

- `yt-dlp.exe`: required for URL metadata loading and downloading.
- `ffmpeg.exe`: required for conversion, audio extraction, and audio adjustment.
- `ffprobe.exe`: checked with FFmpeg and expected to be available from the same FFmpeg package.

The app searches for tools in this order:

1. Paths configured in Settings.
2. The app's `tools` folder.
3. The system `PATH`.

Tool support in the `ツール` menu:

- `外部ツール確認`: checks `yt-dlp`, `ffmpeg`, and `ffprobe`.
- `外部ツールを自動取得`: downloads `yt-dlp.exe` and an FFmpeg essentials package, then places `yt-dlp.exe`, `ffmpeg.exe`, and `ffprobe.exe` in the app `tools` folder.
- `tools フォルダを開く`: opens the app's external tools folder.
- Settings can manually specify executable paths and override download URLs.

## 4. Basic usage

1. Check the external tool status near the top of the window.
2. Use `外部ツール確認` or `外部ツールを自動取得` if tools are missing.
3. Add online videos by search, direct URL, URL drag/drop, or queue drag/drop.
4. Add local media files with the file picker or by dragging them into `Conversion Queue`.
5. Choose the operation mode.
6. Choose an output preset when converting.
7. Adjust aspect mode, numbering, audio options, and original-download retention as needed.
8. Click `実行` to process the queue.

Dropped URLs are added immediately and metadata loading begins. While loading, an item may show `情報取得中...`; if metadata fails, the item can remain with a warning so processing can retry later.

Supported local file extensions are:

- Video: `.mp4`, `.m4v`, `.mov`, `.avi`, `.mpg`, `.mpeg`, `.wmv`, `.mkv`, `.webm`
- Audio: `.wav`, `.mp3`, `.m4a`, `.aac`, `.flac`, `.ogg`, `.wma`

Folders are not scanned recursively in the current version. Unsupported drops are shown in the queue so the reason is visible.

## 5. Operation modes

| UI label | Internal mode | URL | Local files | Main use |
|---|---|---:|---:|---|
| ダウンロードのみ | `Download Only` | Supported | Unsupported | Download source files from URLs. |
| ダウンロードして変換 | `Download & Convert` | Supported | Supported | Download URLs and convert them; also convert supported local files already in the queue. |
| 変換のみ | `Convert Only` | Unsupported | Supported | Convert local video/audio files. |
| ファイルコピー | `Copy Files` | Unsupported | Supported | Copy files to the selected output folder. |

Items that the selected mode cannot process remain visible as unsupported queue items. For example, local files are unsupported in download-only mode, and URLs are unsupported in convert-only and file-copy modes.

## 6. Queue status display

The `状態` column combines status text and a progress bar.

- `情報取得中...`: metadata is being loaded for a dropped URL through `yt-dlp`.
- `待機中`: ready and waiting for processing.
- Processing: downloading, converting, or copying. Progress, speed, and ETA appear when the underlying tool reports them.
- `対象外`: unsupported in the current mode, unsupported file type, folder drop, missing path, or broad URL.
- Warning: metadata failed but the item may still be processed later.
- Completed: `Completed`, `Converted`, or `Downloaded`.
- Failed: `Failed` or `Convert Failed`.
- `Skipped`: skipped due to cancellation, mode mismatch, or another blocking condition.

The queue header and progress bar also show overall queue progress. Failed items can be retried with `失敗分を再実行`.

## 7. Output folders

Defaults are created under the user's Videos folder:

- Working Folder: `Videos\NaviMovie-Maker\work`
- Temporary Folder: `Videos\NaviMovie-Maker\temp`
- Converted Folder: `Videos\NaviMovie-Maker\converted`
- Local Video Folder: `Videos\NaviMovie-Maker\local`

Settings can change these folders, and missing folders are created when settings are saved.

Download-only mode writes to the Working Folder. Download-and-convert mode uses the Temporary Folder for source downloads unless `DL元を残す` is enabled. Converted files and copied files go to the Converted Folder by default, or to the current session output folder selected with `出力先...`.

When `出力形式ごとにサブフォルダを作成` is enabled, outputs are grouped into preset or mode-specific subfolders.

## 8. Presets

Visible output presets can be customized in Settings. Default visible presets include:

- `Car Navi MP4 - Current Compatibility`
- `Car Navi MP4 - Standard`
- `Car Navi MP4 - Small Size`
- `Portable DVD Player MPG - Standard (MP2 audio)`
- `Audio MP4 AAC Only - High (256 kbps)`
- `MP3 - High (320 kbps)`
- `MP3 - Medium (192 kbps)`
- `M4A AAC - High (256 kbps)`

Additional implemented presets include Car Navi MP4 and Portable DVD Player MPG quality variants, plus MP4 AAC, MP3, M4A AAC, WAV PCM, FLAC, OGG Vorbis, and WMA audio outputs.

Car Navi MP4 and DVD Player MPG presets are practical presets, not guarantees that every device will play the output. DVD Player MPG creates `.mpg` files for USB/SD playback devices; it does not author DVD-Video disc structures.

## 9. Notes and limitations

- Users are responsible for complying with copyright law and each video service's terms.
- Download availability depends on `yt-dlp` and the target site.
- Playback compatibility depends on the target device, firmware, storage, codec support, bitrate limits, and playback app.
- Car Navi MP4 and DVD Player MPG are practical starting points, not universal compatibility promises.
- `yt-dlp`, FFmpeg, and `ffprobe` are external tools. NaviMovie-Maker provides setup support, but their behavior and licenses remain separate.
- Folder drag/drop does not recursively import files.
- Broad URLs such as channels or playlists may be rejected or shown as unsupported in normal queue URL handling.
- Physical playback order on SD cards or USB drives is handled outside NaviMovie-Maker.

## 10. Development status / roadmap

Implemented:

- Queue-based workflow
- URL search, direct URL loading, and URL drag/drop
- Local file queueing
- Download, conversion, copy, retry, cancel, status, and logging
- External tool checks and built-in setup support
- Folder, preset visibility, and basic conversion settings

Planned:

- Simple Mode is planned for a future workflow, but it is not implemented yet.
