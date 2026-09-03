using NUnit.Framework;
using UnityEngine;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>
    /// CGameBuild 构建模式门面测试。
    /// 依赖编译环境：dev 工程为 Beta 模式（COFFEEBEAN_DEV_TOOLS/COFFEEBEAN_LOG 已定义）；
    /// Editor 下 HasLogging 恒 true（UNITY_EDITOR 分支）。
    /// </summary>
    public class CGameBuildTests
    {
        [Test]
        public void IsEditor_True_InEditMode()
        {
            Assert.True(CGameBuild.IsEditor);
        }

        [Test]
        public void HasLogging_True_InEditor_RegardlessOfMode()
        {
            // Editor 下无论 Beta/Release 模式都应有日志（UNITY_EDITOR 编译分支）
            Assert.True(CGameBuild.HasLogging);
        }

        [Test]
        public void HasDevTools_True_WhenBetaMacroDefined()
        {
            // dev 工程为 Beta 模式 → 应编译进测试工具
#if COFFEEBEAN_DEV_TOOLS
            Assert.True(CGameBuild.HasDevTools);
#else
            Assert.False(CGameBuild.HasDevTools);
#endif
        }

        [Test]
        public void DevOnly_RunsAction_WhenMacroDefined()
        {
            bool ran = false;
            CGameBuild.DevOnly(() => ran = true);
#if COFFEEBEAN_DEV_TOOLS
            Assert.True(ran);
#else
            Assert.False(ran);
#endif
        }
    }
}
