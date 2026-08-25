# com.coffeebean.tools

CoffeeBean 工具模块（**独立模块**，不依赖任何 CoffeeBean 模块，其他模块可依赖它）。

- **`CSingleton<T>`**：纯 C# 线程安全单例（懒加载）
- **`CSingletonMono<T>`**：MonoBehaviour 单例（自动创建 / 跨场景常驻 / 多实例清理 / 退出与域重载保护）
- **`MainThreadDispatcher`**：主线程调度器——任意线程投递 Action 到主线程执行（网络/广告回调）
- **`ThreadUtil`**：带并发上限的后台任务（SemaphoreSlim 限流）

> 命名约定：`C` 前缀 = CoffeeBean 框架自有类型（后续框架类型命名沿用）。

## 安装

```json
{
  "dependencies": {
    "com.coffeebean.tools": "https://github.com/Herschy0829/com.coffeebean.tools.git#v0.1.0"
  }
}
```

## 用法

```csharp
using CoffeeBean.Tools;

// 纯 C# 单例
public sealed class GameConfig : CSingleton<GameConfig> { }

// MonoBehaviour 单例
public sealed class AudioManager : CSingletonMono<AudioManager> { }
var audio = AudioManager.Instance;

// 主线程调度（任意线程）
MainThreadDispatcher.Post(() => text.text = "主线程更新");   // 投递到主线程
MainThreadDispatcher.RunOnMainThread(() => ...);             // 已主线程则立即执行
MainThreadDispatcher.PostDelayed(() => ..., 1f);             // 延迟 1 秒

// 后台任务（并发受限）
ThreadUtil.RunAsync(() => { /* 耗时计算 */ });
```

## 与 Core 集成

安装 Core 时自动注册 `MainThreadDispatcher` 进服务注册表（`Services.Get<MainThreadDispatcher>()`）；不装 Core 完全独立可用。

## License

[MIT](LICENSE.md)
