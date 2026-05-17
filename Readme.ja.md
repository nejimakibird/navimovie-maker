# NaviMovie-Maker

NaviMovie-Maker は、オンライン動画やローカル動画・音声ファイルを、カーナビやポータブル再生機器で扱いやすい形式に変換するための Windows 向けデスクトップアプリです。

動画候補を検索・取得し、変換対象をキューに追加して、用途に応じた形式へまとめて変換できます。  
また、変換済みファイルを SD カードや USB メモリへコピーするための補助機能として、連番付きコピーにも対応しています。

## 利用上の注意

NaviMovie-Maker は、ユーザーが正当な権利を持つ動画・音声ファイルや、利用が許可されているコンテンツを、個人の利用環境に合わせて変換・整理するためのツールです。

本アプリは、YouTube などの動画配信サイトから動画をダウンロードすることを推奨・助長するものではありません。

動画配信サイトのコンテンツを扱う場合は、各サービスの利用規約、著作権法、権利者の許諾条件を必ず確認してください。  
権利者の許可なくコンテンツを保存・変換・再配布する行為は、法律やサービス規約に違反する可能性があります。

本アプリの利用によって生じた問題については、利用者自身の責任で対応してください。

## 主な用途

- ローカル動画をドラッグ＆ドロップで変換する
- ローカル音声ファイルを音声のみ MP4 や MP3 へ変換する
- 権利上問題のないオンライン動画を取得・変換する
- カーナビ向け MP4 ファイルを作成する
- ポータブル DVD プレーヤー向け MPG ファイルを作成する
- 変換済みファイルを SD カードや USB メモリ向けに連番付きでコピーする
- 音量が小さいソースを Peak Boost で補正する
- 必要なソースだけ Loudness Normalize を個別に適用する

## 特徴

- Windows 向け WPF アプリ
- yt-dlp によるオンライン動画候補取得
- FFmpeg による動画・音声変換
- Direct URL / Search による動画候補取得
- Candidate から Conversion Queue へ追加して実行するキュー型ワークフロー
- ローカルファイルのドラッグ＆ドロップ対応
- 動画・音声の複数プリセット対応
- アスペクト比維持とストレッチの選択
- 音声のみ MP4 / MP3 / M4A などの音声出力対応
- 変換プリセットごとのサブフォルダ出力
- Copy Files モードによる連番付きコピー
- yt-dlp のリトライ処理
- 折りたたみ可能な Video Source / Conversion Queue / Log エリア

## 必要な外部ツール

NaviMovie-Maker は内部で以下の外部ツールを使用します。

- yt-dlp
- FFmpeg

これらはアプリに同梱していません。  
Windows 環境では winget などで事前にインストールしてください。

例:

```powershell
winget install yt-dlp.yt-dlp
winget install Gyan.FFmpeg
```

インストール後、アプリのメニューから以下を実行して検出状態を確認できます。

```text
Tools > Check External Tools
```

## 基本的な使い方

### 1. 動画を検索またはURLから読み込む

上部の `Video Source` で、次のどちらかを選びます。

- Search
- Direct URL

`Search` では検索語を入力して動画候補を取得します。  
現在は YouTube 検索を前提にしていますが、将来的に複数サイト対応を予定しています。

`Direct URL` では動画URLやプレイリストURLを直接指定します。

取得された候補は Candidate リストに表示されます。  
必要な候補を選択し、`Add Selected To Queue` で Conversion Queue に追加します。
表示されているサムネイルをクリックする事で、対象の動画をブラウザ上で再生します。

### 2. ローカルファイルを追加する

ローカルの動画・音声ファイルは、Add Selected to QueueまたはConversion Queue にドラッグ＆ドロップして追加できます。

対応例:

- mp4
- mov
- mkv
- webm
- mpg
- wav
- mp3
- m4a
- flac

### 3. Run Mode を選ぶ

Conversion Queue で Run Mode を選択します。

| Run Mode | 内容 |
| --- | --- |
| Download Only | OnlineVideo を Working Folder にダウンロードします |
| Download & Convert | OnlineVideo をダウンロードして変換します。LocalFile は直接変換します |
| Convert Only | LocalFile を変換します。OnlineVideo はスキップされます |
| Copy Files | Queue 内の LocalFile を指定フォルダへコピーします |

### 4. Output Preset を選ぶ

変換する場合は `Output Preset` を選びます。

主なプリセット例:

主なプリセット例:

| Preset | 内容 |
| --- | --- |
| Car Navi MP4 - Current Compatibility | 既存の実機確認済み互換プリセット。MP4 / H.264 Main Level 3.0 / 720x406 / 30fps / 映像 4000kbps / AAC 256kbps / faststart |
| Car Navi MP4 - Standard | カーナビ向け標準候補。MP4 / H.264 Main Level 3.0 / 720x480 / 30fps / 映像 4000kbps / AAC 128kbps / faststart |
| Car Navi MP4 - Small Size | 容量優先のカーナビ向け。MP4 / H.264 Main Level 3.0 / 640x360 / 30fps / 映像 2000kbps / AAC 128kbps / faststart |
| Portable DVD Player MPG - Standard | USB/SD再生対応DVDプレーヤー向け。MPG / MPEG2 / 720x480 / 30fps / 映像 5000kbps / MP2 192kbps |
| Audio MP4 AAC Only | 音声のみMP4。MP4コンテナ / 映像なし / AAC / 256kbps / 48kHz / stereo / faststart |
| MP3 - High | 音声MP3高音質。MP3 / 320kbps / 48kHz / stereo |
| MP3 - Medium | 音声MP3標準。MP3 / 192kbps / 48kHz / stereo |
| M4A AAC | 音声M4A。M4A / AAC / 256kbps / 48kHz / stereo |

