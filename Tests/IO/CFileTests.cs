using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>CFile 文件工具测试（写入系统临时目录）。</summary>
    public class CFileTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "cb_tools_file_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        [Serializable]
        private class SaveData { public int Level; public string Name; }

        [Test]
        public void Text_Roundtrip()
        {
            string path = Path.Combine(_tempDir, "a", "b", "save.txt"); // 自动创建子目录
            CFile.WriteText(path, "hello");
            Assert.AreEqual("hello", CFile.ReadText(path));
        }

        [Test]
        public void Text_MissingFile_ReturnsDefault()
        {
            Assert.IsNull(CFile.ReadText(Path.Combine(_tempDir, "nope.txt")));
            Assert.AreEqual("d", CFile.ReadText(Path.Combine(_tempDir, "nope.txt"), "d"));
        }

        [Test]
        public void Bytes_Roundtrip()
        {
            string path = Path.Combine(_tempDir, "data.bin");
            byte[] data = { 0x01, 0x02, 0xFF };
            CFile.WriteBytes(path, data);
            CollectionAssert.AreEqual(data, CFile.ReadBytes(path));
        }

        [Test]
        public void Json_Roundtrip()
        {
            string path = Path.Combine(_tempDir, "save.json");
            CFile.WriteJson(path, new SaveData { Level = 9, Name = "hero" });
            SaveData loaded = CFile.ReadJson<SaveData>(path);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(9, loaded.Level);
            Assert.AreEqual("hero", loaded.Name);
        }

        [Test]
        public void Json_MissingFile_ReturnsDefault()
        {
            Assert.IsNull(CFile.ReadJson<SaveData>(Path.Combine(_tempDir, "nope.json")));
        }

        [Test]
        public void ExistsAndDelete()
        {
            string path = Path.Combine(_tempDir, "tmp.txt");
            Assert.IsFalse(CFile.Exists(path));
            CFile.WriteText(path, "x");
            Assert.IsTrue(CFile.Exists(path));
            CFile.Delete(path);
            Assert.IsFalse(CFile.Exists(path));
            CFile.Delete(path); // 幂等
        }

        [Test]
        public void WriteText_NullPath_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CFile.WriteText(null, "x"));
        }
    }
}
