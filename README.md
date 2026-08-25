# com.coffeebean.tools

CoffeeBean 工具模块（**独立模块**，不依赖任何 CoffeeBean 模块，其他模块可依赖它）。

| 工具 | 用途 |
|------|------|
| `CSingleton<T>` | 纯 C# 线程安全单例（懒加载） |
| `CSingletonMono<T>` | MonoBehaviour 单例（自动创建 / 跨场景常驻 / 多实例清理 / 退出与域重载保护） |
| `MainThreadDispatcher` | 主线程调度器——任意线程投递 Action 到主线程执行（网络/广告回调） |
| `ThreadUtil` | 带并发上限的后台任务（SemaphoreSlim 限流） |
| `CSafeInvoke` | 安全调用：异常隔离，单个回调出错不中断调用链 |
| `CReflection` | 按名称解析类型（完整名 / 短名 / 简单名，带缓存） |
| `CPrefs` | PlayerPrefs 强类型封装（int/float/string/bool/枚举/JSON + 键前缀） |
| `CGameObject` | GameObject 便捷工具（GetOrAddComponent / 建子物体 / 批量销毁） |
| `CTime` | 时间戳与时长格式化（倒计时 / 在线时长显示） |
| `CString` | 字符串工具（数字提取 / 容错解析 / 十六进制 / 首字母大写） |
| `CCollectionUtils` | 集合工具（判空 / 安全索引 / 随机 / 交换 / 去重合并） |
| `CJson` | JSON 工具（容错读写 / 覆盖反序列化 / 合法性检查） |
| `CLog` | 统一日志门面（[Tag] 格式 / 按级别开关） |

> 命名约定：`C` 前缀 = CoffeeBean 框架自有类型（后续框架类型命名沿用）。

## 目录结构

```
Runtime/
├── Core/          CSafeInvoke（安全调用）
├── Singleton/     CSingleton / CSingletonMono
├── Threading/     MainThreadDispatcher / ThreadUtil
├── Text/          CString
├── Collections/   CCollectionUtils
├── Json/          CJson
├── Log/           CLog
├── Reflection/    CReflection
├── Persistence/   CPrefs
├── Time/          CTime
├── UnityObject/   CGameObject
└── Bridge/        与 Core 的可选集成（安装 Core 时编译）
```

## 安装

```json
{
  "dependencies": {
    "com.coffeebean.tools": "https://github.com/Herschy0829/com.coffeebean.tools.git#v0.3.0"
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

// 安全调用（SDK / 事件回调）
CSafeInvoke.Invoke(onAdCallback, "广告回调");

// 类型解析（字符串配置）
Type t = CReflection.GetType("MyGame.Config.ItemConfig");

// PlayerPrefs 强类型
var prefs = new CPrefs("MyGame");
prefs.SetInt("Level", 3);
prefs.SetJson("Save", mySaveData);

// GameObject 便捷
var collider = CGameObject.GetOrAddComponent<BoxCollider>(go);
CGameObject.DestroyChildren(poolRoot, immediate: true);

// 时间格式化
string countdown = CTime.FormatClock(90);   // "01:30"
```

## 与 Core 集成

安装 Core 时自动注册 `MainThreadDispatcher` 进服务注册表（`Services.Get<MainThreadDispatcher>()`）；不装 Core 完全独立可用。

## License

[MIT](LICENSE.md)
