using System;
using UnityEngine;

namespace CoffeeBean.Tools
{
    /// <summary>
    /// 数学工具：值重映射、范围判断、进度、回绕（角度/循环）、泛型钳制。
    ///
    /// 用法：
    /// <code>
    /// float p = CMath.Remap(50f, 0f, 100f, 0f, 1f);   // 0.5
    /// float angle = CMath.Wrap(-10f, 0f, 360f);        // 350
    /// int v = CMath.Clamp(15, 0, 10);                  // 10
    /// </code>
    /// </summary>
    public static class CMath
    {
        /// <summary>把值从 [fromMin, fromMax] 线性映射到 [toMin, toMax]（不钳制；输入区间为零时返回 toMin）。</summary>
        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            if (Mathf.Approximately(fromMin, fromMax)) return toMin;
            return toMin + (value - fromMin) / (fromMax - fromMin) * (toMax - toMin);
        }

        /// <summary>值是否在 [min, max] 内（含边界）。</summary>
        public static bool IsBetween(float value, float min, float max)
            => value >= min && value <= max;

        /// <summary>值在 [min, max] 内的进度（0~1，不钳制）。</summary>
        public static float Percent(float value, float min, float max)
            => Remap(value, min, max, 0f, 1f);

        /// <summary>回绕：把值卷进 [min, max] 区间（如角度 -10 → 350、370 → 10）。</summary>
        public static float Wrap(float value, float min, float max)
        {
            float range = max - min;
            if (range <= 0f) return min;
            float offset = (value - min) % range;
            if (offset < 0f) offset += range;
            return min + offset;
        }

        /// <summary>泛型钳制：值小于 min 返回 min，大于 max 返回 max。</summary>
        public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0) return min;
            if (value.CompareTo(max) > 0) return max;
            return value;
        }
    }
}
