using System.Text.Json;

public class Neuron
{
    public List<Value> Weights { get; set; } = new List<Value>();
    public Value Bias { get; set; }
    private static Random _rand = new Random();

    public Neuron(int inputSize)
    {
        // 隨機初始化權重，這是 AI 學習的起點
        for (int i = 0; i < inputSize; i++)
            Weights.Add(new Value(_rand.NextDouble() * 2 - 1));
        Bias = new Value(0);
    }

    public Value Forward(List<Value> x)
    {
        // y = w1*x1 + w2*x2 + ... + b
        Value sum = Bias;
        for (int i = 0; i < Weights.Count; i++)
            sum = sum + (Weights[i] * x[i]);

        return sum; // 為了簡單，我們先不加激發函數（如 Tanh 或 ReLU）
    }

    public List<Value> Parameters()
    {
        var p = new List<Value>(Weights);
        p.Add(Bias);
        return p;
    }

    static public void SaveModel(Neuron neuron, string filePath)
    {
        // 只提取 Data，不需要存 Grad (梯度是訓練時才用的臨時變數)
        var weights = neuron.Weights.Select(w => w.Data).ToList();
        var modelData = new
        {
            Weights = weights,
            Bias = neuron.Bias.Data
        };

        string json = JsonSerializer.Serialize(modelData);
        File.WriteAllText(filePath, json);
        Console.WriteLine("模型已存儲！");
    }

    static public void LoadModel(Neuron neuron, string filePath)
    {
        string json = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<ModelSchema>(json);

        for (int i = 0; i < neuron.Weights.Count; i++)
        {
            neuron.Weights[i].Data = data.Weights[i];
        }
        neuron.Bias.Data = data.Bias;
        Console.WriteLine("模型載入成功，準備推理！");
    }
}

public class ModelSchema
{
    public List<double> Weights { get; set; }
    public double Bias { get; set; }
}