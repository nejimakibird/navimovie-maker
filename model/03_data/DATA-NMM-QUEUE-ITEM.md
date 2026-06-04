---
type: data_object
id: DATA-NMM-QUEUE-ITEM
name: Conversion Queue Item
kind: dto
data_format: object
tags:
  - DataObject
  - NaviMovieMaker
---

# Conversion Queue Item

## Summary

`ConversionQueueItem` の主要フィールド。画面の Conversion Queue に表示され、実行中の状態と進捗を保持する。

## Fields

| name | label | type | length | required | path | ref | notes |
|---|---|---|---:|---|---|---|---|
| IsSelected | 選択 | boolean |  | N | $.IsSelected |  | 実行対象選択 |
| Order | 順序 | number |  | Y | $.Order |  | 表示順 |
| SourceType | ソース種別 | string |  | Y | $.SourceType |  | OnlineVideo / LocalFile / Unsupported |
| Title | タイトル | string |  | Y | $.Title |  | 動画名またはファイル名 |
| SourcePathOrUrl | ソースパスまたはURL | string |  | Y | $.SourcePathOrUrl |  | 入力 |
| IsSimpleModeItem | Simple Mode項目 | boolean |  | N | $.IsSimpleModeItem |  | 簡易モード実行対象 |
| DownloadedFilePath | ダウンロード済みファイル | string |  | N | $.DownloadedFilePath | [[DATA-NMM-OUTPUT-FILE]].OutputFilePath | URL 取得結果 |
| ConvertedFilePath | 変換済みファイル | string |  | N | $.ConvertedFilePath | [[DATA-NMM-OUTPUT-FILE]].OutputFilePath | 変換結果 |
| Status | 状態 | string |  | Y | $.Status |  | 待機、変換中、完了など |
| UnsupportedReason | 対象外理由 | string |  | N | $.UnsupportedReason |  | 処理不可理由 |
| ProgressPercent | 進捗率 | number |  | N | $.ProgressPercent |  | nullable |
| ProgressText | 進捗テキスト | string |  | N | $.ProgressText |  | 表示用 |
| DetailText | 詳細 | string |  | N | $.DetailText |  | サイズなど |
| SpeedText | 速度 | string |  | N | $.SpeedText |  | ダウンロードや変換速度 |
| EtaText | 残り時間 | string |  | N | $.EtaText |  | ETA |
| IsIndeterminate | 不定進捗 | boolean |  | N | $.IsIndeterminate |  | ProgressBar |
| AudioAdjustmentMode | 音声補正 | string |  | N | $.AudioAdjustmentMode |  | Off / LoudnessNormalize |

## Notes

- このデータは Conversion Queue の 1 行に表示される状態を表す。

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/Models/ConversionQueueItem.cs | キュー項目 |
| NaviMovieMaker.App/MainWindow.xaml | DataGrid 表示 |
