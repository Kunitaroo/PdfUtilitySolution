using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PdfUtility.Core.Documents;
using PdfUtility.Core.Drawing;
using PdfUtility.Core.Exceptions;
using PdfUtility.Core.Helpers;
using PdfUtility.Core.Internal;
using PdfUtility.Core.Options;
using PdfUtility.Core.Services;

namespace PdfUtility.Core
{
    /// <summary>
    /// 既存PDFにインクリメンタルアップデート方式で追記を行うクラス。
    ///
    /// 構造：
    ///   [既存PDFバイト列 ... %%EOF]
    ///   [追記オブジェクト群（コンテンツストリーム、画像XObject、更新ページオブジェクト）]
    ///   xref
    ///   trailer &lt;&lt; /Size N /Root X 0 R /Prev &lt;既存xrefオフセット&gt; &gt;&gt;
    ///   startxref
    ///   %%EOF
    /// </summary>
    public class PdfIncrementalWriter
    {
        private readonly PdfDocumentContext _ctx;
        private readonly Dictionary<int, List<string>> _pageContentStreams;
        private readonly Dictionary<int, List<PdfDrawCommand>> _pageCommands;
        private int _nextObjNum;

        public PdfIncrementalWriter(byte[] pdfBytes) : this(pdfBytes, null) { }

        public PdfIncrementalWriter(byte[] pdfBytes, PdfUtilityOptions options)
        {
            if (pdfBytes == null) throw new ArgumentNullException(nameof(pdfBytes));
            if (pdfBytes.Length == 0) throw new PdfLoadException("PDFバイト列が空です。");

            var parser = new PdfRawParser(pdfBytes);
            _ctx = parser.Parse();

            // 暗号化・パーミッションのチェック（オプションに応じて例外または警告）
            var reader = new PdfReaderService(options);
            reader.ApplyEncryptionPolicy(_ctx.Encryption);

            _pageContentStreams = new Dictionary<int, List<string>>();
            _pageCommands = new Dictionary<int, List<PdfDrawCommand>>();
            _nextObjNum = _ctx.TotalObjectCount;
        }

        // ─────────────────────────────────────────────────────
        // ドキュメント情報
        // ─────────────────────────────────────────────────────

        public int PageCount => _ctx.Pages.Count;

        public PdfPageInfo GetPageInfo(int pageNumber)
        {
            ValidatePageNumber(pageNumber);
            var p = _ctx.Pages[pageNumber - 1];
            return new PdfPageInfo { PageNumber = p.PageNumber, Width = p.Width, Height = p.Height };
        }

        public PdfDocumentInfo GetDocumentInfo()
        {
            var parser = new PdfRawParser(_ctx.OriginalBytes);
            string version = parser.ReadPdfVersion();
            var pages = new List<PdfPageInfo>();
            foreach (var p in _ctx.Pages)
                pages.Add(new PdfPageInfo { PageNumber = p.PageNumber, Width = p.Width, Height = p.Height });
            return new PdfDocumentInfo { PageCount = _ctx.Pages.Count, Pages = pages, PdfVersion = version };
        }

        // ─────────────────────────────────────────────────────
        // 描画命令の登録（公開API）
        // ─────────────────────────────────────────────────────

        public void ApplyCommand(PdfDrawCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            ValidatePageNumber(command.PageNumber);
            if (!_pageCommands.ContainsKey(command.PageNumber))
                _pageCommands[command.PageNumber] = new List<PdfDrawCommand>();
            _pageCommands[command.PageNumber].Add(command);
        }

        public void ApplyCommands(IEnumerable<PdfDrawCommand> commands)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            foreach (var cmd in commands) ApplyCommand(cmd);
        }

        internal void AddContentToPage(int pageNumber, string pdfOperators)
        {
            ValidatePageNumber(pageNumber);
            if (!_pageContentStreams.ContainsKey(pageNumber))
                _pageContentStreams[pageNumber] = new List<string>();
            _pageContentStreams[pageNumber].Add(pdfOperators);
        }

        // ─────────────────────────────────────────────────────
        // インクリメンタルアップデートの書き出し
        // ─────────────────────────────────────────────────────

