---
type: app_process
id: PROC-NMM-DOWNLOAD-AND-CONVERT
name: Download and Convert Queue Item
kind: business_flow
tags:
  - AppProcess
  - NaviMovieMaker
---

# Download and Convert Queue Item

## Summary

`Download & Convert` モードまたは Simple Mode で、URL は yt-dlp で一時または作業フォルダへ取得してから ffmpeg で変換する。ローカルファイルはダウンロードを省略して変換へ進む。

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/MainWindow.xaml.cs | RunDownloadAndConvertQueueItemAsync |
| NaviMovieMaker.App/Services/VideoDownloadService.cs | DownloadAsync |
| NaviMovieMaker.App/Services/VideoConversionService.cs | ConvertAsync |

## Triggers

| id | kind | source | event | notes |
|---|---|---|---|---|
| TRG-QUEUE-RUN | screen_action | [[SCR-NMM-MAIN-WINDOW]].ACT-RUN-QUEUE | click | 実行 |

## Inputs

| id | data | source | required | notes |
|---|---|---|---|---|
| IN-QUEUE-ITEM | [[DATA-NMM-QUEUE-ITEM]] | Conversion Queue | Y | OnlineVideo または LocalFile |
| IN-PRESET | [[DATA-NMM-CONVERSION-PRESET]] | [[SCR-NMM-MAIN-WINDOW]] | Y | 出力形式 |
| IN-SETTINGS | [[DATA-NMM-APP-SETTINGS]] | SettingsService | Y | フォルダと UI オプション |

## Outputs

| id | data | target | notes |
|---|---|---|---|
| OUT-DOWNLOADED | [[DATA-NMM-OUTPUT-FILE]] | WorkingFolder or TemporaryFolder | URL 取得結果 |
| OUT-CONVERTED | [[DATA-NMM-OUTPUT-FILE]] | ConvertedFolder | 変換済み |
| OUT-QUEUE-STATUS | [[DATA-NMM-QUEUE-ITEM]] | [[SCR-NMM-MAIN-WINDOW]] | 状態と進捗 |

## Steps

| id | lane | label | kind | input | output | rule | invoke | screen | notes |
|---|---|---|---|---|---|---|---|---|---|
| start | UI | キュー実行 | start | IN-QUEUE-ITEM |  |  |  | SCR-NMM-MAIN-WINDOW |  |
| ensure_tools | Controller | 必要ツールを確認 | decision | IN-SETTINGS | TOOL-STATUS |  | PROC-NMM-RESOLVE-EXTERNAL-TOOLS |  | yt-dlp と ffmpeg |
| source_branch | Controller | ソース種別を判定 | decision | IN-QUEUE-ITEM | SOURCE-KIND |  |  |  | OnlineVideo / LocalFile |
| download | Download | URL をダウンロード | subflow | IN-QUEUE-ITEM | OUT-DOWNLOADED |  |  |  | yt-dlp |
| convert_local | Conversion | 入力ファイルを変換 | subflow | IN-QUEUE-ITEM | OUT-CONVERTED |  | PROC-NMM-CONVERT-LOCAL-FILE |  | ローカルまたは取得済み |
| mark_success | Controller | 完了状態を反映 | process | OUT-CONVERTED | OUT-QUEUE-STATUS |  |  |  | Converted |
| mark_failed | Controller | 失敗状態を反映 | end | IN-QUEUE-ITEM | OUT-QUEUE-STATUS |  |  | SCR-NMM-MAIN-WINDOW | Failed / Skipped |
| end | UI | 結果を表示 | end | OUT-QUEUE-STATUS |  |  |  | SCR-NMM-MAIN-WINDOW |  |

## Flows

| from | to | condition | label | notes |
|---|---|---|---|---|
| start | ensure_tools |  | 確認 |  |
| ensure_tools | source_branch | ready | OK |  |
| ensure_tools | mark_failed | missing | NG |  |
| source_branch | download | OnlineVideo | URL |  |
| source_branch | convert_local | LocalFile | local | ダウンロード省略 |
| source_branch | mark_failed | unsupported | skip |  |
| download | convert_local | success | 変換へ |  |
| download | mark_failed | failed | NG |  |
| convert_local | mark_success | success | OK |  |
| convert_local | mark_failed | failed | NG |  |
| mark_success | end |  | 表示 |  |

## Errors

- yt-dlp または ffmpeg が不足する場合、実行前に中断される。
- キャンセル時は残り項目が Skipped になる。
