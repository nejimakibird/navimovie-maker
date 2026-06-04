---
id: REPORT-NMM-ONE-SHOT-REVERSE-ENGINEERING-EVALUATION
name: NaviMovie-Maker One-shot Reverse Engineering Evaluation
tags:
  - NaviMovieMaker
  - ReverseEngineering
  - ModelWeave
---

# NaviMovie-Maker One-shot Reverse Engineering Evaluation

## Summary

NaviMovie-Maker を対象に、事前知識なしの一回実行で Model Weave model を生成できるかを確認した実験の評価メモ。

## Purpose

既存の小規模アプリケーションについて、ソースコードと関連ドキュメントだけから、DATA / DFD / Process / Screen の初期モデルを作れるかを確認する。

## Input Sources

| source | notes |
|---|---|
| NaviMovieMaker.App | WPF 画面、Models、Services |
| README.md / README-ja.md | 機能概要と使い方 |
| release-docs | 配布向け説明と初回公開内容 |
| Model Weave format docs | モデル形式と表スキーマ |
| Model Weave guidance notes | AI 生成と Markdown table 安全ルール |

## Generated Coverage

| area | generated | notes |
|---|---|---|
| DFD | yes | 中核境界と DFD object |
| DATA | yes | 設定、キュー、プリセット、要求、出力 |
| Process | yes | キュー追加、変換、コピー、設定、外部ツール |
| Screen | yes | Main Window |
| Findings | yes | 調査結果ノート |

## Validation Result

生成後に Markdown table safety、DFD flow endpoint、app_process flow endpoint、Source Links の配置を確認した。発行時には Source Links のパスを NaviMovie-Maker リポジトリ内の相対パスへ正規化した。

## Warnings Found And Fixed

| warning | fix | notes |
|---|---|---|
| data_object Source Links parsed as Fields | `## Notes` を挿入 | `## Fields` 直後を避けた |
| DFD kind `external_entity` warning | `external` へ変更 | 現行 preview に合わせた |
| unresolved `settings.json` target | File System object 参照へ変更 | 実ファイル名は notes へ移動 |
| unresolved `SettingsWindow.SaveButton` source | notes へ移動 | 新規 screen は作成しない |

## Important Finding

`data_object` で `## Fields` の直後に `## Source Links` を置くと、環境によって Source Links table が Fields として解釈される警告が出ることがあった。今回の生成物では次の順序を安全側の運用とした。

1. `## Fields`
2. `## Notes`
3. `## Source Links`

## Evaluation

- DFD-first generation が有効だった。境界を先に整理すると DATA と Process の参照先を決めやすい。
- DATA / DFD / Process / Screen を一回の流れで生成できた。
- Model Weave preview warnings は、生成出力の section order、kind、未解決参照の cleanup に役立った。
- 小規模な既存アプリケーションの初期理解と可視化には有用性があると考えられる。

## Limitations

- 対象アプリケーションは小規模で、コード量と境界が比較的追いやすかった。
- 初回モデルは人間のレビューを前提にしたドラフトであり、完全な仕様ではない。
- 大規模システムでは、DATA、DFD、Process、Screen を段階的に生成し、各段階で preview validation を挟む方が安全だと考えられる。
