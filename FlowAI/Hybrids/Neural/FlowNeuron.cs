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

        public static double SigmoidDerivative(double x)
        {
            var fX = Sigmoid(x);
            return fX * (1 - fX);
        }
    }

    /// <summary>
    /// A simple neuron that can be used to train a binary classifier.
    /// It needs to be trained on some data first. After that, its flow can be used to make predictions.
    /// </summary>
    public class FlowNeuron : FlowTransformer<double[], double>
    {
        public double[] Weights { get; private set; }

        public Func<double, double> ActivationFunction { get; protected set; }

        /// <summary>
        /// If the training buffer contains a droplet, the next input data will be considered an example with that droplet as the known answer.
        /// </summary>
        public FlowBuffer<(double[], double)> TrainingBuffer { get; protected set; }
        public int TrainingBufferEpochs { get; set; }
        public double TrainingBufferLearningRate { get; set; }
        public int TotalTimesTrained { get; private set; }

        public int OutputsReady => OutputBuffer.Contents.Count;

        internal void AdjustWeights(double[] input, double error, double learningRate)
        {
            Weights = new[] { Weights[0] + error * learningRate }.Concat(Weights.Skip(1).Select((w, wi) => w + learningRate * error * input[wi])).ToArray();
        }

        /// <summary>
        /// Trains the neuron on a dataset so that it can be used in a flow network.
        /// </summary>
        /// <param name="dataset">A list of tuples containing the input->output examples for this dataset.</param>
        /// <param name="epochs">The number of epochs to train for.</param>
        /// <param name="learningRate">The learning rate coefficient.</param>
        /// <returns>The total error</returns>
        public double Train(IEnumerable<(double[], double)> dataset, int epochs = 1, double learningRate = 1)
        {
            double globalError = 0;
            for (int i = 0; i < epochs; i++)
            {
                foreach ((double[] input, double target) in dataset)
                {
                    TotalTimesTrained++;
                    double prediction = Activate(input)[0]; // Can be 1 or 0
                    var error = (target - prediction) * ActivationFunctions.SigmoidDerivative(prediction);
                    AdjustWeights(input, error, learningRate);
                    globalError += error;
                }
            }
            return globalError;
        }

        protected double[] Activate(double[] input)
        {
            double weightedSum = new[] { 1.0 }.Concat(input).Select((v, i) => Weights[i] * v).Sum();
            return new[] { ActivationFunction(weightedSum) };
        }

        public override async Task Update(FlowBuffer<double[]> inBuf, FlowBuffer<double> outBuf)
        {
            if(!TrainingBuffer.Empty)
            {
                var dataset = await TrainingBuffer.Flow().Collect();
                Train(dataset, TrainingBufferEpochs, TrainingBufferLearningRate);
            }
            await base.Update(inBuf, outBuf);
        }

        private void InitializeWeights(double[] w)
        {
            var rng = new Random();
            for (int i = 0; i < w.Length; i++)
            {
                w[i] = rng.NextDouble();
            }
        }

        public FlowNeuron(int nInputs, Func<double, double> activation = null, int bufferEpochs = 1, double bufferLearningRate = 1.0) 
            : base(null, (i, o) => i.Length == 1, nInputs)
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
