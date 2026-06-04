---
type: dfd_object
id: DFD-NMM-OBJ-APP-CONTROLLER
name: MainWindow Application Controller
kind: process
tags:
  - DFD
  - NaviMovieMaker
---

# MainWindow Application Controller

## Summary

`MainWindow.xaml.cs` の code-behind。画面イベント、キュー、実行モード、外部ツール確認、サービス呼び出し、進捗反映を制御する。

## Details

| key | value | notes |
|---|---|---|
| owner | MainWindow | WPF code-behind |
| related_process | [[PROC-NMM-ADD-FILE-TO-QUEUE]] | キュー追加 |
| related_process | [[PROC-NMM-DOWNLOAD-AND-CONVERT]] | URL ダウンロード後変換 |
| related_process | [[PROC-NMM-CONVERT-LOCAL-FILE]] | ローカル変換 |
| related_process | [[PROC-NMM-COPY-ONLY]] | コピー |
| related_data | [[DATA-NMM-QUEUE-ITEM]] | キュー項目 |

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/MainWindow.xaml.cs | UI 制御と実行分岐 |
