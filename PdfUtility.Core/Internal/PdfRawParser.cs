using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using PdfUtility.Core.Documents;
using PdfUtility.Core.Exceptions;

namespace PdfUtility.Core.Internal
{
    /// <summary>
    /// 既存PDFのバイト列を解析し、xrefテーブル・トレイラー・ページツリーを取得する内部クラス。
    /// インクリメンタルアップデートに必要な情報を収集することが目的。
    /// xrefテーブル形式（旧来）とxrefストリーム形式（PDF 1.5以降）の両方に対応。
    /// </summary>
    internal class PdfRawParser
    {
        private readonly byte[] _data;
        private int _pos;

        internal PdfRawParser(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _pos = 0;
        }

        // ─────────────────────────────────────────────────────
        // 公開エントリポイント
        // ─────────────────────────────────────────────────────

        internal PdfDocumentContext Parse()
        {
            ValidatePdfHeader();

            long startXrefOffset = FindStartXref();

            // xrefチェーンを辿って全エントリを収集（複数の増分更新がある場合）
            var allEntries = new Dictionary<int, XrefEntry>();
            Dictionary<string, object> latestTrailer = null;
            long currentOffset = startXrefOffset;

            while (currentOffset >= 0)
            {
                _pos = (int)currentOffset;
                SkipWhitespace();

                var (entries, trailer, prevOffset) = ParseXrefAtCurrentPos();

                if (latestTrailer == null)
                    latestTrailer = trailer;

                // 新しいエントリを優先（後ろのチェーンは古い情報なので上書きしない）
                foreach (var kv in entries)
                {
                    if (!allEntries.ContainsKey(kv.Key))
                        allEntries[kv.Key] = kv.Value;
                }

                currentOffset = prevOffset;
            }

            if (latestTrailer == null)
                throw new PdfLoadException("トレイラーが見つかりませんでした。");

            int totalSize = GetDictInt(latestTrailer, "Size");
            PdfRef rootRef = GetDictRef(latestTrailer, "Root");

            List<InternalPageInfo> pages = ParsePagesTree(allEntries, rootRef);

            return new PdfDocumentContext
            {
                OriginalBytes = _data,
                LatestXrefOffset = startXrefOffset,
                XrefEntries = allEntries,
                TotalObjectCount = totalSize,
                RootRef = rootRef,
                Pages = pages
            };
        }

        // ─────────────────────────────────────────────────────
        // ヘッダ検証
        // ─────────────────────────────────────────────────────

        private void ValidatePdfHeader()
        {
            if (_data.Length < 5)
                throw new PdfLoadException("ファイルが小さすぎます。PDFではありません。");

            if (_data[0] != '%' || _data[1] != 'P' || _data[2] != 'D' ||
                _data[3] != 'F' || _data[4] != '-')
                throw new PdfLoadException("PDFヘッダが無効です。%PDF-で始まっていません。");
        }

        internal string ReadPdfVersion()
        {
            // "%PDF-X.Y" の X.Y を返す
            if (_data.Length < 8) return "unknown";
            int start = 5;
            int end = start;
            while (end < _data.Length && _data[end] != '\r' && _data[end] != '\n')
                end++;
            return Encoding.ASCII.GetString(_data, start, end - start).Trim();
        }

        // ─────────────────────────────────────────────────────
        // startxref の検索
        // ─────────────────────────────────────────────────────

        private long FindStartXref()
        {
            // ファイル末尾から最大2048バイトをスキャン
            int searchLen = Math.Min(2048, _data.Length);
            int searchFrom = _data.Length - searchLen;

            byte[] target = Encoding.ASCII.GetBytes("startxref");
            int lastFoundAt = -1;

            for (int i = _data.Length - target.Length; i >= searchFrom; i--)
            {
                bool match = true;
                for (int j = 0; j < target.Length; j++)
                {
                    if (_data[i + j] != target[j]) { match = false; break; }
                }
                if (match) { lastFoundAt = i; break; }
            }

            if (lastFoundAt < 0)
                throw new PdfLoadException("startxrefキーワードが見つかりません。PDFが破損しています。");

            _pos = lastFoundAt + target.Length;
            SkipWhitespace();
            return ReadLong();
        }

        // ─────────────────────────────────────────────────────
        // xref（テーブルまたはストリーム）の解析
        // ─────────────────────────────────────────────────────

