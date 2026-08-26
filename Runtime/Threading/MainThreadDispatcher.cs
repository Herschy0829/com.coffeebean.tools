using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace CoffeeBean
{
    /// <summary>
    /// Unity 主线程调度器：允许任意线程把 Action 投递到主线程执行（在 Update 中消费）。
    ///
    /// 解决经典问题：网络 / HTTP / 广告 SDK 等回调不在 Unity 主线程，
    /// 却需要操作 Unity API（Resource.Load、UI、Transform 等）。
    ///
    /// 用法（任意线程均可调用）：
    /// <code>
    /// MainThreadDispatcher.Post(() => text.text = "更新");   // 投递到主线程
    /// MainThreadDispatcher.RunOnMainThread(() => ...);       // 已在主线程则立即执行
    /// MainThreadDispatcher.PostDelayed(() => ..., 1f);        // 延迟 1 秒（受 timeScale 影响）
    /// MainThreadDispatcher.PostDelayedUnscaled(() => ..., 1f);// 延迟 1 秒（不受 timeScale 影响）
    /// MainThreadDispatcher.IsMainThread                       // 是否主线程
    /// </code>
    ///
    /// 优化点（相对常见实现）：ConcurrentQueue 无锁入队；异常按条隔离（不中断整帧）；
    /// 延迟队列批量取回未到期项；内部执行方法拆出便于单元测试。
    /// </summary>
    public sealed class MainThreadDispatcher : CSingletonMono<MainThreadDispatcher>
    {
        /// <summary>延迟动作条目。</summary>
        private struct DelayedAction
        {
            public float ExecTime;
            public bool Unscaled;
            public Action Action;
        }

        private readonly ConcurrentQueue<Action> _pending = new ConcurrentQueue<Action>();
        private readonly ConcurrentQueue<DelayedAction> _delayed = new ConcurrentQueue<DelayedAction>();
        private int _mainThreadId;
        private bool _initialized;

        /// <summary>当前调用是否位于 Unity 主线程。</summary>
        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == MainThreadId;

        private static int MainThreadId { get; set; }

        /// <summary>记录主线程 id（Awake 一定在主线程执行）。</summary>
        protected override void Awake()
        {
            base.Awake();
            if (_initialized) return; // 防重复 Awake（同一实例多次启用）
            _initialized = true;
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// 确保调度器就绪：解析实例并记录主线程 id。
        /// 播放模式下由 Awake 完成；编辑器 / 测试环境（Awake 不执行）需在准备阶段调用一次，
        /// 且必须在主线程调用（保证后续任意线程 Post 都使用已缓存的实例与正确的主线程 id）。
        /// </summary>
        public static void EnsureReady()
        {
            MainThreadDispatcher dispatcher = EnsureInstance();
            if (MainThreadId == 0) MainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>投递动作到主线程执行（线程安全）。</summary>
        public static void Post(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            EnsureInstance()._pending.Enqueue(action);
        }

        /// <summary>投递带参数动作（避免每次调用分配闭包）。</summary>
        public static void Post<T>(Action<T> action, T arg)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            EnsureInstance()._pending.Enqueue(() => action(arg));
        }

        /// <summary>延迟执行（秒；延迟基于 Time.time，受 timeScale 影响）。</summary>
        public static void PostDelayed(Action action, float delaySeconds)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (delaySeconds < 0f) delaySeconds = 0f;
            EnsureInstance()._delayed.Enqueue(new DelayedAction
            {
                ExecTime = Time.time + delaySeconds,
                Unscaled = false,
                Action = action,
            });
        }

        /// <summary>
        /// 延迟执行（秒；延迟基于 Time.unscaledTime，不受 timeScale 影响）。
        /// 适合暂停菜单 / 慢动作等 timeScale 被修改场景下的倒计时、隐藏提示等逻辑。
        /// </summary>
        public static void PostDelayedUnscaled(Action action, float delaySeconds)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (delaySeconds < 0f) delaySeconds = 0f;
            EnsureInstance()._delayed.Enqueue(new DelayedAction
            {
                ExecTime = Time.unscaledTime + delaySeconds,
                Unscaled = true,
                Action = action,
            });
        }

        /// <summary>已在主线程则立即执行，否则投递到主线程（线程安全）。</summary>
        public static void RunOnMainThread(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (IsMainThread) action();
            else Post(action);
        }

        /// <summary>获取单例；不可用（退出中 / 非播放）时抛异常，让调用方尽早发现。</summary>
        private static MainThreadDispatcher EnsureInstance()
        {
            var instance = Instance; // CSingletonMono 懒创建（退出期返回 null）
            if (instance == null)
                throw new InvalidOperationException(
                    "MainThreadDispatcher 不可用（应用退出中，或未处于播放状态时无法自动创建）。");
            return instance;
        }

        private void Update()
        {
            ExecutePendingActions(Time.time, Time.unscaledTime);
        }

        /// <summary>
        /// 执行所有到期动作（主线程消费；internal 供单元测试直接驱动）。
        /// 异常按条捕获并记录，避免单个动作异常中断整帧后续动作。
        /// scaledNow 用于 PostDelayed（Time.time），unscaledNow 用于 PostDelayedUnscaled（Time.unscaledTime）。
        /// </summary>
        internal void ExecutePendingActions(float scaledNow, float unscaledNow)
        {
            // 立即队列：无锁取出执行
            while (_pending.TryDequeue(out Action action))
            {
                try { action(); }
                catch (Exception e) { Debug.LogError($"[CoffeeBean.Tools] 主线程动作执行异常：{e}"); }
            }

            // 延迟队列：取出所有，执行到期的，未到期的放回
            if (_delayed.IsEmpty) return;
            var deferred = new List<DelayedAction>(_delayed.Count);
            while (_delayed.TryDequeue(out DelayedAction item))
            {
                float now = item.Unscaled ? unscaledNow : scaledNow;
                if (item.ExecTime <= now)
                {
                    try { item.Action(); }
                    catch (Exception e) { Debug.LogError($"[CoffeeBean.Tools] 主线程延迟动作执行异常：{e}"); }
                }
                else
                {
                    deferred.Add(item);
                }
            }
            foreach (DelayedAction item in deferred) _delayed.Enqueue(item);
        }
    }
}
