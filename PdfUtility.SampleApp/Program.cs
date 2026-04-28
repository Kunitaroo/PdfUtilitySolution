using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using PdfUtility.Core;
using PdfUtility.Core.Drawing;
using PdfUtility.Core.Exceptions;
using PdfUtility.Core.Helpers;

// ─────────────────────────────────────────────────────────────────────────
// ディレクトリ設定
// ─────────────────────────────────────────────────────────────────────────

string testdataDir = FindDir("testdata") ?? Path.Combine(AppContext.BaseDirectory, "testdata");
// output は testdata と同じ階層（ソリューションルート）に作成する
string solutionDir = Directory.GetParent(testdataDir)?.FullName ?? AppContext.BaseDirectory;
string outputDir   = Path.Combine(solutionDir, "output");
string fontsDir    = Path.Combine(testdataDir, "fonts");
Directory.CreateDirectory(outputDir);
Directory.CreateDirectory(testdataDir);

// ─────────────────────────────────────────────────────────────────────────
// 入力PDFの決定
// ─────────────────────────────────────────────────────────────────────────

string inputPdfPath = Path.Combine(testdataDir, "PDF編集.pdf");
byte[] inputBytes;

if (File.Exists(inputPdfPath))
{
    inputBytes = File.ReadAllBytes(inputPdfPath);
    Console.WriteLine($"入力: {Path.GetFileName(inputPdfPath)} ({inputBytes.Length:N0} bytes)");
}
else
{
    inputBytes = CreateMinimalA4Pdf();
    Console.WriteLine("入力: 自作ミニマルPDF（A4縦）");
}

// ─────────────────────────────────────────────────────────────────────────
// Phase 2: 矩形・線の描画（既存）
// ─────────────────────────────────────────────────────────────────────────

Console.WriteLine("\n═══ Phase 2: 矩形・線 ═══");
RunPhase2(inputBytes, outputDir);

// ─────────────────────────────────────────────────────────────────────────
// Phase 3: 画像埋め込み
// ─────────────────────────────────────────────────────────────────────────

Console.WriteLine("\n═══ Phase 3: 画像埋め込み ═══");
RunPhase3(inputBytes, testdataDir, outputDir);

// ─────────────────────────────────────────────────────────────────────────
// Phase 4: FontEngine
// ─────────────────────────────────────────────────────────────────────────

Console.WriteLine("\n═══ Phase 4: FontEngine ═══");
RunPhase4(fontsDir);

// ─────────────────────────────────────────────────────────────────────────
// Phase 5: テキスト描画
// ─────────────────────────────────────────────────────────────────────────

Console.WriteLine("\n═══ Phase 5: テキスト描画 ═══");
RunPhase5(inputBytes, fontsDir, outputDir);

// ─────────────────────────────────────────────────────────────────────────
// グリフID診断
// ─────────────────────────────────────────────────────────────────────────

Console.WriteLine("\n═══ グリフID診断 ═══");
RunGlyphDiagnostic(fontsDir);

// ─────────────────────────────────────────────────────────────────────────
// 矩形+テキスト 最小再現テスト
// ─────────────────────────────────────────────────────────────────────────

Console.WriteLine("\n═══ 矩形+テキスト 最小再現テスト ═══");
RunRectTextIsolationTest(inputBytes, fontsDir, outputDir);

// ─────────────────────────────────────────────────────────────────────────
// 総合テスト: 全フェーズを1つのPDFに組み合わせ
// ─────────────────────────────────────────────────────────────────────────

Console.WriteLine("\n═══ 総合テスト: 全フェーズ組み合わせ ═══");
RunComprehensiveTest(inputBytes, testdataDir, fontsDir, outputDir);

// ─────────────────────────────────────────────────────────────────────────
// フォント描画単体テスト（空白PDF ベース）
// ─────────────────────────────────────────────────────────────────────────

Console.WriteLine("\n═══ フォント描画単体テスト（空白PDF）═══");
RunFontTest(fontsDir, outputDir);

Console.WriteLine("\n完了。");

// ─────────────────────────────────────────────────────────────────────────
// Phase 2 実装
// ─────────────────────────────────────────────────────────────────────────

static void RunPhase2(byte[] inputBytes, string outputDir)
{
    var commands = new List<PdfDrawCommand>
    {
        new RectangleDrawCommand { Name="page-border",  PageNumber=1, X=20,  Y=20,  Width=555, Height=802, BorderColor=PdfColor.Black, BorderWidth=0.5, IsFilled=false, ZIndex=0 },
        new RectangleDrawCommand { Name="header-box",   PageNumber=1, X=30,  Y=30,  Width=535, Height=60,  BorderColor=new PdfColor(0,100,180), BorderWidth=1.5, FillColor=new PdfColor(173,216,230), IsFilled=true, ZIndex=1 },
        new LineDrawCommand      { Name="separator",    PageNumber=1, X=30,  Y=110, X2=565, Y2=110, LineColor=PdfColor.Black, LineWidth=1.0, ZIndex=2 },
        new RectangleDrawCommand { Name="left-col",     PageNumber=1, X=30,  Y=120, Width=255, Height=200, BorderColor=PdfColor.Gray, BorderWidth=0.75, IsFilled=false, ZIndex=3 },
        new RectangleDrawCommand { Name="right-col",    PageNumber=1, X=310, Y=120, Width=255, Height=200, BorderColor=PdfColor.Gray, BorderWidth=0.75, IsFilled=false, ZIndex=3 },
        new LineDrawCommand      { Name="dashed",       PageNumber=1, X=30,  Y=340, X2=565, Y2=340, LineColor=PdfColor.Gray, LineWidth=0.75, DashPattern=new double[]{5,3}, ZIndex=4 },
        new RectangleDrawCommand { Name="highlight",    PageNumber=1, X=30,  Y=360, Width=535, Height=80,  BorderColor=PdfColor.Red, BorderWidth=2.5, FillColor=new PdfColor(255,220,220), IsFilled=true, ZIndex=5 },
        new LineDrawCommand      { Name="diag1",        PageNumber=1, X=30,  Y=470, X2=180, Y2=560, LineColor=PdfColor.Blue, LineWidth=1.5, ZIndex=6 },
        new LineDrawCommand      { Name="diag2",        PageNumber=1, X=30,  Y=560, X2=180, Y2=470, LineColor=PdfColor.Blue, LineWidth=1.5, ZIndex=6 },
        new LineDrawCommand      { Name="footer-1",     PageNumber=1, X=30,  Y=790, X2=565, Y2=790, LineColor=PdfColor.Black, LineWidth=1.0, ZIndex=8 },
        new LineDrawCommand      { Name="footer-2",     PageNumber=1, X=30,  Y=795, X2=565, Y2=795, LineColor=PdfColor.Black, LineWidth=0.5, ZIndex=8 },
    };

    try
    {
        var writer = new PdfIncrementalWriter(inputBytes);
        writer.ApplyCommands(commands);
        byte[] result = writer.BuildIncrementalUpdate();
        string outPath = Path.Combine(outputDir, "phase2_shapes.pdf");
        File.WriteAllBytes(outPath, result);
        Console.WriteLine($"出力: {outPath} (+{result.Length - inputBytes.Length:N0} bytes)");
        Console.WriteLine("Phase 2 PASS ✓");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Phase 2 FAIL: [{ex.GetType().Name}] {ex.Message}");
    }
}

// ─────────────────────────────────────────────────────────────────────────
// Phase 3 実装
// ─────────────────────────────────────────────────────────────────────────

