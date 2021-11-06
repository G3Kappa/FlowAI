using FlowAI.Consumers;
using FlowAI.Producers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace FlowAI
{

    public static class AsyncExtensions
    {
        public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerator<T> enumerator)
        =>
            AsyncEnumerable.Create((cancellationToken)
                => AsyncEnumerator.Create(
                    () => new ValueTask<bool>(enumerator.MoveNext()),
                    () => enumerator.Current,
                    () => new ValueTask())
            );
        public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> enumerable)
        => enumerable.GetEnumerator().ToAsyncEnumerable();
        public static IAsyncEnumerator<T> GetAsyncEnumerator<T>(this IEnumerable<T> source)
        =>
            source.GetEnumerator().ToAsyncEnumerable().GetAsyncEnumerator();

        public static async Task UntilAsync<T>(this IAsyncEnumerable<T> e, Func<T, Task<bool>> until)
        {
            await foreach(var t in e) {
                if (!await until(t)) {
                    return;
                }
            }
        }

        public static async Task UntilAsync<T>(this IAsyncEnumerable<T> e, Func<T, bool> until)
        {
            await foreach (var t in e) {
                if (!until(t)) {
                    return;
                }
            }
        }

        /// <summary>
        /// Similar to (and calls) ConsumeFlow, but staunches the flow as soon as it returns false, then starts it again.
        /// This is a piping tool and should not be used directly by consumers.
        /// </summary>
        public static async IAsyncEnumerable<bool> ConsumeFlowUntilFull<T>(this IFlowConsumer<T> c, IAsyncEnumerable<T> flow)
        {
            await foreach(var b in c.ConsumeFlow(flow)) {
                yield return b;
                if (!b) yield break;
            }
        }

        /// <summary>
        /// Similar to (and calls) ConsumeFlow, but staunches the flow as soon as a predicate running on the consumer itself matches, then starts it again.
        /// This is a piping tool and should not be used directly by consumers.
        /// </summary>
        public static async IAsyncEnumerable<bool> ConsumeFlowUntil<T>(this IFlowConsumer<T> c, IFlowProducer<T> producer, IAsyncEnumerable<T> flow, Func<bool> stop)
        {
            if (producer.IsFlowStarted && producer is FlowProducerBase<T> prodImpl) {
                await foreach (var b in c.ConsumeFlow(Inner())) {
                    yield return b;
                }
            }

            async IAsyncEnumerable<T> Inner()
            {
                await foreach (var d in flow) {
                    if (stop()) {
                        yield break;
                    }
                    yield return d;
                }
            }
        }


        /// <summary>
        /// Similar to (and calls) ConsumeFlow, but staunches the flow as soon as a certain droplet is found in the input flow, then starts it again.
        /// This is a piping tool and should not be used directly by consumers.
        /// </summary>
        public static async IAsyncEnumerable<bool> ConsumeFlowUntilDroplet<T>(this IFlowConsumer<T> c, IFlowProducer<T> producer, IAsyncEnumerable<T> flow, Predicate<T> stopOn)
        {
            if (producer.IsFlowStarted && producer is FlowProducerBase<T> prodImpl) {
                await foreach(var t in c.ConsumeFlow(Inner())) {
                    yield return t;
                }
            }

            async IAsyncEnumerable<T> Inner()
            {
                await foreach(var d in flow) {
                    if (stopOn(d)) {
                        yield break;
                    }
                    yield return d;
                }
            }
        }

        /// <summary>
        /// Collects the elements of an IAsyncEnumerator into a regular enumerable.
        /// </summary>
        public static async Task<IProducerConsumerCollection<T>> Collect<T>(this IAsyncEnumerable<T> e, int maxCount = 0)
        {
            var queue = new ConcurrentQueue<T>();
            await foreach(var t in e) {
                queue.Enqueue(t);
                if (--maxCount == 0) {
                    return queue;
                }
            }
            return queue;
        }

        /// <summary>
        /// Collects the elements of an IAsyncEnumerator into a regular enumerable synchronously.
        /// <returns></returns>
        public static IProducerConsumerCollection<T> CollectSync<T>(this IAsyncEnumerable<T> e, int maxCount = 0)
        {
            return e.Collect(maxCount: maxCount).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Appends a second enumerator at the end of the current enumerator, then returns the second enumerator.
        /// The current enumerator will always run to completion before the appended enumerator can start.
        /// </summary>
        public static async IAsyncEnumerable<TResult> Redirect<TSource, TResult>(this IAsyncEnumerable<TSource> e, IAsyncEnumerable<TResult> res)
        {
            await foreach(var _ in e) { }
            await foreach(var r in res) { yield return r; }
        }

        /// <summary>
        /// Appends a second enumerator at the end of the current enumerator, then returns the combined enumerator.
        /// The current enumerator will always run to completion before the appended enumerator can start.
        /// </summary>
        public static async IAsyncEnumerable<T> ContinueWith<T>(this IAsyncEnumerable<T> e, IAsyncEnumerable<T> res, Func<bool> condition = null)
        {
            await foreach (var t in e) {
                yield return t;
            }
            if(condition?.Invoke() ?? false) {
                await foreach (var t in res) {
                    yield return t;
                }
            }
        }

        /// <summary>
        /// Replaces all occurrences of 'needle' with 'replacement'
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="find"></param>
        /// <param name="replacement"></param>
        /// <returns></returns>
        public static IList<T> SequenceReplace<T>(this IList<T> source, T[] needle, T[] replacement)
        {
            var ret = new List<T>(source);
            for (int i = 0; i < ret.Count; i++)
            {
                if(needle.SequenceEqual(ret.Skip(i).Take(needle.Length)))
                {
                    var tmp = ret.Take(i).ToList();
                    tmp.AddRange(replacement);
                    tmp.AddRange(source.Skip(i + needle.Length));
                    ret = tmp;
                    i += (needle.Length - replacement.Length);
                }
            }
            return ret;
        }

        public static IEnumerable<T> DebugPrint<T>(this IEnumerable<T> source, Func<T, string> fmt = null, params object[] args)
        {
            fmt ??= (d => d.ToString());
            Console.Write("[ ");
            foreach (var item in source)
            {
                Console.Write(fmt(item) + " ", args);
                yield return item;
            }
            Console.Write(" ]\n");
        }

        public static T Choose<T>(this Random rng, IList<T> from)
        {
            return from[rng.Next(from.Count)];
        }
    }


}