        private (Dictionary<int, XrefEntry> entries, Dictionary<string, object> trailer, long prevOffset)
            ParseXrefAtCurrentPos()
        {
            // 現在位置の先頭トークンを確認
            int savedPos = _pos;
            string firstToken = PeekToken();

            if (firstToken == "xref")
            {
                return ParseTraditionalXref();
            }
            else
            {
                // xrefストリーム（"N G obj" 形式）
                return ParseXrefStream();
            }
        }

        // ────── 従来型 xrefテーブル ──────

        private (Dictionary<int, XrefEntry> entries, Dictionary<string, object> trailer, long prevOffset)
            ParseTraditionalXref()
        {
            ReadToken(); // "xref" を消費

            var entries = new Dictionary<int, XrefEntry>();

            while (true)
            {
                SkipWhitespace();
                string tok = PeekToken();
                if (tok == "trailer" || string.IsNullOrEmpty(tok)) break;

                int firstObjNum = int.Parse(ReadToken());
                int count = int.Parse(ReadToken());
                SkipToEndOfLine();

                for (int i = 0; i < count; i++)
                {
                    // 各エントリ: "nnnnnnnnnn ggggg n \r\n" または "\n" (20 or 19 bytes)
                    string line = ReadXrefEntryLine();
                    if (line.Length < 18) continue;

                    long entryOffset = long.Parse(line.Substring(0, 10));
                    int gen = int.Parse(line.Substring(11, 5));
                    char inUse = line[17];

                    entries[firstObjNum + i] = new XrefEntry
                    {
                        Offset = entryOffset,
                        Generation = gen,
                        InUse = inUse == 'n'
                    };
                }
            }

            ReadToken(); // "trailer" を消費
            SkipWhitespace();
            var trailerDict = ReadDictionary();

            long prevOffset = -1;
            if (trailerDict.ContainsKey("Prev"))
                prevOffset = Convert.ToInt64(trailerDict["Prev"]);

            return (entries, trailerDict, prevOffset);
        }

        // ────── xrefストリーム（PDF 1.5以降） ──────

        private (Dictionary<int, XrefEntry> entries, Dictionary<string, object> trailer, long prevOffset)
            ParseXrefStream()
        {
            // "objNum gen obj" を読み込む
            SkipWhitespace();
            int objNum = int.Parse(ReadToken());
            int gen = int.Parse(ReadToken());
            string objKw = ReadToken(); // "obj"

            SkipWhitespace();
            var dict = ReadDictionary();

            // ストリームデータを読み込む
            SkipWhitespace();
            string streamKw = ReadToken(); // "stream"
            if (streamKw != "stream")
                throw new PdfLoadException($"xrefストリームでstreamキーワードが見つかりません。");

            // stream の直後の改行をスキップ（\r\n または \n）
            if (_pos < _data.Length && _data[_pos] == '\r') _pos++;
            if (_pos < _data.Length && _data[_pos] == '\n') _pos++;

            int streamLength = GetDictInt(dict, "Length");
            byte[] rawStream = new byte[streamLength];
            Array.Copy(_data, _pos, rawStream, 0, streamLength);

            // フィルター適用（FlateDecode のみ対応）
            byte[] streamData = ApplyFilters(rawStream, dict);

            // /W でエントリのフィールド幅を取得
            int[] w = GetDictIntArray(dict, "W");
            if (w == null || w.Length < 3)
                throw new PdfLoadException("xrefストリームの/Wが無効です。");

            // /Index でオブジェクト番号範囲を取得（省略時は [0 Size]）
            int[] index = GetDictIntArray(dict, "Index");
            if (index == null)
            {
                int size = GetDictInt(dict, "Size");
                index = new int[] { 0, size };
            }

            var entries = new Dictionary<int, XrefEntry>();
            int entrySize = w[0] + w[1] + w[2];
            int dataPos = 0;

            for (int seg = 0; seg < index.Length; seg += 2)
            {
                int startObjNum = index[seg];
                int segCount = index[seg + 1];

                for (int i = 0; i < segCount; i++)
                {
                    if (dataPos + entrySize > streamData.Length) break;

                    int type = ReadBigEndianInt(streamData, dataPos, w[0]);
                    long field2 = ReadBigEndianLong(streamData, dataPos + w[0], w[1]);
                    int field3 = ReadBigEndianInt(streamData, dataPos + w[0] + w[1], w[2]);
                    dataPos += entrySize;

                    int currentObjNum = startObjNum + i;

                    switch (type)
                    {
                        case 0: // free
                            entries[currentObjNum] = new XrefEntry { InUse = false, Generation = field3 };
                            break;
                        case 1: // 通常オブジェクト
                            entries[currentObjNum] = new XrefEntry
                            {
                                Offset = field2,
                                Generation = field3,
                                InUse = true
                            };
                            break;
                        case 2: // オブジェクトストリーム内
                            entries[currentObjNum] = new XrefEntry
                            {
                                InUse = true,
                                IsCompressed = true,
                                StreamObjNum = (int)field2,
                                IndexInStream = field3
                            };
                            break;
                    }
                }
            }

            long prevOffset = -1;
            if (dict.ContainsKey("Prev"))
                prevOffset = Convert.ToInt64(dict["Prev"]);

            return (entries, dict, prevOffset);
        }

