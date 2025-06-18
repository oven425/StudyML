// See https://aka.ms/new-console-template for more information
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Drawing;

var modelPath = "../../../yolov8n.onnx";

var targetHeight = 640;
var targetWidth = 640;
var bitmap = new Bitmap("../../../street640x640.jpg");
var inputTensor = ConvertRgb24RawToOnnxInput(bitmap);

var inputName = "images";
var input = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };
using (var session = new InferenceSession(modelPath))
{
    var outputNames = session.OutputMetadata.Keys.ToList();
    using (var results = session.Run(input))
    {
        // 方法二：根據名稱訪問 (推薦，更穩健)
        // 這要求你知道輸出層的確切名稱
        var output0 = results.FirstOrDefault(o => o.Name == "output0");
        var output1 = results.FirstOrDefault(o => o.Name == "output1");
        
        if (output0 != null)
        {
            
            Console.WriteLine($"Found output: {output0.Name}");
            // 將輸出轉換為特定型別的 Tensor
            var output0Tensor = output0.AsTensor<float>();
            int batchSize = output0Tensor.Dimensions[0]; // 應該是 1
            int attributesPerBox = output0Tensor.Dimensions[1]; // 應該是 116
            int numBoxes = output0Tensor.Dimensions[2]; // 應該是 8400
            var kk = output0Tensor[0, 4, 100];

            ProcessOutput0(output0Tensor);

            // 在這裡處理 outputTensor0，例如：
            //Console.WriteLine($"output0 shape: {string.Join(",", outputTensor0.Dimensions.Select(d => d.ToString()))}");
            // 對於 [1,116,8400] 這樣的輸出，你會進行 NMS 等後處理
        }

        if (output1 != null)
        {
            Console.WriteLine($"Found output: {output1.Name}");
            var outputTensor1 = output1.AsTensor<float>();
            // 在這裡處理 outputTensor1，例如：
            //Console.WriteLine($"output1 shape: {string.Join(",", outputTensor1.Dimensions.Select(d => d.ToString()))}");
            // 對於 [1,32,160,160] 這樣的輸出，這是用於分割的原型遮罩
        }

        if (output0 == null && output1 == null)
        {
            Console.WriteLine("No expected outputs found in the results.");
        }
    }
}

void ProcessOutput0(Tensor<float> output0Tensor)
{
    // output0Tensor shape: [1, 116, 8400]
    int batchSize = output0Tensor.Dimensions[0]; // 應該是 1
    int attributesPerBox = output0Tensor.Dimensions[1]; // 應該是 116
    int numBoxes = output0Tensor.Dimensions[2]; // 應該是 8400

    // 假設你的模型有 80 個類別（需要根據你的模型實際情況調整）
    int numClasses = 80;
    // 類別機率的起始索引 (通常是 5，因為前 4 個是 bbox，第 5 個是 objectness_score)
    int classProbabilitiesStartIndex = 5;

    if (attributesPerBox < classProbabilitiesStartIndex + numClasses)
    {
        Console.WriteLine("Error: attributesPerBox is too small to contain class probabilities based on assumed numClasses.");
        return;
    }

    Console.WriteLine($"Processing {numBoxes} potential detections from output0...");

    // 模擬 NMS 前的處理，提取每個候選框的資訊
    for (int i = 0; i < numBoxes; i++)
    {
        // 獲取物件置信度 (索引 4)
        float objectnessScore = output0Tensor[0, 4, i];

        // 僅處理置信度高於某個閾值的候選框 (這是 NMS 的第一步)
        // 實際應用中，這個閾值會更高，且會再進行 NMS
        if (objectnessScore > 0.05f) // 這裡使用一個較低的閾值來展示
        {
            // 提取類別機率
            float[] classProbabilities = new float[numClasses];
            for (int c = 0; c < numClasses; c++)
            {
                classProbabilities[c] = output0Tensor[0, classProbabilitiesStartIndex + c, i];
            }

            // 找到最高機率的類別及其索引
            float maxClassProbability = 0;
            int predictedClassId = -1;
            for (int c = 0; c < numClasses; c++)
            {
                if (classProbabilities[c] > maxClassProbability)
                {
                    maxClassProbability = classProbabilities[c];
                    predictedClassId = c;
                }
            }

            // 將類別 ID 映射到類別名稱
            string predictedClassName = "Unknown";
            //if (predictedClassId >= 0 && predictedClassId < CocoClassNames.Length)
            //{
            //    predictedClassName = CocoClassNames[predictedClassId];
            //}

            Console.WriteLine($"  Box {i}: Objectness Score = {objectnessScore:F4}");
            Console.WriteLine($"             Predicted Class = {predictedClassName} (ID: {predictedClassId}) with probability = {maxClassProbability:F4}");
            // 在這裡，你還可以提取邊界框座標等信息
        }
    }
}


