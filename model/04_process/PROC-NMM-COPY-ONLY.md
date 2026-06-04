---
type: app_process
id: PROC-NMM-COPY-ONLY
name: Copy Local File Only
kind: business_flow
tags:
  - AppProcess
  - NaviMovieMaker
---

# Copy Local File Only

## Summary

`Copy Files` モードでローカルファイルを出力フォルダへコピーし、進捗をキューに反映する。URL はこのモードでは対象外。

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/MainWindow.xaml.cs | CopyFileWithProgressAsync / GetUnsupportedReason |

## Inputs

| id | data | source | required | notes |
|---|---|---|---|---|
| IN-QUEUE-ITEM | [[DATA-NMM-QUEUE-ITEM]] | Conversion Queue | Y | LocalFile |

## Outputs

| id | data | target | notes |
|---|---|---|---|
| OUT-COPIED | [[DATA-NMM-OUTPUT-FILE]] | ConvertedFolder | コピー結果 |
| OUT-STATUS | [[DATA-NMM-QUEUE-ITEM]] | [[SCR-NMM-MAIN-WINDOW]] | 進捗 |

## Steps

| id | lane | label | kind | input | output | rule | invoke | screen | notes |
|---|---|---|---|---|---|---|---|---|---|
| start | UI | コピー実行 | start | IN-QUEUE-ITEM |  |  |  | SCR-NMM-MAIN-WINDOW |  |
| validate_source | Controller | LocalFile を確認 | decision | IN-QUEUE-ITEM | VALIDATION |  |  |  | URL は対象外 |
| build_destination | Controller | コピー先を決定 | process | IN-QUEUE-ITEM | OUT-COPIED |  |  |  | 出力フォルダ |
| copy_file | File System | ファイルをコピー | process | OUT-COPIED | OUT-COPIED |  |  |  | バッファ単位 |
| success | Controller | 完了状態を反映 | end | OUT-COPIED | OUT-STATUS |  |  | SCR-NMM-MAIN-WINDOW |  |
| failed | Controller | 失敗状態を反映 | end | IN-QUEUE-ITEM | OUT-STATUS |  |  | SCR-NMM-MAIN-WINDOW |  |

## Flows

| from | to | condition | label | notes |
|---|---|---|---|---|
| start | validate_source |  | 確認 |  |
| validate_source | build_destination | LocalFile | OK |  |
| validate_source | failed | OnlineVideo | 対象外 |  |
| build_destination | copy_file |  | コピー |  |
| copy_file | success | success | OK |  |
| copy_file | failed | failed | NG |  |

## Errors

- 出力先作成やコピーに失敗した場合は Failed になる。
