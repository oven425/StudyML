using LLama;
using LLama.Common;
using LLama.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticKernel_Llama
{
    public class image
    {
        public async Task Test()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            string modelPath = @"..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf"; // change it to your own model path.
            string mmprojPath = @"..\..\..\..\gguf_gemma4\mmproj-gemma-4-E2B-it-Q8_0.gguf"; // change it to your own mmproj path.
            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 8192,
                GpuLayerCount = 20,
            };

            using var model = await LLamaWeights.LoadFromFileAsync(parameters);

            var mtmdCtxParams = new MtmdContextParams();

            using var visionWeights = await MtmdWeights.LoadFromFileAsync(mmprojPath, model, mtmdCtxParams);


            using var context = model.CreateContext(parameters);

            var executor = new InteractiveExecutor(context, visionWeights);

            using var jpg = visionWeights.LoadMedia("a.jpg");

            //<|think|>
            string systemPrompt = "<|turn>system\n<turn|>\n";
            string promptBeforeImage = $"{systemPrompt}<|turn>user\n"; // 圖片前的文字
            string promptAfterImage = "Describe this image: <|image|>\n<|turn>model\n"; // 圖片後的文字與指令

            Console.WriteLine("🤖 Gemma 4 正在處理中...\n");

            var inferenceParams = new InferenceParams()
            {
                MaxTokens = 2048,
                AntiPrompts = new[] { "<turn|>" }
            };
            executor.Embeds.Add(jpg);
            await foreach (var token in executor.InferAsync(promptBeforeImage, inferenceParams))
            {
            }


            await foreach (var token in executor.InferAsync(promptAfterImage, inferenceParams))
            {
                Console.Write(token);
                System.Diagnostics.Trace.Write(token);
            }


            Console.ReadLine();
        }
    }
}
