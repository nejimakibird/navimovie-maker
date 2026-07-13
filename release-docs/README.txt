NaviMovie-Maker README
======================

NaviMovie-Maker は、ネット動画やローカル動画・音声を、車載ナビ、USB/SD 対応 DVD プレイヤー、タブレットなどで扱いやすい形式へ変換する Windows アプリです。
URL やローカルファイルをキューへ追加し、ダウンロード、変換、コピーをまとめて実行できます。


1. 動作環境
-----------

- Windows x64
- .NET 10 Desktop Runtime (Windows Desktop Runtime x64)

この配布物は framework-dependent です。.NET 10 Desktop Runtime が未導入の場合は、Microsoft の公式サイトから Windows Desktop Runtime x64 をインストールしてください。


2. インストールとアンインストール
---------------------------------

インストール:

1. GitHub Releases または Vector からアプリ本体の ZIP を取得します。
2. ZIP を任意の書き込み可能なフォルダへ展開します。
3. NaviMovieMaker.App.exe を実行します。

GitHub の「Source code (zip)」「Source code (tar.gz)」は開発者向けであり、アプリ本体ではありません。

アンインストール:

- アプリを終了し、展開した NaviMovie-Maker フォルダを削除します。
- 設定も削除する場合は、必要に応じて次のファイルを削除します。

  %APPDATA%\NaviMovie-Maker\settings.json


3. 外部ツールの準備
-------------------

NaviMovie-Maker は次の外部ツールを使用します。

- yt-dlp.exe: 対応 URL の情報取得とダウンロード
- ffmpeg.exe: 動画・音声変換
- ffprobe.exe: メディア情報確認
- mpv.exe: プレイリストのプレビュー再生

「ツール」メニューから外部ツールの確認、tools フォルダの表示、設定画面を開けます。yt-dlp と FFmpeg/ffprobe には取得支援があります。

mpv は標準配布 ZIP に同梱されず、自動取得も行いません。利用者が別途インストールし、「ツール」→「設定」→「外部ツール」で mpv.exe を選択してください。未指定の場合は tools フォルダ、次に PATH から検索します。

URL の取得・再生可否は、yt-dlp、mpv、対象サイトの仕様、地域制限、ログイン要否などに依存します。すべてのサイトでの動作を保証するものではありません。


4. プレイリスト
---------------

NaviMovie-Maker のプレイリストは、変換キュー、処理設定、出力先、追跡済み処理結果を .nmm-playlist.json に保存する再利用可能な処理定義です。

ショートカット:

- Ctrl+N: 新規プレイリスト
- Ctrl+O: プレイリストを開く
- Ctrl+S: 保存。初回だけ保存先を選択し、以後は同じファイルへ上書き
- Ctrl+Shift+S: 名前を付けて保存
- Ctrl+P: プレイリストを再生

プレイリストごとに出力フォルダを保存でき、別々のプレイリストで異なる出力先を使用できます。未保存の変更がある場合はタイトルに * が表示され、新規作成、別ファイルを開く操作、終了時に保存確認が表示されます。

各項目の「結果」列には、未処理、出力済み、連番未同期、出力なし、出力変更あり、要再処理、名前衝突を表示します。ソース、処理モード、プロファイル、サイズ、更新日時が一致する有効な既存結果は、再実行時に処理を省略します。所有者不明の既存ファイルは上書きせず、安定した識別子を使って出力名の衝突を回避します。

キューを並べ替えて連番がずれた場合は「出力ファイルの連番を同期」で、追跡済みの有効な結果だけの先頭連番を再変換せずに変更できます。

Ctrl+P は、再生可能な全項目に有効な結果があれば処理結果を、未処理項目があれば元のローカルファイル／URLをキュー順で再生します。元データと結果は混在させません。


5. 基本的な使い方
-----------------

1. Conversion Queue へ URL またはローカルファイルを追加します。
2. 処理種別と出力形式を選びます。
3. 必要に応じて出力フォルダを選びます。
4. キューを実行します。

通常モードの処理種別:

- ダウンロードのみ: URL の完成ダウンロードを作業フォルダへ保存
- ダウンロードして変換: URL を取得して変換、またはローカルファイルを変換
- 変換のみ: ローカルファイルを変換
- ファイルコピー: ローカルファイルを出力先へコピー

Simple Mode では URL またはローカルファイルを選択プリセットへ簡単に変換できます。


6. 変換ファイルと出力プリセット
---------------------------------

