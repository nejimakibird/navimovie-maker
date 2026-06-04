---
type: data_object
id: DATA-NMM-OUTPUT-FILE
name: Output File
kind: file
data_format: object
tags:
  - DataObject
  - NaviMovieMaker
---

# Output File

## Summary

yt-dlp のダウンロード結果、ffmpeg の変換結果、またはコピー結果としてファイルシステムに作成されるファイル。

## Fields

| name | label | type | length | required | path | ref | notes |
|---|---|---|---:|---|---|---|---|
| OutputFilePath | 出力ファイルパス | string |  | Y | $.OutputFilePath |  | 変換結果 |
| DownloadedFilePath | ダウンロードファイルパス | string |  | N | $.DownloadedFilePath |  | URL 取得結果 |
| ContainerExtension | 拡張子 | string |  | N | $.ContainerExtension | [[DATA-NMM-CONVERSION-PRESET]].ContainerExtension | プリセット由来 |
| StandardOutput | 標準出力 | string |  | N | $.StandardOutput |  | 外部プロセス |
| StandardError | 標準エラー | string |  | N | $.StandardError |  | 外部プロセス |
| ExitCode | 終了コード | number |  | N | $.ExitCode |  | nullable |
| IsSuccess | 成功 | boolean |  | Y | $.IsSuccess |  | result record |

## Notes

- このデータは外部プロセスやコピー処理の出力結果を論理的にまとめたもの。

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/Services/VideoDownloadResult.cs | ダウンロード結果 |
| NaviMovieMaker.App/Services/VideoConversionResult.cs | 変換結果 |
| NaviMovieMaker.App/MainWindow.xaml.cs | コピー出力 |