        public byte[] BuildIncrementalUpdate()
        {
            _nextObjNum = _ctx.TotalObjectCount;

            // ── Phase A: コマンドを解析し、画像・フォントリソースを収集 ──────────
            var pageImageResources = new Dictionary<int, List<ImageResource>>();
            var pageFontResources  = new Dictionary<int, List<FontResource>>();
            // フォントキー（filePath|index）→ FontResource（同一フォントは1つに集約）
            var allFonts    = new Dictionary<string, FontResource>();
            int imgCounter  = 0;
            int fontCounter = 0;

            foreach (var kv in _pageCommands)
            {
                int pageNumber = kv.Key;
                var page = _ctx.Pages[pageNumber - 1];
                var imageResources = new List<ImageResource>();

                foreach (var cmd in kv.Value)
                {
                    if (cmd is ImageDrawCommand imgCmd)
                    {
                        var parsed = PdfImageHelper.Parse(imgCmd.ImageBytes);
                        imageResources.Add(new ImageResource
                        {
                            XObjectName  = $"Im{imgCounter++}",
                            ObjectNumber = 0,
                            ParsedImage  = parsed,
                            Command      = imgCmd
                        });
                    }
                    else if (cmd is TextDrawCommand txtCmd)
                    {
                        if (string.IsNullOrEmpty(txtCmd.FontFilePath))
                            throw new PdfRenderException(
                                $"TextDrawCommand.FontFilePath が設定されていません。Name={txtCmd.Name}");

                        string fontKey = txtCmd.FontFilePath + "|" + txtCmd.TtcFontIndex;
                        if (!allFonts.TryGetValue(fontKey, out FontResource fr))
                        {
                            byte[] ttfBytes = FontHelper.LoadFontBytes(txtCmd.FontFilePath, txtCmd.TtcFontIndex);
                            FontHelper.GetAscenderDescender(ttfBytes,
                                out int asc, out int desc, out int capH);
                            ushort fsType = FontHelper.GetFsType(ttfBytes);
                            fr = new FontResource
                            {
                                PdfName        = $"PUF{fontCounter++}",
                                FilePath       = txtCmd.FontFilePath,
                                TtcIndex       = txtCmd.TtcFontIndex,
                                TtfBytes       = ttfBytes,
                                UnitsPerEm     = FontHelper.GetUnitsPerEm(ttfBytes),
                                PostScriptName = FontHelper.GetPostScriptName(ttfBytes),
                                Ascender       = asc,
                                Descender      = desc,
                                CapHeight      = capH,
                                BBox           = FontHelper.GetBoundingBox(ttfBytes),
                                EmbedFont      = (fsType & 0x0002) == 0,
                            };
                            allFonts[fontKey] = fr;
                        }

                        // 使用グリフを収集
                        foreach (char c in txtCmd.Text ?? "")
                        {
                            int cp = c;
                            if (!fr.UnicodeToGlyph.ContainsKey(cp))
                            {
                                int gid = FontHelper.GetGlyphId(fr.TtfBytes, cp);
                                fr.UnicodeToGlyph[cp] = gid;
                                if (!fr.GlyphWidths.ContainsKey(gid))
                                    fr.GlyphWidths[gid] = FontHelper.GetAdvanceWidth(fr.TtfBytes, gid);
                            }
                        }

                        // このページのフォントリソースリストに追加（重複排除）
                        if (!pageFontResources.ContainsKey(pageNumber))
                            pageFontResources[pageNumber] = new List<FontResource>();
                        if (!pageFontResources[pageNumber].Contains(fr))
                            pageFontResources[pageNumber].Add(fr);
                    }
                }

                // XObjectName マップを構築
                var xObjNames  = new Dictionary<ImageDrawCommand, string>();
                foreach (var ir in imageResources) xObjNames[ir.Command] = ir.XObjectName;

                // TextDrawCommand → FontResource マップを構築
                var textFontMap = new Dictionary<TextDrawCommand, FontResource>();
                foreach (var cmd in kv.Value)
                {
                    if (cmd is TextDrawCommand tc)
                    {
                        string fk = tc.FontFilePath + "|" + tc.TtcFontIndex;
                        if (allFonts.TryGetValue(fk, out FontResource fr))
                            textFontMap[tc] = fr;
                    }
                }

                string operators = DrawCommandRenderer.Render(kv.Value, page.Height, xObjNames, textFontMap);
                AddContentToPage(pageNumber, operators);

                if (imageResources.Count > 0) pageImageResources[pageNumber] = imageResources;
            }

            if (_pageContentStreams.Count == 0)
                AddContentToPage(1, string.Empty);

            // ── Phase B: 全オブジェクトを書き出す ────────────────────────────────
            using (var ms = new MemoryStream())
            {
                ms.Write(_ctx.OriginalBytes, 0, _ctx.OriginalBytes.Length);
                WriteAscii(ms, EndsWithEof(_ctx.OriginalBytes) ? "\n" : "\n%%EOF\n");

                // フォントオブジェクト番号を事前採番（ページオブジェクトが参照するため先に確定）
                foreach (var fr in allFonts.Values)
                {
                    if (fr.EmbedFont)
                        fr.FontFile2ObjNum  = _nextObjNum++;
                    fr.DescriptorObjNum = _nextObjNum++;
                    fr.CidFontObjNum    = _nextObjNum++;
                    fr.ToUnicodeObjNum  = _nextObjNum++;
                    fr.Type0ObjNum      = _nextObjNum++;
                }

                var newObjects = new List<(int objNum, long offset)>();

                foreach (var kv in _pageContentStreams)
                {
                    int pageNumber = kv.Key;
                    var page = _ctx.Pages[pageNumber - 1];
                    string operators = string.Join("\n", kv.Value);

                    // ① コンテンツストリーム
                    int contentObjNum = _nextObjNum++;
                    long contentOffset = ms.Position;
                    WriteContentStreamObject(ms, contentObjNum, operators);
                    newObjects.Add((contentObjNum, contentOffset));

                    // ② 画像 XObject
                    pageImageResources.TryGetValue(pageNumber, out var imageResources);
                    if (imageResources != null)
                    {
                        foreach (var ir in imageResources)
                        {
                            ir.ObjectNumber = _nextObjNum++;
                            long imgOffset = ms.Position;
                            WriteImageXObject(ms, ir.ObjectNumber, ir.ParsedImage);
                            newObjects.Add((ir.ObjectNumber, imgOffset));
                        }
                    }

                    // ③ 更新版ページオブジェクト
                    pageFontResources.TryGetValue(pageNumber, out var fontResources);
                    long pageOffset = ms.Position;
                    WriteUpdatedPageObject(ms, page, contentObjNum, imageResources, fontResources);
                    newObjects.Add((page.ObjectNumber, pageOffset));
                }

                // ④ フォントオブジェクト群（全ページ共通、各フォントにつき5オブジェクト）
                foreach (var fr in allFonts.Values)
                {
                    if (fr.EmbedFont)
                    {
                        long ff2Off = ms.Position;
                        WriteFontFile2Object(ms, fr);
                        newObjects.Add((fr.FontFile2ObjNum, ff2Off));
                    }

                    long fdOff = ms.Position;
                    WriteFontDescriptorObject(ms, fr);
                    newObjects.Add((fr.DescriptorObjNum, fdOff));

                    long cidOff = ms.Position;
                    WriteCidFontObject(ms, fr);
                    newObjects.Add((fr.CidFontObjNum, cidOff));

                    long touOff = ms.Position;
                    WriteToUnicodeObject(ms, fr);
                    newObjects.Add((fr.ToUnicodeObjNum, touOff));

                    long t0Off = ms.Position;
                    WriteType0FontObject(ms, fr);
                    newObjects.Add((fr.Type0ObjNum, t0Off));
                }

                long xrefOffset = ms.Position;
                WriteXrefSection(ms, newObjects);
                WriteTrailer(ms, _nextObjNum, xrefOffset);

                return ms.ToArray();
            }
        }

