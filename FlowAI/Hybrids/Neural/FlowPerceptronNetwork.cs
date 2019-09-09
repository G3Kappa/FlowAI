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
    /// A network of FlowPerceptrons arranged into an input layer, N hidden layers, and an output layer.
    /// </summary>
    public class FlowPerceptronNetwork : FlowTransformer<double[], double[]>
    {
        public FlowPerceptronLayer[] Layers { get; set; }
        public FlowBuffer<(double[], double[])> TrainingBuffer { get; protected set; }
        public int TrainingBufferEpochs { get; private set; }
        public double TrainingBufferLearningRate { get; private set; }
        public int TotalTimesTrained { get; private set; }

        public FlowPerceptronNetwork(int nInputs, int[] nNeurons, double learningRate, int trainingEpochs, Func<double, double> activation = null) 
            : base(null, (i, o) => i.Length == nNeurons.Last(), nInputs)
        {
            if(nNeurons == null || nNeurons.Length == 0)
            {
                throw new ArgumentException(nameof(nNeurons));
            }

            TrainingBuffer = new FlowBuffer<(double[], double[])>();
            TrainingBufferLearningRate = learningRate;
            TrainingBufferEpochs = trainingEpochs;

            Layers = nNeurons.Select((n, i) => new FlowPerceptronLayer(i == 0 ? nInputs : nNeurons[i - 1], nNeurons[i], learningRate, trainingEpochs, activation))
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
            for (int i = 0; i < epochs; i++)
            {
                TotalTimesTrained += arr.Length;
                // Backpropagation step
                for (int j = 0; j < Layers.Length; j++)
                {
                    //var predictions = await Layers[j].PipeFlow(this, dataset.Select(d => d.Input).GetAsyncEnumerator()).Collect();

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
