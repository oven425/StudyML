// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.ML.OnnxRuntimeGenAI
{
    public class Model1 : IDisposable
    {
        private IntPtr _modelHandle;
        private bool _disposed = false;

        public Model1(string modelPath)
        {
            Result1.VerifySuccess(NativeMethods.OgaCreateModel(StringUtils.ToUtf8(modelPath), out _modelHandle));
        }

        public Model1(Config1 config)
        {
            Result1.VerifySuccess(NativeMethods.OgaCreateModelFromConfig(config.Handle, out _modelHandle));
        }

        internal IntPtr Handle { get { return _modelHandle; } }

        public string GetModelType()
        {
            IntPtr outStr = IntPtr.Zero;
            try
            {
                Result1.VerifySuccess(NativeMethods.OgaModelGetType(_modelHandle, out outStr));
                return StringUtils.FromUtf8(outStr);
            }
            finally
            {
                NativeMethods.OgaDestroyString(outStr);
            }
        }

        ~Model1()
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
            if (_modelHandle != IntPtr.Zero)
            {
                NativeMethods.OgaDestroyModel(_modelHandle);
                _modelHandle = IntPtr.Zero;
            }
            _disposed = true;
        }
    }
}