        // ─────────────────────────────────────────────────────
        // オブジェクト書き込み
        // ─────────────────────────────────────────────────────

        private void WriteContentStreamObject(MemoryStream ms, int objNum, string operators)
        {
            string normalized = operators.Replace("\r\n", "\n");
            byte[] content = Encoding.GetEncoding(28591).GetBytes(normalized);

            WriteAscii(ms, $"{objNum} 0 obj\n");
            WriteAscii(ms, $"<< /Length {content.Length} >>\n");
            WriteAscii(ms, "stream\n");
            ms.Write(content, 0, content.Length);
            WriteAscii(ms, "\nendstream\nendobj\n");
        }

        private void WriteImageXObject(MemoryStream ms, int objNum, ParsedImage img)
        {
            var sb = new StringBuilder();
            sb.Append($"{objNum} 0 obj\n");
            sb.Append("<<");
            sb.Append(" /Type /XObject");
            sb.Append(" /Subtype /Image");
            sb.Append($" /Width {img.Width}");
            sb.Append($" /Height {img.Height}");
            sb.Append($" /ColorSpace {img.ColorSpace}");
            sb.Append($" /BitsPerComponent {img.BitsPerComponent}");
            if (img.Filter != null)
                sb.Append($" /Filter {img.Filter}");
            sb.Append($" /Length {img.ImageData.Length}");
            sb.Append(" >>\n");
            sb.Append("stream\n");
            WriteAscii(ms, sb.ToString());
            ms.Write(img.ImageData, 0, img.ImageData.Length);
            WriteAscii(ms, "\nendstream\nendobj\n");
        }

