# NaviMovie-Maker

NaviMovie-Maker is a Windows app for converting online videos and local video files into formats intended for car navigation systems, USB/SD-capable DVD players, and audio-only use.

For installation, usage, presets, external tool setup, and important copyright/site-terms notes, see the detailed Japanese guide:

- [README-ja.md](README-ja.md)

NaviMovie-Maker is available from official distribution sources such as GitHub Releases and Vector. When downloading from GitHub Releases, choose the app ZIP package. Do not download the automatically generated `Source code` archive if you only want to run the app.

NaviMovie-Maker is currently unsigned. Windows SmartScreen or Smart App Control may show a warning even when downloaded from official distribution sources such as GitHub Releases or Vector.

The Playlist menu saves the current conversion queue and relevant conversion settings as a `.nmm-playlist.json` file. In NaviMovie-Maker, a playlist is a reusable conversion job list, not a media playback playlist. Loading one restores items for editing and conversion; completed or in-progress runtime states are reset.

Playlist shortcuts follow standard document editing: `Ctrl+N` creates a new playlist, `Ctrl+O` opens one, `Ctrl+S` saves (overwriting the current file after the first save), and `Ctrl+Shift+S` opens Save As. Unsaved changes are marked with `*` in the window title and confirmed before replacing the playlist or exiting.
