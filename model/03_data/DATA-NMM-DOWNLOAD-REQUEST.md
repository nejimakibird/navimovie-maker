---
type: data_object
id: DATA-NMM-DOWNLOAD-REQUEST
name: Download Request
kind: request
data_format: object
tags:
  - DataObject
  - NaviMovieMaker
---

# Download Request

## Summary

URL の動画情報取得またはダウンロードに必要な入力値。`VideoDownloadService.DownloadAsync` と `VideoMetadataService.FetchVideoListAsync` に渡される。

## Fields

| name | label | type | length | required | path | ref | notes |
|---|---|---|---:|---|---|---|---|
| url | URL | string |  | Y | $.url |  | HTTP / HTTPS |
| video | 動画候補 | object |  | N | $.video | [[DATA-NMM-QUEUE-ITEM]] | ダウンロード時 |
| workingFolder | 作業フォルダ | string |  | Y | $.workingFolder | [[DATA-NMM-APP-SETTINGS]].WorkingFolder | 出力テンプレート |
| downloadOrder | ダウンロード順 | number |  | N | $.downloadOrder |  | 連番 |
| downloadProfile | プロファイル | object |  | N | $.downloadProfile |  | DownloadProfileOption |
| addNumberPrefix | 連番付与 | boolean |  | N | $.addNumberPrefix |  | 既定 true |

## Notes

- このデータは情報取得とダウンロードで使う入力を論理的にまとめたもの。

## Source Links

| path | notes |
|---|---|
| NaviMovieMaker.App/Services/VideoDownloadService.cs | DownloadAsync 引数 |
| NaviMovieMaker.App/Services/VideoMetadataService.cs | FetchVideoListAsync 引数 |
| NaviMovieMaker.App/Services/DownloadProfileCatalog.cs | プロファイル |
