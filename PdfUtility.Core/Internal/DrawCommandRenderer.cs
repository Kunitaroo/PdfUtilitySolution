using System;
using System.Collections.Generic;
using System.Text;
using PdfUtility.Core.Drawing;
using PdfUtility.Core.Exceptions;
using PdfUtility.Core.Helpers;

namespace PdfUtility.Core.Internal
{
    /// <summary>
    /// PdfDrawCommand のリストを PDF コンテンツストリーム演算子に変換するレンダラー。
    ///
    /// 座標変換：
    ///   ユーザー座標系 → PDF座標系
    ///   ・ユーザー: 左上(0,0)、Y軸下向き
    ///   ・PDF仕様: 左下(0,0)、Y軸上向き
    ///   ・変換式: pdf_y = page_height - user_y
    ///
    /// 生成するPDF演算子：
    ///   矩形: [color] [linewidth] [x y w h] re [S/f/B]
    ///   線:   [color] [linewidth] [x1 y1] m [x2 y2] l S
    ///   画像: [w 0 0 h tx ty] cm  /ImN Do
    /// </summary>
    internal static class DrawCommandRenderer
    {
        /// <summary>
        /// 描画コマンドリストをPDFコンテンツストリーム演算子文字列に変換する。
        /// </summary>
        internal static string Render(
            IEnumerable<PdfDrawCommand> commands,
            double pageHeight,
            Dictionary<ImageDrawCommand, string> imageXObjectNames = null,
            Dictionary<TextDrawCommand, FontResource> textFontMap = null)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));

            var sorted = new List<PdfDrawCommand>(commands);
            sorted.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));

            var sb = new StringBuilder();
            sb.Append("q\n");
            foreach (var cmd in sorted)
            {
                switch (cmd)
                {
                    case RectangleDrawCommand rect:
                        sb.Append(RenderRectangle(rect, pageHeight));
                        break;

                    case LineDrawCommand line:
                        sb.Append(RenderLine(line, pageHeight));
                        break;

                    case ImageDrawCommand img:
                        string xObjName;
                        if (imageXObjectNames == null || !imageXObjectNames.TryGetValue(img, out xObjName))
                            throw new PdfRenderException(
                                "ImageDrawCommand に XObjectName が割り当てられていません。");
                        sb.Append(RenderImage(img, pageHeight, xObjName));
                        break;

                    case TextDrawCommand txt:
                        FontResource fr;
                        if (textFontMap == null || !textFontMap.TryGetValue(txt, out fr))
                            throw new PdfRenderException(
                                $"TextDrawCommand にフォントリソースが割り当てられていません。Name={txt.Name}");
                        sb.Append(txt.WritingMode == WritingMode.Vertical
                            ? RenderTextVertical(txt, fr, pageHeight)
                            : RenderText(txt, fr, pageHeight));
                        break;

                    default:
                        throw new PdfRenderException(
                            $"未対応の描画コマンド型: {cmd.GetType().Name}");
                }
            }
            sb.Append("Q\n");

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────
        // 矩形レンダリング
        // ─────────────────────────────────────────────────────

        private static string RenderRectangle(RectangleDrawCommand rect, double pageHeight)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                throw new PdfRenderException(
                    $"矩形のサイズが無効です。Width={rect.Width}, Height={rect.Height}");

            bool hasBorder = rect.BorderColor != null && rect.BorderWidth > 0;
            bool hasFill   = rect.IsFilled && rect.FillColor != null;

            if (!hasBorder && !hasFill)
                return "% rectangle: no stroke, no fill (skipped)\n";

            // Y座標変換: ユーザー左上角 → PDF左下角
            double pdfX = rect.X;
            double pdfY = pageHeight - rect.Y - rect.Height;

            var sb = new StringBuilder();
            sb.Append("q\n");

            if (hasBorder)
                sb.Append($"{rect.BorderWidth.F(3)} w\n");

            if (hasFill)
                sb.Append($"{rect.FillColor.ToPdfRgb()} rg\n");

            if (hasBorder)
                sb.Append($"{rect.BorderColor.ToPdfRgb()} RG\n");

            sb.Append($"{pdfX.F(3)} {pdfY.F(3)} {rect.Width.F(3)} {rect.Height.F(3)} re\n");

            if (hasFill && hasBorder) sb.Append("B\n");
            else if (hasFill)         sb.Append("f\n");
            else                      sb.Append("S\n");

            sb.Append("Q\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────
        // 線レンダリング
        // ─────────────────────────────────────────────────────

        private static string RenderLine(LineDrawCommand line, double pageHeight)
        {
            if (line.LineColor == null)
                throw new PdfRenderException("LineDrawCommand.LineColor が null です。");
            if (line.LineWidth <= 0)
                throw new PdfRenderException($"線の太さが無効です。LineWidth={line.LineWidth}");

            double pdfY1 = pageHeight - line.Y;
            double pdfY2 = pageHeight - line.Y2;

            var sb = new StringBuilder();
            sb.Append("q\n");
            sb.Append($"{line.LineWidth.F(3)} w\n");
            sb.Append($"{line.LineColor.ToPdfRgb()} RG\n");

            if (line.DashPattern != null && line.DashPattern.Length > 0)
            {
                var pattern = new StringBuilder("[");
                foreach (double d in line.DashPattern)
                    pattern.Append($" {d.F(3)}");
                pattern.Append($" ] {line.DashPhase.F(3)} d\n");
                sb.Append(pattern.ToString());
            }

            sb.Append($"{line.X.F(3)} {pdfY1.F(3)} m\n");
            sb.Append($"{line.X2.F(3)} {pdfY2.F(3)} l\n");
            sb.Append("S\n");
            sb.Append("Q\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────
        // テキストレンダリング
        // ─────────────────────────────────────────────────────

        private static string RenderText(TextDrawCommand cmd, FontResource fr, double pageHeight)
        {
            if (string.IsNullOrEmpty(cmd.Text)) return "";
            if (cmd.FontSize <= 0)
                throw new PdfRenderException($"TextDrawCommand.FontSize が無効です: {cmd.FontSize}");

            // グリフID列と合計テキスト幅を計算
            var glyphIds = new List<int>(cmd.Text.Length);
            double totalWidth = 0;
            foreach (char c in cmd.Text)
            {
                int glyphId;
                fr.UnicodeToGlyph.TryGetValue(c, out glyphId);
                glyphIds.Add(glyphId);

                int advWidth;
                fr.GlyphWidths.TryGetValue(glyphId, out advWidth);
                totalWidth += (double)advWidth * cmd.FontSize / fr.UnitsPerEm;
            }

            // 水平配置に基づいて描画開始 X を決定
            double startX = cmd.X;
            if (cmd.Width > 0)
            {
                switch (cmd.HorizontalAlign)
                {
                    case PdfHorizontalAlign.Center:
                        startX = cmd.X + (cmd.Width - totalWidth) / 2.0;
                        break;
                    case PdfHorizontalAlign.Right:
                        startX = cmd.X + cmd.Width - totalWidth;
                        break;
                }
            }

            // Y: ユーザー座標 (上端0) → PDF座標 (下端0)
            // cmd.Y をベースライン位置とする
            double pdfY = pageHeight - cmd.Y;

            // グリフIDを2バイトBEのhex文字列に変換
            var hex = new StringBuilder(glyphIds.Count * 4);
            foreach (int gid in glyphIds)
                hex.Append(gid.ToString("X4"));

            var color = cmd.FontColor ?? PdfColor.Black;

            var sb = new StringBuilder();
            sb.Append("BT\n");
            sb.Append($"/{fr.PdfName} {cmd.FontSize.F(3)} Tf\n");
            sb.Append($"{color.ToPdfRgb()} rg\n");
            if (cmd.IsBold)
            {
                // 太字シミュレーション: テキスト描画モード2(塗り+輪郭)で太字に見せる
                // ストローク幅はフォントサイズの約4%（過度に太くならない値）
                double strokeW = Math.Max(cmd.FontSize * 0.04, 0.3);
                sb.Append($"{strokeW.F(3)} w\n");
                sb.Append($"{color.ToPdfRgb()} RG\n");
                sb.Append("2 Tr\n");
            }
            sb.Append($"{startX.F(3)} {pdfY.F(3)} Td\n");
            sb.Append($"<{hex}> Tj\n");
            sb.Append("ET\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────
        // 縦書きテキストレンダリング
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// 縦書きテキストを描画する。
        ///   ・cmd.X は最初の列（最も右）の中心 X を指す
        ///   ・cmd.Y は列頭（最上段の文字セル）の上端 Y を指す
        ///   ・"\n" 区切りで列を分け、新しい列は左へ流れる（cmd.X - colIdx * fontSize）
        ///   ・各文字は fontSize × fontSize の正方セルを占有し、Y 軸方向に下へ進む
        ///   ・cmd.Width &gt; 0 のとき列の長さ（縦方向）と解釈し、VerticalTextAlign で配置調整
        /// 文字種別：
        ///   ・Upright（直立）：和字・かな・漢字 — そのまま
        ///   ・Rotated（回転）：ASCII 半角英数記号、長音符ー、括弧類 — 90° 時計回り
        ///   ・UpperRight（右上）：句読点 。、．， — セルの右上にオフセット
        /// </summary>
        private static string RenderTextVertical(TextDrawCommand cmd, FontResource fr, double pageHeight)
        {
            if (string.IsNullOrEmpty(cmd.Text)) return "";
            if (cmd.FontSize <= 0)
                throw new PdfRenderException($"TextDrawCommand.FontSize が無効です: {cmd.FontSize}");

            string[] columns = cmd.Text.Split('\n');
            double fontSize = cmd.FontSize;
            var color = cmd.FontColor ?? PdfColor.Black;

            var sb = new StringBuilder();
            sb.Append("BT\n");
            sb.Append($"/{fr.PdfName} {fontSize.F(3)} Tf\n");
            sb.Append($"{color.ToPdfRgb()} rg\n");
            if (cmd.IsBold)
            {
                double strokeW = Math.Max(fontSize * 0.04, 0.3);
                sb.Append($"{strokeW.F(3)} w\n");
                sb.Append($"{color.ToPdfRgb()} RG\n");
                sb.Append("2 Tr\n");
            }

            for (int colIdx = 0; colIdx < columns.Length; colIdx++)
            {
                string line = columns[colIdx] ?? "";
                double colCenterX = cmd.X - colIdx * fontSize;

                // 列方向の配置（cmd.Width を列長として扱う）
                double colStartUserY = cmd.Y;
                if (cmd.Width > 0 && cmd.VerticalTextAlign != PdfVerticalTextAlign.Top)
                {
                    double textHeight = line.Length * fontSize;
                    double slack = cmd.Width - textHeight;
                    if (slack > 0)
                    {
                        if (cmd.VerticalTextAlign == PdfVerticalTextAlign.Middle)
                            colStartUserY = cmd.Y + slack / 2.0;
                        else if (cmd.VerticalTextAlign == PdfVerticalTextAlign.Bottom)
                            colStartUserY = cmd.Y + slack;
                    }
                }

                for (int chIdx = 0; chIdx < line.Length; chIdx++)
                {
                    char c = line[chIdx];
                    int gid;
                    fr.UnicodeToGlyph.TryGetValue(c, out gid);

                    int advWidth1000 = 0;
                    fr.GlyphWidths.TryGetValue(gid, out advWidth1000);
                    double glyphAdvWidth = (double)advWidth1000 * fontSize / fr.UnitsPerEm;

                    double cellTopUserY   = colStartUserY + chIdx * fontSize;
                    double pdfCellTopY    = pageHeight - cellTopUserY;
                    double pdfCellBottomY = pdfCellTopY - fontSize;

                    string gidHex = gid.ToString("X4");

                    switch (ClassifyVerticalChar(c))
                    {
                        case VerticalCharClass.Rotated:
                        {
                            // 90° 時計回り回転：[0 -1 1 0 tx ty]
                            // (tx, ty) はグリフ原点 (lower-left) の写像先
                            // 回転後の占有：x ∈ [tx + descender, tx + ascender]、y ∈ [ty - advW, ty]
                            // (asc + desc)/2 ≈ 0.38 * fontSize として中心合わせ
                            double tx = colCenterX - fontSize * 0.38;
                            double ty = pdfCellTopY - fontSize / 2.0 + glyphAdvWidth / 2.0;
                            sb.Append($"0 -1 1 0 {tx.F(3)} {ty.F(3)} Tm\n");
                            sb.Append($"<{gidHex}> Tj\n");
                            break;
                        }
                        case VerticalCharClass.UpperRight:
                        {
                            // 句読点をセル右上へオフセット（縦書き慣例）
                            // 通常配置から +0.5em 右、+0.5em 上にずらす
                            double tx = colCenterX - glyphAdvWidth / 2.0 + fontSize * 0.5;
                            double ty = pdfCellBottomY + fontSize * 0.62;
                            sb.Append($"1 0 0 1 {tx.F(3)} {ty.F(3)} Tm\n");
                            sb.Append($"<{gidHex}> Tj\n");
                            break;
                        }
                        default: // Upright
                        {
                            // 通常配置：水平中央・ベースラインはセル底から +0.12em
                            double tx = colCenterX - glyphAdvWidth / 2.0;
                            double ty = pdfCellBottomY + fontSize * 0.12;
                            sb.Append($"1 0 0 1 {tx.F(3)} {ty.F(3)} Tm\n");
                            sb.Append($"<{gidHex}> Tj\n");
                            break;
                        }
                    }
                }
            }

            sb.Append("ET\n");
            return sb.ToString();
        }

        /// <summary>縦書きでの文字分類。</summary>
        private enum VerticalCharClass { Upright, Rotated, UpperRight }

        /// <summary>縦書き時の文字配置種別を返す。</summary>
        private static VerticalCharClass ClassifyVerticalChar(char c)
        {
            // 句読点：右上にオフセット
            if (c == '。' || c == '、' || c == '．' || c == '，') // 。 、 ．，
                return VerticalCharClass.UpperRight;

            // 半角ASCII（英数記号）：90度回転
            if (c >= 0x21 && c <= 0x7E)
                return VerticalCharClass.Rotated;

            // 長音記号・ダッシュ・ハイフン類：90度回転
            switch (c)
            {
                case 'ー': // ー 長音記号
                case '―': // ― 水平線
                case '—': // — em dash
                case '–': // – en dash
                case '‐': // ‐ ハイフン
                case '−': // − minus
                case '－': // － 全角ハイフン
                case '〜': // 〜 波形
                case '～': // ～ 全角波形
                    return VerticalCharClass.Rotated;
            }

            // 縦書き用に回転すべき括弧類
            switch (c)
            {
                case '「': case '」': // 「 」
                case '『': case '』': // 『 』
                case '（': case '）': // （ ）
                case '〈': case '〉': // 〈 〉
                case '《': case '》': // 《 》
                case '【': case '】': // 【 】
                case '〔': case '〕': // 〔 〕
                case '［': case '］': // ［ ］
                case '｛': case '｝': // ｛ ｝
                    return VerticalCharClass.Rotated;
            }

            // CJK・かな等：直立
            return VerticalCharClass.Upright;
        }

        // ─────────────────────────────────────────────────────
        // 画像レンダリング
        // ─────────────────────────────────────────────────────

        private static string RenderImage(ImageDrawCommand img, double pageHeight, string xObjName)
        {
            if (img.ImageBytes == null || img.ImageBytes.Length == 0)
                throw new PdfRenderException("ImageDrawCommand.ImageBytes が null または空です。");
            if (img.Width <= 0 || img.Height <= 0)
                throw new PdfRenderException(
                    $"画像のサイズが無効です。Width={img.Width}, Height={img.Height}");

            // Y座標変換: ユーザー座標の左上 → PDF座標の左下
            double pdfX = img.X;
            double pdfY = pageHeight - img.Y - img.Height;

            var sb = new StringBuilder();
            sb.Append("q\n");
            // CTM: [width 0 0 height tx ty] cm  で画像をスケール+配置
            sb.Append($"{img.Width.F(3)} 0 0 {img.Height.F(3)} {pdfX.F(3)} {pdfY.F(3)} cm\n");
            sb.Append($"/{xObjName} Do\n");
            sb.Append("Q\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────
        // 数値フォーマットヘルパー
        // ─────────────────────────────────────────────────────

        private static string F(this double value, int decimals)
            => value.ToString("F" + decimals, System.Globalization.CultureInfo.InvariantCulture);
    }
}
