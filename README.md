# PdfUtility

**日本語フォント（TTC/TTF）に完全対応した .NET 用 PDF 編集ライブラリ**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Standard](https://img.shields.io/badge/.NET%20Standard-2.0-green.svg)](https://docs.microsoft.com/ja-jp/dotnet/standard/net-standard)
[![言語](https://img.shields.io/badge/言語-C%23-blue.svg)](https://docs.microsoft.com/ja-jp/dotnet/csharp/)

---

## 📖 このライブラリについて

**PdfUtility** は、既存の PDF ファイルにテキスト・画像・図形・枠を追記できる .NET 向け PDF 編集ライブラリです。

### 開発した理由

PDFSharp などの既存 OSS ライブラリは **TTC 形式の日本語フォント（MS明朝・MSゴシック）に対応していません。**

英語のドキュメントで使い方が分かりにくいライブラリが多い中、**日本人開発者が使いやすい日本語フォント対応の PDF ライブラリ**を目指して開発しました。

### 特徴

- ✅ **TTC / TTF 両対応** — MS明朝・MSゴシック・メイリオ等の日本語フォントをそのまま使用可能
- ✅ **フォントライセンス自動判定** — OS/2テーブルのfsTypeを読み取り埋め込み可否を自動判断
- ✅ **インクリメンタルアップデート方式** — 既存PDFのバイト列を一切変更せず末尾に追記
- ✅ **縦書き対応** — 日本語帳票に必要な縦書きテキスト出力
- ✅ **縦中横対応** — 縦書き内の英数字を正立表示
- ✅ **暗号化PDF対応** — パスワード保護・パーミッション設定PDFを安全に検出
- ✅ **シンプルなAPI** — C# / VB.NET どちらでも使いやすい設計
- ✅ **座標確認ツール付き** — WPFアプリで追記位置をビジュアルに確認可能

---

## 🔧 動作環境

| 項目 | 内容 |
|------|------|
| OS | Windows 10 / 11 |
| フレームワーク | .NET Standard 2.0 |
| 言語 | C# / VB.NET |
| Visual Studio | 2019 以降 |

---

## 📦 プロジェクト構成

```
PdfUtilitySolution
├── PdfUtility.Core          ← メインDLL（配布物）
├── PdfUtility.Barcode       ← バーコード専用DLL
├── PdfUtility.SampleApp     ← 動作確認用コンソールアプリ
├── PdfUtility.Tests         ← 単体テスト（80件）
└── PdfUtility.PreviewApp    ← PDF座標確認ツール（WPF）
```

---

## 🚀 使い方

### 1. 参照を追加

プロジェクトに `PdfUtility.Core.dll` を参照追加してください。

### 2. 基本的な使い方（C#）

```csharp
using PdfUtility.Core;
using PdfUtility.Core.Drawing;

// オプション設定
var options = new PdfUtilityOptions
{
    EnableLogging = true,
    DefaultFontName = "MS明朝"
};

// インスタンス生成
var pdf = PdfUtilityFactory.Create(options);

// 既存PDFを読み込む
pdf.Load(@"C:\template\invoice.pdf");

// 描画命令を準備
var commands = new List<PdfDrawCommand>
{
    // テキスト出力
    new TextDrawCommand
    {
        PageNumber = 1,
        X = 100, Y = 700,
        Text = "株式会社サンプル",
        FontName = "MS明朝",
        FontSize = 12,
        FontColor = PdfColor.Black,
        HorizontalAlign = PdfHorizontalAlign.Left
    },
    // 矩形（枠）
    new RectangleDrawCommand
    {
        PageNumber = 1,
        X = 50, Y = 650,
        Width = 300, Height = 30,
        BorderColor = PdfColor.Black,
        BorderWidth = 1.0,
        IsFilled = false
    }
};

// 実行して保存
pdf.ApplyCommands(commands);
pdf.Save(@"C:\output\result.pdf");
```

### 3. VB.NET での使い方

```vb
Imports PdfUtility.Core
Imports PdfUtility.Core.Drawing

Dim options As New PdfUtilityOptions With {
    .EnableLogging = True,
    .DefaultFontName = "MS明朝"
}

Dim pdf = PdfUtilityFactory.Create(options)
pdf.Load("C:\template\invoice.pdf")

Dim commands As New List(Of PdfDrawCommand) From {
    New TextDrawCommand With {
        .PageNumber = 1,
        .X = 100,
        .Y = 700,
        .Text = "株式会社サンプル",
        .FontName = "MS明朝",
        .FontSize = 12,
        .FontColor = PdfColor.Black,
        .HorizontalAlign = PdfHorizontalAlign.Left
    }
}

pdf.ApplyCommands(commands)
pdf.Save("C:\output\result.pdf")
```

---

## 📐 描画命令一覧

### TextDrawCommand（テキスト）

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| PageNumber | int | ページ番号（1始まり） |
| X / Y | double | 座標（ポイント単位） |
| Text | string | 出力するテキスト |
| FontName | string | フォント名（例：MS明朝） |
| FontSize | double | フォントサイズ（ポイント） |
| FontColor | PdfColor | 文字色 |
| IsBold | bool | 太字 |
| HorizontalAlign | PdfHorizontalAlign | 水平配置（Left/Center/Right） |
| WritingMode | WritingMode | 横書き / 縦書き（v2追加） |
| TateChuYoko | bool | 縦中横を有効にするか（v2.1追加） |
| TateChuYokoMaxChars | int | 縦中横の最大文字数（デフォルト2） |

### RectangleDrawCommand（矩形）

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| X / Y | double | 左上の座標 |
| Width / Height | double | サイズ |
| BorderColor | PdfColor | 枠線の色 |
| BorderWidth | double | 枠線の太さ |
| FillColor | PdfColor | 塗りつぶしの色 |
| IsFilled | bool | 塗りつぶしするか |

### LineDrawCommand（線）

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| X / Y | double | 始点座標 |
| X2 / Y2 | double | 終点座標 |
| LineColor | PdfColor | 線の色 |
| LineWidth | double | 線の太さ |

### ImageDrawCommand（画像）

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| X / Y | double | 左上の座標 |
| Width / Height | double | 表示サイズ |
| ImageBytes | byte[] | 画像データ（JPEG/PNG） |
| KeepAspectRatio | bool | アスペクト比を維持するか |

---

## 🈳 縦書き対応（v2）

日本語帳票に必要な縦書きテキスト出力に対応しています。

### 基本的な縦書き（C#）

```csharp
new TextDrawCommand
{
    PageNumber = 1,
    X = 500, Y = 750,
    Text = "山路を登りながら考えた。",
    FontName = "MS明朝",
    FontSize = 12,
    FontColor = PdfColor.Black,
    WritingMode = WritingMode.Vertical  // 縦書き指定
}
```

### 縦中横（C#）

縦書き内の英数字を正立表示します。

```csharp
new TextDrawCommand
{
    PageNumber = 1,
    X = 500, Y = 750,
    Text = "令和5年12月25日",
    FontName = "MS明朝",
    FontSize = 12,
    WritingMode = WritingMode.Vertical,
    TateChuYoko = true,          // 縦中横を有効化
    TateChuYokoMaxChars = 2      // 2文字以内を横向きで表示
}
```

### 文字分類と表示方法

| 文字種別 | 対象 | 表示方法 |
|---------|------|---------|
| Upright（直立） | 漢字・ひらがな・カタカナ | 正立で表示 |
| Rotated（90°回転） | ASCII・ダッシュ類 | 時計回りに回転 |
| UpperRight（右上） | 句読点（。、.,） | セル右上にオフセット |
| 縦中横 | 連続する半角英数字 | 正立で表示（TateChuYoko=true時） |

---

## 🔒 暗号化・パーミッションPDF対応（v2）

### 自動検出機能

PDFを読み込む際に暗号化・パーミッション設定を自動検出します。

```csharp
var options = new PdfUtilityOptions
{
    RejectEncryptedPdf = true,         // パスワード保護PDFを拒否
    RejectPermissionLockedPdf = true   // 編集禁止PDFを拒否
};
```

### 検出される状態

| 状態 | 動作 |
|------|------|
| パスワード保護PDF | PdfLoadExceptionをスロー |
| 編集禁止PDF | PdfLoadExceptionをスロー |
| 通常のPDF | そのまま処理続行 |

---

## 🔤 フォントについて

### 対応フォント形式

| 形式 | 代表例 |
|------|--------|
| TTC（TrueType Collection） | MS明朝・MSゴシック・メイリオ |
| TTF（TrueType Font） | IPAex明朝・Noto Sans JP |

### TTCフォントのインデックス

| ファイル名 | index 0 | index 1 | index 2 |
|-----------|---------|---------|---------|
| msmincho.ttc | MS明朝 | MS P明朝 | MS UI明朝 |
| msgothic.ttc | MSゴシック | MS Pゴシック | MS UIゴシック |

### フォント埋め込みの自動判定

| fsType | 意味 | 動作 |
|--------|------|------|
| 0x0000 | 埋め込み自由 | 埋め込みあり |
| 0x0002 | 埋め込み禁止 | 参照のみ |
| 0x0008 | 埋め込み許可 | 埋め込みあり |

> **注意：** Windows 11 の MS明朝・MSゴシックは fsType=0x0008（埋め込み許可）に更新されています。

---

## 🖥️ PDF座標確認ツール（PreviewApp）

付属の WPF アプリを使うと、PDF上の座標を視覚的に確認できます。

### 主な機能

- PDF ファイルを開いてビューアに表示
- マウスを動かすとリアルタイムで座標を表示
- クリックした座標から **C# / VB.NET のコードを自動生成**
- テキスト・枠・画像をPDF上に重ねてプレビュー表示
- プレビュー要素をドラッグで位置調整
- 全要素のコードを一括生成

### 使い方

```
PdfUtility.PreviewApp.exe を起動
    ↓
「PDF を開く」でテンプレートPDFを選択
    ↓
マウスで座標を確認
    ↓
右パネルの「コードをコピー」でコードを取得
    ↓
Visual Studio に貼り付けて使用
```

---

## ⚙️ 設定オプション（PdfUtilityOptions）

| プロパティ | 型 | デフォルト | 説明 |
|-----------|-----|-----------|------|
| EnableLogging | bool | false | ログ出力を有効にするか |
| LogPath | string | null | ログファイルのパス |
| DefaultFontName | string | null | デフォルトのフォント名 |
| DefaultTtcFontIndex | int | 0 | TTCのデフォルトインデックス |
| RejectSignedPdf | bool | false | 署名済みPDFを拒否するか |
| RejectEncryptedPdf | bool | false | 暗号化PDFを拒否するか |
| RejectPermissionLockedPdf | bool | false | 編集禁止PDFを拒否するか（v2追加） |

---

## ⚠️ 例外クラス

| 例外クラス | 発生タイミング |
|-----------|--------------|
| PdfException | PDF処理全般の基底例外 |
| PdfLoadException | PDF読み込み失敗 |
| PdfRenderException | 描画命令の適用失敗 |
| PdfValidationException | 入力値のバリデーションエラー |

```csharp
try
{
    pdf.Load("input.pdf");
    pdf.ApplyCommands(commands);
    pdf.Save("output.pdf");
}
catch (PdfLoadException ex)
{
    Console.WriteLine($"PDF読み込みエラー: {ex.Message}");
}
catch (PdfRenderException ex)
{
    Console.WriteLine($"描画エラー: {ex.Message}");
}
catch (PdfException ex)
{
    Console.WriteLine($"PDFエラー: {ex.Message}");
}
```

---

## 🧪 テスト

単体テストは MSTest で実装されています。

```
テスト件数：80件
全件合格確認済み
```

| テストクラス | 件数 | 内容 |
|------------|------|------|
| FontEngineTest | 9件 | TTC/TTF読み込み・fsType判定 |
| PdfCoordinateTest | 15件 | 座標変換 |
| PdfImageDrawTest | 8件 | 画像埋め込み |
| PdfIncrementalWriteTest | 15件 | PDF追記・暗号化検出 |
| PdfShapeDrawTest | 10件 | 図形・枠描画 |
| PdfTextDrawTest | 12件 | テキスト描画 |
| PdfVerticalTextTest | 11件 | 縦書き・縦中横 |

---

## 📋 バージョン履歴

### v2.1
- 縦中横（たてちゅうよこ）対応
- 縦書き内の英数字を正立表示

### v2.0
- 縦書きテキスト対応
- 暗号化・パーミッションPDF検出対応
- RejectPermissionLockedPdfオプション追加

### v1.0
- 既存PDFへのテキスト・図形・画像の追記
- TTC/TTF日本語フォント対応
- fsTypeによるフォント埋め込み自動判定
- PDF座標確認ツール（PreviewApp）

---

## 📋 スコープ

| 機能 | 対応状況 |
|------|---------|
| 既存PDFへの追記 | ✅ 対応 |
| テキスト・図形・画像の追記 | ✅ 対応 |
| TTC / TTF フォント | ✅ 対応 |
| 縦書き・縦中横 | ✅ 対応（v2） |
| 暗号化PDF検出 | ✅ 対応（v2） |
| 新規PDFの作成 | ❌ 対象外 |
| 既存テキストの編集 | ❌ 対象外 |

---

## 📄 ライセンス

MIT License — 詳細は [LICENSE](LICENSE) をご覧ください。

商用利用・改変・再配布は自由です。
ただし **フォントファイル（.ttc / .ttf）は各フォントのライセンスに従ってください。**

---

## 👤 作者

**Kunitaroo**

- GitHub: [@Kunitaroo](https://github.com/Kunitaroo)
- Blog: [くんちゃんのゲーム研究室](https://gamelablog.com)
