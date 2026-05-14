// See https://aka.ms/new-console-template for more information



// 1. 定義輸入與權重 (Weights)
var a = new Value(2.0);
var b = new Value(-3.0);
var c = new Value(10.0);

// 2. 向前傳遞 (Forward Pass): d = a * b + c
// 數學上：d = 2 * (-3) + 10 = 4
var d = a * b + c;

// 3. 執行自動微分 (Backward Pass)
d.Backward();

// 4. 查看結果
Console.WriteLine($"Result d: {d.Data}"); // 輸出 4
Console.WriteLine($"a 的梯度: {a.Grad}"); // 輸出 -3 (因為 d 對 a 的微分是 b)
Console.WriteLine($"b 的梯度: {b.Grad}"); // 輸出 2  (因為 d 對 b 的微分是 a)
Console.WriteLine($"c 的梯度: {c.Grad}"); // 輸出 1  (因為 d 對 c 的微分是 1)

TestMLP();
void TestMLP()
{
    // 1. 建立一個多樣化的數據集
    var samples = new List<(List<double> x, double y)> 
    {
        (new() {1.0, 1.0}, 2.0),
        (new() {2.0, 2.0}, 4.0),
        (new() {5.0, 5.0}, 10.0),
        (new() {0.0, 3.0}, 3.0),
        (new() {4.0, 1.0}, 5.0)
    };

    var model = new MLP(2, new int[] { 4, 1 });
    double learningRate = 0.01;

    // 2. 訓練迴圈：每一輪都要跑遍所有的題目
    for (int i = 0; i < 1000; i++)
    {
        double totalLoss = 0;
        foreach (var sample in samples)
        {
            // 轉換為 Value 物件
            var inputs = sample.x.Select(v => new Value(v)).ToList();

            // Forward
            var pred = model.Forward(inputs)[0];

            // Loss
            var loss = (pred + new Value(-sample.y)) * (pred + new Value(-sample.y));
            totalLoss += loss.Data;

            // Backward
            foreach (var p in model.Parameters()) p.Grad = 0;
            loss.Backward();

            // Update 
            foreach (var p in model.Parameters())
                p.Data -= learningRate * p.Grad;
        }

        if (i % 200 == 0) Console.WriteLine($"Step {i}: Total Loss = {totalLoss:F4}");
    }

    var res = model.Forward(new List<Value> { new Value(55.0), new Value(44.0) })[0];
    Console.WriteLine($"測試 {a} + {b} = {res.Data:F2}");
}

void Test11()
{
    var n = new Neuron(2);
    var inputs = new List<Value> { new(1.0), new(1.0) };
    double target = 2.0; // 目標答案

    for (int i = 0; i < 9999; i++)
    {
        // A. 前向傳遞：得到目前預測值
        var pred = n.Forward(inputs);

        // B. 計算損失 (Loss)：預測值跟目標差多少？（用平方差）
        // Loss = (pred - target)^2
        var diff = pred + new Value(-target);
        var loss = diff * diff;

        // C. 歸零梯度 (重要！否則梯度會累加)
        foreach (var p in n.Parameters()) p.Grad = 0;

        // D. 反向傳播 (自動微分)
        loss.Backward();

        // E. 更新參數 (梯度下降 Gradient Descent)
        // 往梯度的反方向走一小步（學習率 0.01）
        double learningRate = 0.01;
        foreach (var p in n.Parameters())
        {
            p.Data -= learningRate * p.Grad;
        }

        if (i % 10 == 0)
            Console.WriteLine($"Step {i}: Prediction = {pred.Data:F4}, Loss = {loss.Data:F4}");
    }

    // 最後結果
    var finalPred = n.Forward(inputs);
    Console.WriteLine($"Final Prediction: {finalPred.Data:F4}");
    var testInputs = new List<Value> { new(2.0), new(2.0) };
    var testPred = n.Forward(testInputs);
    Console.WriteLine($"Final Prediction: {finalPred.Data:F4}");

}