static void RunPhase3(byte[] inputBytes, string testdataDir, string outputDir)
{
    // ── テスト用画像の準備 ────────────────────────────────────────────

    // PNG 1: 200x100 青い矩形（会社ロゴ代わり）
    string png1Path = Path.Combine(testdataDir, "sample_blue.png");
    if (!File.Exists(png1Path))
    {
        File.WriteAllBytes(png1Path, CreateSolidColorPng(200, 100, 50, 100, 200));
        Console.WriteLine($"サンプル画像作成: {png1Path}");
    }

    // PNG 2: 80x80 赤い矩形（スタンプ代わり）
    string png2Path = Path.Combine(testdataDir, "sample_red.png");
    if (!File.Exists(png2Path))
    {
        File.WriteAllBytes(png2Path, CreateSolidColorPng(80, 80, 200, 50, 50));
        Console.WriteLine($"サンプル画像作成: {png2Path}");
    }

    // PNG 3: 300x50 グラデーション風画像（ヘッダーバー代わり）
    string png3Path = Path.Combine(testdataDir, "sample_gradient.png");
    if (!File.Exists(png3Path))
    {
        File.WriteAllBytes(png3Path, CreateGradientPng(300, 50));
        Console.WriteLine($"サンプル画像作成: {png3Path}");
    }

    byte[] imgBlue     = File.ReadAllBytes(png1Path);
    byte[] imgRed      = File.ReadAllBytes(png2Path);
    byte[] imgGradient = File.ReadAllBytes(png3Path);

    Console.WriteLine($"画像読み込み: blue={imgBlue.Length:N0}B, red={imgRed.Length:N0}B, gradient={imgGradient.Length:N0}B");

    // ── 描画コマンド ────────────────────────────────────────────────────

    var commands = new List<PdfDrawCommand>
    {
        // ① ヘッダーグラデーション画像（幅535pt × 高さ50pt、左上30,30）
        new ImageDrawCommand
        {
            Name = "header-gradient",
            PageNumber = 1,
            X = 30, Y = 30,
            Width = 535, Height = 50,
            ImageBytes = imgGradient,
            ZIndex = 1
        },

        // ② 青い画像（左カラム：150x80pt、位置 30,100）
        new ImageDrawCommand
        {
            Name = "blue-logo",
            PageNumber = 1,
            X = 30, Y = 100,
            Width = 150, Height = 80,
            ImageBytes = imgBlue,
            ZIndex = 2
        },

        // ③ 赤いスタンプ画像（右下：80x80pt、位置 450,700）
        new ImageDrawCommand
        {
            Name = "red-stamp",
            PageNumber = 1,
            X = 450, Y = 700,
            Width = 80, Height = 80,
            ImageBytes = imgRed,
            ZIndex = 3
        },

        // 画像の周囲に枠線（矩形）を重ねて確認
        new RectangleDrawCommand
        {
            Name = "blue-border",
            PageNumber = 1,
            X = 30, Y = 100,
            Width = 150, Height = 80,
            BorderColor = new PdfColor(0, 80, 160),
            BorderWidth = 1.0,
            IsFilled = false,
            ZIndex = 4
        },
        new RectangleDrawCommand
        {
            Name = "stamp-border",
            PageNumber = 1,
            X = 450, Y = 700,
            Width = 80, Height = 80,
            BorderColor = PdfColor.Red,
            BorderWidth = 1.5,
            IsFilled = false,
            ZIndex = 4
        },
    };

    // ── 実行 ────────────────────────────────────────────────────────────

    try
    {
        var writer = new PdfIncrementalWriter(inputBytes);
        var info = writer.GetPageInfo(1);
        Console.WriteLine($"解析: {writer.PageCount}ページ, Page1={info.Width:F1}x{info.Height:F1}pt");

        writer.ApplyCommands(commands);
        byte[] result = writer.BuildIncrementalUpdate();

        string outPath = Path.Combine(outputDir, "phase3_test.pdf");
        File.WriteAllBytes(outPath, result);

        Console.WriteLine($"出力: {outPath}");
        Console.WriteLine($"  元サイズ:  {inputBytes.Length:N0} bytes");
        Console.WriteLine($"  出力サイズ: {result.Length:N0} bytes (+{result.Length - inputBytes.Length:N0} bytes)");

        // 追記部分に Image XObject が含まれることを確認
        string incremental = System.Text.Encoding.Latin1.GetString(
            result, inputBytes.Length, result.Length - inputBytes.Length);
        bool hasXObject = incremental.Contains("/Subtype /Image");
        bool hasXObjectResource = incremental.Contains("/XObject");
        Console.WriteLine($"  /Subtype /Image 含む: {(hasXObject ? "✓" : "✗")}");
        Console.WriteLine($"  /XObject リソース含む: {(hasXObjectResource ? "✓" : "✗")}");

        // 再解析して構造確認
        var verify = new PdfIncrementalWriter(result);
        Console.WriteLine($"  再解析ページ数: {verify.PageCount} → {(verify.PageCount == writer.PageCount ? "一致 ✓" : "不一致 ✗")}");

        Console.WriteLine("Phase 3 PASS ✓");

        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(outPath) { UseShellExecute = true }); }
        catch { }
    }
    catch (PdfRenderException ex)
    {
        Console.WriteLine($"Phase 3 FAIL [PdfRenderException]: {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Phase 3 FAIL [{ex.GetType().Name}]: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }
}

// ─────────────────────────────────────────────────────────────────────────
// Phase 4 実装
// ─────────────────────────────────────────────────────────────────────────

static void RunPhase4(string fontsDir)
{
    bool allPass = true;

    // ── [1] TTC 読み込みテスト ─────────────────────────────────────────
    Console.WriteLine("\n[1] TTCファイル読み込み・展開テスト");
    string ttcPath = Path.Combine(fontsDir, "msmincho.ttc");
    byte[]? ttfBytes = null;

    if (!File.Exists(ttcPath))
    {
        Console.WriteLine($"  スキップ: ファイルが見つかりません ({ttcPath})");
        allPass = false;
    }
    else
    {
        try
        {
            ttfBytes = FontHelper.LoadFontBytes(ttcPath, 0); // index 0 = MS明朝
            Console.WriteLine($"  msmincho.ttc → TTF展開サイズ: {ttfBytes.Length:N0} bytes ✓");

            // index 1 (MS P明朝) も確認
            byte[] ttfP = FontHelper.LoadFontBytes(ttcPath, 1);
            Console.WriteLine($"  msmincho.ttc [1] → TTF展開サイズ: {ttfP.Length:N0} bytes ✓");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAIL [{ex.GetType().Name}]: {ex.Message}");
            allPass = false;
        }
    }

    // ── [2] TTF ファイル読み込みテスト ───────────────────────────────
    Console.WriteLine("\n[2] TTFファイル読み込みテスト");
    string? ttfPath = FindFirstFile(fontsDir, "*.ttf");
    if (ttfPath == null)
    {
        // Windows 標準フォントにフォールバック
        ttfPath = FindFirstFile(@"C:\Windows\Fonts", "arial.ttf")
               ?? FindFirstFile(@"C:\Windows\Fonts", "times.ttf")
               ?? FindFirstFile(@"C:\Windows\Fonts", "cour.ttf");
    }

    if (ttfPath == null)
    {
        Console.WriteLine("  スキップ: TTFファイルが見つかりません");
    }
    else
    {
        try
        {
            byte[] ttfData = FontHelper.LoadFontBytes(ttfPath);
            int upm = FontHelper.GetUnitsPerEm(ttfData);
            Console.WriteLine($"  {Path.GetFileName(ttfPath)}: {ttfData.Length:N0} bytes, unitsPerEm={upm} ✓");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAIL [{ex.GetType().Name}]: {ex.Message}");
            allPass = false;
        }
    }

    // ── [3] グリフ ID・文字幅取得テスト ──────────────────────────────
    Console.WriteLine("\n[3] グリフID・文字幅取得テスト (MS明朝 index 0)");
    if (ttfBytes == null)
    {
        Console.WriteLine("  スキップ: TTFバイト列が取得できていません");
        allPass = false;
    }
    else
    {
        try
        {
            int unitsPerEm = FontHelper.GetUnitsPerEm(ttfBytes);
            Console.WriteLine($"  unitsPerEm: {unitsPerEm}");
            Console.WriteLine($"  {"文字",-6} {"U+",-8} {"GlyphID",8} {"AdvWidth",10} {"幅(1/1000em)",14}");
            Console.WriteLine($"  {new string('-', 52)}");

            var testChars = new (int cp, string label)[]
            {
                (0x0041, "A"),
                (0x0061, "a"),
                (0x3042, "あ"),
                (0x30A2, "ア"),
                (0x65E5, "日"),
                (0x672C, "本"),
                (0x8A9E, "語"),
                (0x0020, "Space"),
            };

            bool glyphOk = true;
            foreach (var (cp, label) in testChars)
            {
                int glyphId  = FontHelper.GetGlyphId(ttfBytes, cp);
                int advWidth = FontHelper.GetAdvanceWidth(ttfBytes, glyphId);
                double widthEm = unitsPerEm > 0 ? advWidth * 1000.0 / unitsPerEm : 0;
                string mark = glyphId > 0 ? "✓" : "?";
                Console.WriteLine($"  {label,-6} U+{cp:X4}   {glyphId,8} {advWidth,10} {widthEm,14:F1} {mark}");
                if (glyphId == 0 && cp != 0x0020) glyphOk = false;
            }

            if (glyphOk)
                Console.WriteLine("  グリフID取得 ✓");
            else
            {
                Console.WriteLine("  一部グリフが未対応（glyphId=0）");
                allPass = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAIL [{ex.GetType().Name}]: {ex.Message}");
            Console.WriteLine($"  {ex.StackTrace}");
            allPass = false;
        }
    }

    Console.WriteLine(allPass ? "\nPhase 4 PASS ✓" : "\nPhase 4 一部失敗");
}

// ─────────────────────────────────────────────────────────────────────────
// ヘルパー: ディレクトリ検索
// ─────────────────────────────────────────────────────────────────────────

// ─────────────────────────────────────────────────────────────────────────
// Phase 5 実装
// ─────────────────────────────────────────────────────────────────────────

static void RunPhase5(byte[] inputBytes, string fontsDir, string outputDir)
{
    string ttcMincho  = Path.Combine(fontsDir, "msmincho.ttc");
    string ttcGothic  = Path.Combine(fontsDir, "msgothic.ttc");

    if (!File.Exists(ttcMincho))
    {
        Console.WriteLine($"  スキップ: {ttcMincho} が見つかりません");
        return;
    }

    try
    {
        var commands = new List<PdfDrawCommand>();

        // ── ヘッダ背景 ─────────────────────────────────────────────────────
        commands.Add(new RectangleDrawCommand
        {
            Name="hdr-bg", PageNumber=1, X=30, Y=25, Width=535, Height=30,
            FillColor=new PdfColor(0,70,130), IsFilled=true, BorderWidth=0, ZIndex=0
        });

        // ── [1] MS明朝 日本語テキスト ──────────────────────────────────────
        commands.Add(new TextDrawCommand
        {
            Name="title", PageNumber=1,
            X=35, Y=42,
            Text="業務帳票システム　テキスト出力テスト",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=13, FontColor=PdfColor.White, ZIndex=1
        });

        // ── [2] MSゴシック 日本語テキスト ─────────────────────────────────
        commands.Add(new RectangleDrawCommand
        {
            Name="gothic-bg", PageNumber=1, X=30, Y=65, Width=535, Height=22,
            FillColor=new PdfColor(240,240,255), IsFilled=true, BorderColor=PdfColor.LightGray, BorderWidth=0.5, ZIndex=0
        });
        commands.Add(new TextDrawCommand
        {
            Name="subtitle", PageNumber=1,
            X=35, Y=80,
            Text="MSゴシック：株式会社サンプル　東京都千代田区　2024年04月23日",
            FontFilePath=File.Exists(ttcGothic) ? ttcGothic : ttcMincho,
            TtcFontIndex=0,
            FontSize=9, FontColor=new PdfColor(40,40,80), ZIndex=1
        });

        // ── [3] フォントサイズ変更テスト（8 / 10 / 12 pt）────────────────
        double sectionY = 105;
        commands.Add(new LineDrawCommand
        {
            Name="sep1", PageNumber=1, X=30, Y=sectionY, X2=565, Y2=sectionY,
            LineColor=new PdfColor(0,70,130), LineWidth=0.75, ZIndex=0
        });
        commands.Add(new TextDrawCommand
        {
            Name="size-label", PageNumber=1, X=30, Y=sectionY+12,
            Text="■ フォントサイズテスト（MS明朝）",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=9, FontColor=new PdfColor(0,70,130), ZIndex=1
        });
        double szY = sectionY + 30;
        foreach (var (sz, label) in new[]{(8.0,"8pt：消費税額"), (10.0,"10pt：合計金額"), (12.0,"12pt：請求書")})
        {
            commands.Add(new TextDrawCommand
            {
                Name=$"sz-{sz}", PageNumber=1, X=35, Y=szY,
                Text=$"{label}　日本語テキスト サンプル　１２３",
                FontFilePath=ttcMincho, TtcFontIndex=0,
                FontSize=sz, FontColor=PdfColor.Black, ZIndex=1
            });
            szY += sz * 2.2;
        }

        // ── [4] 水平配置テスト（左・中央・右）────────────────────────────
        double alignBaseY = szY + 12;
        commands.Add(new LineDrawCommand
        {
            Name="sep2", PageNumber=1, X=30, Y=alignBaseY, X2=565, Y2=alignBaseY,
            LineColor=new PdfColor(0,70,130), LineWidth=0.75, ZIndex=0
        });
        commands.Add(new TextDrawCommand
        {
            Name="align-label", PageNumber=1, X=30, Y=alignBaseY+12,
            Text="■ 水平配置テスト（ボックス幅 535pt）",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=9, FontColor=new PdfColor(0,70,130), ZIndex=1
        });

        double aY = alignBaseY + 30;
        foreach (var (align, label, color) in new[]{
            (PdfHorizontalAlign.Left,   "左寄せ：伝票番号 A-00123",  new PdfColor(180,0,0)),
            (PdfHorizontalAlign.Center, "中央：株式会社サンプル",    new PdfColor(0,120,0)),
            (PdfHorizontalAlign.Right,  "右寄せ：金額 ¥123,456",   new PdfColor(0,0,160)),
        })
        {
            // 枠線
            commands.Add(new RectangleDrawCommand
            {
                Name=$"abox-{align}", PageNumber=1,
                X=30, Y=aY-10, Width=535, Height=16,
                BorderColor=PdfColor.LightGray, BorderWidth=0.5, IsFilled=false, ZIndex=0
            });
            commands.Add(new TextDrawCommand
            {
                Name=$"align-{align}", PageNumber=1,
                X=30, Y=aY,
                Width=535, HorizontalAlign=align,
                Text=label,
                FontFilePath=ttcMincho, TtcFontIndex=0,
                FontSize=10, FontColor=color, ZIndex=1
            });
            aY += 22;
        }

        // ── [5] 総合テスト：図形 + 画像 + テキスト ───────────────────────
        double tableY = aY + 15;
        commands.Add(new LineDrawCommand
        {
            Name="sep3", PageNumber=1, X=30, Y=tableY, X2=565, Y2=tableY,
            LineColor=new PdfColor(0,70,130), LineWidth=0.75, ZIndex=0
        });
        commands.Add(new TextDrawCommand
        {
            Name="table-label", PageNumber=1, X=30, Y=tableY+12,
            Text="■ 総合テスト（テキスト + 図形）",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=9, FontColor=new PdfColor(0,70,130), ZIndex=1
        });

        // 表ヘッダ
        double tblTop = tableY + 26;
        string[] cols  = {"品番", "品名", "数量", "単価", "金額"};
        double[] colX  = {30, 85, 280, 360, 450};
        double[] colW  = {55, 195, 80, 90, 115};

        // ヘッダ行背景
        commands.Add(new RectangleDrawCommand
        {
            Name="th-bg", PageNumber=1, X=30, Y=tblTop, Width=535, Height=16,
            FillColor=new PdfColor(0,70,130), IsFilled=true, BorderWidth=0, ZIndex=0
        });

        for (int ci = 0; ci < cols.Length; ci++)
        {
            commands.Add(new TextDrawCommand
            {
                Name=$"th-{ci}", PageNumber=1,
                X=colX[ci], Y=tblTop+12, Width=colW[ci],
                HorizontalAlign=PdfHorizontalAlign.Center,
                Text=cols[ci],
                FontFilePath=File.Exists(ttcGothic) ? ttcGothic : ttcMincho,
                TtcFontIndex=0,
                FontSize=9, FontColor=PdfColor.White, ZIndex=1
            });
        }

        // データ行
        var rows = new[]
        {
            new[]{"A-001", "コピー用紙 A4 500枚", "10", "¥500", "¥5,000"},
            new[]{"B-002", "ボールペン（黒）12本入", "5",  "¥250", "¥1,250"},
            new[]{"C-003", "クリアファイル 10枚",   "20", "¥150", "¥3,000"},
            new[]{"D-004", "蛍光マーカー 5色セット","3",  "¥400", "¥1,200"},
        };
        double rowH = 15;
        double rowY = tblTop + 16;
        for (int ri = 0; ri < rows.Length; ri++)
        {
            if (ri % 2 == 1)
                commands.Add(new RectangleDrawCommand
                {
                    Name=$"tr-bg-{ri}", PageNumber=1, X=30, Y=rowY, Width=535, Height=rowH,
                    FillColor=new PdfColor(235,242,255), IsFilled=true, BorderWidth=0, ZIndex=0
                });
            for (int ci = 0; ci < rows[ri].Length; ci++)
            {
                var ha = (ci >= 2) ? PdfHorizontalAlign.Right : PdfHorizontalAlign.Left;
                commands.Add(new TextDrawCommand
                {
                    Name=$"td-{ri}-{ci}", PageNumber=1,
                    X=colX[ci], Y=rowY+11, Width=colW[ci],
                    HorizontalAlign=ha,
                    Text=rows[ri][ci],
                    FontFilePath=ttcMincho, TtcFontIndex=0,
                    FontSize=8.5, FontColor=PdfColor.Black, ZIndex=1
                });
            }
            // 行罫線
            commands.Add(new LineDrawCommand
            {
                Name=$"tr-line-{ri}", PageNumber=1,
                X=30, Y=rowY+rowH, X2=565, Y2=rowY+rowH,
                LineColor=PdfColor.LightGray, LineWidth=0.3, ZIndex=0
            });
            rowY += rowH;
        }
        // 表外枠
        commands.Add(new RectangleDrawCommand
        {
            Name="tbl-border", PageNumber=1, X=30, Y=tblTop, Width=535, Height=(rowY-tblTop),
            BorderColor=new PdfColor(0,70,130), BorderWidth=0.75, IsFilled=false, ZIndex=2
        });
        // 合計行
        rowY += 3;
        commands.Add(new TextDrawCommand
        {
            Name="total-label", PageNumber=1,
            X=30, Y=rowY+11, Width=420, HorizontalAlign=PdfHorizontalAlign.Right,
            Text="小計",
            FontFilePath=File.Exists(ttcGothic) ? ttcGothic : ttcMincho, TtcFontIndex=0,
            FontSize=9, FontColor=PdfColor.Black, ZIndex=1
        });
        commands.Add(new TextDrawCommand
        {
            Name="total-value", PageNumber=1,
            X=450, Y=rowY+11, Width=115, HorizontalAlign=PdfHorizontalAlign.Right,
            Text="¥10,450",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=9, FontColor=new PdfColor(180,0,0), ZIndex=1
        });

        // ── 実行 ──────────────────────────────────────────────────────────
        var writer = new PdfIncrementalWriter(inputBytes);
        var info = writer.GetPageInfo(1);
        Console.WriteLine($"  入力: {writer.PageCount}p, Page1={info.Width:F0}x{info.Height:F0}pt");

        writer.ApplyCommands(commands);
        byte[] result = writer.BuildIncrementalUpdate();

        string outPath = Path.Combine(outputDir, "phase5_test.pdf");
        File.WriteAllBytes(outPath, result);

        // ── [1] fsType チェック ─────────────────────────────────────────────
        byte[] minchoBytes = FontHelper.LoadFontBytes(ttcMincho, 0);
        ushort minchoFsType = FontHelper.GetFsType(minchoBytes);
        bool isRestricted  = (minchoFsType & 0x0002) != 0;
        bool expectedEmbed = !isRestricted;
        string fsTypeDesc  = minchoFsType switch
        {
            0x0000 => "Installable (埋め込み可)",
            0x0002 => "Restricted  (埋め込み禁止)",
            0x0004 => "Preview & Print (表示・印刷のみ可)",
            0x0008 => "Editable (埋め込み可)",
            _      => $"その他 (bit1={((minchoFsType & 0x0002) != 0 ? "1=禁止" : "0=許可")})",
        };
        Console.WriteLine($"\n  [1] fsType チェック");
        Console.WriteLine($"  MS明朝 fsType:          0x{minchoFsType:X4} ({fsTypeDesc})");
        Console.WriteLine($"  EmbedFont 期待値:       {expectedEmbed}  ← (fsType bit1=0 なら埋め込み許可)");

        // ── [2] PDF 構造チェック ─────────────────────────────────────────
        string inc = System.Text.Encoding.GetEncoding(28591)
            .GetString(result, inputBytes.Length, result.Length - inputBytes.Length);
        bool hasType0    = inc.Contains("/Subtype /Type0");
        bool hasCIDFont  = inc.Contains("/Subtype /CIDFontType2");
        bool hasFont2    = inc.Contains("/FontFile2");
        bool hasCmap     = inc.Contains("begincmap");
        bool hasIdentity = inc.Contains("/Identity-H");
        // PDFの /FontFile2 有無が EmbedFont 期待値と一致しているか
        bool fontEmbedOk = hasFont2 == expectedEmbed;

        Console.WriteLine($"\n  [2] PDF 構造チェック");
        Console.WriteLine($"  出力: {outPath}");
        Console.WriteLine($"  サイズ: {inputBytes.Length:N0} → {result.Length:N0} bytes (+{result.Length-inputBytes.Length:N0})");
        Console.WriteLine($"  /Subtype /Type0:        {(hasType0    ? "✓" : "✗")}");
        Console.WriteLine($"  /CIDFontType2:          {(hasCIDFont  ? "✓" : "✗")}");
        Console.WriteLine($"  /FontFile2 {(expectedEmbed ? "あり" : "なし")} (fsType準拠): {(fontEmbedOk ? "✓" : "✗")} (実際: {(hasFont2 ? "あり" : "なし")})");
        Console.WriteLine($"  ToUnicode CMap:         {(hasCmap     ? "✓" : "✗")}");
        Console.WriteLine($"  /Identity-H:            {(hasIdentity ? "✓" : "✗")}");

        // ── [3] fsType=0x0002 (Restricted) シミュレーション ─────────────
        // フォントバイト列の OS/2.fsType を 0x0002 に書き換えて埋め込みスキップを検証
        Console.WriteLine($"\n  [3] fsType=0x0002 (Restricted) シミュレーション");
        byte[] restrictedBytes = PatchFsType(minchoBytes, 0x0002);
        ushort simFsType = FontHelper.GetFsType(restrictedBytes);
        Console.WriteLine($"  パッチ後 fsType:        0x{simFsType:X4} {(simFsType == 0x0002 ? "✓" : "✗")}");
        bool simEmbedOk = false;
        string tempFont = Path.Combine(Path.GetTempPath(), "sim_restricted.ttf");
        try
        {
            File.WriteAllBytes(tempFont, restrictedBytes);
            var simCmds = new List<PdfDrawCommand>
            {
                new TextDrawCommand
                {
                    Name="sim-text", PageNumber=1, X=35, Y=600,
                    Text="Restricted フォントテスト",
                    FontFilePath=tempFont, TtcFontIndex=0,
                    FontSize=10, FontColor=PdfColor.Black, ZIndex=1
                }
            };
            var simWriter2 = new PdfIncrementalWriter(inputBytes);
            simWriter2.ApplyCommands(simCmds);
            byte[] simResult = simWriter2.BuildIncrementalUpdate();
            string simInc = System.Text.Encoding.GetEncoding(28591)
                .GetString(simResult, inputBytes.Length, simResult.Length - inputBytes.Length);
            bool simHasFont2 = simInc.Contains("/FontFile2");
            simEmbedOk = !simHasFont2;
            Console.WriteLine($"  /FontFile2 なし:        {(simEmbedOk ? "✓ (埋め込みスキップ = 正しい)" : "✗ (まだ埋め込まれている)")}");
        }
        finally { try { File.Delete(tempFont); } catch { } }

        var verify = new PdfIncrementalWriter(result);
        Console.WriteLine($"  再解析ページ数: {verify.PageCount} → {(verify.PageCount == writer.PageCount ? "一致 ✓" : "不一致 ✗")}");

        bool allOk = hasType0 && hasCIDFont && fontEmbedOk && hasCmap && hasIdentity
                  && simFsType == 0x0002 && simEmbedOk
                  && verify.PageCount == writer.PageCount;
        Console.WriteLine(allOk ? "\nPhase 5 PASS ✓" : "\nPhase 5 FAIL ✗");

        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(outPath) { UseShellExecute = true }); }
        catch { }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Phase 5 FAIL [{ex.GetType().Name}]: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }
}

