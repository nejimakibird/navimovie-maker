---
type: app_process
id: PROC-NMM-SAVE-SETTINGS
name: Save Settings
kind: business_flow
tags:
  - AppProcess
  - NaviMovieMaker
---

# Save Settings

## Summary

設定画面の入力値から `AppSettings` を作成し、必須フォルダと実行ファイルパスを検証して、フォルダ作成後に JSON 保存する。

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/SettingsWindow.xaml.cs | SaveButton_Click / Validate |
| NaviMovieMaker.App/Services/SettingsService.cs | EnsureFolders / Save |
| NaviMovieMaker.App/Models/AppSettings.cs | 設定項目 |

## Triggers

| id | kind | source | event | notes |
|---|---|---|---|---|
| TRG-SAVE | screen_action |  | click | SettingsWindow.SaveButton_Click |

## Inputs

| id | data | source | required | notes |
|---|---|---|---|---|
| IN-FORM | [[DATA-NMM-APP-SETTINGS]] |  | Y | SettingsWindow 入力値 |

## Outputs

| id | data | target | notes |
|---|---|---|---|
| OUT-SETTINGS | [[DATA-NMM-APP-SETTINGS]] | [[DFD-NMM-OBJ-FILE-SYSTEM]] | settings.json |

## Steps

| id | lane | label | kind | input | output | rule | invoke | screen | notes |
|---|---|---|---|---|---|---|---|---|---|
| start | SettingsWindow | 保存クリック | start | IN-FORM |  |  |  |  |  |
| collect | SettingsWindow | 画面値を収集 | input | IN-FORM | SETTINGS-DRAFT |  |  |  | TextBox / ComboBox |
| validate | SettingsWindow | 入力を検証 | decision | SETTINGS-DRAFT | VALIDATION |  |  |  | 必須フォルダと exe |
| ensure_folders | SettingsService | フォルダを作成 | process | SETTINGS-DRAFT | SETTINGS-DRAFT |  |  |  | EnsureFolders |
| save_json | SettingsService | JSON を保存 | process | SETTINGS-DRAFT | OUT-SETTINGS |  |  |  | Save |
| success | SettingsWindow | DialogResult true | end | OUT-SETTINGS |  |  |  |  |  |
| error | SettingsWindow | エラーを表示 | end | VALIDATION |  |  |  |  | MessageBox |

## Flows

| from | to | condition | label | notes |
|---|---|---|---|---|
| start | collect |  | 収集 |  |
| collect | validate |  | 検証 |  |
| validate | ensure_folders | valid | OK |  |
| validate | error | invalid | NG |  |
| ensure_folders | save_json | success | 保存 |  |
| ensure_folders | error | failed | NG |  |
| save_json | success | success | OK |  |
| save_json | error | failed | NG |  |

## Errors

- 必須フォルダが空、表示プリセットがゼロ、指定 exe 名が期待値と異なる場合は保存しない。
