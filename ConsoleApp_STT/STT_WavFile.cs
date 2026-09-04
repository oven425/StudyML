using QSoft.Audio;
using SherpaOnnx;
using System;
using System.Collections.Generic;
using System.Text;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace ConsoleApp_STT
{
    internal class STT_WavFile
    {
        async public static Task<string> Transform()
        {
            var lookback_aduiofile = "loopback.wav";
            var sherronxfile = "sheronnx.wav";
            //don't remove
            QSoft.Audio.WasapiLoopbackDriver loopback = new();
            using CancellationTokenSource cts = new();
            Console.WriteLine("按 Enter 停止錄音...");
            WinRtWavWriter? wav = null;
            //Task captureTask = Task.Run(() => loopback.StartCapture(lookback_aduiofile, cts.Token));
            loopback.OnReceiveData += (in WAVEFORMATEXTENSIBLE fmt, byte[] raw, int len) =>
                {
                    wav ??= WinRtWavWriter.CreateAsync(lookback_aduiofile, fmt).GetAwaiter().GetResult();
                    wav?.Write(raw, len);
                };
            var captureTask = Task.Run(() => loopback.Capture(cts.Token));

            Console.ReadLine();
            cts.Cancel();
            await captureTask;
            wav?.Dispose();
            Console.WriteLine($"錄音已停止，檔案：{lookback_aduiofile}");


            var exefolder = AppContext.BaseDirectory;
            var inputfileanme = System.IO.Path.Combine(exefolder, lookback_aduiofile);
            var output = await StorageFolder.GetFolderFromPathAsync(AppContext.BaseDirectory);
            var outputfile = await output.CreateFileAsync(sherronxfile, CreationCollisionOption.ReplaceExisting);

            var input = await StorageFile.GetFileFromPathAsync(inputfileanme);
            MediaTranscoder tr = new() { HardwareAccelerationEnabled = true };
            var prepare = await tr.PrepareFileTranscodeAsync(input, outputfile, new Windows.Media.MediaProperties.MediaEncodingProfile()
            {
                Container = new ContainerEncodingProperties()
                {
                    Subtype = MediaEncodingSubtypes.Wave
                },
                Audio = AudioEncodingProperties.CreatePcm(16000, 1, 16),
            });
            if (prepare.CanTranscode)
            {
                var progress = new Progress<double>(value =>
                {
                    System.Diagnostics.Trace.WriteLine($"轉檔進度：{value:P0}");
                });

                await prepare.TranscodeAsync().AsTask(progress);
            }

            var modelDirectory = "../../../../SherpaOnnx/offline";
            //var audioPath = "test_wavs\\en.wav";

            var modelPath = Path.Combine(modelDirectory, "model.int8.onnx");
            var tokensPath = Path.Combine(modelDirectory, "tokens.txt");

            var recognizerConfig = new OfflineRecognizerConfig
            {
                FeatConfig = { SampleRate = 16000, FeatureDim = 80 },
                ModelConfig =
                {
                    Tokens = tokensPath,
                    SenseVoice =
                    {
                        Model = modelPath,
                        UseInverseTextNormalization = 1
                    }
                },
                DecodingMethod = "greedy_search"
            };

            using var recognizer = new OfflineRecognizer(recognizerConfig);
            using var stream = recognizer.CreateStream();
            var wave = new WaveReader(sherronxfile);
            stream.AcceptWaveform(wave.SampleRate, wave.Samples);
            recognizer.Decode(stream);

            return stream.Result.Text;
        }
    }
}
