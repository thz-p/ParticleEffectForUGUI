using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coffee.UIParticleInternal
{
    internal static class FrameCache
    {
        // 静态字典，用于存储不同类型对应的帧缓存实例
        // Key: 缓存值的类型Type，Value: 实现IFrameCache接口的缓存容器
        private static readonly Dictionary<Type, IFrameCache> s_Caches = new Dictionary<Type, IFrameCache>();

        // 静态构造函数，在类首次被访问时自动执行
        static FrameCache()
        {
            // 清空缓存字典，确保初始状态为空
            s_Caches.Clear();

            // 注册Canvas重建后的回调事件，在每次Canvas重建后自动清空所有缓存
            // 这样可以避免使用过期的缓存数据，确保渲染的正确性
            UIExtraCallbacks.onLateAfterCanvasRebuild += ClearAllCache;
        }

#if UNITY_EDITOR
        // 仅在Unity编辑器中生效的运行时初始化方法
        // 在子系统注册时执行，用于编辑器模式下的缓存清理
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Clear()
        {
            // 清空所有缓存，防止编辑器重新编译后使用旧的缓存数据
            s_Caches.Clear();
        }
#endif
        /// <summary>
        /// Tries to retrieve a value from the frame cache with a specified key.
        /// </summary>
        public static bool TryGet<T>(object key1, string key2, out T result)
        {
            return GetFrameCache<T>().TryGet((key1.GetHashCode(), key2.GetHashCode()), out result);
        }

        /// <summary>
        /// Tries to retrieve a value from the frame cache with a specified key.
        /// </summary>
        public static bool TryGet<T>(object key1, string key2, int key3, out T result)
        {
            return GetFrameCache<T>().TryGet((key1.GetHashCode(), key2.GetHashCode() + key3), out result);
        }

        /// <summary>
        /// Sets a value in the frame cache with a specified key.
        /// </summary>
        public static void Set<T>(object key1, string key2, T result)
        {
            GetFrameCache<T>().Set((key1.GetHashCode(), key2.GetHashCode()), result);
        }

        /// <summary>
        /// Sets a value in the frame cache with a specified key.
        /// </summary>
        public static void Set<T>(object key1, string key2, int key3, T result)
        {
            GetFrameCache<T>().Set((key1.GetHashCode(), key2.GetHashCode() + key3), result);
        }

        private static void ClearAllCache()
        {
            foreach (var cache in s_Caches.Values)
            {
                cache.Clear();
            }
        }

        private static FrameCacheContainer<T> GetFrameCache<T>()
        {
            var t = typeof(T);
            if (s_Caches.TryGetValue(t, out var frameCache)) return frameCache as FrameCacheContainer<T>;

            frameCache = new FrameCacheContainer<T>();
            s_Caches.Add(t, frameCache);

            return (FrameCacheContainer<T>)frameCache;
        }

        // 帧缓存接口定义，为不同类型的缓存容器提供统一的抽象契约
        // 该接口用于实现多态缓存管理，允许FrameCache类统一管理不同类型的缓存实例
        private interface IFrameCache
        {
            // 清空缓存内容的方法契约
            // 实现此接口的类必须提供清空其内部缓存数据的逻辑
            // 在Canvas重建或编辑器重新编译时被调用，确保缓存数据的时效性
            void Clear();
        }

        private class FrameCacheContainer<T> : IFrameCache
        {
            private readonly Dictionary<(int, int), T> _caches = new Dictionary<(int, int), T>();

            public void Clear()
            {
                _caches.Clear();
            }

            public bool TryGet((int, int) key, out T result)
            {
                return _caches.TryGetValue(key, out result);
            }

            public void Set((int, int) key, T result)
            {
                _caches[key] = result;
            }
        }
    }
}
