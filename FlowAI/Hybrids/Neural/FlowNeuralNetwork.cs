using FlowAI.Hybrids.Buffers;
using FlowAI.Hybrids.Machines;
using FlowAI.Producers;
using System;
using System.Collections.Async;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace FlowAI.Hybrids.Neural
{

    /// <summary>
    /// A network of FlowNeurons arranged into an input layer, N hidden layers, and an output layer.
    /// </summary>
    public class FlowNeuralNetwork : FlowTransformer<double[], double[]>
    {
        public FlowNeuronLayer[] Layers { get; set; }
        public FlowBuffer<(double[], double[])> TrainingBuffer { get; protected set; }
        public int TrainingBufferEpochs { get; private set; }
        public double TrainingBufferLearningRate { get; private set; }
        public int TotalTimesTrained { get; private set; }

        internal void AdjustWeights(double[] input, double[][] error, double learningRate)
        {
            for (int i = 0; i < Layers.Length; i++)
            {
                Layers[i].AdjustWeights(input, error[i], learningRate);
            }
        }

        public FlowNeuralNetwork(int nInputs, int[] nNeurons, double learningRate, int trainingEpochs, Func<double, double> activation = null) 
            : base(null, (i, o) => i.Length == nNeurons.Last(), nInputs)
        {
            if(nNeurons == null || nNeurons.Length == 0)
            {
                throw new ArgumentException(nameof(nNeurons));
            }

            TrainingBuffer = new FlowBuffer<(double[], double[])>();
            TrainingBufferLearningRate = learningRate;
            TrainingBufferEpochs = trainingEpochs;

            Layers = nNeurons.Select((n, i) => new FlowNeuronLayer(i == 0 ? nInputs : nNeurons[i - 1], nNeurons[i], learningRate, trainingEpochs, activation))
                .ToArray();


            Map = inputs =>
            {
                var inPipe = Layers.First().PipeFlow(this, inputs);
                var last = inPipe;
                for (int i = 1; i < Layers.Length; i++)
                {
                    last = Layers[i].PipeFlow(this, last);
                }
                return last
                    .Collect()
                    .GetAwaiter()
                    .GetResult()
                    .ToArray();
            };
        }

        private double[] ElementwiseSum(double[] a, double[] b)
        {
            var l = Math.Min(a.Length, b.Length);
            double[] ret = new double[Math.Max(a.Length, b.Length)];
            for (int i = 0; i < l; i++)
            {
                ret[i] = a[i] + b[i];
            }
            for (int i = 0; i < ret.Length - l; i++)
            {
                ret[i] = b[i];
            }
            return ret;
        }

        public async Task Train(IEnumerable<(double[] Input, double[] Output)> dataset, int epochs = 1, double learningRate = 1)
        {
            var arr = dataset.ToArray();
            var inputs = new List<double[]>();
            var outputs = new List<double[]>();
            var errors = new List<double[]>();

            var toTrain = new List<(double[], double[])>();

            for (int _epoch = 0; _epoch < epochs; _epoch++)
            {
                foreach (var d in arr)
                {
                    TotalTimesTrained++;
                    /*
                     WARNING: TODO: TO COMPLETE
                     */
                    // Feedforward step
                    inputs.Add(d.Input);
                    for (int j = 0; j < Layers.Length; j++)
                    {
                        var output = (await Layers[j].PipeFlow(this, new[] { inputs.Last() }.GetAsyncEnumerator()).Collect()).Single().ToArray();
                        outputs.Add(output);
                        inputs.Add(output);
                    }
                    inputs.RemoveAt(inputs.Count - 1);
                    // Backpropagation step
                    var outError = outputs.Last().Select((o, _o) => (d.Output[_o] - o) * ActivationFunctions.SigmoidDerivative(o)).ToArray();
                    errors.Add(outError);
                    for (int l = Layers.Length - 2; l >= 0; l--)
                    {
                        var err = outputs[l].Select((o, _o) =>
                                ActivationFunctions.SigmoidDerivative(o) 
                                * Layers[l + 1].Neurons.Select((n, _n) =>
                                    n.Weights[_o + 1] * errors.Last()[_n])
                                .Sum())
                        .ToArray();
                        errors.Add(err);
                    }
                    errors.Reverse();
                    // Learning step
                    for (int i = 0; i < Layers.Length; i++)
                    {
                        toTrain.Add((inputs[i], errors[i]));
                    }
                    inputs.Clear();
                    outputs.Clear();
                    errors.Clear();
                }
                for (int i = 0; i < toTrain.Count; i++)
                {
                    Layers[i % Layers.Length].AdjustWeights(toTrain[i].Item1, toTrain[i].Item2, learningRate);
                }
                toTrain.Clear();
            }
        }

        public override async Task Update(FlowBuffer<double[]> inBuf, FlowBuffer<double[]> outBuf)
        {
            if (!TrainingBuffer.Empty)
            {
                var dataset = await TrainingBuffer.Flow().Collect();
                await Train(dataset, TrainingBufferEpochs, TrainingBufferLearningRate);
            }
            await base.Update(inBuf, outBuf);
        }
    }
}
