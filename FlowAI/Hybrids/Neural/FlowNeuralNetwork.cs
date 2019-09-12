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

        public async Task Train(IEnumerable<(double[] Input, double[] Output)> dataset, int epochs = 1, double learningRate = 1)
        {
            var arr = dataset.ToArray();
            var perLayerError = new List<double[]>();
            var inputs = new List<double[]>();
            var outputs = new List<double[]>();
            for (int i = 0; i < epochs; i++)
            {
                TotalTimesTrained += arr.Length;
                foreach (var d in arr)
                {
                    /*
                     WARNING: TODO: TO COMPLETE
                     */
                    // Prediction step
                    inputs.Add(d.Input);
                    for (int j = 0; j < Layers.Length; j++)
                    {
                        var input = inputs.Last();
                        var output = (await Layers[j].PipeFlow(this, new[] { input }.GetAsyncEnumerator()).Collect()).Single().ToArray();
                        outputs.Add(output);
                        inputs.Add(output);
                    }
                    inputs.RemoveAt(inputs.Count - 1);
                    // Error for the datum's output and the last layer's output
                    var err = d.Output.Select((o, _o) => (o - inputs.Last()[_o]) * ActivationFunctions.SigmoidDerivative(inputs.Last()[_o])).ToArray();
                    // Backpropagation step
                    Layers.Last().AdjustWeights(inputs[inputs.Count - 1], err, learningRate);
                    for (int j = Layers.Length - 2; j >= 0; j--)
                    {
                        err = outputs[j + 1].Select((o, _o) => o * err[_o]).ToArray();
                        Layers[j].AdjustWeights(inputs[j], err, learningRate);
                    }
                    inputs.Clear();
                    outputs.Clear();
                }
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
