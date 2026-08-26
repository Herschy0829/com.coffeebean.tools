using System;
using System.Collections.Generic;
using System.Reflection;

namespace CoffeeBean
{
    /// <summary>
    /// 反射工具：按名称解析类型（用于字符串配置、序列化恢复、数据驱动等场景）。
    ///
    /// 支持三种名称格式（按优先级依次尝试）：
    /// 1. 完整名（含程序集，如 "UnityEngine.Vector3, UnityEngine.CoreModule"）
    /// 2. 短名（命名空间.类型，如 "CoffeeBean.Tools.CSafeInvoke"）
    /// 3. 简单名（仅类型名，如 "Vector3"）
    ///
    /// 结果带缓存，重复解析零成本；找不到返回 null。
    /// </summary>
    public static class CReflection
    {
        private static readonly Dictionary<string, Type> _cache = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static readonly object _lock = new object();

        /// <summary>按名称获取类型；找不到返回 null（结果会缓存，包括 null 以避免重复扫描）。</summary>
        public static Type GetType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;

            lock (_lock)
            {
                if (_cache.TryGetValue(typeName, out Type cached)) return cached;
            }

            Type result = Resolve(typeName);

            lock (_lock)
            {
                if (!_cache.ContainsKey(typeName)) _cache[typeName] = result;
            }
            return result;
        }

        private static Type Resolve(string typeName)
        {
            // 1) 完整名（含程序集）或系统内置短名
            Type type = Type.GetType(typeName);
            if (type != null) return type;

            // 2) 在所有已加载程序集中查找：先匹配 命名空间.类型 全名，再匹配简单名
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException) { continue; } // 忽略无法完整加载类型的程序集

                foreach (Type t in types)
                {
                    if (t.FullName == typeName) return t;
                }
                foreach (Type t in types)
                {
                    if (t.Name == typeName) return t;
                }
            }
            return null;
        }
    }
}
