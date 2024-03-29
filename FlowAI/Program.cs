﻿using FlowAI.Consumers.Plumbing;
using FlowAI.Hybrids.Adapters;
using FlowAI.Hybrids.Buffers;
using FlowAI.Hybrids.Machines;
using FlowAI.Hybrids.Neural;
using FlowAI.Hybrids.Sensors;
using FlowAI.Producers;
using FlowAI.Producers.Plumbing;
using FlowAI.Producers.Sequences;
using System;
using System.Collections.Async;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;


namespace FlowAI
{
    [AttributeUsage(AttributeTargets.Method)]
    class TestAttribute : Attribute
    {
        public string Description { get; }
        public string Suite { get; set; } = "Default";
        public ConsoleColor Color { get; set; } = ConsoleColor.Gray;

        public TestAttribute(string desc)
        {
            Description = desc;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    class RepeatAttribute : Attribute
    {
        /// <summary>
        /// How many times to repeat this test.
        /// </summary>
        public int Times { get; }
        /// <summary>
        /// How many times to repeat this test when the debugger is attached.
        /// </summary>
        public int DebugTimes { get; set; }
        public RepeatAttribute(int times)
        {
            Times = times;
            DebugTimes = times;
        }
    }


    [AttributeUsage(AttributeTargets.Method)]
    class MayFailAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    class SkipAttribute : Attribute { }

    public class Benchmarks
    {
        private static T DebugPrint<T>(T obj, string template = "{0}", params object[] args)
        {
#if DEBUG
            Console.WriteLine("\t" + template, new object[] { obj }.Union(args).ToArray());
#endif
            return obj;
        }

        private static async Task<(int Passed, int Total)> RunTest(string name, MethodInfo testToAwait, int passed_tests, int total_tests)
        {
            var stopwatch = new Stopwatch();

            var repeatAttr = (RepeatAttribute)testToAwait.GetCustomAttributes(typeof(RepeatAttribute), false).FirstOrDefault();
            var testAttr = (TestAttribute)testToAwait.GetCustomAttributes(typeof(TestAttribute), false).FirstOrDefault();

            int repeat = repeatAttr != null ? (Debugger.IsAttached ? repeatAttr.DebugTimes : repeatAttr.Times) : 0;
            bool mayFail = testToAwait.GetCustomAttributes(typeof(MayFailAttribute), false).FirstOrDefault() != null;
            bool skip = testToAwait.GetCustomAttributes(typeof(SkipAttribute), false).FirstOrDefault() != null;

            Func<Task<bool>> test = () => (Task<bool>)testToAwait.Invoke(null, null);

            Console.ForegroundColor = testAttr.Color;
            Console.Write($"\r{name}: ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"(x01) ");

            stopwatch.Start();
            bool ret = skip || await test();
            for (int i = 0; !skip && ret && i < repeat; i++)
            {
                Console.ForegroundColor = testAttr.Color;
                Console.Write($"\r{name}: ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"(x{i + 2:00}) ");
                ret &= await test();
            }
            stopwatch.Stop();

            var verb = ret ? "PASS" : "FAIL";
            verb = mayFail && !ret ? "WARN" : verb;
            verb = skip ? "SKIP" : verb;

            switch (verb)
            {
                case "FAIL":
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case "WARN":
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case "SKIP":
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
                case "PASS":
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
            }
            Console.WriteLine($"{stopwatch.Elapsed.TotalSeconds:0.000}s. ({verb})");
            Console.ForegroundColor = ConsoleColor.Gray;
            return (passed_tests + (ret || mayFail ? 1 : 0), total_tests + 1);
        }

        private static IEnumerable<(MethodInfo Method, string Name, string Suite)> GetTests()
        {
            return Assembly
                .GetCallingAssembly()
                .GetTypes()
                .SelectMany(t =>
                    t.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                    .Union(t.GetMethods(BindingFlags.Static | BindingFlags.Public)))
                .Select(m => (Method: m, Attr: m.GetCustomAttribute<TestAttribute>()))
                .Where(_ => _.Attr != null)
                .Select(m => (Method: m.Method, Name: m.Attr.Description, Suite: m.Attr.Suite))
                ;
        }

        static async Task Main(string[] _)
        {
            // In order for these tests to pass, they must all terminate and they must all return true.
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            int passed_tests = 0; int total_tests = 0;

            var testsBySuite = GetTests().GroupBy(t => t.Suite).ToArray();
            var suite = "Default";
            var input = "";
            var skip = 0;
            if (testsBySuite.Length > 1)
            {
                Console.WriteLine($"Choose test suite [{String.Join("|", testsBySuite.Select(t => t.Key).Append("All").ToArray())}]: ");
                input = Console.ReadLine();
                var m = Regex.Match(input, @"(\w+)(?:\s+s(?:kip)?\s*(\d+))?");
                if(m.Success)
                {
                    input = m.Groups[1].Success ? m.Groups[1].Value : input;
                    skip = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
                }
                suite = testsBySuite.FirstOrDefault(t => t.Key.StartsWith(input, StringComparison.OrdinalIgnoreCase))?.Key ?? suite;
            }

            foreach (var (Method, Name, _) in testsBySuite
                .Where(t => input.Equals("All", StringComparison.OrdinalIgnoreCase) || t.Key.Equals(suite, StringComparison.OrdinalIgnoreCase))
                .SelectMany(t => t.AsEnumerable())
                .Skip(skip))
            {
                (passed_tests, total_tests) = await RunTest($"Test {total_tests + 1:00}: {Name.PadRight(27)}", Method, passed_tests, total_tests);
            }
            stopwatch.Stop();
            Console.ForegroundColor = passed_tests == total_tests ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"\n{passed_tests:00}/{total_tests:00} tests passed. Elapsed time    : {stopwatch.Elapsed.TotalSeconds:0.000}s. ({(passed_tests == total_tests ? "PASS" : "FAIL")})");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.ReadKey();
        }

        [Test("FlowConstant -> FlowBuffer")]
        static async Task<bool> TestConstToBuf()
        {
            // Create a constant producer that continously emits 5's
            var k = new FlowVariable<int>(5);
            // Create a buffer that stores up to ten ints
            var buf = new FlowBuffer<int>(capacity: 10);
            // Consume flow from the constant producer until the buffer is full, then close the faucet to tell the buffer to stop consuming
            await buf.ConsumeFlowUntilFull(k.Flow()).Collect();
            // Create a smaller buffer
            var smallBuf = new FlowBuffer<int>(capacity: 5);
            // Fill the smaller buffer from the larger one
            await smallBuf.ConsumeFlowUntilFull(buf.Flow()).Collect();
            // The smaller buffer is now full of fives
            return smallBuf.Full
                && buf.Contents.Count == buf.Capacity - smallBuf.Capacity
                && smallBuf.Contents.All(x => x == 5);
        }
        [Test("FlowInputJunction")]
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
            await pipe.ConsumeFlowUntilFull(p.Flow()).Collect();
            // Now both buffers contain a supersequence of: 1, 2, 3, 4, 5 ...
            return bufA.Full
                && bufB.Full
                && bufA.Contents.SequenceEqual(new[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 })
                && bufB.Contents.SequenceEqual(new[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 });
        }
        [Test("SplittingFlowInputJunction")]
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
            await pipe.ConsumeFlowUntilFull(p.Flow()).Collect();
            // Now both buffers contain a supersequence of: 1, 3, 5, 2, 4 ... 
            return bufA.Full
                && bufB.Full
                && !bufA.Contents.SequenceEqual(new[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 })
                && !bufB.Contents.SequenceEqual(new[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 });
        }
        [Test("SequentialFlowInputJunction")]
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
            await pipe.ConsumeFlowUntilFull(p.Flow()).Collect();
            // Now check that bufB got filled only after bufA was already full
            return bufA.Full
                && bufB.Full
                && bufA.Contents.SequenceEqual(new[] { 1, 2, 3 })
                && bufB.Contents.SequenceEqual(new[] { 4, 5, 1, 2, 3, 4, 5 });
        }
        [Test("FlowSensor (takes a while)")]
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
        [Test("Max&MinDropletBuffers")]
        static async Task<bool> TestMaxMinBuffers()
        {
            var seq = new FlowSequence<int>(new[] { -100, 1, 3, 5, 7, 100 });
            // These only consume values higher or lower than their contained value and store it until requested
            var max = new MaxDropletBuffer<int>((a, b) => a - b);
            var min = new MinDropletBuffer<int>((a, b) => a - b);
            // Pipe seq through max and min
            var inPipe = new FlowInputJunction<int>(max, min);
            // Join max and min at the output
            var outPipe = new SequentialFlowOutputJunction<int>(() => min.Flow(), () => max.Flow());
            // Let the sequence exhaust itself and collect the maximum and minimum values found
            IProducerConsumerCollection<int> res =
                await inPipe.ConsumeFlow(seq.Flow())
                .Take(seq.Sequence.Count)
                .Redirect(outPipe.Flow())
                .Collect();
            return res.SequenceEqual(new[] { -100, 100 });
        }
        [Test("DropletMapper w/ PipeFlow()")]
        static async Task<bool> TestMapWithPipe()
        {
            // Create a sequence producer that continously emits a pattern
            var p = new FlowSequence<int>(new[] { 1, 2, 3, 4, 5 });
            // Create a mapper that transforms the sequence into a sequence of squares
            var map = new DropletTransformer<int, int>(i => i * i);
            // Create a buffer to store the results
            var buf = new FlowBuffer<int>(5);
            // Collect the squared sequence into the buffer by having it consume from the map which is piped to the sequence.
            await buf.ConsumeFlowUntilFull(map.PipeFlow(p.Flow())).Collect();
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
            var map = new DropletTransformer<char, char>(c => Char.ToUpper(c) == c ? Char.ToLower(c) : Char.ToUpper(c));
            // Create a receiving buffer for the resulting sequence
            var buf = new FlowBuffer<char>(capacity: 10);
            // Pipe the two sequences sequentially into the buffer with a splitting output junction
            // Here chunkSize: 5 means "take 5 droplets from A, then 5 from B, then 5 from A..."
            var pipe = new SplittingFlowOutputJunction<char>(chunkSize: 5, () => seqA.Flow(), () => seqB.Flow());
            // Collect the results into buf by consuming from map through which the splitter is being piped
            await buf.ConsumeFlowUntilFull(map.PipeFlow(pipe.Flow())).Collect();
            // Now the buffer contains: "HELLOworld"
            return buf.Full
                && buf.Contents.SequenceEqual("HELLOworld");
        }
        [Test("FlowMapper")]
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
            IProducerConsumerCollection<int> ret = await map.PipeFlow(p.Flow()).Collect(12);
            return ret.SequenceEqual(new[] { 1, 9, 9, 9, 8, 8, 9, 9, 9, 8, 8, 9 });
        }
        [Test("FlowFilter")]
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
            IProducerConsumerCollection<int> ret = await flt.PipeFlow(seq.Flow()).Collect(10);
            // And make sure that our buffer got the removed droplets
            return ret.SequenceEqual(new[] { 1, 5, 6, 7, 8, 9, 0, 1, 5, 6 })
                && snk.Contents.SequenceEqual(new[] { 2, 3, 4, 2, 3, 4 });
        }
        [Test("ReducingFlowOutputJunction")]
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
                () => flt.PipeFlow(seq.Flow()),
                () => snk.Flow()
            );
            IProducerConsumerCollection<int> ret = await pipe.Flow().Collect(10);
            // Now the pipe is doing the following on repeat: 
            // pull 1 from seq; pull 2 from seq, filter it into snk; pull 3 from seq and 2 from snk and reduce them
            return ret.SequenceEqual(new[] { 1, 5, 1, 5, 1, 5, 1, 5, 1, 5 })
                && snk.Empty;
        }
        [Test("Fibonacci w/ Recursive Pipe")]
        static async Task<bool> TestFibonacciScenario()
        {
            // Create a buffer to store the fibonacci sequence
            var outBuf = new FlowBuffer<int>(10);
            // Create a machine that sums and returns the contents of its input buffer
            var fibonacci = new FlowMapper<int>(buf =>
            {
                if (buf.Length == 2) // eg. [ 1, 2 ]
                {
                    return new[] { buf[1], buf[0] + buf[1] }; // eg. [ 2, 3 ]
                }
                return buf;
            }, chunkSize: 2);
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
            // Once outBuf is full, redirect the Flow to outBuf's own Flow, and collect that.
            IProducerConsumerCollection<int> res =
                await inPipe.ConsumeFlowUntil(                          // Let inPipe consume droplets until stop() returns true
                    fibonacci,                                          // The droplets are being consumed from the 'fibonacci' object
                    fibonacci.KickstartFlow(                            // Seed the fibonacci machine with initial state: { 0, 1 }
                        new[] { 0, 1 }                       // And then start using the machine's own flow
                    ),
                    stop: () => outBuf.Full                             // Finally, stop when outBuf has reached max. capacity
                )                                                       // Once inPipe.ConsumeFlow has finished, drop its results and start piping from outBuf
                .Redirect(outBuf.Flow())                                // (ConsumeFlow returns booleans similar in function to IEnumerator.MoveNext(), but we don't need them)
                .Collect();                                             // Collect the results into res
            // Res contains the fibonacci sequence!
            return res.SequenceEqual(new[] { 1, 1, 2, 3, 5, 8, 13, 21, 34, 55 });
        }
        [Test("FlowAdapter<FileStream,_>")]
        static async Task<bool> TestStreamAdapters1()
        {
            // Create an adapter that parses FileStreams into strings of length 2
            var adapter = new FileStreamFlowAdapterString(
                File.OpenRead(@"Tests\hello_world.txt"),
                Encoding.UTF8,
                chunkSize: 4
            );
            // Create a reducer that simply concatenates any string that passes through it (chunkSize doesn't really matter)
            var aggregator = new FlowMapper<string>(
                mapping: (a) => new[] { String.Join("", a) },
                chunkSize: 8
            );
            // Pipe the reducer's flow into its own flow piped through the output of the adapter (this exemplifies recursive flows)
            string ret = (await aggregator.PipeFlow(aggregator.PipeFlow(adapter.Flow())).Collect()).First();
            return "Hello world!".Equals(ret);
        }
        [Test("FileStreamFlowAdapter")]
        static async Task<bool> TestStreamAdapters2()
        {
            // Create an adapter that parses FileStreams into individual chars
            var adapter = new FileStreamFlowAdapterChar(
                File.OpenRead(@"Tests\hello_world.txt"),
                Encoding.UTF8
            );
            // And a mapper for kicks
            var mapper = new FlowMapper<char>(buf =>
            {
                return new string(buf).Replace("world", "my dudes").ToCharArray();
            }, chunkSize: 32);
            // That's it - just collect the flow and you'll read until the EOF
            IProducerConsumerCollection<char> ret = await mapper.PipeFlow(adapter.Flow()).Collect();
            return ret.SequenceEqual("Hello my dudes!");
        }
        // Adapts socket streams and tests that they work. Sometimes it fails, but rerunning this test seems to fix it.
        [MayFail]
        [Test("Network Adapters (may fail)")]
        static async Task<bool> TestStreamAdapters3()
        {
            const int PORT = 5555;
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            var localEP = new IPEndPoint(ipHostInfo.AddressList[0], PORT);
            // Create a socket that simulates a remote server sending a message and then shutting the connection down
            var t = Task.Run(async () =>
            {
                var remoteSocket = new Socket(localEP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                remoteSocket.Bind(localEP);
                remoteSocket.Listen(backlog: 1);

                Socket listener = await remoteSocket.AcceptAsync();
                byte[] helloMsg = Encoding.UTF8.GetBytes("Hello world from the web!".ToCharArray());
                await listener.SendAsync(new ArraySegment<byte>(helloMsg), SocketFlags.None);
                listener.Close();
                remoteSocket.Close();
            });
            // Then create an adapter that connects to our remote socket
            var adapter = new NetworkStreamFlowAdapterChar(
                new IPEndPoint(localEP.Address, PORT),
                Encoding.UTF8
            );
            // Create a replacement mapper
            var mapper = new FlowMapper<char>(buf =>
            {
                return new string(buf).Replace("world", "my dudes").ToCharArray();
            }, chunkSize: 32);

            IProducerConsumerCollection<char> ret = await mapper.PipeFlow(adapter.Flow()).Collect();
            return ret.SequenceEqual("Hello my dudes from the web!");
        }
        [Test("FlowTransformer<int,string>")]
        static async Task<bool> TestDropletTransformers1()
        {
            string[] choices = new[] {
                "The quick brown fox jumps over the lazy dog",
                "Lorem ipsum dolor sit amet",
                "undefined",
                $"{DateTime.Now.ToShortDateString()}"
            };
            var seq = new FlowSequence<int>(new[] { 0, 3, 1, 2 });
            var mapper = new DropletTransformer<int, string>(
                i => choices[i % choices.Length]
            );

            IProducerConsumerCollection<string> ret = await mapper.PipeFlow(seq.Flow()).Collect(seq.Sequence.Count);
            return ret.SequenceEqual(seq.Sequence.Select(i => choices[i]));
        }
        // Parses CSV files with a heading row into dynamic key-value store objects with properties matching the heading row
        [Test("CSV file to dynamic objects")]
        static async Task<bool> ParseCsvToDynamicObjects()
        {
            // Create an adapter that parses FileStreams into individual chars
            var adapter = new FileStreamFlowAdapterChar(
                File.OpenRead(@"Tests\csv.txt"),
                Encoding.UTF8
            );
            // An aggregator that creates strings each time it reaches a newline or the EOF (implicit - see Flush())
            var newlineAggr = new FlowTransformer<char, string>(
                (chars) => new[] { new string(chars).Replace("\r\n", "") },
                consumeIf: (chars, strings) => chars.Last() == '\n',
                chunkSize: 1024 /* Strings longer than this get truncated */
            );
            // And a splitter that splits the parsed lines at the semicolon
            var commaSplitter = new FlowTransformer<string, string[]>(
                (instrings) => instrings.Select(s => s.Split(';')).ToArray(),
                consumeIf: (instrings, outstrings) => instrings.Any(),
                chunkSize: 1024
            );
            // Create a transformer that takes the parser's output and creates a list of custom objects
            // It expects a string[] header and one or more string[] data droplets
            var instantiator = new FlowTransformer<string[], ExpandoObject>(
                (input) =>
                {
                    string[] header = input.Take(1).First();
                    string[][] values = input.Skip(1).ToArray();

                    IEnumerable<ExpandoObject> ret = values.Select((vals) =>
                    {
                        var obj = new ExpandoObject();
                        IEnumerable<KeyValuePair<string, object>> props = vals.Select((v, i) => new KeyValuePair<string, object>(header[i], v));
                        foreach (KeyValuePair<string, object> p in props)
                        {
                            ((IDictionary<string, object>)obj).Add(p);
                        }
                        return obj;
                    });

                    return ret.ToArray();
                },
                consumeIf: (i, o) => false, // Run when the flow staunches
                chunkSize: 4096
            );
            // Pipe everything together and you have a CSV file parser
            IProducerConsumerCollection<ExpandoObject> objects =
                await instantiator.PipeFlow(
                    commaSplitter.PipeFlow(
                        newlineAggr.PipeFlow(
                            adapter.Flow())))
                .Collect();
            return objects.Count == 3
                && ((dynamic)objects.ElementAt(0)).Id.Equals("0")
                && ((dynamic)objects.ElementAt(0)).Name.Equals("Foo")
                && ((dynamic)objects.ElementAt(0)).Desc.Equals("Bar")
                && ((dynamic)objects.ElementAt(1)).Id.Equals("1")
                && ((dynamic)objects.ElementAt(1)).Name.Equals("Bob")
                && ((dynamic)objects.ElementAt(1)).Desc.Equals("Alice")
                && ((dynamic)objects.ElementAt(2)).Id.Equals("2")
                && ((dynamic)objects.ElementAt(2)).Name.Equals("Ying")
                && ((dynamic)objects.ElementAt(2)).Desc.Equals("Yang");
        }
        // Creates a string rewriting language and tests its efficacy
        [Test("String rewriting engine")]
        static async Task<bool> TestStringRewritingMachine()
        {
            // http://www.freefour.com/rewriting-as-a-computational-paradigm/

            /* 
                "Now that we have a string-rewriting language, let’s write a program. 
                 Let’s say you want to increment a binary number that is given to you as a string delimited with underscores. 
                 E.g., you’d like a program that takes in “_1011_” as input and returns as output “_1100”. 
                 Here is a program to do so:" 
             */
            var rewriter = new DropletTransformer<string, string>(
                input => input
                    // get started
                    .Replace("1_", "1++")      // [Rule 1]
                    .Replace("0_", "1")        // [Rule 2]
                                               // eliminate ++
                    .Replace("01++", "10")     // [Rule 3]
                    .Replace("11++", "1++0")   // [Rule 4]
                    .Replace("_1++", "_10")    // [Rule 5]
            );

            /*
                "In this program, we use ++ as a marker for “increment what’s to the left of this marker”. 
                 Computation on our example input proceeds through the following program states:"

                 String	    Reasoning
                 -------------------------
                _1011_	    Input
                _1011++	    by applying Rule1
                _101++0 	Rule4
                _1100	    Rule3
            */

            // As for our flow network, we just need to recursively transform a string until the mapping does nothing!
            IProducerConsumerCollection<string> ret =
                await rewriter.PipeFlow(                         // So we pipe the rewriter's flow
                    rewriter.PipeFlow(                           // to its own flow after it has consumed
                        new[] { "_1011_" }                       // the input droplet _1011_
                    )
                ).Collect();

            return ret.Count == 1
                && ret.First().Equals("_1100");
        }
        // Creates a boolean function as a flow of bools and evaluates it by collecting the result
        [Test("Boolean gates")]
        static async Task<bool> TestBinaryCircuit()
        {
            var not = new DropletTransformer<bool, bool>(a => !a);

            Func<bool, bool, bool> op_and = (a, b) => a && b;
            Func<bool, bool, bool> op_or = (a, b) => a || b;
            Func<bool, bool, bool> op_xor = (a, b) => a ^ b;
            Func<bool, bool, bool> op_nand = (a, b) => !(a && b);
            Func<bool, bool, bool> op_nor = (a, b) => !(a || b);
            Func<bool, bool, bool> op_xnor = (a, b) => !(a ^ b);

            // Make a machine that evaluates the function: (A and (B or ((B xor D) and C)))
            var A = new FlowVariable<bool>(false);
            var B = new FlowVariable<bool>(true);
            var C = new FlowVariable<bool>(false);
            var D = new FlowVariable<bool>(true);

            ReducingFlowOutputJunction<bool> xor_gate = Gate(op_xor, () => B.Flow(), () => D.Flow());
            ReducingFlowOutputJunction<bool> and_gate1 = Gate(op_and, () => xor_gate.Flow(), () => C.Flow());
            ReducingFlowOutputJunction<bool> or_gate = Gate(op_or, () => B.Flow(), () => and_gate1.Flow());
            ReducingFlowOutputJunction<bool> and_gate2 = Gate(op_and, () => A.Flow(), () => or_gate.Flow());

            // This example demonstrates a new usage pattern with FlowVariables being changed after each drip
            bool ret = (await and_gate2.Drip()) == false;

            A.Value = true;
            B.Value = true;
            C.Value = true;
            D.Value = true;

            ret &= (await and_gate2.Drip()) == true;

            A.Value = true;
            B.Value = false;
            C.Value = true;
            D.Value = true;

            ret &= (await and_gate2.Drip()) == true;

            return ret;

            ReducingFlowOutputJunction<bool> Gate(Func<bool, bool, bool> op, params Func<IAsyncEnumerator<bool>>[] flows)
            {
                return new ReducingFlowOutputJunction<bool>(op, flows);
            }
        }
        // Creates a pre-trained neuron-based binary gate and tests that it works
        [Repeat(times: 98, DebugTimes = 0)]
        [Test("Neural Boolean gate", Suite = "Neural")]
        static async Task<bool> TestNeuron1()
        {
            const int epochs = 1000;
            const double lr = 10;

            var neuron = new FlowNeuron(nInputs: 2, ActivationFunction.SigmoidLogistic);

            // Train against an AND gate and tests that it works
            var trainingSequence = new FlowSequence<(double[] Inputs, double Output)>(new[] {
                (new[]{ 0.0, 0.0 }, 0.0),
                (new[]{ 1.0, 0.0 }, 0.0),
                (new[]{ 0.0, 1.0 }, 0.0),
                (new[]{ 1.0, 1.0 }, 1.0)
            });
            _ = neuron.Train(trainingSequence.Sequence, epochs: epochs, learningRate: lr)
                .ToArray();

            Func<Task<bool>> predictAndCheck = async () => (await neuron.PipeFlow(trainingSequence.Flow(maxDroplets: trainingSequence.Sequence.Count).Select(x => x.Inputs)
            )
            .Collect())
            //.DebugPrint("{0:0.00} ")
            .Select(s => s > 0.5 ? 1.0 : 0.0)
            .SequenceEqual(trainingSequence.Sequence.Select(s => s.Output));
            bool ret = await predictAndCheck();
            return ret;
        }
        // Creates a more advanced neuron that is trained from another flow component
        [Repeat(times: 98, DebugTimes = 0)]
        [Test("Neural Flow interfaces", Suite = "Neural")]
        static async Task<bool> TestNeuron2()
        {
            const int epochs = 100;
            const double lr = 0.5;

            var neuron = new FlowNeuron(nInputs: 2, bufferEpochs: epochs, bufferLearningRate: lr);
            // We're going to test binary gates, so these are our test inputs
            var testSequence = new FlowSequence<double[]>(new[]
            {
                new[]{ 0.0, 0.0 },
                new[]{ 1.0, 0.0 },
                new[]{ 0.0, 1.0 },
                new[]{ 1.0, 1.0 }
            });
            // The first training sequence is a binary AND gate
            var trainingSequence = new FlowSequence<(double[] Inputs, double Output)>(new[] {
                (new[]{ 0.0, 0.0 }, 0.0),
                (new[]{ 1.0, 0.0 }, 0.0),
                (new[]{ 0.0, 1.0 }, 0.0),
                (new[]{ 1.0, 1.0 }, 1.0)
            });
            // The neuron is trained by filling its dedicated TrainingBuffer first
            Func<IAsyncEnumerator<double>> trainAndPredict = () =>
                neuron.TrainingBuffer.ConsumeFlow(trainingSequence.Flow(maxDroplets: trainingSequence.Sequence.Count))
            // Then it is used to make predictions by piping the test sequence into it
                .Redirect(
                    neuron.PipeFlow(testSequence.Flow(maxDroplets: testSequence.Sequence.Count)));
            // If the neuron works, then its predictions should be the same as the training examples

            bool ret = (await trainAndPredict().Collect()).Select(s => s > 0.5 ? 1.0 : 0.0).SequenceEqual(trainingSequence.Sequence.Select(s => s.Output));
            ret &= neuron.TotalTimesTrained == epochs * trainingSequence.Sequence.Count;

            return ret;
        }
        // Creates a simple layer of neurons that trains 3 binary gates
        [Repeat(times: 98, DebugTimes = 0)]
        [Test("Individual Neuron Layer", Suite = "Neural")]
        static async Task<bool> TestNeuron3()
        {
            const int epochs = 1000;
            const double lr = 0.1;

            var layer = new FlowNeuronLayer(
                nInputs: 2,
                nNeurons: 3,
                learningRate: lr,
                trainingEpochs: epochs
            );

            var testSequence = new FlowSequence<double[]>(new[]
            {
                new[]{ 0.0, 0.0 },
                new[]{ 0.0, 1.0 },
                new[]{ 1.0, 0.0 },
                new[]{ 1.0, 1.0 }
            });

            var trainingSequence = new FlowSequence<(double[] Inputs, double[] Output)>(new[] {
                (new[]{ 0.0, 0.0 }, new []{ 0.0, 1.0, 0.0 }),
                (new[]{ 1.0, 0.0 }, new []{ 1.0, 0.0, 0.0 }),
                (new[]{ 0.0, 1.0 }, new []{ 1.0, 0.0, 0.0 }),
                (new[]{ 1.0, 1.0 }, new []{ 1.0, 0.0, 1.0 })
            });

            // Exactly the same interface as individual neurons
            Func<IAsyncEnumerator<double[]>> trainAndPredict = () =>
                layer.TrainingBuffer.ConsumeFlow(trainingSequence.Flow(maxDroplets: trainingSequence.Sequence.Count))
                .Redirect(
                    layer.PipeFlow(testSequence.Flow(maxDroplets: testSequence.Sequence.Count)));

            bool ret = (await trainAndPredict().Collect())
                .Select((p, i) => p.Select(s => s > 0.6 ? 1.0 : 0.0).SequenceEqual(trainingSequence.Sequence[i].Output)).All(x => x);

            return ret;
        }
        // Tests a 2-1 neural network in a classification task by asking it to predict the XOR table.
        [Test("Neural Network XOR", Suite = "Neural", Color = ConsoleColor.Cyan)]
        [Repeat(times: 8, DebugTimes = 0)]
        static async Task<bool> TestNeuralNet1()
        {
            const int epochs = 300;
            const double lr = 0.35;
            const double tolerance = 0.1;

            var net = new FlowNeuralNetwork(
                nInputs: 2,
                // As it turns out, Tanh is much better than the Sigmoid, especially at this task.
                // The Sigmoid works, but occasionally the test fails as it gets stuck on local minima.
                layerDef: new[] { (2, ActivationFunction.HyperbolicTangent), (1, ActivationFunction.HyperbolicTangent) },
                learningRate: lr,
                trainingEpochs: epochs
            );

            // The XOR truth table is not solvable by an individual neuron, which is a linear classifier.
            // The minimum required configuration is a 2-1 network, so this problem is perfect for testing its robustness.
            await net.Train(new[] {
                (new[]{ 0.0, 0.0 }, new[]{ -1.00 }),
                (new[]{ 0.0, 1.0 }, new[]{ 1.00 }),
                (new[]{ 1.0, 0.0 }, new[]{ 1.00 }),
                (new[]{ 1.0, 1.0 }, new[]{ -1.00 })
            }, epochs, lr);

            var input = new FlowVariable<double[]>(new double[0]);
            Func<double[], Task<double>> predict = async (double[] d) =>
            {
                input.Value = d;
                return (await net.PipeFlow(input.Flow(maxDroplets: 1)).Collect())
                    .Single()[0];
            };

            bool
            ret = await predict(new[] { 0.0, 0.0 }) <= -1 + tolerance;
            ret &= await predict(new[] { 0.0, 1.0 }) >= 1 - tolerance;
            ret &= await predict(new[] { 1.0, 0.0 }) >= 1 - tolerance;
            ret &= await predict(new[] { 1.0, 1.0 }) <= -1 + tolerance;

            return ret;
        }

        // 
        [Test("Game: Flappy Bird", Suite = "Games", Color = ConsoleColor.Red)]
        static async Task<bool> TestFlappyBird()
        {
            var cur = (CursorLeft: 0, CursorTop: Console.CursorTop + 1);
            var globals = new {
                MaxScore = 30,
                MaxTime = 60 * 1000,
                TimeStep = 16,
                MapWidth = 40,
                MapHeight = 20,
                Sigma = 0.5
            };
            var goals = new {
                ScoreWin = (Func<int, bool>)(s => s >= globals.MaxScore),
                TimeUp   = (Func<int, bool>)(t => t >= globals.MaxTime),
            };
            var state = (
                Time: 0, 
                Score: 0, 
                Speed: 0.0, 
                PlayerY: globals.MapHeight / 2, 
                Rng: new Random(0),
                Brain: new FlowNeuralNetwork(
                    nInputs: 4,
                    layerDef: new[] { (8, ActivationFunction.HyperbolicTangent), (1, ActivationFunction.HyperbolicTangent) },
                    learningRate: 0.5,
                    trainingEpochs: 1)
            );

            var _lastPipe = -1;
            var _rngSeed = 100;
            while(!goals.TimeUp(state.Time) && !goals.ScoreWin(state.Score))
            {
                int i = state.Time / globals.TimeStep;
                var pipe = (
                    Id: (int)(i * state.Speed) / globals.MapWidth,
                    X: globals.MapWidth - (int)(i * state.Speed) % globals.MapWidth - 1,
                    HoleY: 0,
                    HoleH: 0
                );
                state.Rng = new Random(_rngSeed + pipe.Id);
                pipe.HoleY = state.Rng.Next(1, globals.MapHeight - 2);
                pipe.HoleH = state.Rng.Next(2, (globals.MapHeight - pipe.HoleY) / 2 + 1);

                if(i % 2 == 0 && state.PlayerY < globals.MapHeight - 1)
                {
                    state.PlayerY++; // Gravity
                }


                var input = new[] {
                    state.PlayerY / (double)globals.MapHeight,
                    pipe.HoleY / (double)globals.MapHeight,
                    (pipe.HoleY + pipe.HoleH) / (double)globals.MapHeight,
                    pipe.X / (double)globals.MapWidth
                };
                var prediction = state.Brain.PipeFlow(new[] { input })
                    .CollectSync()
                    .Single()[0];
                
                if (prediction < -globals.Sigma && state.PlayerY > 0)
                {
                    state.PlayerY--;
                }

                double error =  (1 - state.Score / (double)globals.MaxScore) * (input[0] - (input[1] + (input[2]-input[1]) / 2));
                await state.Brain.TrainingBuffer.ConsumeDroplet((input, new[] { prediction - error }));

                if(pipe.Id != _lastPipe && pipe.X <= 1)
                {
                    bool hit = (state.PlayerY < pipe.HoleY || state.PlayerY >= pipe.HoleY + pipe.HoleH);
                    state.Score += hit ? -1 : 1;
                    _lastPipe = pipe.Id;
                }
                Console.SetCursorPosition(cur.CursorLeft, cur.CursorTop);
                Console.WriteLine($"Score: {state.Score:00} | Speed: {state.Speed:0.0}");
                for (int j = 0; j < globals.MapHeight; j++)
                {
                    char c = j >= pipe.HoleY && j < pipe.HoleY + pipe.HoleH ? ' ' : '#';
                    var row = (state.PlayerY == j ? '@' : ' ') + (pipe.X <= 1 ? "" : new string(' ', pipe.X - 2)) + c + (pipe.X == globals.MapWidth ? "" : new string(' ', globals.MapWidth - pipe.X));
                    Console.WriteLine(row);
                }
                Console.WriteLine($"Network: {String.Join(" -> ", new[] { input.Length }.Concat(state.Brain.Layers.Select(l => l.Neurons.Length)))}");
                Console.WriteLine($"Input: [{ String.Join(", ", input.Select(i => i.ToString("0.00")))}] ");
                Console.WriteLine($"Error: {error:0.00} | Prediction: {prediction:0.00}  ");
                await Task.Delay(globals.TimeStep);
                state.Time += globals.TimeStep;
                state.Speed = 0.5 + 0.5 * Math.Pow(state.Time / (double)globals.MaxTime, 2);
            }

            return goals.ScoreWin(state.Score);
        }
        [Test("Game: Snake", Suite = "Games", Color = ConsoleColor.Red)]
        static async Task<bool> TestSnake()
        {
            var cur = (CursorLeft: 0, CursorTop: Console.CursorTop + 1);
            var globals = new
            {
                MaxScore = 30.0,
                MaxTime = 60 * 1000,
                TimeStep = 16,
                Map = new { Width = 40, Height = 20, Cells = new int[40 * 20] },
                MoveStep = 1
            };
            var goals = new
            {
                ScoreWin = (Func<int, bool>)(s => s >= globals.MaxScore),
                TimeUp = (Func<int, bool>)(t => t >= globals.MaxTime),
            };
            var state = (
                Time: 0,
                Score: 0,
                Snake: (
                    Body: new List<(int X, int Y)>(),
                    Direction: 0,
                    _LastDirection: 0,
                    Brain: new FlowNeuralNetwork(
                        nInputs: globals.Map.Cells.Length + 1,
                        layerDef: new[] {
                            (64, ActivationFunction.Identity),
                            (32, ActivationFunction.HyperbolicTangent),
                            (16, ActivationFunction.HyperbolicTangent),
                            (8, ActivationFunction.HyperbolicTangent),
                            (4, ActivationFunction.SigmoidLogistic),
                            (16, ActivationFunction.SigmoidLogistic),
                            (32, ActivationFunction.SigmoidLogistic),
                            (1, ActivationFunction.Identity) },
                        learningRate: 1.05,
                        trainingEpochs: 1)),
                Food: (X: 0, Y: 0),
                Rng: new Random(0)
            );

            Reset();
            while (!goals.TimeUp(state.Time) && !goals.ScoreWin(state.Score))
            {

                if (state.Snake.Body[0].Equals(state.Food))
                {
                    state.Score++;
                    state.Snake.Body.Insert(0, state.Snake.Body[0]);
                    PlaceFood();
                }

                var inputs = GetInputs();
                var newDir = Predict(inputs);
                state.Snake.Direction = (int)(newDir * 4);
                MoveOrReset();
                var bestDir = Enumerable.Range(0, 4)
                    .Select(d =>
                    {
                        var p = TileAhead(state.Snake.Body[0], d);
                        if (HitsBody(p) || HitsWall(p))
                        {
                            p = (100000, 100000);
                        }
                        return (D: d, P: p);
                    })
                    .OrderBy(t => Distance(t.P, state.Food))
                    .First().D;

                if(bestDir != state.Snake.Direction)
                {
                    await state.Snake.Brain.TrainingBuffer.ConsumeDroplet((inputs, new[] { bestDir / 4.0 + 0.125 }));
                }
                var bestTile = TileAhead(state.Snake.Body[0], bestDir);

                Console.SetCursorPosition(cur.CursorLeft, cur.CursorTop + globals.Map.Height);
                Console.WriteLine($"Inputs: [{String.Join(", ", inputs.Select(x => x.ToString("0.00")))}]");
                Console.WriteLine($"Output: {newDir:0.00}");
                Console.WriteLine($"");
                Console.WriteLine($"Score: {state.Score}");

                Draw(state.Snake.Body[0], bestTile);

                await Task.Delay(globals.TimeStep);
                state.Time += globals.TimeStep;
                state.Snake._LastDirection = state.Snake.Direction;
            }

            return goals.ScoreWin(state.Score);
            double Predict(double[] inputs)
            {
                return state.Snake.Brain.PipeFlow(new[] { inputs })
                    .CollectSync()
                    .Single()
                    .ToArray()
                    .Single();
            }
            double[] GetInputs()
            {
                return globals.Map.Cells.Select((c, i) =>
                {
                    var xy = (i % globals.Map.Width, i / globals.Map.Width);
                    if(xy.Equals(state.Food))
                    {
                        return 10.0;
                    }
                    if(xy.Equals(state.Snake.Body[0]))
                    {
                        return 1;
                    }
                    if (HitsBody(xy) || HitsWall(xy))
                    {
                        return -10.0;
                    }
                    return 0.0;
                }).Select(x => x + (state.Rng.NextDouble() * 2 - 1) * 0.0).Prepend(state.Snake.Direction / 4.0).ToArray();

                return new[] {
                    (state.Food.X - state.Snake.Body[0].X) / (double)globals.Map.Width,
                    (state.Food.Y - state.Snake.Body[0].Y) / (double)globals.Map.Height,
                    state.Snake.Direction / 4.0,

                    //state.Food.X / (double)globals.Map.Width,
                    //state.Food.Y / (double)globals.Map.Height,

                    // Distance(state.Snake.Body[0], state.Food) / Math.Sqrt(globals.Map.Width * globals.Map.Height),

                    //state.Snake.Direction / 4.0,

                    //state.Score / globals.MaxScore,

                    //HitsWall(TileAhead()) ? 1 : 0,
                };
            }
            void Draw((int X, int Y) pred, (int X, int Y) best)
            {
                var sb = new StringBuilder();
                for (int y = 0; y < globals.Map.Height; y++)
                {
                    for (int x = 0; x < globals.Map.Width; x++)
                    {
                        var snakeHere = state.Snake.Body.Any(p => p.Equals((x, y)));
                        var wallHere = globals.Map.Cells[x + globals.Map.Width * y] == 0;
                        var foodHere = state.Food.Equals((x, y));
                        var predHere = pred.Equals((x, y));
                        var bestHere = best.Equals((x, y));

                        switch (snakeHere)
                        {
                            case false when predHere:
                                sb.Append('*'); break;
                            case false when bestHere:
                                sb.Append('+'); break;
                            case true when foodHere:
                                sb.Append('O'); break;
                            case false when foodHere:
                                sb.Append('.'); break;
                            case true when !wallHere:
                                sb.Append('o'); break;
                            case false when !wallHere:
                                sb.Append(' '); break;
                            case true when wallHere:
                                sb.Append('X'); break;
                            case false when wallHere:
                                sb.Append('#'); break;
                        }
                    }
                    sb.Append("\r\n");
                }
                Console.SetCursorPosition(cur.CursorLeft, cur.CursorTop);
                Console.WriteLine(sb.ToString());
            }
            void Reset()
            {
                state.Snake.Body.Clear();
                state.Snake.Body.AddRange(new[] {
                    /* HEAD -> */ (3, 0), (2, 0), (1, 0), (0, 0) /* <- TAIL */,
                });
                Enumerable.Range(0, globals.Map.Cells.Length)
                    .Select(i => globals.Map.Cells[i] = state.Rng.NextDouble() < 0.01 ? 0 : 1)
                    .Count();
                PlaceFood();
            }
            void PlaceFood()
            {
                do
                {
                    state.Food = (X: state.Rng.Next(globals.Map.Width), Y: state.Rng.Next(globals.Map.Height));
                }
                while (HitsWall(state.Food));
            }
            (int X, int Y) TileAhead((int X, int Y) xy, int dir)
            {
                switch (dir)
                {
                    case 0:
                        xy = (xy.X, xy.Y - 1);
                        break;
                    case 1:
                        xy = (xy.X + 1, xy.Y);
                        break;
                    case 2:
                        xy = (xy.X, xy.Y + 1);
                        break;
                    case 3:
                        xy = (xy.X - 1, xy.Y);
                        break;
                }
                xy = (Mod(xy.X, globals.Map.Width), Mod(xy.Y, globals.Map.Height));
                return xy;
            }
            double Distance((int X, int Y) p, (int X, int Y) q)
            {
                return Math.Abs(p.X - q.X) + Math.Abs(p.Y - q.Y);
                //return Math.Sqrt(Math.Pow(p.X - q.X, 2) + Math.Pow(p.Y - q.Y, 2));
            }
            bool HitsWall((int X, int Y) p)
            {
                return globals.Map.Cells[p.X + globals.Map.Width * p.Y] == 0;
            }
            bool HitsBody((int X, int Y) p)
            {
                return state.Snake.Body.Skip(1).Any(q => q.Equals(p));
            }
            int Mod(int x, int m)
            {
                return (x % m + m) % m;
            }
            bool MoveOrReset()
            {
                var xy = TileAhead(state.Snake.Body[0], state.Snake.Direction);
                if (HitsBody(xy) || HitsWall(xy))
                {
                    state.Score = 0;
                    Reset();
                    return false;
                }
                for (int i = state.Snake.Body.Count - 1; i >= 1; i--)
                {
                    state.Snake.Body[i] = state.Snake.Body[i - 1];
                }
                state.Snake.Body[0] = xy;
                return true;
            }
        }
    }
}
