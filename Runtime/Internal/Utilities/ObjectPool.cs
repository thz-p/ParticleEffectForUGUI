using System;
using System.Collections.Generic;

namespace Coffee.UIParticleInternal
{
    /// <summary>
    /// Object pool.
    /// </summary>
    internal class InternalObjectPool<T> where T : class
    {
#if UNITY_2021_1_OR_NEWER
        private readonly Predicate<T> _onValid; // Delegate for checking if instances are valid
        private readonly UnityEngine.Pool.ObjectPool<T> _pool;

        /// <summary>
        /// 构造函数，初始化对象池
        /// </summary>
        /// <param name="onCreate">创建新实例的委托函数</param>
        /// <param name="onValid">验证实例是否有效的委托函数</param>
        /// <param name="onReturn">实例返回到池中时执行的委托函数</param>
        public InternalObjectPool(Func<T> onCreate, Predicate<T> onValid, Action<T> onReturn)
        {
            // 使用Unity 2021.1及以上版本提供的对象池实现
            // 创建Unity内置的对象池，传入创建函数和返回函数
            _pool = new UnityEngine.Pool.ObjectPool<T>(onCreate, null, onReturn);
            
            // 保存验证实例有效性的委托函数
            _onValid = onValid;
        }

        /// <summary>
        /// Rent an instance from the pool.
        /// When you no longer need it, return it with <see cref="Return" />.
        /// </summary>
        public T Rent()
        {
            while (0 < _pool.CountInactive)
            {
                var instance = _pool.Get();
                if (_onValid(instance))
                {
                    return instance;
                }
            }

            // If there are no instances in the pool, create a new one.
            Logging.Log(this, $"A new instance is created (pooled: {_pool.CountInactive}, created: {_pool.CountAll}).");
            return _pool.Get();
        }

        /// <summary>
        /// Return an instance to the pool and assign null.
        /// Be sure to return the instance obtained with <see cref="Rent" /> with this method.
        /// </summary>
        public void Return(ref T instance)
        {
            if (instance == null) return; // Ignore if already pooled or null.

            _pool.Release(instance);
            Logging.Log(this, $"An instance is released (pooled: {_pool.CountInactive}, created: {_pool.CountAll}).");
            instance = default; // Set the reference to null.
        }
#else
        private readonly Func<T> _onCreate; // Delegate for creating instances
        private readonly Action<T> _onReturn; // Delegate for returning instances to the pool
        private readonly Predicate<T> _onValid; // Delegate for checking if instances are valid
        private readonly Stack<T> _pool = new Stack<T>(32); // Object pool
        private int _count; // Total count of created instances

        public InternalObjectPool(Func<T> onCreate, Predicate<T> onValid, Action<T> onReturn)
        {
            _onCreate = onCreate;
            _onValid = onValid;
            _onReturn = onReturn;
        }

        /// <summary>
        /// Rent an instance from the pool.
        /// When you no longer need it, return it with <see cref="Return" />.
        /// </summary>
        public T Rent()
        {
            while (0 < _pool.Count)
            {
                var instance = _pool.Pop();
                if (_onValid(instance))
                {
                    return instance;
                }
            }

            // If there are no instances in the pool, create a new one.
            Logging.Log(this, $"A new instance is created (pooled: {_pool.Count}, created: {++_count}).");
            return _onCreate();
        }

        /// <summary>
        /// Return an instance to the pool and assign null.
        /// Be sure to return the instance obtained with <see cref="Rent" /> with this method.
        /// </summary>
        public void Return(ref T instance)
        {
            if (instance == null || _pool.Contains(instance)) return; // Ignore if already pooled or null.

            _onReturn(instance); // Return the instance to the pool.
            _pool.Push(instance);
            Logging.Log(this, $"An instance is released (pooled: {_pool.Count}, created: {_count}).");
            instance = default; // Set the reference to null.
        }
#endif
    }

    /// <summary>
    /// Object pool for <see cref="List{T}" />.
    /// </summary>
    internal static class InternalListPool<T>
    {
#if UNITY_2021_1_OR_NEWER
        /// <summary>
        /// Rent an instance from the pool.
        /// When you no longer need it, return it with <see cref="Return" />.
        /// </summary>
        public static List<T> Rent()
        {
            return UnityEngine.Pool.ListPool<T>.Get();
        }

        /// <summary>
        /// Return an instance to the pool and assign null.
        /// Be sure to return the instance obtained with <see cref="Rent" /> with this method.
        /// </summary>
        public static void Return(ref List<T> toRelease)
        {
            if (toRelease != null)
            {
                UnityEngine.Pool.ListPool<T>.Release(toRelease);
            }

            toRelease = null;
        }
#else
        private static readonly InternalObjectPool<List<T>> s_ListPool =
            new InternalObjectPool<List<T>>(() => new List<T>(), _ => true, x => x.Clear());

        /// <summary>
        /// Rent an instance from the pool.
        /// When you no longer need it, return it with <see cref="Return" />.
        /// </summary>
        public static List<T> Rent()
        {
            return s_ListPool.Rent();
        }

        /// <summary>
        /// Return an instance to the pool and assign null.
        /// Be sure to return the instance obtained with <see cref="Rent" /> with this method.
        /// </summary>
        public static void Return(ref List<T> toRelease)
        {
            s_ListPool.Return(ref toRelease);
        }
#endif
    }
}
