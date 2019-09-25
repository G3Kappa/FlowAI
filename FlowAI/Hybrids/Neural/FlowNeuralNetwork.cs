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
        public FlowNeuralNetwork(int nInputs, (int Neurons, ActivationFunction Activation)[] layerDef, double learningRate, int trainingEpochs) 
            : base(null, (i, o) => i.All(x => x.Length == nInputs), nInputs)
        {
            if(layerDef == null || layerDef.Length == 0)
            {
                throw new ArgumentException(nameof(layerDef));
            }

            InputBufferStrategy = InputBufferFullStrategy.Empty;

            TrainingBuffer = new FlowBuffer<(double[], double[])>();
            TrainingBufferLearningRate = learningRate;
            TrainingBufferEpochs = trainingEpochs;

            Layers = layerDef.Select((n, i) => new FlowNeuronLayer(i == 0 ? nInputs : layerDef[i - 1].Neurons, layerDef[i].Neurons, learningRate, trainingEpochs, layerDef[i].Activation))
                .ToArray();


            Map = inputs =>
            {
                var inPipe = Layers.First().PipeFlow(inputs);
                var last = inPipe;
                for (int i = 1; i < Layers.Length; i++)
                {
                    last = Layers[i].PipeFlow(last);
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
            var inputs = new List<double[]>();
            var outputs = new List<double[]>();
            var errors = new List<double[]>();

            var toTrain = new List<(double[], double[])>();

            for (int _epoch = 0; _epoch < epochs; _epoch++)
            {
                foreach (var (InputExample, OutputExample) in arr)
                {
                    TotalTimesTrained++;
                    // Feedforward step: predict the result (and keep a list of inputs/outputs for each layer).
                    inputs.Add(InputExample);
                    for (int j = 0; j < Layers.Length; j++)
                    {
                        var output = (await Layers[j].PipeFlow(new[] { inputs.Last() }.GetAsyncEnumerator()).Collect()).Single().ToArray();
                        outputs.Add(output);
                        inputs.Add(output);
                    }
                    inputs.RemoveAt(inputs.Count - 1);
                    // Backpropagation step: calcualate the error for the output of the network and propagate it backwards
                    var outError = Layers.Last().Error((inputs.Last(), OutputExample));
                    errors.Add(outError);
                    for (int l = Layers.Length - 2; l >= 0; l--)
                    {
                        // Multiply the activation of each neuron by the sum of the partial derivative of the weights
                        var err = outputs[l].Select((o, _o) =>
                                Layers[l].Neurons[_o].Activation.Call(o, derivative: true) 
                                * Layers[l + 1].Neurons.Select((n, _n) =>
                                    n.Weights[_o + 1] * errors.Last()[_n])
                                .Sum())
                        .ToArray();
                        errors.Add(err);
                    }
                    errors.Reverse();
                    // Learning step: adjust the weights (deferred as to not affect the outcome for this batch)
                    for (int i = 0; i < Layers.Length; i++)
                    {
                        toTrain.Add((inputs[i], errors[i]));
                    }
                    inputs.Clear();
                    outputs.Clear();
                    errors.Clear();
                }
                // (Learning step)
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
