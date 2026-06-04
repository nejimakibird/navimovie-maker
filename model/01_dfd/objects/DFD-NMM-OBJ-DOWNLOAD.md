---
type: dfd_object
id: DFD-NMM-OBJ-DOWNLOAD
name: Download Service
kind: process
tags:
  - DFD
  - NaviMovieMaker
---

# Download Service

## Summary

`VideoDownloadService` と `VideoMetadataService` が yt-dlp を起動し、動画候補取得と URL ダウンロードを行う。

## Details

| key | value | notes |
|---|---|---|
| service | VideoDownloadService | ダウンロード |
| service | VideoMetadataService | メタデータ取得 |
| external_tool | yt-dlp.exe | 外部プロセス |
| related_data | [[DATA-NMM-DOWNLOAD-REQUEST]] | ダウンロード要求 |

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/Services/VideoDownloadService.cs | yt-dlp ダウンロード |
| NaviMovieMaker.App/Services/VideoMetadataService.cs | yt-dlp JSON 取得 |
