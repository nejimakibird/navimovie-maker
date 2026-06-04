---
type: dfd_diagram
id: DFD-NMM-CORE-L0
name: NaviMovie-Maker Core L0 DFD
render_mode: mermaid
level: L0
scope: NaviMovie-Maker
tags:
  - DFD
  - NaviMovieMaker
---

# NaviMovie-Maker Core L0 DFD

## Summary

NaviMovie-Maker の主な実行時境界とデータの流れを表す。WPF の `MainWindow` が UI とアプリケーション制御を担い、各 Service が yt-dlp / FFmpeg / ファイルシステムを呼び出す。

## Objects

| id | label | kind | ref | notes |
|---|---|---|---|---|
| user | User | external | [[DFD-NMM-OBJ-USER]] | 画面を操作する利用者 |
| ui | Main Window / UI | process | [[DFD-NMM-OBJ-UI]] | WPF 画面 |
| controller | Application Controller | process | [[DFD-NMM-OBJ-APP-CONTROLLER]] | MainWindow code-behind |
| queue | Conversion Queue | datastore | [[DFD-NMM-OBJ-QUEUE]] | ObservableCollection のキュー |
| download | Download Service | process | [[DFD-NMM-OBJ-DOWNLOAD]] | yt-dlp 呼び出し |
| conversion | Conversion Service | process | [[DFD-NMM-OBJ-CONVERSION]] | ffmpeg 呼び出し |
| settings | Preset / Settings Service | process | [[DFD-NMM-OBJ-SETTINGS]] | 設定とプリセット |
| tools | External Tools | external | [[DFD-NMM-OBJ-EXTERNAL-TOOLS]] | yt-dlp / ffmpeg / ffprobe |
| file_system | File System | datastore | [[DFD-NMM-OBJ-FILE-SYSTEM]] | settings.json と入出力フォルダ |

## Flows

| id | from | to | data | notes |
|---|---|---|---|---|
| flow_user_actions | user | ui | screen input | URL、ファイル、実行操作 |
| flow_screen_events | ui | controller | [[DATA-NMM-DOWNLOAD-REQUEST]] | 画面イベントと選択値 |
| flow_queue_update | controller | queue | [[DATA-NMM-QUEUE-ITEM]] | キュー項目追加と状態更新 |
| flow_settings_read | controller | settings | [[DATA-NMM-APP-SETTINGS]] | 起動時と実行時設定 |
| flow_settings_file | settings | file_system | [[DATA-NMM-APP-SETTINGS]] | settings.json 読み書き |
| flow_download_request | controller | download | [[DATA-NMM-DOWNLOAD-REQUEST]] | URL ダウンロード要求 |
| flow_download_tool | download | tools | yt-dlp command | メタデータ取得とダウンロード |
| flow_download_output | download | file_system | [[DATA-NMM-OUTPUT-FILE]] | 作業または一時ファイル |
| flow_conversion_request | controller | conversion | [[DATA-NMM-CONVERSION-JOB]] | 変換ジョブ |
| flow_conversion_tool | conversion | tools | ffmpeg command | 変換と音声解析 |
| flow_conversion_output | conversion | file_system | [[DATA-NMM-OUTPUT-FILE]] | 変換済みファイル |
| flow_status_display | controller | ui | [[DATA-NMM-QUEUE-ITEM]] | 状態、進捗、ログ |
| flow_result_view | ui | user | screen output | キュー状態とログ表示 |

## Source Links

| path | notes |
|---|---|
| README-ja.md | 機能と外部ツール説明 |
| NaviMovieMaker.App/MainWindow.xaml | 画面構造 |
| NaviMovieMaker.App/MainWindow.xaml.cs | UI 制御とキュー処理 |
| NaviMovieMaker.App/Services/ | サービス境界 |

## Notes

- ViewModel クラスは確認できず、`MainWindow.xaml.cs` が UI 制御とアプリケーション制御を兼ねる。
- `ffprobe` は外部ツール確認対象だが、今回確認した変換サービスの主要処理では `ffmpeg` が直接使われる。
