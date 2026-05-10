using System;
using System.IO;
using PdfUtility.Core.Exceptions;
using PdfUtility.Core.Internal;
using PdfUtility.Core.Options;

namespace PdfUtility.Core.Services
{
    /// <summary>
    /// PDF バイト列を読み込み、暗号化・パーミッション設定をオプションに照らして検証するサービス。
    /// 検証に失敗した場合は <see cref="PdfLoadException"/> をスローする。
    /// </summary>
    public class PdfReaderService
    {
        private readonly PdfUtilityOptions _options;

        public PdfReaderService() : this(null) { }

        public PdfReaderService(PdfUtilityOptions options)
        {
            _options = options ?? new PdfUtilityOptions();
        }

        /// <summary>
        /// ファイルパスを指定して PDF を読み込み、検証済みのバイト列を返す。
        /// </summary>
        public byte[] Read(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("PDFファイルパスが空です。", nameof(path));
            if (!File.Exists(path))
                throw new PdfLoadException($"PDFファイルが見つかりません: {path}");

            byte[] bytes = File.ReadAllBytes(path);
            Validate(bytes);
            return bytes;
        }

        /// <summary>
        /// バイト列を検証する。例外が発生しなければ読み込み可能。
        /// </summary>
        public void Validate(byte[] pdfBytes)
        {
            if (pdfBytes == null) throw new ArgumentNullException(nameof(pdfBytes));
            if (pdfBytes.Length == 0) throw new PdfLoadException("PDFバイト列が空です。");

            var parser = new PdfRawParser(pdfBytes);
            var ctx = parser.Parse();
            ApplyEncryptionPolicy(ctx.Encryption);
        }

        /// <summary>
        /// 既にパース済みの暗号化情報に対してオプションに基づくポリシーを適用する。
        /// PdfIncrementalWriter からも利用される共通エントリポイント。
        /// </summary>
        internal void ApplyEncryptionPolicy(PdfEncryptionInfo encryption)
        {
            if (encryption == null) return; // 暗号化なし

            // (1) オープンパスワード必須 → パスワード保護PDF
            if (!encryption.IsOpenableWithoutPassword)
            {
                if (_options.RejectEncryptedPdf)
                    throw new PdfLoadException("パスワード保護されたPDFは対応していません");
                _options.EmitWarning("パスワード保護されたPDFを検出しましたが、RejectEncryptedPdf=false のため処理を続行します。");
                return;
            }

            // (2) パーミッション設定（編集禁止・変更禁止）
            if (encryption.HasEditRestrictions)
            {
                if (_options.RejectPermissionLockedPdf)
                    throw new PdfLoadException("編集が禁止されているPDFは対応していません");
                _options.EmitWarning("編集禁止フラグが設定されたPDFを検出しましたが、RejectPermissionLockedPdf=false のため処理を続行します。");
            }
        }
    }
}
