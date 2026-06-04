---
type: data_object
id: DATA-NMM-CONVERSION-JOB
name: Conversion Job
kind: dto
data_format: object
tags:
  - DataObject
  - NaviMovieMaker
---

# Conversion Job

## Summary

キュー項目、入力ファイル、出力ファイル、プリセット、音声補正、キャンセル状態を合わせた変換実行単位。

## Fields

| name | label | type | length | required | path | ref | notes |
|---|---|---|---:|---|---|---|---|
| queueItem | キュー項目 | object |  | Y | $.queueItem | [[DATA-NMM-QUEUE-ITEM]] | 実行対象 |
| inputFilePath | 入力ファイル | string |  | Y | $.inputFilePath |  | ダウンロード済みまたはローカル |
| outputFilePath | 出力ファイル | string |  | Y | $.outputFilePath | [[DATA-NMM-OUTPUT-FILE]].OutputFilePath | 生成先 |
| preset | 変換プリセット | object |  | Y | $.preset | [[DATA-NMM-CONVERSION-PRESET]] | 選択出力形式 |
| aspectMode | 画面比率 | string |  | N | $.aspectMode |  | 変換フィルタ |
| audioFilter | 音声フィルタ | string |  | N | $.audioFilter |  | loudnorm など |
| cancellationToken | キャンセル | object |  | N | $.cancellationToken |  | 中断制御 |

## Notes

- このデータはキュー項目と変換サービス引数を論理的にまとめたもの。

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/MainWindow.xaml.cs | ConvertQueueItemAsync 周辺 |
| NaviMovieMaker.App/Services/VideoConversionService.cs | ConvertAsync |
