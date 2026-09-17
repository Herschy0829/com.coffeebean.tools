using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoffeeBean
{
    /// <summary>一条「框架级必需」的 Android Gradle 依赖。</summary>
    public sealed class CAndroidGradleRequirement
    {
        /// <summary>Maven 坐标：<c>group:artifact:version</c>。</summary>
        public readonly string Artifact;

        /// <summary>为什么需要它（写进构建日志，便于排查）。</summary>
        public readonly string Reason;

        public CAndroidGradleRequirement(string artifact, string reason)
        {
            Artifact = artifact;
            Reason = reason ?? string.Empty;
        }

        /// <summary>实际要写进 build.gradle 的那一行。</summary>
        public string ToGradleLine() => "implementation '" + Artifact + "'";

        public override string ToString() => Artifact + (string.IsNullOrEmpty(Reason) ? string.Empty : "  // " + Reason);
    }

    /// <summary>
    /// 框架级必需的 Android Gradle 依赖登记表。
    ///
    /// **要解决的问题**：有些能力必须往 Gradle 依赖里加一行才可用（比如应用内评价需要
    /// <c>com.google.android.play:review</c>），而 Unity 包**没法**直接改消费工程的
    /// <c>build.gradle</c>。此前只能靠文档提醒开发者手改，漏了就表现为「接口在真机上静默失效」。
    ///
    /// 这里把「某个模块需要什么依赖」与「谁来写」解耦：
    /// · 模块只登记需求（<see cref="Add"/> / <see cref="AddInAppReview"/>）；
    /// · **装了 build 模块**时由它写（<c>CAndroidRequiredDeps</c>，与用户配置无关、每次 Android 导出都执行）；
    /// · **没装 build 模块**时由 tools 自己的 Editor 回调兜底写
    ///   （<c>CAndroidGradleDependencyFallback</c>）。
    ///
    /// **"项目到底用没用" 的判定**（<see cref="ResolveForBuild"/>）：
    /// 1. 运行期痕迹：编辑器里跑过游戏且调用过 <see cref="CAppReview.Request"/>（持久化，跨域重载有效）；
    /// 2. 源码扫描：工程的 <c>Assets/**/*.cs</c> 里出现 <c>CAppReview</c> 标识 ——
    ///    这条是给 CI / 新克隆的机器兜底的（那里从没跑过游戏，只有代码）；
    /// 3. 显式登记：任何模块/业务代码直接 <see cref="Add"/>。
    /// 扫描是启发式的（注释里提到也会命中），但**误判的代价只是多一个未使用的依赖**，
    /// 比漏依赖导致真机功能静默失效小得多。可用 <see cref="EnableSourceScan"/> 关掉。
    /// </summary>
    public static class CAndroidGradleRequirements
    {
        /// <summary>Google Play In-App Review。版本取自 Google Maven 元数据（2.0.2 / 2024-10-18）。</summary>
        public const string PlayInAppReviewArtifact = "com.google.android.play:review:2.0.2";

        /// <summary>应用内评价的源码标识（用于扫描项目是否引用）。</summary>
        public const string AppReviewTypeName = "CAppReview";

        private const string AppReviewUsedKey = "AppReviewUsed";

        private static readonly CPrefs Prefs = new CPrefs("CoffeeBean.AndroidRequirements");
        private static readonly List<CAndroidGradleRequirement> Items = new List<CAndroidGradleRequirement>();
        private static readonly HashSet<string> ArtifactSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>是否启用「扫描工程源码」判定（默认开；关掉后只看运行期痕迹与显式登记）。</summary>
        public static bool EnableSourceScan = true;

        /// <summary>当前登记的全部需求（按登记顺序，已去重）。</summary>
        public static IReadOnlyList<CAndroidGradleRequirement> All => Items;

        /// <summary>登记一条必需依赖（按 artifact 去重，幂等）。空白串一律忽略。</summary>
        public static void Add(string artifact, string reason = null)
        {
            if (string.IsNullOrWhiteSpace(artifact)) return;
            string key = artifact.Trim();
            if (!ArtifactSet.Add(key)) return;
            Items.Add(new CAndroidGradleRequirement(key, reason));
        }

        /// <summary>登记应用内评价所需的 Play In-App Review 依赖（幂等）。</summary>
        public static void AddInAppReview(string reason = null)
            => Add(PlayInAppReviewArtifact, reason ?? "使用 CAppReview（原生应用内评价）");

        /// <summary>是否已登记某条依赖。</summary>
        public static bool Contains(string artifact)
            => !string.IsNullOrEmpty(artifact) && ArtifactSet.Contains(artifact.Trim());

        /// <summary>清空登记表（仅供测试与显式重置）。</summary>
        public static void Clear()
        {
            Items.Clear();
            ArtifactSet.Clear();
        }

        // ========== 运行期使用痕迹 ==========

        /// <summary>本次进程内是否调用过应用内评价接口。</summary>
        private static bool _appReviewUsedThisSession;

        /// <summary>是否用过应用内评价接口（本进程标记 **或** 持久化痕迹）。</summary>
        public static bool InAppReviewUsed => _appReviewUsedThisSession || Prefs.GetBool(AppReviewUsedKey);

        /// <summary>
        /// 记录"用过应用内评价"。由 <see cref="CAppReview.Request"/> 自动调用；
        /// 业务若把调用藏在别的程序集里、怕源码扫描漏掉，也可以显式调一次。
        /// 用 PlayerPrefs 持久化，因为退出 Play 模式会重载域、静态标记会丢。
        /// </summary>
        public static void MarkInAppReviewUsed()
        {
            _appReviewUsedThisSession = true;
            try
            {
                Prefs.SetBool(AppReviewUsedKey, true);
            }
            catch (Exception)
            {
                // PlayerPrefs 在极少数环境不可用（如某些批处理场景）→ 本进程标记仍然有效
            }
        }

        /// <summary>清除使用痕迹（测试 / 显式重置）。</summary>
        public static void ClearInAppReviewUsed()
        {
            _appReviewUsedThisSession = false;
            try
            {
                Prefs.Delete(AppReviewUsedKey);
            }
            catch (Exception)
            {
            }
        }

        // ========== 构建前解析 ==========

        /// <summary>
        /// 构建/导出前解析出真正要写入的依赖清单：
        /// 汇总显式登记 + 运行期痕迹 + （可选）源码扫描。幂等，可重复调用。
        /// </summary>
        public static IReadOnlyList<CAndroidGradleRequirement> ResolveForBuild()
        {
            bool used = InAppReviewUsed;

            if (!used && EnableSourceScan)
            {
                try
                {
                    used = SourceReferencesAppReview(Application.dataPath);
                }
                catch (Exception e)
                {
                    CLog.Warn("CoffeeBean.AndroidRequirements", "扫描工程源码判定应用内评价使用情况失败：" + e.Message);
                }
            }

            if (used)
            {
                AddInAppReview(InAppReviewUsed
                    ? "运行期调用过 CAppReview（或已显式登记）"
                    : "工程源码中出现 CAppReview");
            }

            return Items;
        }

        /// <summary>
        /// 扫描 <paramref name="assetsRoot"/> 下的 <c>*.cs</c> 是否出现 <see cref="AppReviewTypeName"/> 标识。
        /// 纯文件扫描、不依赖 Unity 资产库，便于单测。
        /// </summary>
        internal static bool SourceReferencesAppReview(string assetsRoot)
            => SourceContains(assetsRoot, AppReviewTypeName);

        /// <summary>扫描目录下所有 .cs 是否包含给定标识。</summary>
        internal static bool SourceContains(string root, string token, int maxFiles = 20000)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(token)) return false;
            if (!System.IO.Directory.Exists(root)) return false;

            int scanned = 0;
            foreach (string file in System.IO.Directory.EnumerateFiles(root, "*.cs", System.IO.SearchOption.AllDirectories))
            {
                if (++scanned > maxFiles) break;
                try
                {
                    if (System.IO.File.ReadAllText(file).IndexOf(token, StringComparison.Ordinal) >= 0) return true;
                }
                catch (Exception)
                {
                    // 单个文件读不了（权限/占用）不影响整体判定
                }
            }
            return false;
        }
    }
}
