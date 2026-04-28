# PdfUtilitySolution 設計書

## 1. プロジェクト概要

### 目的
既存PDFファイルに対して、テキスト・画像・図形・枠を追記できる業務帳票向けDLLの自作。

### 背景
- 社内システムでPDF編集処理が必要
- 既存OSSはTTC形式の日本語フォント（MS明朝・MSゴシック）に対応していない
- ライセンス問題を避けるため完全自作DLLを開発する

### スコープ

| 機能 | 対象 |
|------|------|
| 既存PDFへの追記（テキスト・画像・図形・枠） | ✅ 対象 |
| 新規PDFの作成 | ❌ 対象外 |
| 既存PDF内の文字編集 | ❌ 対象外 |
| 電子署名・暗号化PDF対応 | ❌ 対象外 |

---

## 2. 技術方針

### 言語・フレームワーク

| プロジェクト | 言語 | ターゲット |
|------------|------|-----------|
| PdfUtility.Core | C# | .NET Standard 2.0 |
| PdfUtility.Barcode | C# | .NET Standard 2.0 |
| PdfUtility.SampleApp | C# | .NET 8.0 / Console App |
| PdfUtility.Tests | C# | .NET 8.0 / MSTest |
| PdfUtility.PreviewWeb | C# | .NET 8.0 / ASP.NET Core |

### PDF追記方式
**インクリメンタルアップデート**方式を採用する。

```
既存PDFバイト列（一切変更しない）
    ↓ 末尾に追加
追記コンテンツ（新オブジェクト群）
新xrefテーブル
新トレイラー
%%EOF
```

既存部分を触らないため、元PDFの破損リスクがゼロ。

### フォント対応方針

| 形式 | 対応 | 代表例 |
|------|------|--------|
| TTC | ✅ | MS明朝、MSゴシック、メイリオ |
| TTF | ✅ | IPAex明朝、Noto Sans JP |

TTCはメモリ上でTTFバイト列に展開して使用する。
フォントファイルの改造・再配布は行わない（ライセンス遵守）。

---

## 3. ソリューション構成

```
PdfUtilitySolution
├── PdfUtility.Core          ← メインDLL
├── PdfUtility.Barcode       ← バーコード専用DLL
├── PdfUtility.SampleApp     ← 動作確認用コンソールアプリ
├── PdfUtility.Tests         ← 単体テスト
└── PdfUtility.PreviewWeb    ← 配置確認用WebApp（後から追加）
```

---

## 4. プロジェクト参照関係

```
PdfUtility.Core
    ↑
PdfUtility.Barcode
    ↑
    ├── PdfUtility.SampleApp
    ├── PdfUtility.Tests
    └── PdfUtility.PreviewWeb（後から追加）
```

**禁止事項：** CoreがSampleApp・PreviewWebを参照してはならない。

---

## 5. PdfUtility.Core フォルダ構成

