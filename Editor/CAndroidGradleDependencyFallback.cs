using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace CoffeeBean.EditorTools
{
    /// <summary>
    /// **没有集成 build 模块时**的 Android Gradle 依赖兜底注入。
    ///
    /// 背景：某些能力必须往 Gradle 依赖里加一行才可用（如应用内评价需要
    /// <c>com.google.android.play:review</c>），而 Unity 包没法直接改消费工程的 <c>build.gradle</c>。
    /// 框架把它拆成「谁需要」（tools 的 <see cref="CAndroidGradleRequirements"/>）与「谁来写」：
    ///
    /// · 装了 **build 模块** → 由它的 <c>CAndroidRequiredDeps</c> 写（那是首选路径：
    ///   有完整日志、dry-run、与它既有的 Gradle 注入实现一致）；
    /// · 没装 build 模块 → 由本回调写，用的就是 tools 自带的
    ///   <see cref="CAndroidGradleInjector"/>。
    ///
    /// 两条路径都做了幂等（按行判重），所以即使都执行也只会写入一次。
    /// 触发时机：Unity 生成完 Gradle 工程之后（导出工程与直接构建都会触发）。
    /// </summary>
    public sealed class CAndroidGradleDependencyFallback : IPostGenerateGradleAndroidProject
    {
        private const string Tag = "CoffeeBean.Tools";

        /// <summary>Gradle 依赖所在的文件（相对 Gradle 工程根）。</summary>
        public const string GradleRelativePath = "unityLibrary/build.gradle";

        /// <summary>比 build 模块的 2000 更早：若 build 在场，本回调已跳过，先后无影响。</summary>
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string exportPath)
        {
            try
            {
                if (IsHandledByBuildModule())
                {
                    Debug.Log($"[{Tag}] 检测到 build 模块，Android 必需 Gradle 依赖交给 CAndroidRequiredDeps 处理。");
                    return;
                }

                IReadOnlyList<CAndroidGradleRequirement> requirements = CAndroidGradleRequirements.ResolveForBuild();
                if (requirements.Count == 0) return;

                var artifacts = new List<string>(requirements.Count);
                var reasons = new List<string>(requirements.Count);
                foreach (CAndroidGradleRequirement r in requirements)
                {
                    artifacts.Add(r.Artifact);
                    reasons.Add(r.ToString());
                }

                string gradlePath = Path.Combine(exportPath, GradleRelativePath);
                if (CAndroidGradleInjector.TryEnsureDependencies(gradlePath, artifacts, out int inserted, out string error))
                {
                    Debug.Log(inserted > 0
                        ? $"[{Tag}] 已向 {GradleRelativePath} 注入 {inserted} 条必需 Gradle 依赖：{string.Join("；", reasons)}"
                        : $"[{Tag}] 必需 Gradle 依赖已存在（{string.Join("；", reasons)}），无需改动。");
                }
                else
                {
                    Debug.LogWarning($"[{Tag}] 注入必需 Gradle 依赖失败：{error}\n" +
                                     $"（目标文件：{gradlePath}；依赖：{string.Join("；", reasons)}）");
                }
            }
            catch (Exception e)
            {
                // 兜底路径绝不因为自己出错而中断构建
                Debug.LogError($"[{Tag}] Android 必需 Gradle 依赖兜底注入异常：{e}");
            }
        }

        /// <summary>
        /// build 模块是否在场（决定要不要由本回调兜底）。
        /// 探测 <c>CoffeeBean.CAndroidRequiredDeps</c> 这个类型而不是程序集名：
        /// · 不硬引用 build（build 依赖 tools，反向引用会成环）；
        /// · 顺带做到**版本感知** —— 装了旧版 build（还没有该类型）时本回调依然会兜底。
        /// </summary>
        internal static bool IsHandledByBuildModule()
        {
            const string typeName = "CoffeeBean.CAndroidRequiredDeps";
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetType(typeName, throwOnError: false) != null) return true;
                }
                catch (Exception)
                {
                    // 个别程序集反射会抛（如动态/损坏）→ 跳过
                }
            }
            return false;
        }
    }
}
