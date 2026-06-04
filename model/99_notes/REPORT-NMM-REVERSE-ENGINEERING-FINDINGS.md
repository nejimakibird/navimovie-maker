---
id: REPORT-NMM-REVERSE-ENGINEERING-FINDINGS
name: NaviMovie-Maker Reverse Engineering Findings
tags:
  - NaviMovieMaker
  - ReverseEngineering
  - ModelWeave
---

# NaviMovie-Maker Reverse Engineering Findings

## Summary

NaviMovie-Maker のソース、README、release docs、Model Weave format docs だけを参照して、初回の Model Weave モデルセットを生成した。

## Source Files Inspected

| path | notes |
|---|---|
| README.md | 英語概要 |
| README-ja.md | 機能、使い方、外部ツール、設定 |
| release-docs/README.txt | 配布向け説明 |
| release-docs/CHANGELOG.txt | リリース文書 |
| NaviMovieMaker.App/NaviMovieMaker.App.csproj | WPF / net10.0-windows |
| NaviMovieMaker.App/MainWindow.xaml | 主画面 |
| NaviMovieMaker.App/MainWindow.xaml.cs | UI 制御、キュー、実行分岐 |
| NaviMovieMaker.App/SettingsWindow.xaml.cs | 設定保存 |
| NaviMovieMaker.App/Models/AppSettings.cs | 設定データ |
| NaviMovieMaker.App/Models/ConversionQueueItem.cs | キュー項目 |
| NaviMovieMaker.App/Models/VideoListItem.cs | 動画候補 |
| NaviMovieMaker.App/Services/SettingsService.cs | settings.json |
| NaviMovieMaker.App/Services/ExternalToolService.cs | 外部ツール確認と取得 |
| NaviMovieMaker.App/Services/VideoMetadataService.cs | yt-dlp JSON 取得 |
| NaviMovieMaker.App/Services/VideoDownloadService.cs | yt-dlp ダウンロード |
| NaviMovieMaker.App/Services/VideoConversionService.cs | ffmpeg 変換 |
| NaviMovieMaker.App/Services/ConversionPresetCatalog.cs | 出力プリセット |
| NaviMovieMaker.App/Services/DownloadProfileCatalog.cs | ダウンロードプロファイル |

## Generated Model Coverage

| area | model | notes |
|---|---|---|
| DFD | [[DFD-NMM-CORE-L0]] | 実行時境界 |
| DFD object | DFD-NMM-OBJ-* | UI、Controller、Queue、Services、Tools |
| DATA | DATA-NMM-* | 設定、キュー、プリセット、ジョブ、結果 |
| Process | PROC-NMM-* | 追加、変換、コピー、設定、ツール解決 |
| Screen | [[SCR-NMM-MAIN-WINDOW]] | 主画面 |

## Architecture Summary

NaviMovie-Maker は WPF デスクトップアプリで、`MainWindow.xaml.cs` が画面イベントとアプリケーション制御を担当する。永続設定は `SettingsService` が JSON として保存する。動画候補取得と URL ダウンロードは yt-dlp、変換と音声解析は ffmpeg、外部ツール確認は yt-dlp / ffmpeg / ffprobe を対象にする。キューはメモリ上の `ObservableCollection<ConversionQueueItem>` として管理され、状態と進捗が DataGrid に反映される。

## Implemented Workflows Found

| workflow | source evidence |
|---|---|
| ファイルと URL のキュー追加 | `AddLocalFilesToQueue` / `AddOnlineUrlsToQueueAsync` |
| URL ダウンロード後変換 | `RunDownloadAndConvertQueueItemAsync` / `VideoDownloadService` |
| ローカルファイル変換 | `ConvertQueueItemAsync` / `VideoConversionService` |
| ファイルコピー | `CopyFileWithProgressAsync` |
| プリセット適用 | `GetSelectedConversionPreset` / `ConversionPresetCatalog` |
| 外部ツール解決 | `ExternalToolService.CheckAllAsync` |
| 設定保存 | `SettingsWindow.SaveButton_Click` / `SettingsService.Save` |

## Uncertain or Deferred Areas

- `ffprobe` は確認対象だが、読み取った範囲では主処理の直接呼び出しは見つけていない。
- `VideoListItem` ベースの候補一覧からの個別ダウンロード / 変換は存在するが、今回のプロセスモデルは Conversion Queue 中心にした。
- SettingsWindow 自体の screen model は、必須範囲外のため別画面としては作成していない。
- リリース文書は README と重複する内容が多く、主な根拠は README-ja とソースコードに置いた。

## Recommended Next Improvements

- `SettingsWindow` の screen model を追加する。
- 個別ダウンロード操作と候補一覧操作を別 process として分離する。
- 音声補正の peak boost / loudnorm を詳細 process 化する。
- 実際に Model Weave preview に通して図の読みやすさを調整する。

## Model Weave Effectiveness

Model Weave はこのアプリの初期理解に有効だった。特に、`MainWindow.xaml.cs` に集中している UI 制御を DFD と app_process に分けることで、キュー、サービス、外部ツール、ファイルシステムの境界が読みやすくなった。

## Validation Notes

- frontmatter の `type` は正式モデルにだけ付与した。
- DFD の `Flows.from` / `Flows.to` はローカル `Objects.id` に揃えた。
- app_process の `Flows.from` / `Flows.to` は各ファイル内 `Steps.id` に揃えた。
- テーブル内で Wikilink alias は使用していない。
- テーブル内で C# / TypeScript 風の配列表記は使わず、`string list` を使用した。
- 生成先は `model/` 配下のみ。