        private void WriteUpdatedPageObject(
            MemoryStream ms,
            InternalPageInfo page,
            int newContentObjNum,
            List<ImageResource> imageResources,
            List<FontResource> fontResources)
        {
            string contentsValue = BuildContentsArray(page.ExistingContents, newContentObjNum);

            double[] mb = page.MediaBox;
            string mediaBoxStr = $"[{mb[0].ToInvariant()} {mb[1].ToInvariant()} {mb[2].ToInvariant()} {mb[3].ToInvariant()}]";

            var originalPageDict = ReadOriginalPageDict(page);
            bool needResources = (imageResources != null && imageResources.Count > 0)
                              || (fontResources  != null && fontResources.Count  > 0);

            var sb = new StringBuilder();
            sb.Append($"{page.ObjectNumber} {page.Generation} obj\n");
            sb.Append("<<");

            foreach (var kv in originalPageDict)
            {
                if (kv.Key == "Contents" || kv.Key == "MediaBox") continue;
                if (needResources && kv.Key == "Resources") continue;
                sb.Append($" /{kv.Key} {SerializeValue(kv.Value)}");
            }

            sb.Append($" /MediaBox {mediaBoxStr}");
            sb.Append($" /Contents {contentsValue}");

            if (needResources)
            {
                string resourcesStr = BuildMergedResources(originalPageDict, imageResources, fontResources);
                sb.Append($" /Resources {resourcesStr}");
            }

            sb.Append(" >>\nendobj\n");
            WriteAscii(ms, sb.ToString());
        }

        // ─────────────────────────────────────────────────────
        // フォントオブジェクト書き込み
        // ─────────────────────────────────────────────────────

        private void WriteFontFile2Object(MemoryStream ms, FontResource fr)
        {
            byte[] ttf = fr.TtfBytes;
            WriteAscii(ms, $"{fr.FontFile2ObjNum} 0 obj\n");
            WriteAscii(ms, $"<< /Length1 {ttf.Length} /Length {ttf.Length} >>\n");
            WriteAscii(ms, "stream\n");
            ms.Write(ttf, 0, ttf.Length);
            WriteAscii(ms, "\nendstream\nendobj\n");
        }

