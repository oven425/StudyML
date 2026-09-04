using QSoft.Audio;
using SherpaOnnx;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ConsoleApp_STT
{
    internal static class STT_WavStream
    {
        private const string OnlineModelDirectoryEnvironmentVariable = "SHERPA_ONNX_ONLINE_MODEL_DIR";

        public static async IAsyncEnumerable<string> Transform(
            string? waveFilePath = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var modelDirectory = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "../../../../SherpaOnnx/online"));
            var tokensPath = Path.Combine(modelDirectory, "tokens.txt");
            var encoderPath = Path.Combine(modelDirectory, "encoder-epoch-99-avg-1.int8.onnx");
            var decoderPath = Path.Combine(modelDirectory, "decoder-epoch-99-avg-1.int8.onnx");
            var joinerPath = Path.Combine(modelDirectory, "joiner-epoch-99-avg-1.int8.onnx");

            var recognizerConfig = new OnlineRecognizerConfig();
            recognizerConfig.FeatConfig.SampleRate = Pcm16Mono16kConverter.TargetSampleRate;
            recognizerConfig.FeatConfig.FeatureDim = 80;
            recognizerConfig.ModelConfig.Tokens = tokensPath;
            recognizerConfig.ModelConfig.Provider = "cpu";
            recognizerConfig.ModelConfig.NumThreads = 1;
            recognizerConfig.ModelConfig.Transducer.Encoder = encoderPath;
            recognizerConfig.ModelConfig.Transducer.Decoder = decoderPath;
            recognizerConfig.ModelConfig.Transducer.Joiner = joinerPath;
            recognizerConfig.DecodingMethod = "greedy_search";
            recognizerConfig.EnableEndpoint = 1;
            recognizerConfig.Rule1MinTrailingSilence = 2.4F;
            recognizerConfig.Rule2MinTrailingSilence = 0.8F;
            recognizerConfig.Rule3MinUtteranceLength = 20.0F;

            using var recognizer = new OnlineRecognizer(recognizerConfig);
            using var stream = recognizer.CreateStream();

            var lastText = string.Empty;
            var segmentIndex = 0;

            IEnumerable<string> DecodeSamples(float[] samples)
            {
                stream.AcceptWaveform(Pcm16Mono16kConverter.TargetSampleRate, samples);

                while (recognizer.IsReady(stream))
                    recognizer.Decode(stream);

                var text = recognizer.GetResult(stream).Text;
                if (!string.IsNullOrWhiteSpace(text) && text != lastText)
                {
                    lastText = text;
                    yield return $"\r{segmentIndex}: {text}";
                }

                if (recognizer.IsEndpoint(stream))
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        yield return Environment.NewLine;
                        segmentIndex++;
                    }

                    recognizer.Reset(stream);
                    lastText = string.Empty;
                }
            }

            IEnumerable<string> FinishStream()
            {
                foreach (var output in DecodeSamples(new float[
                    (int)(Pcm16Mono16kConverter.TargetSampleRate * 0.6F)]))
                {
                    yield return output;
                }

                stream.InputFinished();
                while (recognizer.IsReady(stream))
                    recognizer.Decode(stream);

                var finalText = recognizer.GetResult(stream).Text;
                if (!string.IsNullOrWhiteSpace(finalText))
                {
                    if (finalText != lastText)
                        yield return $"\r{segmentIndex}: {finalText}";

                    yield return Environment.NewLine;
                }
            }

            if (!string.IsNullOrWhiteSpace(waveFilePath))
            {
                var wavePath = Path.GetFullPath(waveFilePath);
                var wave = new WaveReader(wavePath);
                var chunkSize = Math.Max(1, wave.SampleRate / 10);

                Console.WriteLine($"使用 WAV 測試檔案：{wavePath}");

                foreach (var output in DecodeSamples(new float[(int)(wave.SampleRate * 0.3F)]))
                    yield return output;

                for (var offset = 0; offset < wave.Samples.Length; offset += chunkSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var length = Math.Min(chunkSize, wave.Samples.Length - offset);
                    var samples = new float[length];
                    Array.Copy(wave.Samples, offset, samples, 0, length);

                    foreach (var output in DecodeSamples(samples))
                        yield return output;
                }

                foreach (var output in FinishStream())
                    yield return output;

                yield break;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var audioChunks = Channel.CreateUnbounded<float[]>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            });
            var converter = new Pcm16Mono16kConverter();
            var loopback = new WasapiLoopbackDriver();

            void OnReceiveData(in WAVEFORMATEXTENSIBLE format, byte[] raw, int length)
            {
                var pcm16k = converter.Convert(in format, raw.AsSpan(0, length));
                if (pcm16k.Length == 0)
                    return;

                var samples = ConvertPcm16ToFloat(pcm16k);
                if (!audioChunks.Writer.TryWrite(samples) && !cts.IsCancellationRequested)
                    throw new InvalidOperationException("無法將音訊送入 STT 佇列。");
            }

            loopback.OnReceiveData += OnReceiveData;
            Task? captureTask = null;
            var captureTaskObserved = false;
            var stopTask = Task.Run(Console.ReadLine, CancellationToken.None);


            try
            {
                Console.WriteLine("線上 STT 已啟動，按 Enter 停止錄音...");
                captureTask = Task.Run(() => loopback.Capture(cts.Token), CancellationToken.None);

                while (!cts.IsCancellationRequested)
                {
                    while (audioChunks.Reader.TryRead(out var samples))
                    {
                        foreach (var output in DecodeSamples(samples))
                            yield return output;

                        if (stopTask.IsCompleted)
                            break;
                    }

                    if (stopTask.IsCompleted)
                    {
                        await stopTask;
                        break;
                    }

                    var waitForAudio = audioChunks.Reader.WaitToReadAsync(cts.Token).AsTask();
                    var completedTask = await Task.WhenAny(waitForAudio, stopTask, captureTask);
                    if (completedTask == stopTask)
                    {
                        await stopTask;
                        break;
                    }

                    if (completedTask == captureTask)
                    {
                        captureTaskObserved = true;
                        await captureTask;
                        break;
                    }

                    await waitForAudio;
                }

                cts.Cancel();
                captureTaskObserved = true;
                await captureTask;

                while (audioChunks.Reader.TryRead(out var samples))
                {
                    foreach (var output in DecodeSamples(samples))
                        yield return output;
                }

                foreach (var output in FinishStream())
                    yield return output;

                Console.WriteLine("錄音已停止。");
            }
            finally
            {
                cts.Cancel();
                try
                {
                    if (captureTask is not null && !captureTaskObserved)
                    {
                        captureTaskObserved = true;
                        await captureTask;
                    }
                }
                finally
                {
                    loopback.OnReceiveData -= OnReceiveData;
                    audioChunks.Writer.TryComplete();
                }
            }
        }

        public static float[] ConvertPcm16ToFloat(ReadOnlySpan<byte> pcm16)
        {
            if ((pcm16.Length & 1) != 0)
                throw new ArgumentException("PCM16 音訊資料長度必須是偶數。", nameof(pcm16));

            var samples = new float[pcm16.Length / sizeof(short)];
            for (var i = 0; i < samples.Length; i++)
            {
                var sample = BinaryPrimitives.ReadInt16LittleEndian(
                    pcm16.Slice(i * sizeof(short), sizeof(short)));
                samples[i] = sample / 32768F;
            }

            return samples;
        }
    }
}
