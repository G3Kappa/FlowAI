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
    public class ActivationFunction
    {
        private Func<double, double> F { get; }
        private Func<double, double> G { get; }

        public double Call(double x, bool derivative)
        {
            return derivative ? G(x) : F(x);
        }

        public ActivationFunction(Func<double, double> f, Func<double, double> deriv)
        {
            F = f;
            G = deriv;
        }

        public static ActivationFunction Identity = new ActivationFunction(
            x => x,
            x => 1
        );

        public static ActivationFunction SigmoidLogistic = new ActivationFunction(
            x => 1.0 / (1 + Math.Exp(-x)),
            x => x * (1 - x)
        );

        public static ActivationFunction HyperbolicTangent = new ActivationFunction(
            x => Math.Tanh(x),
            x => 1 - Math.Pow(Math.Tanh(x), 2)
        );

        public static ActivationFunction Softplus = new ActivationFunction(
            x => Math.Log(1 + Math.Pow(Math.E, x)),
            x => 1 / (1 + Math.Pow(Math.E, -x))
        );

        public static ActivationFunction ReLU = new ActivationFunction(
            x => Math.Max(0, x),
            x => x > 0 ? 1 : 0
        );
    }

    /// <summary>
    /// A simple neuron that can be used to train a binary classifier.
    /// It needs to be trained on some data first. After that, its flow can be used to make predictions.
    /// </summary>
    public class FlowNeuron : FlowTransformer<double[], double>
    {
        internal static Random Rng { get; } = new Random(10);
        public double[] Weights { get; private set; }

        public ActivationFunction Activation { get; protected set; }

        /// <summary>
        /// If the training buffer contains a droplet, the next input data will be considered an example with that droplet as the known answer.
        /// </summary>
        public FlowBuffer<(double[], double)> TrainingBuffer { get; protected set; }
        public int TrainingBufferEpochs { get; set; }
        public double TrainingBufferLearningRate { get; set; }
        public int TotalTimesTrained { get; private set; }

        internal void AdjustWeights(double[] input, double error, double learningRate)
        {
            Weights = new[] { Weights[0] + error * learningRate }.Concat(Weights.Skip(1).Select((w, wi) => w + learningRate * error * input[wi])).ToArray();
        }

        public double Error((double[] input, double target) data)
        {
            double prediction = Activate(data.input)[0];
            return (data.target - prediction) * Activation.Call(prediction, derivative: true);
        }

        public double Train((double[] input, double target) data, double learningRate = 1)
        {
            double err = Error(data);
            AdjustWeights(data.input, err, learningRate);
            TotalTimesTrained++;
            return err;
        }
        public IEnumerable<double> Train(IEnumerable<(double[], double)> dataset, double learningRate = 1)
        {
            foreach (var d in dataset)
            {
                yield return Train(d, learningRate);
            }
        }
        public IEnumerable<double[]> Train(IEnumerable<(double[], double)> dataset, int epochs, double learningRate = 1)
        {
            for (int i = 0; i < epochs; i++)
            {
                yield return Train(dataset, learningRate).ToArray();
            }
        }
        protected double[] Activate(double[] input)
        {
            double weightedSum = new[] { 1.0 }.Concat(input).Select((v, i) => Weights[i] * v).Sum();
            return new[] { Activation.Call(weightedSum, derivative: false) };
        }
        public override async Task Update(FlowBuffer<double[]> inBuf, FlowBuffer<double> outBuf)
        {
            if(!TrainingBuffer.Empty)
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
        private void InitializeWeights(double[] w)
        {
            for (int i = 0; i < w.Length; i++)
            {
                w[i] = Rng.NextDouble() * 2 - 1;
            }
        }
        public FlowNeuron(int nInputs, ActivationFunction activation = null, int bufferEpochs = 1, double bufferLearningRate = 1.0) 
            : base(null, (i, o) => i.Length == 1, nInputs)
        {
            Weights = new double[nInputs + 1];
            InitializeWeights(Weights);

            Activation = activation ?? ActivationFunction.HyperbolicTangent;
            Map = inputs => inputs.SelectMany(i => Activate(i)).ToArray();

            TrainingBuffer = new FlowBuffer<(double[], double)>();
            TrainingBufferEpochs = bufferEpochs;
            TrainingBufferLearningRate = bufferLearningRate;
        }
    }
}
