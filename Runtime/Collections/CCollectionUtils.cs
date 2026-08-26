using System;
using System.Collections.Generic;

namespace CoffeeBean
{
    /// <summary>
    /// 集合工具：判空、安全索引、随机取元素、交换、去重合并等高频操作。
    ///
    /// 用法：
    /// <code>
    /// if (CCollectionUtils.IsNullOrEmpty(list)) return;
    /// var first = CCollectionUtils.GetOrDefault(list, 0);
    /// var item = CCollectionUtils.GetRandom(list);
    /// </code>
    /// </summary>
    public static class CCollectionUtils
    {
        /// <summary>列表为 null 或空。</summary>
        public static bool IsNullOrEmpty<T>(IList<T> list) => list == null || list.Count == 0;

        /// <summary>集合为 null 或空。</summary>
        public static bool IsNullOrEmpty<T>(ICollection<T> collection) => collection == null || collection.Count == 0;

        /// <summary>安全索引：越界或 null 时返回默认值（不抛异常）。</summary>
        public static T GetOrDefault<T>(IList<T> list, int index, T defaultValue = default)
        {
            if (list == null || index < 0 || index >= list.Count) return defaultValue;
            return list[index];
        }

        /// <summary>随机取一个元素；列表为空返回默认值（使用 UnityEngine.Random）。</summary>
        public static T GetRandom<T>(IList<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        /// <summary>随机取一个元素（使用指定的 System.Random，便于测试/种子控制）。</summary>
        public static T GetRandom<T>(IList<T> list, System.Random random)
        {
            if (list == null || list.Count == 0) return default;
            if (random == null) throw new ArgumentNullException(nameof(random));
            return list[random.Next(0, list.Count)];
        }

        /// <summary>交换两个元素（索引越界时静默忽略）。</summary>
        public static void Swap<T>(IList<T> list, int a, int b)
        {
            if (list == null || a < 0 || b < 0 || a >= list.Count || b >= list.Count) return;
            (list[a], list[b]) = (list[b], list[a]);
        }

        /// <summary>追加不重复的元素（保持原有顺序；目标已含的元素跳过）。</summary>
        public static void AddRangeUnique<T>(ICollection<T> target, IEnumerable<T> items)
        {
            if (target == null || items == null) return;
            var seen = new HashSet<T>(target);
            foreach (T item in items)
            {
                if (seen.Add(item)) target.Add(item);
            }
        }
    }
}