// OS/2 テーブルの fsType フィールドを指定値で書き換えたコピーを返す（シミュレーション用）
static byte[] PatchFsType(byte[] ttfData, ushort newFsType)
{
    var patched = (byte[])ttfData.Clone();
    int numTables = (patched[4] << 8) | patched[5];
    for (int i = 0; i < numTables; i++)
    {
        int e = 12 + i * 16;
        if (e + 16 > patched.Length) break;
        if (System.Text.Encoding.ASCII.GetString(patched, e, 4) != "OS/2") continue;
        int os2Off = (int)(((uint)patched[e+8] << 24) | ((uint)patched[e+9] << 16)
                         | ((uint)patched[e+10] << 8) | patched[e+11]);
        if (os2Off + 10 <= patched.Length)
        {
            patched[os2Off + 8] = (byte)(newFsType >> 8);
            patched[os2Off + 9] = (byte)(newFsType & 0xFF);
        }
        return patched;
    }
    return patched;
}

static string? FindFirstFile(string dir, string pattern)
{
    if (!Directory.Exists(dir)) return null;
    string[] files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly);
    return files.Length > 0 ? files[0] : null;
}

static string? FindDir(string name)
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        string candidate = Path.Combine(dir.FullName, name);
        if (Directory.Exists(candidate)) return candidate;
        dir = dir.Parent;
    }
    return null;
}

