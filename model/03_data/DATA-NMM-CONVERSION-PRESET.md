---
type: data_object
id: DATA-NMM-CONVERSION-PRESET
name: Conversion Preset
kind: dto
data_format: object
tags:
  - DataObject
  - NaviMovieMaker
---

# Conversion Preset

## Summary

`ConversionPreset` record と `ConversionPresetCatalog` により定義される出力形式。

## Fields

| name | label | type | length | required | path | ref | notes |
|---|---|---|---:|---|---|---|---|
| Id | ID | string |  | Y | $.Id |  | stable ID |
| DisplayName | 表示名 | string |  | Y | $.DisplayName |  | 画面表示 |
| ContainerExtension | 拡張子 | string |  | Y | $.ContainerExtension |  | .mp4 など |
| VideoCodec | 映像コーデック | string |  | N | $.VideoCodec |  | 音声専用では空 |
| AudioCodec | 音声コーデック | string |  | Y | $.AudioCodec |  | aac など |
| Width | 幅 | number |  | N | $.Width |  | 音声専用では 0 |
| Height | 高さ | number |  | N | $.Height |  | 音声専用では 0 |
| FrameRate | フレームレート | number |  | N | $.FrameRate |  | nullable |
| VideoBitrateKbps | 映像ビットレート | number |  | N | $.VideoBitrateKbps |  | kbps |
| AudioBitrateKbps | 音声ビットレート | number |  | N | $.AudioBitrateKbps |  | kbps または品質値 |
| VideoProfile | 映像プロファイル | string |  | N | $.VideoProfile |  | nullable |
| VideoLevel | 映像レベル | string |  | N | $.VideoLevel |  | nullable |
| EnableFastStart | faststart | boolean |  | N | $.EnableFastStart |  | movflags |
| FormatName | format | string |  | N | $.FormatName |  | nullable |
| SupportsAspectMode | 画面比率対応 | boolean |  | N | $.SupportsAspectMode |  | UI 有効化 |
| IsAudioOnlyPreset | 音声専用 | boolean |  | N | $.IsAudioOnlyPreset |  | 音声変換分岐 |

## Notes

- このデータは `ConversionPreset` record の主要項目を表す。

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/Services/ConversionPreset.cs | record 定義 |
| NaviMovieMaker.App/Services/ConversionPresetCatalog.cs | カタログ |
