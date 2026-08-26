QSoft.Audio.WasapiLoopbackDriver loopback = new();
using CancellationTokenSource cts = new();

Console.WriteLine("按 Enter 停止錄音...");
Task captureTask = Task.Run(() => loopback.StartCapture("loopback.wav", cts.Token));
Console.ReadLine();
cts.Cancel();
captureTask.GetAwaiter().GetResult();
Console.WriteLine("錄音已停止，檔案：loopback.wav");