// ─────────────────────────────────────────────────────────────────────────
// ヘルパー: ミニマルA4 PDF 生成
// ─────────────────────────────────────────────────────────────────────────

static byte[] CreateMinimalA4Pdf()
{
    using var ms = new MemoryStream();
    Action<string> w = s => { var b = System.Text.Encoding.ASCII.GetBytes(s); ms.Write(b, 0, b.Length); };

    w("%PDF-1.4\n%\xff\xff\xff\xff\n");
    var offsets = new long[4];

    offsets[1] = ms.Position;
    w("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

    offsets[2] = ms.Position;
    w("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

    offsets[3] = ms.Position;
    w("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] >>\nendobj\n");

    long xrefOffset = ms.Position;
    w("xref\n0 4\n");
    w("0000000000 65535 f \r\n");
    for (int i = 1; i <= 3; i++) w($"{offsets[i]:D10} 00000 n \r\n");
    w("trailer\n<< /Size 4 /Root 1 0 R >>\n");
    w($"startxref\n{xrefOffset}\n%%EOF\n");
    return ms.ToArray();
}

// ─────────────────────────────────────────────────────────────────────────
// ヘルパー: 単色 PNG 生成
// ─────────────────────────────────────────────────────────────────────────

static byte[] CreateSolidColorPng(int width, int height, byte r, byte g, byte b)
{
    // フィルターなし(0) + RGB ピクセル行
    int stride = width * 3;
    byte[] rawData = new byte[(1 + stride) * height];
    int pos = 0;
    for (int row = 0; row < height; row++)
    {
        rawData[pos++] = 0; // フィルターなし
        for (int col = 0; col < width; col++)
        {
            rawData[pos++] = r;
            rawData[pos++] = g;
            rawData[pos++] = b;
        }
    }
    return BuildPng(width, height, 8, 2, rawData); // colorType=2: RGB
}

static byte[] CreateGradientPng(int width, int height)
{
    int stride = width * 3;
    byte[] rawData = new byte[(1 + stride) * height];
    int pos = 0;
    for (int row = 0; row < height; row++)
    {
        rawData[pos++] = 0;
        float t = (float)row / height;
        for (int col = 0; col < width; col++)
        {
            float s = (float)col / width;
            rawData[pos++] = (byte)(30  + s * 180);  // R
            rawData[pos++] = (byte)(100 + t * 100);  // G
            rawData[pos++] = (byte)(200 - s * 100);  // B
        }
    }
    return BuildPng(width, height, 8, 2, rawData);
}

static byte[] BuildPng(int width, int height, int bitDepth, int colorType, byte[] rawPixelData)
{
    byte[] idatData = CompressZlibPng(rawPixelData);

    using var ms = new MemoryStream();
    // PNG シグネチャ
    ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);

    // IHDR
    WritePngChunk(ms, "IHDR", new byte[]
    {
        (byte)(width  >> 24), (byte)(width  >> 16), (byte)(width  >> 8), (byte)width,
        (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height,
        (byte)bitDepth, (byte)colorType,
        0, 0, 0 // compression, filter, interlace
    });

    // IDAT
    WritePngChunk(ms, "IDAT", idatData);

    // IEND
    WritePngChunk(ms, "IEND", Array.Empty<byte>());

    return ms.ToArray();
}

static void WritePngChunk(MemoryStream ms, string type, byte[] data)
{
    int len = data.Length;
    ms.WriteByte((byte)(len >> 24));
    ms.WriteByte((byte)(len >> 16));
    ms.WriteByte((byte)(len >> 8));
    ms.WriteByte((byte)len);

    byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
    ms.Write(typeBytes, 0, 4);
    if (data.Length > 0) ms.Write(data, 0, data.Length);

    // CRC32 of type + data
    byte[] crcInput = new byte[4 + data.Length];
    Array.Copy(typeBytes, crcInput, 4);
    Array.Copy(data, 0, crcInput, 4, data.Length);
    uint crc = ComputeCrc32(crcInput);
    ms.WriteByte((byte)(crc >> 24));
    ms.WriteByte((byte)(crc >> 16));
    ms.WriteByte((byte)(crc >> 8));
    ms.WriteByte((byte)crc);
}

static byte[] CompressZlibPng(byte[] data)
{
    using var ms = new MemoryStream();
    ms.WriteByte(0x78);
    ms.WriteByte(0x9C);
    using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        deflate.Write(data, 0, data.Length);
    uint adler = ComputeAdler32(data);
    ms.WriteByte((byte)(adler >> 24));
    ms.WriteByte((byte)(adler >> 16));
    ms.WriteByte((byte)(adler >> 8));
    ms.WriteByte((byte)adler);
    return ms.ToArray();
}

static uint ComputeAdler32(byte[] data)
{
    const uint MOD = 65521;
    uint a = 1, b = 0;
    foreach (byte bt in data) { a = (a + bt) % MOD; b = (b + a) % MOD; }
    return (b << 16) | a;
}

static uint ComputeCrc32(byte[] data)
{
    uint crc = 0xFFFFFFFF;
    foreach (byte bt in data)
    {
        crc ^= bt;
        for (int i = 0; i < 8; i++)
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
    }
    return crc ^ 0xFFFFFFFF;
}

// ─────────────────────────────────────────────────────────────────────────
// グリフID診断: cmap解析の検証
// ─────────────────────────────────────────────────────────────────────────

static void RunGlyphDiagnostic(string fontsDir)
{
    string ttcPath = Path.Combine(fontsDir, "msmincho.ttc");
    if (!File.Exists(ttcPath))
    {
        Console.WriteLine($"  スキップ: {ttcPath} が見つかりません");
        return;
    }

    byte[] ttfBytes = FontHelper.LoadFontBytes(ttcPath, 0);
    int upm = FontHelper.GetUnitsPerEm(ttfBytes);
    Console.WriteLine($"  フォント: MS明朝 (idx=0), unitsPerEm={upm}");

    // ユーザー指定の検証文字列
    var testChars = "最初はうまく功がいちに";
    Console.WriteLine($"\n  [1] 文字 → GlyphID マッピング");
    Console.WriteLine($"  {"文字",-4} {"U+",-8} {"GlyphID",8}  {"AdvWidth",10}  状態");
    Console.WriteLine($"  {new string('-', 48)}");
    bool anyZero = false;
    foreach (char c in testChars)
    {
        int gid = FontHelper.GetGlyphId(ttfBytes, (int)c);
        int adv = FontHelper.GetAdvanceWidth(ttfBytes, gid);
        string mark = gid > 0 ? "✓" : "✗ GID=0 (文字化け)";
        if (gid == 0) anyZero = true;
        Console.WriteLine($"  {c,-4} U+{(int)c:X4}   {gid,8}  {adv,10}  {mark}");
    }

    // 追加: 枠なしテキストとして使われる代表文字と比較
    Console.WriteLine($"\n  [2] 代表ASCII文字のGlyphID");
    foreach (char c in "ABCabc123")
    {
        int gid = FontHelper.GetGlyphId(ttfBytes, (int)c);
        int adv = FontHelper.GetAdvanceWidth(ttfBytes, gid);
        Console.WriteLine($"  {c,-4} U+{(int)c:X4}   {gid,8}  {adv,10}");
    }

    // ひらがな・カタカナ境界チェック
    Console.WriteLine($"\n  [3] ひらがな範囲 (U+3041-U+3046) 境界チェック");
    for (int cp = 0x3041; cp <= 0x3046; cp++)
    {
        int gid = FontHelper.GetGlyphId(ttfBytes, cp);
        int adv = FontHelper.GetAdvanceWidth(ttfBytes, gid);
        string ch = ((char)cp).ToString();
        string mark = gid > 0 ? "✓" : "✗";
        Console.WriteLine($"  {ch,-4} U+{cp:X4}   {gid,8}  {adv,10}  {mark}");
    }

    if (anyZero)
        Console.WriteLine("\n  警告: GID=0 の文字があります。cmapテーブルの解析に問題がある可能性があります。");
    else
        Console.WriteLine("\n  全文字のGlyphID取得 ✓");
}

// ─────────────────────────────────────────────────────────────────────────
// 矩形+テキスト 最小再現テスト（文字化けの切り分け用）
// ─────────────────────────────────────────────────────────────────────────

static void RunRectTextIsolationTest(byte[] inputBytes, string fontsDir, string outputDir)
{
    string ttcMincho = Path.Combine(fontsDir, "msmincho.ttc");
    if (!File.Exists(ttcMincho))
    {
        Console.WriteLine($"  スキップ: {ttcMincho} が見つかりません");
        return;
    }

    try
    {
        // ケース1: テキストのみ（基準）
        var cmdsTextOnly = new List<PdfDrawCommand>
        {
            new TextDrawCommand {
                Name="text-only", PageNumber=1, X=50, Y=100,
                Text="最初はうまく功がいちに",
                FontFilePath=ttcMincho, TtcFontIndex=0,
                FontSize=14, FontColor=PdfColor.Black, ZIndex=1
            }
        };

        var w1 = new PdfIncrementalWriter(inputBytes);
        w1.ApplyCommands(cmdsTextOnly);
        byte[] r1 = w1.BuildIncrementalUpdate();
        string p1 = Path.Combine(outputDir, "isolation_text_only.pdf");
        File.WriteAllBytes(p1, r1);
        Console.WriteLine($"  ケース1(テキストのみ): {p1}");

        // ケース2: 黒枠（RectangleDrawCommand）+ テキスト
        var cmdsRectText = new List<PdfDrawCommand>
        {
            new RectangleDrawCommand {
                Name="black-rect", PageNumber=1, X=40, Y=85, Width=280, Height=25,
                BorderColor=PdfColor.Black, BorderWidth=1.5, IsFilled=false, ZIndex=0
            },
            new TextDrawCommand {
                Name="text-in-rect", PageNumber=1, X=50, Y=100,
                Text="最初はうまく功がいちに",
                FontFilePath=ttcMincho, TtcFontIndex=0,
                FontSize=14, FontColor=PdfColor.Black, ZIndex=1
            }
        };

        var w2 = new PdfIncrementalWriter(inputBytes);
        w2.ApplyCommands(cmdsRectText);
        byte[] r2 = w2.BuildIncrementalUpdate();
        string p2 = Path.Combine(outputDir, "isolation_rect_text.pdf");
        File.WriteAllBytes(p2, r2);
        Console.WriteLine($"  ケース2(枠+テキスト):  {p2}");

        // ケース3: 塗り矩形 + テキスト（枠なしでテキスト上に重なる）
        var cmdsFilledRectText = new List<PdfDrawCommand>
        {
            new RectangleDrawCommand {
                Name="filled-rect", PageNumber=1, X=40, Y=85, Width=280, Height=25,
                FillColor=new PdfColor(220, 235, 255), IsFilled=true, BorderWidth=0, ZIndex=0
            },
            new TextDrawCommand {
                Name="text-on-fill", PageNumber=1, X=50, Y=100,
                Text="最初はうまく功がいちに",
                FontFilePath=ttcMincho, TtcFontIndex=0,
                FontSize=14, FontColor=PdfColor.Black, ZIndex=1
            }
        };

        var w3 = new PdfIncrementalWriter(inputBytes);
        w3.ApplyCommands(cmdsFilledRectText);
        byte[] r3 = w3.BuildIncrementalUpdate();
        string p3 = Path.Combine(outputDir, "isolation_filled_rect_text.pdf");
        File.WriteAllBytes(p3, r3);
        Console.WriteLine($"  ケース3(塗り+テキスト): {p3}");

        // コンテンツストリーム内容を表示して検証
        Console.WriteLine("\n  [コンテンツストリーム差分確認]");
        DumpIncrementalContent("ケース1", r1, inputBytes.Length);
        DumpIncrementalContent("ケース2", r2, inputBytes.Length);

        Console.WriteLine("\n矩形+テキスト 最小再現テスト 完了");
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(p2) { UseShellExecute = true }); }
        catch { }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  FAIL [{ex.GetType().Name}]: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }
}

