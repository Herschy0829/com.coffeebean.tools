using System.Runtime.CompilerServices;

// 允许测试程序集访问 internal 成员（如 MainThreadDispatcher.ExecutePendingActions）
[assembly: InternalsVisibleTo("CoffeeBean.Tools.Tests")]
