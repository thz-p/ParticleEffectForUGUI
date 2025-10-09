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
        /// Get a component of a specific type in the parent hierarchy of a GameObject.
        /// </summary>
        public static T GetComponentInParent<T>(this Component self, bool includeSelf, Transform stopAfter,
            Predicate<T> valid)
            where T : Component
        {
            var tr = includeSelf ? self.transform : self.transform.parent;
            while (tr)
            {
                if (tr.TryGetComponent<T>(out var c) && valid(c)) return c;
                if (tr == stopAfter) return null;
                tr = tr.parent;
            }

            return null;
        }

        /// <summary>
        /// Add a component of a specific type to the children of a GameObject.
        /// </summary>
        public static void AddComponentOnChildren<T>(this Component self, HideFlags hideFlags, bool includeSelf)
            where T : Component
        {
            if (self == null) return;

            Profiler.BeginSample("(COF)[ComponentExt] AddComponentOnChildren > Self");
            if (includeSelf && !self.TryGetComponent<T>(out _))
            {
                var c = self.gameObject.AddComponent<T>();
                c.hideFlags = hideFlags;
            }

            Profiler.EndSample();

            Profiler.BeginSample("(COF)[ComponentExt] AddComponentOnChildren > Child");
            var childCount = self.transform.childCount;
            for (var i = 0; i < childCount; i++)
            {
                var child = self.transform.GetChild(i);
                if (child.TryGetComponent<T>(out _)) continue;

                var c = child.gameObject.AddComponent<T>();
                c.hideFlags = hideFlags;
            }

            Profiler.EndSample();
        }

        /// <summary>
        /// Add a component of a specific type to the children of a GameObject.
        /// </summary>
        public static void AddComponentOnChildren<T>(this Component self, bool includeSelf)
            where T : Component
        {
            if (self == null) return;

            Profiler.BeginSample("(COF)[ComponentExt] AddComponentOnChildren > Self");
            if (includeSelf && !self.TryGetComponent<T>(out _))
            {
                self.gameObject.AddComponent<T>();
            }

            Profiler.EndSample();

            Profiler.BeginSample("(COF)[ComponentExt] AddComponentOnChildren > Child");
            var childCount = self.transform.childCount;
            for (var i = 0; i < childCount; i++)
            {
                var child = self.transform.GetChild(i);
                if (child.TryGetComponent<T>(out _)) continue;

                child.gameObject.AddComponent<T>();
            }

            Profiler.EndSample();
        }

#if !UNITY_2021_2_OR_NEWER && !UNITY_2020_3_45 && !UNITY_2020_3_46 && !UNITY_2020_3_47 && !UNITY_2020_3_48
        public static T GetComponentInParent<T>(this Component self, bool includeInactive) where T : Component
        {
            if (!self) return null;
            if (!includeInactive) return self.GetComponentInParent<T>();

            var current = self.transform;
            while (current)
            {
                if (current.TryGetComponent<T>(out var c)) return c;
                current = current.parent;
            }

            return null;
        }
#endif

#if UNITY_EDITOR
        /// <summary>
        /// Verify whether it can be converted to the specified component.
        /// </summary>
        internal static bool CanConvertTo<T>(this Object context) where T : MonoBehaviour
        {
            return context && context.GetType() != typeof(T);
        }

        /// <summary>
        /// Convert to the specified component.
        /// </summary>
        internal static void ConvertTo<T>(this Object context) where T : MonoBehaviour
        {
            var target = context as MonoBehaviour;
            if (target == null) return;

            var so = new SerializedObject(target);
            so.Update();

            var oldEnable = target.enabled;
            target.enabled = false;

            // Find MonoScript of the specified component.
            foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                if (script.GetClass() != typeof(T))
                {
                    continue;
                }

                // Set 'm_Script' to convert.
                so.FindProperty("m_Script").objectReferenceValue = script;
                so.ApplyModifiedProperties();
                break;
            }

            if (so.targetObject is MonoBehaviour mb)
            {
                mb.enabled = oldEnable;
            }
        }
#endif
    }
}
