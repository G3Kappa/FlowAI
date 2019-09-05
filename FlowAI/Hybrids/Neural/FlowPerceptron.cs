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
    public static class ActivationFunctions
    {
        public static double Sigmoid(double x)
        {
            return 1.0 / (1 + Math.Pow(Math.E, -x));
        }
    }

    /// <summary>
    /// A simple perceptron that can be used to train a binary classifier.
    /// It needs to be trained on some data first. After that, its flow can be used to make predictions.
    /// </summary>
    public class FlowPerceptron : FlowTransformer<double[], double>
    {
        public double[] Weights { get; private set; }

        public Func<double, double> ActivationFunction { get; protected set; }

        /// <summary>
        /// If the training buffer contains a droplet, the next input data will be considered an example with that droplet as the known answer.
        /// </summary>
        public FlowBuffer<(double[], double)> TrainingBuffer { get; protected set; }
        public int TrainingBufferEpochs { get; private set; }
        public double TrainingBufferLearningRate { get; private set; }

        public int TotalTimesTrained { get; private set; }

        /// <summary>
        /// Trains the perceptron on a dataset so that it can be used in a flow network.
        /// </summary>
        /// <param name="dataset">A list of tuples containing the input->output examples for this dataset.</param>
        /// <param name="epochs">The number of epochs to train for.</param>
        /// <param name="learningRate">The learning rate coefficient.</param>
        public void Train(IEnumerable<(double[], double)> dataset, int epochs = 1, double learningRate = 1)
        {
            for (int i = 0; i < epochs; i++)
            {
                foreach ((double[] input, double output) in dataset)
                {
                    TotalTimesTrained++;
                    double prediction = Activate(input)[0]; // Can be 1 or 0
                    if(output > prediction)
                    {
                        Weights = new[] { Weights[0] - learningRate }.Concat(Weights.Skip(1).Select((w, wi) => w + learningRate * input[wi])).ToArray();
                    }
                    else if(output < prediction)
                    {
                        Weights = new[] { Weights[0] + learningRate }.Concat(Weights.Skip(1).Select((w, wi) => w - learningRate * input[wi])).ToArray();
                    }
                }
            }
        }

        protected double[] Activate(double[] input)
        {
            double weightedSum = new[] { 1.0 }.Concat(input).Select((v, i) => Weights[i] * v).Sum();
            return new[] { ActivationFunction(weightedSum) >= Weights[0] ? 1.0 : 0.0 };
        }

        public override async Task Update(FlowBuffer<double[]> inBuf, FlowBuffer<double> outBuf)
        {
            await base.Update(inBuf, outBuf);
            if(!TrainingBuffer.Empty)
            {
                var dataset = await TrainingBuffer.Flow().Collect();
                Train(dataset, TrainingBufferEpochs, TrainingBufferLearningRate);
            }
        }

        private void InitializeWeights(double[] w)
        {
            var rng = new Random();
            for (int i = 0; i < w.Length; i++)
            {
                w[i] = rng.NextDouble();
            }
        }

        public FlowPerceptron(int nInputs, Func<double, double> activation = null, int bufferEpochs = 1, double bufferLearningRate = 1.0) : base(null, (i, o) => i.Length == nInputs, nInputs)
        {
            Weights = new double[nInputs + 1];
            InitializeWeights(Weights);

            ActivationFunction = activation ?? ActivationFunctions.Sigmoid;
            Map = inputs => inputs.SelectMany(i => Activate(i)).ToArray();

            TrainingBuffer = new FlowBuffer<(double[], double)>();
            TrainingBufferEpochs = bufferEpochs;
            TrainingBufferLearningRate = bufferLearningRate;
        }
    }
}
