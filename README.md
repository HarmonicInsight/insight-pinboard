# Insight Pinboard

**ファイルのピンボード — プロジェクトごとにファイルを視覚的にグルーピングして管理するWindowsデスクトップアプリ**

![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![WPF](https://img.shields.io/badge/WPF-Windows-blue)
![License](https://img.shields.io/badge/license-MIT-green)

## 概要

Insight Pinboard は、ファイル・フォルダ・URL・メモを自由にキャンバス上に配置できるWindows用ピンボードアプリです。

**Fencesのグルーピング ＋ Miroの自由配置 ＋ ローカルファイルの直接起動** を一つにしたツールです。

## こんな人に

- 開発プロジェクトが多すぎてどこに何があるかわからない
- プロジェクトごとにファイルを視覚的にまとめたい
- デスクトップがアイコンだらけで整理できない
- exeやスクリプトをすぐ起動したい

## 主な機能

### キャンバス
- 無限キャンバスにファイル・フォルダ・URLをドラッグ&ドロップ
- 自由な位置に配置（グリッドスナップON/OFF）
- ズーム＆パン対応
- アイテムのサイズ変更

### ピンアイテム
- ファイル / フォルダ → クリックで直接起動
- URL → クリックでブラウザで開く
- メモ（付箋）→ テキストメモをキャンバスに配置
- アイテムにコメント（ラベル）を付加可能
- 色分け・アイコン表示

### ボード管理
- 複数ボード対応（タブ切替）
- ボードの追加・削除・リネーム
- 例：「町内会」「Harmonic Insight」「FPT案件A」

### グループ
- アイテムをグループ化（半透明の囲み）
- グループにタイトルを設定
- グループごと移動

### データ保存
- 自動保存（JSON形式でローカル保存）
- 手動エクスポート/インポート

## 技術スタック

- **言語:** C# 12
- **フレームワーク:** .NET 8 + WPF
- **保存形式:** JSON (System.Text.Json)
- **最小要件:** Windows 10 以降

## ビルド方法

```bash
# .NET 8 SDK が必要
dotnet build
dotnet run
```

## プロジェクト構造

```
InsightPinboard/
├── InsightPinboard.sln
├── InsightPinboard/
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / MainWindow.xaml.cs
│   ├── Models/
│   │   ├── PinItem.cs          # ピンアイテムのモデル
│   │   ├── PinGroup.cs         # グループのモデル
│   │   └── Board.cs            # ボードのモデル
│   ├── ViewModels/
│   │   ├── MainViewModel.cs    # メインウィンドウVM
│   │   ├── BoardViewModel.cs   # ボードVM
│   │   └── PinItemViewModel.cs # ピンアイテムVM
│   ├── Views/
│   │   ├── BoardCanvas.xaml     # キャンバスView
│   │   └── PinItemControl.xaml  # ピンアイテムView
│   ├── Services/
│   │   ├── StorageService.cs   # JSON保存/読込
│   │   └── FileIconService.cs  # ファイルアイコン取得
│   └── Helpers/
│       └── RelayCommand.cs     # ICommand実装
├── README.md
├── LICENSE
└── .gitignore
```

## ロードマップ

- [x] v0.1 — MVP：キャンバス＋ドラッグ&ドロップ＋ファイル起動
- [ ] v0.2 — グループ化＋色分け
- [ ] v0.3 — 複数ボード（タブ）
- [ ] v0.4 — メモ（付箋）＋コメント
- [ ] v0.5 — ズーム＆パン
- [ ] v1.0 — 正式リリース

## ライセンス

MIT License — 詳細は [LICENSE](LICENSE) を参照

## 開発

Harmonic Insight  
https://github.com/HarmonicInsight