表示するプリセットは Settings から調整できます。

※ `Car Navi MP4 - Current Compatibility` は、既存環境で再生確認済みの退避用プリセットです。  
※ `Portable DVD Player MPG` は DVD-Video 形式を作成するものではなく、USB/SDなどのメディア上で再生する `.mpg` ファイルを作成するプリセットです。  
※ 実際の再生可否は、カーナビやプレーヤー側の対応形式に依存します。

#### iPod touch などでの再生について

iPod touch などの Apple 系デバイスでは、一般的に MP4 / H.264 / AAC 系のファイルが扱いやすいです。

既存プリセットでは、以下が再生候補になります。

| 用途 | 推奨プリセット |
| --- | --- |
| 動画再生 | Car Navi MP4 - Small Size |
| 動画再生 | Car Navi MP4 - Standard |
| 音声再生 | MP3 - High |
| 音声再生 | MP3 - Medium |
| 音声再生 | M4A AAC |
| 音声のみMP4 | Audio MP4 AAC Only |

`Portable DVD Player MPG` は、USB/SD再生対応DVDプレーヤー向けの `.mpg` ファイルを作成するプリセットであり、iPod touch向けではありません。

実際の再生可否は、iPod touch の世代や同期・転送方法、再生アプリの仕様に依存します。  
まずは `Car Navi MP4 - Small Size` を試し、問題なければ `Car Navi MP4 - Standard` を確認するのがおすすめです。


### 5. 必要に応じてオプションを設定する

| オプション | 内容 |
| --- | --- |
| Aspect Mode | アスペクト比維持 + 余白追加、またはストレッチを選択します |
| Keep DL Files | Download & Convert 時に元ダウンロードファイルを残します |
| Number Prefix | CopyまたはConvert後のファイル名に連番を付与します。空欄なら連番なし。数値を入れるとその番号から連番を付けます |
| Peak Boost | 音量が小さいソースだけ自動で持ち上げます |
| Set Audio | 選択したQueue項目に音声補正を個別設定します |
| Target Peak | Peak Boost の目標ピークを指定します |

### 6. Run Queue を実行する

`Run Queue` を押すと、Conversion Queue の上から順番に処理します。

処理状況はプログレスバーとログで確認できます。  
失敗した項目は `Retry Failed` で再実行できます。

## 出力フォルダ

既定では、変換済みファイルは `Converted Folder` に出力されます。

設定により、プリセットごとのサブフォルダに分けて出力できます。

例:

```text
Converted/
  CarNavi_MP4_Standard/
  PortableDVD_MPG_Standard/
  Audio_MP3/
  Audio_MP4_AAC_Only/
```

## Copy Files モード

`Copy Files` モードでは、Queue 内のローカルファイルを指定フォルダへコピーできます。

これは、変換済みファイルを SD カードや USB メモリへ移す用途を想定しています。

`Number Prefix` に数値を入れると、その番号から連番を付けてコピーします。

例:

```text
Number Prefix: 16

016_TitleA.mp4
017_TitleB.mp4
018_TitleC.mp4
```

空欄の場合は連番を付けません。

## SDカード上の再生順について

一部のカーナビやメディアプレーヤーでは、ファイル名順ではなく、SDカードやUSBメモリ上の書き込み順で再生順が決まることがあります。

NaviMovie-Maker 本体では、FAT/exFAT のディレクトリエントリを直接編集する機能は持ちません。

再生順の物理反映が必要な場合は、Explorer でのコピーや UMSSort などの外部ツールの利用を想定しています。

## 設定

`Settings` から以下の項目を設定できます。

- Working Folder
- Temporary Folder
- Converted Folder
- Local Video Folder
- プリセット別サブフォルダ出力
- 表示する Output Preset

「普段使うプリセットだけを表示する」ことで、メイン画面を整理できます。

## 開発・ビルド

このアプリは C# / .NET / WPF で作成されています。

開発には .NET SDK が必要です。

```powershell
dotnet build
dotnet run
```

Release ビルド:

```powershell
dotnet build -c Release
```

配布用 publish 例:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

## 注意事項

- yt-dlp / FFmpeg は別途インストールが必要です
- 本アプリは YouTube 等の動画配信サイトからのダウンロードを推奨するものではありません
- 動画配信サイトの利用規約、著作権、権利者の許諾条件を確認したうえで利用してください
- 対応サイトは yt-dlp の対応状況に依存します
- サイト側の仕様変更により、一時的に取得できない場合があります
- 変換結果の再生可否は機器側の仕様に依存します
- カーナビやポータブルDVDプレーヤー向けプリセットは、実機確認しながら調整する前提です

## 今後の予定

### 複数サイト対応

- NicoNico
- Vimeo
- 外部 JSON によるサイト定義追加

### Pro Mode

- FFmpeg パラメータをより自由に設定できるユーザー定義プリセット
- カスタムプリセットの追加
- プリセットのインポート / エクスポート

### UI改善

- ライト / ダークテーマ
- OSテーマ連動
- ボタン・アイコン改善
- Queue タグ表示
- サムネイル表示の強化

### 配布改善

- アプリ用アイコン
- 少ないファイル構成での配布
- yt-dlp / FFmpeg インストール案内の強化

## ライセンス

未定。
