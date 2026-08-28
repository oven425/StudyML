using SherpaOnnx;

//var modelDirectory = "../../../../SherpaOnnx";
////var audioPath = "test_wavs\\en.wav";
//var audioPath = "loopback.wav";
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
//var wave = new WaveReader(audioPath);
//stream.AcceptWaveform(wave.SampleRate, wave.Samples);
//recognizer.Decode(stream);

//Console.WriteLine($"音訊：{audioPath}");
//Console.WriteLine($"辨識結果：{stream.Result.Text}");



//don't remove
QSoft.Audio.WasapiLoopbackDriver loopback = new();
using CancellationTokenSource cts = new();
Console.WriteLine("按 Enter 停止錄音...");
Task captureTask = Task.Run(() => loopback.StartCapture("loopback.wav", cts.Token));
Console.ReadLine();
cts.Cancel();
captureTask.GetAwaiter().GetResult();
Console.WriteLine("錄音已停止，檔案：loopback.wav");