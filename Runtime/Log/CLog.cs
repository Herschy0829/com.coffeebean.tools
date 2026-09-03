using System;
using UnityEngine;

namespace CoffeeBean
{
    /// <summary>
    /// 统一日志门面：统一输出格式（[Tag] message）、按级别开关、支持全局静音与检索。
    ///
    /// 用法：
    /// <code>
    /// CLog.Info("Game", "玩家进入关卡 1");
    /// CLog.Warn("Network", "重试 2/3");
    /// CLog.Error("IAP", "核销失败", exception);
    ///
    /// CLog.ErrorEnabled = false;   // 全局关闭错误日志（生产环境兜底）
    /// </code>
    ///
    /// 约定：tag 用系统/模块名（如 "IAP"、"Network"），便于按标签过滤日志。
    ///
    /// 构建模式（Beta/Release，见 docs/design-build-modes.md）：
    /// - Info/Warn 方法体按 `#if UNITY_EDITOR || COFFEEBEAN_LOG` 编译：
    ///   Editor 下（无论 Beta/Release 模式）恒有日志；Beta 包（定义 COFFEEBEAN_LOG）有日志；
    ///   Release 包两者皆无 → 方法体为空（日志剥离）。
    /// - Error 无条件保留（正式包错误仍可见，可经打点上送）。
    /// </summary>
    public static class CLog
    {
        /// <summary>Info 级别开关。</summary>
        public static bool InfoEnabled = true;

        /// <summary>Warning 级别开关。</summary>
        public static bool WarningEnabled = true;

        /// <summary>Error 级别开关。</summary>
        public static bool ErrorEnabled = true;

        public static void Info(string tag, string message)
        {
#if UNITY_EDITOR || COFFEEBEAN_LOG
            if (InfoEnabled) Debug.Log(Format(tag, message));
#endif
        }

        public static void Warn(string tag, string message)
        {
#if UNITY_EDITOR || COFFEEBEAN_LOG
            if (WarningEnabled) Debug.LogWarning(Format(tag, message));
#endif
        }

        public static void Error(string tag, string message)
        {
            if (ErrorEnabled) Debug.LogError(Format(tag, message));
        }

        /// <summary>带异常堆栈的错误日志。</summary>
        public static void Error(string tag, string message, Exception exception)
        {
            if (!ErrorEnabled) return;
            Debug.LogError(Format(tag, message) + "\n" + exception);
        }

        private static string Format(string tag, string message)
            => string.IsNullOrEmpty(tag) ? message : $"[{tag}] {message}";
    }
}
