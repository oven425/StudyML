// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.ML.OnnxRuntimeGenAI
{
    public class TokenizerStream1 : IDisposable
    {
        private IntPtr _tokenizerStreamHandle;
        private bool _disposed = false;

        internal TokenizerStream1(IntPtr tokenizerStreamHandle)
        {
            _tokenizerStreamHandle = tokenizerStreamHandle;
        }

        internal IntPtr Handle { get { return _tokenizerStreamHandle; } }

        public string Decode(int token)
        {
            IntPtr decodedStr = IntPtr.Zero;
            Result1.VerifySuccess(NativeMethods.OgaTokenizerStreamDecode(_tokenizerStreamHandle, token, out decodedStr));
            return StringUtils.FromUtf8(decodedStr);
        }

        ~TokenizerStream1()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            NativeMethods.OgaDestroyTokenizerStream(_tokenizerStreamHandle);
            _tokenizerStreamHandle = IntPtr.Zero;
            _disposed = true;
        }
    }
}
