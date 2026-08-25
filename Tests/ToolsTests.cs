using System;
using System.Threading;
using CoffeeBean.Tools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>单例与主线程调度器测试（EditMode；测试内创建组件验证逻辑）。</summary>
    public class ToolsTests
    {
        private sealed class PlainSingletonImpl : CSingleton<PlainSingletonImpl> { }

        private GameObject _dispatcherGo;

        [SetUp]
        public void SetUp()
        {
            // EditMode 下 CSingletonMono 的 Awake 不执行：手动创建组件，并 EnsureReady
            // 在主线程解析实例 + 捕获主线程 id（保证后台线程 Post 使用已缓存实例）。
            _dispatcherGo = new GameObject("TestDispatcher");
            _dispatcherGo.AddComponent<MainThreadDispatcher>();
            MainThreadDispatcher.EnsureReady();
        }

        [TearDown]
        public void TearDown()
        {
            if (_dispatcherGo != null) UnityEngine.Object.DestroyImmediate(_dispatcherGo);
        }

        // ===== Singleton（纯 C#）=====

        [Test]
        public void Singleton_SameInstance_Always()
        {
            Assert.AreSame(PlainSingletonImpl.Instance, PlainSingletonImpl.Instance);
        }

        [Test]
        public void Singleton_ThreadSafe_MultipleThreadsSameInstance()
        {
            PlainSingletonImpl first = PlainSingletonImpl.Instance;
            PlainSingletonImpl fromThread = null;
            var threads = new Thread[4];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() =>
                {
                    PlainSingletonImpl local = PlainSingletonImpl.Instance;
                    // 至少证明并发访问不抛异常且能拿到实例
                    if (local != null && Interlocked.CompareExchange(ref fromThread, local, null) == null) { }
                });
                threads[i].Start();
            }
            foreach (Thread t in threads) t.Join();

            Assert.IsNotNull(fromThread, "后台线程应能拿到实例");
            Assert.AreSame(first, fromThread, "多线程获取的必须是同一实例");
        }

        // ===== MainThreadDispatcher =====

        [Test]
        public void Dispatcher_Post_ExecutedInOrder()
        {
            var dispatcher = _dispatcherGo.GetComponent<MainThreadDispatcher>();
            var order = new System.Collections.Generic.List<int>();
            MainThreadDispatcher.Post(() => order.Add(1));
            MainThreadDispatcher.Post(() => order.Add(2));
            MainThreadDispatcher.Post(() => order.Add(3));

            Assert.AreEqual(0, order.Count, "投递后未执行前不应有任何动作运行");

            dispatcher.ExecutePendingActions(Time.time);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, order);
        }

        [Test]
        public void Dispatcher_PostDelayed_OnlyExpiredRun()
        {
            var dispatcher = _dispatcherGo.GetComponent<MainThreadDispatcher>();
            int immediate = 0;
            int later = 0;
            float now = Time.time;
            MainThreadDispatcher.PostDelayed(() => immediate++, 0.1f);   // 0.1s 后到期
            MainThreadDispatcher.PostDelayed(() => later++, 10f);        // 10s 后到期

            dispatcher.ExecutePendingActions(now);                       // 未到期：都不执行
            Assert.AreEqual(0, immediate);
            Assert.AreEqual(0, later);

            dispatcher.ExecutePendingActions(now + 0.2f);                // 只执行已到期
            Assert.AreEqual(1, immediate);
            Assert.AreEqual(0, later);

            dispatcher.ExecutePendingActions(now + 11f);                 // 全部到期
            Assert.AreEqual(1, immediate);
            Assert.AreEqual(1, later);
        }

        [Test]
        public void Dispatcher_RunOnMainThread_ExecutesImmediately()
        {
            // EditMode 测试运行在主线程
            Assert.IsTrue(MainThreadDispatcher.IsMainThread, "测试应运行在主线程");
            int calls = 0;
            MainThreadDispatcher.RunOnMainThread(() => calls++);
            Assert.AreEqual(1, calls, "主线程调用应立即执行");
        }

        [Test]
        public void Dispatcher_ExceptionInAction_IsIsolated()
        {
            var dispatcher = _dispatcherGo.GetComponent<MainThreadDispatcher>();
            int after = 0;
            MainThreadDispatcher.Post(() => throw new InvalidOperationException("模拟异常"));
            MainThreadDispatcher.Post(() => after++);

            // 异常应按条隔离并记录，后续动作照常执行；声明该错误日志为预期
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("主线程动作执行异常"));
            Assert.DoesNotThrow(() => dispatcher.ExecutePendingActions(Time.time));
            Assert.AreEqual(1, after);
        }

        [Test]
        public void Dispatcher_PostFromBackgroundThread_ExecutesOnMainThreadQueue()
        {
            var dispatcher = _dispatcherGo.GetComponent<MainThreadDispatcher>();
            int executed = 0;
            bool onMainThread = false;
            var thread = new Thread(() =>
            {
                MainThreadDispatcher.Post(() =>
                {
                    executed++;
                    onMainThread = MainThreadDispatcher.IsMainThread;
                });
            });
            thread.Start();
            thread.Join();

            dispatcher.ExecutePendingActions(Time.time);

            Assert.AreEqual(1, executed, "后台线程投递的动作应被主线程队列执行");
            Assert.IsTrue(onMainThread, "动作应在主线程执行");
        }
    }
}