```
PdfUtility.Core
├── PdfUtility.cs                  ← 公開APIの中心クラス
├── PdfUtilityFactory.cs           ← インスタンス生成
├── Documents/
│   ├── PdfDocumentContext.cs      ← 内部保持用（開いているPDF状態）
│   ├── PdfDocumentInfo.cs         ← PDF情報（ページ数・タイトル等）
│   ├── PdfPageContext.cs          ← ページ内部情報
│   └── PdfPageInfo.cs             ← ページサイズ情報
├── Drawing/
│   ├── PdfDrawCommand.cs          ← 描画命令の抽象基底クラス
│   ├── TextDrawCommand.cs         ← テキスト描画
│   ├── RectangleDrawCommand.cs    ← 矩形描画
│   ├── LineDrawCommand.cs         ← 線描画
│   ├── ImageDrawCommand.cs        ← 画像埋め込み
│   ├── BarcodeDrawCommand.cs      ← バーコード貼付
│   ├── PdfColor.cs                ← RGB色指定
│   ├── PdfHorizontalAlign.cs      ← 水平配置列挙
│   ├── PdfVerticalAlign.cs        ← 垂直配置列挙
│   └── PdfBarcodeType.cs          ← バーコード種別列挙
├── Services/
│   ├── IPdfReader.cs
│   ├── IPdfWriter.cs
│   ├── IPdfRenderer.cs
│   ├── IPdfMerger.cs
│   ├── IPdfSplitter.cs
│   ├── PdfReaderService.cs        ← 既存PDF読み込み
│   ├── PdfWriterService.cs        ← PDF保存・追記
│   ├── PdfRendererService.cs      ← 描画命令の適用（最重要）
│   ├── PdfMergerService.cs
│   └── PdfSplitterService.cs
├── Barcode/
│   ├── IBarcodeGenerator.cs
│   ├── BarcodeGenerateOptions.cs
│   └── BarcodeGenerateResult.cs
├── Factories/
│   └── PdfUtilityFactory.cs
├── Options/
│   └── PdfUtilityOptions.cs
├── Results/
│   └── PdfProcessResult.cs
├── Exceptions/
│   ├── PdfException.cs
│   ├── PdfLoadException.cs
│   ├── PdfRenderException.cs
│   ├── PdfValidationException.cs
│   └── BarcodeGenerateException.cs
├── Logging/
│   ├── ILogger.cs
│   ├── FileLogger.cs
│   └── NullLogger.cs
├── Helpers/
│   ├── FontHelper.cs              ← TTC/TTF読み込み・展開（重要）
│   ├── PdfCoordinateHelper.cs
│   ├── PdfValidationHelper.cs
│   └── StreamHelper.cs
└── Constants/
    └── PdfDefaultValues.cs
```

---

## 6. 主要クラス設計

### PdfUtility.cs（公開APIの中心）

```csharp
public class PdfUtility
{
    public void Load(string filePath);
    public void Load(byte[] pdfBytes);
    public PdfDocumentInfo GetDocumentInfo();
    public void ApplyCommand(PdfDrawCommand command);
    public void ApplyCommands(IEnumerable<PdfDrawCommand> commands);
    public void Save(string outputPath);
    public byte[] SaveToBytes();
    public byte[] Merge(IEnumerable<string> files);
    public byte[] ExtractPages(IEnumerable<int> pageNumbers);
    public IEnumerable<byte[]> SplitAllPages();
}
```

### PdfDrawCommand.cs（描画命令の抽象基底）

```csharp
public abstract class PdfDrawCommand
{
    public int PageNumber { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public int ZIndex { get; set; }
    public string Name { get; set; }
}
```

### TextDrawCommand.cs

```csharp
public class TextDrawCommand : PdfDrawCommand
{
    public string Text { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string FontName { get; set; }
    public double FontSize { get; set; }
    public PdfColor FontColor { get; set; }
    public bool IsBold { get; set; }
    public PdfHorizontalAlign HorizontalAlign { get; set; }
    public PdfVerticalAlign VerticalAlign { get; set; }
    public bool MultiLine { get; set; }
}
```

### RectangleDrawCommand.cs

```csharp
public class RectangleDrawCommand : PdfDrawCommand
{
    public double Width { get; set; }
    public double Height { get; set; }
    public PdfColor BorderColor { get; set; }
    public PdfColor FillColor { get; set; }
    public double BorderWidth { get; set; }
    public bool IsFilled { get; set; }
}
```

### LineDrawCommand.cs

```csharp
public class LineDrawCommand : PdfDrawCommand
{
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public PdfColor LineColor { get; set; }
    public double LineWidth { get; set; }
}
```

### ImageDrawCommand.cs

```csharp
public class ImageDrawCommand : PdfDrawCommand
{
    public byte[] ImageBytes { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool KeepAspectRatio { get; set; }
}
```

### PdfUtilityOptions.cs

```csharp
public class PdfUtilityOptions
{
    public bool EnableLogging { get; set; }
    public string LogPath { get; set; }
    public string DefaultFontName { get; set; }
    public int DefaultTtcFontIndex { get; set; } = 0;
    public bool RejectSignedPdf { get; set; }
    public bool RejectEncryptedPdf { get; set; }
}
```

### PdfColor.cs

```csharp
public class PdfColor
{
    public int R { get; set; }
    public int G { get; set; }
    public int B { get; set; }

    public static PdfColor Black => new PdfColor { R = 0, G = 0, B = 0 };
    public static PdfColor White => new PdfColor { R = 255, G = 255, B = 255 };
    public static PdfColor Red   => new PdfColor { R = 255, G = 0, B = 0 };
}
```

