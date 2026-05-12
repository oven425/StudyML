using System;
using System.Collections.Generic;

public class Value
{
    public double Data { get; set; }
    public double Grad { get; set; } = 0;

    private readonly HashSet<Value> _prev;
    private readonly Action<Value> _backward;

    public Value(double data, IEnumerable<Value> children=null, Action<Value> backward=null)
    {
        Data = data;
        _prev = children != null ? [.. children] : [];
        _backward = backward??(_ => { });
    }

    public static Value operator *(Value a, Value b)
    {
        var outNode = new Value(a.Data * b.Data, [a, b], (x) =>
        {
            a.Grad += b.Data * x.Grad;
            b.Grad += a.Data * x.Grad;
        });
        return outNode;
    }

    public static Value operator +(Value a, Value b)
    {
        var outNode = new Value(a.Data + b.Data, [a, b], (x) =>
        {
            a.Grad += 1.0 * x.Grad;
            b.Grad += 1.0 * x.Grad;
        });
        return outNode;
    }

    public void Backward()
    {
        var topo = new List<Value>();
        var visited = new HashSet<Value>();
        void BuildTopo(Value v)
        {
            if (!visited.Contains(v))
            {
                visited.Add(v);
                foreach (var child in v._prev)
                {
                    BuildTopo(child);
                }
                topo.Add(v);
            }
        }
        BuildTopo(this);
        topo.Reverse();

        this.Grad = 1.0;
        foreach (var v in topo) v._backward(v);
    }

    //public Value Sigmoid()
    //{
    //    double x = this.Data;
    //    double t = 1.0 / (1.0 + Math.Exp(-x));
    //    var outValue = new Value(t, new List<Value> { this }, "sigmoid");

    //    outValue._backward = () =>
    //    {
    //        // Sigmoid 的導數公式：f'(x) = f(x) * (1 - f(x))
    //        this.Grad += t * (1.0 - t) * outValue.Grad;
    //    };
    //    return outValue;
    //}

    //public Value Relu()
    //{
    //    var outValue = new Value(this.Data < 0 ? 0 : this.Data, new List<Value> { this }, "relu");

    //    outValue._backward = () =>
    //    {
    //        // 如果輸入 > 0，梯度原樣傳回；如果 < 0，梯度直接切斷變 0
    //        this.Grad += (this.Data > 0 ? 1 : 0) * outValue.Grad;
    //    };
    //    return outValue;
    //}
}