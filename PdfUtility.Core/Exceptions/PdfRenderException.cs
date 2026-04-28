using System;

namespace PdfUtility.Core.Exceptions
{
    /// <summary>
    /// PDF描画命令の変換・レンダリング時に発生する例外。
    /// </summary>
    public class PdfRenderException : PdfException
    {
        public PdfRenderException(string message) : base(message) { }
        public PdfRenderException(string message, Exception inner) : base(message, inner) { }
    }
}
