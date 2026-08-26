using System;
using System.Threading;
using NUnit.Framework;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>CSingleton（纯 C# 单例）测试。</summary>
    public class CSingletonTests
    {
        private sealed class PlainSingletonImpl : CSingleton<PlainSingletonImpl> { }

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
                    if (local != null) Interlocked.CompareExchange(ref fromThread, local, null);
                });
                threads[i].Start();
            }
            foreach (Thread t in threads) t.Join();

            Assert.IsNotNull(fromThread, "后台线程应能拿到实例");
            Assert.AreSame(first, fromThread, "多线程获取的必须是同一实例");
        }
    }
}
