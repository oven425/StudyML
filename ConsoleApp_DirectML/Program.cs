// See https://aka.ms/new-console-template for more information
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Drawing;


// COCO 資料集的 80 個類別標籤
string[] classNames = new string[] {
    "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light",
    "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow",
    "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee",
    "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard",
    "tennis racket", "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple",
    "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch",
    "potted plant", "bed", "dining table", "toilet", "tv", "laptop", "mouse", "remote", "keyboard", "cell phone",
    "microwave", "oven", "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear",
    "hair drier", "toothbrush"
};

var modelPath = "../../../yolov8n.onnx";

var targetHeight = 640;
var targetWidth = 640;
var bitmap = new Bitmap("../../../street640x640.jpg");
var inputTensor = ConvertRgb24RawToOnnxInput(bitmap);
SessionOptions sessionOptions = new SessionOptions();
//sessionOptions.AppendExecutionProvider_DML();
var inputName = "images";
var input = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };
using var session = new InferenceSession(modelPath, sessionOptions);
var outputNames = session.OutputMetadata.Keys.ToList();
using var results = session.Run(input);
var output = results.First().AsTensor<float>();
var ooo = ParseOutput(output, 640, 640);
ooo?.Clear();

List<Detection> ParseOutput(Tensor<float> output, int imageWidth, int imageHeight)
{
    const float confidenceThreshold = 0.5f; // 信度門檻
    const int numClasses = 80; // COCO 資料集有 80 個類別

    var results = new List<Detection>();

    var outputData = output.ToDenseTensor(); // 轉換為 DenseTensor 更方便存取
    int numProposals = outputData.Dimensions[2]; // e.g., 8400

    for (int i = 0; i < numProposals; i++)
    {
        // 1. 找到最高信度及其類別
        float maxConfidence = 0.0f;
        int classId = -1;

        // 從第 5 個元素開始 (索引為 4) 是類別信度
        for (int j = 0; j < numClasses; j++)
        {
            float currentConfidence = outputData[0, 4 + j, i];
            if (currentConfidence > maxConfidence)
            {
                maxConfidence = currentConfidence;
                classId = j;
            }
        }

        // 2. 過濾低信度的結果
        if (maxConfidence < confidenceThreshold)
        {
            continue;
        }

        // 3. 提取邊界框資訊 (cx, cy, w, h)
        float cx = outputData[0, 0, i];
        float cy = outputData[0, 1, i];
        float w = outputData[0, 2, i];
        float h = outputData[0, 3, i];

        // 將中心點、寬高轉換為左上角、右下角座標 (x1, y1, x2, y2)
        float x1 = cx - w / 2;
        float y1 = cy - h / 2;

        results.Add(new Detection
        {
            Label = classNames[classId],
            Confidence = maxConfidence,
            Box = new System.Drawing.RectangleF(x1, y1, w, h)
        });
    }



    // 在進入 NMS 之前，先進行座標縮放
    ScaleBoxes(results, imageWidth, imageHeight);

    // 執行 NMS
    return NonMaxSuppression(results);
}

List<Detection> NonMaxSuppression(List<Detection> detections)
{
    const float iouThreshold = 0.45f; // IoU 門檻
    var finalDetections = new List<Detection>();

    // 按信度從高到低排序
    var sortedDetections = detections.OrderByDescending(d => d.Confidence).ToList();

    while (sortedDetections.Count > 0)
    {
        var bestDetection = sortedDetections[0];
        finalDetections.Add(bestDetection);

        // 移除第一個 (已選為最佳)
        sortedDetections.RemoveAt(0);

        // 計算剩餘的框與最佳框的 IoU，並移除重疊過多的
        for (int i = sortedDetections.Count - 1; i >= 0; i--)
        {
            float iou = CalculateIoU(bestDetection.Box, sortedDetections[i].Box);
            if (iou > iouThreshold)
            {
                sortedDetections.RemoveAt(i);
            }
        }
    }

    return finalDetections;
}

float CalculateIoU(System.Drawing.RectangleF a, System.Drawing.RectangleF b)
{
    float intersectionArea = System.Drawing.RectangleF.Intersect(a, b).Width * System.Drawing.RectangleF.Intersect(a, b).Height;
    float unionArea = (a.Width * a.Height) + (b.Width * b.Height) - intersectionArea;

    return unionArea > 0 ? intersectionArea / unionArea : 0;
}


void ScaleBoxes(List<Detection> detections, int imageWidth, int imageHeight)
{
    // 假設模型輸入尺寸為 640x640
    const float modelWidth = 640.0f;
    const float modelHeight = 640.0f;

    // 計算縮放比例
    // 注意：預處理時可能會有 letterboxing (添加黑邊)，這裡的縮放需要與預處理對應
    // 這裡是一個簡化的範例，假設是直接縮放
    float scaleX = imageWidth / modelWidth;
    float scaleY = imageHeight / modelHeight;

    foreach (var detection in detections)
    {
        var box = detection.Box;

        // 縮放座標
        box.X *= scaleX;
        box.Y *= scaleY;
        box.Width *= scaleX;
        box.Height *= scaleY;

        // 確保座標在圖片範圍內
        box.X = Math.Max(0, box.X);
        box.Y = Math.Max(0, box.Y);
        box.Width = Math.Min(imageWidth - box.X, box.Width);
        box.Height = Math.Min(imageHeight - box.Y, box.Height);

        detection.Box = box;
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


// 建議定義一個類來存放偵測結果
public class Detection
{
    public System.Drawing.RectangleF Box { get; set; } // 邊界框
    public string Label { get; set; }                  // 類別名稱
    public float Confidence { get; set; }              // 信度
}