---

## 7. FontHelper 設計（重要）

TTC・TTF両対応。ファイルの改造・保存は行わずメモリ上のみで処理する。

```csharp
public static class FontHelper
{
    // TTC・TTF両対応の入口
    public static byte[] LoadFontBytes(string fontPath, int ttcIndex = 0)
    {
        string ext = Path.GetExtension(fontPath).ToLower();
        return ext switch
        {
            ".ttc" => ExtractTtfFromTtc(fontPath, ttcIndex),
            ".ttf" => File.ReadAllBytes(fontPath),
            _ => throw new PdfException($"未対応のフォント形式: {ext}")
        };
    }

    // TTCからTTFバイト列をメモリ展開
    private static byte[] ExtractTtfFromTtc(string ttcPath, int fontIndex)
    {
        byte[] ttcData = File.ReadAllBytes(ttcPath);
        // "ttcf" ヘッダ検証
        // オフセット8: フォント数
        // オフセット12 + index*4: 対象フォントのオフセット
        // そのオフセット以降をTTFバイト列として返す
    }
}
```

### 主なTTCフォントインデックス

| ファイル | index 0 | index 1 | index 2 |
|---------|---------|---------|---------|
| msgothic.ttc | MSゴシック | MS Pゴシック | MS UIゴシック |
| msmincho.ttc | MS明朝 | MS P明朝 | MS UI明朝 |

---

## 8. PDF追記の技術仕様

### インクリメンタルアップデートの構造

```
[既存PDFバイト列 ... %%EOF]
[追記オブジェクト群]
xref
（新オブジェクトのオフセット）
trailer
<< /Size (全オブジェクト数) /Root 1 0 R /Prev (既存xrefのオフセット) >>
startxref
（新xrefのオフセット）
%%EOF
```

### 既存PDFから取得が必要な情報

| 情報 | 取得方法 |
|------|---------|
| 最大オブジェクト番号 | 既存xrefテーブルを解析 |
| 既存xrefのオフセット | startxrefの値を取得 |
| Rootオブジェクト番号 | trailerの/Rootを取得 |
| ページオブジェクト番号 | Pages → Kidsを辿る |

---

## 9. 実装フェーズ

### Phase 1：PDF追記の骨格（最初に実装）
- 既存PDFのバイト列読み込み
- インクリメンタルアップデート形式での末尾追記
- 空コンテンツでも追記してファイルが開けることを確認

### Phase 2：図形・枠描画
- 矩形（RectangleDrawCommand）
- 線（LineDrawCommand）

### Phase 3：画像埋め込み
- JPEG画像のバイト埋め込み（ImageDrawCommand）

### Phase 4：FontEngine
- TTC/TTFのメモリ展開
- フォントテーブル解析（cmap・hmtx）

### Phase 5：テキスト描画
- 日本語テキストのグリフ埋め込み（TextDrawCommand）

---

## 10. 利用イメージ

```csharp
var options = new PdfUtilityOptions
{
    EnableLogging = true,
    DefaultFontName = "MS明朝"
};

var pdf = PdfUtilityFactory.Create(options);
pdf.Load("input.pdf");

var commands = new List<PdfDrawCommand>
{
    new TextDrawCommand
    {
        PageNumber = 1,
        X = 100, Y = 120,
        Text = "テスト出力",
        FontName = "MS明朝",
        FontSize = 10,
        FontColor = PdfColor.Black,
        HorizontalAlign = PdfHorizontalAlign.Left
    },
    new RectangleDrawCommand
    {
        PageNumber = 1,
        X = 50, Y = 50,
        Width = 200, Height = 100,
        BorderColor = PdfColor.Black,
        BorderWidth = 1.0,
        IsFilled = false
    }
};

pdf.ApplyCommands(commands);
pdf.Save("output.pdf");
```

---

## 11. v1で見送る機能

- 電子署名対応
- 暗号化PDFの編集
- 縦書き対応
- 全バーコード規格対応
- ブラウザ上でのPDF直接編集
