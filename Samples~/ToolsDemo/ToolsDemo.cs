using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace CoffeeBean.Tools.Samples
{
    /// <summary>
    /// 工具模块示例：
    /// - 本组件自身是 <see cref="CSingletonMono{ToolsDemo}"/>（演示单例用法）
    /// - 后台线程投递到主线程（MainThreadDispatcher.Post）
    /// - 已在主线程立即执行（RunOnMainThread）
    /// - 延迟执行（PostDelayed）
    /// - 线程池限流（ThreadUtil.RunAsync）
    ///
    /// 使用：场景中新建空物体挂上本组件，运行后点按钮观察日志。
    /// </summary>
    public sealed class ToolsDemo : CSingletonMono<ToolsDemo>
    {
        private readonly List<string> _log = new List<string>();
        private int _mainThreadCounter;
        private int _crossThreadCounter;
        private Vector2 _scroll;

        private void Start()
        {
            // CSingletonMono 已由 Instance 保证唯一；本组件被场景放置时由 Awake 注册
            Log($"ToolsDemo 单例就绪：{CSingletonMono<ToolsDemo>.Instance == this}");
            Log($"当前在主线程：{MainThreadDispatcher.IsMainThread}");
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 520, Screen.height - 20));

            GUILayout.Label("<b>CoffeeBean Tools Demo</b>", GUILayout.Height(22));
            GUILayout.Label($"主线程计数：{_mainThreadCounter}  后台投递计数：{_crossThreadCounter}");
            GUILayout.Label($"当前在主线程：{MainThreadDispatcher.IsMainThread}");

            GUILayout.Space(6);
            if (GUILayout.Button("后台线程投递 → 主线程", GUILayout.Height(32))) SpawnBackgroundPost();
            if (GUILayout.Button("RunOnMainThread（已主线程则立即）", GUILayout.Height(32))) RunOnMainThread();
            if (GUILayout.Button("PostDelayed 延迟 1 秒", GUILayout.Height(32))) PostDelayedDemo();
            if (GUILayout.Button("PostDelayedUnscaled 延迟 1 秒", GUILayout.Height(32))) PostDelayedUnscaledDemo();
            if (GUILayout.Button("ThreadUtil.RunAsync（后台计算）", GUILayout.Height(32))) RunAsyncDemo();

            GUILayout.Space(10);
            GUILayout.Label("日志:");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(240));
            int start = Mathf.Max(0, _log.Count - 20);
            for (int i = start; i < _log.Count; i++) GUILayout.Label(_log[i]);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        /// <summary>后台线程里投递动作到主线程（模拟网络/广告回调）。</summary>
        private void SpawnBackgroundPost()
        {
            var thread = new Thread(() =>
            {
                // 注意：此回调运行在后台线程，不能直接操作 Unity API
                Thread.Sleep(200); // 模拟耗时
                MainThreadDispatcher.Post(() =>
                {
                    _crossThreadCounter++;
                    Log($"后台线程 → 主线程投递成功（线程 id {Thread.CurrentThread.ManagedThreadId}）");
                });
            });
            thread.Start();
            Log("已启动后台线程（200ms 后投递到主线程）");
        }

        private void RunOnMainThread()
        {
            // 演示：RunOnMainThread 在主线程调用会立即执行，后台线程调用则排队
            MainThreadDispatcher.RunOnMainThread(() =>
            {
                _mainThreadCounter++;
                Log($"RunOnMainThread 执行（立即）");
            });
        }

        private void PostDelayedDemo()
        {
            MainThreadDispatcher.PostDelayed(() =>
            {
                _mainThreadCounter++;
                Log("PostDelayed 延迟 1 秒执行完成");
            }, 1f);
            Log("已投递延迟动作（1 秒后执行）");
        }

        private void PostDelayedUnscaledDemo()
        {
            // 不受 timeScale 影响：暂停菜单 / 慢动作时仍按真实时间执行
            MainThreadDispatcher.PostDelayedUnscaled(() =>
            {
                _mainThreadCounter++;
                Log("PostDelayedUnscaled 延迟 1 秒执行完成（不受 timeScale 影响）");
            }, 1f);
            Log("已投递延迟动作（1 秒后执行，不受 timeScale 影响）");
        }

        private void RunAsyncDemo()
        {
            ThreadUtil.RunAsync(() =>
            {
                int sum = 0;
                for (int i = 0; i < 1_000_000; i++) sum += i; // 模拟耗时计算
                MainThreadDispatcher.Post(() => Log($"后台计算完成：sum={sum}"));
            });
            Log("已启动后台计算任务（ThreadUtil.RunAsync）");
        }

        private void Log(string message)
        {
            Debug.Log("[ToolsDemo] " + message);
            _log.Add(message);
            while (_log.Count > 40) _log.RemoveAt(0);
        }
    }
}