static void DumpIncrementalContent(string label, byte[] pdf, int originalLength)
{
    if (pdf.Length <= originalLength) return;
    string inc = System.Text.Encoding.GetEncoding(28591)
        .GetString(pdf, originalLength, pdf.Length - originalLength);

    // コンテンツストリームだけ抽出して表示
    int streamStart = inc.IndexOf("stream\n", StringComparison.Ordinal);
    int streamEnd   = inc.IndexOf("\nendstream", StringComparison.Ordinal);
    if (streamStart >= 0 && streamEnd > streamStart)
    {
        string content = inc.Substring(streamStart + 7, Math.Min(streamEnd - streamStart - 7, 600));
        Console.WriteLine($"\n  --- {label} コンテンツストリーム先頭 ---");
        foreach (string line in content.Split('\n'))
            if (!string.IsNullOrWhiteSpace(line))
                Console.WriteLine($"  {line}");
    }

    // フォントリソース名を表示
    int fontStart = inc.IndexOf("/Font <<", StringComparison.Ordinal);
    if (fontStart >= 0)
    {
        int fontEnd = inc.IndexOf(">>", fontStart + 8) + 2;
        Console.WriteLine($"\n  --- {label} フォントリソース ---");
        Console.WriteLine($"  {inc.Substring(fontStart, fontEnd - fontStart).Trim()}");
    }
}

// ─────────────────────────────────────────────────────────────────────────
// 総合テスト: テキスト・矩形・線・画像・帳票レイアウトを1つのPDFに出力
// ─────────────────────────────────────────────────────────────────────────

