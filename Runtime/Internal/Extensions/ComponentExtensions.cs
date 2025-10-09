using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace Coffee.UIParticleInternal
{
    /// <summary>
    /// Extension methods for Component class.
    /// </summary>
    internal static class ComponentExtensions
    {
        /// <summary>
        /// 在GameObject的层次结构中获取指定类型的子组件，限制搜索深度。
        /// </summary>
        /// <typeparam name="T">要查找的组件类型</typeparam>
        /// <param name="self">当前组件</param>
        /// <param name="depth">搜索深度，0表示只搜索自身，1表示搜索自身和直接子对象，以此类推</param>
        /// <returns>找到的所有指定类型组件的数组</returns>
        public static T[] GetComponentsInChildren<T>(this Component self, int depth)
            where T : Component
        {
            // 从对象池租用一个列表来存储结果，避免频繁创建新列表导致的GC开销
            var results = InternalListPool<T>.Rent();

            // 调用内部实现方法来填充结果列表
            self.GetComponentsInChildren_Internal(results, depth);

            // 将列表转换为数组，这是返回给调用者的最终结果
            var array = results.ToArray();

            // 将临时列表归还对象池，以便重用
            InternalListPool<T>.Return(ref results);

            // 返回找到的组件数组
            return array;
        }

        /// <summary>
        /// 在GameObject的层次结构中获取指定类型的子组件，并将结果存储在指定的列表中。
        /// </summary>
        /// <typeparam name="T">要查找的组件类型</typeparam>
        /// <param name="self">当前组件（扩展方法的目标组件）</param>
        /// <param name="results">用于存储结果的列表</param>
        /// <param name="depth">搜索深度，0表示只搜索自身，1表示搜索自身和直接子对象，以此类推</param>
        public static void GetComponentsInChildren<T>(this Component self, List<T> results, int depth)
            where T : Component
        {
            // 清空结果列表，确保不会有之前的残留数据
            results.Clear();
            // 调用内部递归实现方法来填充结果列表
            self.GetComponentsInChildren_Internal(results, depth);
        }

        /// <summary>
        /// 获取子组件的内部递归实现方法
        /// </summary>
        /// <typeparam name="T">要查找的组件类型</typeparam>
        /// <param name="self">当前组件</param>
        /// <param name="results">用于存储结果的列表</param>
        /// <param name="depth">剩余的搜索深度</param>
        private static void GetComponentsInChildren_Internal<T>(this Component self, List<T> results, int depth)
            where T : Component
        {
            // 参数有效性检查：如果组件为空、结果列表为空或深度小于0，则直接返回
            if (!self || results == null || depth < 0) return;

            // 获取当前组件的变换组件
            var tr = self.transform;
            // 尝试获取当前变换组件上的指定类型组件
            if (tr.TryGetComponent<T>(out var t))
            {
                // 如果找到匹配的组件，则添加到结果列表中
                results.Add(t);
            }

            // 检查是否还有剩余搜索深度（是否继续递归搜索子对象）
            if (depth - 1 < 0) return;
            // 获取子对象数量
            var childCount = tr.childCount;
            // 遍历所有子对象
            for (var i = 0; i < childCount; i++)
            {
                // 递归搜索每个子对象，深度减1
                tr.GetChild(i).GetComponentsInChildren_Internal(results, depth - 1);
            }
        }

        /// <summary>
        /// 获取GameObject上指定类型的组件，如果不存在则添加该组件
        /// </summary>
        /// <typeparam name="T">要获取或添加的组件类型</typeparam>
        /// <param name="self">当前组件（扩展方法的目标组件）</param>
        /// <returns>找到的现有组件或新添加的组件，如果self为null则返回null</returns>
        public static T GetOrAddComponent<T>(this Component self) where T : Component
        {
            // 参数有效性检查：如果组件为空，则直接返回null
            if (!self) return null;
            
            // 尝试获取组件，如果存在则返回现有组件，否则添加新组件并返回
            return self.TryGetComponent<T>(out var component)
                ? component  // 组件已存在，返回找到的组件
                : self.gameObject.AddComponent<T>();  // 组件不存在，添加新组件并返回
        }

        /// <summary>
        /// 获取游戏对象层次结构中特定类型的根组件
        /// </summary>
        /// <typeparam name="T">要查找的组件类型</typeparam>
        /// <param name="self">当前组件</param>
        /// <returns>在层次结构中找到的最后一个（最接近根）的指定类型组件，如果没有找到则返回null</returns>
        public static T GetRootComponent<T>(this Component self) where T : Component
        {
            // 初始化结果组件为null
            T component = null;
            // 获取当前组件的变换组件
            var transform = self.transform;
            
            // 遍历从当前变换直到根变换的整个层次结构
            while (transform)
            {
                // 尝试在当前变换上获取指定类型的组件
                if (transform.TryGetComponent<T>(out var c))
                {
                    // 如果找到组件，更新结果（注意：会被上层找到的组件覆盖）
                    component = c;
                }

                // 移动到父变换继续查找
                transform = transform.parent;
            }

            // 返回在整个层次结构中找到的最接近根的组件
            return component;
        }

        /// <summary>
        /// 在游戏对象的父级层次结构中获取特定类型的组件
        /// </summary>
        /// <typeparam name="T">要查找的组件类型</typeparam>
        /// <param name="self">当前组件</param>
        /// <param name="includeSelf">是否包含自身</param>
        /// <param name="stopAfter">停止查找的父级变换，如果达到此变换则停止查找</param>
        /// <param name="valid">用于验证找到的组件是否有效的谓词函数</param>
        /// <returns>找到的符合条件的组件，如果没有找到则返回null</returns>
        public static T GetComponentInParent<T>(this Component self, bool includeSelf, Transform stopAfter,
            Predicate<T> valid)
            where T : Component
        {
            // 根据includeSelf参数决定从当前变换还是父变换开始查找
            var tr = includeSelf ? self.transform : self.transform.parent;
            
            // 遍历层次结构中的每个父变换
            while (tr)
            {
                // 尝试获取组件，并使用valid谓词函数验证是否符合条件
                if (tr.TryGetComponent<T>(out var c) && valid(c)) return c;
                
                // 如果当前变换是指定的停止点，则返回null
                if (tr == stopAfter) return null;
                
                // 移动到下一个父变换
                tr = tr.parent;
            }

            // 遍历完整个层次结构后仍然没有找到符合条件的组件，返回null
            return null;
        }

        /// <summary>
        /// 向游戏对象的子对象添加特定类型的组件
        /// </summary>
        /// <typeparam name="T">要添加的组件类型</typeparam>
        /// <param name="self">当前组件</param>
        /// <param name="hideFlags">添加的组件的隐藏标志</param>
        /// <param name="includeSelf">是否也在当前对象上添加组件</param>
        public static void AddComponentOnChildren<T>(this Component self, HideFlags hideFlags, bool includeSelf)
            where T : Component
        {
            // 参数检查，如果当前组件为null则直接返回
            if (self == null) return;

            // 开始性能分析采样（针对当前对象）
            Profiler.BeginSample("(COF)[ComponentExt] AddComponentOnChildren > Self");
            // 如果需要包含自身，并且自身没有该组件，则添加组件
            if (includeSelf && !self.TryGetComponent<T>(out _))
            {
                var c = self.gameObject.AddComponent<T>();
                c.hideFlags = hideFlags;  // 设置组件的隐藏标志
            }

            // 结束当前对象的性能分析采样
            Profiler.EndSample();

            // 开始子对象的性能分析采样
            Profiler.BeginSample("(COF)[ComponentExt] AddComponentOnChildren > Child");
            // 获取子对象数量
            var childCount = self.transform.childCount;
            // 遍历所有子对象
            for (var i = 0; i < childCount; i++)
            {
                var child = self.transform.GetChild(i);
                // 如果子对象已经有该组件，则跳过
                if (child.TryGetComponent<T>(out _)) continue;

                // 为没有该组件的子对象添加组件
                var c = child.gameObject.AddComponent<T>();
                c.hideFlags = hideFlags;  // 设置组件的隐藏标志
            }

            // 结束子对象的性能分析采样
            Profiler.EndSample();
        }

        /// <summary>
        /// 向GameObject的子对象添加指定类型的组件
        /// </summary>
        /// <typeparam name="T">要添加的组件类型</typeparam>
        /// <param name="self">扩展的组件实例</param>
        /// <param name="includeSelf">是否同时在自身GameObject上添加组件</param>
        public static void AddComponentOnChildren<T>(this Component self, bool includeSelf)
            where T : Component
        {
            // 参数检查，防止空引用异常
            if (self == null) return;

            // 开始性能分析采样 - 处理自身GameObject
            Profiler.BeginSample("(COF)[ComponentExt] AddComponentOnChildren > Self");
            // 如果需要包含自身，并且自身GameObject上还没有该组件，则添加
            if (includeSelf && !self.TryGetComponent<T>(out _))
            {
                self.gameObject.AddComponent<T>();
            }

            // 结束自身GameObject的性能分析采样
            Profiler.EndSample();

            // 开始性能分析采样 - 处理子对象
            Profiler.BeginSample("(COF)[ComponentExt] AddComponentOnChildren > Child");
            // 获取子对象数量
            var childCount = self.transform.childCount;
            // 遍历所有直接子对象
            for (var i = 0; i < childCount; i++)
            {
                var child = self.transform.GetChild(i);
                // 如果子对象已经有该组件，则跳过
                if (child.TryGetComponent<T>(out _)) continue;

                // 为子对象添加组件
                child.gameObject.AddComponent<T>();
            }

            // 结束子对象的性能分析采样
            Profiler.EndSample();
        }

#if !UNITY_2021_2_OR_NEWER && !UNITY_2020_3_45 && !UNITY_2020_3_46 && !UNITY_2020_3_47 && !UNITY_2020_3_48
        /// <summary>
        /// 在组件的父级对象中查找指定类型的组件
        /// 该方法是为了兼容旧版本Unity，在新版本中Unity已提供原生支持
        /// </summary>
        /// <typeparam name="T">要查找的组件类型</typeparam>
        /// <param name="self">扩展的组件实例</param>
        /// <param name="includeInactive">是否包含非激活状态的GameObject</param>
        /// <returns>找到的组件，如果没有找到则返回null</returns>
        public static T GetComponentInParent<T>(this Component self, bool includeInactive) where T : Component
        {
            // 参数检查，防止空引用异常
            if (!self) return null;
            
            // 如果不需要包含非激活对象，直接使用Unity原生方法
            if (!includeInactive) return self.GetComponentInParent<T>();

            // 从当前对象的Transform开始向上查找
            var current = self.transform;
            // 当还有父级时继续查找
            while (current)
            {
                // 尝试获取当前Transform上的目标组件
                if (current.TryGetComponent<T>(out var c)) return c;
                // 移动到父级Transform
                current = current.parent;
            }

            // 未找到指定类型的组件
            return null;
        }
#endif

#if UNITY_EDITOR
        /// <summary>
        /// 验证对象是否可以转换为指定的MonoBehaviour组件类型
        /// </summary>
        /// <typeparam name="T">目标组件类型</typeparam>
        /// <param name="context">要检查的对象</param>
        /// <returns>如果对象非空且与目标类型不同，则返回true，表示可以转换</returns>
        internal static bool CanConvertTo<T>(this Object context) where T : MonoBehaviour
        {
            // 检查对象是否非空，并且其类型与目标类型不同
            return context && context.GetType() != typeof(T);
        }

        /// <summary>
        /// 将对象转换为指定的MonoBehaviour组件类型
        /// 此方法通过修改组件的m_Script属性实现类型转换
        /// </summary>
        /// <typeparam name="T">目标组件类型</typeparam>
        /// <param name="context">要转换的对象</param>
        internal static void ConvertTo<T>(this Object context) where T : MonoBehaviour
        {
            // 将对象转换为MonoBehaviour，如果失败则直接返回
            var target = context as MonoBehaviour;
            if (target == null) return;

            // 创建序列化对象以访问私有属性
            var so = new SerializedObject(target);
            so.Update();

            // 保存当前组件的启用状态，并暂时禁用它
            var oldEnable = target.enabled;
            target.enabled = false;

            // 查找指定组件类型的MonoScript
            foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                // 跳过不匹配的脚本类型
                if (script.GetClass() != typeof(T))
                {
                    continue;
                }

                // 设置'm_Script'属性以执行转换
                so.FindProperty("m_Script").objectReferenceValue = script;
                so.ApplyModifiedProperties();
                break;
            }

            // 恢复组件的原始启用状态
            if (so.targetObject is MonoBehaviour mb)
            {
                mb.enabled = oldEnable;
            }
        }
#endif
    }
}
