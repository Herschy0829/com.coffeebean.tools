using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CoffeeBean
{
    /// <summary>
    /// Loading 框视图（挂在 LoadingCanvas 预制体上）。
    ///
    /// **职责边界**：本类只管「怎么显示」，不管「什么时候显示」——
    /// 生命周期、引用计数、线程调度都在 <see cref="CLoading"/> 门面里。
    ///
    /// 相对初版修掉的问题：
    /// · 原来 `Show()` 里 `await Task.Delay(45s); Hide();` 是**游离的定时器**：
    ///   期间再次 `Show()` 会被上一次的定时器提前关掉。现在自动隐藏用协程，
    ///   每次 Show 先停掉上一轮的，Hide 也会停，不会互相误伤。
    /// · 原来没有 `DontDestroyOnLoad`，切场景时遮罩连同实例一起被销毁。
    /// · 原来用 `SetAsFirstSibling()` 把遮罩塞到**最底层**（会被别的 UI 盖住），现在置顶。
    /// · 原来 `Resources.Load` 的路径与实际资源位置不符、失败会 NRE。
    /// · 原来 `uiLoading` 字段从未被使用（死字段）。
    ///
    /// 可选绑定（留空即自动降级，不影响使用）：
    /// · <see cref="canvasGroup"/>：淡入淡出；留空则直接显隐
    /// · <see cref="spinner"/>：旋转指示器；留空则不旋转
    /// · <see cref="progressFill"/>：进度填充；留空则 SetProgress 空转
    /// · <see cref="progressText"/>：文字；留空则 SetText 空转
    ///   （框架不预置文字节点，避免强依赖某个字体资源；需要就自己在预制体里放一个再拖进来）
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UILoading : MonoBehaviour
    {
        [Header("可选绑定（留空自动降级）")]
        [Tooltip("整块面板的 CanvasGroup：用于淡入淡出。留空则直接显隐。")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("旋转指示器（Loading 节点）。留空则不旋转。")]
        [SerializeField] private RectTransform spinner;

        [Tooltip("进度填充节点（普通 Image 即可，靠锚点宽度表现进度）。留空则 SetProgress 空转。")]
        [SerializeField] private RectTransform progressFill;

        [Tooltip("进度文字。框架不预置（避免依赖字体资源），需要就自己放一个 Text 拖进来。")]
        [SerializeField] private Text progressText;

        [Header("表现参数")]
        [Tooltip("指示器角速度（度/秒，负值 = 顺时针）。")]
        [SerializeField] private float spinnerDegreesPerSecond = -180f;

        [Tooltip("淡入淡出时长（秒）；0 = 不做淡入淡出。")]
        [SerializeField] private float fadeDuration = 0.12f;

        [Tooltip("切换场景时保留本遮罩。")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        private Coroutine _autoHideCoroutine;
        private Coroutine _fadeCoroutine;
        private float _alpha = 1f;

        /// <summary>当前是否可见。</summary>
        public bool IsVisible => gameObject.activeSelf;

        /// <summary>是否绑定了进度节点（没有时 SetProgress 为空转）。</summary>
        public bool HasProgressFill => progressFill != null;

        /// <summary>是否绑定了文字节点。</summary>
        public bool HasProgressText => progressText != null;

        /// <summary>是否绑定了旋转指示器（测试用）。</summary>
        internal bool HasSpinner => spinner != null;

        /// <summary>是否绑定了用于淡入淡出的 CanvasGroup（测试用）。</summary>
        internal bool HasCanvasGroup => canvasGroup != null;

        /// <summary>是否开启跨场景保留（测试用）。</summary>
        internal bool DontDestroyEnabled => dontDestroyOnLoad;

        /// <summary>当前淡入淡出时长（测试用）。</summary>
        internal float FadeDuration => fadeDuration;

        /// <summary>遮罩排序值：足够大，压过常规 UI Canvas（默认 0）。</summary>
        internal const int OverlaySortingOrder = 30000;

        private void Awake()
        {
            // EditMode（编辑器工具 / 单测）下 DontDestroyOnLoad 会告警且无意义，只在播放模式生效
            if (dontDestroyOnLoad && Application.isPlaying) DontDestroyOnLoad(gameObject);

            // 初始不可见：预制体在编辑器里保持可见便于调样式，运行期由 Show/Hide 控制
            ApplyAlpha(0f);
            gameObject.SetActive(false);
        }

        /// <summary>显示（由 <see cref="CLoading"/> 调用）。autoHideSeconds &gt; 0 时自动隐藏。</summary>
        internal void ShowInternal(float autoHideSeconds)
        {
            StopAutoHide();

            gameObject.SetActive(true);
            BringToFront();

            if (CanFade)
            {
                StartFade(1f, fadeDuration);
            }
            else
            {
                ApplyAlpha(1f);
            }

            if (autoHideSeconds > 0f)
            {
                _autoHideCoroutine = StartCoroutine(AutoHideAfter(autoHideSeconds));
            }
        }

        /// <summary>隐藏（由 <see cref="CLoading"/> 在引用计数归零时调用）。</summary>
        internal void HideInternal()
        {
            StopAutoHide();

            if (!gameObject.activeSelf) return;

            if (CanFade)
            {
                StartFade(0f, fadeDuration, deactivateWhenDone: true);
            }
            else
            {
                ApplyAlpha(0f);
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 是否真的能做淡入淡出。淡入淡出依赖协程推进（每帧一步），
        /// 非播放模式（Editor 工具 / EditMode 测试）根本没有帧，必须退化成立即显隐，
        /// 否则 Hide 之后对象会永远停在"还可见"的状态。
        /// </summary>
        private bool CanFade => Application.isPlaying && fadeDuration > 0f && canvasGroup != null && gameObject.activeInHierarchy;

        /// <summary>立即隐藏（不做淡出）。</summary>
        internal void HideImmediate()
        {
            StopAutoHide();
            StopFade();
            ApplyAlpha(0f);
            gameObject.SetActive(false);
        }

        /// <summary>设置进度（0..1）。没有绑定进度节点时什么也不做。</summary>
        internal void SetProgressInternal(float value)
        {
            float clamped = Clamp01(value);

            if (progressFill != null)
            {
                // 用锚点宽度表现进度：普通 Image 无需 sprite / Filled 类型也能工作
                Vector2 max = progressFill.anchorMax;
                max.x = clamped;
                progressFill.anchorMax = max;
            }

            if (progressText != null)
            {
                progressText.text = Mathf.RoundToInt(clamped * 100f) + "%";
            }
        }

        /// <summary>设置文字。没有绑定文字节点时什么也不做。</summary>
        internal void SetTextInternal(string text)
        {
            if (progressText != null) progressText.text = text ?? string.Empty;
        }

        private void Update()
        {
            if (spinner != null && spinnerDegreesPerSecond != 0f)
            {
                // 用 unscaledDeltaTime：加载期间常常把 timeScale 设为 0，指示器仍要转
                spinner.Rotate(0f, 0f, spinnerDegreesPerSecond * Time.unscaledDeltaTime);
            }
        }

        private IEnumerator AutoHideAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            _autoHideCoroutine = null;
            // 只是兜底：正常路径应由调用方 Hide()。走到这里说明忘了配对。
            CLog.Warn("CoffeeBean.Loading", $"Loading 已展示 {seconds:0.#} 秒仍未 Hide，按超时自动隐藏。");
            CLoading.HideAll();
        }

        private void StopAutoHide()
        {
            if (_autoHideCoroutine == null) return;
            StopCoroutine(_autoHideCoroutine);
            _autoHideCoroutine = null;
        }

        private void StartFade(float target, float duration, bool deactivateWhenDone = false)
        {
            StopFade();
            if (gameObject.activeInHierarchy)
            {
                _fadeCoroutine = StartCoroutine(FadeTo(target, duration, deactivateWhenDone));
            }
            else
            {
                ApplyAlpha(target);
                if (deactivateWhenDone) gameObject.SetActive(false);
            }
        }

        private void StopFade()
        {
            if (_fadeCoroutine == null) return;
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        private IEnumerator FadeTo(float target, float duration, bool deactivateWhenDone)
        {
            float start = _alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime; // 同样不受 timeScale 影响
                ApplyAlpha(Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            ApplyAlpha(target);
            _fadeCoroutine = null;
            if (deactivateWhenDone) gameObject.SetActive(false);
        }

        private void ApplyAlpha(float alpha)
        {
            _alpha = alpha;
            if (canvasGroup != null) canvasGroup.alpha = alpha;
        }

        /// <summary>置顶：Canvas 调 sortingOrder，普通节点调到最后一个兄弟位。</summary>
        private void BringToFront()
        {
            transform.SetAsLastSibling();

            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null && !canvas.overrideSorting) canvas.sortingOrder = OverlaySortingOrder;
        }

        /// <summary>0..1 截断（internal 便于单测）。</summary>
        internal static float Clamp01(float value)
        {
            if (float.IsNaN(value)) return 0f;
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
