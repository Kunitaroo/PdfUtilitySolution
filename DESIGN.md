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
| PdfUtility.PreviewApp | C# | .NET 8.0 / WPF |

### PDF追記方式
**インクリメンタルアップデート**方式を採用する。

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
└── PdfUtility.PreviewApp    ← PDF座標確認ツール（WPF）
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
    └── PdfUtility.PreviewApp
```

**禁止事項：** CoreがSampleApp・PreviewAppを参照してはならない。

---

## 5. PdfUtility.Core フォルダ構成

```
PdfUtility.Core
├── PdfUtility.cs
├── PdfUtilityFactory.cs
├── Documents/
├── Drawing/
├── Services/
├── Barcode/
├── Options/
├── Results/
├── Exceptions/
├── Logging/
├── Helpers/
│   ├── FontHelper.cs
│   └── ...
└── Constants/
```

---

## 6. FontHelper 設計（重要）

TTC・TTF両対応。メモリ上のみで処理しファイル改造禁止。

### fsTypeによる埋め込み自動判定

| fsType値 | 意味 | EmbedFont |
|---------|------|-----------|
| 0x0000 | 埋め込み自由 | true |
| 0x0002 | 埋め込み禁止 | false（参照のみ） |
| 0x0008 | 編集可能な埋め込み許可 | true |

Windows 11のMS明朝・MSゴシックはfsType=0x0008（埋め込み許可）に更新済み。

### 主なTTCフォントインデックス

| ファイル | index 0 | index 1 | index 2 |
|---------|---------|---------|---------|
| msgothic.ttc | MSゴシック | MS Pゴシック | MS UIゴシック |
| msmincho.ttc | MS明朝 | MS P明朝 | MS UI明朝 |

---

## 7. PDF追記の技術仕様（インクリメンタルアップデート）

```
[既存PDFバイト列 ... %%EOF]
[追記オブジェクト群]
xref
trailer << /Size N /Root 1 0 R /Prev (既存xrefオフセット) >>
startxref
%%EOF
```

---

## 8. PdfUtility.PreviewApp 設計（WPF）

### 目的
PDF上の座標を視覚的に確認し、描画命令のC#/VB.NETコードを自動生成するローカルツール。

### 機能一覧

| 優先度 | 機能 | 内容 |
|--------|------|------|
| 🔴 必須 | PDFファイルを開く | ダイアログで選択 |
| 🔴 必須 | マウス座標表示 | PDF上でリアルタイムX・Y表示 |
| 🔴 必須 | クリックで座標取得 | クリックした座標を右パネルに表示 |
| 🔴 必須 | コード生成 | C# / VB.NET 切り替え対応 |
| 🔴 必須 | コードコピー | クリップボードにコピー |
| 🔴 必須 | グリッド表示 | ON/OFF切り替え |
| 🟡 推奨 | ページ切り替え | 複数ページ対応 |
| 🟡 推奨 | 範囲選択 | ドラッグでWidth・Height取得 |

### 画面レイアウト

```
┌─────────────────────────────────────────┐
│ PdfUtility 座標確認ツール                 │
│ [PDFを開く] [グリッド ON/OFF] [◀ 1/3 ▶] │
├────────────────────────┬────────────────┤
│                        │ 座標情報        │
│                        │ X :  142.5 pt  │
│                        │ Y :  380.2 pt  │
│   PDFビューア           │ Page :  1      │
│                        │────────────────│
│   ← マウスで操作        │ 選択範囲        │
│   ← ドラッグで範囲選択  │ W :  200.0 pt  │
│                        │ H :   50.0 pt  │
│                        │────────────────│
│                        │ 生成コード      │
│                        │ 言語:[C#][VB]  │
│                        │ ┌────────────┐ │
│                        │ │コード表示   │ │
│                        │ └────────────┘ │
│                        │ [コードをコピー]│
└────────────────────────┴────────────────┘
```

### フォルダ構成

```
PdfUtility.PreviewApp
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
├── ViewModels/
│   └── MainViewModel.cs        ← MVVM ViewModel
├── Services/
│   ├── PdfRenderService.cs     ← PDF→画像変換
│   └── CodeGeneratorService.cs ← C#/VB.NETコード生成
├── Models/
│   ├── CoordinateInfo.cs       ← 座標情報
│   └── CodeLanguage.cs         ← 言語選択列挙
└── Helpers/
    └── PdfCoordinateConverter.cs ← 画面座標↔PDF座標変換
```

### コード生成サービス設計

```csharp
public enum CodeLanguage { CSharp, VbNet }

public class CodeGeneratorService
{
    public string Generate(CoordinateInfo info, CodeLanguage language)
    {
        return language switch
        {
            CodeLanguage.CSharp => GenerateCSharp(info),
            CodeLanguage.VbNet  => GenerateVbNet(info),
            _ => ""
        };
    }
}
```

### 生成コードサンプル（クリック時）

**C#**
```csharp
new TextDrawCommand
{
    PageNumber = 1,
    X = 142.5,
    Y = 380.2,
    FontName = "MS明朝",
    FontSize = 10,
    FontColor = PdfColor.Black,
    HorizontalAlign = PdfHorizontalAlign.Left
};
```

**VB.NET**
```vb
New TextDrawCommand With {
    .PageNumber = 1,
    .X = 142.5,
    .Y = 380.2,
    .FontName = "MS明朝",
    .FontSize = 10,
    .FontColor = PdfColor.Black,
    .HorizontalAlign = PdfHorizontalAlign.Left
}
```

### PDF表示方式
- PDFをページ単位で画像（PNG）に変換してWPFのImageコントロールに表示
- PDF→画像変換にはWindows標準の `Windows.Data.Pdf` を使用（追加ライセンス不要）

### 座標変換の仕組み

```
画面座標（ピクセル・左上原点）
    ↓ PdfCoordinateConverter
PDF座標（ポイント・左下原点）

変換式：
pdfX = screenX / scaleX
pdfY = pageHeight - (screenY / scaleY)
```

---

## 9. 実装フェーズ

| フェーズ | 内容 | 状態 |
|---------|------|------|
| Phase 1 | PDF追記の骨格 | ✅ 完了 |
| Phase 2 | 図形・枠描画 | ✅ 完了 |
| Phase 3 | 画像埋め込み | ✅ 完了 |
| Phase 4 | FontEngine | ✅ 完了 |
| Phase 5 | テキスト描画 | ✅ 完了 |
| Phase 6 | PreviewApp（WPF） | 🔄 進行中 |

### Phase 6 実装ステップ
- Step 1：WPFプロジェクト作成・基本レイアウト
- Step 2：PDFファイルを開いて表示
- Step 3：マウス座標のリアルタイム表示
- Step 4：グリッド表示ON/OFF
- Step 5：クリックで座標取得・コード生成
- Step 6：C#/VB.NET切り替え対応
- Step 7：ドラッグによる範囲選択

---

## 10. v1で見送る機能

- 電子署名対応
- 暗号化PDFの編集
- 縦書き対応
- 全バーコード規格対応
- Web上でのPDF座標確認ツール公開