        // FlateDecode（zlib）展開
        private byte[] ApplyFilters(byte[] data, Dictionary<string, object> dict)
        {
            if (!dict.ContainsKey("Filter"))
                return data;

            string filter = dict["Filter"]?.ToString();
            if (filter == "FlateDecode" || filter == "Fl")
            {
                return InflateZlib(data);
            }
            // 他のフィルター（ASCIIHexDecode等）は Phase 1 では未対応
            return data;
        }

        private byte[] InflateZlib(byte[] data)
        {
            // zlibヘッダ（2バイト）をスキップしてDeflate展開
            if (data.Length < 3)
                throw new PdfLoadException("FlateDecode: データが短すぎます。");

            using (var ms = new MemoryStream(data, 2, data.Length - 2))
            using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
            using (var result = new MemoryStream())
            {
                deflate.CopyTo(result);
                return result.ToArray();
            }
        }

        // ─────────────────────────────────────────────────────
        // ページツリー解析
        // ─────────────────────────────────────────────────────

        private List<InternalPageInfo> ParsePagesTree(Dictionary<int, XrefEntry> xrefs, PdfRef rootRef)
        {
            // Catalog → /Pages → Pages/Kids を辿る
            var catalogDict = ReadObjectDict(xrefs, rootRef.ObjectNumber);
            if (catalogDict == null)
                throw new PdfLoadException("DocumentCatalog(/Root)が読み込めません。");

            PdfRef pagesRef = GetDictRef(catalogDict, "Pages");
            var pages = new List<InternalPageInfo>();
            CollectPages(xrefs, pagesRef, null, pages);
            return pages;
        }

        private void CollectPages(
            Dictionary<int, XrefEntry> xrefs,
            PdfRef nodeRef,
            double[] inheritedMediaBox,
            List<InternalPageInfo> result)
        {
            var nodeDict = ReadObjectDict(xrefs, nodeRef.ObjectNumber);
            if (nodeDict == null) return;

            string type = nodeDict.ContainsKey("Type") ? nodeDict["Type"]?.ToString() : null;

            double[] mediaBox = GetMediaBox(nodeDict) ?? inheritedMediaBox;

            if (type == "Pages")
            {
                // 中間ノード：/Kids を辿る
                var kids = nodeDict.ContainsKey("Kids") ? nodeDict["Kids"] as List<object> : null;
                if (kids == null) return;

                foreach (var kid in kids)
                {
                    if (kid is PdfRef kidRef)
                        CollectPages(xrefs, kidRef, mediaBox, result);
                }
            }
            else if (type == "Page")
            {
                // 葉ノード：ページ情報を収集
                object existingContents = null;
                if (nodeDict.ContainsKey("Contents"))
                    existingContents = nodeDict["Contents"];

                result.Add(new InternalPageInfo
                {
                    PageNumber = result.Count + 1,
                    ObjectNumber = nodeRef.ObjectNumber,
                    Generation = nodeRef.Generation,
                    MediaBox = mediaBox ?? new double[] { 0, 0, 595.28, 841.89 },
                    ExistingContents = existingContents
                });
            }
        }

        private double[] GetMediaBox(Dictionary<string, object> dict)
        {
            if (!dict.ContainsKey("MediaBox")) return null;
            var arr = dict["MediaBox"] as List<object>;
            if (arr == null || arr.Count < 4) return null;

            var box = new double[4];
            for (int i = 0; i < 4; i++)
                box[i] = Convert.ToDouble(arr[i]);
            return box;
        }

        // ─────────────────────────────────────────────────────
        // オブジェクト読み込み
        // ─────────────────────────────────────────────────────

