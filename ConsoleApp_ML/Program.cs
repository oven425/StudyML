// See https://aka.ms/new-console-template for more information
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Onnx;
using SkiaSharp;
using static System.Net.Mime.MediaTypeNames;

Console.WriteLine("Hello, World!");

string modelPath = "../../../yolov8n.onnx";
string imagePath = "../../../crosswalk.jpg";
string outputImagePath = "image_result.jpg";

// COCO 資料集的 80 個類別標籤
string[] cocoLabels = new string[] {
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

// 初始化模型
Console.WriteLine("Loading model...");
var yoloModel = new YoloModel(modelPath, cocoLabels, true);

// 讀取影像
Console.WriteLine("Loading image...");
using var image = SKBitmap.Decode(imagePath);

// 進行預測
Console.WriteLine("Detecting objects...");
var boundingBoxes = yoloModel.Predict(image);

// 繪製結果
Console.WriteLine($"Detected {boundingBoxes.Count} objects.");
DrawAndSaveResult(image, boundingBoxes, outputImagePath);
Console.WriteLine($"Result saved to {outputImagePath}");


// 繪製結果並儲存的方法
static void DrawAndSaveResult(SKBitmap image, List<BoundingBox> boxes, string path)
{
    using var canvas = new SKCanvas(image);
    using var paint = new SKPaint
    {
        Style = SKPaintStyle.Stroke,
        Color = SKColors.Red,
        StrokeWidth = 3
    };
    using var textPaint = new SKPaint
    {
        Style = SKPaintStyle.Fill,
        Color = SKColors.White,
        TextSize = 24,
        IsAntialias = true
    };
    using var textBackgroundPaint = new SKPaint
    {
        Style = SKPaintStyle.Fill,
        Color = SKColors.Red,
    };

    foreach (var box in boxes)
    {
        // 繪製邊界框
        canvas.DrawRect(box.Rectangle.Left, box.Rectangle.Top, box.Rectangle.Width, box.Rectangle.Height, paint);

        // 繪製標籤與分數
        string label = $"{box.Label} ({box.Score:P2})"; // P2: 格式化為百分比
        var textBounds = new SKRect();
        textPaint.MeasureText(label, ref textBounds);

        // 繪製文字背景
        canvas.DrawRect(box.Rectangle.Left, box.Rectangle.Top - textBounds.Height - 5, textBounds.Width + 10, textBounds.Height + 10, textBackgroundPaint);
        // 繪製文字
        canvas.DrawText(label, box.Rectangle.Left + 5, box.Rectangle.Top - 5, textPaint);
    }

    using var outputImage = SKImage.FromBitmap(image);
    using var data = outputImage.Encode(SKEncodedImageFormat.Jpeg, 90);
    using var stream = File.OpenWrite(path);
    data.SaveTo(stream);
}

public class YoloModel
{
    private readonly PredictionEngine<YoloInput, YoloOutput> _predictionEngine;
    private readonly string[] _labels;
    private readonly int _imageWidth;
    private readonly int _imageHeight;

    public YoloModel(string modelPath, string[] labels, bool useGpu, int imageWidth = 640, int imageHeight = 640)
    {
        var mlContext = new MLContext();
        _labels = labels;
        _imageWidth = imageWidth;
        _imageHeight = imageHeight;

        var onnxOptions = new OnnxOptions
        {
            ModelFile = modelPath,
            // 指定要使用的 GPU 裝置 ID。0 代表系統上的預設/第一個 GPU。
            // 如果設定為 null，則會退回使用 CPU。
            //GpuDeviceId = useGpu ? 0 : (int?)null,
            // FallbackToCpu：如果 GPU 執行失敗，是否允許退回到 CPU 執行。
            FallbackToCpu = true,
            // 定義輸入和輸出欄位名稱
            InputColumns = new[] { "images" },
            OutputColumns = new[] { "output0" }
        };
        
        // 建立 ML.NET 的處理管線
        var pipeline = mlContext.Transforms.ApplyOnnxModel(onnxOptions);

        var model = pipeline.Fit(mlContext.Data.LoadFromEnumerable(new List<YoloInput>()));

        // 建立預測引擎
        _predictionEngine = mlContext.Model.CreatePredictionEngine<YoloInput, YoloOutput>(model);
    }

    public List<BoundingBox> Predict(SKBitmap image)
    {
        // 1. 影像前處理
        var input = PreprocessImage(image);

        // 2. 進行預測
        var output = _predictionEngine.Predict(input);

        // 3. 進行後處理
        return PostprocessOutput(output, image.Width, image.Height);
    }

    private YoloInput PreprocessImage(SKBitmap image)
    {
        // 調整影像大小並轉換為 float 陣列
        using var resizedImage = image.Resize(new SKImageInfo(_imageWidth, _imageHeight), SKFilterQuality.Medium);

        var floatArray = new float[_imageWidth * _imageHeight * 3];
        var span = new Span<float>(floatArray);

        for (int y = 0; y < _imageHeight; y++)
        {
            for (int x = 0; x < _imageWidth; x++)
            {
                var pixel = resizedImage.GetPixel(x, y);
                // 將像素值正規化到 [0, 1] 之間，並轉換為 Planar 格式 (R,G,B 分開)
                span[_imageHeight * _imageWidth * 0 + y * _imageWidth + x] = pixel.Red / 255.0f;
                span[_imageHeight * _imageWidth * 1 + y * _imageWidth + x] = pixel.Green / 255.0f;
                span[_imageHeight * _imageWidth * 2 + y * _imageWidth + x] = pixel.Blue / 255.0f;
            }
        }

        return new YoloInput { Image = floatArray };
    }

    private List<BoundingBox> PostprocessOutput(YoloOutput output, int originalImageWidth, int originalImageHeight)
    {
        var results = new List<BoundingBox>();
        int boxInfoLength = 84; // 4 (box) + 80 (classes)
        int outputLength = output.Output.Length / boxInfoLength;

        var data = output.Output;

        // 將輸出從 [1, 84, 8400] 轉置為 [1, 8400, 84]
        float[,] transposedData = new float[outputLength, boxInfoLength];
        for (int i = 0; i < outputLength; i++)
        {
            for (int j = 0; j < boxInfoLength; j++)
            {
                transposedData[i, j] = data[j * outputLength + i];
            }
        }

        // 過濾結果
        for (int i = 0; i < outputLength; i++)
        {
            float maxScore = 0;
            int maxIndex = -1;

            // 取得最高分的類別
            for (int j = 4; j < boxInfoLength; j++)
            {
                if (transposedData[i, j] > maxScore)
                {
                    maxScore = transposedData[i, j];
                    maxIndex = j - 4;
                }
            }

            // 非極大值抑制 (NMS) 的簡化版 - 只保留分數高的
            if (maxScore > 0.5f) // 信賴度閾值
            {
                // 解析邊界框座標 (cx, cy, w, h) -> (x1, y1, x2, y2)
                float cx = transposedData[i, 0];
                float cy = transposedData[i, 1];
                float w = transposedData[i, 2];
                float h = transposedData[i, 3];

                float x1 = cx - w / 2;
                float y1 = cy - h / 2;
                float x2 = cx + w / 2;
                float y2 = cy + h / 2;

                // 將座標從模型尺寸 (640x640) 縮放回原始影像尺寸
                float scaleX = (float)originalImageWidth / _imageWidth;
                float scaleY = (float)originalImageHeight / _imageHeight;
                scaleX = scaleY = 1;
                results.Add(new BoundingBox
                {
                    Label = _labels[maxIndex],
                    Score = maxScore,
                    Rectangle = new System.Drawing.RectangleF(
                        x1 * scaleX * originalImageWidth,
                        y1 * scaleY * originalImageHeight,
                        (x2 - x1) * scaleX * originalImageWidth,
                        (y2 - y1) * scaleY * originalImageHeight)
                });
            }
        }

        // 在此處可以加入更複雜的 NMS 邏輯來移除重疊的框
        // 為了範例簡潔，此處省略

        return results;
    }
}

public class BoundingBox
{
    public string Label { get; set; }
    public float Score { get; set; }
    public System.Drawing.RectangleF Rectangle { get; set; }
}

public class YoloOutput
{
    // YOLOv8 的 ONNX 模型通常輸出名稱為 "output0"
    [ColumnName("output0")]
    public float[]? Output { get; set; }
}
public class YoloInput
{
    // YOLOv8 的 ONNX 模型通常期望輸入名稱為 "images"
    [ColumnName("images")]
    [VectorType(1, 3, 640, 640)] // 根據你的模型調整尺寸 (BatchSize, Channels, Height, Width)
    public float[]? Image { get; set; }
}