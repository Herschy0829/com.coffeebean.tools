# Changelog

## [0.5.0] - 2025-xx-xx

### Changed
- **统一命名空间**：全部类型迁移到 `CoffeeBean` 根命名空间（业务只需 `using CoffeeBean;` 即可使用所有模块主类型），模块内部辅助 / 测试 / 示例保留 `CoffeeBean.X` 子命名空间（父命名空间自动可见）
- **破坏性变更**：旧 `using CoffeeBean.X;` 需移除（类型已上移到根命名空间）

# Changelog

## [0.4.1] - 2025-xx-xx

### Added
- `MainThreadDispatcher.PostDelayedUnscaled`：基于 `Time.unscaledTime` 的延迟执行，不受 timeScale 影响
  （暂停菜单 / 慢动作等 timeScale 被修改场景下的倒计时、隐藏提示）

### Fixed
- `CSingletonMono` 多实例清理：播放模式改用 `Destroy`（帧末销毁，符合 Unity 生命周期约定），
  编辑器 / 测试环境仍用 `DestroyImmediate` 立即清理

### Changed
- 示例同步：ToolsDemo 增加 PostDelayedUnscaled 演示按钮

## [0.4.0] - 2025-xx-xx

### Added
- `CRandom`：随机工具（权重随机 / Fisher-Yates 洗牌 / 带种子控制）
- `CEnum`：枚举工具（安全解析 / 类型化取值缓存 / 数量统计）
- `CMath`：数学工具（值重映射 / 范围判断 / 进度 / 回绕 / 泛型钳制）
- `CFile`：文件工具（文本 / 字节 / JSON 磁盘读写，配合 CJson）
- 上述工具对应 EditMode 测试（按类别目录组织）

## [0.3.0] - 2025-xx-xx

### Added
- `CString`：字符串工具（数字提取 / 容错解析 / 十六进制转换 / 首字母大写）
- `CCollectionUtils`：集合工具（判空 / 安全索引 / 随机 / 交换 / 去重合并）
- `CJson`：JSON 工具（容错读写 / 覆盖反序列化 / 合法性检查）
- `CLog`：统一日志门面（[Tag] 格式 / 按级别开关）
- 上述工具对应 EditMode 测试

### Changed
- **目录结构按类别分组**：Runtime 与 Tests 拆分为 Core / Singleton / Threading / Text /
  Collections / Json / Log / Reflection / Persistence / Time / UnityObject 等子目录
  （单程序集不变，仅文件组织优化，便于查找）

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
