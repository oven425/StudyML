using LLama;
using LLama.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SemanticKernel_Llama
{
    public class chat1
    {
        async public Task Test()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            string modelPath = @"..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf"; // change it to your own model path.
            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 8192,
                GpuLayerCount = 20,
            };

            using var model = await LLamaWeights.LoadFromFileAsync(parameters);


            using var context = model.CreateContext(parameters);

            var executor = new InteractiveExecutor(context);
            
            //<|think|>
            // 3. 根據 Gemma 4 規範建構 System Prompt (宣告工具與啟用思考)
            var systemPrompt = """
        <|turn>system
        你是個AI助理,所有回答都要用中文
        <turn|>
        """;

            Console.WriteLine("🤖 Gemma 4 正在處理中...\n");
            // 1. 建立採樣參數，並設定 Temperature

            var inferenceParams = new InferenceParams()
            {
                MaxTokens = 81920,
                AntiPrompts = ["<turn|>"]
            };

            await foreach (var token in executor.InferAsync($"{systemPrompt}\n", inferenceParams))
            {
                Console.Write(token);
                System.Diagnostics.Trace.Write(token);
            }
            while(true)
            {
                string userQuestion = Console.ReadLine();
                if (userQuestion == "exit")
                {
                    return;
                }



                await foreach (var token in executor.InferAsync($"<|turn>user\n{userQuestion}<turn|>\n<|turn>model\n", inferenceParams))
                {
                    Console.Write(token);
                    System.Diagnostics.Trace.Write(token);
                }
            }
            




            Console.WriteLine("\n\n[對話結束]");
        }

    }
}
