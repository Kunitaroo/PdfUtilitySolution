using System;

namespace PdfUtility.Core.Exceptions
{
    /// <summary>
    /// PdfUtility.Core で発生する例外の基底クラス。
    /// </summary>
    public class PdfException : Exception
    {
        public PdfException(string message) : base(message) { }
        public PdfException(string message, Exception inner) : base(message, inner) { }
    }
}
