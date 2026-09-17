using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>
    /// 通用 Loading 框测试。
    ///
    /// 这些用例有两层价值：
    /// 1. **行为**：引用计数、进度/文字、作用域自动收起、缺资源时不崩；
    /// 2. **资产完整性**：直接加载包里那份 <c>Resources/CoffeeBean/LoadingCanvas.prefab</c>，
    ///    断言脚本与 spinner / CanvasGroup 绑定正确 —— 相当于给预制体 YAML 上了一道回归锁，
    ///    手工改坏会立刻红。
    ///
    /// 注意：EditMode 不会触发 <c>RuntimeInitializeOnLoadMethod</c>，
    /// 因此 SetUp 里显式调一次 <c>MainThreadDispatcher.EnsureReady()</c>，
    /// 否则 <c>IsMainThread</c> 恒为 false、调用会被投递而不是立即执行。
    /// </summary>
    public class CLoadingTests
    {
        private Func<GameObject> _savedProvider;
        private GameObject _savedPrefab;
        private float _savedAutoHide;
        private Func<string, GameObject> _savedLoader;
        private GameObject _dispatcherGo;

        [SetUp]
        public void SetUp()
        {
            // EditMode 下 CSingletonMono 不会自建（只在 Application.isPlaying 时创建），
            // 因此手动建一个调度器并 EnsureReady —— 否则 IsMainThread 恒为 false，
            // CLoading 的调用会被投递而不是立即执行（EditMode 没有 Update 去消费队列）。
            _dispatcherGo = new GameObject("TestDispatcher");
            _dispatcherGo.AddComponent<MainThreadDispatcher>();
            MainThreadDispatcher.EnsureReady();

            _savedProvider = CLoading.PrefabProvider;
            _savedPrefab = CLoading.Prefab;
            _savedAutoHide = CLoading.DefaultAutoHideSeconds;
            _savedLoader = CLoading.ResourceLoaderOverride;

            CLoading.PrefabProvider = null;
            CLoading.Prefab = null;
            CLoading.ResourceLoaderOverride = null;
            CLoading.DefaultAutoHideSeconds = 45f;
            CLoading.DestroyInstance();
        }

        [TearDown]
        public void TearDown()
        {
            CLoading.DestroyInstance();

            CLoading.PrefabProvider = _savedProvider;
            CLoading.Prefab = _savedPrefab;
            CLoading.ResourceLoaderOverride = _savedLoader;
            CLoading.DefaultAutoHideSeconds = _savedAutoHide;

            if (_dispatcherGo != null) UnityEngine.Object.DestroyImmediate(_dispatcherGo);
        }

        // ========== 资产完整性（预制体 YAML 的回归锁） ==========

        [Test]
        public void DefaultPrefab_ExistsInResources()
        {
            var prefab = Resources.Load<GameObject>(CLoading.DefaultResourcePath);
            Assert.IsNotNull(prefab, $"包里应带 Resources/{CLoading.DefaultResourcePath}.prefab");
        }

        [Test]
        public void DefaultPrefab_HasViewComponent()
        {
            var prefab = Resources.Load<GameObject>(CLoading.DefaultResourcePath);
            Assert.IsNotNull(prefab);

            var view = prefab.GetComponent<UILoading>();
            Assert.IsNotNull(view, "预制体上应有 UILoading 组件（脚本引用没丢）");
        }

        [Test]
        public void DefaultPrefab_HasSpinnerAndCanvasGroupWired()
        {
            var prefab = Resources.Load<GameObject>(CLoading.DefaultResourcePath);
            var view = prefab.GetComponent<UILoading>();
            Assert.IsNotNull(view);

            Assert.IsTrue(view.HasSpinner, "spinner 应已绑定（否则指示器不会转）");
            Assert.IsTrue(view.HasCanvasGroup, "canvasGroup 应已绑定（否则没有淡入淡出）");
            Assert.IsTrue(view.DontDestroyEnabled, "默认应跨场景保留");
        }

        [Test]
        public void DefaultPrefab_OverlaySortsAboveNormalUi()
        {
            var prefab = Resources.Load<GameObject>(CLoading.DefaultResourcePath);
            var canvas = prefab.GetComponent<Canvas>();
            Assert.IsNotNull(canvas);
            Assert.Greater(canvas.sortingOrder, 0, "遮罩排序值要压过常规 UI Canvas（默认 0）");
        }

        // ========== 引用计数 ==========

        [Test]
        public void Show_CreatesInstanceAndMarksShowing()
        {
            CLoading.Show();

            Assert.IsTrue(CLoading.HasInstance);
            Assert.IsTrue(CLoading.IsShowing);
            Assert.AreEqual(1, CLoading.ShowCount);
            Assert.IsNotNull(CLoading.InstanceForTests);
        }

        [Test]
        public void Show_Twice_ThenHideOnce_StillShowing()
        {
            CLoading.Show();
            CLoading.Show();
            Assert.AreEqual(2, CLoading.ShowCount);

            CLoading.Hide();

            Assert.AreEqual(1, CLoading.ShowCount);
            Assert.IsTrue(CLoading.IsShowing, "还有一处没释放，遮罩不能被关掉");
        }

        [Test]
        public void Hide_WhenCountReachesZero_HidesInstance()
        {
            CLoading.Show();
            CLoading.Show();

            CLoading.Hide();
            CLoading.Hide();

            Assert.AreEqual(0, CLoading.ShowCount);
            Assert.IsFalse(CLoading.IsShowing);
            Assert.IsFalse(CLoading.InstanceForTests.IsVisible);
        }

        [Test]
        public void Hide_WithoutShow_IsHarmless()
        {
            CLoading.Hide();
            Assert.AreEqual(0, CLoading.ShowCount);
            Assert.IsFalse(CLoading.IsShowing);
        }

        [Test]
        public void HideAll_IgnoresRefCount()
        {
            CLoading.Show();
            CLoading.Show();
            CLoading.Show();

            CLoading.HideAll();

            Assert.AreEqual(0, CLoading.ShowCount);
            Assert.IsFalse(CLoading.IsShowing);
        }

        [Test]
        public void Instance_IsReusedAcrossShows()
        {
            CLoading.Show();
            UILoading first = CLoading.InstanceForTests;
            CLoading.Hide();
            CLoading.Show();

            Assert.AreSame(first, CLoading.InstanceForTests, "复用同一实例，避免反复 Instantiate");
        }

        [Test]
        public void DestroyedInstance_IsRecreated()
        {
            CLoading.Show();
            UILoading first = CLoading.InstanceForTests;

            CLoading.DestroyInstance();
            CLoading.Show();

            Assert.IsNotNull(CLoading.InstanceForTests);
            Assert.AreNotSame(first, CLoading.InstanceForTests, "实例被销毁后应能重建");
        }

        // ========== 进度与文字 ==========

        [Test]
        public void SetProgress_ClampsAndRemembers()
        {
            CLoading.SetProgress(0.42f);
            Assert.AreEqual(0.42f, CLoading.Progress, 0.0001f);

            CLoading.SetProgress(-5f);
            Assert.AreEqual(0f, CLoading.Progress);

            CLoading.SetProgress(9f);
            Assert.AreEqual(1f, CLoading.Progress);
        }

        [Test]
        public void SetText_Remembers()
        {
            CLoading.SetText("加载中...");
            Assert.AreEqual("加载中...", CLoading.Text);
        }

        [Test]
        public void Show_AppliesStoredProgressAndText()
        {
            CLoading.SetProgress(0.5f);
            CLoading.SetText("半路");

            CLoading.Show();

            Assert.IsNotNull(CLoading.InstanceForTests);
            Assert.AreEqual(0.5f, CLoading.Progress, 0.0001f);
            Assert.AreEqual("半路", CLoading.Text);
        }

        [Test]
        public void SetProgress_WithoutBoundFill_DoesNotThrow()
        {
            CLoading.Show();
            Assert.IsFalse(CLoading.InstanceForTests.HasProgressFill, "默认预制体没有进度节点");
            Assert.DoesNotThrow(() => CLoading.SetProgress(0.7f));
            Assert.DoesNotThrow(() => CLoading.SetText("x"));
        }

        [Test]
        public void Clamp01_HandlesEdgeCases()
        {
            Assert.AreEqual(0f, UILoading.Clamp01(float.NaN));
            Assert.AreEqual(0f, UILoading.Clamp01(-1f));
            Assert.AreEqual(0f, UILoading.Clamp01(0f));
            Assert.AreEqual(0.5f, UILoading.Clamp01(0.5f));
            Assert.AreEqual(1f, UILoading.Clamp01(1f));
            Assert.AreEqual(1f, UILoading.Clamp01(2f));
            Assert.AreEqual(0f, UILoading.Clamp01(float.NegativeInfinity));
            Assert.AreEqual(1f, UILoading.Clamp01(float.PositiveInfinity));
        }

        // ========== 作用域 ==========

        [Test]
        public void Scope_DisposeHides()
        {
            using (CLoading.Scope("加载中"))
            {
                Assert.IsTrue(CLoading.IsShowing);
                Assert.AreEqual("加载中", CLoading.Text);
            }

            Assert.IsFalse(CLoading.IsShowing);
            Assert.AreEqual(0, CLoading.ShowCount);
        }

        [Test]
        public void Scope_DisposeIsIdempotent()
        {
            var scope = CLoading.Scope();
            scope.Dispose();
            scope.Dispose();

            Assert.AreEqual(0, CLoading.ShowCount, "重复 Dispose 不应把计数减成负数");
            Assert.IsTrue(scope.IsDisposed);
        }

        [Test]
        public void Scope_Nested_InnerDisposeKeepsOuterVisible()
        {
            using (CLoading.Scope("外层"))
            {
                using (CLoading.Scope("内层"))
                {
                    Assert.AreEqual(2, CLoading.ShowCount);
                }
                Assert.IsTrue(CLoading.IsShowing, "内层结束后外层仍应可见");
            }
            Assert.IsFalse(CLoading.IsShowing);
        }

        // ========== 预制体来源 ==========

        [Test]
        public void PrefabProvider_TakesPrecedenceOverResources()
        {
            var custom = new GameObject("CustomLoading");
            try
            {
                CLoading.PrefabProvider = () => custom;

                CLoading.Show();

                Assert.IsNotNull(CLoading.InstanceForTests);
                StringAssert.StartsWith("CustomLoading", CLoading.InstanceForTests.name);
            }
            finally
            {
                CLoading.DestroyInstance();
                UnityEngine.Object.DestroyImmediate(custom);
            }
        }

        [Test]
        public void ExplicitPrefab_TakesPrecedenceOverProvider()
        {
            var explicitPrefab = new GameObject("ExplicitLoading");
            var providerCalled = false;
            try
            {
                CLoading.Prefab = explicitPrefab;
                CLoading.PrefabProvider = () => { providerCalled = true; return null; };

                CLoading.Show();

                Assert.IsFalse(providerCalled, "显式 Prefab 优先，不应再问 Provider");
                StringAssert.StartsWith("ExplicitLoading", CLoading.InstanceForTests.name);
            }
            finally
            {
                CLoading.DestroyInstance();
                UnityEngine.Object.DestroyImmediate(explicitPrefab);
            }
        }

        [Test]
        public void ProviderThrowing_FallsBackToResources()
        {
            CLoading.PrefabProvider = () => throw new InvalidOperationException("boom");
            LogAssert.Expect(LogType.Error, new Regex("PrefabProvider 抛异常"));

            Assert.DoesNotThrow(() => CLoading.Show());
            Assert.IsNotNull(CLoading.InstanceForTests, "Provider 抛异常应回退 Resources，而不是整体失败");
        }

        // ========== 缺资源时不崩 ==========

        [Test]
        public void MissingPrefab_LogsButDoesNotThrow()
        {
            CLoading.ResourceLoaderOverride = _ => null;
            LogAssert.Expect(LogType.Error, new Regex("找不到 Loading 预制体"));

            Assert.DoesNotThrow(() => CLoading.Show());
            Assert.IsFalse(CLoading.HasInstance);
            Assert.IsFalse(CLoading.IsShowing, "拿不到预制体就不该把状态算成「在显示」");
        }

        [Test]
        public void MissingPrefab_ThenPrefabBecomesAvailable_Works()
        {
            CLoading.ResourceLoaderOverride = _ => null;
            LogAssert.Expect(LogType.Error, new Regex("找不到 Loading 预制体"));
            CLoading.Show();
            Assert.IsFalse(CLoading.IsShowing);

            CLoading.ResourceLoaderOverride = null; // 恢复真实加载
            CLoading.Show();

            Assert.IsTrue(CLoading.IsShowing);
            Assert.IsNotNull(CLoading.InstanceForTests);
        }

        // ========== 排序与自动隐藏参数 ==========

        [Test]
        public void OverlaySortingOrder_IsHighEnoughToCoverNormalUi()
        {
            Assert.Greater(UILoading.OverlaySortingOrder, 1000,
                "要能压过常见的高 sortingOrder UI（如弹窗层）");
        }

        [Test]
        public void DefaultAutoHideSeconds_IsPositiveFailsafe()
        {
            Assert.Greater(CLoading.DefaultAutoHideSeconds, 0f);
        }

        [Test]
        public void DefaultResourcePath_MatchesShippedLocation()
        {
            Assert.AreEqual("CoffeeBean/LoadingCanvas", CLoading.DefaultResourcePath);
        }
    }
}
