using LLama;
using LLama.Common;
using LLama.Sampling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SemanticKernel_Llama
{
    public class TTT
    {
        public async Task Test1()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            string modelPath = @"..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf"; // change it to your own model path.
            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 819200,
                GpuLayerCount = 20,
            };

            using var model = LLamaWeights.LoadFromFile(parameters);


            using var context = model.CreateContext(parameters);

            var executor = new InteractiveExecutor(context);


            // 3. 根據 Gemma 4 規範建構 System Prompt (宣告工具與啟用思考)
            var systemPrompt = """
        <|turn>system
        <|think|>透過文字來和你玩井字棋遊戲,玩家是X,AI是O
        ### 輸入格式要求 (Input Specification)
        **格式：** 一個包含九個字符的字串，用 `O`、`X` 和 `_` (空格) 來表示棋盤上的狀態。
        **範例：** `OOOXX_XXX`

        ### 遊戲執行邏輯與步驟
        AI 收到棋盤字串後,依照井子棋規則並嚴格依照以下順序執行：

        1. **判斷目前局勢（檢查玩家是否獲勝）：**
           - 檢查玩家傳入的棋盤。若玩家（X）已經連成一線，或棋盤已滿（平手），請直接呼叫 `ttt_state` 結束遊戲。

        2. **AI 進行下棋動作：**
           - 若遊戲未結束，AI 必須從棋盤中的空格（`_`）挑選一個位置填入 `O`。

        3. **判斷最新局勢（檢查 AI 是否獲勝）：**
           - AI 下完 `O` 之後，立即檢查新的棋盤狀態：
             - **若 AI 勝利或平手：** 呼叫 `ttt_state` 輸出最後結果。
             - **若仍未分出勝負：** 呼叫 `ttt_out` 輸出最新的棋盤狀態，等待玩家下下一手。
        4. 當你收到 `<|tool_response>` 時，代表後端系統已經讓玩家（X）下完棋，並且該 response 中的 `data` 字串就是玩家下完後的「最新棋盤狀態」。你必須「立刻重新執行步驟 1」，分析這個新字串，並決定你的下一步，絕對不能停止發言。

        ### 輸出規則
        1. 回傳棋盤狀態要使用ttt_out
        2. 若偵測到分出勝負,要使用ttt_state
        <|tool>declaration:ttt_out{
          description: <|"|>AI計算後的棋盤狀態，必須是一個包含九個字符的字串，用 `O`、`X` 和 `_` (空格) 來表示棋盤上的狀態，例如 `OOOXX_XXX`<|"|>,
          parameters: {
            type: <|"|>object<|"|>,
            properties: {
              data: {
                type: <|"|>string<|"|>,
                description: <|"|>AI計算後的棋盤狀態，必須是一個包含九個字符的字串，用 `O`、`X` 和 `_` (空格) 來表示棋盤上的狀態，例如 `OOOXX_XXX`<|"|>
              }
            },
            required: [<|"|>data<|"|>]
          }
        }<tool|>
        <|tool>declaration:ttt_state{
          description: <|"|>當偵測是一方輸了或贏了要輸出相應的結果<|"|>,
          parameters: {
            type: <|"|>object<|"|>,
            properties: {
              state: {
                type: <|"|>integer<|"|>,
                description: <|"|>0:未分勝負, 1:AI勝利, 2:玩家勝利,3:平手<|"|>
              }
            },
            required: [<|"|>state<|"|>]
          }
        }<tool|>
        <turn|>
        """;


            Console.WriteLine("🤖 Gemma 4 正在處理中...\n");

            var inferenceParams = new InferenceParams()
            {
                MaxTokens = 819200,
                AntiPrompts = ["<turn|>", "<tool_call>"],
                SamplingPipeline = new DefaultSamplingPipeline()
                {
                    TopK = 40,
                    TopP = 0.9F,
                    Temperature = 0.1F
                }
            };
            Console.Write("init system....");
            await foreach (var token in executor.InferAsync($"{systemPrompt}\n", inferenceParams))
            {
                System.Diagnostics.Trace.Write(token);
            }
            Console.WriteLine("complete");
            string resp = "";
            string ttt = "_________";
            var userQuestion = $"""
                <|turn>user
                這是目前的棋盤狀態{ttt},之後會透過ttt_out回傳更新後的棋盤狀態
                <turn|>
                <|turn>model
                """;
            using CancellationTokenSource cts = new CancellationTokenSource(1000);
            await foreach (var token in executor.InferAsync(userQuestion, inferenceParams))
            {
                resp = resp + token;
                System.Diagnostics.Trace.Write(token);
            }
            System.Diagnostics.Trace.WriteLine("");
            while (true)
            {
                Console.WriteLine("");
                Console.WriteLine("print AI");
                var (action, args) = ProcessCommand(resp);

                var readFileArgs = JsonSerializer.Deserialize<TttData>(args);
                PrintJson(readFileArgs.data);
                Console.Write("輸入要下的位置0-8:");
                var userInput = Console.ReadLine();
                if (userInput == null) return;
                if (!int.TryParse(userInput, out var pos)) return;

                char[] chars = readFileArgs.data.ToCharArray();
                chars[pos] = 'X';
                ttt = new string(chars);

                resp = "";
                var toolresp = $"<|tool_response>response:ttt_out{{}}<tool_response|>";
                await foreach (var token in executor.InferAsync(toolresp, inferenceParams))
                {
                    resp = resp + token;
                    Console.Write(token);
                    System.Diagnostics.Trace.Write(token);
                }
                
                userQuestion = $"""
                <|turn>user
                這是目前的棋盤狀態{ttt},之後會透過ttt_out回傳更新後的棋盤狀態
                <turn|>
                <|turn>model
                """;
                await foreach (var token in executor.InferAsync(userQuestion, inferenceParams))
                {
                    resp = resp + token;
                    Console.Write(token);
                    System.Diagnostics.Trace.Write(token);
                }
            }

            
            Console.WriteLine("\n\n[對話結束]");
        }

        //public async Task Test()
        //{
        //    Console.OutputEncoding = Encoding.UTF8;
        //    Console.InputEncoding = Encoding.UTF8;

        //    string modelPath = @"..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf"; // change it to your own model path.
        //    var parameters = new ModelParams(modelPath)
        //    {
        //        ContextSize = 819200,
        //        GpuLayerCount = 20,
        //    };

        //    using var model = LLamaWeights.LoadFromFile(parameters);


        //    using var context = model.CreateContext(parameters);

        //    var executor = new InteractiveExecutor(context);


        //    3.根據 Gemma 4 規範建構 System Prompt(宣告工具與啟用思考)
        //    var systemPrompt = """
        //<|turn>system
        //<|think|>我想要透過文字來和你玩井字棋遊戲,玩家是X,AI是O
        //### 🛠️ 輸入格式要求 (Input Specification)
        //**格式：** 一個包含九個字符的字串，用 `O`、`X` 和 `_` (空格) 來表示棋盤上的狀態。
        //**範例：** `OOOXX_XXX`

        //### 🎮 遊戲執行邏輯與步驟
        //AI 收到棋盤字串後,依照井子棋規則並嚴格依照以下順序執行：

        //1. **判斷目前局勢（檢查玩家是否獲勝）：**
        //   - 檢查玩家傳入的棋盤。若玩家（X）已經連成一線，或棋盤已滿（平手），請直接呼叫 `ttt_state` 結束遊戲。

        //2. **AI 進行下棋動作：**
        //   - 若遊戲未結束，AI 必須從棋盤中的空格（`_`）挑選一個位置填入 `O`。

        //3. **判斷最新局勢（檢查 AI 是否獲勝）：**
        //   - AI 下完 `O` 之後，立即檢查新的棋盤狀態：
        //     - **若 AI 勝利或平手：** 呼叫 `ttt_state` 輸出最後結果。
        //     - **若仍未分出勝負：** 呼叫 `ttt_out` 輸出最新的棋盤狀態，等待玩家下下一手。

        //### 輸出規則
        //1. 回傳棋盤狀態要使用ttt_out
        //2. 若偵測到分出勝負,要使用ttt_state
        //<|tool>declaration:ttt_out{
        //  description: <|"|>輸出AI計算後的棋盤狀態,之後會收到回應值是玩家更新的棋盤狀態<|"|>,
        //  parameters: {
        //    type: <|"|>object<|"|>,
        //    properties: {
        //      data: {
        //        type: <|"|>string<|"|>,
        //        description: <|"|>AI計算後的棋盤狀態，必須是一個包含九個字符的字串，用 `O`、`X` 和 `_` (空格) 來表示棋盤上的狀態，例如 `OOOXX_XXX`<|"|>
        //      }
        //    },
        //    required: [<|"|>data<|"|>]
        //  }
        //}<tool|>
        //<|tool>declaration:ttt_state{
        //  description: <|"|>當偵測是一方輸了或贏了要輸出相應的結果<|"|>,
        //  parameters: {
        //    type: <|"|>object<|"|>,
        //    properties: {
        //      state: {
        //        type: <|"|>integer<|"|>,
        //        description: <|"|>0:未分勝負, 1:AI勝利, 2:玩家勝利,3:平手<|"|>
        //      }
        //    },
        //    required: [<|"|>state<|"|>]
        //  }
        //}<tool|>
        //<turn|>
        //""";


        //    Console.WriteLine("🤖 Gemma 4 正在處理中...\n");

        //    var inferenceParams = new InferenceParams()
        //    {
        //        MaxTokens = 512,
        //        AntiPrompts = ["<turn|>"],
        //        SamplingPipeline = new DefaultSamplingPipeline()
        //        {
        //            TopK = 0,
        //            TopP = 1,
        //            Temperature = 0.2F
        //        }
        //    };
        //    Console.Write("init system....");
        //    await foreach (var token in executor.InferAsync($"{systemPrompt}\n", inferenceParams))
        //    {
        //        System.Diagnostics.Trace.Write(token);
        //    }
        //    Console.WriteLine("complete");
        //    string ttt = "_________";
        //    var userQuestion = $"""
        //        <|turn>user
        //        {ttt}<turn|>
        //        <|turn>model
        //        """;
        //    await foreach (var token in executor.InferAsync(userQuestion, inferenceParams))
        //    {
        //        System.Diagnostics.Trace.Write(token);
        //    }
        //    while (true)
        //    {
        //        Console.Write("輸入要下的位置0-8:");
        //        var userInput = Console.ReadLine();
        //        if (userInput == null) return;
        //        if (!int.TryParse(userInput, out var pos)) return;

        //        char[] chars = ttt.ToCharArray();
        //        chars[pos] = 'X';
        //        ttt = new string(chars);

        //        string resp = "";
        //        await foreach (var token in executor.InferAsync(userQuestion, inferenceParams))
        //        {
        //            resp = resp + token;
        //            Console.Write(token);
        //            System.Diagnostics.Trace.Write(token);

        //        }

        //    }

        //    Console.WriteLine("");
        //    Console.WriteLine("print AI");
        //    var (action, args) = ProcessCommand(resp);

        //    var readFileArgs = JsonSerializer.Deserialize<TttData>(args);
        //    PrintJson(readFileArgs.data);
        //    Console.WriteLine("\n\n[對話結束]");
        //}

        static (string action, string argsContent) ProcessCommand(string input)
        {
            string basePattern = @"call:(?<action>\w+)\{(?<argsContent>.*?)\}";
            Match match = Regex.Match(input, basePattern);

            if (!match.Success)
            {
                return (string.Empty, string.Empty);
            }

            string action = match.Groups["action"].Value;
            string argsContent = match.Groups["argsContent"].Value;
            string cleanPattern = @"(?<key>\w+)\s*:\s*<\|""\|>(?<val>.*?)<\|""\|>";
            string standardizedArgs = Regex.Replace(argsContent, cleanPattern, @"""${key}"":""${val}""");


            string finalJson = $"{{{standardizedArgs}}}";
            return (action, finalJson);
        }

        void PrintJson(string board)
        {
            Console.WriteLine("     |     |      ");
            Console.WriteLine($"  {board[0]}  |  {board[1]}  |  {board[2]}   ");
            Console.WriteLine("_____|_____|_____ ");
            Console.WriteLine("     |     |      ");
            Console.WriteLine($"  {board[3]}  |  {board[4]}  |  {board[5]}   ");
            Console.WriteLine("_____|_____|_____ ");
            Console.WriteLine("     |     |      ");
            Console.WriteLine($"  {board[6]}  |  {board[7]}  |  {board[8]}   ");
            Console.WriteLine("     |     |      \n");
        }
    }
    public class  TttData
    {
        public string data { set; get; }
    }

}
