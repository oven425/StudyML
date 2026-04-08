// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.ML.OnnxRuntimeGenAI
{
    public class GeneratorParams1 : IDisposable
    {
        private IntPtr _generatorParamsHandle;
        private bool _disposed = false;
        public GeneratorParams1(Model1 model)
        {
            Result1.VerifySuccess(NativeMethods.OgaCreateGeneratorParams(model.Handle, out _generatorParamsHandle));
        }

        internal IntPtr Handle { get { return _generatorParamsHandle; } }

        public void SetSearchOption(string searchOption, double value)
        {
            Result1.VerifySuccess(NativeMethods.OgaGeneratorParamsSetSearchNumber(_generatorParamsHandle, StringUtils.ToUtf8(searchOption), value));
        }

        public void SetSearchOption(string searchOption, bool value)
        {
            Result1.VerifySuccess(NativeMethods.OgaGeneratorParamsSetSearchBool(_generatorParamsHandle, StringUtils.ToUtf8(searchOption), value));
        }

        public void SetGuidance(string type, string data, bool enableFFTokens = false)
        {
            Result1.VerifySuccess(NativeMethods.OgaGeneratorParamsSetGuidance(_generatorParamsHandle, StringUtils.ToUtf8(type), StringUtils.ToUtf8(data), enableFFTokens));
        }

        public double GetSearchNumber(string searchOption)
        {
            Result1.VerifySuccess(NativeMethods.OgaGeneratorParamsGetSearchNumber(_generatorParamsHandle, StringUtils.ToUtf8(searchOption), out double value));
            return value;
        }

        public bool GetSearchBool(string searchOption)
        {
            Result1.VerifySuccess(NativeMethods.OgaGeneratorParamsGetSearchBool(_generatorParamsHandle, StringUtils.ToUtf8(searchOption), out bool value));
            return value;
        }

        ~GeneratorParams1()
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
            NativeMethods.OgaDestroyGeneratorParams(_generatorParamsHandle);
            _generatorParamsHandle = IntPtr.Zero;
            _disposed = true;
        }
    }
}
