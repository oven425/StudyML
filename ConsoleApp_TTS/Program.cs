using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Runtime.InteropServices;
using System.Text;
using ConsoleApp_TTS;
Console.OutputEncoding = System.Text.Encoding.UTF8;

const int SampleRate = 24000; // Kokoro 輸出音訊的取樣率



string text = "烏薩奇是超人氣日本角色《吉伊卡哇》中的熱門角色，外觀為亮黃色兔子。她個性活潑好動、調皮且自由奔放，總是發出「呀哈」、「嗚啦」等獨特叫聲，同時擁有極強的戰鬥力與「除草檢定5級」證照，是充滿反差萌的開心";



var voicepath = "../../../../tts/kokoro/zf_xiaobei.bin";
// voice bin 檔的形狀是 [max_len, 1, 256]，要依照 token 數量取對應那一列的 style 向量
var num = NumSharp.np.Load<float[,,]>(System.IO.Path.GetFullPath(voicepath));

var modelpath = "../../../../tts/kokoro/model.onnx";
using InferenceSession session = new InferenceSession(modelpath);

// 1) 純 C# 將中文轉成 IPA 音素字串（ToolGood.Words 拼音 + 自實作的聲母/韻母對照表）
string phonemes = ChinesePinyinIpa.TextToIpa(text);
Console.WriteLine($"音素: {phonemes}");

// 2) 依 Kokoro 詞彙表 (config.json 的 vocab) 把每個音素字元轉成 token id，未收錄的字元直接跳過
long[] phonemeIds = phonemes
    .Where(KokoroVocab.Map.ContainsKey)
    .Select(c => (long)KokoroVocab.Map[c])
    .ToArray();

// 3) 前後補 0 當作 pad/BOS/EOS，最長不能超過 510 個音素 (加 pad 後 512)
if (phonemeIds.Length > 510)
{
    phonemeIds = phonemeIds[..510];
}
long[] tokenIds = new long[phonemeIds.Length + 2];
tokenIds[0] = 0;
Array.Copy(phonemeIds, 0, tokenIds, 1, phonemeIds.Length);
tokenIds[^1] = 0;

var tokensTensor = new DenseTensor<long>(tokenIds, new[] { 1, tokenIds.Length });

// 依「補 pad 前」的音素數量取出對應的 style 向量那一列（對齊官方 Python 範例的 voices[len(tokens)]）
int styleRow = Math.Min(phonemeIds.Length, num.GetLength(0) - 1);
var styleValues = new float[256];
for (int c = 0; c < 256; c++)
{
    styleValues[c] = num[styleRow, 0, c];
}
var styleTensor = new DenseTensor<float>(styleValues, new[] { 1, 256 });

var speedTensor = new DenseTensor<float>(new float[] { 1.0f }, new[] { 1 });

var inputs = new List<NamedOnnxValue>
{
    NamedOnnxValue.CreateFromTensor("input_ids", tokensTensor),
    NamedOnnxValue.CreateFromTensor("style", styleTensor),
    NamedOnnxValue.CreateFromTensor("speed", speedTensor),
};

using var results = session.Run(inputs);
var audioTensor = results.First().AsEnumerable<float>().ToArray();

var outputPath = Path.Combine(AppContext.BaseDirectory, "output.wav");
WriteWav(outputPath, audioTensor, SampleRate);
Console.WriteLine($"已輸出音檔: {outputPath}");

Console.ReadLine();

