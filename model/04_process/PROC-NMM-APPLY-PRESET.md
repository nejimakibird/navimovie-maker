---
type: app_process
id: PROC-NMM-APPLY-PRESET
name: Apply Conversion Preset
kind: business_flow
tags:
  - AppProcess
  - NaviMovieMaker
---

# Apply Conversion Preset

## Summary

表示対象の `ConversionPreset` から選択プリセットを取得し、出力拡張子、動画フィルタ、音声変換分岐、画面比率 UI を決定する。

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/MainWindow.xaml.cs | GetSelectedConversionPreset / PopulateOutputPresetComboBox |
| NaviMovieMaker.App/Services/ConversionPresetCatalog.cs | プリセット定義 |
| NaviMovieMaker.App/Services/VideoConversionService.cs | AddPresetArguments |

## Inputs

| id | data | source | required | notes |
|---|---|---|---|---|
| IN-SETTINGS | [[DATA-NMM-APP-SETTINGS]] | SettingsService | Y | 表示プリセット |
| IN-SELECTION | preset id | [[SCR-NMM-MAIN-WINDOW]] | N | ComboBox Tag |

## Outputs

| id | data | target | notes |
|---|---|---|---|
| OUT-PRESET | [[DATA-NMM-CONVERSION-PRESET]] | Controller | 選択プリセット |

## Steps

| id | lane | label | kind | input | output | rule | invoke | screen | notes |
|---|---|---|---|---|---|---|---|---|---|
| start | UI | プリセット選択 | start | IN-SELECTION |  |  |  | SCR-NMM-MAIN-WINDOW |  |
| load_catalog | Settings | カタログを取得 | process | IN-SETTINGS | PRESET-LIST |  |  |  | ConversionPresetCatalog |
| filter_visible | Settings | 表示プリセットを絞る | process | PRESET-LIST | VISIBLE-LIST |  |  |  | VisibleOutputPresetIds |
| resolve_selection | Controller | 選択IDを解決 | decision | VISIBLE-LIST | OUT-PRESET |  |  |  | 未一致なら既定 |
| update_ui | UI | 関連 UI を更新 | screen | OUT-PRESET |  |  |  | SCR-NMM-MAIN-WINDOW | 画面比率など |
| end | Controller | プリセット適用完了 | end | OUT-PRESET |  |  |  |  |  |

## Flows

| from | to | condition | label | notes |
|---|---|---|---|---|
| start | load_catalog |  | 読込 |  |
| load_catalog | filter_visible |  | 表示 |  |
| filter_visible | resolve_selection |  | 解決 |  |
| resolve_selection | update_ui | found or default | 適用 |  |
| update_ui | end |  | 完了 |  |

## Notes

- 実際の ffmpeg 引数は `VideoConversionService` が preset のフィールドから構築する。
