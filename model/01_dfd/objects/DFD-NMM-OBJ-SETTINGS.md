---
type: dfd_object
id: DFD-NMM-OBJ-SETTINGS
name: Preset / Settings Service
kind: process
tags:
  - DFD
  - NaviMovieMaker
---

# Preset / Settings Service

## Summary

設定 JSON の読み書き、既定フォルダ、表示プリセット、ダウンロードプロファイル、外部ツールパスを管理する。

## Details

| key | value | notes |
|---|---|---|
| service | SettingsService | settings.json |
| catalog | ConversionPresetCatalog | 出力形式 |
| catalog | DownloadProfileCatalog | yt-dlp format |
| related_data | [[DATA-NMM-APP-SETTINGS]] | 設定 |
| related_data | [[DATA-NMM-CONVERSION-PRESET]] | 出力形式 |

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/Services/SettingsService.cs | 設定保存 |
| NaviMovieMaker.App/Services/ConversionPresetCatalog.cs | 変換プリセット |
| NaviMovieMaker.App/Services/DownloadProfileCatalog.cs | ダウンロードプロファイル |