DenseTensor<float> ConvertRgb24RawToOnnxInput(Bitmap bmp)
{
    // 3. 創建輸出張量 (NCHW 格式)
    var inputTensor = new DenseTensor<float>([1, 3, targetHeight, targetWidth]);

    // 4. 正規化並轉換到 NCHW 格式
    // 遍歷每個像素，提取 R, G, B，正規化，並放入 Tensor 的正確位置
    // 注意：ONNX 模型通常期望 NCHW 格式，且通道順序可能是 RGB 或 BGR，
    // 大多數預訓練模型（如 PyTorch 轉換的 YOLO）期望 RGB
    // 如果模型期望 BGR，你需要在這一步調換通道順序
    for (int y = 0; y < targetHeight; y++)
    {
        for (int x = 0; x < targetWidth; x++)
        {
            //Rgb24 pixel = image[x, y]; // 獲取像素值
            var cc = bmp.GetPixel(x, y);
            // 將像素值從 0-255 轉換為 0.0-1.0 (或 -1.0-1.0，取決於你的模型訓練方式)
            // 大多數圖像分類或物件偵測模型是 0.0-1.0
            // 如果是 ImageNet 預訓練模型，可能需要更複雜的正規化 (減平均值，除標準差)
            float r = cc.R / 255.0f;
            float g = cc.G / 255.0f;
            float b = cc.B / 255.0f;

            // 填充到 DenseTensor (NCHW 格式)
            // Batch = 0, Channel = 0 (R), Height = y, Width = x
            inputTensor[0, 0, y, x] = r; // Red channel
            inputTensor[0, 1, y, x] = g; // Green channel
            inputTensor[0, 2, y, x] = b; // Blue channel
        }
    }
    return inputTensor;
}


//DenseTensor<float> ConvertRgb24RawToOnnxInput(
//        byte[] rawRgb24Data,
//        int originalWidth,
//        int originalHeight,
//        int targetWidth = 640,
//        int targetHeight = 640)
//{
//    //// 1. 從 RAW 位元組數據創建 ImageSharp 圖片對象
//    //// 注意：ImageSharp 預設處理 RGB24 像素數據時，通常是按 R, G, B, R, G, B... 順序
//    //// 所以我們需要手動將 byte[] 轉換為 Image<Rgb24>
//    //// 或者更簡單地，直接使用 ImageSharp 的 FromPixelData 方法
//    //Image<Rgb24> image;
//    //try
//    //{
//    //    image = Image.LoadPixelData<Rgb24>(rawRgb24Data, originalWidth, originalHeight);
//    //}
//    //catch (Exception ex)
//    //{
//    //    Console.WriteLine($"Error loading pixel data: {ex.Message}");
//    //    throw;
//    //}


//    //// 2. 調整大小 (Resize)
//    //// 使用 ResizeOptions.Mode.Pad 或 ResizeOptions.Mode.Max 來保持圖片比例
//    //// 這裡我們簡單地直接 Resize 到目標尺寸，可能會導致圖片變形
//    //// 如果需要保持比例，可以參考 YOLO 的 Letterbox 處理方式
//    //image.Mutate(x => x.Resize(new ResizeOptions
//    //{
//    //    Size = new Size(targetWidth, targetHeight),
//    //    Mode = ResizeMode.Stretch // Stretch 會導致變形，Fit/Pad 更能保持比例
//    //}));

//    //// 如果要實現 YOLO 的 Letterbox 效果（保持比例，填充空白區域）：
//    //// Image<Rgb24> letterboxedImage = new Image<Rgb24>(targetWidth, targetHeight, new Rgb24(114, 114, 114)); // 填充灰色
//    //// image.Mutate(x => x.Resize(new ResizeOptions
//    //// {
//    ////     Size = new Size(targetWidth, targetHeight),
//    ////     Mode = ResizeMode.Max // Max 模式會按比例縮放到最大尺寸，然後剩下的區域需要手動填充
//    //// }));
//    //// // 計算居中位置並複製圖像到 letterboxedImage
//    //// int xOffset = (targetWidth - image.Width) / 2;
//    //// int yOffset = (targetHeight - image.Height) / 2;
//    //// letterboxedImage.Mutate(ctx => ctx.DrawImage(image, new Point(xOffset, yOffset), 1f));
//    //// image = letterboxedImage;


//    // 3. 創建輸出張量 (NCHW 格式)
//    var inputTensor = new DenseTensor<float>(new[] { 1, 3, targetHeight, targetWidth });

//    // 4. 正規化並轉換到 NCHW 格式
//    // 遍歷每個像素，提取 R, G, B，正規化，並放入 Tensor 的正確位置
//    // 注意：ONNX 模型通常期望 NCHW 格式，且通道順序可能是 RGB 或 BGR，
//    // 大多數預訓練模型（如 PyTorch 轉換的 YOLO）期望 RGB
//    // 如果模型期望 BGR，你需要在這一步調換通道順序
//    for (int y = 0; y < targetHeight; y++)
//    {
//        for (int x = 0; x < targetWidth; x++)
//        {
//            //Rgb24 pixel = image[x, y]; // 獲取像素值
//            var cc = 
//            // 將像素值從 0-255 轉換為 0.0-1.0 (或 -1.0-1.0，取決於你的模型訓練方式)
//            // 大多數圖像分類或物件偵測模型是 0.0-1.0
//            // 如果是 ImageNet 預訓練模型，可能需要更複雜的正規化 (減平均值，除標準差)
//            float r = pixel.R / 255.0f;
//            float g = pixel.G / 255.0f;
//            float b = pixel.B / 255.0f;

//            // 填充到 DenseTensor (NCHW 格式)
//            // Batch = 0, Channel = 0 (R), Height = y, Width = x
//            inputTensor[0, 0, y, x] = r; // Red channel
//            inputTensor[0, 1, y, x] = g; // Green channel
//            inputTensor[0, 2, y, x] = b; // Blue channel
//        }
//    }

//    return inputTensor;
//}