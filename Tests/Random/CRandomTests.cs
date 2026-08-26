using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>CRandom 随机工具测试。</summary>
    public class CRandomTests
    {
        [Test]
        public void WeightedPick_ZeroWeights_PicksOnlyOne()
        {
            var items = new List<string> { "a", "b", "c" };
            var weights = new List<float> { 0f, 0f, 1f };
            var random = new Random(42);

            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual("c", CRandom.WeightedPick(items, weights, random), "唯一非零权重应总是被选中");
            }
        }

        [Test]
        public void WeightedPick_CountMismatch_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                CRandom.WeightedPick(new List<int> { 1 }, new List<float> { 0.5f, 0.5f }, new Random(1)));
        }

        [Test]
        public void WeightedPick_NegativeWeight_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                CRandom.WeightedPick(new List<int> { 1, 2 }, new List<float> { 1f, -1f }, new Random(1)));
        }

        [Test]
        public void WeightedPick_TotalZero_ReturnsDefault()
        {
            Assert.AreEqual(0, CRandom.WeightedPick(new List<int> { 1, 2 }, new List<float> { 0f, 0f }, new Random(1)));
            Assert.AreEqual(0, CRandom.WeightedPick(new List<int>(), new List<float>(), new Random(1)));
        }

        [Test]
        public void Shuffle_PreservesElements()
        {
            var original = new List<int> { 1, 2, 3, 4, 5 };
            var shuffled = new List<int>(original);
            CRandom.Shuffle(shuffled, new Random(42));

            CollectionAssert.AreNotEqual(original, shuffled, "洗牌后顺序应变化");
            CollectionAssert.AreEquivalent(original, shuffled, "洗牌后元素集合不变");
        }

        [Test]
        public void NextBool_ProbabilityBounds()
        {
            var random = new Random(1);
            Assert.IsFalse(CRandom.NextBool(random, 0f), "概率 0 恒为 false");
            Assert.IsTrue(CRandom.NextBool(random, 1f), "概率 1 恒为 true");
        }
    }
}
