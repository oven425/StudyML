public class Layer
{
    public List<Neuron> Neurons { get; set; } = new();

    // nIn: 每個工人接幾個數字, nOut: 這層樓總共有幾個工人
    public Layer(int nIn, int nOut)
    {
        for (int i = 0; i < nOut; i++)
            Neurons.Add(new Neuron(nIn));
    }

    public List<Value> Forward(List<Value> x)
    {
        // 讓這層的每個神經元都算一遍，收集成一個 List
        var outputs = new List<Value>();
        foreach (var n in Neurons)
            outputs.Add(n.Forward(x));
        return outputs;
    }

    public List<Value> Parameters()
    {
        var p = new List<Value>();
        foreach (var n in Neurons) p.AddRange(n.Parameters());
        return p;
    }
}