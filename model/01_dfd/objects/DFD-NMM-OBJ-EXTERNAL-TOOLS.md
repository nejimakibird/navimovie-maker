---
type: dfd_object
id: DFD-NMM-OBJ-EXTERNAL-TOOLS
name: External Tools
kind: external
tags:
  - DFD
  - NaviMovieMaker
---

# External Tools

## Summary

NaviMovie-Maker が外部プロセスとして利用する `yt-dlp.exe`、`ffmpeg.exe`、`ffprobe.exe`。

## Details

| key | value | notes |
|---|---|---|
| tool | yt-dlp.exe | 情報取得とダウンロード |
| tool | ffmpeg.exe | 変換と音声解析 |
| tool | ffprobe.exe | 外部ツール確認対象 |
| resolver | [[PROC-NMM-RESOLVE-EXTERNAL-TOOLS]] | パス解決 |

## Source Links

| path | notes |
|---|---|
| README-ja.md | 初回準備 |
| NaviMovieMaker.App/Services/ExternalToolService.cs | 確認と取得 |
