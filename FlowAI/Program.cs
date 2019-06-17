﻿using System;
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
            bool ret = await testToAwait;
            Console.WriteLine($"{name}: {stopwatch.Elapsed.TotalSeconds:0.000}s. ({(ret ? "PASS" : "FAIL" )})");
            stopwatch.Stop();
            return (passed_tests + (ret ? 1 : 0), total_tests + 1);
        }

        static async Task Main(string[] _)
        {
            // In order for these tests to pass, they must all terminate and they must all return true.
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            int passed_tests = 0; int total_tests = 0;
            (passed_tests, total_tests) = await RunTest($"Test {total_tests:00}: Constant flow into buffer  ", TestConstToBuf(), passed_tests, total_tests);
            (passed_tests, total_tests) = await RunTest($"Test {total_tests:00}: Basic input junctions      ", TestInputJunctions1(), passed_tests, total_tests);
            (passed_tests, total_tests) = await RunTest($"Test {total_tests:00}: Splitting input junctions  ", TestSplittingInputJunctions1(), passed_tests, total_tests);
            (passed_tests, total_tests) = await RunTest($"Test {total_tests:00}: Sensors (may last a bit)   ", TestSensors1(), passed_tests, total_tests);
            (passed_tests, total_tests) = await RunTest($"Test {total_tests:00}: Mapping squares w/ PipeFlow", TestMapWithPipe(), passed_tests, total_tests);
            (passed_tests, total_tests) = await RunTest($"Test {total_tests:00}: Splitting output junctions ", TestSplittingOutputJunctions1(), passed_tests, total_tests);
            (passed_tests, total_tests) = await RunTest($"Test {total_tests:00}: Chunk mapping of a sequence", TestChunkMap(), passed_tests, total_tests);
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
            return smallBuf.Contents.Count == smallBuf.Capacity
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
            return bufA.Contents.Count == bufA.Capacity
                && bufB.Contents.Count == bufB.Capacity
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
            return bufA.Contents.Count == bufA.Capacity
                && bufB.Contents.Count == bufB.Capacity
                && !bufA.Contents.SequenceEqual(new[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 })
                && !bufB.Contents.SequenceEqual(new[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 });
        }
        static async Task<bool> TestSensors1()
        {
            // Create a sequence producer that continously emits a random pattern
            var p = new RandomFlowSequence<char>(symbols: new char[] { 'h', 'l', 'o', 'e' }, sequenceLength: 5, repeatSameSequence: false);
            // Create a flow sensor that matches a possible pattern
            var sensor = new FlowSensor<char>(new[] { 'h', 'e', 'l', 'l', 'o' }.ToList(), onValue: 'Y', offValue: 'N');
            // Consume from the sequence generator until the pattern is matched
            await sensor.ConsumeFlowUntil(p, p.Flow(), () => sensor.Value == sensor.OnValue).Collect();
            // Now the sensor contains the matched pattern ("hello") and is set to its OnValue
            return sensor.Value == sensor.OnValue 
                && sensor.Contents.SequenceEqual("hello");
        }
        static async Task<bool> TestMapWithPipe()
        {
            // Create a sequence producer that continously emits a pattern
            var p = new FlowSequence<int>(new[] { 1, 2, 3, 4, 5 });
            // Create a mapper that transforms the sequence into a sequence of squares
            var map = new DropletFlowMapper<int>(i => i * i);
            // Create a buffer to store the results
            var buf = new FlowBuffer<int>(5);
            // Collect the squared sequence into the buffer by having it consume from the map which is piped to the sequence.
            await buf.ConsumeFlowUntilFull(map, map.PipeFlow(p, p.Flow())).Collect();
            // Buf now contains: 1, 4, 9, 16, 25
            return buf.Contents.Count == buf.Capacity
                && buf.Contents.SequenceEqual(new[] { 1, 4, 9, 16, 25 });

        }
        static async Task<bool> TestSplittingOutputJunctions1()
        {
            // Create two sequences that continously emit two different patterns
            var seqA = new FlowSequence<char>(seq: new char[] { 'h', 'e', 'l', 'l', 'o' });
            var seqB = new FlowSequence<char>(seq: new char[] { 'W', 'O', 'R', 'L', 'D' });
            // Create a mapper that inverts the casing of any character that flows into it
            var map = new DropletFlowMapper<char>(c => Char.ToUpper(c) == c ? Char.ToLower(c) : Char.ToUpper(c));
            // Create a receiving buffer for the resulting sequence
            var buf = new FlowBuffer<char>(capacity: 10);
            // Pipe the two sequences sequentially into the buffer with a splitting output junction
            // Here chunkSize: 5 means "take 5 droplets from B, then 5 from A, then 5 from B..."
            var pipe = new SplittingFlowOutputJunction<char>(chunkSize: 5, seqB, seqA);
            // Collect the results into buf by consuming from map through which the splitter is being piped
            await buf.ConsumeFlowUntilFull(map, map.PipeFlow(pipe, pipe.Flow())).Collect();
            // Now the buffer contains: "HELLOworld"
            return buf.Contents.Count == buf.Capacity
                && buf.Contents.SequenceEqual("HELLOworld");
        }
        static async Task<bool> TestChunkMap()
        {
            // Create a sequence with a simple pattern
            var p = new FlowSequence<int>(new[] { 1, 2, 3, 4, 5 });
            // Create a chunk mapper that replaces parts of the pattern
            var map = new ChunkFlowMapper<int>(chunk =>
            {
                // Notice how input and output sizes are decoupled
                // i.e. You can map a chunk of size 1 to a chunk of size 3 and vice-versa
                return chunk.SequenceReplace(new[] { 2, 3 }, new[] { 9, 9, 9 })
                            .SequenceReplace(new[] { 4, 5, 1 }, new[] { 8, 8 })
                            .ToArray();

            }, chunkSize: 3);
            // Collect the mapped sequence
            var ret = await map.PipeFlow(p, p.Flow()).Collect(12);
            return ret.SequenceEqual(new[] { 1, 9, 9, 9, 8, 8, 9, 9, 9, 8, 8, 9 });
        }
    }
}