        private void WriteFontDescriptorObject(MemoryStream ms, FontResource fr)
        {
            int upm = fr.UnitsPerEm;
            int[] bb = fr.BBox ?? new[] { 0, 0, upm, upm };
            int scale(int v) => upm > 0 ? (int)Math.Round((double)v * 1000 / upm) : v;

            int asc  = scale(fr.Ascender);
            int desc = scale(fr.Descender);
            int capH = scale(fr.CapHeight);
            int bx0  = scale(bb[0]); int by0 = scale(bb[1]);
            int bx1  = scale(bb[2]); int by1 = scale(bb[3]);

            // スケールが極端に小さいフォント（unitsPerEm=256等）では
            // FontBBoxとメトリクスが正常に見えても実際より小さい場合がある。
            // 大きいフォントサイズでビューアがクリッピングするのを防ぐため
            // 各値が妥当な範囲に収まるよう補正する。
            if (asc  <  500) asc  =  880;
            if (desc >  -50) desc = -120;
            if (capH <  400) capH =  693;
            // FontBBox: CJKフォントの一般的な安全値（1000em単位）
            if (bx1 - bx0 < 800) { bx0 = -20;  bx1 = 1030; }
            if (by1 - by0 < 800) { by0 = -210; by1 =  900; }

            var sb = new StringBuilder();
            sb.Append($"{fr.DescriptorObjNum} 0 obj\n");
            sb.Append("<<");
            sb.Append(" /Type /FontDescriptor");
            sb.Append($" /FontName /{fr.PostScriptName}");
            sb.Append(" /Flags 4");
            sb.Append($" /FontBBox [{bx0} {by0} {bx1} {by1}]");
            sb.Append(" /ItalicAngle 0");
            sb.Append($" /Ascent {asc}");
            sb.Append($" /Descent {desc}");
            sb.Append($" /CapHeight {capH}");
            sb.Append(" /StemV 80");
            if (fr.EmbedFont)
                sb.Append($" /FontFile2 {fr.FontFile2ObjNum} 0 R");
            sb.Append(" >>\nendobj\n");
            WriteAscii(ms, sb.ToString());
        }

        private void WriteCidFontObject(MemoryStream ms, FontResource fr)
        {
            // /W 配列：使用グリフのみ個別記載 (glyphId [width_in_1000units])
            int upm = fr.UnitsPerEm;
            var wSb = new StringBuilder("[");
            foreach (var kv in fr.GlyphWidths.OrderBy(x => x.Key))
            {
                int pdfWidth = upm > 0 ? (int)Math.Round((double)kv.Value * 1000 / upm) : kv.Value;
                wSb.Append($" {kv.Key} [{pdfWidth}]");
            }
            wSb.Append(" ]");

            var sb = new StringBuilder();
            sb.Append($"{fr.CidFontObjNum} 0 obj\n");
            sb.Append("<<");
            sb.Append(" /Type /Font");
            sb.Append(" /Subtype /CIDFontType2");
            sb.Append($" /BaseFont /{fr.PostScriptName}");
            sb.Append(" /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >>");
            sb.Append($" /FontDescriptor {fr.DescriptorObjNum} 0 R");
            sb.Append(" /DW 1000");
            sb.Append($" /W {wSb}");
            sb.Append(" /CIDToGIDMap /Identity");
            sb.Append(" >>\nendobj\n");
            WriteAscii(ms, sb.ToString());
        }

        private void WriteToUnicodeObject(MemoryStream ms, FontResource fr)
        {
            // ToUnicode CMap：GID(hex) → Unicode(hex) のマッピング
            // 同一GIDが複数Unicodeにマップされる場合は最初の1件のみ使用（重複排除）
            var entries = fr.UnicodeToGlyph
                .Where(kv => kv.Value != 0)
                .GroupBy(kv => kv.Value)
                .Select(g => g.First())
                .OrderBy(kv => kv.Value)
                .ToList();

            var cmap = new StringBuilder();
            cmap.AppendLine("/CIDInit /ProcSet findresource begin");
            cmap.AppendLine("12 dict begin");
            cmap.AppendLine("begincmap");
            cmap.AppendLine("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def");
            cmap.AppendLine("/CMapName /Adobe-Identity-UCS def");
            cmap.AppendLine("/CMapType 2 def");
            // PDF spec limits each beginbfchar...endbfchar block to max 100 entries
            const int MaxPerBlock = 100;
            for (int start = 0; start < entries.Count; start += MaxPerBlock)
            {
                int count = Math.Min(MaxPerBlock, entries.Count - start);
                cmap.AppendLine($"{count} beginbfchar");
                for (int i = start; i < start + count; i++)
                    cmap.AppendLine($"<{entries[i].Value:X4}> <{entries[i].Key:X4}>");
                cmap.AppendLine("endbfchar");
            }
            cmap.AppendLine("endcmap");
            cmap.AppendLine("CMapName currentdict /CMap defineresource pop");
            cmap.AppendLine("end");
            cmap.AppendLine("end");

            byte[] cmapBytes = Encoding.GetEncoding(28591).GetBytes(cmap.ToString());
            WriteAscii(ms, $"{fr.ToUnicodeObjNum} 0 obj\n");
            WriteAscii(ms, $"<< /Length {cmapBytes.Length} >>\n");
            WriteAscii(ms, "stream\n");
            ms.Write(cmapBytes, 0, cmapBytes.Length);
            WriteAscii(ms, "\nendstream\nendobj\n");
        }

