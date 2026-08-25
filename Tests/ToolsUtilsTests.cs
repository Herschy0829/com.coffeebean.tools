using System;
using CoffeeBean.Tools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>新增工具（CSafeInvoke / CReflection / CPrefs / CGameObject / CTime）测试。</summary>
    public class ToolsUtilsTests
    {
        [SetUp]
        public void SetUp() => CPrefs.ClearAll();

        [TearDown]
        public void TearDown() => CPrefs.ClearAll();

        // ===== CSafeInvoke =====

        [Test]
        public void SafeInvoke_NormalAction_Runs()
        {
            int calls = 0;
            CSafeInvoke.Invoke(() => calls++);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void SafeInvoke_ThrowingAction_IsIsolatedAndLogged()
        {
            int after = 0;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("安全调用发生异常"));
            Assert.DoesNotThrow(() => CSafeInvoke.Invoke(() => throw new InvalidOperationException("模拟"), "测试"));
            CSafeInvoke.Invoke(() => after++);
            Assert.AreEqual(1, after, "异常隔离后，后续调用应正常执行");
        }

        [Test]
        public void SafeInvoke_Func_ReturnsDefaultOnException()
        {
            Assert.AreEqual(42, CSafeInvoke.Invoke(() => 42, "正常"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("安全调用发生异常"));
            Assert.AreEqual(0, CSafeInvoke.Invoke<int>(() => throw new InvalidOperationException("模拟"), "异常"));
        }

        [Test]
        public void SafeInvoke_NullAction_Ignored()
        {
            Assert.DoesNotThrow(() => CSafeInvoke.Invoke((Action)null, "null"));
            Assert.DoesNotThrow(() => CSafeInvoke.Invoke<int>((Func<int>)null, "null"));
        }

        // ===== CReflection =====

        [Test]
        public void Reflection_ByFullName_Resolved()
        {
            Type type = CReflection.GetType("CoffeeBean.Tools.CSafeInvoke");
            Assert.IsNotNull(type);
            Assert.AreEqual(typeof(CSafeInvoke), type);
        }

        [Test]
        public void Reflection_BySimpleName_Resolved()
        {
            Type type = CReflection.GetType("CSafeInvoke");
            Assert.IsNotNull(type);
            Assert.AreEqual(typeof(CSafeInvoke), type);
        }

        [Test]
        public void Reflection_ByAssemblyQualifiedName_Resolved()
        {
            Type type = CReflection.GetType(typeof(CSafeInvoke).AssemblyQualifiedName);
            Assert.IsNotNull(type);
            Assert.AreEqual(typeof(CSafeInvoke), type);
        }

        [Test]
        public void Reflection_UnknownName_ReturnsNull()
        {
            Assert.IsNull(CReflection.GetType("No.Such.Type.Exists"));
            Assert.IsNull(CReflection.GetType(null));
            Assert.IsNull(CReflection.GetType(""));
        }

        // ===== CPrefs =====

        private enum TestQuality { Low, Medium, High }

        [Serializable]
        private class SaveData { public int Level; public string Name; }

        [Test]
        public void Prefs_IntBoolString_Roundtrip()
        {
            var prefs = new CPrefs("Test");
            prefs.SetInt("Level", 7);
            prefs.SetBool("SoundOn", true);
            prefs.SetString("Name", "hero");

            Assert.AreEqual(7, prefs.GetInt("Level", 0));
            Assert.IsTrue(prefs.GetBool("SoundOn", false));
            Assert.AreEqual("hero", prefs.GetString("Name"));
            Assert.IsTrue(prefs.Has("Level"));
            prefs.Delete("Level");
            Assert.IsFalse(prefs.Has("Level"));
            Assert.AreEqual(0, prefs.GetInt("Level", 0));
        }

        [Test]
        public void Prefs_EnumAndJson_Roundtrip()
        {
            var prefs = new CPrefs("Test");
            prefs.SetEnum("Quality", TestQuality.High);
            prefs.SetJson("Save", new SaveData { Level = 5, Name = "hero" });

            Assert.AreEqual(TestQuality.High, prefs.GetEnum("Quality", TestQuality.Low));
            SaveData data = prefs.GetJson<SaveData>("Save");
            Assert.IsNotNull(data);
            Assert.AreEqual(5, data.Level);
            Assert.AreEqual("hero", data.Name);
        }

        [Test]
        public void Prefs_Prefix_IsolatesKeys()
        {
            var a = new CPrefs("A");
            var b = new CPrefs("B");
            a.SetInt("Key", 1);
            b.SetInt("Key", 2);
            Assert.AreEqual(1, a.GetInt("Key", 0));
            Assert.AreEqual(2, b.GetInt("Key", 0));
        }

        [Test]
        public void Prefs_MissingKey_ReturnsDefault()
        {
            var prefs = new CPrefs("Test");
            Assert.AreEqual(0, prefs.GetInt("Nope", 0));
            Assert.IsFalse(prefs.GetBool("Nope", false));
            Assert.AreEqual(TestQuality.Low, prefs.GetEnum("Nope", TestQuality.Low));
            Assert.IsNull(prefs.GetJson<SaveData>("Nope"));
        }

        // ===== CGameObject =====

        [Test]
        public void GameObject_GetOrAddComponent_AddsOrReuses()
        {
            var go = new GameObject("Test");
            try
            {
                BoxCollider added = CGameObject.GetOrAddComponent<BoxCollider>(go);
                Assert.IsNotNull(added, "缺失时应自动添加");
                Assert.AreSame(added, CGameObject.GetOrAddComponent<BoxCollider>(go), "已存在时应复用");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void GameObject_FindOrCreateChild_CreatesAndFinds()
        {
            var parent = new GameObject("Parent");
            try
            {
                GameObject child = CGameObject.FindOrCreateChild(parent.transform, "HpBar");
                Assert.IsNotNull(child);
                Assert.AreSame(child, CGameObject.FindOrCreateChild(parent.transform, "HpBar"), "已存在时应复用");
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
        }

        [Test]
        public void GameObject_DestroyChildren_EmptiesParent()
        {
            var parent = CGameObject.Create("Parent");
            CGameObject.Create("A", parent.transform);
            CGameObject.Create("B", parent.transform);
            Assert.AreEqual(2, parent.transform.childCount);

            CGameObject.DestroyChildren(parent.transform, immediate: true);
            Assert.AreEqual(0, parent.transform.childCount);

            UnityEngine.Object.DestroyImmediate(parent);
        }

        // ===== CTime =====

        [Test]
        public void Time_UnixNow_Positive()
        {
            Assert.Greater(CTime.UnixNow, 1_700_000_000, "UnixNow 应在合理范围内");
        }

        [Test]
        public void Time_FormatDuration()
        {
            Assert.AreEqual("00:00:00", CTime.FormatDuration(0));
            Assert.AreEqual("01:01:01", CTime.FormatDuration(3661));
            Assert.AreEqual("1天00:00:00", CTime.FormatDuration(86400));
            Assert.AreEqual("00:00:00", CTime.FormatDuration(-5), "负数按 0 处理");
        }

        [Test]
        public void Time_FormatClock()
        {
            Assert.AreEqual("00:00", CTime.FormatClock(0));
            Assert.AreEqual("01:30", CTime.FormatClock(90));
            Assert.AreEqual("01:01:01", CTime.FormatClock(3661), "超过 1 小时退回 HH:mm:ss");
        }
    }
}
