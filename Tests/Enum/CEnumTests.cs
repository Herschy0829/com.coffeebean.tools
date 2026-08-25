using CoffeeBean.Tools;
using NUnit.Framework;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>CEnum 枚举工具测试。</summary>
    public class CEnumTests
    {
        private enum TestQuality { Low, Medium, High }

        [Test]
        public void Parse_IgnoresCase()
        {
            Assert.AreEqual(TestQuality.High, CEnum.Parse<TestQuality>("high"));
            Assert.AreEqual(TestQuality.High, CEnum.Parse<TestQuality>("HIGH"));
        }

        [Test]
        public void Parse_InvalidOrEmpty_ReturnsDefault()
        {
            Assert.AreEqual(TestQuality.Low, CEnum.Parse<TestQuality>("none", TestQuality.Low));
            Assert.AreEqual(TestQuality.Low, CEnum.Parse<TestQuality>("", TestQuality.Low));
            Assert.AreEqual(TestQuality.Low, CEnum.Parse<TestQuality>(null, TestQuality.Low));
        }

        [Test]
        public void GetValues_ReturnsAllAndCached()
        {
            TestQuality[] first = CEnum.GetValues<TestQuality>();
            TestQuality[] second = CEnum.GetValues<TestQuality>();

            Assert.AreEqual(3, first.Length);
            Assert.AreEqual(TestQuality.Low, first[0]);
            Assert.AreSame(first, second, "结果应缓存复用同一数组");
        }

        [Test]
        public void Count_MatchesValues()
        {
            Assert.AreEqual(CEnum.GetValues<TestQuality>().Length, CEnum.Count<TestQuality>());
        }
    }
}
