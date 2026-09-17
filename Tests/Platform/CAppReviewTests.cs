using System;
using NUnit.Framework;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>
    /// 应用内评价测试。
    /// 真机弹窗无法在 EditMode 里断言（系统还不保证一定弹），因此这里覆盖
    /// **可确定验证的部分**：冷却判定、商店地址拼接、编辑器下的降级行为。
    /// </summary>
    public class CAppReviewTests
    {
        private int _savedCooldownDays;
        private bool _savedUseCooldown;
        private string _savedAndroidPackage;
        private string _savedIosAppId;

        [SetUp]
        public void SetUp()
        {
            _savedCooldownDays = CAppReview.CooldownDays;
            _savedUseCooldown = CAppReview.UseCooldown;
            _savedAndroidPackage = CAppReview.AndroidPackageName;
            _savedIosAppId = CAppReview.IosAppId;

            CAppReview.ResetCooldown();
        }

        [TearDown]
        public void TearDown()
        {
            CAppReview.CooldownDays = _savedCooldownDays;
            CAppReview.UseCooldown = _savedUseCooldown;
            CAppReview.AndroidPackageName = _savedAndroidPackage;
            CAppReview.IosAppId = _savedIosAppId;
            CAppReview.ResetCooldown();
        }

        // ========== 冷却判定 ==========

        [Test]
        public void ComputeIsOnCooldown_NoPreviousRequest_IsFalse()
        {
            var now = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
            Assert.IsFalse(CAppReview.ComputeIsOnCooldown(now, null, 90, true));
        }

        [Test]
        public void ComputeIsOnCooldown_WithinWindow_IsTrue()
        {
            var now = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
            Assert.IsTrue(CAppReview.ComputeIsOnCooldown(now, now.AddDays(-1), 90, true));
            Assert.IsTrue(CAppReview.ComputeIsOnCooldown(now, now.AddDays(-89), 90, true));
        }

        [Test]
        public void ComputeIsOnCooldown_AfterWindow_IsFalse()
        {
            var now = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
            Assert.IsFalse(CAppReview.ComputeIsOnCooldown(now, now.AddDays(-91), 90, true));
            Assert.IsFalse(CAppReview.ComputeIsOnCooldown(now, now.AddDays(-365), 90, true));
        }

        [Test]
        public void ComputeIsOnCooldown_ExactlyAtBoundary_IsFalse()
        {
            // 恰好满 90 天即视为可再次请求（now < last + 90d 为 false）
            var now = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
            Assert.IsFalse(CAppReview.ComputeIsOnCooldown(now, now.AddDays(-90), 90, true));
        }

        [Test]
        public void ComputeIsOnCooldown_DisabledOrZeroDays_IsFalse()
        {
            var now = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
            Assert.IsFalse(CAppReview.ComputeIsOnCooldown(now, now.AddDays(-1), 90, false), "UseCooldown=false 应永不冷却");
            Assert.IsFalse(CAppReview.ComputeIsOnCooldown(now, now.AddDays(-1), 0, true), "0 天应视为不限制");
            Assert.IsFalse(CAppReview.ComputeIsOnCooldown(now, now.AddDays(-1), -5, true), "负数天应视为不限制");
        }

        [Test]
        public void IsOnCooldown_CleanState_IsFalse()
        {
            Assert.IsFalse(CAppReview.IsOnCooldown);
            Assert.IsNull(CAppReview.LastRequestUtc);
        }

        // ========== 商店地址 ==========

        [Test]
        public void BuildAndroidStoreUrl_MarketScheme()
        {
            Assert.AreEqual("market://details?id=com.foo.bar",
                CAppReview.BuildAndroidStoreUrl("com.foo.bar", useMarketScheme: true));
        }

        [Test]
        public void BuildAndroidStoreUrl_HttpsFallback()
        {
            Assert.AreEqual("https://play.google.com/store/apps/details?id=com.foo.bar",
                CAppReview.BuildAndroidStoreUrl("com.foo.bar", useMarketScheme: false));
        }

        [Test]
        public void BuildAndroidStoreUrl_EmptyPackage_UsesPlaceholderNotMalformedUrl()
        {
            string url = CAppReview.BuildAndroidStoreUrl(null, useMarketScheme: true);
            StringAssert.StartsWith("market://details?id=", url);
            Assert.IsFalse(url.EndsWith("id="), "包名为空时不应拼出缺少 id 的地址: " + url);
        }

        [Test]
        public void BuildIosStoreUrl_WithAppId_PointsToWriteReview()
        {
            Assert.AreEqual("itms-apps://itunes.apple.com/app/id1234567890?action=write-review",
                CAppReview.BuildIosStoreUrl("1234567890"));
        }

        [Test]
        public void BuildIosStoreUrl_WithoutAppId_FallsBackToGenericStore()
        {
            Assert.AreEqual("https://apps.apple.com/", CAppReview.BuildIosStoreUrl(null));
            Assert.AreEqual("https://apps.apple.com/", CAppReview.BuildIosStoreUrl(""));
        }

        // ========== 编辑器下的降级行为 ==========

        [Test]
        public void IsSupported_IsFalseInEditor()
        {
            // EditMode 测试恒在编辑器里跑；真机才支持原生应用内评价
            Assert.IsFalse(CAppReview.IsSupported);
        }

        [Test]
        public void Request_InEditor_ReturnsNotSupported()
        {
            CAppReviewResult observed = (CAppReviewResult)(-1);
            CAppReviewResult returned = CAppReview.Request(r => observed = r);

            Assert.AreEqual(CAppReviewResult.NotSupported, returned);
            Assert.AreEqual(CAppReviewResult.NotSupported, observed, "回调也应收到 NotSupported");
        }

        [Test]
        public void Request_InEditor_DoesNotRecordCooldown()
        {
            CAppReview.Request();
            Assert.IsNull(CAppReview.LastRequestUtc, "不支持的平台不应写入冷却记录，否则真机上首次请求会被误判为冷却");
        }

        [Test]
        public void Request_OnCooldown_ShortCircuitsWithoutTouchingPlatform()
        {
            // 直接构造冷却状态：把上次请求时间设成"刚刚"
            CAppReview.CooldownDays = 90;
            CAppReview.UseCooldown = true;
            SetLastRequestUtc(DateTime.UtcNow);

            CAppReviewResult observed = (CAppReviewResult)(-1);
            CAppReviewResult returned = CAppReview.Request(r => observed = r);

            Assert.AreEqual(CAppReviewResult.OnCooldown, returned);
            Assert.AreEqual(CAppReviewResult.OnCooldown, observed);
        }

        [Test]
        public void ResetCooldown_ClearsRecord()
        {
            SetLastRequestUtc(DateTime.UtcNow);
            Assert.IsTrue(CAppReview.IsOnCooldown);

            CAppReview.ResetCooldown();
            Assert.IsFalse(CAppReview.IsOnCooldown);
            Assert.IsNull(CAppReview.LastRequestUtc);
        }

        [Test]
        public void LastRequestUtc_RoundTripsThroughPrefs()
        {
            var stamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            SetLastRequestUtc(stamp);

            DateTime? read = CAppReview.LastRequestUtc;
            Assert.IsNotNull(read);
            Assert.AreEqual(stamp, read.Value);
            Assert.AreEqual(DateTimeKind.Utc, read.Value.Kind);
        }

        [Test]
        public void ResultEnum_ValuesAreStable()
        {
            // 枚举是公开契约，数值不要随意变动
            Assert.AreEqual(0, (int)CAppReviewResult.Requested);
            Assert.AreEqual(1, (int)CAppReviewResult.NotSupported);
            Assert.AreEqual(2, (int)CAppReviewResult.OnCooldown);
            Assert.AreEqual(3, (int)CAppReviewResult.Unavailable);
            Assert.AreEqual(4, (int)CAppReviewResult.Failed);
        }

        /// <summary>写入"上次请求时间"（复用生产代码的存储格式与键名，避免测试自造格式而失真）。</summary>
        private static void SetLastRequestUtc(DateTime utc)
        {
            new CPrefs("CoffeeBean.AppReview").SetString("LastRequestUtcTicks", utc.Ticks.ToString());
        }
    }
}