        internal Dictionary<string, object> ReadObjectDict(Dictionary<int, XrefEntry> xrefs, int objNum)
        {
            if (!xrefs.TryGetValue(objNum, out XrefEntry entry)) return null;
            if (!entry.InUse) return null;

            if (entry.IsCompressed)
            {
                return ReadCompressedObjectDict(xrefs, entry.StreamObjNum, entry.IndexInStream);
            }

            _pos = (int)entry.Offset;
            SkipWhitespace();

            // "N G obj" をスキップ
            ReadToken(); // obj番号
            ReadToken(); // gen番号
            ReadToken(); // "obj"
            SkipWhitespace();

            if (_pos < _data.Length && _data[_pos] == '<' && _pos + 1 < _data.Length && _data[_pos + 1] == '<')
                return ReadDictionary();

            return null;
        }

        private Dictionary<string, object> ReadCompressedObjectDict(
            Dictionary<int, XrefEntry> xrefs, int streamObjNum, int indexInStream)
        {
            // オブジェクトストリームを展開して中のオブジェクトを取得
            if (!xrefs.TryGetValue(streamObjNum, out XrefEntry streamEntry)) return null;

            _pos = (int)streamEntry.Offset;
            SkipWhitespace();
            ReadToken(); // obj番号
            ReadToken(); // gen番号
            ReadToken(); // "obj"
            SkipWhitespace();

            var streamDict = ReadDictionary();
            SkipWhitespace();
            ReadToken(); // "stream"
            if (_pos < _data.Length && _data[_pos] == '\r') _pos++;
            if (_pos < _data.Length && _data[_pos] == '\n') _pos++;

            int length = GetDictInt(streamDict, "Length");
            byte[] rawData = new byte[length];
            Array.Copy(_data, _pos, rawData, 0, length);
            byte[] streamData = ApplyFilters(rawData, streamDict);

            // オブジェクトストリームの先頭はオフセットテーブル
            int n = GetDictInt(streamDict, "N");
            int first = GetDictInt(streamDict, "First");

            string header = Encoding.GetEncoding(28591).GetString(streamData, 0, first);
            string[] tokens = header.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (indexInStream * 2 + 1 >= tokens.Length) return null;

            int objOffset = first + int.Parse(tokens[indexInStream * 2 + 1]);

            var innerParser = new PdfRawParser(streamData);
            innerParser._pos = objOffset;
            innerParser.SkipWhitespace();

            if (innerParser._pos < streamData.Length &&
                streamData[innerParser._pos] == '<' &&
                innerParser._pos + 1 < streamData.Length &&
                streamData[innerParser._pos + 1] == '<')
            {
                return innerParser.ReadDictionary();
            }

            return null;
        }

        // ─────────────────────────────────────────────────────
        // PDF辞書・値のパーサー
        // ─────────────────────────────────────────────────────

        internal Dictionary<string, object> ReadDictionary()
        {
            // "<<" を消費
            Expect('<'); Expect('<');
            var dict = new Dictionary<string, object>();

            while (true)
            {
                SkipWhitespace();
                if (_pos >= _data.Length) break;

                // ">>" で終端
                if (_data[_pos] == '>' && _pos + 1 < _data.Length && _data[_pos + 1] == '>')
                {
                    _pos += 2;
                    break;
                }

                // キー（/Name）
                if (_data[_pos] != '/') break; // 予期しない文字
                _pos++; // '/' をスキップ
                string key = ReadName();

                SkipWhitespace();
                object value = ReadValue();
                dict[key] = value;
            }

            return dict;
        }

        private object ReadValue()
        {
            SkipWhitespace();
            if (_pos >= _data.Length) return null;

            byte b = _data[_pos];

            if (b == '<')
            {
                if (_pos + 1 < _data.Length && _data[_pos + 1] == '<')
                    return ReadDictionary();
                else
                    return ReadHexString();
            }
            else if (b == '(')
            {
                return ReadLiteralString();
            }
            else if (b == '[')
            {
                return ReadArray();
            }
            else if (b == '/')
            {
                _pos++;
                return ReadName();
            }
            else if (b == 't' || b == 'f')
            {
                return ReadBoolOrToken();
            }
            else if (b == 'n')
            {
                // null
                if (MatchKeyword("null")) return null;
                return ReadToken();
            }
            else
            {
                // 数値 or 間接参照 (X Y R)
                return ReadNumericOrRef();
            }
        }

        private List<object> ReadArray()
        {
            Expect('[');
            var list = new List<object>();

            while (true)
            {
                SkipWhitespace();
                if (_pos >= _data.Length) break;
                if (_data[_pos] == ']') { _pos++; break; }
                list.Add(ReadValue());
            }

            return list;
        }

