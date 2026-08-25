using System;
using UnityEngine;

namespace CoffeeBean.Tools
{
    /// <summary>
    /// 安全调用工具：执行委托并捕获异常（记录日志），避免单个回调异常中断调用链或整帧。
    /// 典型场景：第三方 SDK 回调、事件回调、批量遍历执行等"不希望被一个坏回调打断"的代码。
    ///
    /// 用法：
    /// <code>
    /// CSafeInvoke.Invoke(onAdCallback, "广告回调");
    /// CSafeInvoke.Invoke<int>(OnProgress, 50);      // 带参，避免闭包
    /// int v = CSafeInvoke.Invoke(() => Parse(), "解析"); // 异常时返回 default
    /// </code>
    /// </summary>
    public static class CSafeInvoke
    {
        /// <summary>执行动作；异常被捕获并记录（带上下文），不会向上抛出。null 动作直接忽略。</summary>
        public static void Invoke(Action action, string context = null)
        {
            if (action == null) return;
            try { action(); }
            catch (Exception e) { LogError(e, context); }
        }

        /// <summary>执行带参数动作（避免为每个调用分配闭包）。</summary>
        public static void Invoke<T>(Action<T> action, T arg, string context = null)
        {
            if (action == null) return;
            try { action(arg); }
            catch (Exception e) { LogError(e, context); }
        }

        /// <summary>执行并返回结果；异常时记录日志并返回 default(T)。</summary>
        public static T Invoke<T>(Func<T> func, string context = null)
        {
            if (func == null) return default;
            try { return func(); }
            catch (Exception e) { LogError(e, context); return default; }
        }

        private static void LogError(Exception e, string context)
        {
            if (string.IsNullOrEmpty(context))
                Debug.LogError($"[CoffeeBean.Tools] 安全调用发生异常：{e}");
            else
                Debug.LogError($"[CoffeeBean.Tools] 安全调用发生异常（{context}）：{e}");
        }
    }
}
