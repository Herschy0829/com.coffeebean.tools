using UnityEngine;

namespace CoffeeBean.Tools
{
    /// <summary>
    /// CoffeeBean 框架专属：MonoBehaviour 单例基类（主线程创建）。
    /// "C" 前缀表示 CoffeeBean 框架自有类型（后续框架类型命名沿用此约定）。
    ///
    /// 特性：
    /// - 懒创建：无场景实例时自动生成 GameObject（默认跨场景常驻 DontDestroyOnLoad）；
    /// - 场景已有实例则复用，多个实例时保留第一个并清理其余；
    /// - 退出保护：应用退出后 <see cref="Instance"/> 返回 null，避免销毁期重建；
    /// - 域重载重置：支持"Enter Play Mode Options &gt; Disable Domain Reload"（静态状态在进入播放时复位）。
    ///
    /// 用法：
    /// <code>
    /// public sealed class AudioManager : CSingletonMono&lt;AudioManager&gt; { }
    /// var audio = AudioManager.Instance;
    /// </code>
    /// </summary>
    /// <typeparam name="T">单例类型。</typeparam>
    public abstract class CSingletonMono<T> : MonoBehaviour where T : CSingletonMono<T>
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _quitting;

        /// <summary>
        /// 无场景实例时是否自动创建（默认 true）。
        /// 注意：静态 Instance 访问器无法读取实例属性，故用静态字段；
        /// 子类如需关闭，可在其静态构造函数中置 false。
        /// </summary>
        protected static bool AutoCreateOnDemand = true;

        /// <summary>是否跨场景常驻（默认 true）。</summary>
        protected virtual bool DontDestroy => true;

        /// <summary>全局唯一实例；应用退出中返回 null。</summary>
        public static T Instance
        {
            get
            {
                if (_quitting) return null;
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindExistingInstance();
                        if (_instance == null && AutoCreateOnDemand && Application.isPlaying)
                        {
                            var go = new GameObject(typeof(T).Name);
                            _instance = go.AddComponent<T>();
                        }
                    }
                    return _instance;
                }
            }
        }

        /// <summary>在场景中查找已有实例；存在多个时保留第一个并清理其余（仅主线程调用）。</summary>
        private static T FindExistingInstance()
        {
            T[] found = FindObjectsByType<T>(FindObjectsSortMode.None);
            if (found == null || found.Length == 0) return null;
            if (found.Length > 1)
            {
                Debug.LogError($"[CoffeeBean.Tools] 场景中存在多个 {typeof(T).Name} 实例，已保留第一个并清理其余。");
                for (int i = found.Length - 1; i > 0; i--) DestroyImmediate(found[i].gameObject);
            }
            return found[0];
        }

        /// <summary>注册自身；遇到重复实例（已有单例时）销毁当前对象。</summary>
        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = (T)this;
            if (DontDestroy) DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        protected virtual void OnApplicationQuit()
        {
            _quitting = true;
        }

        /// <summary>
        /// 复位静态状态：在"禁用域重载"的进入播放模式下，静态字段不会随脚本重载清零，需要手动复位。
        /// SubsystemRegistration 是播放循环最早的回调时机。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _quitting = false;
        }
    }
}
