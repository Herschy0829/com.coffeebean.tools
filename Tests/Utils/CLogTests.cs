using System;
using CoffeeBean.Tools;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>CLog 统一日志测试。</summary>
    public class CLogTests
    {
        [TearDown]
        public void TearDown()
        {
            // 还原开关，避免影响其他测试
            CLog.InfoEnabled = true;
            CLog.WarningEnabled = true;
            CLog.ErrorEnabled = true;
        }

        [Test]
        public void Error_FormatsWithTag()
        {
            LogAssert.Expect(LogType.Error, "[IAP] 核销失败");
            CLog.Error("IAP", "核销失败");
        }

        [Test]
        public void Error_WithException_IncludesStack()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("\\[IAP\\] 异常详情"));
            CLog.Error("IAP", "异常详情", new InvalidOperationException("模拟"));
        }

        [Test]
        public void DisabledLevel_SuppressesOutput()
        {
            CLog.ErrorEnabled = false;
            LogAssert.NoUnexpectedReceived();
            CLog.Error("IAP", "不应输出");
        }

        [Test]
        public void NullTag_UsesPlainMessage()
        {
            LogAssert.Expect(LogType.Log, "plain message");
            CLog.Info(null, "plain message");
        }
    }
}
