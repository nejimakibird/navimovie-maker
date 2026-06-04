---
type: app_process
id: PROC-NMM-CONVERT-LOCAL-FILE
name: Convert Local File
kind: business_flow
tags:
  - AppProcess
  - NaviMovieMaker
---

# Convert Local File

## Summary

ローカルファイルまたはダウンロード済みファイルを、選択プリセットと音声補正に基づいて ffmpeg で変換する。

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/MainWindow.xaml.cs | ConvertQueueItemAsync / BuildAudioFilterAsync |
| NaviMovieMaker.App/Services/VideoConversionService.cs | ConvertAsync / ConvertAudioPresetAsync |

## Inputs

| id | data | source | required | notes |
|---|---|---|---|---|
| IN-JOB | [[DATA-NMM-CONVERSION-JOB]] | Controller | Y | 入力、出力、プリセット |

## Outputs

| id | data | target | notes |
|---|---|---|---|
| OUT-FILE | [[DATA-NMM-OUTPUT-FILE]] | File System | 変換結果 |
| OUT-STATUS | [[DATA-NMM-QUEUE-ITEM]] | [[SCR-NMM-MAIN-WINDOW]] | 進捗表示 |

## Steps

| id | lane | label | kind | input | output | rule | invoke | screen | notes |
|---|---|---|---|---|---|---|---|---|---|
| start | Controller | 変換開始 | start | IN-JOB |  |  |  |  |  |
| validate_input | Controller | 入力ファイルを確認 | decision | IN-JOB | INPUT-VALID |  |  |  | File.Exists |
| build_output | Controller | 出力パスを生成 | process | IN-JOB | OUT-FILE |  | PROC-NMM-APPLY-PRESET |  | 拡張子と連番 |
| build_audio | Controller | 音声補正を決定 | decision | IN-JOB | AUDIO-FILTER |  |  |  | loudnorm / peak boost |
| run_ffmpeg | Conversion | ffmpeg を実行 | process | IN-JOB | OUT-FILE |  |  |  | 変換 |
| update_success | Controller | 成功状態を反映 | end | OUT-FILE | OUT-STATUS |  |  | SCR-NMM-MAIN-WINDOW | Converted |
| update_failed | Controller | 失敗状態を反映 | end | IN-JOB | OUT-STATUS |  |  | SCR-NMM-MAIN-WINDOW | Failed |

## Flows

| from | to | condition | label | notes |
|---|---|---|---|---|
| start | validate_input |  | 入力 |  |
| validate_input | build_output | exists | OK |  |
| validate_input | update_failed | missing | NG |  |
| build_output | build_audio |  | 補正 |  |
| build_audio | run_ffmpeg |  | 実行 |  |
| run_ffmpeg | update_success | success | OK |  |
| run_ffmpeg | update_failed | failed | NG |  |

## Errors

- 入力ファイルがない場合は Skipped または Failed として扱われる。
- ffmpeg の終了コードが成功でない場合、標準出力と標準エラーをログに出す。
