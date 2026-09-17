using System;
using System.Globalization;
using UnityEngine;

namespace CoffeeBean
{
    /// <summary>
    /// 设备地区 / 语言（原生平台）。
    ///
    /// 取值来源按平台优先级：
    /// · **Android**：JNI 直读 <c>java.util.Locale.getDefault()</c> ——
    ///   <c>getLanguage()</c>（ISO 639-1）、<c>getCountry()</c>（ISO 3166-1 alpha-2）、
    ///   <c>getScript()</c>，以及 <c>getDisplayLanguage/getDisplayCountry(Locale.ENGLISH)</c> 的英文名。
    ///   （Unity 另有 <c>UnityEngine.Android.AndroidLocale</c>，但它没有公开构造函数、无法取实例，故不用。）
    /// · **iOS / 其它平台**：<see cref="CultureInfo.CurrentCulture"/> —— Unity 在启动时用系统区域设置
    ///   （iOS 为 <c>NSLocale</c>）初始化它，因此这就是系统原生值；区域取 <see cref="RegionInfo.CurrentRegion"/>。
    /// · **兜底**：<see cref="Application.systemLanguage"/> 映射到 ISO 639-1。
    ///
    /// 结果会缓存（区域/语言在一次运行内不会变）；需要重新读取时调 <see cref="Refresh"/>。
    ///
    /// 用法：
    /// <code>
    /// string lang = CDeviceLocale.LanguageCode;   // "zh"
    /// string country = CDeviceLocale.CountryCode; // "CN"
    /// CDeviceLocaleSnapshot snap = CDeviceLocale.Current;
    /// </code>
    /// </summary>
    public static class CDeviceLocale
    {
        private const string Tag = "CoffeeBean.Locale";

        /// <summary>从右往左书写的语言（ISO 639-1）。</summary>
        private static readonly string[] RtlLanguages = { "ar", "he", "fa", "ur", "yi", "dv", "ps", "sd", "ug" };

        private static bool _loaded;
        private static string _languageCode;
        private static string _countryCode;
        private static string _localeIdentifier;
        private static string _languageNameEnglish;
        private static string _countryNameEnglish;

        /// <summary>ISO 639-1 语言码，小写，如 <c>zh</c>、<c>en</c>。取不到时为空串。</summary>
        public static string LanguageCode { get { EnsureLoaded(); return _languageCode; } }

        /// <summary>ISO 3166-1 alpha-2 地区码，大写，如 <c>CN</c>、<c>US</c>。取不到时为空串。</summary>
        public static string CountryCode { get { EnsureLoaded(); return _countryCode; } }

        /// <summary>
        /// 组合标识：有地区时 <c>语言_地区</c>（如 <c>zh_CN</c>），否则只有语言（如 <c>en</c>）。
        /// 统一用下划线，便于与 <c>Resources</c> 语言目录 / 配置表命名对齐。
        /// </summary>
        public static string LocaleIdentifier { get { EnsureLoaded(); return _localeIdentifier; } }

        /// <summary>语言英文名（如 <c>Chinese</c>）；取不到时回退为语言码。</summary>
        public static string LanguageNameEnglish { get { EnsureLoaded(); return _languageNameEnglish; } }

        /// <summary>地区英文名（如 <c>China</c>）；取不到时回退为地区码。</summary>
        public static string CountryNameEnglish { get { EnsureLoaded(); return _countryNameEnglish; } }

        /// <summary>Unity 自己的语言枚举（无地区信息），可与其他模块 / 引擎 API 对齐。</summary>
        public static SystemLanguage UnityLanguage => Application.systemLanguage;

        /// <summary>当前语言是否从右往左书写。</summary>
        public static bool IsRightToLeft => IsRightToLeftLanguage(LanguageCode);

