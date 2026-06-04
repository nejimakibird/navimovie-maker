---
type: app_process
id: PROC-NMM-RESOLVE-EXTERNAL-TOOLS
name: Resolve External Tools
kind: business_flow
tags:
  - AppProcess
  - NaviMovieMaker
---

# Resolve External Tools

## Summary

設定済みパス、アプリの tools フォルダ、Windows PATH の順に yt-dlp / ffmpeg / ffprobe を確認し、必要に応じて自動取得を促す。

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/MainWindow.xaml.cs | CheckExternalToolsAsync / EnsureRequiredToolsAsync |
| NaviMovieMaker.App/Services/ExternalToolService.cs | CheckAllAsync / InstallToolsAsync |

## Inputs

| id | data | source | required | notes |
|---|---|---|---|---|
| IN-SETTINGS | [[DATA-NMM-APP-SETTINGS]] | SettingsService | Y | 手動パスと URL |

## Outputs

| id | data | target | notes |
|---|---|---|---|
| OUT-TOOLS | [[DATA-NMM-EXTERNAL-TOOL]] | Controller | 確認結果 |

## Steps

| id | lane | label | kind | input | output | rule | invoke | screen | notes |
|---|---|---|---|---|---|---|---|---|---|
| start | Controller | ツール確認開始 | start | IN-SETTINGS |  |  |  |  |  |
| check_configured | ExternalToolService | 設定済みパスを確認 | decision | IN-SETTINGS | TOOL-CANDIDATE |  |  |  | File.Exists |
| check_tools_folder | ExternalToolService | tools フォルダを確認 | decision | TOOL-CANDIDATE | TOOL-CANDIDATE |  |  |  | AppContext.BaseDirectory |
| check_path | ExternalToolService | PATH を確認 | decision | TOOL-CANDIDATE | TOOL-CANDIDATE |  |  |  | 環境変数 |
| run_version | ExternalToolService | バージョンコマンド実行 | process | TOOL-CANDIDATE | OUT-TOOLS |  |  |  | --version / -version |
| apply_paths | Controller | 解決済みパスを反映 | process | OUT-TOOLS | OUT-TOOLS |  |  |  | サービスへ設定 |
| prompt_install | UI | 自動取得または設定を促す | screen | OUT-TOOLS |  |  |  | SCR-NMM-MAIN-WINDOW | 不足時 |
| end | Controller | 確認完了 | end | OUT-TOOLS |  |  |  |  |  |

## Flows

| from | to | condition | label | notes |
|---|---|---|---|---|
| start | check_configured |  | 開始 |  |
| check_configured | run_version | found | 設定 |  |
| check_configured | check_tools_folder | missing | 次へ |  |
| check_tools_folder | run_version | found | tools |  |
| check_tools_folder | check_path | missing | PATH |  |
| check_path | run_version | found | PATH |  |
| check_path | prompt_install | missing | 不足 |  |
| run_version | apply_paths | available | OK |  |
| run_version | prompt_install | failed | NG |  |
| apply_paths | end |  | 完了 |  |

## Errors

- 自動取得はネットワークと ZIP 内容に依存し、失敗時はログとメッセージで通知される。
