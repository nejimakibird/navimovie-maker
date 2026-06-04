---
type: dfd_object
id: DFD-NMM-OBJ-FILE-SYSTEM
name: File System
kind: datastore
tags:
  - DFD
  - NaviMovieMaker
---

# File System

## Summary

設定ファイル、tools フォルダ、作業フォルダ、一時フォルダ、変換済みフォルダ、ローカル動画フォルダを保持する。

## Details

| key | value | notes |
|---|---|---|
| settings_file | %APPDATA%/settings.json | 設定保存先 |
| tools_folder | AppContext.BaseDirectory/tools | 外部ツール配置先 |
| output_folder | ConvertedFolder | 変換済み出力 |
| work_folder | WorkingFolder | ダウンロード元保存 |
| temp_folder | TemporaryFolder | 一時ダウンロード |

## Source Links

| path | notes |
|---|---|
| README-ja.md | 出力先と設定ファイル |
| NaviMovieMaker.App/Services/SettingsService.cs | フォルダ作成 |
| NaviMovieMaker.App/Services/ExternalToolService.cs | tools フォルダ |
