---
type: data_object
id: DATA-NMM-EXTERNAL-TOOL
name: External Tool
kind: result
data_format: object
tags:
  - DataObject
  - NaviMovieMaker
---

# External Tool

## Summary

`ExternalToolResult` が表す外部ツール確認結果。yt-dlp、ffmpeg、ffprobe の利用可否と解決済みパスを保持する。

## Fields

| name | label | type | length | required | path | ref | notes |
|---|---|---|---:|---|---|---|---|
| ToolName | ツール名 | string |  | Y | $.ToolName |  | yt-dlp など |
| IsAvailable | 利用可能 | boolean |  | Y | $.IsAvailable |  | 終了コード 0 |
| Version | バージョン | string |  | N | $.Version |  | nullable |
| ExecutablePath | 実行ファイルパス | string |  | N | $.ExecutablePath |  | 解決済み |
| Message | メッセージ | string |  | Y | $.Message |  | UI 表示 |
| StandardOutput | 標準出力 | string |  | N | $.StandardOutput |  | 確認結果 |
| StandardError | 標準エラー | string |  | N | $.StandardError |  | 確認結果 |
| ExitCode | 終了コード | number |  | N | $.ExitCode |  | nullable |

## Notes

- このデータは外部ツール確認結果を表す。

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/Services/ExternalToolResult.cs | result record |
| NaviMovieMaker.App/Services/ExternalToolService.cs | 解決と確認 |
