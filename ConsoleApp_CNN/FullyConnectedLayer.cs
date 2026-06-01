using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp_CNN
{
    public class FullyConnectedLayer
    {
        private double[,] weights;
        private double[] biases;
        private int inputSize, outputSize;
        private double lr = 0.01f;

        public FullyConnectedLayer(int inputSize, int outputSize)
        {
            this.inputSize = inputSize;
            this.outputSize = outputSize;
            weights = new double[outputSize, inputSize];
            biases = new double[outputSize];
            Random rand = new Random();
            for (int i = 0; i < outputSize; i++)
                for (int j = 0; j < inputSize; j++)
                    weights[i, j] = rand.NextDouble() * 0.01;
        }

        public double[] Forward(double[] input)
        {
            double[] logits = new double[outputSize];
            for (int i = 0; i < outputSize; i++)
            {
                double sum = biases[i];
                for (int j = 0; j < inputSize; j++)
                    sum += weights[i, j] * input[j];
                logits[i] = sum;
            }
            return Softmax(logits);
        }

        public void Backward(float[] input, float[] predicted, int label)
        {
            float[] grad = new float[outputSize];
            for (int i = 0; i < outputSize; i++)
                grad[i] = predicted[i] - (i == label ? 1 : 0);

            for (int i = 0; i < outputSize; i++)
            {
                for (int j = 0; j < inputSize; j++)
                    weights[i, j] -= lr * grad[i] * input[j];
                biases[i] -= lr * grad[i];
            }
        }

        private double[] Softmax(double[] logits)
        {
            var maxLogit = logits.Max();
            double sumExp = 0;
            double[] expVals = new double[logits.Length];
            for (int i = 0; i < logits.Length; i++)
            {
                expVals[i] = Math.Exp(logits[i] - maxLogit);
                sumExp += expVals[i];
            }
            for (int i = 0; i < logits.Length; i++)
                expVals[i] /= sumExp;
            return expVals;
        }
    }
}
