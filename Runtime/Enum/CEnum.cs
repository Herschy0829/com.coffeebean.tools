using System;
using System.Collections.Generic;

namespace CoffeeBean.Tools
{
    /// <summary>
    /// 枚举工具：安全解析（忽略大小写）、类型化取值（带缓存）、数量统计。
    ///
    /// 用法：
    /// <code>
    /// Quality q = CEnum.Parse&lt;Quality&gt;("high");      // 忽略大小写；失败返回默认值
    /// Quality[] all = CEnum.GetValues&lt;Quality&gt;();     // 全部值（缓存，复用同一数组）
    /// int count = CEnum.Count&lt;Quality&gt;();
    /// </code>
    /// </summary>
    public static class CEnum
    {
        private static readonly Dictionary<Type, Array> _cache = new Dictionary<Type, Array>();
        private static readonly object _lock = new object();

        /// <summary>解析枚举名（忽略大小写）；名称非法或为空时返回默认值。</summary>
        public static T Parse<T>(string name, T defaultValue = default) where T : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(name)) return defaultValue;
            return Enum.TryParse(name, ignoreCase: true, out T result) ? result : defaultValue;
        }

        /// <summary>获取全部枚举值（结果缓存并复用同一数组，避免每次反射）。</summary>
        public static T[] GetValues<T>() where T : struct, Enum
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(typeof(T), out Array cached)) return (T[])cached;
                var values = (T[])Enum.GetValues(typeof(T));
                _cache[typeof(T)] = values;
                return values;
            }
        }

        /// <summary>枚举值的数量。</summary>
        public static int Count<T>() where T : struct, Enum => GetValues<T>().Length;
    }
}
