---
type: data_object
id: DATA-NMM-APP-SETTINGS
name: NaviMovie-Maker App Settings
kind: dto
data_format: json
tags:
  - DataObject
  - NaviMovieMaker
---

# NaviMovie-Maker App Settings

## Summary

`AppSettings` として保持され、`SettingsService` により `%APPDATA%/settings.json` に保存される設定。

## Fields

| name | label | type | length | required | path | ref | notes |
|---|---|---|---:|---|---|---|---|
| WorkingFolder | 作業フォルダ | string |  | Y | $.WorkingFolder |  | ダウンロード元保存先 |
| TemporaryFolder | 一時フォルダ | string |  | Y | $.TemporaryFolder |  | 一時ダウンロード先 |
| ConvertedFolder | 変換済みフォルダ | string |  | Y | $.ConvertedFolder |  | 出力先 |
| LocalVideoFolder | ローカル動画フォルダ | string |  | Y | $.LocalVideoFolder |  | ファイル選択初期位置 |
| CreateSubfolderPerOutputPreset | プリセット別サブフォルダ | boolean |  | N | $.CreateSubfolderPerOutputPreset |  | 出力先分岐 |
| DownloadProfile | ダウンロードプロファイル | string |  | N | $.DownloadProfile |  | DownloadProfileCatalog |
| RunMode | 実行モード | string |  | N | $.RunMode |  | キュー実行モード |
| OutputPresetId | 出力プリセットID | string |  | N | $.OutputPresetId | [[DATA-NMM-CONVERSION-PRESET]].Id | 選択プリセット |
| AspectMode | 画面比率 | string |  | N | $.AspectMode |  | 比率維持または引き伸ばし |
| KeepOriginalDownloadedFiles | DL元を残す | boolean |  | N | $.KeepOriginalDownloadedFiles |  | 作業フォルダ選択に影響 |
| PeakBoost | 音量持ち上げ | boolean |  | N | $.PeakBoost |  | 音声補正 |
| SimpleModeEnabled | Simple Mode | boolean |  | N | $.SimpleModeEnabled |  | 簡易モード |
| TargetPeakDb | 目標ピーク | number |  | N | $.TargetPeakDb |  | 既定は -1.0 |
| StartupLayout | 起動レイアウト | string |  | N | $.StartupLayout |  | QueueFocus など |
| YtDlpPath | yt-dlp パス | string |  | N | $.YtDlpPath | [[DATA-NMM-EXTERNAL-TOOL]].ExecutablePath | 手動指定 |
| FfmpegPath | ffmpeg パス | string |  | N | $.FfmpegPath | [[DATA-NMM-EXTERNAL-TOOL]].ExecutablePath | 手動指定 |
| FfprobePath | ffprobe パス | string |  | N | $.FfprobePath | [[DATA-NMM-EXTERNAL-TOOL]].ExecutablePath | 手動指定 |
| VisibleOutputPresetIds | 表示プリセットID | string list |  | N | $.VisibleOutputPresetIds | [[DATA-NMM-CONVERSION-PRESET]].Id | 複数値 |
| KnownOutputPresetIds | 既知プリセットID | string list |  | N | $.KnownOutputPresetIds | [[DATA-NMM-CONVERSION-PRESET]].Id | 複数値 |

## Notes

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/Models/AppSettings.cs | 設定クラス |
| NaviMovieMaker.App/Services/SettingsService.cs | JSON 読み書き |
