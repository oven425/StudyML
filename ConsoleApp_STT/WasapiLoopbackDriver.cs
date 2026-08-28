using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.MediaProperties;
using Windows.Storage;

namespace QSoft.Audio;
public partial class WasapiLoopbackDriver
{
    private const uint AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
    private const uint AUDCLNT_SHAREMODE_SHARED = 0;
    private const uint CLSCTX_ALL = 23;

    public unsafe void StartCapture(string outputPath, CancellationToken cancelToken)
    {
        CoInitializeEx(IntPtr.Zero, 0x2); // COINIT_APARTMENTTHREADED

        try
        {
            Guid clsid = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
            Guid iidEnum = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
            int hr = CoCreateInstance(in clsid, IntPtr.Zero, CLSCTX_ALL, in iidEnum, out IntPtr pEnum);
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);

            var enumerator = ComInterfaceMarshaller<IMMDeviceEnumerator>.ConvertToManaged((void*)pEnum)!;
            hr = enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out IntPtr pDevice);
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
            var device = ComInterfaceMarshaller<IMMDevice>.ConvertToManaged((void*)pDevice)!;

            Guid audioClientIid = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
            hr = device.Activate(in audioClientIid, CLSCTX_ALL, IntPtr.Zero, out IntPtr pAudioClient);
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
            var audioClient = ComInterfaceMarshaller<IAudioClient>.ConvertToManaged((void*)pAudioClient)!;

            hr = audioClient.GetMixFormat(out IntPtr waveFormatPtr);
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
            var wavefx = Marshal.PtrToStructure<WAVEFORMATEX>(waveFormatPtr);
            
            try
            {
                using var wav = WinRtWavWriter.CreateAsync(outputPath, waveFormatPtr).GetAwaiter().GetResult();
                hr = audioClient.Initialize(AUDCLNT_SHAREMODE_SHARED, AUDCLNT_STREAMFLAGS_LOOPBACK, 10000000, 0, waveFormatPtr, IntPtr.Zero);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);

                Guid captureClientIid = new("C8ADBD64-E71E-48A0-A4DE-185C395CD317");
                hr = audioClient.GetService(in captureClientIid, out IntPtr pCaptureClient);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                var captureClient = ComInterfaceMarshaller<IAudioCaptureClient>.ConvertToManaged((void*)pCaptureClient)!;

                hr = audioClient.Start();
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);

                while (!cancelToken.IsCancellationRequested)
                {
                    Thread.Sleep(10);
                    hr = captureClient.GetBuffer(out IntPtr pData, out uint frames, out uint flags, out _, out _);
                    if (hr >= 0)
                    {
                        if (frames > 0)
                        {
                            wav.Write(pData, frames, flags);
                            hr = captureClient.ReleaseBuffer(frames);
                            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                        }
                    }
                    else
                    {
                        Marshal.ThrowExceptionForHR(hr);
                    }
                }

                hr = audioClient.Stop();
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                wav.Complete();
            }
            finally
            {
                Marshal.FreeCoTaskMem(waveFormatPtr);
            }
        }
        finally
        {
            CoUninitialize();
        }
    }

    #region Source-Generated P/Invoke ([LibraryImport])

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(
        in Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        out IntPtr ppv);

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    #endregion
}

#region Native AOT 相容的 Source-Generated COM 介面宣告

[GeneratedComInterface]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
public partial interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, uint dwStateMask, out IntPtr ppDevices);
    [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IntPtr endpoint);
}

[GeneratedComInterface]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
public partial interface IMMDevice
{
    [PreserveSig] int Activate(in Guid iid, uint dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);
}

record struct WAVEFORMATEX
{
    ushort wFormatTag;         /* format type */
    ushort nChannels;          /* number of channels (i.e. mono, stereo...) */
    uint nSamplesPerSec;     /* sample rate */
    uint nAvgBytesPerSec;    /* for buffer estimation */
    ushort nBlockAlign;        /* block size of data */
    ushort wBitsPerSample;     /* number of bits per sample of mono data */
    ushort cbSize;             /* the count in bytes of the size of */
    /* extra information (after cbSize) */
};

