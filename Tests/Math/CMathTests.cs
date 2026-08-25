using CoffeeBean.Tools;
using NUnit.Framework;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>CMath 数学工具测试。</summary>
    public class CMathTests
    {
        [Test]
        public void Remap_Linear()
        {
            Assert.AreEqual(0.5f, CMath.Remap(50f, 0f, 100f, 0f, 1f), 0.0001f);
            Assert.AreEqual(1f, CMath.Remap(100f, 0f, 100f, 0f, 1f), 0.0001f);
        }

        [Test]
        public void Remap_ZeroSourceRange_ReturnsToMin()
        {
            Assert.AreEqual(5f, CMath.Remap(10f, 3f, 3f, 5f, 9f), "源区间为零应返回 toMin");
        }

        [Test]
        public void IsBetween_IncludesBounds()
        {
            Assert.IsTrue(CMath.IsBetween(5f, 0f, 10f));
            Assert.IsTrue(CMath.IsBetween(0f, 0f, 10f), "下边界应包含");
            Assert.IsTrue(CMath.IsBetween(10f, 0f, 10f), "上边界应包含");
            Assert.IsFalse(CMath.IsBetween(-1f, 0f, 10f));
            Assert.IsFalse(CMath.IsBetween(11f, 0f, 10f));
        }

        [Test]
        public void Percent_ReturnsProgress()
        {
            Assert.AreEqual(0.5f, CMath.Percent(5f, 0f, 10f), 0.0001f);
            Assert.AreEqual(0f, CMath.Percent(0f, 0f, 10f), 0.0001f);
        }

        [Test]
        public void Wrap_Angles()
        {
            Assert.AreEqual(350f, CMath.Wrap(-10f, 0f, 360f), 0.001f);
            Assert.AreEqual(10f, CMath.Wrap(370f, 0f, 360f), 0.001f);
            Assert.AreEqual(90f, CMath.Wrap(90f, 0f, 360f), 0.001f);
        }

        [Test]
        public void Clamp_Generic()
        {
            Assert.AreEqual(10, CMath.Clamp(15, 0, 10));
            Assert.AreEqual(0, CMath.Clamp(-5, 0, 10));
            Assert.AreEqual(7, CMath.Clamp(7, 0, 10));
            Assert.AreEqual(1.5f, CMath.Clamp(1.5f, 0f, 2f), 0.0001f);
        }
    }
}
