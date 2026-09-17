using System;
using UnityEngine;

namespace CoffeeBean
{
    /// <summary>
    /// 通用 Loading 框门面。
    ///
    /// **解决什么**：加载场景 / 拉取资源 / 等网络时需要一个遮罩，但「谁负责显示」
    /// 很容易写错——并发操作各自 Show/Hide 会互相打断、异常路径忘了 Hide 会永久卡住、
    /// 切场景会把遮罩一起销毁。这里把这些统一收口。
    ///
    /// 用法：
    /// <code>
    /// CLoading.Show();                       // 显示
    /// CLoading.SetProgress(0.4f);            // 有绑定进度节点时才有效
    /// CLoading.Hide();                       // 隐藏
    ///
    /// // 推荐：用 using 作用域，异常路径也能自动收起
    /// using (CLoading.Scope("加载中..."))
    /// {
    ///     await LoadSomething();
    /// }
    /// </code>
    ///
    /// 设计要点：
    /// · **引用计数**：嵌套 / 并发的 Show 会累加，只有计数归零才真正隐藏，
    ///   避免"后一个操作先 Hide 把前一个的遮罩关掉"。
    /// · **自动隐藏是可控的兜底**：默认 45 秒（沿用初版意图），但每次 Show 会重置计时、
    ///   Hide 会取消计时。初版用 `await Task.Delay(45s)` 的游离定时器，
    ///   会把期间重新 Show 的遮罩一起关掉——这是本次修掉的核心缺陷。
    /// · **线程安全**：加载常发生在后台线程，非主线程调用会经
    ///   <see cref="MainThreadDispatcher"/> 投递到主线程执行。
    /// · **预制体来源可替换**：显式 <see cref="Prefab"/> → <see cref="PrefabProvider"/>
    ///   → 默认从 <c>Resources/CoffeeBean/LoadingCanvas</c> 加载（零配置即可用）。
    /// </summary>
    public static class CLoading
    {
        private const string Tag = "CoffeeBean.Loading";

        /// <summary>默认预制体路径（相对任意 Resources 目录）。</summary>
        public const string DefaultResourcePath = "CoffeeBean/LoadingCanvas";

        /// <summary>显式指定预制体；为空时依次看 <see cref="PrefabProvider"/> 与 Resources。</summary>
        public static GameObject Prefab;

        /// <summary>自定义预制体来源（例如走 Addressables / 自己管理实例）。返回 null 则回退 Resources。</summary>
        public static Func<GameObject> PrefabProvider;

        /// <summary>测试钩子：替换 Resources.Load 的预制体加载（null = 用真实的 Resources.Load）。</summary>
        internal static Func<string, GameObject> ResourceLoaderOverride;

        /// <summary>测试钩子：当前视图实例。</summary>
        internal static UILoading InstanceForTests => _instance;

        /// <summary>
        /// 调用 <see cref="Show"/> 未显式给超时时的兜底秒数（&lt;= 0 表示不自动隐藏）。
        /// 这只是防止调用方漏了 Hide 的保险，正常路径应当成对调用。
        /// </summary>
        public static float DefaultAutoHideSeconds = 45f;

        private static UILoading _instance;
        private static int _showCount;
        private static float _progress = -1f;
        private static string _text;
        private static bool _prefabMissingLogged;

        /// <summary>是否正在展示（引用计数 &gt; 0）。</summary>
        public static bool IsShowing => _showCount > 0;

        /// <summary>当前引用计数。</summary>
        public static int ShowCount => _showCount;

        /// <summary>当前进度（未设置过为 -1）。</summary>
        public static float Progress => _progress;

        /// <summary>当前文字（未设置过为 null）。</summary>
        public static string Text => _text;

        /// <summary>
        /// 启动期把主线程调度器准备好。
        ///
        /// 原因：<see cref="MainThreadDispatcher.IsMainThread"/> 在调度器初始化之前恒为 <c>false</c>
        /// （主线程 id 还没记下来），那样**主线程**调用也会被当成后台线程投递，白白延后一帧。
        /// <c>RuntimeInitializeOnLoadMethod</c> 保证在主线程、且在场景加载前执行。
        ///
        /// 注意：EditMode（编辑器工具 / 单元测试）不会触发本方法，
        /// 这些场景请在准备阶段自己调一次 <c>MainThreadDispatcher.EnsureReady()</c>。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureDispatcherReady()
        {
            MainThreadDispatcher.EnsureReady();
        }

        /// <summary>
        /// 显示 Loading（引用计数 +1）。必须与 <see cref="Hide"/> 成对。
        /// </summary>
        /// <param name="text">可选文字（需要预制体绑定了文字节点）。</param>
        /// <param name="autoHideSeconds">兜底自动隐藏秒数；&lt; 0 用 <see cref="DefaultAutoHideSeconds"/>，= 0 不自动隐藏。</param>
        public static void Show(string text = null, float autoHideSeconds = -1f)
        {
            float timeout = autoHideSeconds < 0f ? DefaultAutoHideSeconds : autoHideSeconds;
            if (text != null) _text = text;

            RunOnMain(() =>
            {
                if (EnsureInstance() == null) return;

                _showCount++;
                _instance.gameObject.SetActive(true);
                ApplyState();
                _instance.ShowInternal(timeout);
            });
        }

        /// <summary>隐藏 Loading（引用计数 -1；归零才真正收起）。</summary>
        public static void Hide()
        {
            RunOnMain(() =>
            {
                if (_showCount > 0) _showCount--;
                if (_showCount > 0) return;

                if (_instance != null) _instance.HideInternal();
            });
        }

