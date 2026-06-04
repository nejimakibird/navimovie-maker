---
type: dfd_object
id: DFD-NMM-OBJ-CONVERSION
name: Conversion Service
kind: process
tags:
  - DFD
  - NaviMovieMaker
---

# Conversion Service

## Summary

`VideoConversionService` が選択プリセットと音声補正に従い、ffmpeg を起動して動画または音声ファイルを生成する。

## Details

| key | value | notes |
|---|---|---|
| service | VideoConversionService | FFmpeg 実行 |
| external_tool | ffmpeg.exe | 変換と音量解析 |
| related_data | [[DATA-NMM-CONVERSION-JOB]] | 変換入力 |
| related_data | [[DATA-NMM-CONVERSION-PRESET]] | プリセット |

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/Services/VideoConversionService.cs | 変換処理 |
