#if COFFEEBEAN_CORE
// CoffeeBean 模块标识 + Core 生命周期集成。
// 本文件所在的 Bridge 程序集仅在安装 Core 时编译（asmdef defineConstraints），
// 因此工具模块本身不依赖 Core 也能独立工作。
using CoffeeBean;

[assembly: CoffeeBeanModule(
    "com.coffeebean.tools",
    "0.6.0",
    DisplayName = "Tools",
    Description = "Standalone utility module: singleton, MonoSingleton, main-thread dispatcher, thread pool.",
    Dependencies = new[] { "com.coffeebean.core" }
)]

namespace CoffeeBean
{
    /// <summary>Core 集成：把 MainThreadDispatcher 注册进服务注册表，其他模块可通过 context.Services.Get&lt;MainThreadDispatcher&gt;() 使用。</summary>
    public sealed class ToolsModule : ICoffeeBeanModule
    {
        public void OnLoad(CoffeeBeanContext context)
        {
            // 引导在主线程进行，可安全创建单例
            context.Services.Register(MainThreadDispatcher.Instance);
            context.Log("CoffeeBean.Tools integrated (MainThreadDispatcher registered).");
        }

        public void OnStart()
        {
        }

        public void OnShutdown()
        {
        }
    }
}
#endif
