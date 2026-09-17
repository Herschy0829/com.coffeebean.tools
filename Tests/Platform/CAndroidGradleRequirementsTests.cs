using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>
    /// 框架级 Android Gradle 依赖登记表测试。
    /// 覆盖：登记去重、使用痕迹（含跨域重载的持久化）、源码扫描、构建前解析。
    /// </summary>
    public class CAndroidGradleRequirementsTests
    {
        private string _tmpRoot;

        [SetUp]
        public void SetUp()
        {
            CAndroidGradleRequirements.Clear();
            CAndroidGradleRequirements.ClearInAppReviewUsed();
            CAndroidGradleRequirements.EnableSourceScan = true;
            _tmpRoot = Path.Combine(Path.GetTempPath(), "CBAgpReq_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tmpRoot);
        }

        [TearDown]
        public void TearDown()
        {
            CAndroidGradleRequirements.Clear();
            CAndroidGradleRequirements.ClearInAppReviewUsed();
            CAndroidGradleRequirements.EnableSourceScan = true;
            try { if (Directory.Exists(_tmpRoot)) Directory.Delete(_tmpRoot, true); } catch { }
        }

        // ========== 登记 ==========

        [Test]
        public void Add_DeduplicatesByArtifact()
        {
            CAndroidGradleRequirements.Add("com.a:b:1.0", "第一次");
            CAndroidGradleRequirements.Add("com.a:b:1.0", "第二次");

            Assert.AreEqual(1, CAndroidGradleRequirements.All.Count);
            Assert.AreEqual("第一次", CAndroidGradleRequirements.All[0].Reason, "重复登记应保留最早那条");
        }

        [Test]
        public void Add_IgnoresEmptyArtifact()
        {
            CAndroidGradleRequirements.Add(null);
            CAndroidGradleRequirements.Add("");
            CAndroidGradleRequirements.Add("   ");
            Assert.AreEqual(0, CAndroidGradleRequirements.All.Count);
        }

        [Test]
        public void AddInAppReview_RegistersKnownArtifact()
        {
            CAndroidGradleRequirements.AddInAppReview();

            Assert.IsTrue(CAndroidGradleRequirements.Contains(CAndroidGradleRequirements.PlayInAppReviewArtifact));
            Assert.AreEqual(1, CAndroidGradleRequirements.All.Count);
            Assert.IsNotEmpty(CAndroidGradleRequirements.All[0].Reason, "应带上「为什么需要」的说明，便于排查构建日志");
        }

        [Test]
        public void Contains_IsCaseInsensitive()
        {
            CAndroidGradleRequirements.Add("com.A:B:1.0");
            Assert.IsTrue(CAndroidGradleRequirements.Contains("COM.a:b:1.0"));
        }

        [Test]
        public void ToGradleLine_UsesImplementationWithSingleQuotes()
        {
            var r = new CAndroidGradleRequirement("com.a:b:1.0", "x");
            Assert.AreEqual("implementation 'com.a:b:1.0'", r.ToGradleLine());
        }

        [Test]
        public void Clear_RemovesEverything()
        {
            CAndroidGradleRequirements.AddInAppReview();
            CAndroidGradleRequirements.Clear();
            Assert.AreEqual(0, CAndroidGradleRequirements.All.Count);
            Assert.IsFalse(CAndroidGradleRequirements.Contains(CAndroidGradleRequirements.PlayInAppReviewArtifact));
        }

        // ========== 使用痕迹 ==========

        [Test]
        public void MarkInAppReviewUsed_SetsFlagAndPersists()
        {
            Assert.IsFalse(CAndroidGradleRequirements.InAppReviewUsed);
            CAndroidGradleRequirements.MarkInAppReviewUsed();
            Assert.IsTrue(CAndroidGradleRequirements.InAppReviewUsed);
        }

        [Test]
        public void MarkInAppReviewUsed_SurvivesNewPrefsInstance()
        {
            // 持久化的意义：退出 Play 模式会重载域、静态标记会丢，但打包时仍要能被判定到
            CAndroidGradleRequirements.MarkInAppReviewUsed();
            bool persisted = new CPrefs("CoffeeBean.AndroidRequirements").GetBool("AppReviewUsed");
            Assert.IsTrue(persisted, "使用痕迹必须落到 PlayerPrefs，否则打包时判定不到");
        }

        [Test]
        public void ClearInAppReviewUsed_ResetsFlag()
        {
            CAndroidGradleRequirements.MarkInAppReviewUsed();
            CAndroidGradleRequirements.ClearInAppReviewUsed();
            Assert.IsFalse(CAndroidGradleRequirements.InAppReviewUsed);
        }

        // ========== 源码扫描 ==========

        [Test]
        public void SourceContains_FindsTokenInNestedFile()
        {
            string dir = Path.Combine(_tmpRoot, "Assets", "Scripts", "Deep");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Game.cs"), "// x\nvoid F() { CAppReview.Request(); }\n");

            Assert.IsTrue(CAndroidGradleRequirements.SourceContains(Path.Combine(_tmpRoot, "Assets"), "CAppReview"));
        }

        [Test]
        public void SourceContains_FalseWhenAbsent()
        {
            string dir = Path.Combine(_tmpRoot, "Assets");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Other.cs"), "void F() { }\n");

            Assert.IsFalse(CAndroidGradleRequirements.SourceContains(dir, "CAppReview"));
        }

        [Test]
        public void SourceContains_MissingDirectory_ReturnsFalse()
        {
            Assert.IsFalse(CAndroidGradleRequirements.SourceContains(Path.Combine(_tmpRoot, "不存在"), "CAppReview"));
        }

        [Test]
        public void SourceContains_NullArgs_ReturnsFalse()
        {
            Assert.IsFalse(CAndroidGradleRequirements.SourceContains(null, "x"));
            Assert.IsFalse(CAndroidGradleRequirements.SourceContains(_tmpRoot, null));
        }

        [Test]
        public void SourceContains_IgnoresNonCsFiles()
        {
            string dir = Path.Combine(_tmpRoot, "Assets");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "notes.txt"), "CAppReview");

            Assert.IsFalse(CAndroidGradleRequirements.SourceContains(dir, "CAppReview"), "只扫描 .cs");
        }

        [Test]
        public void SourceReferencesAppReview_UsesAppReviewToken()
        {
            string dir = Path.Combine(_tmpRoot, "Assets");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "A.cs"), "CAppReview.MarkUsed();\n");

            Assert.IsTrue(CAndroidGradleRequirements.SourceReferencesAppReview(dir));
        }

        // ========== 构建前解析 ==========

        [Test]
        public void ResolveForBuild_WhenUsed_MarksReviewRequired()
        {
            CAndroidGradleRequirements.MarkInAppReviewUsed();

            var resolved = CAndroidGradleRequirements.ResolveForBuild();

            Assert.AreEqual(1, resolved.Count);
            StringAssert.Contains("play:review", resolved[0].Artifact);
        }

        [Test]
        public void ResolveForBuild_WhenSourceScanDisabledAndNotUsed_ReturnsEmpty()
        {
            CAndroidGradleRequirements.EnableSourceScan = false;

            var resolved = CAndroidGradleRequirements.ResolveForBuild();

            Assert.AreEqual(0, resolved.Count, "项目自己用了 CAppReview 时才会命中，这里什么都没登记");
        }

        [Test]
        public void ResolveForBuild_IsIdempotent()
        {
            CAndroidGradleRequirements.MarkInAppReviewUsed();
            CAndroidGradleRequirements.ResolveForBuild();
            var second = CAndroidGradleRequirements.ResolveForBuild();
            Assert.AreEqual(1, second.Count, "重复解析不应重复登记");
        }

        [Test]
        public void ResolveForBuild_KeepsExplicitlyAddedItems()
        {
            CAndroidGradleRequirements.EnableSourceScan = false; // 本条只验"显式登记不被清掉"
            CAndroidGradleRequirements.Add("com.vendor:thing:9.9", "业务自己登记的");

            var resolved = CAndroidGradleRequirements.ResolveForBuild();

            Assert.IsTrue(CAndroidGradleRequirements.Contains("com.vendor:thing:9.9"));
            Assert.AreEqual(1, resolved.Count);
        }

        [Test]
        public void PlayInAppReviewArtifact_IsPinnedToKnownGoodVersion()
        {
            // 版本来自 Google Maven 元数据；改成新版本时请同步更新这条断言与 CHANGELOG
            Assert.AreEqual("com.google.android.play:review:2.0.2", CAndroidGradleRequirements.PlayInAppReviewArtifact);
        }
    }
}
