using System;

namespace CoffeeBean.Tools
{
    /// <summary>
    /// CoffeeBean 框架专属：纯 C# 泛型单例（线程安全、懒加载）。
    /// "C" 前缀表示 CoffeeBean 框架自有类型（后续框架类型命名沿用此约定）。
    ///
    /// 用法：
    /// <code>
    /// public sealed class GameConfig : CSingleton&lt;GameConfig&gt; { ... }
    /// var cfg = GameConfig.Instance;
    /// </code>
    ///
    /// 注意：本类只适合普通 C# 对象。UnityEngine.Object 子类（MonoBehaviour）必须由主线程创建，
    /// 请改用 <see cref="CSingletonMono{T}"/>。
    /// </summary>
    /// <typeparam name="T">单例类型（必须有公共无参构造）。</typeparam>
    public abstract class CSingleton<T> where T : class, new()
    {
        /// <summary>使用 Lazy 实现线程安全的懒加载（ExecutionAndPublication：多线程只初始化一次）。</summary>
        private static readonly Lazy<T> _instance =
            new Lazy<T>(() => new T(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>全局唯一实例。</summary>
        public static T Instance => _instance.Value;
    }
}
