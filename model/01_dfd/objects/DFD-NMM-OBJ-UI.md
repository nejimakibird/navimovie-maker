---
type: dfd_object
id: DFD-NMM-OBJ-UI
name: Main Window / UI
kind: process
tags:
  - DFD
  - NaviMovieMaker
---

# Main Window / UI

## Summary

WPF の `MainWindow.xaml` に定義された主画面。動画ソース、Conversion Queue、出力設定、実行ボタン、進捗、ログを表示する。

## Details

| key | value | notes |
|---|---|---|
| technology | WPF | `net10.0-windows` |
| related_screen | [[SCR-NMM-MAIN-WINDOW]] | 画面モデル |
| controller | [[DFD-NMM-OBJ-APP-CONTROLLER]] | code-behind |

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/MainWindow.xaml | UI 定義 |
| NaviMovieMaker.App/NaviMovieMaker.App.csproj | WPF 設定 |