        private string ReadName()
        {
            var sb = new StringBuilder();
            while (_pos < _data.Length)
            {
                byte b = _data[_pos];
                if (IsWhitespace(b) || IsDelimiter(b)) break;
                if (b == '#')
                {
                    // 16進数エスケープ (#XX)
                    _pos++;
                    if (_pos + 1 < _data.Length)
                    {
                        char hi = (char)_data[_pos++];
                        char lo = (char)_data[_pos++];
                        sb.Append((char)Convert.ToByte(new string(new[] { hi, lo }), 16));
                    }
                }
                else
                {
                    sb.Append((char)b);
                    _pos++;
                }
            }
            return sb.ToString();
        }

        private string ReadLiteralString()
        {
            Expect('(');
            var sb = new StringBuilder();
            int depth = 1;

            while (_pos < _data.Length && depth > 0)
            {
                byte b = _data[_pos++];
                if (b == '\\')
                {
                    if (_pos >= _data.Length) break;
                    byte esc = _data[_pos++];
                    switch ((char)esc)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '(': sb.Append('('); break;
                        case ')': sb.Append(')'); break;
                        case '\\': sb.Append('\\'); break;
                        default: sb.Append((char)esc); break;
                    }
                }
                else if (b == '(') { depth++; sb.Append('('); }
                else if (b == ')') { depth--; if (depth > 0) sb.Append(')'); }
                else sb.Append((char)b);
            }

            return sb.ToString();
        }

        private string ReadHexString()
        {
            Expect('<');
            var sb = new StringBuilder();
            while (_pos < _data.Length && _data[_pos] != '>')
            {
                if (!IsWhitespace(_data[_pos]))
                    sb.Append((char)_data[_pos]);
                _pos++;
            }
            if (_pos < _data.Length) _pos++; // '>' をスキップ

            string hex = sb.ToString();
            if (hex.Length % 2 != 0) hex += "0";
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return Encoding.GetEncoding(28591).GetString(bytes);
        }

        private object ReadNumericOrRef()
        {
            int savedPos = _pos;

            // 最初の数値
            string tok1 = ReadToken();
            if (!IsNumeric(tok1))
            {
                _pos = savedPos;
                return ReadToken();
            }

            int savedPos2 = _pos;
            SkipWhitespace();

            // 2番目のトークンが数値なら間接参照の可能性
            if (_pos < _data.Length && (char.IsDigit((char)_data[_pos]) || _data[_pos] == '-'))
            {
                string tok2 = ReadToken();
                if (IsNonNegativeInt(tok2))
                {
                    int savedPos3 = _pos;
                    SkipWhitespace();
                    if (_pos < _data.Length && _data[_pos] == 'R')
                    {
                        _pos++;
                        // 間接参照
                        return new PdfRef
                        {
                            ObjectNumber = int.Parse(tok1),
                            Generation = int.Parse(tok2)
                        };
                    }
                    _pos = savedPos3;
                }
                _pos = savedPos2;
            }

            // 単純な数値
            if (tok1.Contains("."))
                return double.Parse(tok1, System.Globalization.CultureInfo.InvariantCulture);
            if (long.TryParse(tok1, out long lv))
                return lv > int.MaxValue || lv < int.MinValue ? (object)lv : (int)lv;
            return tok1;
        }

        private object ReadBoolOrToken()
        {
            if (MatchKeyword("true")) return true;
            if (MatchKeyword("false")) return false;
            return ReadToken();
        }

        private bool MatchKeyword(string keyword)
        {
            for (int i = 0; i < keyword.Length; i++)
            {
                if (_pos + i >= _data.Length || _data[_pos + i] != (byte)keyword[i])
                    return false;
            }
            // キーワードの次は非アルファベットであること
            int next = _pos + keyword.Length;
            if (next < _data.Length && !IsWhitespace(_data[next]) && !IsDelimiter(_data[next]))
                return false;
            _pos += keyword.Length;
            return true;
        }

        // ─────────────────────────────────────────────────────
        // 低レベルトークン読み取り
        // ─────────────────────────────────────────────────────

        private string PeekToken()
        {
            int saved = _pos;
            string tok = ReadToken();
            _pos = saved;
            return tok;
        }