static void RunComprehensiveTest(byte[] inputBytes, string testdataDir, string fontsDir, string outputDir)
{
    string ttcMincho = Path.Combine(fontsDir, "msmincho.ttc");
    string ttcGothic = Path.Combine(fontsDir, "msgothic.ttc");
    string fontGothic = File.Exists(ttcGothic) ? ttcGothic : ttcMincho;

    if (!File.Exists(ttcMincho))
    {
        Console.WriteLine($"  スキップ: {ttcMincho} が見つかりません");
        return;
    }

    // 画像の用意（存在しなければ生成）
    string pngBlue     = Path.Combine(testdataDir, "sample_blue.png");
    string pngRed      = Path.Combine(testdataDir, "sample_red.png");
    string pngGradient = Path.Combine(testdataDir, "sample_gradient.png");
    if (!File.Exists(pngBlue))
        File.WriteAllBytes(pngBlue, CreateSolidColorPng(200, 100, 50, 100, 200));
    if (!File.Exists(pngRed))
        File.WriteAllBytes(pngRed, CreateSolidColorPng(80, 80, 200, 50, 50));
    if (!File.Exists(pngGradient))
        File.WriteAllBytes(pngGradient, CreateGradientPng(300, 50));

    byte[] imgBlue     = File.ReadAllBytes(pngBlue);
    byte[] imgRed      = File.ReadAllBytes(pngRed);
    byte[] imgGradient = File.ReadAllBytes(pngGradient);

    try
    {
        var cmds = new List<PdfDrawCommand>();

        // ════════════════════════════════════════════════
        // Section 1: ヘッダー（グラデーション画像 + タイトルテキスト）
        // ════════════════════════════════════════════════
        // 背景矩形（濃い青）
        cmds.Add(new RectangleDrawCommand {
            Name="hdr-bg", PageNumber=1, X=20, Y=20, Width=555, Height=55,
            FillColor=new PdfColor(0,60,120), IsFilled=true, BorderWidth=0, ZIndex=0
        });
        // グラデーション画像をヘッダー全面に重ねる
        cmds.Add(new ImageDrawCommand {
            Name="hdr-img", PageNumber=1, X=20, Y=20, Width=555, Height=55,
            ImageBytes=imgGradient, ZIndex=1
        });
        // タイトル（MS明朝 16pt 白）
        cmds.Add(new TextDrawCommand {
            Name="title", PageNumber=1, X=20, Y=52, Width=555,
            HorizontalAlign=PdfHorizontalAlign.Center,
            Text="請求書　Invoice No. 2024-0042",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=16, FontColor=PdfColor.White, ZIndex=2
        });

        // ════════════════════════════════════════════════
        // Section 2: 会社情報（MSゴシック・MS明朝 混在）
        // ════════════════════════════════════════════════
        cmds.Add(new RectangleDrawCommand {
            Name="info-bg", PageNumber=1, X=20, Y=85, Width=555, Height=55,
            FillColor=new PdfColor(245,248,255), IsFilled=true,
            BorderColor=new PdfColor(200,210,230), BorderWidth=0.5, ZIndex=0
        });
        // 会社名（MSゴシック 10pt）
        cmds.Add(new TextDrawCommand {
            Name="company", PageNumber=1, X=30, Y=103,
            Text="株式会社サンプル商事　東京都千代田区丸の内1-1-1",
            FontFilePath=fontGothic, TtcFontIndex=0,
            FontSize=10, FontColor=new PdfColor(40,40,80), ZIndex=1
        });
        // 日付・担当（MS明朝 9pt）
        cmds.Add(new TextDrawCommand {
            Name="date", PageNumber=1, X=30, Y=122,
            Text="発行日：2024年04月27日　担当者：山田 太郎　Tel: 03-1234-5678",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=9, FontColor=new PdfColor(60,60,80), ZIndex=1
        });

        // 区切り線（濃い青・太線）
        cmds.Add(new LineDrawCommand {
            Name="sep1", PageNumber=1, X=20, Y=150, X2=575, Y2=150,
            LineColor=new PdfColor(0,60,120), LineWidth=1.5, ZIndex=0
        });

        // ════════════════════════════════════════════════
        // Section 3: 図形テスト（矩形3種 + 横線・縦線）
        // ════════════════════════════════════════════════
        cmds.Add(new TextDrawCommand {
            Name="shapes-label", PageNumber=1, X=20, Y=165,
            Text="■ 図形テスト（矩形：枠のみ / 塗りのみ / 枠＋塗り）",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=9, FontColor=new PdfColor(0,60,120), ZIndex=1
        });

        // [矩形1] 枠あり・塗りなし
        cmds.Add(new RectangleDrawCommand {
            Name="rect-border", PageNumber=1, X=20, Y=178, Width=165, Height=55,
            BorderColor=PdfColor.Black, BorderWidth=1.5, IsFilled=false, ZIndex=1
        });
        cmds.Add(new TextDrawCommand {
            Name="rect-border-lbl", PageNumber=1, X=20, Y=200, Width=165,
            HorizontalAlign=PdfHorizontalAlign.Center,
            Text="枠あり・塗りなし",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=8, FontColor=PdfColor.Black, ZIndex=2
        });

        // [矩形2] 塗りあり・枠なし（赤系）
        cmds.Add(new RectangleDrawCommand {
            Name="rect-fill", PageNumber=1, X=205, Y=178, Width=165, Height=55,
            FillColor=new PdfColor(255,180,180), IsFilled=true, BorderWidth=0, ZIndex=1
        });
        cmds.Add(new TextDrawCommand {
            Name="rect-fill-lbl", PageNumber=1, X=205, Y=200, Width=165,
            HorizontalAlign=PdfHorizontalAlign.Center,
            Text="塗りあり・枠なし",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=8, FontColor=PdfColor.Black, ZIndex=2
        });

        // [矩形3] 塗りあり＋枠あり（青系）
        cmds.Add(new RectangleDrawCommand {
            Name="rect-both", PageNumber=1, X=390, Y=178, Width=185, Height=55,
            FillColor=new PdfColor(180,220,255), IsFilled=true,
            BorderColor=new PdfColor(0,80,160), BorderWidth=2.0, ZIndex=1
        });
        cmds.Add(new TextDrawCommand {
            Name="rect-both-lbl", PageNumber=1, X=390, Y=200, Width=185,
            HorizontalAlign=PdfHorizontalAlign.Center,
            Text="枠あり・塗りあり",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=8, FontColor=new PdfColor(0,60,120), ZIndex=2
        });

        // 縦線（矩形間に挿入）
        cmds.Add(new LineDrawCommand {
            Name="vline1", PageNumber=1, X=192, Y=178, X2=192, Y2=233,
            LineColor=PdfColor.LightGray, LineWidth=0.5, ZIndex=0
        });
        cmds.Add(new LineDrawCommand {
            Name="vline2", PageNumber=1, X=377, Y=178, X2=377, Y2=233,
            LineColor=PdfColor.LightGray, LineWidth=0.5, ZIndex=0
        });

        // 線テスト: 横線（実線・破線）
        cmds.Add(new TextDrawCommand {
            Name="line-label", PageNumber=1, X=20, Y=248,
            Text="■ 線テスト（横線：実線・破線、縦線）",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=9, FontColor=new PdfColor(0,60,120), ZIndex=1
        });
        cmds.Add(new LineDrawCommand {
            Name="hline-solid", PageNumber=1, X=20, Y=262, X2=575, Y2=262,
            LineColor=PdfColor.Black, LineWidth=1.0, ZIndex=1
        });
        cmds.Add(new TextDrawCommand {
            Name="hline-solid-lbl", PageNumber=1, X=577, Y=265,
            Text="実線", FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=7, FontColor=PdfColor.Gray, ZIndex=1
        });
        cmds.Add(new LineDrawCommand {
            Name="hline-dash", PageNumber=1, X=20, Y=272, X2=575, Y2=272,
            LineColor=PdfColor.Gray, LineWidth=0.75,
            DashPattern=new double[]{5,3}, ZIndex=1
        });
        cmds.Add(new TextDrawCommand {
            Name="hline-dash-lbl", PageNumber=1, X=577, Y=275,
            Text="破線", FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=7, FontColor=PdfColor.Gray, ZIndex=1
        });

        // ════════════════════════════════════════════════
        // Section 4: 画像テスト（PNG 3枚）
        // ════════════════════════════════════════════════
        cmds.Add(new LineDrawCommand {
            Name="sep2", PageNumber=1, X=20, Y=290, X2=575, Y2=290,
            LineColor=new PdfColor(0,60,120), LineWidth=1.0, ZIndex=0
        });
        cmds.Add(new TextDrawCommand {
            Name="img-label", PageNumber=1, X=20, Y=305,
            Text="■ 画像テスト（PNG埋め込み：青・グラデーション・赤）",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=9, FontColor=new PdfColor(0,60,120), ZIndex=1
        });

        // 青い画像（左）
        cmds.Add(new ImageDrawCommand {
            Name="img-blue", PageNumber=1, X=20, Y=315, Width=160, Height=75,
            ImageBytes=imgBlue, ZIndex=1
        });
        cmds.Add(new RectangleDrawCommand {
            Name="img-blue-border", PageNumber=1, X=20, Y=315, Width=160, Height=75,
            BorderColor=new PdfColor(0,80,160), BorderWidth=1.0, IsFilled=false, ZIndex=2
        });
        cmds.Add(new TextDrawCommand {
            Name="img-blue-lbl", PageNumber=1, X=20, Y=397, Width=160,
            HorizontalAlign=PdfHorizontalAlign.Center,
            Text="ロゴ画像（青）", FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=7.5, FontColor=PdfColor.Gray, ZIndex=2
        });

        // グラデーション画像（中央）
        cmds.Add(new ImageDrawCommand {
            Name="img-grad", PageNumber=1, X=200, Y=315, Width=175, Height=75,
            ImageBytes=imgGradient, ZIndex=1
        });
        cmds.Add(new RectangleDrawCommand {
            Name="img-grad-border", PageNumber=1, X=200, Y=315, Width=175, Height=75,
            BorderColor=PdfColor.Gray, BorderWidth=0.75, IsFilled=false, ZIndex=2
        });
        cmds.Add(new TextDrawCommand {
            Name="img-grad-lbl", PageNumber=1, X=200, Y=397, Width=175,
            HorizontalAlign=PdfHorizontalAlign.Center,
            Text="グラデーション画像", FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=7.5, FontColor=PdfColor.Gray, ZIndex=2
        });

        // 赤い画像（右）
        cmds.Add(new ImageDrawCommand {
            Name="img-red", PageNumber=1, X=395, Y=315, Width=180, Height=75,
            ImageBytes=imgRed, ZIndex=1
        });
        cmds.Add(new RectangleDrawCommand {
            Name="img-red-border", PageNumber=1, X=395, Y=315, Width=180, Height=75,
            BorderColor=PdfColor.Red, BorderWidth=1.0, IsFilled=false, ZIndex=2
        });
        cmds.Add(new TextDrawCommand {
            Name="img-red-lbl", PageNumber=1, X=395, Y=397, Width=180,
            HorizontalAlign=PdfHorizontalAlign.Center,
            Text="スタンプ画像（赤）", FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=7.5, FontColor=PdfColor.Gray, ZIndex=2
        });

        // ════════════════════════════════════════════════
        // Section 5: 請求書明細テーブル（テキスト＋図形の複合）
        // ════════════════════════════════════════════════
        cmds.Add(new LineDrawCommand {
            Name="sep3", PageNumber=1, X=20, Y=415, X2=575, Y2=415,
            LineColor=new PdfColor(0,60,120), LineWidth=1.5, ZIndex=0
        });
        cmds.Add(new TextDrawCommand {
            Name="table-label", PageNumber=1, X=20, Y=430,
            Text="■ 請求書明細（テキスト＋図形の組み合わせ帳票レイアウト）",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=9, FontColor=new PdfColor(0,60,120), ZIndex=1
        });

        // テーブル列定義
        double tableTop = 443;
        double[] colX = {20,  80, 305, 375, 455};
        double[] colW = {60, 225,  70,  80, 120};
        string[] colNames = {"品番", "品名・仕様", "数量", "単価", "金額"};
        double headerH = 16;

        // ヘッダー背景（濃い青）
        cmds.Add(new RectangleDrawCommand {
            Name="th-bg", PageNumber=1, X=20, Y=tableTop, Width=555, Height=headerH,
            FillColor=new PdfColor(0,60,120), IsFilled=true, BorderWidth=0, ZIndex=1
        });
        // ヘッダーテキスト（MSゴシック 白）
        for (int ci = 0; ci < colNames.Length; ci++)
        {
            cmds.Add(new TextDrawCommand {
                Name=$"th-{ci}", PageNumber=1,
                X=colX[ci], Y=tableTop+12, Width=colW[ci],
                HorizontalAlign=PdfHorizontalAlign.Center,
                Text=colNames[ci],
                FontFilePath=fontGothic, TtcFontIndex=0,
                FontSize=9, FontColor=PdfColor.White, ZIndex=2
            });
        }

        // データ行
        var rows = new[]
        {
            new[]{"A-001", "コピー用紙 A4 500枚×5冊",       "10", "¥500",   "¥5,000"},
            new[]{"B-002", "ボールペン（黒）12本入セット",   " 5", "¥250",   "¥1,250"},
            new[]{"C-003", "クリアファイル A4 30枚入",       "20", "¥150",   "¥3,000"},
            new[]{"D-004", "蛍光マーカー 5色セット",         " 8", "¥400",   "¥3,200"},
            new[]{"E-005", "ノート B5 40枚 5冊セット",       "15", "¥180",   "¥2,700"},
        };

        double rowH = 15;
        double rowY = tableTop + headerH;

        for (int ri = 0; ri < rows.Length; ri++)
        {
            // 偶数行に薄い青背景
            if (ri % 2 == 1)
            {
                cmds.Add(new RectangleDrawCommand {
                    Name=$"tr-bg-{ri}", PageNumber=1, X=20, Y=rowY, Width=555, Height=rowH,
                    FillColor=new PdfColor(235,242,255), IsFilled=true, BorderWidth=0, ZIndex=1
                });
            }
            // セルテキスト（MS明朝 8.5pt）
            for (int ci = 0; ci < rows[ri].Length; ci++)
            {
                var ha = ci >= 2 ? PdfHorizontalAlign.Right : PdfHorizontalAlign.Left;
                cmds.Add(new TextDrawCommand {
                    Name=$"td-{ri}-{ci}", PageNumber=1,
                    X=colX[ci], Y=rowY+11, Width=colW[ci],
                    HorizontalAlign=ha,
                    Text=rows[ri][ci],
                    FontFilePath=ttcMincho, TtcFontIndex=0,
                    FontSize=8.5, FontColor=PdfColor.Black, ZIndex=2
                });
            }
            // 行罫線
            cmds.Add(new LineDrawCommand {
                Name=$"tr-line-{ri}", PageNumber=1,
                X=20, Y=rowY+rowH, X2=575, Y2=rowY+rowH,
                LineColor=PdfColor.LightGray, LineWidth=0.3, ZIndex=1
            });
            rowY += rowH;
        }

        // 表外枠
        cmds.Add(new RectangleDrawCommand {
            Name="tbl-border", PageNumber=1, X=20, Y=tableTop, Width=555, Height=rowY-tableTop,
            BorderColor=new PdfColor(0,60,120), BorderWidth=0.75, IsFilled=false, ZIndex=3
        });
        // 列縦線
        for (int ci = 1; ci < colX.Length; ci++)
        {
            cmds.Add(new LineDrawCommand {
                Name=$"col-vline-{ci}", PageNumber=1,
                X=colX[ci], Y=tableTop, X2=colX[ci], Y2=rowY,
                LineColor=new PdfColor(100,130,180), LineWidth=0.3, ZIndex=2
            });
        }

        // 小計行
        rowY += 3;
        cmds.Add(new TextDrawCommand {
            Name="subtotal-lbl", PageNumber=1,
            X=20, Y=rowY+11, Width=435, HorizontalAlign=PdfHorizontalAlign.Right,
            Text="小計",
            FontFilePath=fontGothic, TtcFontIndex=0,
            FontSize=9, FontColor=PdfColor.Black, ZIndex=1
        });
        cmds.Add(new TextDrawCommand {
            Name="subtotal-val", PageNumber=1,
            X=455, Y=rowY+11, Width=120, HorizontalAlign=PdfHorizontalAlign.Right,
            Text="¥15,150",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=9, FontColor=new PdfColor(160,0,0), ZIndex=1
        });
        rowY += 17;

        // 消費税行
        cmds.Add(new TextDrawCommand {
            Name="tax-lbl", PageNumber=1,
            X=20, Y=rowY+11, Width=435, HorizontalAlign=PdfHorizontalAlign.Right,
            Text="消費税 (10%)",
            FontFilePath=fontGothic, TtcFontIndex=0,
            FontSize=9, FontColor=PdfColor.Black, ZIndex=1
        });
        cmds.Add(new TextDrawCommand {
            Name="tax-val", PageNumber=1,
            X=455, Y=rowY+11, Width=120, HorizontalAlign=PdfHorizontalAlign.Right,
            Text="¥1,515",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=9, FontColor=new PdfColor(160,0,0), ZIndex=1
        });
        rowY += 17;

        // 合計行（背景付き）
        cmds.Add(new RectangleDrawCommand {
            Name="total-bg", PageNumber=1, X=355, Y=rowY, Width=220, Height=20,
            FillColor=new PdfColor(0,60,120), IsFilled=true,
            BorderColor=new PdfColor(0,40,80), BorderWidth=1.0, ZIndex=1
        });
        cmds.Add(new TextDrawCommand {
            Name="total-lbl", PageNumber=1,
            X=355, Y=rowY+15, Width=100, HorizontalAlign=PdfHorizontalAlign.Right,
            Text="合計金額",
            FontFilePath=fontGothic, TtcFontIndex=0,
            FontSize=10, FontColor=PdfColor.White, ZIndex=2
        });
        cmds.Add(new TextDrawCommand {
            Name="total-val", PageNumber=1,
            X=455, Y=rowY+15, Width=120, HorizontalAlign=PdfHorizontalAlign.Right,
            Text="¥16,665",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=11, FontColor=PdfColor.White, ZIndex=2
        });
        rowY += 25;

        // ════════════════════════════════════════════════
        // Section 6: フォントサイズ・種類テスト
        // ════════════════════════════════════════════════
        double fontY = rowY + 12;
        cmds.Add(new LineDrawCommand {
            Name="sep4", PageNumber=1, X=20, Y=fontY, X2=575, Y2=fontY,
            LineColor=new PdfColor(0,60,120), LineWidth=0.75, ZIndex=0
        });
        fontY += 15;
        cmds.Add(new TextDrawCommand {
            Name="font-label", PageNumber=1, X=20, Y=fontY,
            Text="■ フォントサイズ・種類テスト（MS明朝・MSゴシック）",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=9, FontColor=new PdfColor(0,60,120), ZIndex=1
        });
        fontY += 14;

        // MS明朝 3サイズ
        foreach (var (sz, label) in new[]{
            (8.0,  "8pt MS明朝：消費税額・摘要・備考など小さいテキスト　１２３ＡＢＣ"),
            (10.0, "10pt MS明朝：通常テキスト・品名・住所　請求書サンプル　¥123,456"),
            (12.0, "12pt MS明朝：強調テキスト・見出し・合計金額　東京都千代田区"),
        })
        {
            cmds.Add(new TextDrawCommand {
                Name=$"mincho-{sz}", PageNumber=1, X=25, Y=fontY,
                Text=label, FontFilePath=ttcMincho, TtcFontIndex=0,
                FontSize=sz, FontColor=PdfColor.Black, ZIndex=1
            });
            fontY += sz * 2.2;
        }

        // MSゴシック 2サイズ
        foreach (var (sz, label) in new[]{
            (8.0,  "8pt MSゴシック：表ヘッダー・ラベル・フォームフィールド名"),
            (10.0, "10pt MSゴシック：一般テキスト・会社名・部署名・担当者名"),
        })
        {
            cmds.Add(new TextDrawCommand {
                Name=$"gothic-{sz}", PageNumber=1, X=25, Y=fontY,
                Text=label, FontFilePath=fontGothic, TtcFontIndex=0,
                FontSize=sz, FontColor=new PdfColor(40,40,80), ZIndex=1
            });
            fontY += sz * 2.2;
        }

        // IsBold テスト（太字シミュレーション）
        fontY += 4;
        cmds.Add(new TextDrawCommand {
            Name="bold-label", PageNumber=1, X=20, Y=fontY,
            Text="■ IsBold 太字テスト（Tr 2 シミュレーション）",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=9, FontColor=new PdfColor(0,60,120), ZIndex=1
        });
        fontY += 14;

        foreach (var (sz, label) in new[]{
            (10.0, "10pt 太字：株式会社サンプル商事　請求書"),
            (12.0, "12pt 太字：合計金額　¥16,665"),
            (14.0, "14pt 太字：最初はうまく功がいちに"),
        })
        {
            cmds.Add(new TextDrawCommand {
                Name=$"bold-{sz}", PageNumber=1, X=25, Y=fontY,
                Text=label, FontFilePath=ttcMincho, TtcFontIndex=0,
                FontSize=sz, FontColor=PdfColor.Black, IsBold=true, ZIndex=1
            });
            fontY += sz * 2.4;
        }

        // ════════════════════════════════════════════════
        // Section 7: フッター（2重横線 + 著作権テキスト）
        // ════════════════════════════════════════════════
        cmds.Add(new LineDrawCommand {
            Name="footer-line1", PageNumber=1, X=20, Y=812, X2=575, Y2=812,
            LineColor=PdfColor.Black, LineWidth=1.0, ZIndex=1
        });
        cmds.Add(new LineDrawCommand {
            Name="footer-line2", PageNumber=1, X=20, Y=816, X2=575, Y2=816,
            LineColor=PdfColor.Black, LineWidth=0.5, ZIndex=1
        });
        cmds.Add(new TextDrawCommand {
            Name="footer-text", PageNumber=1, X=20, Y=829, Width=555,
            HorizontalAlign=PdfHorizontalAlign.Center,
            Text="PdfUtility.Core 総合テスト出力　Copyright © 2024 PdfUtilitySolution. All rights reserved.",
            FontFilePath=ttcMincho, TtcFontIndex=0,
            FontSize=7, FontColor=PdfColor.Gray, ZIndex=1
        });

        // ── 実行 ──────────────────────────────────────────────────────────
        var writer = new PdfIncrementalWriter(inputBytes);
        var pageInfo = writer.GetPageInfo(1);
        Console.WriteLine($"  入力: {writer.PageCount}ページ, Page1={pageInfo.Width:F0}x{pageInfo.Height:F0}pt");

        writer.ApplyCommands(cmds);
        byte[] result = writer.BuildIncrementalUpdate();

        string outPath = Path.Combine(outputDir, "comprehensive_test.pdf");
        File.WriteAllBytes(outPath, result);

        // 構造確認
        string inc = System.Text.Encoding.GetEncoding(28591)
            .GetString(result, inputBytes.Length, result.Length - inputBytes.Length);

        bool hasType0    = inc.Contains("/Subtype /Type0");
        bool hasCIDFont  = inc.Contains("/Subtype /CIDFontType2");
        bool hasImage    = inc.Contains("/Subtype /Image");
        bool hasXObject  = inc.Contains("/XObject");
        bool hasCmap     = inc.Contains("begincmap");

        Console.WriteLine($"  出力: {outPath}");
        Console.WriteLine($"  サイズ: {inputBytes.Length:N0} → {result.Length:N0} bytes (+{result.Length-inputBytes.Length:N0})");
        Console.WriteLine($"  /Subtype /Type0 (テキストフォント): {(hasType0   ? "✓" : "✗")}");
        Console.WriteLine($"  /CIDFontType2:                      {(hasCIDFont  ? "✓" : "✗")}");
        Console.WriteLine($"  /Subtype /Image (PNG埋め込み):      {(hasImage    ? "✓" : "✗")}");
        Console.WriteLine($"  /XObject (画像リソース):            {(hasXObject  ? "✓" : "✗")}");
        Console.WriteLine($"  ToUnicode CMap:                     {(hasCmap     ? "✓" : "✗")}");

        var verify = new PdfIncrementalWriter(result);
        bool pagesOk = verify.PageCount == writer.PageCount;
        Console.WriteLine($"  再解析ページ数: {verify.PageCount} → {(pagesOk ? "一致 ✓" : "不一致 ✗")}");

        bool allOk = hasType0 && hasCIDFont && hasImage && hasXObject && hasCmap && pagesOk;
        Console.WriteLine(allOk ? "\n総合テスト PASS ✓" : "\n総合テスト FAIL ✗");

        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(outPath) { UseShellExecute = true }); }
        catch { }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"総合テスト FAIL [{ex.GetType().Name}]: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }
}