        /// <summary>给定 ISO 639-1 语言码是否从右往左书写。</summary>
        internal static bool IsRightToLeftLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode)) return false;
            for (int i = 0; i < RtlLanguages.Length; i++)
            {
                if (string.Equals(RtlLanguages[i], languageCode, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>一次性快照（避免多次读属性时反复取缓存/加锁）。</summary>
        public static CDeviceLocaleSnapshot Current
        {
            get
            {
                EnsureLoaded();
                return new CDeviceLocaleSnapshot(
                    _languageCode, _countryCode, _localeIdentifier,
                    _languageNameEnglish, _countryNameEnglish,
                    Application.systemLanguage, IsRightToLeft);
            }
        }

        /// <summary>重新读取当前平台区域 / 语言（清缓存后下次访问重取）。</summary>
        public static void Refresh()
        {
            _loaded = false;
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;

            try
            {
                ReadInto(out _languageCode, out _countryCode, out _languageNameEnglish, out _countryNameEnglish);
            }
            catch (Exception e)
            {
                CLog.Warn(Tag, $"读取系统区域/语言失败，回退默认值: {e.Message}");
                _languageCode = string.Empty;
                _countryCode = string.Empty;
                _languageNameEnglish = string.Empty;
                _countryNameEnglish = string.Empty;
            }

            _languageCode = NormalizeLanguageCode(_languageCode);
            _countryCode = NormalizeCountryCode(_countryCode);

            // 语言仍为空 → 用 Unity 的枚举兜底
            if (string.IsNullOrEmpty(_languageCode))
            {
                _languageCode = NormalizeLanguageCode(SystemLanguageToIso639(Application.systemLanguage));
            }

            _languageNameEnglish = string.IsNullOrEmpty(_languageNameEnglish) ? _languageCode : _languageNameEnglish;
            _countryNameEnglish = string.IsNullOrEmpty(_countryNameEnglish) ? _countryCode : _countryNameEnglish;

            _localeIdentifier = string.IsNullOrEmpty(_countryCode)
                ? _languageCode
                : _languageCode + "_" + _countryCode;

            _loaded = true;
        }

        /// <summary>按平台读取原生值。任一平台失败都向上抛，由调用方兜底。</summary>
        private static void ReadInto(out string language, out string country,
            out string languageNameEnglish, out string countryNameEnglish)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ReadAndroid(out language, out country, out languageNameEnglish, out countryNameEnglish);
#else
            ReadManaged(out language, out country, out languageNameEnglish, out countryNameEnglish);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>Android：直读 java.util.Locale.getDefault()。</summary>
        private static void ReadAndroid(out string language, out string country,
            out string languageNameEnglish, out string countryNameEnglish)
        {
            language = null; country = null; languageNameEnglish = null; countryNameEnglish = null;

            AndroidJavaClass localeClass = null;
            AndroidJavaObject locale = null;
            AndroidJavaClass englishLocale = null;
            try
            {
                localeClass = new AndroidJavaClass("java.util.Locale");
                locale = localeClass.CallStatic<AndroidJavaObject>("getDefault");
                if (locale == null) return;

                language = locale.Call<string>("getLanguage");   // ISO 639-1
                country = locale.Call<string>("getCountry");     // ISO 3166-1 alpha-2

                // 英文显示名：getDisplayLanguage(Locale.ENGLISH) / getDisplayCountry(Locale.ENGLISH)
                englishLocale = new AndroidJavaClass("java.util.Locale").GetStatic<AndroidJavaObject>("ENGLISH");
                languageNameEnglish = locale.Call<string>("getDisplayLanguage", englishLocale);
                countryNameEnglish = locale.Call<string>("getDisplayCountry", englishLocale);
            }
            finally
            {
                locale?.Dispose();
                englishLocale?.Dispose();
                localeClass?.Dispose();
            }
        }
#else
        /// <summary>
        /// iOS / 编辑器 / 桌面：Unity 在启动时用系统区域设置（iOS 为 NSLocale）初始化
        /// <see cref="CultureInfo.CurrentCulture"/>，所以这里拿到的就是系统原生值。
        /// </summary>
        private static void ReadManaged(out string language, out string country,
            out string languageNameEnglish, out string countryNameEnglish)
        {
            language = null; country = null; languageNameEnglish = null; countryNameEnglish = null;

            CultureInfo culture = CultureInfo.CurrentCulture;
            if (culture != null)
            {
                language = culture.TwoLetterISOLanguageName;
                try
                {
                    languageNameEnglish = culture.EnglishName;
                }
                catch (Exception)
                {
                    // 少数文化（不变文化等）读 EnglishName 会抛异常 → 交给上层回退成语言码
                }
            }

            try
            {
                RegionInfo region = RegionInfo.CurrentRegion;
                if (region != null)
                {
                    country = region.TwoLetterISORegionName;
                    countryNameEnglish = region.EnglishName;
                }
            }
            catch (Exception)
            {
                // RegionInfo.CurrentRegion 在区域信息不可用时会抛异常 → 交给上层兜底
            }

            // 语言英文名去掉 "(China)" 这类后缀，只留语言本身
            if (!string.IsNullOrEmpty(languageNameEnglish))
            {
                int paren = languageNameEnglish.IndexOf(" (", StringComparison.Ordinal);
                if (paren > 0) languageNameEnglish = languageNameEnglish.Substring(0, paren);
            }
        }
#endif

        /// <summary>语言码归一化：转小写、去空白；<c>zh-Hans</c> 这类带 script 的取主语言。</summary>
        internal static string NormalizeLanguageCode(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            string s = raw.Trim();
            int cut = s.IndexOfAny(new[] { '-', '_' });
            if (cut > 0) s = s.Substring(0, cut);
            return s.ToLowerInvariant();
        }

        /// <summary>地区码归一化：转大写、去空白。</summary>
        internal static string NormalizeCountryCode(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            return raw.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Unity <see cref="SystemLanguage"/> → ISO 639-1（无法确定时返回空串）。
        /// 覆盖的是 Unity 实际定义的全部语言值 —— 注意 Unity 并没有 Persian / Urdu 等条目，
        /// 这些语言只能靠平台原生值（Android JNI / 系统 CultureInfo）拿到。
        /// </summary>
        internal static string SystemLanguageToIso639(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Afrikaans: return "af";
                case SystemLanguage.Arabic: return "ar";
                case SystemLanguage.Basque: return "eu";
                case SystemLanguage.Belarusian: return "be";
                case SystemLanguage.Bulgarian: return "bg";
                case SystemLanguage.Catalan: return "ca";
                case SystemLanguage.Chinese: return "zh";
                case SystemLanguage.ChineseSimplified: return "zh";
                case SystemLanguage.ChineseTraditional: return "zh";
                case SystemLanguage.Czech: return "cs";
                case SystemLanguage.Danish: return "da";
                case SystemLanguage.Dutch: return "nl";
                case SystemLanguage.English: return "en";
                case SystemLanguage.Estonian: return "et";
                case SystemLanguage.Faroese: return "fo";
                case SystemLanguage.Finnish: return "fi";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Greek: return "el";
                case SystemLanguage.Hebrew: return "he";
                case SystemLanguage.Hindi: return "hi";
                case SystemLanguage.Hungarian: return "hu";
                case SystemLanguage.Icelandic: return "is";
                case SystemLanguage.Indonesian: return "id";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Latvian: return "lv";
                case SystemLanguage.Lithuanian: return "lt";
                case SystemLanguage.Norwegian: return "no";
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Romanian: return "ro";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.SerboCroatian: return "sh";
                case SystemLanguage.Slovak: return "sk";
                case SystemLanguage.Slovenian: return "sl";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Swedish: return "sv";
                case SystemLanguage.Thai: return "th";
                case SystemLanguage.Turkish: return "tr";
                case SystemLanguage.Ukrainian: return "uk";
                case SystemLanguage.Vietnamese: return "vi";
                default: return string.Empty;
            }
        }
    }

    /// <summary>设备区域 / 语言的一次性快照。</summary>
    public readonly struct CDeviceLocaleSnapshot
    {
        /// <summary>ISO 639-1 语言码（小写）。</summary>
        public readonly string LanguageCode;

        /// <summary>ISO 3166-1 alpha-2 地区码（大写）。</summary>
        public readonly string CountryCode;

        /// <summary>组合标识：<c>语言_地区</c> 或只有语言。</summary>
        public readonly string LocaleIdentifier;

        /// <summary>语言英文名。</summary>
        public readonly string LanguageNameEnglish;

        /// <summary>地区英文名。</summary>
        public readonly string CountryNameEnglish;

        /// <summary>Unity 的语言枚举。</summary>
        public readonly SystemLanguage UnityLanguage;

        /// <summary>是否从右往左书写。</summary>
        public readonly bool IsRightToLeft;

        public CDeviceLocaleSnapshot(string languageCode, string countryCode, string localeIdentifier,
            string languageNameEnglish, string countryNameEnglish, SystemLanguage unityLanguage, bool isRightToLeft)
        {
            LanguageCode = languageCode;
            CountryCode = countryCode;
            LocaleIdentifier = localeIdentifier;
            LanguageNameEnglish = languageNameEnglish;
            CountryNameEnglish = countryNameEnglish;
            UnityLanguage = unityLanguage;
            IsRightToLeft = isRightToLeft;
        }

        public override string ToString() => LocaleIdentifier;
    }
}
