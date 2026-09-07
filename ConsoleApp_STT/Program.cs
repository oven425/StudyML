using ConsoleApp_STT;
using Microsoft.Extensions.AI;
using QSoft.Audio;
using SherpaOnnx;
using System.Runtime.Intrinsics.X86;
using System.Threading.Channels;
using Windows.Media.MediaProperties;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Channel<byte[]> m_AudioPCM16 = Channel.CreateUnbounded<byte[]>();
Channel<string> m_EnglishSentences = Channel.CreateUnbounded<string>(
    new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = true,
        AllowSynchronousContinuations = false
    });

var converter = new Pcm16Mono16kConverter();
var loopback = new WasapiLoopbackDriver();
var ccc = AudioEncodingProperties.CreatePcm(16000, 1, 16);
WAVEFORMATEX wavefmt = new()
{
    nChannels =1,
    nSamplesPerSec = 16000,
    nBlockAlign = 8,
    wFormatTag = 1,
    wBitsPerSample = 16
};
var wav = await WinRtWavWriter.CreateAsync("pcm16.wav", wavefmt);
void OnReceiveData(in WAVEFORMATEXTENSIBLE format, byte[] raw, int length)
{
    var pcm16k = converter.Convert(in format, raw.AsSpan(0, length));
    if (pcm16k is null || pcm16k.Length == 0)
        return;
    m_AudioPCM16.Writer.WriteAsync(pcm16k);

}

loopback.OnReceiveData += OnReceiveData;
CancellationTokenSource cts = new();



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


var task_capture = Task.Run(() =>
{
    loopback.Capture(cts.Token);
});

var gemmaModelPath = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory,
    "../../../../gguf_gemma4/gemma-4-E2B-it-Q4_K_M.gguf"));

if (!File.Exists(gemmaModelPath))
    throw new FileNotFoundException("找不到 Gemma 4 模型。", gemmaModelPath);

var options = new ChatOptions
{
    Instructions = """
    You are a professional English-to-Traditional-Chinese translator.
    Translate the user's English speech into natural Traditional Chinese.
    Output only the translation.
    Do not explain, summarize, or add extra text.
    """
};
var translationTask = Task.Run(async () =>
{
    using var translator = new QSoft.GGUF.Gemma4(gemmaModelPath);

    await foreach (var english in m_EnglishSentences.Reader.ReadAllAsync())
    {
        var response = await translator.GetResponseAsync(
        [
            new ChatMessage(ChatRole.User,english)
        ], options);

        var translated = response.Text.Trim();
        if (!string.IsNullOrWhiteSpace(translated))
            Console.WriteLine($"\n[中文] {translated}");
    }
});

var stopTask = Task.Run(Console.ReadLine);
var lastText = string.Empty;

try
{
    while (!cts.IsCancellationRequested)
    {
        while (m_AudioPCM16.Reader.TryRead(out var oo))
        {
            var ff = STT_WavStream.ConvertPcm16ToFloat(oo);
            stream.AcceptWaveform(Pcm16Mono16kConverter.TargetSampleRate, ff);

            while (recognizer.IsReady(stream))
                recognizer.Decode(stream);

            var text = recognizer.GetResult(stream).Text;
            if (!string.IsNullOrWhiteSpace(text) && text != lastText)
            {
                var write = text.AsSpan()[lastText.Length..];
                Console.Write(write);
                lastText = text;
            }

            if (recognizer.IsEndpoint(stream))
            {
                Console.WriteLine();
                if (!string.IsNullOrWhiteSpace(text))
                    await m_EnglishSentences.Writer.WriteAsync(text);

                recognizer.Reset(stream);
                lastText = string.Empty;
            }
        }

        if (stopTask.IsCompleted)
            break;

        var waitForAudio = m_AudioPCM16.Reader.WaitToReadAsync(cts.Token).AsTask();
        var completedTask = await Task.WhenAny(waitForAudio, stopTask, task_capture);

        if (completedTask == stopTask || completedTask == task_capture)
            break;

        await waitForAudio;
    }
}
finally
{
    await cts.CancelAsync();
    await task_capture;
    m_EnglishSentences.Writer.TryComplete();
    await translationTask;
    wav.Dispose();
}

//var resp = await STT_WavFile.Transform();
//Console.WriteLine(resp);

//var waveFilePath = args.Length > 0 ? args[0] : null;
//waveFilePath = "sheronnx.wav";
//await foreach (var oo in STT_WavStream.Transform(waveFilePath))
//{
//    Console.Write(oo);
//}


//using SherpaOnnx;
//using Windows.Media.MediaProperties;
//using Windows.Media.Transcoding;
//using Windows.Storage;
////test audio link
////https://www.youtube.com/watch?v=wSZeUoywCn0
//var lookback_aduiofile = "loopback.wav";
//var sherronxfile = "sheronnx.wav";
////don't remove
//QSoft.Audio.WasapiLoopbackDriver loopback = new();
//using CancellationTokenSource cts = new();
//Console.WriteLine("按 Enter 停止錄音...");
//Task captureTask = Task.Run(() => loopback.StartCapture(lookback_aduiofile, cts.Token));
//Console.ReadLine();
//cts.Cancel();
//await captureTask;
//Console.WriteLine($"錄音已停止，檔案：{lookback_aduiofile}");


//var exefolder = AppContext.BaseDirectory;
//var inputfileanme = System.IO.Path.Combine(exefolder, lookback_aduiofile);
//var output = await StorageFolder.GetFolderFromPathAsync(AppContext.BaseDirectory);
//var outputfile = await output.CreateFileAsync(sherronxfile, CreationCollisionOption.ReplaceExisting);

//var input = await StorageFile.GetFileFromPathAsync(inputfileanme);
//MediaTranscoder tr = new() { HardwareAccelerationEnabled = true };

//var prepare = await tr.PrepareFileTranscodeAsync(input, outputfile, new Windows.Media.MediaProperties.MediaEncodingProfile()
//{
//    Container = new ContainerEncodingProperties()
//    {
//        Subtype = MediaEncodingSubtypes.Wave
//    },
//    Audio = AudioEncodingProperties.CreatePcm(16000, 1, 16),
//});
//if (prepare.CanTranscode)
//{
//    var progress = new Progress<double>(value =>
//    {
//        System.Diagnostics.Trace.WriteLine($"轉檔進度：{value:P0}");
//    });

//    await prepare.TranscodeAsync().AsTask(progress);
//}

//var modelDirectory = "../../../../SherpaOnnx";
////var audioPath = "test_wavs\\en.wav";

//var modelPath = Path.Combine(modelDirectory, "model.int8.onnx");
//var tokensPath = Path.Combine(modelDirectory, "tokens.txt");

//var recognizerConfig = new OfflineRecognizerConfig
//{
//    FeatConfig = { SampleRate = 16000, FeatureDim = 80 },
//    ModelConfig =
//    {
//        Tokens = tokensPath,
//        SenseVoice =
//        {
//            Model = modelPath,
//            UseInverseTextNormalization = 1
//        }
//    },
//    DecodingMethod = "greedy_search"
//};

//using var recognizer = new OfflineRecognizer(recognizerConfig);
//using var stream = recognizer.CreateStream();
//var wave = new WaveReader(sherronxfile);
//stream.AcceptWaveform(wave.SampleRate, wave.Samples);
//recognizer.Decode(stream);

//Console.WriteLine($"音訊：{sherronxfile}");
//Console.WriteLine($"辨識結果：{stream.Result.Text}");

