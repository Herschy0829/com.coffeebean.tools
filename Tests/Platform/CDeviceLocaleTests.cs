using NUnit.Framework;
using UnityEngine;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>
    /// 设备地区 / 语言测试。
    /// 原生取值本身依赖真机（Android 走 JNI、iOS 走系统 CultureInfo），因此这里覆盖
    /// **可确定验证的部分**：归一化、Unity 枚举映射、右到左判定，以及快照的自洽性。
    /// </summary>
    public class CDeviceLocaleTests
    {
        // ========== 语言码归一化 ==========

        [Test]
        public void NormalizeLanguageCode_StripsScriptAndRegion()
        {
            Assert.AreEqual("zh", CDeviceLocale.NormalizeLanguageCode("zh-Hans"));
            Assert.AreEqual("zh", CDeviceLocale.NormalizeLanguageCode("zh-Hant-TW"));
            Assert.AreEqual("zh", CDeviceLocale.NormalizeLanguageCode("zh_CN"));
            Assert.AreEqual("en", CDeviceLocale.NormalizeLanguageCode("en-US"));
        }

        [Test]
        public void NormalizeLanguageCode_Lowercases()
        {
            Assert.AreEqual("en", CDeviceLocale.NormalizeLanguageCode("EN"));
            Assert.AreEqual("pt", CDeviceLocale.NormalizeLanguageCode("PT"));
        }

        [Test]
        public void NormalizeLanguageCode_HandlesEmptyAndNull()
        {
            Assert.AreEqual(string.Empty, CDeviceLocale.NormalizeLanguageCode(null));
            Assert.AreEqual(string.Empty, CDeviceLocale.NormalizeLanguageCode(""));
            Assert.AreEqual(string.Empty, CDeviceLocale.NormalizeLanguageCode("   "));
        }

        // ========== 地区码归一化 ==========

        [Test]
        public void NormalizeCountryCode_UppercasesAndTrims()
        {
            Assert.AreEqual("CN", CDeviceLocale.NormalizeCountryCode("cn"));
            Assert.AreEqual("US", CDeviceLocale.NormalizeCountryCode("  us "));
            Assert.AreEqual("CN", CDeviceLocale.NormalizeCountryCode("CN"));
        }

        [Test]
        public void NormalizeCountryCode_HandlesEmptyAndNull()
        {
            Assert.AreEqual(string.Empty, CDeviceLocale.NormalizeCountryCode(null));
            Assert.AreEqual(string.Empty, CDeviceLocale.NormalizeCountryCode(""));
        }

        // ========== SystemLanguage → ISO 639-1 ==========

        [Test]
        public void SystemLanguageToIso639_MapsCommonLanguages()
        {
            Assert.AreEqual("zh", CDeviceLocale.SystemLanguageToIso639(SystemLanguage.ChineseSimplified));
            Assert.AreEqual("zh", CDeviceLocale.SystemLanguageToIso639(SystemLanguage.ChineseTraditional));
            Assert.AreEqual("zh", CDeviceLocale.SystemLanguageToIso639(SystemLanguage.Chinese));
            Assert.AreEqual("en", CDeviceLocale.SystemLanguageToIso639(SystemLanguage.English));
            Assert.AreEqual("ja", CDeviceLocale.SystemLanguageToIso639(SystemLanguage.Japanese));
            Assert.AreEqual("ko", CDeviceLocale.SystemLanguageToIso639(SystemLanguage.Korean));
            Assert.AreEqual("ru", CDeviceLocale.SystemLanguageToIso639(SystemLanguage.Russian));
        }

        [Test]
        public void SystemLanguageToIso639_UnknownReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, CDeviceLocale.SystemLanguageToIso639(SystemLanguage.Unknown));
        }

        [Test]
        public void SystemLanguageToIso639_AllMappedValuesAreLowercaseTwoLetter()
        {
            // 防漂移：新增映射时不要写成 "ZH" 或 "zho" 这类形式
            foreach (SystemLanguage lang in System.Enum.GetValues(typeof(SystemLanguage)))
            {
                string code = CDeviceLocale.SystemLanguageToIso639(lang);
                if (string.IsNullOrEmpty(code)) continue;
                Assert.AreEqual(2, code.Length, lang + " 映射不是两字母: " + code);
                Assert.AreEqual(code.ToLowerInvariant(), code, lang + " 映射不是小写: " + code);
            }
        }

        // ========== 右到左 ==========

        [Test]
        public void IsRightToLeftLanguage_RecognizesRtlLanguages()
        {
            foreach (string code in new[] { "ar", "he", "fa", "ur", "yi", "dv", "ps", "sd", "ug" })
            {
                Assert.IsTrue(CDeviceLocale.IsRightToLeftLanguage(code), code + " 应判为 RTL");
            }
        }

        [Test]
        public void IsRightToLeftLanguage_LtrLanguagesAreFalse()
        {
            foreach (string code in new[] { "en", "zh", "ja", "ko", "de", "fr", "ru" })
            {
                Assert.IsFalse(CDeviceLocale.IsRightToLeftLanguage(code), code + " 不应判为 RTL");
            }
        }

        [Test]
        public void IsRightToLeftLanguage_EmptyIsFalse()
        {
            Assert.IsFalse(CDeviceLocale.IsRightToLeftLanguage(null));
            Assert.IsFalse(CDeviceLocale.IsRightToLeftLanguage(""));
        }

        [Test]
        public void IsRightToLeftLanguage_IsCaseInsensitive()
        {
            Assert.IsTrue(CDeviceLocale.IsRightToLeftLanguage("AR"));
        }

        // ========== 运行时快照 ==========

        [Test]
        public void Current_HasNormalizedFields()
        {
            CDeviceLocale.Refresh();
            CDeviceLocaleSnapshot snap = CDeviceLocale.Current;

            Assert.IsNotNull(snap.LanguageCode);
            Assert.AreEqual(snap.LanguageCode.ToLowerInvariant(), snap.LanguageCode, "语言码应为小写");
            Assert.AreEqual(snap.CountryCode.ToUpperInvariant(), snap.CountryCode, "地区码应为大写");

            if (!string.IsNullOrEmpty(snap.LanguageCode) && !string.IsNullOrEmpty(snap.CountryCode))
            {
                Assert.AreEqual(snap.LanguageCode + "_" + snap.CountryCode, snap.LocaleIdentifier);
            }
            else
            {
                Assert.AreEqual(snap.LanguageCode, snap.LocaleIdentifier, "无地区时标识应只有语言");
            }
        }

        [Test]
        public void Current_IsCachedAndStableUntilRefresh()
        {
            CDeviceLocale.Refresh();
            CDeviceLocaleSnapshot first = CDeviceLocale.Current;
            CDeviceLocaleSnapshot second = CDeviceLocale.Current;

            Assert.AreEqual(first.LanguageCode, second.LanguageCode);
            Assert.AreEqual(first.CountryCode, second.CountryCode);
            Assert.AreEqual(first.LocaleIdentifier, second.LocaleIdentifier);
        }

        [Test]
        public void Current_MirrorsUnitySystemLanguage()
        {
            CDeviceLocaleSnapshot snap = CDeviceLocale.Current;
            Assert.AreEqual(Application.systemLanguage, snap.UnityLanguage);
            Assert.AreEqual(Application.systemLanguage, CDeviceLocale.UnityLanguage);
        }

        [Test]
        public void Current_LocaleIdentifierNeverHasTrailingSeparator()
        {
            CDeviceLocale.Refresh();
            string id = CDeviceLocale.Current.LocaleIdentifier;
            if (!string.IsNullOrEmpty(id))
            {
                Assert.IsFalse(id.EndsWith("_"), "标识不应以分隔符结尾: " + id);
                Assert.IsFalse(id.StartsWith("_"), "标识不应以分隔符开头: " + id);
            }
        }

        [Test]
        public void Snapshot_ToStringIsLocaleIdentifier()
        {
            var snap = new CDeviceLocaleSnapshot("zh", "CN", "zh_CN", "Chinese", "China",
                SystemLanguage.ChineseSimplified, false);
            Assert.AreEqual("zh_CN", snap.ToString());
        }
    }
}
