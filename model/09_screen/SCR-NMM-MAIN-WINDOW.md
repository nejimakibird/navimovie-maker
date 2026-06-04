---
type: screen
id: SCR-NMM-MAIN-WINDOW
name: NaviMovie-Maker Main Window
kind: entry
tags:
  - Screen
  - NaviMovieMaker
---

# NaviMovie-Maker Main Window

## Summary

NaviMovie-Maker の主画面。動画候補取得、URL / ローカルファイル追加、Conversion Queue の編集、出力プリセット選択、キュー実行、進捗とログ表示を行う。

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/MainWindow.xaml | 画面定義 |
| NaviMovieMaker.App/MainWindow.xaml.cs | イベント処理 |
| README-ja.md | 画面操作説明 |

## Fields

| id | label | kind | layout | data_type | required | ref | condition | rule | notes |
|---|---|---|---|---|---|---|---|---|---|
| external_tools_status | External tools | label | header | string | N | [[DATA-NMM-EXTERNAL-TOOL]] |  |  | 導入状態 |
| simple_mode | Simple Mode | checkbox | simple | boolean | N | [[DATA-NMM-APP-SETTINGS]].SimpleModeEnabled |  |  | 簡易モード |
| simple_output_preset | Simple output preset | select | simple | string | N | [[DATA-NMM-CONVERSION-PRESET]].Id |  |  | 簡易モード用 |
| simple_drop_area | Simple drop area | input | simple | string | N |  | simple_mode on |  | URL またはファイル |
| source_mode | Source mode | select | source | string | N |  |  |  | 検索 / URL指定 |
| url_text | 動画URL | input | source | string | N | [[DATA-NMM-DOWNLOAD-REQUEST]].url |  |  | URL指定 |
| search_query | 検索語 | input | source | string | N |  |  |  | YouTube検索入力 |
| search_result_count | 検索結果 | select | source | number | N |  |  |  | 10 / 20 / 50 |
| video_list | 動画ソース | grid | source | object | N |  |  |  | 候補一覧 |
| queue_execution_mode | 処理 | select | queue | string | N | [[DATA-NMM-APP-SETTINGS]].RunMode |  |  | Download Only など |
| output_preset | 出力形式 | select | queue | string | N | [[DATA-NMM-CONVERSION-PRESET]].Id |  |  | 変換プリセット |
| aspect_mode | 画面比率 | select | queue | string | N | [[DATA-NMM-APP-SETTINGS]].AspectMode | preset supports aspect |  | 比率維持など |
| keep_original_downloaded | DL元を残す | checkbox | queue | boolean | N | [[DATA-NMM-APP-SETTINGS]].KeepOriginalDownloadedFiles |  |  | URL 変換時 |
| number_prefix | 連番開始 | input | queue | number | N |  |  |  | 出力連番 |
| peak_boost | 音量持ち上げ | checkbox | audio | boolean | N | [[DATA-NMM-APP-SETTINGS]].PeakBoost |  |  | 音声補正 |
| audio_adjustment | 音声補正 | select | audio | string | N | [[DATA-NMM-QUEUE-ITEM]].AudioAdjustmentMode |  |  | なし / 音量ノーマライズ |
| target_peak | 目標ピーク | select | audio | number | N | [[DATA-NMM-APP-SETTINGS]].TargetPeakDb | peak_boost on |  | dBFS |
| conversion_queue | Conversion Queue | grid | queue | object | N | [[DATA-NMM-QUEUE-ITEM]] |  |  | 実行対象一覧 |
| queue_progress | Queue progress | label | queue | string | N | [[DATA-NMM-QUEUE-ITEM]].Status |  |  | 状態と進捗 |
| log_list | ログ | grid | log | string | N |  |  |  | AppLog 表示 |

## Actions

