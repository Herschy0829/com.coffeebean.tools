using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>
    /// Android Gradle 依赖兜底注入器测试（tools 不依赖 build 模块时使用的那份）。
    /// 重点：幂等、保留原内容、锚点缺失/文件缺失时的**非抛出**失败（兜底路径不该中断构建）。
    /// </summary>
    public class CAndroidGradleInjectorTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "CBAgpInject_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
        }

        private string WriteGradle(string content)
        {
            string path = Path.Combine(_root, "build.gradle");
            File.WriteAllText(path, content);
            return path;
        }

        private const string StandardGradle =
            "apply plugin: 'com.android.library'\n" +
            "\n" +
            "dependencies {\n" +
            "    implementation fileTree(dir: 'libs', include: ['*.jar'])\n" +
            "}\n";

        // ========== 正常注入 ==========

        [Test]
        public void TryEnsureDependencies_InjectsIntoDependenciesBlock()
        {
            string path = WriteGradle(StandardGradle);

            bool ok = CAndroidGradleInjector.TryEnsureDependencies(path,
                new[] { "com.google.android.play:review:2.0.2" }, out int inserted, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(1, inserted);

            string text = File.ReadAllText(path);
            StringAssert.Contains("implementation 'com.google.android.play:review:2.0.2'", text);
            StringAssert.Contains("implementation fileTree(dir: 'libs', include: ['*.jar'])", text); // 原内容保留
        }

        [Test]
        public void TryEnsureDependencies_IndentMatchesAnchorPlusFour()
        {
            string path = WriteGradle("dependencies {\n}\n");

            CAndroidGradleInjector.TryEnsureDependencies(path, new[] { "com.a:b:1" }, out _, out _);

            string[] lines = File.ReadAllLines(path);
            int idx = Array.FindIndex(lines, l => l.Contains("com.a:b:1"));
            Assert.Greater(idx, 0);
            Assert.AreEqual("    implementation 'com.a:b:1'", lines[idx]);
        }

        [Test]
        public void TryEnsureDependencies_IsIdempotent()
        {
            string path = WriteGradle(StandardGradle);
            var artifacts = new[] { "com.google.android.play:review:2.0.2" };

            CAndroidGradleInjector.TryEnsureDependencies(path, artifacts, out int first, out _);
            bool ok = CAndroidGradleInjector.TryEnsureDependencies(path, artifacts, out int second, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(1, first);
            Assert.AreEqual(0, second, "重复注入应为 0 行改动");

            string text = File.ReadAllText(path);
            Assert.AreEqual(1, CountOccurrences(text, "com.google.android.play:review:2.0.2"));
        }

        [Test]
        public void TryEnsureDependencies_MultipleArtifacts_AllInserted()
        {
            string path = WriteGradle(StandardGradle);

            CAndroidGradleInjector.TryEnsureDependencies(path,
                new[] { "com.a:b:1", "com.c:d:2", "com.e:f:3" }, out int inserted, out _);

            Assert.AreEqual(3, inserted);
            string text = File.ReadAllText(path);
            StringAssert.Contains("com.a:b:1", text);
            StringAssert.Contains("com.c:d:2", text);
            StringAssert.Contains("com.e:f:3", text);
        }

        [Test]
        public void TryEnsureDependencies_DuplicateWithinBatch_InsertedOnce()
        {
            string path = WriteGradle(StandardGradle);

            CAndroidGradleInjector.TryEnsureDependencies(path,
                new[] { "com.a:b:1", "com.a:b:1" }, out int inserted, out _);

            Assert.AreEqual(1, inserted);
        }

        [Test]
        public void TryEnsureDependencies_SkipsNullAndEmptyArtifacts()
        {
            string path = WriteGradle(StandardGradle);

            CAndroidGradleInjector.TryEnsureDependencies(path,
                new[] { null, "", "   " }, out int inserted, out string error);

            Assert.IsTrue(error == null, error);
            Assert.AreEqual(0, inserted);
        }

        // ========== 失败路径：不抛异常，返回 false + error ==========

        [Test]
        public void TryEnsureDependencies_MissingFile_ReturnsFalseWithError()
        {
            string missing = Path.Combine(_root, "nope.gradle");

            bool ok = CAndroidGradleInjector.TryEnsureDependencies(missing, new[] { "com.a:b:1" },
                out int inserted, out string error);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, inserted);
            StringAssert.Contains("不存在", error);
        }

        [Test]
        public void TryEnsureDependencies_MissingAnchor_ReturnsFalseWithError()
        {
            string path = WriteGradle("apply plugin: 'x'\n");

            bool ok = CAndroidGradleInjector.TryEnsureDependencies(path, new[] { "com.a:b:1" },
                out int inserted, out string error);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, inserted);
            StringAssert.Contains("锚点", error);
        }

        [Test]
        public void TryEnsureDependencies_EmptyAnchor_ReturnsFalseWithError()
        {
            string path = WriteGradle(StandardGradle);

            bool ok = CAndroidGradleInjector.TryEnsureDependencies(path, "", new[] { "com.a:b:1" },
                out _, out string error);

            Assert.IsFalse(ok);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void TryEnsureDependencies_ExistingLineInDifferentIndent_NotDuplicated()
        {
            // 用户手工加过、缩进不同 → 按 Trim 判重，不应重复插入
            string path = WriteGradle("dependencies {\n        implementation 'com.a:b:1'\n}\n");

            CAndroidGradleInjector.TryEnsureDependencies(path, new[] { "com.a:b:1" }, out int inserted, out _);

            Assert.AreEqual(0, inserted);
            Assert.AreEqual(1, CountOccurrences(File.ReadAllText(path), "com.a:b:1"));
        }

        [Test]
        public void TryEnsureLines_CustomAnchor_Works()
        {
            string path = WriteGradle("repositories {\n}\ndependencies {\n}\n");

            bool ok = CAndroidGradleInjector.TryEnsureLines(path, "repositories {",
                new[] { "maven { url 'https://x' }" }, out int inserted, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(1, inserted);
            string[] lines = File.ReadAllLines(path);
            int reposIdx = Array.FindIndex(lines, l => l.Trim() == "repositories {");
            int mavenIdx = Array.FindIndex(lines, l => l.Contains("maven {"));
            Assert.Greater(mavenIdx, reposIdx);
            Assert.Less(mavenIdx, Array.FindIndex(lines, l => l.Trim() == "dependencies {"));
        }

        [Test]
        public void DependenciesAnchor_IsTheExpectedToken()
        {
            Assert.AreEqual("dependencies {", CAndroidGradleInjector.DependenciesAnchor);
        }

        private static int CountOccurrences(string text, string token)
        {
            int count = 0, i = 0;
            while ((i = text.IndexOf(token, i, StringComparison.Ordinal)) >= 0)
            {
                count++;
                i += token.Length;
            }
            return count;
        }
    }
}
