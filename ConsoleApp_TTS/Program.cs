using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Runtime.InteropServices;

var voicepath = "../../../../tts/kokoro/zf_xiaobei.bin";
var voicebytes = File.ReadAllBytes(voicepath);
var num = NumSharp.np.Load<float[,,]>(System.IO.Path.GetFullPath(voicepath));
var floatSpan = MemoryMarshal.Cast<byte, float>(voicebytes);
var styleDimensions = new int[] { 1, 256 };
var styleTensor = new DenseTensor<float>(new Memory<float>(floatSpan.ToArray()), styleDimensions);

var modelpath = "../../../../tts/kokoro/model.onnx";
InferenceSession session = new InferenceSession(modelpath);
string text = "烏薩奇是超人氣日本角色《吉伊卡哇》中的熱門角色，外觀為亮黃色兔子。她個性活潑好動、調皮且自由奔放，總是發出「呀哈」、「嗚啦」等獨特叫聲，同時擁有極強的戰鬥力與「除草檢定5級」證照，是充滿反差萌的開心";

Console.ReadLine();