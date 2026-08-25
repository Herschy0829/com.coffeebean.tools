# Tools Demo（工具模块示例）

演示工具模块的核心能力：

- **`CSingletonMono<T>`**：本组件自身就是单例（`CSingletonMono<ToolsDemo>`）
- **`MainThreadDispatcher`**：后台线程投递动作到主线程（模拟网络/广告回调）
- **`RunOnMainThread`**：已在主线程立即执行、后台线程自动排队
- **`PostDelayed`**：延迟执行
- **`ThreadUtil.RunAsync`**：带并发上限的后台任务（SemaphoreSlim 限流）

## 使用

1. Package Manager → `com.coffeebean.tools` → **Samples → Tools Demo → Import**
2. 场景中新建空物体，挂上 **`ToolsDemo`**
3. 运行（Play），点按钮观察日志：
   - 「后台线程投递 → 主线程」：后台线程 200ms 后把动作投递到主线程（Console 里看线程 id 变化）
   - 「RunOnMainThread」：立即执行
   - 「PostDelayed 延迟 1 秒」：1 秒后执行
   - 「ThreadUtil.RunAsync」：后台计算后回主线程显示结果

## 文件

| 文件 | 说明 |
|------|------|
| `ToolsDemo.cs` | 主演示组件（自身即 MonoSingleton）+ 各类工具用法演示 |