変換済みファイルは、プレイリストで選択した出力フォルダの下へ保存されます。「出力形式ごとにサブフォルダを作成」が有効な場合は、プリセット別のサブフォルダを使用します。ダウンロードのみの完成ファイルは、アプリで設定したダウンロード／作業先に保存されます。変換のための一時入力は最終出力ではありません。

有効な処理結果が追跡済みの場合は、再生成せず処理を省略することがあります。連番を有効にした出力では、ファイル名の先頭の連番がキュー内の再生順を表します。

車載ナビ向け MP4:

- Car Navi MP4 - Current Compatibility
- Car Navi MP4 - Small Size
- Car Navi MP4 - Standard
- Car Navi MP4 - High Quality

MP4 / H.264 / AAC の動画を作成します。Current Compatibility は既知の互換設定を維持するための選択肢です。Small Size は容量、Standard はバランス、High Quality は画質を優先します。車載機器ごとに対応コーデック、解像度、ビットレート、ファイルサイズ制限が異なるため、すべての機器での再生を保証するものではありません。

DVD プレイヤー向け MPG:

- Portable DVD Player MPG - Small Size (MP2 audio)
- Portable DVD Player MPG - Standard (MP2 audio)
- Portable DVD Player MPG - High Quality (MP2 audio)

USB/SD 再生対応 DVD プレイヤー向けの MPEG-2 動画／MP2 音声を含む .mpg ファイルを作成します。Small Size、Standard、High Quality の順に容量より画質を優先します。DVD-Video ディスク構造を作るオーサリング機能ではありません。

iPad／タブレット向け MP4:

- iPad / タブレット MP4 1080p 標準
- iPad / タブレット MP4 720p 互換
- iPad / タブレット HEVC 1080p 高圧縮

Android タブレット向け MP4:

- Androidタブレット MP4 1080p 標準
- Androidタブレット MP4 720p 互換
- Androidタブレット HEVC 1080p 高圧縮

1080p 標準と 720p 互換は MP4 / H.264 / AAC です。1080p 標準は画質、720p 互換は処理負荷と幅広い再生互換性を重視します。HEVC 1080p 高圧縮は MP4 / H.265(HEVC) / AAC で、比較的新しい機器で容量を抑えたい場合に向きますが、古い機器や再生アプリでは再生できないことがあります。互換性を優先する場合は、まず H.264 の 720p 互換または 1080p 標準を試してください。

音声抽出・音声変換:

- Audio MP4 AAC Only - High / Medium / Low: 音声だけを含む .mp4
- MP3 - High / Medium / Low: 一般的な .mp3
- M4A AAC - High / Medium / Low: AAC 音声の .m4a
- WAV PCM 16bit: 非圧縮の .wav
- FLAC Lossless: 可逆圧縮の .flac
- OGG Vorbis - High / Medium / Low: Vorbis 音声の .ogg
- WMA - High / Medium / Low: Windows Media Audio の .wma

音声プリセットは映像を含まないファイルを作成します。High、Medium、Low は、おおむね音質とファイルサイズのどちらを優先するかで選択します。対応形式は再生機器やアプリによって異なり、特に OGG、WMA、FLAC は事前に再生対応を確認してください。


7. 配布元とWindowsの警告
------------------------

NaviMovie-Maker は GitHub Releases と Vector から配布します。

本アプリはコード署名されていないため、正規の配布元から入手した場合でも Windows SmartScreen や Smart App Control の警告が表示されることがあります。配布元にかかわらず警告が表示されないことを保証するものではありません。


8. 利用上の注意
---------------

- 各動画サイトの利用規約、著作権法、権利者の許諾条件を守ってください。
- 権利のないコンテンツの保存、変換、再配布を行わないでください。
- NaviMovie-Maker は違法なダウンロードや権利侵害を推奨しません。
- 出力ファイルの再生可否は、利用する機器や再生ソフトの仕様に依存します。
- FFmpeg、ffprobe、yt-dlp、mpv は別の第三者プロジェクトです。


9. 作者・ライセンス
-------------------

作者: nejimakibird
Webサイト: https://ooojouhoukan.truthlr.com/
GitHub: https://github.com/nejimakibird/NaviMovie-Maker
不具合報告: https://github.com/nejimakibird/NaviMovie-Maker/issues

NaviMovie-Maker は MIT License で提供されます。詳細は同梱の LICENSE と NOTICE.txt を参照してください。
