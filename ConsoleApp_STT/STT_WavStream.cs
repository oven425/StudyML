using QSoft.Audio;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleApp_STT
{
    internal class STT_WavStream
    {
        async public static IAsyncEnumerable<string> Transform()
        {
            var lookback_aduiofile = "loopback.wav";
            var sherronxfile = "sheronnx.wav";
            //don't remove
            QSoft.Audio.WasapiLoopbackDriver loopback = new();
            using CancellationTokenSource cts = new();
            Console.WriteLine("按 Enter 停止錄音...");
            var converter = new Pcm16Mono16kConverter();
            loopback.OnReceiveData += (in WAVEFORMATEXTENSIBLE fmt, byte[] raw, int len) =>
            {
                byte[] pcm16k = converter.Convert(
                    in fmt,
                    raw.AsSpan(0, len));
                        };
            var captureTask = Task.Run(() => loopback.Capture(cts.Token));

            
            Console.WriteLine($"錄音已停止，檔案：{lookback_aduiofile}");
            yield return "";

            Console.ReadLine();
            cts.Cancel();
            await captureTask;
        }
    }
}
