public class Neuron
{
    public List<Value> Weights { get; set; } = new();
    public Value Bias { get; set; }

    // nIn 代表這個神經元有幾個輸入口
    public Neuron(int nIn)
    {
        var rand = new Random();
        for (int i = 0; i < nIn; i++)
            Weights.Add(new Value(rand.NextDouble() * 2 - 1)); // 隨機權重 -1 ~ 1
        Bias = new Value(rand.NextDouble() * 2 - 1);         // 隨機偏差
    }

    public Value Forward(List<Value> x)
    {
        // 運算：sum = (x[0]*w[0] + x[1]*w[1] + ...) + bias
        Value sum = Bias;
        for (int i = 0; i < x.Count; i++)
        {
            sum = sum + (Weights[i] * x[i]);
        }
        return sum; // 這裡可以選擇加上 .Sigmoid()
    }

    public List<Value> Parameters() // 收集這顆神經元裡所有的參數
    {
        var p = new List<Value>(Weights);
        p.Add(Bias);
        return p;
    }
}