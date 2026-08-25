using System;
using System.Threading;
using UnityEngine;

namespace CoffeeBean.Tools
{
    /// <summary>
    /// 线程池工具：带并发上限的后台执行（替代"忙等限流"的低效实现）。
    /// 用于把耗时 / 阻塞操作（网络请求、IO、计算）放到后台线程，配合
    /// <see cref="MainThreadDispatcher"/> 回到主线程更新 Unity API。
    /// </summary>
    public static class ThreadUtil
    {
        /// <summary>最大并发后台任务数。</summary>
        public const int DefaultMaxConcurrency = 8;

        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(DefaultMaxConcurrency, DefaultMaxConcurrency);

        /// <summary>
        /// 后台执行动作（并发数受限，超出则排队等待；异常会被记录，不会抛出到调用方线程）。
        /// </summary>
        public static void RunAsync(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            // 等待可用信号量（异步等待，不占用线程池线程忙等）
            Gate.WaitAsync().ContinueWith(_ =>
            {
                try { action(); }
                catch (Exception e) { Debug.LogError($"[CoffeeBean.Tools] 后台动作执行异常：{e}"); }
                finally { Gate.Release(); }
            });
        }
    }
}
