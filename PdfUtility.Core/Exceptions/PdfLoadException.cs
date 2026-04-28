using System;

namespace PdfUtility.Core.Exceptions
{
    /// <summary>
    /// PDFファイルの読み込み時に発生する例外。
    /// </summary>
    public class PdfLoadException : PdfException
    {
        public PdfLoadException(string message) : base(message) { }
        public PdfLoadException(string message, Exception inner) : base(message, inner) { }
    }
}
