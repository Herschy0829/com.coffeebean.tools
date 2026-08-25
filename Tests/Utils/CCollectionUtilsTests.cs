using System.Collections.Generic;
using CoffeeBean.Tools;
using NUnit.Framework;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>CCollectionUtils 工具测试。</summary>
    public class CCollectionUtilsTests
    {
        [Test]
        public void IsNullOrEmpty_Checks()
        {
            Assert.IsTrue(CCollectionUtils.IsNullOrEmpty((List<int>)null));
            Assert.IsTrue(CCollectionUtils.IsNullOrEmpty(new List<int>()));
            Assert.IsFalse(CCollectionUtils.IsNullOrEmpty(new List<int> { 1 }));
        }

        [Test]
        public void GetOrDefault_SafeIndexing()
        {
            var list = new List<int> { 10, 20, 30 };
            Assert.AreEqual(10, CCollectionUtils.GetOrDefault(list, 0));
            Assert.AreEqual(0, CCollectionUtils.GetOrDefault(list, 5));          // 越界
            Assert.AreEqual(-1, CCollectionUtils.GetOrDefault(list, -1, -1));    // 负索引
            Assert.AreEqual(0, CCollectionUtils.GetOrDefault((List<int>)null, 0));
        }

        [Test]
        public void GetRandom_WithSeededRandom_Deterministic()
        {
            var list = new List<int> { 1, 2, 3, 4, 5 };
            var randomA = new System.Random(42);
            var randomB = new System.Random(42);

            for (int i = 0; i < 10; i++)
            {
                int a = CCollectionUtils.GetRandom(list, randomA);
                int b = CCollectionUtils.GetRandom(list, randomB);
                Assert.AreEqual(a, b, "相同种子应产生相同结果");
            }
        }

        [Test]
        public void GetRandom_EmptyList_ReturnsDefault()
        {
            Assert.AreEqual(0, CCollectionUtils.GetRandom(new List<int>(), new System.Random(1)));
        }

        [Test]
        public void Swap_ExchangesElements()
        {
            var list = new List<int> { 1, 2, 3 };
            CCollectionUtils.Swap(list, 0, 2);
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, list);
            CCollectionUtils.Swap(list, 0, 99); // 越界静默
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, list);
        }

        [Test]
        public void AddRangeUnique_KeepsOrderAndDeduplicates()
        {
            var target = new List<int> { 1, 2 };
            CCollectionUtils.AddRangeUnique(target, new[] { 2, 3, 1, 4 });
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, target);
        }
    }
}
