using System;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>CJson 工具测试。</summary>
    public class CJsonTests
    {
        [Serializable]
        private class SaveData
        {
            public int Level;
            public string Name;
        }

        [Test]
        public void ToJson_FromJson_Roundtrip()
        {
            var data = new SaveData { Level = 5, Name = "hero" };
            string json = CJson.ToJson(data);
            Assert.IsTrue(json.Contains("\"Level\":5"), "应包含字段");

            SaveData restored = CJson.FromJson<SaveData>(json);
            Assert.IsNotNull(restored);
            Assert.AreEqual(5, restored.Level);
            Assert.AreEqual("hero", restored.Name);
        }

        [Test]
        public void ToJson_Pretty_ContainsNewlines()
        {
            string json = CJson.ToJson(new SaveData { Level = 1 }, pretty: true);
            Assert.IsTrue(json.Contains("\n"), "美化输出应含换行");
        }

        [Test]
        public void FromJson_InvalidOrEmpty_ReturnsDefault()
        {
            Assert.IsNull(CJson.FromJson<SaveData>(null));
            Assert.IsNull(CJson.FromJson<SaveData>(""));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("JSON 反序列化失败"));
            Assert.IsNull(CJson.FromJson<SaveData>("{invalid json"));
        }

        [Test]
        public void FromJsonOverwrite_ReusesObject()
        {
            var target = new SaveData { Level = 1, Name = "old" };
            bool ok = CJson.FromJsonOverwrite("{\"Level\":9}", target);
            Assert.IsTrue(ok);
            Assert.AreEqual(9, target.Level);
            Assert.AreEqual("old", target.Name, "未出现在 JSON 的字段应保留");
        }

        [Test]
        public void IsValid_Checks()
        {
            Assert.IsTrue(CJson.IsValid("{\"a\":1}"));
            Assert.IsFalse(CJson.IsValid("not json"));
            Assert.IsFalse(CJson.IsValid(""));
            Assert.IsFalse(CJson.IsValid(null));
        }
    }
}
