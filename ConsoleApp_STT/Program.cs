using ConsoleApp_STT;
using QSoft.Audio;
using SherpaOnnx;
using System.Runtime.Intrinsics.X86;
using System.Threading.Channels;
using Windows.Media.MediaProperties;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Channel<byte[]> m_AudioPCM16 = Channel.CreateUnbounded<byte[]>();

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
    //wav.Write(pcm16k, pcm16k.Length);
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
var lastText = string.Empty;
await foreach (var oo in m_AudioPCM16.Reader.ReadAllAsync())
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
        recognizer.Reset(stream);
        lastText = string.Empty;
    }
}

Console.ReadLine();
await cts.CancelAsync();
await task_capture;
wav.Dispose();

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


