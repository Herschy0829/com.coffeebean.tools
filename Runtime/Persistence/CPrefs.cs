using System;
using UnityEngine;

namespace CoffeeBean
{
    /// <summary>
    /// PlayerPrefs 强类型封装：类型安全、可带键前缀避免命名冲突、支持枚举与 JSON 对象。
    ///
    /// 用法：
    /// <code>
    /// var prefs = new CPrefs("MyGame");          // 键自动加 "MyGame." 前缀
    /// prefs.SetInt("Level", 3);
    /// int level = prefs.GetInt("Level", 0);
    /// prefs.SetBool("SoundOn", true);
    /// prefs.SetEnum("Quality", Quality.High);
    /// prefs.SetJson("SaveData", mySaveData);     // JsonUtility 序列化
    /// </code>
    ///
    /// 注意：所有读写都在主线程（PlayerPrefs 的 Unity 实现非线程安全）。
    /// </summary>
    public sealed class CPrefs
    {
        /// <summary>无前缀实例（键为原始值）。</summary>
        public static readonly CPrefs Default = new CPrefs(null);

        private readonly string _prefix;

        /// <summary>创建带前缀的存储；键会自动加上 "prefix." 前缀，避免多模块键冲突。</summary>
        /// <param name="prefix">前缀（null/空 = 无前缀）。</param>
        public CPrefs(string prefix)
        {
            _prefix = string.IsNullOrEmpty(prefix) ? string.Empty : prefix + ".";
        }

        private string Key(string key) => _prefix + key;

        // ===== 基础类型 =====

        public bool Has(string key) => PlayerPrefs.HasKey(Key(key));

        public void Delete(string key) => PlayerPrefs.DeleteKey(Key(key));

        public int GetInt(string key, int defaultValue = 0) => PlayerPrefs.GetInt(Key(key), defaultValue);

        public void SetInt(string key, int value) => PlayerPrefs.SetInt(Key(key), value);

        public float GetFloat(string key, float defaultValue = 0f) => PlayerPrefs.GetFloat(Key(key), defaultValue);

        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(Key(key), value);

        public string GetString(string key, string defaultValue = null) => PlayerPrefs.GetString(Key(key), defaultValue);

        public void SetString(string key, string value) => PlayerPrefs.SetString(Key(key), value);

        public bool GetBool(string key, bool defaultValue = false) => PlayerPrefs.GetInt(Key(key), defaultValue ? 1 : 0) == 1;

        public void SetBool(string key, bool value) => PlayerPrefs.SetInt(Key(key), value ? 1 : 0);

        // ===== 枚举 =====

        /// <summary>读取枚举（按名称存储）；键不存在或值非法时返回默认值。</summary>
        public TEnum GetEnum<TEnum>(string key, TEnum defaultValue = default) where TEnum : struct, Enum
        {
            string name = GetString(key, null);
            if (name == null) return defaultValue;
            return Enum.TryParse(name, out TEnum result) ? result : defaultValue;
        }

        public void SetEnum<TEnum>(string key, TEnum value) where TEnum : struct, Enum
            => SetString(key, value.ToString());

        // ===== JSON 对象 =====

        /// <summary>读取 JSON 对象；键不存在或解析失败时返回默认值。</summary>
        public TJson GetJson<TJson>(string key, TJson defaultValue = default) where TJson : class
        {
            string json = GetString(key, null);
            if (string.IsNullOrEmpty(json)) return defaultValue;
            try { return JsonUtility.FromJson<TJson>(json); }
            catch (Exception e)
            {
                Debug.LogError($"[CoffeeBean.Tools] CPrefs 反序列化失败（{Key(key)}）：{e.Message}");
                return defaultValue;
            }
        }

        public void SetJson<TJson>(string key, TJson value)
        {
            if (value == null) { Delete(key); return; }
            SetString(key, JsonUtility.ToJson(value));
        }

        /// <summary>清空全部 PlayerPrefs（全局，谨慎使用；通常用于测试或"重置游戏"）。</summary>
        public static void ClearAll() => PlayerPrefs.DeleteAll();
    }
}
