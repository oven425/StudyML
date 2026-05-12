public class MLP
{
    public List<Layer> Layers { get; set; } = new();

    // nIn: 總輸入數量
    // nOuts: 陣列，定義每層要有幾個神經元。例如 new int[] {4, 4, 1}
    public MLP(int nIn, int[] nOuts)
    {
        int currentIn = nIn;
        for (int i = 0; i < nOuts.Length; i++)
        {
            Layers.Add(new Layer(currentIn, nOuts[i]));
            currentIn = nOuts[i]; // 重點：下一層的輸入數量 = 這一層的神經元數量
        }
    }

    public List<Value> Forward(List<Value> x)
    {
        var currentInput = x;
        foreach (var layer in Layers)
        {
            // 資料流過每一層
            currentInput = layer.Forward(currentInput);
        }
        return currentInput; // 最終輸出（最後一層的結果）
    }

    public List<Value> Parameters()
    {
        var p = new List<Value>();
        foreach (var l in Layers) p.AddRange(l.Parameters());
        return p;
    }
}