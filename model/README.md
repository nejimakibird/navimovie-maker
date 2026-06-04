# NaviMovie-Maker Model Weave model

このディレクトリには、NaviMovie-Maker の Model Weave リバースエンジニアリング出力を格納しています。

## Model Weave とは

Model Weave は、Markdown で記述した設計情報をもとに、DFD、データ定義、業務フロー、画面構造などを Obsidian 上で可視化するためのモデリング支援ツールです。

この `model/` ディレクトリの各 Markdown ファイルは、通常の文章として読むこともできますが、Model Weave を導入した Obsidian Vault で開くことで、図・プレビュー・診断・関連リンクとして確認できます。

NaviMovie-Maker では、既存ソースコードをもとに以下の観点でモデル化しています。

* アプリ全体の構成を示す DFD
* 主要なデータ構造
* ダウンロード、変換、コピー、設定保存などの処理フロー
* WPF 主画面の構造
* リバースエンジニアリング実験のメモ

## このモデルの位置づけ

このモデル群は、NaviMovie-Maker のソースコード、README / release docs、Model Weave のフォーマット文書を入力として生成したものです。

手作業で完成させた完全な仕様書ではなく、既存アプリケーションを理解するための初期アーキテクチャマップとして扱います。

そのため、次の用途に向いています。

* 初めてソースを読む前の全体把握
* UI、キュー、ダウンロード、変換、設定、外部ツールの関係確認
* 実装変更時の影響範囲の確認
* 将来の設計ドキュメント整備のたたき台

## Main entry points

| path                                                           | notes                     |
| -------------------------------------------------------------- | ------------------------- |
| 01_dfd/diagrams/DFD-NMM-CORE-L0.md                             | 中核 DFD                    |
| 09_screen/SCR-NMM-MAIN-WINDOW.md                               | 主画面モデル                    |
| 04_process/PROC-NMM-DOWNLOAD-AND-CONVERT.md                    | URL ダウンロード後変換             |
| 04_process/PROC-NMM-CONVERT-LOCAL-FILE.md                      | ローカルファイル変換                |
| 04_process/PROC-NMM-RESOLVE-EXTERNAL-TOOLS.md                  | ffmpeg / yt-dlp など外部ツール解決 |
| 99_notes/REPORT-NMM-REVERSE-ENGINEERING-FINDINGS.md            | 初回リバースエンジニアリング結果          |
| 99_notes/REPORT-NMM-ONE-SHOT-REVERSE-ENGINEERING-EVALUATION.md | 一発生成実験の評価メモ               |

## Directory structure

| path        | notes                   |
| ----------- | ----------------------- |
| 01_dfd/     | アプリ全体の境界とデータフロー         |
| 03_data/    | 主要なデータ構造                |
| 04_process/ | 主要ユースケースの Business Flow |
| 09_screen/  | 画面構造                    |
| 99_notes/   | 実験結果、補足メモ、今後の改善候補       |

## Notes

* モデル内の Source Links は、このリポジトリのルートからの相対パスとして記述しています。
* 生成結果はレビューの出発点です。実装変更時は、対応する Model Weave model も見直してください。
* Model Weave がなくても Markdown として読むことはできますが、Obsidian + Model Weave で開くと図や診断として確認できます。
* `.obsidian/` など Vault 固有の設定ファイルは、リポジトリ管理対象外にしています。
