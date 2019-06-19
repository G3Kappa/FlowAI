using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace FlowAI
{
    class Program
    {
        private static async Task<(int Passed, int Total)> RunTest(string name, Task<bool> testToAwait, int passed_tests, int total_tests)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Console.Write($"{name}: ");
            bool ret = await testToAwait;
            Console.WriteLine($"{stopwatch.Elapsed.TotalSeconds:0.000}s. ({(ret ? "PASS" : "FAIL" )})");
            stopwatch.Stop();
            return (passed_tests + (ret ? 1 : 0), total_tests + 1);
        }

        static async Task Main(string[] _)
        {
            // In order for these tests to pass, they must all terminate and they must all return true.
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            int passed_tests = 0; int total_tests = 0;
            (passed_tests, total_tests) = await RunTest($"Test {total_tests + 1:00}: FlowConstant -> FlowBuffer ", TestConstToBuf(), passed_tests, total_tests);
            (passed_tests, total_tests) = await RunTest($"Test {total_tests + 1:00}: FlowInputJunction          ", TestInputJunctions1(), passed_tests, total_tests);
            (passed_tests, total_tests) = await RunTest($"Test {total_tests + 1:00}: SplittingFlowInputJunction ", TestSplittingInputJunctions1(), passed_tests, total_tests);
            (passed_tests, total_tests) = await RunTest($"Test {total_tests + 1:00}: SequentialFlowInputJunction", TestSequentialInputJunctions1(), passed_tests, total_tests);
            (passed_tests, total_tests) = await RunTest($"Test {total_tests + 1:00}: DropletMapper w/ PipeFlow()", TestMapWithPipe(), passed_tests, total_tests);
            (passed_tests, total_tests) = await RunTest($"Test {total_tests + 1:00}: FlowSensor (takes a while) ", TestSensors1(), passed_tests, total_tests);
            (passed_tests, total_tests) = await RunTest($"Test {total_tests + 1:00}: FlowMapper                 ", TestFlowMapper(), passed_tests, total_tests);
            (passed_tests, total_tests) = await RunTest($"Test {total_tests + 1:00}: FlowFilter                 ", TestFlowFilter(), passed_tests, total_tests);
            (passed_tests, total_tests) = await RunTest($"Test {total_tests + 1:00}: SplittingFlowOutputJunction", TestSplittingOutputJunctions1(), passed_tests, total_tests);
            (passed_tests, total_tests) = await RunTest($"Test {total_tests + 1:00}: ReducingFlowOutputJunction ", TestReducingOutputJunctions(), passed_tests, total_tests);
            (passed_tests, total_tests) = await RunTest($"Test {total_tests + 1:00}: Fibonacci                  ", TestFibonacciScenario(), passed_tests, total_tests);
            stopwatch.Stop();
            Console.WriteLine($"\n{passed_tests:00}/{total_tests:00} tests passed. Elapsed time    : {stopwatch.Elapsed.TotalSeconds:0.000}s. ({(passed_tests == total_tests ? "PASS" : "FAIL")})");
            Console.ReadKey();
        }

        static async Task<bool> TestConstToBuf()
        {
            // Create a constant producer that continously emits 5's
            var k = new FlowConstant<int>(5);
            // Create a buffer that stores up to ten ints
            var buf = new FlowBuffer<int>(capacity: 10);
            // Consume flow from the constant producer until the buffer is full, then close the faucet to tell the buffer to stop consuming
            await buf.ConsumeFlowUntilFull(k, k.Flow()).Collect();
            // Create a smaller buffer
            var smallBuf = new FlowBuffer<int>(capacity: 5);
            // Fill the smaller buffer from the larger one
            await smallBuf.ConsumeFlowUntilFull(buf, buf.Flow()).Collect();
            // The smaller buffer is now full of fives
            return smallBuf.Full
                && buf.Contents.Count == buf.Capacity - smallBuf.Capacity
                && smallBuf.Contents.All(x => x == 5);
        }
        static async Task<bool> TestInputJunctions1()
        {
            // Create a sequence producer that continously emits a pattern
            var p = new FlowSequence<int>(new[] { 1, 2, 3, 4, 5 });
            // Create two buffers with different sizes
            var bufA = new FlowBuffer<int>(capacity: 10);
            var bufB = new FlowBuffer<int>(capacity: 20);
            // Create a junction that copies its input to both buffers
            var pipe = new FlowInputJunction<int>(bufA, bufB);
            // Fill both buffers from the constant through the junction
            await pipe.ConsumeFlowUntilFull(p, p.Flow()).Collect();
            // Now both buffers contain a supersequence of: 1, 2, 3, 4, 5 ...
            return bufA.Full
                && bufB.Full
                && bufA.Contents.SequenceEqual(new[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 })
                && bufB.Contents.SequenceEqual(new[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 });
        }
        static async Task<bool> TestSplittingInputJunctions1()
        {
            // Create a sequence producer that continously emits a pattern
            var p = new FlowSequence<int>(new[] { 1, 2, 3, 4, 5 });
            // Create two buffers with different sizes
            var bufA = new FlowBuffer<int>(capacity: 10);
            var bufB = new FlowBuffer<int>(capacity: 20);
            // Create a splitter junction that distributes its input to both buffers
            var pipe = new SplittingFlowInputJunction<int>(bufA, bufB);
            // Fill both buffers from the constant through the splitter
            await pipe.ConsumeFlowUntilFull(p, p.Flow()).Collect();
            // Now both buffers contain a supersequence of: 1, 3, 5, 2, 4 ... 
            return bufA.Full
                && bufB.Full
                && !bufA.Contents.SequenceEqual(new[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 })
                && !bufB.Contents.SequenceEqual(new[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 });
        }
        static async Task<bool> TestSequentialInputJunctions1()
        {
            // Create a sequence producer that continously emits a pattern
            var p = new FlowSequence<int>(new[] { 1, 2, 3, 4, 5 });
            // Create two buffers with different sizes
            var bufA = new FlowBuffer<int>(capacity: 3);
            var bufB = new FlowBuffer<int>(capacity: 7);
            // Create a splitter junction that fills bufA and then bufB
            var pipe = new SequentialFlowInputJunction<int>(bufA, bufB);
            // Fill both buffers from the constant through the splitter
            await pipe.ConsumeFlowUntilFull(p, p.Flow()).Collect();
            // Now check that bufB got filled only after bufA was already full
            return bufA.Full
                && bufB.Full
                && bufA.Contents.SequenceEqual(new[] { 1, 2, 3 })
                && bufB.Contents.SequenceEqual(new[] { 4, 5, 1, 2, 3, 4, 5 });
        }
        static async Task<bool> TestSensors1()
        {
            // Create a sequence producer that continously emits a random pattern
            var p = new RandomFlowSequence<char>(
                getSymbol: (rng) => rng.Choose("ohlea".ToList()),
                sequenceLength: 5, 
                repeatSameSequence: false
            );
            // Create a flow sensor that matches a possible pattern
            var sensor = new FlowSensor<char>("hell".ToList(), onValue: 'Y', offValue: 'N');
            // Consume from the sequence generator until the pattern is matched
            await sensor.ConsumeFlowUntil(p, p.Flow(), () => sensor.Value == sensor.OnValue).Collect();
            // Now the sensor contains the matched pattern ("hello") and is set to its OnValue
            return sensor.Value == sensor.OnValue 
                && sensor.Contents.SequenceEqual("hell");
        }
        static async Task<bool> TestMapWithPipe()
        {
            // Create a sequence producer that continously emits a pattern
            var p = new FlowSequence<int>(new[] { 1, 2, 3, 4, 5 });
            // Create a mapper that transforms the sequence into a sequence of squares
            var map = new DropletMapper<int>(i => i * i);
            // Create a buffer to store the results
            var buf = new FlowBuffer<int>(5);
            // Collect the squared sequence into the buffer by having it consume from the map which is piped to the sequence.
            await buf.ConsumeFlowUntilFull(map, map.PipeFlow(p, p.Flow())).Collect();
            // Buf now contains: 1, 4, 9, 16, 25
            return buf.Full
                && buf.Contents.SequenceEqual(new[] { 1, 4, 9, 16, 25 });

        }
        static async Task<bool> TestSplittingOutputJunctions1()
        {
            // Create two sequences that continously emit two different patterns
            var seqA = new FlowSequence<char>(seq: new char[] { 'h', 'e', 'l', 'l', 'o' });
            var seqB = new FlowSequence<char>(seq: new char[] { 'W', 'O', 'R', 'L', 'D' });
            // Create a mapper that inverts the casing of any character that flows into it
            var map = new DropletMapper<char>(c => Char.ToUpper(c) == c ? Char.ToLower(c) : Char.ToUpper(c));
            // Create a receiving buffer for the resulting sequence
            var buf = new FlowBuffer<char>(capacity: 10);
            // Pipe the two sequences sequentially into the buffer with a splitting output junction
            // Here chunkSize: 5 means "take 5 droplets from A, then 5 from B, then 5 from A..."
            var pipe = new SplittingFlowOutputJunction<char>(chunkSize: 5, () => seqA.Flow(), () => seqB.Flow());
            // Collect the results into buf by consuming from map through which the splitter is being piped
            await buf.ConsumeFlowUntilFull(map, map.PipeFlow(pipe, pipe.Flow())).Collect();
            // Now the buffer contains: "HELLOworld"
            return buf.Full
                && buf.Contents.SequenceEqual("HELLOworld");
        }
        static async Task<bool> TestFlowMapper()
        {
            // Create a sequence with a simple pattern
            var p = new FlowSequence<int>(new[] { 1, 2, 3, 4, 5 });
            // Create a chunk mapper that replaces parts of the pattern
            var map = new FlowMapper<int>(chunk =>
            {
                // Notice how input and output sizes are decoupled
                // i.e. You can map a chunk of size 1 to a chunk of size 3 and vice-versa
                return chunk.SequenceReplace(new[] { 2, 3 }, new[] { 9, 9, 9 })
                            .SequenceReplace(new[] { 4, 5, 1 }, new[] { 8, 8 })
                            .ToArray();

            }, chunkSize: 3);
            // Collect the mapped sequence
            IProducerConsumerCollection<int> ret = await map.PipeFlow(p, p.Flow()).Collect(12);
            return ret.SequenceEqual(new[] { 1, 9, 9, 9, 8, 8, 9, 9, 9, 8, 8, 9 });
        }
        static async Task<bool> TestFlowFilter()
        {
            // Create a sequence with a simple pattern
            var seq = new FlowSequence<int>(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 });
            // Create a sink to store the filtered values
            var snk = new CyclicFlowBuffer<int>(10);
            // Create a filter that removes the sequence '2, 3, 4'
            var flt = new FlowFilter<int>(
                chunk => chunk.SequenceEqual(new[] { 2, 3, 4 }),
                chunkSize: 3,
                filterConsumer: snk
            );
            // Collect the filtered sequence
            IProducerConsumerCollection<int> ret = await flt.PipeFlow(seq, seq.Flow()).Collect(10);
            // And make sure that our buffer got the removed droplets
            return ret.SequenceEqual(new[] { 1, 5, 6, 7, 8, 9, 0, 1, 5, 6 })
                && snk.Contents.SequenceEqual(new[] { 2, 3, 4, 2, 3, 4 });
        }
        static async Task<bool> TestReducingOutputJunctions()
        {
            // Create a sequence with a simple pattern
            var seq = new FlowSequence<int>(new[] { 1, 2, 3 });
            // Create a sink to store the filtered values
            var snk = new CyclicFlowBuffer<int>(10);
            // Create a filter that removes the value '2'
            var flt = new FlowFilter<int>(
                chunk => chunk.SequenceEqual(new[] { 2 }),
                chunkSize: 1,
                filterConsumer: snk
            );
            // Use a reductor to merge flt and snk's flows
            var pipe = new ReducingFlowOutputJunction<int>(
                reduce: (a, b) => a + b,
                () => flt.PipeFlow(seq, seq.Flow()),
                () => snk.Flow()
            );
            IProducerConsumerCollection<int> ret = await pipe.Flow().Collect(10);
            // Now the pipe is doing the following on repeat: 
            // pull 1 from seq; pull 2 from seq, filter it into snk; pull 3 from seq and 2 from snk and reduce them
            return ret.SequenceEqual(new[] { 1, 5, 1, 5, 1, 5, 1, 5, 1, 5 })
                && snk.Empty;
        }
        static async Task<bool> TestFibonacciScenario()
        {
            // Create a buffer to store the fibonacci sequence
            var outBuf = new FlowBuffer<int>(10);
            // Create a machine that sums and returns the contents of its input buffer
            var fibonacci = new FlowMapper<int>(buf =>
            {
                if(buf.Length == 2) // eg. [ 1, 2 ]
                {
                    return new[] { buf[1], buf[0] + buf[1] }; // eg. [ 2, 3 ]
                }
                return buf;
            }, chunkSize: 2);
            // Seed the fibonacci machine with initial state: { 0, 1 }
            await fibonacci.ConsumeFlow(fibonacci, new[] { 0, 1 }.GetAsyncEnumerator()).Collect();
            // Create a complex junction that pipes every droplet back to fibonacci, which is the source, 
            // and to a splitter that then pipes 50% of those droplets to outBuf and discards the rest.
            var inPipe = new FlowInputJunction<int>(
                new SplittingFlowInputJunction<int>(  // This is because the fib. machine returns a stream of the form:
                    outBuf,                           // (1, 1), (1, 2), (2, 3), (3, 5), (5, 8), (8, 13) ...
                    new FlowBlackHole<int>()          // And we only want the first number for each pair
                ),                                    // 1, 1, 2, 3, 5, 8, 13: That's fibonacci's sequence!
                fibonacci
            );
            // Get the fib. machine flowing and keep piping its output into inPipe until outBuf is full
            await inPipe.ConsumeFlowUntil(fibonacci, fibonacci.Flow(), () => outBuf.Contents.Count == outBuf.Capacity).Collect();
            // Now outBuf contains the fibonacci sequence starting from 1!
            return outBuf.Full
                && outBuf.Contents.SequenceEqual(new[] { 1, 1, 2, 3, 5, 8, 13, 21, 34, 55 });
        }
    }
}