        private void WriteType0FontObject(MemoryStream ms, FontResource fr)
        {
            var sb = new StringBuilder();
            sb.Append($"{fr.Type0ObjNum} 0 obj\n");
            sb.Append("<<");
            sb.Append(" /Type /Font");
            sb.Append(" /Subtype /Type0");
            sb.Append($" /BaseFont /{fr.PostScriptName}");
            sb.Append(" /Encoding /Identity-H");
            sb.Append($" /DescendantFonts [{fr.CidFontObjNum} 0 R]");
            sb.Append($" /ToUnicode {fr.ToUnicodeObjNum} 0 R");
            sb.Append(" >>\nendobj\n");
            WriteAscii(ms, sb.ToString());
        }

        // ─────────────────────────────────────────────────────
        // /Resources 辞書のマージ
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// 既存の /Resources に画像 XObject とフォントを追加した Resources 辞書文字列を生成する。
        /// </summary>
        private string BuildMergedResources(
            Dictionary<string, object> originalPageDict,
            List<ImageResource> imageResources,
            List<FontResource> fontResources)
        {
            // 既存 Resources を取得
            Dictionary<string, object> resources = null;
            if (originalPageDict.TryGetValue("Resources", out object resVal))
            {
                if (resVal is PdfRef resRef)
                {
                    var parser = new PdfRawParser(_ctx.OriginalBytes);
                    resources = parser.ReadObjectDict(_ctx.XrefEntries, resRef.ObjectNumber);
                }
                else if (resVal is Dictionary<string, object> resDict)
                {
                    resources = new Dictionary<string, object>(resDict);
                }
            }
            if (resources == null) resources = new Dictionary<string, object>();

            // 既存 /XObject をマージ
            if (imageResources != null && imageResources.Count > 0)
            {
                Dictionary<string, object> xObjects = ResolveSubDict(resources, "XObject");
                foreach (var ir in imageResources)
                    xObjects[ir.XObjectName] = new PdfRef { ObjectNumber = ir.ObjectNumber, Generation = 0 };
                resources["XObject"] = xObjects;
            }

            // /Font をマージ
            if (fontResources != null && fontResources.Count > 0)
            {
                Dictionary<string, object> fonts = ResolveSubDict(resources, "Font");
                foreach (var fr in fontResources)
                    fonts[fr.PdfName] = new PdfRef { ObjectNumber = fr.Type0ObjNum, Generation = 0 };
                resources["Font"] = fonts;
            }

            return SerializeValue(resources);
        }

        private Dictionary<string, object> ResolveSubDict(
            Dictionary<string, object> parent, string key)
        {
            if (!parent.TryGetValue(key, out object val))
                return new Dictionary<string, object>();
            if (val is Dictionary<string, object> d)
                return new Dictionary<string, object>(d);
            if (val is PdfRef r)
            {
                var parser = new PdfRawParser(_ctx.OriginalBytes);
                var resolved = parser.ReadObjectDict(_ctx.XrefEntries, r.ObjectNumber);
                return resolved != null
                    ? new Dictionary<string, object>(resolved)
                    : new Dictionary<string, object>();
            }
            return new Dictionary<string, object>();
        }

        // ─────────────────────────────────────────────────────
        // /Contents 配列の構築
        // ─────────────────────────────────────────────────────