        private string ReadToken()
        {
            SkipWhitespace();
            if (_pos >= _data.Length) return string.Empty;

            var sb = new StringBuilder();
            byte b = _data[_pos];

            // デリミタは単体でトークン（<< >> は2文字）
            if (b == '<' && _pos + 1 < _data.Length && _data[_pos + 1] == '<') { _pos += 2; return "<<"; }
            if (b == '>' && _pos + 1 < _data.Length && _data[_pos + 1] == '>') { _pos += 2; return ">>"; }
            if (IsDelimiter(b) && b != '<' && b != '>') { _pos++; return ((char)b).ToString(); }

            while (_pos < _data.Length && !IsWhitespace(_data[_pos]) && !IsDelimiter(_data[_pos]))
            {
                sb.Append((char)_data[_pos]);
                _pos++;
            }

            return sb.ToString();
        }

        private long ReadLong()
        {
            string tok = ReadToken();
            if (long.TryParse(tok, out long v)) return v;
            throw new PdfLoadException($"数値として解析できません: '{tok}'");
        }

        private string ReadXrefEntryLine()
        {
            // xrefエントリ行：20バイト固定（実際はCRLFまたはLFで終端）
            var sb = new StringBuilder();
            while (_pos < _data.Length && _data[_pos] != '\r' && _data[_pos] != '\n')
            {
                sb.Append((char)_data[_pos]);
                _pos++;
            }
            // 改行スキップ（\r\n または \n）
            if (_pos < _data.Length && _data[_pos] == '\r') _pos++;
            if (_pos < _data.Length && _data[_pos] == '\n') _pos++;
            return sb.ToString();
        }

        private void SkipToEndOfLine()
        {
            while (_pos < _data.Length && _data[_pos] != '\r' && _data[_pos] != '\n')
                _pos++;
            if (_pos < _data.Length && _data[_pos] == '\r') _pos++;
            if (_pos < _data.Length && _data[_pos] == '\n') _pos++;
        }

        private void SkipWhitespace()
        {
            while (_pos < _data.Length)
            {
                byte b = _data[_pos];
                if (b == '%')
                {
                    // コメント：行末までスキップ
                    while (_pos < _data.Length && _data[_pos] != '\n' && _data[_pos] != '\r')
                        _pos++;
                    continue;
                }
                if (!IsWhitespace(b)) break;
                _pos++;
            }
        }

        private void Expect(char c)
        {
            if (_pos >= _data.Length || _data[_pos] != (byte)c)
                throw new PdfLoadException($"文字 '{c}' が期待されますが、位置 {_pos} に '{(char)_data[_pos]}' があります。");
            _pos++;
        }

        // ─────────────────────────────────────────────────────
        // ユーティリティ
        // ─────────────────────────────────────────────────────

        private static bool IsWhitespace(byte b) =>
            b == 0 || b == 9 || b == 10 || b == 12 || b == 13 || b == 32;

        private static bool IsDelimiter(byte b) =>
            b == '(' || b == ')' || b == '<' || b == '>' ||
            b == '[' || b == ']' || b == '{' || b == '}' ||
            b == '/' || b == '%';

        private static bool IsNumeric(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            int start = s[0] == '-' || s[0] == '+' ? 1 : 0;
            bool hasDot = false;
            for (int i = start; i < s.Length; i++)
            {
                if (s[i] == '.' && !hasDot) { hasDot = true; continue; }
                if (!char.IsDigit(s[i])) return false;
            }
            return s.Length > start;
        }

        private static bool IsNonNegativeInt(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) if (!char.IsDigit(c)) return false;
            return true;
        }

        private static int ReadBigEndianInt(byte[] data, int offset, int width)
        {
            int val = 0;
            for (int i = 0; i < width; i++)
                val = (val << 8) | data[offset + i];
            return val;
        }

        private static long ReadBigEndianLong(byte[] data, int offset, int width)
        {
            long val = 0;
            for (int i = 0; i < width; i++)
                val = (val << 8) | data[offset + i];
            return val;
        }

        // ─────────────────────────────────────────────────────
        // 辞書ヘルパー
        // ─────────────────────────────────────────────────────

        private int GetDictInt(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out object val)) return 0;
            return Convert.ToInt32(val);
        }

        private PdfRef GetDictRef(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out object val))
                throw new PdfLoadException($"トレイラーに /{key} が見つかりません。");
            if (val is PdfRef r) return r;
            throw new PdfLoadException($"/{key} は間接参照ではありません: {val}");
        }

        private int[] GetDictIntArray(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out object val)) return null;
            if (val is List<object> list)
            {
                var arr = new int[list.Count];
                for (int i = 0; i < list.Count; i++)
                    arr[i] = Convert.ToInt32(list[i]);
                return arr;
            }
            return null;
        }
    }
}
