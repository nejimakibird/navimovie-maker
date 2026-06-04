---
type: app_process
id: PROC-NMM-ADD-FILE-TO-QUEUE
name: Add File or URL to Conversion Queue
kind: business_flow
tags:
  - AppProcess
  - NaviMovieMaker
---

# Add File or URL to Conversion Queue

## Summary

ファイル選択、ドラッグ＆ドロップ、候補選択、URL ドロップから `ConversionQueueItem` を作成し、Conversion Queue に追加する。

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/MainWindow.xaml.cs | AddLocalFilesToQueue / AddOnlineUrlsToQueueAsync |
| NaviMovieMaker.App/Models/ConversionQueueItem.cs | キュー項目 |

## Triggers

| id | kind | source | event | notes |
|---|---|---|---|---|
| TRG-ADD-LOCAL | screen_action | [[SCR-NMM-MAIN-WINDOW]].ACT-ADD-LOCAL-FILES | click/drop | ローカルファイル追加 |
| TRG-ADD-URL | screen_action | [[SCR-NMM-MAIN-WINDOW]].ACT-ADD-URL-TO-QUEUE | drop | URL 追加 |
| TRG-ADD-CANDIDATE | screen_action | [[SCR-NMM-MAIN-WINDOW]].ACT-ADD-SELECTED-TO-QUEUE | click | 候補追加 |

## Inputs

| id | data | source | required | notes |
|---|---|---|---|---|
| IN-SOURCE | file path or URL | [[SCR-NMM-MAIN-WINDOW]] | Y | 追加元 |
| IN-SETTINGS | [[DATA-NMM-APP-SETTINGS]] | SettingsService | Y | 実行モード確認 |

## Outputs

| id | data | target | notes |
|---|---|---|---|
| OUT-QUEUE-ITEM | [[DATA-NMM-QUEUE-ITEM]] | Conversion Queue | 追加または対象外 |

## Steps

| id | lane | label | kind | input | output | rule | invoke | screen | notes |
|---|---|---|---|---|---|---|---|---|---|
| start | User | 追加操作 | start | IN-SOURCE |  |  |  | SCR-NMM-MAIN-WINDOW | ファイルまたは URL |
| classify | Controller | 入力種別を判定 | decision | IN-SOURCE | SOURCE-KIND |  |  |  | ファイル / URL / 不明 |
| validate_file | Controller | ファイルを検証 | decision | SOURCE-KIND | FILE-VALIDATION |  |  |  | 存在と拡張子 |
| normalize_url | Controller | URL を正規化 | decision | SOURCE-KIND | URL-VALIDATION |  |  |  | 単体動画 URL 制限 |
| create_supported | Controller | 対象キュー項目を作成 | process | FILE-VALIDATION | OUT-QUEUE-ITEM |  |  |  | LocalFile または OnlineVideo |
| create_unsupported | Controller | 対象外項目を作成 | process | SOURCE-KIND | OUT-QUEUE-ITEM |  |  |  | 理由を保持 |
| apply_mode | Controller | 実行モード適合を反映 | process | OUT-QUEUE-ITEM | OUT-QUEUE-ITEM |  |  |  | ApplyQueueSupportStatus |
| refresh | UI | 順序と表示を更新 | screen | OUT-QUEUE-ITEM |  |  |  | SCR-NMM-MAIN-WINDOW | DataGrid |
| end | UI | 追加完了 | end | OUT-QUEUE-ITEM |  |  |  | SCR-NMM-MAIN-WINDOW |  |

## Flows

| from | to | condition | label | notes |
|---|---|---|---|---|
| start | classify |  | 入力 |  |
| classify | validate_file | file path | file |  |
| classify | normalize_url | URL | URL |  |
| classify | create_unsupported | unknown | 対象外 |  |
| validate_file | create_supported | supported | OK |  |
| validate_file | create_unsupported | unsupported | NG |  |
| normalize_url | create_supported | allowed | OK |  |
| normalize_url | create_unsupported | rejected | NG |  |
| create_supported | apply_mode |  | 追加 |  |
| create_unsupported | apply_mode |  | 追加 |  |
| apply_mode | refresh |  | 表示 |  |
| refresh | end |  | 完了 |  |

## Errors

- フォルダ、未存在ファイル、未対応拡張子、重複、許可されない URL は通常処理対象外として扱われる。
