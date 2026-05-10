using System;

namespace PdfUtility.Core.Options
{
    /// <summary>
    /// PdfUtility 全体の動作オプション。
    /// </summary>
    public class PdfUtilityOptions
    {
        /// <summary>
        /// オープンパスワード付きの暗号化PDFを拒否するかどうか。
        /// true の場合、読み込み時に PdfLoadException をスローする。
        /// 既定値: true（パスワード保護PDFは復号できないため）。
        /// </summary>
        public bool RejectEncryptedPdf { get; set; } = true;

        /// <summary>
        /// パーミッション設定（編集禁止・変更禁止など）が掛かっているPDFを拒否するかどうか。
        /// true の場合、読み込み時に PdfLoadException をスローする。
        /// false の場合、警告ログを出力したうえで処理を続行する。
        /// 既定値: true。
        /// </summary>
        public bool RejectPermissionLockedPdf { get; set; } = true;

        /// <summary>
        /// 警告メッセージのコールバック。null の場合は System.Diagnostics.Debug に書き出す。
        /// RejectPermissionLockedPdf=false でパーミッション設定PDFを読み込む際に呼ばれる。
        /// </summary>
        public Action<string> WarningLogger { get; set; }

        internal void EmitWarning(string message)
        {
            if (WarningLogger != null)
                WarningLogger(message);
            else
                System.Diagnostics.Debug.WriteLine("[PdfUtility WARN] " + message);
        }
    }
}
