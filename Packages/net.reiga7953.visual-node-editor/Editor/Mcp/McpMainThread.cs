using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// 別スレッド（HTTP の受け取り）から、処理をメインスレッドで実行して結果を待つ。
    /// AssetDatabase やアセットの編集はメインスレッドでしか行えないため。処理は <see cref="EditorApplication.update"/> で順に行う。
    /// </summary>
    internal static class McpMainThread
    {
        // 処理の状態。待ち（Pending）からは、始める（Started）か取り消す（Cancelled）のどちらか一方だけに変わる
        private const int Pending = 0;
        private const int Started = 1;
        private const int Cancelled = 2;

        private static readonly ConcurrentQueue<Action> _queue = new();

        [InitializeOnLoadMethod]
        private static void Initialize() => EditorApplication.update += Pump;

        /// <summary>
        /// <paramref name="work"/> をメインスレッドで実行し、結果を返す（呼んだスレッドは待つ）。
        /// <paramref name="timeout"/> 以内に始まらなければ取り消して <see cref="TimeoutException"/>（その処理は実行しない）。
        /// 始まった処理は、終わるまで待つ（保存まで済んだ編集を「失敗」と返して、やり直しで重ねさせないため）。
        /// </summary>
        public static T Run<T>(Func<T> work, TimeSpan timeout)
        {
            var done = new ManualResetEventSlim(false);
            var state = Pending;
            var result = default(T);
            Exception failure = null;
            _queue.Enqueue(() =>
            {
                if (Interlocked.CompareExchange(ref state, Started, Pending) != Pending)
                {
                    return;
                }

                try
                {
                    result = work();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    done.Set();
                }
            });

            if (!done.Wait(timeout))
            {
                if (Interlocked.CompareExchange(ref state, Cancelled, Pending) == Pending)
                {
                    throw new TimeoutException("Unity did not run the request in time (it may be compiling, importing or showing a dialog). Nothing was changed.");
                }

                // 取り消す前に始まっていた: 終わるまで待つ
                done.Wait();
            }

            done.Dispose();
            if (failure != null)
            {
                throw new InvalidOperationException(failure.Message, failure);
            }

            return result;
        }

        /// <summary>たまった処理を実行する（メインスレッドから。テストでは直接呼んでもよい）。</summary>
        internal static void Pump()
        {
            while (_queue.TryDequeue(out var action))
            {
                action();
            }
        }
    }
}
