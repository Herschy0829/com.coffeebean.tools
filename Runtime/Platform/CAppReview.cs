using System;
using UnityEngine;

namespace CoffeeBean
{
    /// <summary>应用内评价请求的结果。</summary>
    public enum CAppReviewResult
    {
        /// <summary>已请求原生评价流程。**是否真的弹出由系统决定**（配额、是否已评过等），调用方无法得知。</summary>
        Requested = 0,

        /// <summary>当前平台不支持原生应用内评价（编辑器 / 桌面等）。</summary>
        NotSupported = 1,

        /// <summary>处于冷却期内，本次跳过。</summary>
        OnCooldown = 2,

        /// <summary>平台支持，但环境不满足（Android 未接入 Google Play Core 的 review 依赖）。</summary>
        Unavailable = 3,

        /// <summary>调用过程中抛异常。</summary>
        Failed = 4,
    }

    /// <summary>
    /// 原生平台**应用内评价**（In-App Review）接口。
    ///
    /// · **iOS**：<c>UnityEngine.iOS.Device.RequestStoreReview()</c>（Unity 对
    ///   <c>SKStoreReviewController</c> 的封装）；iOS 14+ 需要 App 有活跃窗口。
    /// · **Android**：Google Play In-App Review —— 通过 JNI 调用
    ///   <c>com.google.android.play.core.review.ReviewManagerFactory</c>。
    ///   所需 Gradle 依赖 <c>com.google.android.play:review</c> **由框架自动写入**，无需手改 gradle：
    ///   一旦本接口被调用过（或工程源码里出现 <c>CAppReview</c>），打包 Android 时
    ///   <see cref="CAndroidGradleRequirements"/> 会把该依赖登记为必需项，
    ///   再由 **build 模块**（装了的话）或 **tools 自己的 Editor 兜底回调** 注入到
    ///   <c>unityLibrary/build.gradle</c>。框架不代为分发 Google 的二进制，只写一行 Maven 坐标。
    ///   万一依赖仍缺失，接口返回 <see cref="CAppReviewResult.Unavailable"/> 并给出可操作告警，
    ///   而不是静默失败。
    /// · **编辑器 / 其它平台**：返回 <see cref="CAppReviewResult.NotSupported"/>。
    ///
    /// **重要：系统可能不弹窗**。两家平台都对弹窗有硬性配额（Play 有周期配额；iOS 每个 App
    /// 每年最多 3 次），且不会告知调用方是否真的弹了。所以：
    /// · 不要把它当作「一定让玩家评分」的手段，也不要依赖它做引导流程；
    /// · 内置冷却（<see cref="CooldownDays"/>）避免频繁请求被系统静默丢弃；
    /// · 想要确定的入口，用 <see cref="OpenStorePage"/> 引导到商店评价页。
    ///
    /// 用法：
    /// <code>
    /// CAppReview.IosAppId = "1234567890";             // iOS 需要数字 App ID 才能直达商店页
    /// CAppReview.Request(r => Debug.Log(r));
    /// </code>
    /// </summary>
    public static class CAppReview
    {
        private const string Tag = "CoffeeBean.AppReview";
        private const string LastRequestPrefsKey = "LastRequestUtcTicks";

        /// <summary>冷却记录（放在自己的前缀下，避免与业务 PlayerPrefs 冲突；CPrefs 会自动补 "."）。</summary>
        private static readonly CPrefs Prefs = new CPrefs("CoffeeBean.AppReview");

        /// <summary>Android 商店地址用的包名；默认取 <see cref="Application.identifier"/>。</summary>
        public static string AndroidPackageName;

        /// <summary>iOS 数字 App ID（App Store Connect 里的 Apple ID）；为空时商店页只能打开通用地址。</summary>
        public static string IosAppId;

        /// <summary>两次评价请求的最小间隔天数（默认 90）。设为 0 表示不限制。</summary>
        public static int CooldownDays = 90;

        /// <summary>是否启用冷却。关掉后每次调用都会真的去请求（不推荐）。</summary>
        public static bool UseCooldown = true;

