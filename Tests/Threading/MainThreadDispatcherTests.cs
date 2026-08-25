using System;
using System.Threading;
using CoffeeBean.Tools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>MainThreadDispatcher 测试（EditMode：手动创建组件，直接驱动 ExecutePendingActions）。</summary>
    public class MainThreadDispatcherTests
    {
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

        [Test]
        public void Post_ExecutedInOrder()
        {
            var dispatcher = _dispatcherGo.GetComponent<MainThreadDispatcher>();
            var order = new System.Collections.Generic.List<int>();
            MainThreadDispatcher.Post(() => order.Add(1));
            MainThreadDispatcher.Post(() => order.Add(2));
            MainThreadDispatcher.Post(() => order.Add(3));

            Assert.AreEqual(0, order.Count, "投递后未执行前不应有任何动作运行");

            dispatcher.ExecutePendingActions(Time.time, Time.unscaledTime);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, order);
        }

        [Test]
        public void PostDelayed_OnlyExpiredRun()
        {
            var dispatcher = _dispatcherGo.GetComponent<MainThreadDispatcher>();
            int immediate = 0;
            int later = 0;
            float now = Time.time;
            MainThreadDispatcher.PostDelayed(() => immediate++, 0.1f);
            MainThreadDispatcher.PostDelayed(() => later++, 10f);

            dispatcher.ExecutePendingActions(now, Time.unscaledTime);
            Assert.AreEqual(0, immediate);
            Assert.AreEqual(0, later);

            dispatcher.ExecutePendingActions(now + 0.2f, Time.unscaledTime);
            Assert.AreEqual(1, immediate);
            Assert.AreEqual(0, later);

            dispatcher.ExecutePendingActions(now + 11f, Time.unscaledTime);
            Assert.AreEqual(1, immediate);
            Assert.AreEqual(1, later);
        }

        [Test]
        public void PostDelayedUnscaled_UsesUnscaledClock()
        {
            var dispatcher = _dispatcherGo.GetComponent<MainThreadDispatcher>();
            int scaled = 0;
            int unscaled = 0;
            float now = Time.time;
            float unow = Time.unscaledTime;
            MainThreadDispatcher.PostDelayed(() => scaled++, 10f);          // 基于 Time.time
            MainThreadDispatcher.PostDelayedUnscaled(() => unscaled++, 0.1f); // 基于 Time.unscaledTime

            // scaled 时钟前进 11s、unscaled 时钟原地：只有 PostDelayed 到期
            dispatcher.ExecutePendingActions(now + 11f, unow);
            Assert.AreEqual(1, scaled);
            Assert.AreEqual(0, unscaled);

            // scaled 时钟原地、unscaled 时钟前进 0.2s：只有 PostDelayedUnscaled 到期
            dispatcher.ExecutePendingActions(now, unow + 0.2f);
            Assert.AreEqual(1, scaled);
            Assert.AreEqual(1, unscaled);
        }

        [Test]
        public void RunOnMainThread_ExecutesImmediately()
        {
            Assert.IsTrue(MainThreadDispatcher.IsMainThread, "测试应运行在主线程");
            int calls = 0;
            MainThreadDispatcher.RunOnMainThread(() => calls++);
            Assert.AreEqual(1, calls, "主线程调用应立即执行");
        }

        [Test]
        public void ExceptionInAction_IsIsolated()
        {
            var dispatcher = _dispatcherGo.GetComponent<MainThreadDispatcher>();
            int after = 0;
            MainThreadDispatcher.Post(() => throw new InvalidOperationException("模拟异常"));
            MainThreadDispatcher.Post(() => after++);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("主线程动作执行异常"));
            Assert.DoesNotThrow(() => dispatcher.ExecutePendingActions(Time.time, Time.unscaledTime));
            Assert.AreEqual(1, after);
        }

        [Test]
        public void PostFromBackgroundThread_ExecutesOnMainThreadQueue()
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

            dispatcher.ExecutePendingActions(Time.time, Time.unscaledTime);

            Assert.AreEqual(1, executed, "后台线程投递的动作应被主线程队列执行");
            Assert.IsTrue(onMainThread, "动作应在主线程执行");
        }
    }
}
