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
        _prev = children != null ? new HashSet<Value>(children) : new HashSet<Value>();
        _backward = backward??(_ => { });
    }

    public static Value operator *(Value a, Value b)
    {
        var outNode = new Value(a.Data * b.Data, new[] { a, b }, (x) =>
        {
            a.Grad += b.Data * x.Grad;
            b.Grad += a.Data * x.Grad;
        });
        return outNode;
    }

    public static Value operator +(Value a, Value b)
    {
        var outNode = new Value(a.Data + b.Data, new[] { a, b }, (x) =>
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
                foreach (var child in v._prev) BuildTopo(child);
                topo.Add(v);
            }
        }
        BuildTopo(this);
        topo.Reverse();

        this.Grad = 1.0;
        foreach (var v in topo) v._backward(v);
    }
}