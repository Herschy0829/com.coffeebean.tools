using System;
using System.Collections.Generic;

namespace CoffeeBean.Tools
{
    /// <summary>
    /// 随机工具：权重随机、洗牌、带种子控制（可测试 / 可复现）。
    ///
    /// 用法：
    /// <code>
    /// var item = CRandom.WeightedPick(items, weights, random);  // 按权重抽取
    /// CRandom.Shuffle(deck, random);                            // 洗牌（原地）
    /// bool hit = CRandom.NextBool(random, 0.3f);                // 30% 概率
    /// </code>
    /// </summary>
    public static class CRandom
    {
        /// <summary>
        /// 按权重随机取一个元素（权重非负；总权重为 0 或列表空时返回默认值）。
        /// 数量不一致 / 权重为负会抛出异常（属于程序错误，尽早暴露）。
        /// </summary>
        public static T WeightedPick<T>(IList<T> items, IList<float> weights, Random random)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (items.Count == 0) return default;
            if (items.Count != weights.Count)
                throw new ArgumentException("items 与 weights 数量必须一致", nameof(weights));

            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] < 0f)
                    throw new ArgumentException($"权重不能为负（index={i}）", nameof(weights));
                total += weights[i];
            }
            if (total <= 0f) return default;

            float roll = (float)(random.NextDouble() * total);
            float acc = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                acc += weights[i];
                if (roll < acc) return items[i];
            }
            return items[items.Count - 1]; // 浮点累加误差兜底
        }

        /// <summary>洗牌（Fisher-Yates，原地修改列表；保留全部元素仅打乱顺序）。</summary>
        public static void Shuffle<T>(IList<T> list, Random random)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (random == null) throw new ArgumentNullException(nameof(random));
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>按概率返回 true（probability 为 0~1 的命中率）。</summary>
        public static bool NextBool(Random random, float probability = 0.5f)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (probability <= 0f) return false;
            if (probability >= 1f) return true;
            return random.NextDouble() < probability;
        }
    }
}
