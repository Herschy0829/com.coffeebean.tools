using NUnit.Framework;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>CString 工具测试。</summary>
    public class CStringTests
    {
        [Test]
        public void FirstUpper_CapitalizesFirstChar()
        {
            Assert.AreEqual("Hero", CString.FirstUpper("hero"));
            Assert.AreEqual("Hero", CString.FirstUpper("Hero"));
            Assert.AreEqual("", CString.FirstUpper(""));
            Assert.IsNull(CString.FirstUpper(null));
        }

        [Test]
        public void ExtractDigits_KeepsOnlyDigits()
        {
            Assert.AreEqual("123", CString.ExtractDigits("abc12x3"));
            Assert.AreEqual("", CString.ExtractDigits("abc"));
            Assert.AreEqual("", CString.ExtractDigits(null));
        }

        [Test]
        public void ExtractLeadingNumber_HandlesPrefixes()
        {
            Assert.AreEqual(68f, CString.ExtractLeadingNumber("¥68"));
            Assert.AreEqual(1.99f, CString.ExtractLeadingNumber("$1.99"));
            Assert.AreEqual(1.2f, CString.ExtractLeadingNumber("v1.2"));
            Assert.AreEqual(0f, CString.ExtractLeadingNumber("abc"));
            Assert.AreEqual(0f, CString.ExtractLeadingNumber(""));
        }

        [Test]
        public void IsNumeric_Validates()
        {
            Assert.IsTrue(CString.IsNumeric("123"));
            Assert.IsTrue(CString.IsNumeric("1.5"));
            Assert.IsFalse(CString.IsNumeric("abc"));
            Assert.IsFalse(CString.IsNumeric(""));
        }

        [Test]
        public void Hex_Roundtrip()
        {
            byte[] bytes = { 0x1A, 0x2B, 0xFF, 0x00 };
            string hex = CString.BytesToHex(bytes);
            Assert.AreEqual("1A2BFF00", hex);
            CollectionAssert.AreEqual(bytes, CString.HexToBytes(hex));
        }

        [Test]
        public void Hex_InvalidInput_ReturnsNull()
        {
            Assert.IsNull(CString.HexToBytes("1A2"));   // 奇数长度
            Assert.IsNull(CString.HexToBytes("1Z2B"));   // 非法字符
            Assert.IsNull(CString.HexToBytes(null));
        }
    }
}
