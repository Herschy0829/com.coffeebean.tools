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
| `CRandom` | 随机工具（权重随机 / 洗牌 / 带种子控制） |
| `CEnum` | 枚举工具（安全解析 / 类型化取值 / 数量统计） |
| `CMath` | 数学工具（重映射 / 范围 / 进度 / 回绕 / 泛型钳制） |
| `CFile` | 文件工具（文本 / 字节 / JSON 磁盘读写） |

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
├── Random/        CRandom
├── Enum/          CEnum
├── Math/          CMath
├── IO/            CFile
├── Platform/      CAppReview（原生应用内评价）/ CDeviceLocale（原生地区与语言）/ Android Gradle 依赖
├── Loading/       CLoading + UILoading（通用 Loading 框）
└── Bridge/        与 Core 的可选集成（安装 Core 时编译）

Editor/
├── CAndroidGradleDependencyFallback   没装 build 模块时的 Gradle 依赖兜底
└── CThirdPartyIntegration             UniRx / UniTask 一键集成菜单
```

## 安装

```json
{
  "dependencies": {
    "com.coffeebean.tools": "https://github.com/Herschy0829/com.coffeebean.tools.git#v0.10.0"
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

## 可选的第三方依赖（一键集成）

UniRx / UniTask 是**可选**的（框架自身不依赖），但工程里几乎总会用到。手工往
`Packages/manifest.json` 里贴 git 地址很容易写错，也容易漏掉 `?path=` 子目录，所以做成菜单：

```
Tools/CoffeeBean/第三方依赖/集成 UniRx（Git）          ← 勾选 = 工程已集成
Tools/CoffeeBean/第三方依赖/集成 UniTask（Git）
Tools/CoffeeBean/第三方依赖/查看第三方依赖状态         ← 打印来源 / 修订 / 用途
```

- 勾选 = `Client.Add("<git 地址>#<锁定修订>")`，取消 = `Client.Remove(包名)` ——
  与在 Package Manager 里手动操作完全等价，只改 manifest 的 `dependencies`。
- **修订锁定**，不跟默认分支。UniRx 锁的是 commit（上游最后的 tag `7.1.0` 早于
  给该子目录补 `package.json` 的提交，用 tag 装机时 UPM 会报
  `Repository does not contain a package manifest`）；UniTask 锁 `2.5.11`。
- 工程已由**别的来源**提供同一个包（`file:` 本地路径 / registry 版本 / 其它 git 地址）时，
  点击会先弹确认框说明"这一项将被替换成什么"，不会静默替换。
- 变更期间两个勾选项置灰；一次成功的变更会重载域，重载后自动回读 manifest 校验并报告结果。

不改菜单也可以直接调 API：

```csharp
using CoffeeBean.EditorTools;

CThirdPartyIntegration.SetIntegrated(CThirdPartyCatalog.UniRx, true);   // 集成
CThirdPartyIntegration.SetIntegrated(CThirdPartyCatalog.UniRx, false);  // 移除
bool has = CThirdPartyIntegration.IsIntegrated("com.neuecc.unirx");
```

## 与 Core 集成

安装 Core 时自动注册 `MainThreadDispatcher` 进服务注册表（`Services.Get<MainThreadDispatcher>()`）；不装 Core 完全独立可用。

## License

[MIT](LICENSE.md)
