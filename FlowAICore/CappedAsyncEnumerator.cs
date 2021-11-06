using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace FlowAI
{
    public class CappedAsyncEnumerator<T> : IAsyncEnumerator<T>, IAsyncDisposable
    {
        protected readonly IAsyncEnumerator<T> Source;

        public bool IsEnumerationComplete { get; private set; }

        public CappedAsyncEnumerator(IAsyncEnumerator<T> source)
        {
            Source = source;
        }

        public T Current => Source.Current;

        public ValueTask DisposeAsync() => Source.DisposeAsync();
        public async ValueTask<bool> MoveNextAsync()
        {
            if(IsEnumerationComplete) {
                return false;
            }
            if(!await Source.MoveNextAsync()) {
                IsEnumerationComplete = true;
                return false;
            }
            return true;
        }
    }


}
