// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ML.OnnxRuntimeGenAI
{
    public class Tokenizer1 : IDisposable
    {
        private IntPtr _tokenizerHandle;
        private bool _disposed = false;

        public Tokenizer1(Model1 model)
        {
            Result1.VerifySuccess(NativeMethods.OgaCreateTokenizer(model.Handle, out _tokenizerHandle));
        }

        public Sequences1 EncodeBatch(string[] strings)
        {
            Result1.VerifySuccess(NativeMethods.OgaCreateSequences(out IntPtr nativeSequences));
            try
            {
                foreach (string str in strings)
                {
                    Result1.VerifySuccess(NativeMethods.OgaTokenizerEncode(_tokenizerHandle, StringUtils.ToUtf8(str), nativeSequences));
                }

                return new Sequences1(nativeSequences);
            }
            catch
            {
                NativeMethods.OgaDestroySequences(nativeSequences);
                throw;
            }
        }

        public string[] DecodeBatch(Sequences sequences)
        {
            string[] result = new string[sequences.NumSequences];
            for (ulong i = 0; i < sequences.NumSequences; i++)
            {
                result[i] = Decode(sequences[i]);
            }

            return result;
        }

        public void UpdateOptions(Dictionary<string, string> options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            // Prepare native arrays
            string[] keys = new string[options.Count];
            string[] values = new string[options.Count];
            int i = 0;
            foreach (var kvp in options)
            {
                keys[i] = kvp.Key;
                values[i] = kvp.Value;
                i++;
            }

            // Call native function
            Result1.VerifySuccess(
                NativeMethods.OgaUpdateTokenizerOptions(
                    _tokenizerHandle,
                    keys,
                    values,
                    (UIntPtr)options.Count));
        }

        public Sequences1 Encode(string str)
        {
            Result1.VerifySuccess(NativeMethods.OgaCreateSequences(out IntPtr nativeSequences));
            try
            {
                Result1.VerifySuccess(NativeMethods.OgaTokenizerEncode(_tokenizerHandle, StringUtils.ToUtf8(str), nativeSequences));
                return new Sequences1(nativeSequences);
            }
            catch
            {
                NativeMethods.OgaDestroySequences(nativeSequences);
                throw;
            }
        }

        public string Decode(ReadOnlySpan<int> sequence)
        {
            IntPtr outStr = IntPtr.Zero;
            unsafe
            {
                fixed (int* sequencePtr = sequence)
                {
                    Result1.VerifySuccess(NativeMethods.OgaTokenizerDecode(_tokenizerHandle, sequencePtr, (UIntPtr)sequence.Length, out outStr));
                }
            }
            try
            {
                return StringUtils.FromUtf8(outStr);
            }
            finally
            {
                NativeMethods.OgaDestroyString(outStr);
            }
        }

        public string ApplyChatTemplate(string template_str, string messages, string tools, bool add_generation_prompt)
        {
            IntPtr outStr = IntPtr.Zero;
            try
            {
                Result1.VerifySuccess(NativeMethods.OgaTokenizerApplyChatTemplate(_tokenizerHandle, StringUtils.ToUtf8(template_str), StringUtils.ToUtf8(messages), StringUtils.ToUtf8(tools), add_generation_prompt, out outStr));
                return StringUtils.FromUtf8(outStr);
            }
            finally
            {
                NativeMethods.OgaDestroyString(outStr);
            }
        }

        public int GetBosTokenId()
        {
            Result1.VerifySuccess(NativeMethods.OgaTokenizerGetBosTokenId(_tokenizerHandle, out int bosTokenId));
            return bosTokenId;
        }

        public ReadOnlySpan<int> GetEosTokenIds()
        {
            Result1.VerifySuccess(NativeMethods.OgaTokenizerGetEosTokenIds(_tokenizerHandle, out IntPtr eosTokenIds, out UIntPtr tokenCount));
            unsafe
            {
                return new ReadOnlySpan<int>(eosTokenIds.ToPointer(), (int)tokenCount.ToUInt64());
            }
        }

        public int GetPadTokenId()
        {
            Result1.VerifySuccess(NativeMethods.OgaTokenizerGetPadTokenId(_tokenizerHandle, out int padTokenId));
            return padTokenId;
        }

        public TokenizerStream1 CreateStream()
        {
            IntPtr tokenizerStreamHandle = IntPtr.Zero;
            Result1.VerifySuccess(NativeMethods.OgaCreateTokenizerStream(_tokenizerHandle, out tokenizerStreamHandle));
            return new TokenizerStream1(tokenizerStreamHandle);
        }

        ~Tokenizer1()
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
            NativeMethods.OgaDestroyTokenizer(_tokenizerHandle);
            _tokenizerHandle = IntPtr.Zero;
            _disposed = true;
        }
    }
}
