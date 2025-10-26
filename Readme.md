# AnalogReader

![AnalogReader](/AnalogReader.png)

---

## 概要

AnalogReader は、カメラ映像から電圧計の値を読み取るアプリケーションです。  
RTSP動画再生ボタンをクリックすると設定した接続先の動画を再生します。  
撮影ボタンをクリックした時に電圧計を読み取ります。  
画像ファイル選択ボタンをクリックすると静止画から選択することもできます。  

---

## 特徴
- RTSP カメラ映像または画像ファイルに対応
- Python スクリプトによる針検出
- C# アプリケーションとのシームレス連携
- ハードウェアとして LILYGO T-Camera S3 をサポート

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

## プロジェクト構成

```
AnalogReader/
├── AnalogReader.sln
├── AnalogReader/
│   ├── PythonScript/
│   │   ├── main.py                        # Image processing script
│   │   └── best.pt                        # YOLO model file
│   ├── App.config                         # Configuration file
│   ├── FormMain.cs                        # Main form
│   └── Program.cs                         # Application entry point
```

---

## C# と Python の連携

アプリケーションは C# から Python スクリプトを呼び出し、電圧値の解析を行います。
呼び出し時に画像ファイルパスを JSON で Python 側へ渡し、処理結果を C# 側で受け取ります。

- 呼び出しスクリプト: `AnalogReader/PythonScript/main.py`
- モデルファイル: `AnalogReader/PythonScript/best.pt`
- C# 実装箇所: `FormMain.cs` → `DetectNeedleWithPython()` メソッド

---

## 使い方

1. **Visual Studio** で `AnalogReader.sln` を開く
2. `AnalogReader\App.config` の `cameraAddress` に自分の RTSP サーバーの URL を記載する
3. アプリを起動する

---

## ハードウェア要件

* RTSP カメラ：LILYGO T-Camera S3
* Arduino IDE にてスケッチ `sketch_INTERN_S3_RTSP_SSD1306` をボードへ書き込み
* 参考：`LilygoT-CameraS3設定まとめ.txt`

---

## 注意事項

* 動作保証はしていません。改良や実験のベースとしてご利用ください。
* OpenAI バージョンはこちら ー＞ https://github.com/Vector5D/AnalogReaderWithOpenAI
