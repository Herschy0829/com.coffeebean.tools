using System;
using System.IO;
using UnityEngine;

namespace CoffeeBean
{
    /// <summary>
    /// 文件工具：文本 / 字节 / JSON 的磁盘读写（显式路径，不绑定平台目录）。
    /// 游戏常用：把配置或存档写到 Application.persistentDataPath / Application.streamingAssetsPath。
    ///
    /// 约定：读取失败返回默认值并记录日志（容错）；写入失败抛出异常（数据不能静默丢失，由调用方处理）。
    /// </summary>
    public static class CFile
    {
        // ===== 文本 =====

        /// <summary>写入文本（自动创建目录）；写入失败抛异常。</summary>
        public static void WriteText(string path, string content)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            EnsureDirectory(path);
            File.WriteAllText(path, content ?? string.Empty);
        }

        /// <summary>读取文本；文件不存在或读取失败返回默认值。</summary>
        public static string ReadText(string path, string defaultValue = null)
        {
            if (string.IsNullOrEmpty(path)) return defaultValue;
            try { return File.Exists(path) ? File.ReadAllText(path) : defaultValue; }
            catch (Exception e)
            {
                Debug.LogError($"[CoffeeBean.Tools] 读取文本失败（{path}）：{e.Message}");
                return defaultValue;
            }
        }

        // ===== 字节 =====

        /// <summary>写入字节（自动创建目录）；写入失败抛异常。</summary>
        public static void WriteBytes(string path, byte[] bytes)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            EnsureDirectory(path);
            File.WriteAllBytes(path, bytes ?? Array.Empty<byte>());
        }

        /// <summary>读取字节；文件不存在或读取失败返回默认值。</summary>
        public static byte[] ReadBytes(string path, byte[] defaultValue = null)
        {
            if (string.IsNullOrEmpty(path)) return defaultValue;
            try { return File.Exists(path) ? File.ReadAllBytes(path) : defaultValue; }
            catch (Exception e)
            {
                Debug.LogError($"[CoffeeBean.Tools] 读取文件失败（{path}）：{e.Message}");
                return defaultValue;
            }
        }

        // ===== JSON（组合 CJson）=====

        /// <summary>写入 JSON 文件（自动创建目录）。</summary>
        public static void WriteJson<T>(string path, T data, bool pretty = false)
            => WriteText(path, CJson.ToJson(data, pretty));

        /// <summary>读取 JSON 文件；不存在或解析失败返回默认值。</summary>
        public static T ReadJson<T>(string path, T defaultValue = default)
            => CJson.FromJson(ReadText(path, null), defaultValue);

        // ===== 基础操作 =====

        public static bool Exists(string path) => !string.IsNullOrEmpty(path) && File.Exists(path);

        /// <summary>删除文件（不存在时静默；失败记录日志）。</summary>
        public static void Delete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception e) { Debug.LogError($"[CoffeeBean.Tools] 删除文件失败（{path}）：{e.Message}"); }
        }

        private static void EnsureDirectory(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }
    }
}
