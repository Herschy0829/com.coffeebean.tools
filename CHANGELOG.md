# Changelog

## [0.2.0] - 2025-xx-xx

### Added
- `CSafeInvoke`：安全调用（异常隔离，SDK/事件回调不被打断；带参与返回值重载）
- `CReflection`：按名称解析类型（完整名 / 短名 / 简单名，结果缓存）
- `CPrefs`：PlayerPrefs 强类型封装（基础类型 / 枚举 / JSON，可带键前缀）
- `CGameObject`：GameObject 便捷工具（GetOrAddComponent / 建子物体 / 批量销毁）
- `CTime`：Unix 时间戳与时长格式化（倒计时 / 在线时长）
- 上述工具对应 EditMode 测试

## [0.1.0] - 2025-xx-xx

### Added
- `CSingleton<T>`：纯 C# 线程安全单例（Lazy 懒加载）
- `CSingletonMono<T>`：MonoBehaviour 单例（自动创建 / DontDestroyOnLoad / 多实例清理 / 退出保护 / 域重载复位）
- `MainThreadDispatcher`：主线程调度器（ConcurrentQueue 无锁入队、RunOnMainThread 立即执行优化、
  延迟执行、异常按条隔离、内部执行方法可测试）
- `ThreadUtil.RunAsync`：带并发上限的后台任务（SemaphoreSlim 限流，替代忙等）
- 与 Core 的可选集成桥（versionDefines：`COFFEEBEAN_CORE`）
- **ToolsDemo 示例**（Samples~/ToolsDemo）：单例、后台线程→主线程、延迟执行、线程池
- EditMode 测试：单例线程安全 / 队列顺序 / 延迟 / 异常隔离 / 后台线程投递

### Notes
- 命名约定：`C` 前缀 = CoffeeBean 框架自有类型
