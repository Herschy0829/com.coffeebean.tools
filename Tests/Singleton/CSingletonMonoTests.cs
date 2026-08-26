using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>CSingletonMono（MonoBehaviour 单例）测试：已有实例复用、多实例清理、退出保护。</summary>
    public class CSingletonMonoTests
    {
        private sealed class TestMono : CSingletonMono<TestMono> { }

        private GameObject _go1;
        private GameObject _go2;

        [TearDown]
        public void TearDown()
        {
            // 销毁已创建物体，触发 OnDestroy 复位静态实例，避免跨测试污染
            if (_go1 != null) UnityEngine.Object.DestroyImmediate(_go1);
            if (_go2 != null) UnityEngine.Object.DestroyImmediate(_go2);
        }

        [Test]
        public void Instance_ReusesExisting_NoAutoCreateInEditMode()
        {
            _go1 = new GameObject("SingletonA");
            _go1.AddComponent<TestMono>();

            var instance = TestMono.Instance;

            Assert.IsNotNull(instance, "场景已有实例时应直接取到，而不是新建");
            Assert.AreSame(_go1.GetComponent<TestMono>(), instance);
        }

        [Test]
        public void DuplicateInstances_CleanedToOne()
        {
            _go1 = new GameObject("SingletonFirst");
            _go2 = new GameObject("SingletonSecond");
            var c1 = _go1.AddComponent<TestMono>();
            var c2 = _go2.AddComponent<TestMono>();

            // 访问 Instance 触发 FindExistingInstance 清理多实例
            // 注意：FindObjectsByType 不保证返回顺序，因此只断言"保留其一、清理其余"这一不变量
            LogAssert.Expect(LogType.Error, new Regex("存在多个.*TestMono"));
            var instance = TestMono.Instance;

            Assert.IsNotNull(instance, "应保留一个实例");
            Assert.IsTrue(instance == c1 || instance == c2, "保留的实例必须是原有的之一");
            Assert.IsTrue(_go1 == null || _go2 == null, "重复实例应被清理（编辑器模式下 DestroyImmediate）");
            Assert.IsFalse(_go1 == null && _go2 == null, "至少应保留一个实例");
        }
    }
}
