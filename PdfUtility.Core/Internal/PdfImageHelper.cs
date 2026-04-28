using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using PdfUtility.Core.Drawing;
using PdfUtility.Core.Exceptions;

namespace PdfUtility.Core.Internal
{
    /// <summary>解析済み画像情報。PDF XObject の生成に使用する。</summary>
    internal class ParsedImage
    {
        internal int Width { get; set; }
        internal int Height { get; set; }
        internal int BitsPerComponent { get; set; } = 8;
        internal string ColorSpace { get; set; } = "/DeviceRGB";
        /// <summary>null の場合フィルターなし（生ピクセル）。</summary>
        internal string Filter { get; set; }
        internal byte[] ImageData { get; set; }
    }

    /// <summary>ページに配置する画像XObjectのリソース情報。</summary>
    internal class ImageResource
    {
        internal string XObjectName { get; set; }   // "Im0" 等
        internal int ObjectNumber { get; set; }
        internal ParsedImage ParsedImage { get; set; }
        internal ImageDrawCommand Command { get; set; }
    }

    /// <summary>
    /// JPEG・PNGバイト列を解析して PDF Image XObject 用データを生成する。
    ///
    /// JPEG: SOFマーカーから幅・高さ・チャンネル数を取得。ImageDataはJPEGバイト列をそのまま使用。
    /// PNG:  IHDRから幅・高さ・色タイプを取得。IDATを展開→逆フィルター→zlib再圧縮。
    ///       対応色タイプ: 0=Grayscale, 2=RGB, 3=Indexed, 4=GrayAlpha, 6=RGBA
    ///       アルファチャンネルは無視（ドロップ）する。
    /// </summary>
    internal static class PdfImageHelper
    {
        internal static ParsedImage Parse(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length < 4)
                throw new PdfRenderException("画像データが無効です。");

            // JPEG: FF D8
            if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8)
                return ParseJpeg(imageBytes);

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 &&
                imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
                return ParsePng(imageBytes);

