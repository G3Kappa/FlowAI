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
    /// A parallel arrangement of FlowPerceptrons, to be used in a FlowPerceptronNetwork.
    /// </summary>
    public class FlowPerceptronLayer : FlowTransformer<double[], double[]>
    {
        public FlowBuffer<(double[], double[])> TrainingBuffer { get; protected set; }

        public int TrainingBufferEpochs { get; private set; }
        public double TrainingBufferLearningRate { get; private set; }
        public int TotalTimesTrained { get; private set; }
        public FlowPerceptron[] Neurons { get; }

        public FlowPerceptronLayer(int nInputs, int nNeurons, double learningRate, int trainingEpochs, Func<double, double> activation = null)
            : base(null, (i, o) => i.Length == nNeurons, 1)
        {
            TrainingBuffer = new FlowBuffer<(double[], double[])>();
            TrainingBufferLearningRate = learningRate;
            TrainingBufferEpochs = trainingEpochs;

            Neurons = Enumerable.Range(0, nNeurons)
                .Select(_ => new FlowPerceptron(nInputs, activation, trainingEpochs, learningRate))
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
        public double[] Train(IEnumerable<(double[], double[])> dataset, int epochs = 1, double learningRate = 1)
        {
            var errors = new double[Neurons.Length];
            TotalTimesTrained += epochs * dataset.Count();
            for (int j = 0; j < Neurons.Length; j++)
            {
                errors[j] = Neurons[j].Train(dataset.Select(d => (d.Item1, d.Item2[j])), epochs, learningRate);
            }
            return errors;
        }

        public override async Task Update(FlowBuffer<double[]> inBuf, FlowBuffer<double[]> outBuf)
        {
            if (!TrainingBuffer.Empty)
            {
                var dataset = await TrainingBuffer.Flow().Collect();
                Train(dataset, TrainingBufferEpochs, TrainingBufferLearningRate);
            }
            await base.Update(inBuf, outBuf);
        }
    }
}
