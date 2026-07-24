# NaviMovie-Maker

NaviMovie-Maker is a Windows app for converting online videos and local video files into formats intended for car navigation systems, USB/SD-capable DVD players, and audio-only use.

For installation, usage, presets, external tool setup, and important copyright/site-terms notes, see the detailed Japanese guide:

- [README-ja.md](README-ja.md)

NaviMovie-Maker is available from official distribution sources such as GitHub Releases and Vector. When downloading from GitHub Releases, choose the app ZIP package. Do not download the automatically generated `Source code` archive if you only want to run the app.

NaviMovie-Maker is currently unsigned. Windows SmartScreen or Smart App Control may show a warning even when downloaded from official distribution sources such as GitHub Releases or Vector.

The Playlist menu saves the current conversion queue and relevant conversion settings as a `.nmm-playlist.json` file. In NaviMovie-Maker, a playlist is a reusable conversion job list, not a media playback playlist. Loading one restores items for editing and conversion; completed or in-progress runtime states are reset.

Playlist shortcuts follow standard document editing: `Ctrl+N` creates a new playlist, `Ctrl+O` opens one, `Ctrl+S` saves (overwriting the current file after the first save), and `Ctrl+Shift+S` opens Save As. Unsaved changes are marked with `*` in the window title and confirmed before replacing the playlist or exiting.

Each playlist retains its own selected output folder, so reusable jobs can target different destinations independently.

Audio gain normalization can use either the existing peak-based mode or the new ReplayGain-based `Volume normalization (dB)` mode. The ReplayGain mode uses an XMedia Recode-like 89.0 dB reference, accepts 80.0–105.0 dB in 0.1 steps, applies peak limiting when needed, works in both normal and Simple Mode, and continues conversion without normalization if analysis fails. Per-item loudnorm settings take priority, and FFmpeg results may not exactly match XMedia Recode.

Playlist items persist stable row IDs and verified successful-result metadata. The Result column distinguishes available, missing, modified, out-of-sequence, conflicting, and reprocessing states. Valid existing results are skipped on subsequent processing runs, while unknown files are never claimed or overwritten. Numbered tracked results can have only their sequence prefixes synchronized after reordering.

Normal outputs now use natural names such as `Video title.mp4` or `001_Video title.mp4`. A stable collision identifier is added only when the basic name actually conflicts with an unmanaged file or another source result. Existing files are not overwritten, and previously tracked identifier-suffixed results remain valid.

Playlist preview uses the separate third-party [mpv](https://mpv.io/) application; it is not bundled, and NaviMovie-Maker does not embed a media player. Select `mpv.exe` under Tools > Settings > External Tools. `Ctrl+P` plays all verified results when every playable item has one; otherwise it plays the original local files and URLs supported by mpv/yt-dlp in queue order without mixing sources and results.
