using FlowAI.Consumers.Plumbing;
using FlowAI.Hybrids.Buffers;
using FlowAI.Hybrids.Machines;
using FlowAI.Producers.Plumbing;
using System;
using System.Collections.Async;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace FlowAI.Hybrids.Neural
{
    /// <summary>
    /// A parallel arrangement of FlowNeurons, to be used in a FlowNeuronNetwork.
    /// </summary>
    public class FlowNeuronLayer : FlowTransformer<double[], double[]>
    {
        public FlowBuffer<(double[], double[])> TrainingBuffer { get; protected set; }

        public int TrainingBufferEpochs { get; private set; }
        public double TrainingBufferLearningRate { get; private set; }
        public int TotalTimesTrained { get; private set; }
        public FlowNeuron[] Neurons { get; }

        internal void AdjustWeights(double[] input, double[] error, double learningRate)
        {
            for (int i = 0; i < Neurons.Length; i++)
            {
                Neurons[i].AdjustWeights(input, error[i], learningRate);
            }
        }

        public FlowNeuronLayer(int nInputs, int nNeurons, double learningRate, int trainingEpochs, Func<double, double> activation = null)
            : base(null, (i, o) => i.Length == nNeurons, 1)
        {
            TrainingBuffer = new FlowBuffer<(double[], double[])>();
            TrainingBufferLearningRate = learningRate;
            TrainingBufferEpochs = trainingEpochs;

            Neurons = Enumerable.Range(0, nNeurons)
                .Select(_ => new FlowNeuron(nInputs, activation, trainingEpochs, learningRate))
                .ToArray();

            var inputJ = new FlowInputJunction<double[]>(Neurons);
            var outputJ = new MergingFlowOutputJunction<double>(Neurons.Select(n => (Func<IAsyncEnumerator<double>>)(() => n.Flow())).ToArray());

            Map = inputs =>
               {
                   var arr = inputJ.ConsumeFlow(this, inputs.GetAsyncEnumerator())
                    .Select(_ => outputJ.Drip().GetAwaiter().GetResult())
                    .Collect()
                    .GetAwaiter()
                    .GetResult()
                    .ToArray();
                   return arr;
               };
        }
        public IEnumerable<double> Train((double[] input, double[] output) data, double learningRate = 1)
        {
            TotalTimesTrained ++;
            for (int j = 0; j < Neurons.Length; j++)
            {
                yield return Neurons[j].Train((data.Item1, data.Item2[j]), learningRate);
            }
        }
        public IEnumerable<double[]> Train(IEnumerable<(double[], double[])> dataset, double learningRate = 1)
        {
            foreach (var d in dataset)
            {
                yield return Train(d, learningRate).ToArray();
            }
        }
        public IEnumerable<double[][]> Train(IEnumerable<(double[], double[])> dataset, int epochs, double learningRate = 1)
        {
            for (int i = 0; i < epochs; i++)
            {
                yield return Train(dataset, learningRate).ToArray();
            }
        }


        public override async Task Update(FlowBuffer<double[]> inBuf, FlowBuffer<double[]> outBuf)
        {
            if (!TrainingBuffer.Empty)
            {
                var dataset = await TrainingBuffer.Flow().Collect();
                for (int i = 0; i < TrainingBufferEpochs; i++)
                {
                    _ = Train(dataset, TrainingBufferLearningRate)
                        .ToArray();
                }
            }
            await base.Update(inBuf, outBuf);
        }
    }
}
