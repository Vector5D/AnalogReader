# AnalogReader

![AnalogReader](/AnalogReader.png)

---

## 概要

AnalogReader は、カメラ映像から電圧計の値を読み取るアプリケーションです。  
RTSP動画再生ボタンをクリックすると設定した接続先の動画を再生します。  
撮影ボタンをクリックした時に電圧計を読み取ります。  
画像ファイル選択ボタンをクリックすると静止画から選択することもできます。  

---

## 動作環境

- OS: Windows 10/11
- Python: 3.10
- Visual Studio: 2022

---

## 環境構築

※ 簡便のため Python はグローバル環境に依存しています。  
必要に応じて仮想環境やライブラリ管理ツールをご利用ください。

以下のライブラリをインストールしてください

```bash
pip install opencv-python numpy ultralytics
```

---

## 使い方

1. **Visual Studio** で `AnalogReader.sln` を開く
2. `AnalogReader\App.config` の `cameraAddress` に自分の RTSP サーバーの URL を記載する
3. アプリを起動する

---

## 注意事項

* RTSP動画再生機能は、LILYGO の T-Camera S3 を依存しています。（設定情報は、 `LilygoT-CameraS3設定まとめ.txt` を参照してください）
* Arduino IDEを使用して、開発ボードにスケッチを書き込みます。（書き込みコードは、`sketch_INTERN_S3_RTSP_SSD1306` にあります）
* 動作保証はしていません。改良や実験のベースとしてご利用ください。
* OpenAI バージョンはこちら ー＞ https://github.com/Vector5D/AnalogReaderWithOpenAI