[GeneratedComInterface]
[Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2")]
public partial interface IAudioClient
{
    [PreserveSig] int Initialize(uint shareMode, uint streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, IntPtr audioSessionGuid);
    [PreserveSig] int GetBufferSize(out uint pNumBufferFrames);
    [PreserveSig] int GetStreamLatency(out long phnsLatency);
    [PreserveSig] int GetCurrentPadding(out uint pNumPaddingFrames);
    [PreserveSig] int IsFormatSupported(uint shareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
    [PreserveSig] int GetMixFormat(out IntPtr deviceFormat);
    [PreserveSig] int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
    [PreserveSig] int Start();
    [PreserveSig] int Stop();
    [PreserveSig] int Reset();
    [PreserveSig] int SetEventHandle(IntPtr eventHandle);
    [PreserveSig] int GetService(in Guid riid, out IntPtr ppv);
}

[GeneratedComInterface]
[Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317")]
public partial interface IAudioCaptureClient
{
    [PreserveSig] int GetBuffer(out IntPtr pData, out uint numFramesToRead, out uint dwFlags, out ulong devicePosition, out ulong qpcPosition);
    [PreserveSig] int ReleaseBuffer(uint numFramesRead);
    [PreserveSig] int GetNextPacketSize(out uint pNumFramesInNextPacket);
}

public enum EDataFlow { eRender, eCapture, eAll }
public enum ERole { eConsole, eMultimedia, eCommunications }

#endregion

internal sealed class WinRtWavWriter : IDisposable
{
    private const uint AudioClientBufferFlagsSilent = 0x2;
    private readonly IRandomAccessStream _stream;
    private readonly DataWriter _writer;
    private readonly uint _bytesPerFrame;
    private readonly int _formatSize;
    private ulong _dataBytes;
    private bool _completed;

    private WinRtWavWriter(IRandomAccessStream stream, byte[] header, uint bytesPerFrame, int formatSize)
    {
        _stream = stream;
        _writer = new DataWriter(stream);
        _bytesPerFrame = bytesPerFrame;
        _formatSize = formatSize;
        _writer.WriteBytes(header);
        Store();
    }

    public static async Task<WinRtWavWriter> CreateAsync(string outputPath, IntPtr formatPointer)
    {
        string fullPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (directory == null)
            throw new ArgumentException("Output path must contain a directory.", nameof(outputPath));

        StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(directory);
        StorageFile file = await folder.CreateFileAsync(Path.GetFileName(fullPath), CreationCollisionOption.ReplaceExisting);
        IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);

        ushort blockAlign = unchecked((ushort)Marshal.ReadInt16(formatPointer, 12));
        ushort extraSize = unchecked((ushort)Marshal.ReadInt16(formatPointer, 16));
        int formatSize = extraSize == 0 ? 16 : 18 + extraSize;
        byte[] format = new byte[formatSize];
        Marshal.Copy(formatPointer, format, 0, format.Length);

        byte[] header = new byte[28 + formatSize];
        "RIFF"u8.CopyTo(header.AsSpan(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), 0);
        "WAVE"u8.CopyTo(header.AsSpan(8, 4));
        "fmt "u8.CopyTo(header.AsSpan(12, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), (uint)formatSize);
        format.CopyTo(header, 20);
        "data"u8.CopyTo(header.AsSpan(20 + formatSize, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(24 + formatSize), 0);

        return new WinRtWavWriter(stream, header, blockAlign, formatSize);
    }

    public unsafe void Write(IntPtr data, uint frames, uint flags)
    {
        ulong byteCount = (ulong)frames * _bytesPerFrame;
        if (byteCount == 0) return;
        if (byteCount > int.MaxValue) throw new InvalidOperationException("Audio packet is too large.");

        byte[] bytes = new byte[(int)byteCount];
        if ((flags & AudioClientBufferFlagsSilent) == 0)
        {
            if (data == IntPtr.Zero) throw new InvalidOperationException("WASAPI returned a null audio buffer.");
            Marshal.Copy(data, bytes, 0, bytes.Length);
        }

        _writer.WriteBytes(bytes);
        Store();
        _dataBytes += byteCount;
    }

    public void Complete()
    {
        if (_completed) return;
        Store();
        _stream.Seek(4);
        _writer.WriteUInt32((uint)(36 + _dataBytes));
        Store();
        _stream.Seek(24 + (uint)_formatSize);
        _writer.WriteUInt32((uint)_dataBytes);
        Store();
        _completed = true;
    }

    private void Store()
    {
        _writer.StoreAsync().AsTask().GetAwaiter().GetResult();
        _writer.FlushAsync().AsTask().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (!_completed) Complete();
        _writer.Dispose();
        _stream.Dispose();
    }
}

public class NativeWinRTAudioRecorder
{
    private AudioGraph? _audioGraph;
    private AudioFileOutputNode? _fileOutputNode;
    private AudioFrameInputNode? _frameInputNode;
    private bool _isRecording;

    // 1. 初始化 WinRT AudioGraph 與 WAV 寫入節點
    public async Task InitializeAsync(StorageFile outputFile)
    {
        // 建立 AudioGraph 設定
        var graphSettings = new AudioGraphSettings(Windows.Media.Render.AudioRenderCategory.Media);
        var graphResult = await AudioGraph.CreateAsync(graphSettings);
        if (graphResult.Status != AudioGraphCreationStatus.Success)
            throw new Exception($"AudioGraph 建立失敗: {graphResult.Status}");

        _audioGraph = graphResult.Graph;

        // 使用 WinRT 原生 WAV 編碼設定
        MediaEncodingProfile wavProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Auto);

        // 建立檔案輸出節點
        var fileOutputResult = await _audioGraph.CreateFileOutputNodeAsync(outputFile, wavProfile);
        if (fileOutputResult.Status != AudioFileNodeCreationStatus.Success)
            throw new Exception($"WAV 輸出節點建立失敗: {fileOutputResult.Status}");

        _fileOutputNode = fileOutputResult.FileOutputNode;

        // 建立 Frame 輸入節點，作為 PCM 數據輸入介面
        _frameInputNode = _audioGraph.CreateFrameInputNode(_fileOutputNode.EncodingProperties);
        _frameInputNode.AddOutgoingConnection(_fileOutputNode);

        _audioGraph.Start();
    }

    // 2. 將 WASAPI 擷取到的原始 PCM 記憶體輸入至 WinRT 音訊管道
    public void PushAudioBuffer(byte[] buffer, uint frameCount)
    {
        if (!_isRecording || _frameInputNode == null) return;

        unsafe
        {
            // 將 Byte Array 封裝為 WinRT AudioFrame
            using var audioFrame = new AudioFrame((uint)buffer.Length);
            using var bufferAccess = audioFrame.LockBuffer(AudioBufferAccessMode.Write);
            using var reference = bufferAccess.CreateReference();

            ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* dataInBytes, out _);
            Marshal.Copy(buffer, 0, (IntPtr)dataInBytes, buffer.Length);

            _frameInputNode.AddFrame(audioFrame);
        }
    }

    public void Start() => _isRecording = true;

    // 3. 停止錄音並由 WinRT 自動完成 WAV 檔頭寫入
    public async Task StopAsync()
    {
        _isRecording = false;
        _audioGraph?.Stop();

        if (_fileOutputNode != null)
        {
            // 關鍵：WinRT 會在此時自動計算並修復 WAV 檔頭 (ChunkSize / Subchunk2Size)
            var status = await _fileOutputNode.FinalizeAsync();
        }

        _audioGraph?.Dispose();
    }
}
[GeneratedComInterface]
[Guid("5B0D3235-4DBA-4D41-8259-8C210632A3BD")]
 unsafe partial interface IMemoryBufferByteAccess
{
    void GetBuffer(out byte* buffer, out uint capacity);
}