        /// <summary>无视引用计数强制隐藏（异常路径 / 场景切换前的兜底）。</summary>
        public static void HideAll()
        {
            RunOnMain(() =>
            {
                _showCount = 0;
                if (_instance != null) _instance.HideInternal();
            });
        }

        /// <summary>设置进度 0..1（需要预制体绑定了进度节点，否则空转）。</summary>
        public static void SetProgress(float value)
        {
            float clamped = UILoading.Clamp01(value);
            _progress = clamped;
            RunOnMain(() => { if (_instance != null) _instance.SetProgressInternal(clamped); });
        }

        /// <summary>设置文字（需要预制体绑定了文字节点，否则空转）。</summary>
        public static void SetText(string text)
        {
            _text = text;
            RunOnMain(() => { if (_instance != null) _instance.SetTextInternal(text); });
        }

        /// <summary>
        /// 作用域：<c>using (CLoading.Scope()) { ... }</c> —— 离开作用域（含异常/return）自动 <see cref="Hide"/>。
        /// 这是最不易出错的用法。
        /// </summary>
        public static CLoadingScope Scope(string text = null, float autoHideSeconds = -1f)
        {
            Show(text, autoHideSeconds);
            return new CLoadingScope();
        }

        /// <summary>当前实例是否已创建（未创建不代表不可用，只说明还没 Show 过）。</summary>
        public static bool HasInstance => _instance != null;

        /// <summary>强制收起并清空状态（切换场景 / 退出登录 / 测试用）。</summary>
        public static void Reset()
        {
            RunOnMain(() =>
            {
                if (_instance != null) _instance.HideImmediate();
                _showCount = 0;
                _progress = -1f;
                _text = null;
            });
        }

        /// <summary>
        /// 销毁实例并清空状态（切换场景需要彻底释放时用；测试里做清理也用它）。
        /// 编辑器下用 <c>DestroyImmediate</c>，因为 EditMode 没有 Update 循环、延迟销毁不会发生。
        /// </summary>
        public static void DestroyInstance()
        {
            RunOnMain(() =>
            {
                if (_instance != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(_instance.gameObject);
                    else UnityEngine.Object.DestroyImmediate(_instance.gameObject);
                }
                _instance = null;
                _showCount = 0;
                _progress = -1f;
                _text = null;
                _prefabMissingLogged = false;
            });
        }

        // ========== 内部 ==========

        private static void ApplyState()
        {
            if (_progress >= 0f) _instance.SetProgressInternal(_progress);
            if (_text != null) _instance.SetTextInternal(_text);
        }

        /// <summary>取（必要时创建）视图实例。必须在主线程调用。</summary>
        private static UILoading EnsureInstance()
        {
            if (_instance != null) return _instance; // Unity 的 fake-null：被销毁时这里为 false，会重建

            GameObject prefab = ResolvePrefab();
            if (prefab == null)
            {
                if (!_prefabMissingLogged)
                {
                    _prefabMissingLogged = true;
                    CLog.Error(Tag,
                        "找不到 Loading 预制体，无法显示。" +
                        $"请确认包内有 Resources/{DefaultResourcePath}.prefab，" +
                        "或自行设置 CLoading.Prefab / CLoading.PrefabProvider。");
                }
                return null;
            }

            GameObject go = UnityEngine.Object.Instantiate(prefab);
            go.name = prefab.name; // 去掉 Instantiate 的 "(Clone)" 后缀，便于在层级里查找

            _instance = go.GetComponent<UILoading>();
            if (_instance == null)
            {
                // 预制体被换成了不带视图脚本的版本：补一个，保证 API 仍然可用
                CLog.Warn(Tag, "预制体上没有 UILoading 组件，已自动补上。");
                _instance = go.AddComponent<UILoading>();
            }

            // DontDestroyOnLoad 由 UILoading.Awake 处理；此处实例已处于 inactive，Awake 仍会执行
            return _instance;
        }

        private static GameObject ResolvePrefab()
        {
            if (Prefab != null) return Prefab;

            if (PrefabProvider != null)
            {
                try
                {
                    GameObject provided = PrefabProvider();
                    if (provided != null) return provided;
                }
                catch (Exception e)
                {
                    CLog.Error(Tag, "PrefabProvider 抛异常：" + e.Message, e);
                }
            }

            if (ResourceLoaderOverride != null) return ResourceLoaderOverride(DefaultResourcePath);

            return Resources.Load<GameObject>(DefaultResourcePath);
        }

        /// <summary>
        /// 保证操作在主线程执行。非主线程时投递到 <see cref="MainThreadDispatcher"/>。
        /// 调度器不可用（如未在主线程初始化过）时给出明确错误，而不是让 Unity 抛底层异常。
        /// </summary>
        private static void RunOnMain(Action action)
        {
            if (MainThreadDispatcher.IsMainThread)
            {
                action();
                return;
            }

            try
            {
                MainThreadDispatcher.Post(action);
            }
            catch (Exception e)
            {
                CLog.Error(Tag,
                    "从非主线程调用 Loading 接口，但主线程调度器不可用，" +
                    "本次调用已丢弃。请在启动阶段（任意主线程代码）调用一次 " +
                    "MainThreadDispatcher.EnsureReady()，或改为在主线程调用。", e);
            }
        }
    }

    /// <summary><see cref="CLoading.Scope"/> 返回的一次性作用域：Dispose 时自动隐藏。</summary>
    public struct CLoadingScope : IDisposable
    {
        private bool _disposed;

        /// <summary>是否已释放。</summary>
        public bool IsDisposed => _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CLoading.Hide();
        }
    }
}
