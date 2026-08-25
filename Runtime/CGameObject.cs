using UnityEngine;

namespace CoffeeBean.Tools
{
    /// <summary>
    /// GameObject 便捷工具：高频且零碎的 Unity 对象操作。
    ///
    /// 用法：
    /// <code>
    /// var collider = CGameObject.GetOrAddComponent&lt;BoxCollider&gt;(go);
    /// var child = CGameObject.FindOrCreateChild(transform, "HpBar");
    /// CGameObject.DestroyChildren(poolRoot, immediate: true);
    /// </code>
    /// </summary>
    public static class CGameObject
    {
        /// <summary>获取组件；不存在则添加（经典 GetOrAddComponent 模式，免去判空+Add 两步）。</summary>
        public static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            if (go == null) throw new System.ArgumentNullException(nameof(go));
            T component = go.GetComponent<T>();
            if (component == null) component = go.AddComponent<T>();
            return component;
        }

        /// <summary>创建空物体（可选名称与父级；默认世界坐标对齐父级）。</summary>
        public static GameObject Create(string name = null, Transform parent = null)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "New GameObject" : name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>按名称查找子物体；不存在则创建（常用于运行时动态拼 UI / 挂点）。</summary>
        public static GameObject FindOrCreateChild(Transform parent, string name)
        {
            if (parent == null) throw new System.ArgumentNullException(nameof(parent));
            if (string.IsNullOrEmpty(name)) throw new System.ArgumentNullException(nameof(name));

            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            return Create(name, parent);
        }

        /// <summary>销毁全部子物体（倒序遍历，避免索引错乱）。immediate=true 用 DestroyImmediate（编辑器/测试用）。</summary>
        public static void DestroyChildren(Transform parent, bool immediate = false)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (immediate) Object.DestroyImmediate(child);
                else Object.Destroy(child);
            }
        }
    }
}