// ─────────────────────────────────────────────────────────────────────────
// フォント描画単体テスト
// 空白A4 PDF をベースに、フォントサイズ違い＋太字のテキストのみ追記する。
// 元PDFのコンテンツと一切重ならないため、文字化けの原因を純粋に切り分けられる。
// ─────────────────────────────────────────────────────────────────────────

static void RunFontTest(string fontsDir, string outputDir)
{
    string ttcMincho = Path.Combine(fontsDir, "msmincho.ttc");
    if (!File.Exists(ttcMincho))
    {
        Console.WriteLine($"  スキップ: {ttcMincho} が見つかりません");
        return;
    }

    try
    {
        // 空白 A4 PDF をベースとして使う（元PDFの既存コンテンツを完全に排除）
        byte[] blankPdf = CreateMinimalA4Pdf();
        Console.WriteLine($"  ベース: 空白 A4 PDF ({blankPdf.Length} bytes)");

        var cmds = new List<PdfDrawCommand>
        {
            // 1. MS明朝  8pt 通常
            new TextDrawCommand {
                Name="t-8n",  PageNumber=1, X=50, Y=100,
                Text="MS明朝  8pt 通常：テスト123",
                FontFilePath=ttcMincho, TtcFontIndex=0,
                FontSize=8, FontColor=PdfColor.Black, IsBold=false, ZIndex=1
            },
            // 2. MS明朝 10pt 通常
            new TextDrawCommand {
                Name="t-10n", PageNumber=1, X=50, Y=130,
                Text="MS明朝 10pt 通常：テスト123",
                FontFilePath=ttcMincho, TtcFontIndex=0,
                FontSize=10, FontColor=PdfColor.Black, IsBold=false, ZIndex=1
            },
            // 3. MS明朝 12pt 通常
            new TextDrawCommand {
                Name="t-12n", PageNumber=1, X=50, Y=165,
                Text="MS明朝 12pt 通常：テスト123",
                FontFilePath=ttcMincho, TtcFontIndex=0,
                FontSize=12, FontColor=PdfColor.Black, IsBold=false, ZIndex=1
            },
            // 4. MS明朝 14pt 通常
            new TextDrawCommand {
                Name="t-14n", PageNumber=1, X=50, Y=205,
                Text="MS明朝 14pt 通常：テスト123",
                FontFilePath=ttcMincho, TtcFontIndex=0,
                FontSize=14, FontColor=PdfColor.Black, IsBold=false, ZIndex=1
            },
            // 5. MS明朝 12pt 太字
            new TextDrawCommand {
                Name="t-12b", PageNumber=1, X=50, Y=245,
                Text="MS明朝 12pt 太字：テスト123",
                FontFilePath=ttcMincho, TtcFontIndex=0,
                FontSize=12, FontColor=PdfColor.Black, IsBold=true, ZIndex=1
            },
        };

        var writer = new PdfIncrementalWriter(blankPdf);
        writer.ApplyCommands(cmds);
        byte[] result = writer.BuildIncrementalUpdate();

        string outPath = Path.Combine(outputDir, "fonttest.pdf");
        File.WriteAllBytes(outPath, result);

        Console.WriteLine($"  出力: {outPath}");
        Console.WriteLine($"  サイズ: {blankPdf.Length} → {result.Length:N0} bytes (+{result.Length - blankPdf.Length:N0})");

        // 追記部分のコンテンツストリームを表示して確認
        string inc = System.Text.Encoding.GetEncoding(28591)
            .GetString(result, blankPdf.Length, result.Length - blankPdf.Length);
        int streamStart = inc.IndexOf("stream\n", StringComparison.Ordinal);
        int streamEnd   = inc.IndexOf("\nendstream", StringComparison.Ordinal);
        if (streamStart >= 0 && streamEnd > streamStart)
        {
            string content = inc.Substring(streamStart + 7, streamEnd - streamStart - 7);
            Console.WriteLine("\n  --- コンテンツストリーム ---");
            foreach (string line in content.Split('\n'))
                if (!string.IsNullOrWhiteSpace(line))
                    Console.WriteLine($"  {line}");
        }

        Console.WriteLine("\nフォント描画単体テスト PASS ✓");

        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(outPath) { UseShellExecute = true }); }
        catch { }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  FAIL [{ex.GetType().Name}]: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }
}