| id | label | kind | target | event | condition | invoke | transition | rule | notes |
|---|---|---|---|---|---|---|---|---|---|
| ACT-CHECK-EXTERNAL-TOOLS | 外部ツール確認 | submit | CheckExternalToolsMenuItem | click |  | [[PROC-NMM-RESOLVE-EXTERNAL-TOOLS]] |  |  | ツール確認 |
| ACT-INSTALL-EXTERNAL-TOOLS | 外部ツールを自動取得 | submit | InstallExternalToolsMenuItem | click |  | [[PROC-NMM-RESOLVE-EXTERNAL-TOOLS]] |  |  | 自動取得含む |
| ACT-OPEN-SETTINGS | 設定 | open | SettingsMenuItem | click |  | [[PROC-NMM-SAVE-SETTINGS]] |  |  | 設定画面 |
| ACT-FETCH-VIDEO-LIST | URL読込 | submit | FetchVideoListButton | click |  | [[PROC-NMM-ADD-FILE-TO-QUEUE]] |  |  | 候補取得は yt-dlp |
| ACT-ADD-SELECTED-TO-QUEUE | 選択項目をキューへ追加 | submit | AddSelectedToQueueButton | click |  | [[PROC-NMM-ADD-FILE-TO-QUEUE]] |  |  | 候補から追加 |
| ACT-ADD-LOCAL-FILES | ローカルファイル追加 | submit | AddLocalFilesButton | click/drop |  | [[PROC-NMM-ADD-FILE-TO-QUEUE]] |  |  | ファイル選択または Drop |
| ACT-ADD-URL-TO-QUEUE | URLをキューへ追加 | submit | ConversionQueueDataGrid | drop |  | [[PROC-NMM-ADD-FILE-TO-QUEUE]] |  |  | ドロップテキスト |
| ACT-APPLY-AUDIO-ADJUSTMENT | 選択項目に適用 | submit | ApplyAudioAdjustmentButton | click |  |  |  |  | キュー項目に音声補正 |
| ACT-APPLY-PRESET | 出力形式選択 | select | OutputPresetComboBox | change |  | [[PROC-NMM-APPLY-PRESET]] |  |  | 選択プリセット保存 |
| ACT-RUN-QUEUE | 実行 | submit | ConvertQueueButton | click | queue has items | [[PROC-NMM-DOWNLOAD-AND-CONVERT]] |  |  | モード別に分岐 |
| ACT-RETRY-FAILED | 失敗分を再実行 | submit | RetryFailedQueueButton | click | failed items exist | [[PROC-NMM-DOWNLOAD-AND-CONVERT]] |  |  | 失敗項目のみ |
| ACT-CANCEL-QUEUE | キャンセル | submit | CancelQueueButton | click | processing |  |  |  | CancellationToken |
| ACT-COPY-ONLY | ファイルコピー | submit | ConvertQueueButton | click | mode Copy Files | [[PROC-NMM-COPY-ONLY]] |  |  | ローカルファイル |

## Messages

| id | text | severity | timing | condition | notes |
|---|---|---|---|---|---|
| MSG-TOOLS-MISSING | 必要な外部ツールが不足しています | warning | before_process | missing tools | 実行前確認 |
| MSG-QUEUE-UNSUPPORTED | 対象外 | warning | queue_update | unsupported item | SourceType / mode 不一致 |
| MSG-FETCH-FAILED | yt-dlp failed | error | fetch_result | fetch failed | ログ出力 |
| MSG-CONVERSION-FAILED | Failed | error | queue_result | conversion failed | キュー状態 |
| MSG-READY | 準備完了 | info | load |  | 初期表示 |

## Notes

- 画面定義は `MainWindow.xaml` に集約されている。
- `SettingsWindow` は別ファイルに存在するが、今回の必須 Screen では Main Window から呼ばれる設定保存フローとして扱った。
- `DownloadSelectedButton` と `ConvertDownloadedButton` は候補一覧からの旧来操作として残っているが、主要モデルは Conversion Queue の一括処理を中心にした。