        /// <summary>当前平台是否支持原生应用内评价（仅 iOS / Android 真机）。</summary>
        public static bool IsSupported
        {
            get
            {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>上次请求时间（UTC）；从未请求过返回 null。</summary>
        public static DateTime? LastRequestUtc
        {
            get
            {
                string raw = Prefs.GetString(LastRequestPrefsKey);
                if (string.IsNullOrEmpty(raw)) return null;
                if (!long.TryParse(raw, out long ticks) || ticks <= 0) return null;
                return new DateTime(ticks, DateTimeKind.Utc);
            }
        }

        /// <summary>是否正处于冷却期。</summary>
        public static bool IsOnCooldown
            => ComputeIsOnCooldown(DateTime.UtcNow, LastRequestUtc, CooldownDays, UseCooldown);

        /// <summary>冷却判定的纯函数（便于单测）。</summary>
        internal static bool ComputeIsOnCooldown(DateTime nowUtc, DateTime? lastUtc, int cooldownDays, bool useCooldown)
        {
            if (!useCooldown || cooldownDays <= 0) return false;
            if (lastUtc == null) return false;
            return nowUtc < lastUtc.Value.AddDays(cooldownDays);
        }

        /// <summary>
        /// 请求原生应用内评价。
        /// Android 的流程是异步的，回调会在结果就绪时触发；iOS 为同步（但**是否弹窗仍由系统决定**）。
        /// </summary>
        /// <param name="onCompleted">结果回调（可空）。</param>
        /// <returns>本次请求的即时结果；Android 成功发起时返回 <see cref="CAppReviewResult.Requested"/>。</returns>
        public static CAppReviewResult Request(Action<CAppReviewResult> onCompleted = null)
        {
            // 只要被调用过就留下使用痕迹：Android 打包时需要据此自动写入 Play In-App Review 的
            // Gradle 依赖（见 CAndroidGradleRequirements）。放在最前面，是为了让"编辑器里调用"
            // "冷却期调用"这些不真正发起请求的情况也算数 —— 它们同样证明项目用到了本接口。
            CAndroidGradleRequirements.MarkInAppReviewUsed();

            if (IsOnCooldown)
            {
                CLog.Info(Tag, $"处于冷却期（{CooldownDays} 天），跳过本次评价请求。");
                onCompleted?.Invoke(CAppReviewResult.OnCooldown);
                return CAppReviewResult.OnCooldown;
            }

            if (!IsSupported)
            {
                CLog.Info(Tag, "当前平台不支持原生应用内评价（仅 iOS / Android 真机可用）。");
                onCompleted?.Invoke(CAppReviewResult.NotSupported);
                return CAppReviewResult.NotSupported;
            }

            MarkRequested();

#if UNITY_IOS && !UNITY_EDITOR
            return RequestIos(onCompleted);
#elif UNITY_ANDROID && !UNITY_EDITOR
            return RequestAndroid(onCompleted);
#else
            onCompleted?.Invoke(CAppReviewResult.NotSupported);
            return CAppReviewResult.NotSupported;
#endif
        }

        /// <summary>
        /// 打开商店详情页（评价入口的确定性兜底）。
        /// Android 优先用 <c>market://</c> 唤起 Play 应用，失败再退 <c>https://</c>；
        /// iOS 需要 <see cref="IosAppId"/> 才能直达带「写评价」的页面，否则打开通用地址。
        /// </summary>
        public static void OpenStorePage()
        {
            string packageName = string.IsNullOrEmpty(AndroidPackageName)
                ? Application.identifier
                : AndroidPackageName;

            string url;
#if UNITY_IOS && !UNITY_EDITOR
            url = BuildIosStoreUrl(IosAppId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            url = BuildAndroidStoreUrl(packageName, useMarketScheme: true);
#else
            // 编辑器/桌面：按当前构建目标给一个可点开的地址，便于调试
            url = Application.isEditor
                ? BuildAndroidStoreUrl(packageName, useMarketScheme: false)
                : BuildIosStoreUrl(IosAppId);
#endif

            CLog.Info(Tag, "打开商店页: " + url);
            try
            {
                Application.OpenURL(url);
            }
            catch (Exception e)
            {
                CLog.Error(Tag, "打开商店页失败: " + e.Message, e);
            }
        }

        /// <summary>
        /// 显式声明「本项目用到了应用内评价」。
        ///
        /// 正常调用 <see cref="Request"/> 会自动记录，**通常不需要手动调用**；
        /// 只有调用点位于源码扫描范围之外的程序集（例如你自建的包）时才需要补一次，
        /// 以确保打包 Android 时会自动写入 Play In-App Review 的 Gradle 依赖。
        /// </summary>
        public static void MarkUsed() => CAndroidGradleRequirements.MarkInAppReviewUsed();

        /// <summary>清空冷却记录，让下一次 <see cref="Request"/> 立刻生效（调试用）。</summary>
        public static void ResetCooldown()
        {
            Prefs.Delete(LastRequestPrefsKey);
        }

        // ========== 内部 ==========

        private static void MarkRequested()
        {
            Prefs.SetString(LastRequestPrefsKey, DateTime.UtcNow.Ticks.ToString());
        }

#if UNITY_IOS && !UNITY_EDITOR
        private static CAppReviewResult RequestIos(Action<CAppReviewResult> onCompleted)
        {
            try
            {
                bool ok = UnityEngine.iOS.Device.RequestStoreReview();
                var result = ok ? CAppReviewResult.Requested : CAppReviewResult.Failed;
                CLog.Info(Tag, "iOS RequestStoreReview 返回: " + ok);
                onCompleted?.Invoke(result);
                return result;
            }
            catch (Exception e)
            {
                CLog.Error(Tag, "iOS 评价请求异常: " + e.Message, e);
                onCompleted?.Invoke(CAppReviewResult.Failed);
                return CAppReviewResult.Failed;
            }
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// 持有 JNI 对象与监听器，避免在异步回调返回前被 GC 回收
        /// （AndroidJavaProxy 被回收后 Java 侧回调会直接崩）。
        /// </summary>
        private static AndroidJavaObject _reviewManager;
        private static AndroidJavaObject _activity;
        private static OnCompleteListenerProxy _listener;

        private static CAppReviewResult RequestAndroid(Action<CAppReviewResult> onCompleted)
        {
            try
            {
                if (!IsPlayCoreReviewAvailable())
                {
                    CLog.Warn(Tag,
                        "未检测到 Google Play Core 的 review 依赖，无法弹出应用内评价。" +
                        "框架本应在打包 Android 时自动往 unityLibrary/build.gradle 注入 " +
                        CAndroidGradleRequirements.PlayInAppReviewArtifact + "；" +
                        "若你用了自定义 Gradle 模板、或自行导出工程后再手动构建，请自行补上这一行。" +
                        "需要确定的评价入口请改用 CAppReview.OpenStorePage()。");
                    onCompleted?.Invoke(CAppReviewResult.Unavailable);
                    return CAppReviewResult.Unavailable;
                }

                DisposeAndroidState();

                _activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                    .GetStatic<AndroidJavaObject>("currentActivity");

                var factory = new AndroidJavaClass("com.google.android.play.core.review.ReviewManagerFactory");
                _reviewManager = factory.CallStatic<AndroidJavaObject>("create", _activity);
                factory.Dispose();

                if (_reviewManager == null)
                {
                    CLog.Warn(Tag, "ReviewManagerFactory.create 返回空。");
                    onCompleted?.Invoke(CAppReviewResult.Unavailable);
                    return CAppReviewResult.Unavailable;
                }

                AndroidJavaObject task = _reviewManager.Call<AndroidJavaObject>("requestReviewFlow");
                _listener = new OnCompleteListenerProxy(_reviewManager, _activity, onCompleted);
                task.Call<AndroidJavaObject>("addOnCompleteListener", _activity, _listener);
                task.Dispose();

                return CAppReviewResult.Requested;
            }
            catch (Exception e)
            {
                CLog.Error(Tag, "Android 评价请求异常: " + e.Message, e);
                DisposeAndroidState();
                onCompleted?.Invoke(CAppReviewResult.Failed);
                return CAppReviewResult.Failed;
            }
        }

        /// <summary>Play Core 的 review 类是否在位（结果缓存，只探一次）。</summary>
        private static bool? _playCoreAvailable;

        private static bool IsPlayCoreReviewAvailable()
        {
            if (_playCoreAvailable.HasValue) return _playCoreAvailable.Value;

            bool available;
            AndroidJavaClass probe = null;
            try
            {
                probe = new AndroidJavaClass("com.google.android.play.core.review.ReviewManagerFactory");
                available = probe != null;
            }
            catch (Exception)
            {
                available = false; // ClassNotFoundException
            }
            finally
            {
                probe?.Dispose();
            }

            _playCoreAvailable = available;
            return available;
        }

        private static void DisposeAndroidState()
        {
            _listener = null;
            try { _reviewManager?.Dispose(); } catch { }
            try { _activity?.Dispose(); } catch { }
            _reviewManager = null;
            _activity = null;
        }

        /// <summary>
        /// <c>com.google.android.gms.tasks.OnCompleteListener</c> 的 C# 代理。
        /// 方法名必须是小写 <c>onComplete</c>（Java 接口方法名），AndroidJavaProxy 按名匹配。
        /// </summary>
        private sealed class OnCompleteListenerProxy : AndroidJavaProxy
        {
            private readonly AndroidJavaObject _manager;
            private readonly AndroidJavaObject _activity;
            private readonly Action<CAppReviewResult> _callback;

            public OnCompleteListenerProxy(AndroidJavaObject manager, AndroidJavaObject activity,
                Action<CAppReviewResult> callback)
                : base("com.google.android.gms.tasks.OnCompleteListener")
            {
                _manager = manager;
                _activity = activity;
                _callback = callback;
            }

            // ReSharper disable once InconsistentNaming —— Java 接口方法名
            public void onComplete(AndroidJavaObject task)
            {
                CAppReviewResult result = CAppReviewResult.Failed;
                try
                {
                    if (task != null && task.Call<bool>("isSuccessful"))
                    {
                        AndroidJavaObject reviewInfo = task.Call<AndroidJavaObject>("getResult");
                        try
                        {
                            _manager.Call("launchReviewFlow", _activity, reviewInfo);
                            result = CAppReviewResult.Requested;
                        }
                        finally
                        {
                            reviewInfo?.Dispose();
                        }
                    }
                    else
                    {
                        CLog.Warn(Tag, "requestReviewFlow 未成功（可能被 Play 配额限制）。");
                    }
                }
                catch (Exception e)
                {
                    CLog.Error(Tag, "launchReviewFlow 异常: " + e.Message, e);
                    result = CAppReviewResult.Failed;
                }
                finally
                {
                    try { task?.Dispose(); } catch { }
                    DisposeAndroidState();
                    _callback?.Invoke(result);
                }
            }
        }
#endif

        /// <summary>Android 商店地址。<paramref name="useMarketScheme"/> 为 true 时用 market:// 唤起 Play 应用。</summary>
        internal static string BuildAndroidStoreUrl(string packageName, bool useMarketScheme)
        {
            string pkg = string.IsNullOrEmpty(packageName) ? "com.example.app" : packageName;
            return useMarketScheme
                ? "market://details?id=" + pkg
                : "https://play.google.com/store/apps/details?id=" + pkg;
        }

        /// <summary>iOS 商店地址。有数字 App ID 时直达「写评价」页。</summary>
        internal static string BuildIosStoreUrl(string appId)
        {
            if (string.IsNullOrEmpty(appId))
            {
                return "https://apps.apple.com/";
            }
            return "itms-apps://itunes.apple.com/app/id" + appId + "?action=write-review";
        }
    }
}
