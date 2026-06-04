---
type: dfd_object
id: DFD-NMM-OBJ-QUEUE
name: Conversion Queue
kind: datastore
tags:
  - DFD
  - NaviMovieMaker
---

# Conversion Queue

## Summary

`ObservableCollection<ConversionQueueItem>` として保持される変換キュー。URL、ローカルファイル、対象外項目を並べ、状態と進捗を表示する。

## Details

| key | value | notes |
|---|---|---|
| data | [[DATA-NMM-QUEUE-ITEM]] | 1 行のキュー項目 |
| storage | memory | 実行中の画面状態 |
| related_screen | [[SCR-NMM-MAIN-WINDOW]] | DataGrid に表示 |

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/Models/ConversionQueueItem.cs | キュー項目 |
| NaviMovieMaker.App/MainWindow.xaml.cs | 追加と実行 |
