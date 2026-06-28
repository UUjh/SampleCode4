using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SampleClient.Service.Firebase
{
    /// <summary>
    /// Firebase 서버 요청을 하나씩 순서대로 실행하는 큐.
    /// Dispose되면 대기 중인 요청을 실패 처리해 씬 전환 중 남는 요청을 막음.
    /// </summary>
    public sealed class SendQueue : IDisposable
    {
        private readonly Queue<ISendQueueItem> _items = new Queue<ISendQueueItem>();
        private readonly CancellationTokenSource _disposeCts = new CancellationTokenSource();
        private bool _running;
        private bool _disposed;

        /// <summary>
        /// 요청 큐에 추가.
        /// </summary>
        /// <typeparam name="T">요청 결과 타입.</param>
        /// <param name="action">요청 작업.</param>
        /// <returns>요청 작업 태스크.</returns>
        public UniTask<T> Enqueue<T>(Func<CancellationToken, UniTask<T>> action)
        {
            var item = new SendQueueItem<T>(action);

            // 큐에 추가.
            lock (_items)
            {
                if (_disposed)
                {
                    item.SetException(new ObjectDisposedException(nameof(SendQueue)));
                    return item.Task;
                }

                _items.Enqueue(item);
                if (!_running)
                {
                    _running = true;
                    RunAsync().Forget();
                }
            }

            return item.Task;
        }

        /// <summary>
        /// 대기 중인 요청을 실패 처리하고 큐 리소스를 정리한다.
        /// </summary>
        public void Dispose()
        {
            List<ISendQueueItem> pendingItems = null;

            lock (_items)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _disposeCts.Cancel();

                if (_items.Count > 0)
                {
                    pendingItems = new List<ISendQueueItem>(_items.Count);
                    while (_items.Count > 0)
                    {
                        pendingItems.Add(_items.Dequeue());
                    }
                }
            }

            if (pendingItems != null)
            {
                var exception = new ObjectDisposedException(nameof(SendQueue));
                for (var i = 0; i < pendingItems.Count; i++)
                {
                    pendingItems[i].SetException(exception);
                }
            }

            _disposeCts.Dispose();
        }
        
        /// <summary>
        /// 요청 큐 실행.
        /// </summary>
        private async UniTaskVoid RunAsync()
        {
            while (true)
            {
                ISendQueueItem item;

                lock (_items)
                {
                    // 큐가 Dispose되었으면 실행을 중단.
                    if (_disposed)
                    {
                        _running = false;
                        return;
                    }

                    // 큐가 비어있으면 실행을 중단.
                    if (_items.Count == 0)
                    {
                        _running = false;
                        return;
                    }

                    // 큐에서 요청을 꺼낸다.
                    item = _items.Dequeue();
                }

                // 큐에서 꺼낸 요청을 끝까지 기다린 뒤 다음 요청을 처리한다.
                await item.RunAsync(_disposeCts.Token);
            }
        }

        // 요청 큐 항목 인터페이스.
        private interface ISendQueueItem
        {
            UniTask RunAsync(CancellationToken cancellationToken);
            void SetException(Exception exception);
        }

        // 요청 큐 항목 구현.
        private sealed class SendQueueItem<T> : ISendQueueItem
        {
            private readonly Func<CancellationToken, UniTask<T>> _action;
            private readonly UniTaskCompletionSource<T> _source = new UniTaskCompletionSource<T>();

            public SendQueueItem(Func<CancellationToken, UniTask<T>> action)
            {
                _action = action;
            }

            public UniTask<T> Task => _source.Task;

            /// <summary>
            /// 큐 항목의 비동기 작업을 실행하고 결과를 완료 소스에 전달한다.
            /// </summary>
            public async UniTask RunAsync(CancellationToken cancellationToken)
            {
                try
                {
                    if (_action == null)
                    {
                        throw new ArgumentNullException(nameof(_action));
                    }

                    var result = await _action(cancellationToken);
                    _source.TrySetResult(result);
                }
                catch (Exception e)
                {
                    _source.TrySetException(e);
                }
            }

            /// <summary>
            /// 큐 항목을 지정 예외로 실패 처리한다.
            /// </summary>
            public void SetException(Exception exception)
            {
                _source.TrySetException(exception);
            }
        }
    }
}