        private string BuildContentsArray(object existingContents, int newObjNum)
        {
            string newRef = $"{newObjNum} 0 R";

            if (existingContents == null)
                return $"[{newRef}]";

            if (existingContents is PdfRef r)
                return $"[{r.ObjectNumber} {r.Generation} R {newRef}]";

            if (existingContents is List<object> arr)
            {
                var sb = new StringBuilder("[");
                foreach (var item in arr) sb.Append($" {SerializeValue(item)}");
                sb.Append($" {newRef}]");
                return sb.ToString();
            }

            return $"[{newRef}]";
        }

        private Dictionary<string, object> ReadOriginalPageDict(InternalPageInfo page)
        {
            try
            {
                var parser = new PdfRawParser(_ctx.OriginalBytes);
                return parser.ReadObjectDict(_ctx.XrefEntries, page.ObjectNumber)
                       ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object> { { "Type", "Page" } };
            }
        }

        private string SerializeValue(object val)
        {
            if (val == null) return "null";
            if (val is PdfRef r) return $"{r.ObjectNumber} {r.Generation} R";
            if (val is List<object> arr)
            {
                var sb = new StringBuilder("[");
                foreach (var item in arr) sb.Append($" {SerializeValue(item)}");
                sb.Append(" ]");
                return sb.ToString();
            }
            if (val is Dictionary<string, object> dict)
            {
                var sb = new StringBuilder("<<");
                foreach (var kv in dict) sb.Append($" /{kv.Key} {SerializeValue(kv.Value)}");
                sb.Append(" >>");
                return sb.ToString();
            }
            if (val is string s) return $"/{s}";
            if (val is bool b) return b ? "true" : "false";
            if (val is double d) return d.ToInvariant();
            if (val is long lv) return lv.ToString();
            return val.ToString();
        }

        // ─────────────────────────────────────────────────────
        // xrefセクションとtrailerの書き込み
        // ─────────────────────────────────────────────────────

        private void WriteXrefSection(MemoryStream ms, List<(int objNum, long offset)> objects)
        {
            objects.Sort((a, b) => a.objNum.CompareTo(b.objNum));
            WriteAscii(ms, "xref\n");

            int i = 0;
            while (i < objects.Count)
            {
                int startObj = objects[i].objNum;
                int j = i;
                while (j + 1 < objects.Count && objects[j + 1].objNum == objects[j].objNum + 1)
                    j++;

                int count = j - i + 1;
                WriteAscii(ms, $"{startObj} {count}\n");

                for (int k = i; k <= j; k++)
                    WriteAscii(ms, $"{objects[k].offset:D10} 00000 n \r\n");

                i = j + 1;
            }
        }

        private void WriteTrailer(MemoryStream ms, int newSize, long xrefOffset)
        {
            WriteAscii(ms, "trailer\n");
            WriteAscii(ms, $"<< /Size {newSize}\n");
            WriteAscii(ms, $"   /Root {_ctx.RootRef.ObjectNumber} {_ctx.RootRef.Generation} R\n");
            WriteAscii(ms, $"   /Prev {_ctx.LatestXrefOffset}\n");
            WriteAscii(ms, ">>\n");
            WriteAscii(ms, $"startxref\n{xrefOffset}\n%%EOF\n");
        }

        // ─────────────────────────────────────────────────────
        // ユーティリティ
        // ─────────────────────────────────────────────────────

        private static void WriteAscii(MemoryStream ms, string text)
        {
            byte[] bytes = Encoding.GetEncoding(28591).GetBytes(text);
            ms.Write(bytes, 0, bytes.Length);
        }

        private static bool EndsWithEof(byte[] data)
        {
            int searchFrom = Math.Max(0, data.Length - 1024);
            for (int i = data.Length - 5; i >= searchFrom; i--)
            {
                if (data[i] == '%' && data[i + 1] == '%' &&
                    data[i + 2] == 'E' && data[i + 3] == 'O' && data[i + 4] == 'F')
                    return true;
            }
            return false;
        }

        private void ValidatePageNumber(int pageNumber)
        {
            if (pageNumber < 1 || pageNumber > _ctx.Pages.Count)
                throw new ArgumentOutOfRangeException(nameof(pageNumber),
                    $"ページ番号は1〜{_ctx.Pages.Count}の範囲で指定してください。実際の値: {pageNumber}");
        }
    }

    internal static class DoubleExtensions
    {
        internal static string ToInvariant(this double value)
            => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
