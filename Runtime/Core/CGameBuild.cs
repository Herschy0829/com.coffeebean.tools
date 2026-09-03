using System;
using UnityEngine;

namespace CoffeeBean
{
    /// <summary>
    /// 构建模式运行时门面（Beta / Release，见 docs/design-build-modes.md）。
    /// - Editor 下（无论 Beta/Release 模式）：日志恒有（UNITY_EDITOR 编译分支）
    /// - Beta 包：测试工具 + 日志进包（宏 COFFEEBEAN_DEV_TOOLS / COFFEEBEAN_LOG）
    /// - Release 包：两者剥离（宏未定义）
    /// 用法：敏感操作（作弊命令等）用 CDebug.Register（[Conditional] 更彻底）；
    /// 非敏感 UI 显隐用 <see cref="HasDevTools"/>；不适合 Conditional 的开发操作用 <see cref="DevOnly"/>。
    /// </summary>
    public static class CGameBuild
    {
        /// <summary>是否编辑器（开发态恒真）。</summary>
        public static bool IsEditor => Application.isEditor;

        /// <summary>是否 Development Build（Unity 判定，勾选 Development Build 构建时为真）。</summary>
        public static bool IsDevelopmentBuild => Debug.isDebugBuild;

        /// <summary>测试工具是否编译进包（Beta 模式 true；Release 模式 false）。</summary>
        public static bool HasDevTools =>
#if COFFEEBEAN_DEV_TOOLS
            true;
#else
            false;
#endif

        /// <summary>普通日志是否编译进包（Editor 恒 true；Beta 包 true；Release 包 false）。</summary>
        public static bool HasLogging =>
#if UNITY_EDITOR || COFFEEBEAN_LOG
            true;
#else
            false;
#endif

        /// <summary>
        /// 开发期专用操作：Beta/Editor（宏定义）执行；Release 下是 no-op。
        /// 适合不适合 [Conditional]（如非 void）的开发操作；敏感路径仍建议用 CDebug.Register（更彻底）。
        /// </summary>
        public static void DevOnly(Action action)
        {
#if COFFEEBEAN_DEV_TOOLS
            action?.Invoke();
#endif
        }
    }
}
