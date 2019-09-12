using FlowAI.Consumers;
using FlowAI.Producers;
using System;
using System.Collections.Async;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace FlowAI
{
    public static class AsyncExtensions
    {
        public static async Task UntilAsync<T>(this IAsyncEnumerator<T> e, Func<T, Task<bool>> until, AsyncEnumerator<T>.Yield yield = null)
        {
            await e.ForEachAsync(async t =>
            {
                if (!await until(t))
                {
                    if (yield != null) yield.Break();
                    e.Dispose();
                }
            });
        }
        public static async Task UntilAsync<T>(this IAsyncEnumerator<T> e, Func<T, bool> until, AsyncEnumerator<T>.Yield yield = null)
        {
            await e.ForEachAsync(t =>
            {
                if (!until(t))
                {
                    if (yield != null) yield.Break();
                    e.Dispose();
                }
            });
        }

        /// <summary>
        /// Similar to (and calls) ConsumeFlow, but staunches the flow as soon as it returns false, then starts it again.
        /// This is a piping tool and should not be used directly by consumers.
        /// </summary>
        public static IAsyncEnumerator<bool> ConsumeFlowUntilFull<T>(this IFlowConsumer<T> c, IFlowProducer<T> producer, IAsyncEnumerator<T> flow)
        {
            return new AsyncEnumerator<bool>(async yield =>
            {
                if (producer.IsFlowStarted&& producer is FlowProducerBase<T> prodImpl)
                {
                    await c.ConsumeFlow(producer, flow).UntilAsync(async b =>
                    {
                        await yield.ReturnAsync(b);
                        return b;
                    }, yield);
                }
            });
        }

        /// <summary>
        /// Similar to (and calls) ConsumeFlow, but staunches the flow as soon as a predicate running on the consumer itself matches, then starts it again.
        /// This is a piping tool and should not be used directly by consumers.
        /// </summary>
        public static IAsyncEnumerator<bool> ConsumeFlowUntil<T>(this IFlowConsumer<T> c, IFlowProducer<T> producer, IAsyncEnumerator<T> flow, Func<bool> stop)
        {
            return new AsyncEnumerator<bool>(async yield =>
            {
                if (producer.IsFlowStarted&& producer is FlowProducerBase<T> prodImpl)
                {
                    await c.ConsumeFlow(producer, new AsyncEnumerator<T>(async innerYield =>
                    {
                        await flow.ForEachAsync(async d =>
                        {
                            if (stop())
                            {
                                innerYield.Break();
                            }
                            await innerYield.ReturnAsync(d);
                        });
                    })).UntilAsync(async b =>
                    {
                        await yield.ReturnAsync(b);
                        return b;
                    }, yield);
                }
            });
        }


        /// <summary>
        /// Similar to (and calls) ConsumeFlow, but staunches the flow as soon as a certain droplet is found in the input flow, then starts it again.
        /// This is a piping tool and should not be used directly by consumers.
        /// </summary>
        public static IAsyncEnumerator<bool> ConsumeFlowUntilDroplet<T>(this IFlowConsumer<T> c, IFlowProducer<T> producer, IAsyncEnumerator<T> flow, Predicate<T> stopOn)
        {
            return new AsyncEnumerator<bool>(async yield =>
            {
                if (producer.IsFlowStarted&& producer is FlowProducerBase<T> prodImpl)
                {
                    await c.ConsumeFlow(producer, new AsyncEnumerator<T>(async innerYield =>
                    {
                        await flow.ForEachAsync(async d =>
                        {
                            if (stopOn(d))
                            {
                                innerYield.Break();
                            }
                            await innerYield.ReturnAsync(d);
                        });
                    })).UntilAsync(async b =>
                    {
                        await yield.ReturnAsync(b);
                        return b;
                    }, yield);
                }
            });
        }

        /// <summary>
        /// Collects the elements of an IAsyncEnumerator into a regular enumerable.
        /// </summary>
        public static async Task<IProducerConsumerCollection<T>> Collect<T>(this IAsyncEnumerator<T> e, int maxCount = 0)
        {
            var queue = new ConcurrentQueue<T>();
            await e.ForEachAsync(t =>
            {
                queue.Enqueue(t);
                if (--maxCount == 0)
                {
                    e.Dispose();
                }
            });
            return queue;
        }

        /// <summary>
        /// Collects the elements of an IAsyncEnumerator into a regular enumerable synchronously.
        /// <returns></returns>
        public static IProducerConsumerCollection<T> CollectSync<T>(this IAsyncEnumerator<T> e, int maxCount = 0)
        {
            return e.Collect(maxCount: maxCount).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Transforms the elements of an IAsyncEnumerator.
        /// </summary>
        public static IAsyncEnumerator<U> Map<T, U>(this IAsyncEnumerator<T> e, Func<T, U> mapping)
        {
            return new AsyncEnumerator<U>(async yield =>
            {
                while (await e.MoveNextAsync())
                {
                    await yield.ReturnAsync(mapping(e.Current));
                }
            });
        }

        /// <summary>
        /// Appends a second enumerator at the end of the current enumerator, then returns the second enumerator.
        /// The current enumerator will always run to completion before the appended enumerator can start.
        /// </summary>
        public static IAsyncEnumerator<TResult> Redirect<TSource, TResult>(this IAsyncEnumerator<TSource> e, IAsyncEnumerator<TResult> res)
        {
            return new AsyncEnumerator<TResult>(async yield =>
            {
                while (await e.MoveNextAsync()) ;
                while (await res.MoveNextAsync())
                {
                    await yield.ReturnAsync(res.Current);
                }
            });
        }

        /// <summary>
        /// Appends a second enumerator at the end of the current enumerator, then returns the combined enumerator.
        /// The current enumerator will always run to completion before the appended enumerator can start.
        /// </summary>
        public static IAsyncEnumerator<T> ContinueWith<T>(this IAsyncEnumerator<T> e, IAsyncEnumerator<T> res, Func<bool> condition = null)
        {
            return new AsyncEnumerator<T>(async yield =>
            {
                while (await e.MoveNextAsync())
                {
                    await yield.ReturnAsync(e.Current);
                }

                if(condition?.Invoke() ?? false)
                {
                    while (await res.MoveNextAsync())
                    {
                        await yield.ReturnAsync(res.Current);
                    }
                }
            });
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

        public static T Choose<T>(this Random rng, IList<T> from)
        {
            return from[rng.Next(from.Count)];
        }
    }


}
