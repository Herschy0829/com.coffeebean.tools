using System;
using UnityEngine;

namespace CoffeeBean
{
    /// <summary>
    /// JSON 工具（基于 JsonUtility）：带容错与默认值的读写封装。
    ///
    /// 注意：JsonUtility 不支持 Dictionary&lt;,&gt; / 接口 / 多态 / 数组根对象（部分支持），
    /// 复杂结构请使用 Newtonsoft.Json 等；本工具面向常规可序列化类。
    ///
    /// 用法：
    /// <code>
    /// string json = CJson.ToJson(myData, pretty: true);
    /// var data = CJson.FromJson&lt;SaveData&gt;(json);
    /// CJson.FromJsonOverwrite(json, existing);   // 复用对象，避免分配
    /// bool ok = CJson.IsValid(json);
    /// </code>
    /// </summary>
    public static class CJson
    {
        /// <summary>序列化为 JSON；null 返回空字符串。</summary>
        public static string ToJson(object obj, bool pretty = false)
        {
            if (obj == null) return string.Empty;
            return JsonUtility.ToJson(obj, pretty);
        }

        /// <summary>反序列化；json 为空或解析失败时返回默认值（并记录错误）。</summary>
        public static T FromJson<T>(string json, T defaultValue = default)
        {
            if (string.IsNullOrEmpty(json)) return defaultValue;
            try { return JsonUtility.FromJson<T>(json); }
            catch (Exception e)
            {
                Debug.LogError($"[CoffeeBean.Tools] JSON 反序列化失败（{typeof(T).Name}）：{e.Message}");
                return defaultValue;
            }
        }

        /// <summary>反序列化并覆盖到已有对象（避免分配新实例）；失败返回 false。</summary>
        public static bool FromJsonOverwrite<T>(string json, T target)
        {
            if (string.IsNullOrEmpty(json) || target == null) return false;
            try { JsonUtility.FromJsonOverwrite(json, target); return true; }
            catch (Exception e)
            {
                Debug.LogError($"[CoffeeBean.Tools] JSON 覆盖反序列化失败（{typeof(T).Name}）：{e.Message}");
                return false;
            }
        }

        /// <summary>判断字符串是否为合法 JSON 对象。</summary>
        public static bool IsValid(string json)
        {
            if (string.IsNullOrEmpty(json)) return false;
            try { JsonUtility.FromJson<JsonProbe>(json); return true; }
            catch { return false; }
        }

        [Serializable]
        private class JsonProbe { }
    }
}