            throw new PdfRenderException("未対応の画像形式です。JPEG(.jpg)またはPNG(.png)を使用してください。");
        }

        // ── JPEG ─────────────────────────────────────────────────────────

        private static ParsedImage ParseJpeg(byte[] data)
        {
            int width = 0, height = 0, components = 3;

            int i = 2; // SOI の直後から
            while (i + 1 < data.Length)
            {
                if (data[i] != 0xFF) break;
                byte marker = data[i + 1];
                i += 2;

                // SOF マーカー: C0–CF（C4=DHT, C8, CC は除く）
                if (marker >= 0xC0 && marker <= 0xCF &&
                    marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                {
                    // セグメント: length(2), precision(1), height(2), width(2), components(1)
                    if (i + 7 < data.Length)
                    {
                        height     = (data[i + 3] << 8) | data[i + 4];
                        width      = (data[i + 5] << 8) | data[i + 6];
                        components = data[i + 7];
                    }
                    break;
                }

                // セグメント長をスキップ（FFxx + length(2バイト, length自身を含む)）
                if (i + 1 >= data.Length) break;
                int segLen = (data[i] << 8) | data[i + 1];
                if (segLen < 2) break;
                i += segLen;
            }

            if (width == 0 || height == 0)
                throw new PdfRenderException("JPEGの幅・高さを解析できませんでした。");

            string colorSpace;
            switch (components)
            {
                case 1:  colorSpace = "/DeviceGray"; break;
                case 4:  colorSpace = "/DeviceCMYK"; break;
                default: colorSpace = "/DeviceRGB";  break;
            }

            return new ParsedImage
            {
                Width = width,
                Height = height,
                BitsPerComponent = 8,
                ColorSpace = colorSpace,
                Filter = "/DCTDecode",
                ImageData = data   // JPEGはバイト列をそのまま埋め込む
            };
        }

        // ── PNG ──────────────────────────────────────────────────────────

        private static ParsedImage ParsePng(byte[] data)
        {
            // PNG シグネチャ(8バイト)をスキップ
            int pos = 8;
            int width = 0, height = 0, bitDepth = 8, colorType = 2;
            byte[] palette = null;
            var idatChunks = new List<byte[]>();

            while (pos + 12 <= data.Length)
            {
                int chunkLen = ReadInt32BE(data, pos); pos += 4;
                if (pos + 4 > data.Length) break;
                string chunkType = System.Text.Encoding.ASCII.GetString(data, pos, 4); pos += 4;

                if (chunkType == "IHDR" && chunkLen >= 13)
                {
                    width     = ReadInt32BE(data, pos);
                    height    = ReadInt32BE(data, pos + 4);
                    bitDepth  = data[pos + 8];
                    colorType = data[pos + 9];
                }
                else if (chunkType == "PLTE" && chunkLen > 0)
                {
                    palette = new byte[chunkLen];
                    Array.Copy(data, pos, palette, 0, chunkLen);
                }
                else if (chunkType == "IDAT" && chunkLen > 0)
                {
                    var chunk = new byte[chunkLen];
                    Array.Copy(data, pos, chunk, 0, chunkLen);
                    idatChunks.Add(chunk);
                }
                else if (chunkType == "IEND") break;

                pos += chunkLen + 4; // データ + CRC
            }

            if (width == 0 || height == 0 || idatChunks.Count == 0)
                throw new PdfRenderException("PNG解析に失敗しました（IHDR/IDATが見つかりません）。");

            // チャンネル数を決定
            int channels;
            switch (colorType)
            {
                case 0: channels = 1; break;  // Grayscale
                case 2: channels = 3; break;  // RGB
                case 3: channels = 1; break;  // Indexed
                case 4: channels = 2; break;  // Grayscale+Alpha
                case 6: channels = 4; break;  // RGBA
                default: channels = 3; break;
            }

            int bytesPerSample = (bitDepth + 7) / 8;
            int stride = width * channels * bytesPerSample;

            // IDAT を結合して zlib展開
            byte[] compressedIdat = ConcatenateArrays(idatChunks);
            byte[] decompressed = DecompressZlib(compressedIdat);

            // 逆PNGフィルターを適用してピクセルを復元
            byte[] pixels = ReconstructScanlines(decompressed, width, height, channels, bytesPerSample, stride);

            // 色タイプ別の後処理
            string colorSpace;
            byte[] finalPixels;

            if (colorType == 3 && palette != null)
            {
                // Indexed: パレット展開
                finalPixels = ExpandPalette(pixels, palette, width, height);
                colorSpace = "/DeviceRGB";
            }
            else if (colorType == 4)
            {
                // GrayAlpha: アルファ除去
                finalPixels = StripAlpha(pixels, width, height, 1, bytesPerSample);
                colorSpace = "/DeviceGray";
            }
            else if (colorType == 6)
            {
                // RGBA: アルファ除去
                finalPixels = StripAlpha(pixels, width, height, 3, bytesPerSample);
                colorSpace = "/DeviceRGB";
            }
            else if (colorType == 0)
            {
                finalPixels = pixels;
                colorSpace = "/DeviceGray";
            }
            else
            {
                // RGB (type 2)
                finalPixels = pixels;
                colorSpace = "/DeviceRGB";
            }

            // PDF FlateDecode 用に zlib 圧縮
            byte[] zlibData = CompressZlib(finalPixels);

            return new ParsedImage
            {
                Width = width,
                Height = height,
                BitsPerComponent = bitDepth > 8 ? 8 : bitDepth,
                ColorSpace = colorSpace,
                Filter = "/FlateDecode",
                ImageData = zlibData
            };
        }

        private static byte[] ReconstructScanlines(
            byte[] data, int width, int height, int channels, int bytesPerSample, int stride)
        {
            int bytesPerPixel = channels * bytesPerSample;
            var result = new byte[stride * height];
            int inPos = 0;

            for (int row = 0; row < height; row++)
            {
                if (inPos >= data.Length) break;
                byte filterType = data[inPos++];
                int outBase = row * stride;

                for (int col = 0; col < stride; col++)
                {
                    if (inPos >= data.Length) break;
                    byte x = data[inPos++];

                    byte a = col >= bytesPerPixel ? result[outBase + col - bytesPerPixel] : (byte)0;
                    byte b = row > 0 ? result[(row - 1) * stride + col] : (byte)0;
                    byte c = row > 0 && col >= bytesPerPixel
                        ? result[(row - 1) * stride + col - bytesPerPixel]
                        : (byte)0;

                    byte recon;
                    switch (filterType)
                    {
                        case 0: recon = x; break;                                      // None
                        case 1: recon = (byte)(x + a); break;                          // Sub
                        case 2: recon = (byte)(x + b); break;                          // Up
                        case 3: recon = (byte)(x + ((a + b) >> 1)); break;             // Average
                        case 4: recon = (byte)(x + PaethPredictor(a, b, c)); break;    // Paeth
                        default: recon = x; break;
                    }
                    result[outBase + col] = recon;
                }
            }

            return result;
        }

        private static byte PaethPredictor(byte a, byte b, byte c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a);
            int pb = Math.Abs(p - b);
            int pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc) return a;
            if (pb <= pc) return b;
            return c;
        }

        private static byte[] StripAlpha(byte[] pixels, int width, int height, int colorChannels, int bytesPerSample)
        {
            var result = new byte[width * height * colorChannels * bytesPerSample];
            int inPos = 0, outPos = 0;
            int totalChannels = colorChannels + 1; // +alpha

            for (int i = 0; i < width * height; i++)
            {
                for (int c = 0; c < colorChannels; c++)
                    for (int b = 0; b < bytesPerSample; b++)
                        result[outPos++] = pixels[inPos++];
                inPos += bytesPerSample; // アルファをスキップ
            }

            return result;
        }

        private static byte[] ExpandPalette(byte[] indexData, byte[] palette, int width, int height)
        {
            var result = new byte[width * height * 3];
            for (int i = 0; i < width * height; i++)
            {
                int idx = indexData[i] * 3;
                result[i * 3]     = idx     < palette.Length ? palette[idx]     : (byte)0;
                result[i * 3 + 1] = idx + 1 < palette.Length ? palette[idx + 1] : (byte)0;
                result[i * 3 + 2] = idx + 2 < palette.Length ? palette[idx + 2] : (byte)0;
            }
            return result;
        }

        private static byte[] DecompressZlib(byte[] data)
        {
            // zlibヘッダ2バイトをスキップしてDeflate展開
            using (var ms = new MemoryStream(data, 2, data.Length - 2))
            using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
            using (var result = new MemoryStream())
            {
                deflate.CopyTo(result);
                return result.ToArray();
            }
        }

        /// <summary>生ピクセルデータを zlib 圧縮する（PDF FlateDecode 用）。</summary>
        internal static byte[] CompressZlib(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                // zlib ヘッダ: CMF=0x78, FLG=0x9C（Deflate, default compression, checksum）
                ms.WriteByte(0x78);
                ms.WriteByte(0x9C);

                using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                    deflate.Write(data, 0, data.Length);

                // Adler-32 チェックサム（ビッグエンディアン）
                uint adler = ComputeAdler32(data);
                ms.WriteByte((byte)(adler >> 24));
                ms.WriteByte((byte)(adler >> 16));
                ms.WriteByte((byte)(adler >> 8));
                ms.WriteByte((byte)adler);

                return ms.ToArray();
            }
        }

        private static uint ComputeAdler32(byte[] data)
        {
            const uint MOD = 65521;
            uint a = 1, b = 0;
            foreach (byte bt in data)
            {
                a = (a + bt) % MOD;
                b = (b + a) % MOD;
            }
            return (b << 16) | a;
        }

        private static byte[] ConcatenateArrays(List<byte[]> arrays)
        {
            int total = 0;
            foreach (var arr in arrays) total += arr.Length;
            var result = new byte[total];
            int pos = 0;
            foreach (var arr in arrays) { Array.Copy(arr, 0, result, pos, arr.Length); pos += arr.Length; }
            return result;
        }

        private static int ReadInt32BE(byte[] data, int pos)
            => (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
    }
}