static void WriteWav(string path, float[] samples, int sampleRate)
{
    using var fs = new FileStream(path, FileMode.Create);
    using var writer = new BinaryWriter(fs);

    short bitsPerSample = 16;
    short channels = 1;
    int byteRate = sampleRate * channels * (bitsPerSample / 8);
    int dataSize = samples.Length * (bitsPerSample / 8);

    writer.Write(Encoding.ASCII.GetBytes("RIFF"));
    writer.Write(36 + dataSize);
    writer.Write(Encoding.ASCII.GetBytes("WAVE"));

    writer.Write(Encoding.ASCII.GetBytes("fmt "));
    writer.Write(16); // fmt chunk size
    writer.Write((short)1); // PCM
    writer.Write(channels);
    writer.Write(sampleRate);
    writer.Write(byteRate);
    writer.Write((short)(channels * (bitsPerSample / 8)));
    writer.Write(bitsPerSample);

    writer.Write(Encoding.ASCII.GetBytes("data"));
    writer.Write(dataSize);
    foreach (var sample in samples)
    {
        short pcm = (short)(Math.Clamp(sample, -1f, 1f) * short.MaxValue);
        writer.Write(pcm);
    }
}

// Kokoro-82M config.json 內的 vocab 表：IPA 音素字元 -> token id
// 來源: https://huggingface.co/hexgrad/Kokoro-82M/blob/main/config.json
static class KokoroVocab
{
    public static readonly Dictionary<char, int> Map = new()
    {
        [';'] = 1, [':'] = 2, [','] = 3, ['.'] = 4, ['!'] = 5, ['?'] = 6,
        ['—'] = 9, ['…'] = 10, ['"'] = 11, ['('] = 12, [')'] = 13,
        ['“'] = 14, ['”'] = 15, [' '] = 16, ['\u0303'] = 17,
        ['ʣ'] = 18, ['ʥ'] = 19, ['ʦ'] = 20, ['ʨ'] = 21, ['ᵝ'] = 22, ['\uAB67'] = 23,
        ['A'] = 24, ['I'] = 25, ['O'] = 31, ['Q'] = 33, ['S'] = 35, ['T'] = 36,
        ['W'] = 39, ['Y'] = 41, ['ᵊ'] = 42,
        ['a'] = 43, ['b'] = 44, ['c'] = 45, ['d'] = 46, ['e'] = 47, ['f'] = 48,
        ['h'] = 50, ['i'] = 51, ['j'] = 52, ['k'] = 53, ['l'] = 54, ['m'] = 55,
        ['n'] = 56, ['o'] = 57, ['p'] = 58, ['q'] = 59, ['r'] = 60, ['s'] = 61,
        ['t'] = 62, ['u'] = 63, ['v'] = 64, ['w'] = 65, ['x'] = 66, ['y'] = 67, ['z'] = 68,
        ['ɑ'] = 69, ['ɐ'] = 70, ['ɒ'] = 71, ['æ'] = 72, ['β'] = 75, ['ɔ'] = 76,
        ['ɕ'] = 77, ['ç'] = 78, ['ɖ'] = 80, ['ð'] = 81, ['ʤ'] = 82, ['ə'] = 83,
        ['ɚ'] = 85, ['ɛ'] = 86, ['ɜ'] = 87, ['ɟ'] = 90, ['ɡ'] = 92, ['ɥ'] = 99,
        ['ɨ'] = 101, ['ɪ'] = 102, ['ʝ'] = 103, ['ɯ'] = 110, ['ɰ'] = 111, ['ŋ'] = 112,
        ['ɳ'] = 113, ['ɲ'] = 114, ['ɴ'] = 115, ['ø'] = 116, ['ɸ'] = 118, ['θ'] = 119,
        ['œ'] = 120, ['ɹ'] = 123, ['ɾ'] = 125, ['ɻ'] = 126, ['ʁ'] = 128, ['ɽ'] = 129,
        ['ʂ'] = 130, ['ʃ'] = 131, ['ʈ'] = 132, ['ʧ'] = 133, ['ʊ'] = 135, ['ʋ'] = 136,
        ['ʌ'] = 138, ['ɣ'] = 139, ['ɤ'] = 140, ['χ'] = 142, ['ʎ'] = 143, ['ʒ'] = 147,
        ['ʔ'] = 148, ['ˈ'] = 156, ['ˌ'] = 157, ['ː'] = 158, ['ʰ'] = 162, ['ʲ'] = 164,
        ['↓'] = 169, ['→'] = 171, ['↗'] = 172, ['↘'] = 173, ['ᵻ'] = 177,
    };
}