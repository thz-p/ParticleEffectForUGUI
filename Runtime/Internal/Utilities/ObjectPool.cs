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
        /// 从对象池中租借一个实例。
        /// 当不再需要时，使用 <see cref="Return" /> 方法将其返回到池中。
        /// </summary>
        public T Rent()
        {
            // 循环检查池中是否有可用的实例
            while (0 < _pool.CountInactive)
            {
                // 从池中获取一个实例
                var instance = _pool.Get();

                // 验证实例是否有效（通过_onValid委托函数检查）
                if (_onValid(instance))
                {
                    // 如果实例有效，则返回给调用者使用
                    return instance;
                }
                // 如果实例无效，继续循环查找下一个可用实例
            }

            // 如果池中没有可用的有效实例，则创建一个新的实例
            // 记录日志，显示当前池状态（池中实例数，已创建实例总数）
            Logging.Log(this, $"A new instance is created (pooled: {_pool.CountInactive}, created: {_pool.CountAll}).");

            // 创建并返回一个新的实例
            return _pool.Get();
        }

        /// <summary>
        /// 将实例返回到对象池并设置为null。
        /// 确保使用此方法返回通过 <see cref="Rent" /> 方法获取的实例。
        /// </summary>
        /// <param name="instance">要返回到对象池的实例引用</param>
        public void Return(ref T instance)
        {
            // 如果实例为null，直接返回，避免重复释放或处理空引用
            if (instance == null) return; // Ignore if already pooled or null.

            // 将实例释放回Unity的对象池中
            _pool.Release(instance);
            
            // 记录日志，显示当前对象池的状态信息
            // 包括池中可用的实例数量和已创建的总实例数量
            Logging.Log(this, $"An instance is released (pooled: {_pool.CountInactive}, created: {_pool.CountAll}).");
            
            // 将实例引用设置为默认值（对于引用类型就是null）
            // 这样可以防止外部代码继续使用已返回到池中的实例
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
        /// 从对象池中租借一个实例。
        /// 当不再需要时，使用 <see cref="Return" /> 方法将其返回到池中。
        /// </summary>
        /// <returns>从对象池获取的可用实例</returns>
        public T Rent()
        {
            // 循环检查对象池中是否有可用的实例
            while (0 < _pool.Count)
            {
                // 从对象池的栈顶弹出一个实例（后进先出原则）
                var instance = _pool.Pop();
                
                // 使用验证委托函数检查实例是否仍然有效
                // 例如：检查实例是否已被销毁或处于无效状态
                if (_onValid(instance))
                {
                    // 如果实例有效，则返回给调用者使用
                    return instance;
                }
                // 如果实例无效，继续循环查找下一个可用实例
            }

            // 如果对象池中没有可用的有效实例，则创建一个新的实例
            // 记录日志，显示当前对象池状态（池中实例数，已创建实例总数）
            Logging.Log(this, $"A new instance is created (pooled: {_pool.Count}, created: {++_count}).");
            
            // 调用创建委托函数创建新的实例并返回
            return _onCreate();
        }

        /// <summary>
        /// 将实例返回到对象池并设置为null。
        /// 确保使用此方法返回通过 <see cref="Rent" /> 方法获取的实例。
        /// </summary>
        /// <param name="instance">要返回到对象池的实例引用</param>
        public void Return(ref T instance)
        {
            // 检查实例是否为null或已经在对象池中
            // 避免重复释放或处理无效引用
            if (instance == null || _pool.Contains(instance)) return; // Ignore if already pooled or null.

            // 调用返回委托函数，执行实例返回前的清理操作
            // 例如：重置实例状态、清理资源等
            _onReturn(instance); // Return the instance to the pool.
            
            // 将实例压入对象池栈中（后进先出原则）
            _pool.Push(instance);
            
            // 记录日志，显示当前对象池状态信息
            // 包括池中实例数量和已创建的总实例数量
            Logging.Log(this, $"An instance is released (pooled: {_pool.Count}, created: {_count}).");
            
            // 将实例引用设置为默认值（对于引用类型就是null）
            // 防止外部代码继续使用已返回到池中的实例
            instance = default; // Set the reference to null.
        }

#endif
    }

    /// <summary>
    /// List<T>泛型集合的对象池。
    /// 提供List实例的复用机制，避免频繁创建和销毁List带来的性能开销。
    /// </summary>
    /// <typeparam name="T">List中元素的类型</typeparam>
    internal static class InternalListPool<T>
    {
#if UNITY_2021_1_OR_NEWER
        /// <summary>
        /// 从对象池中租借一个List实例。
        /// 当不再需要时，使用 <see cref="Return" /> 方法将其返回到池中。
        /// </summary>
        /// <returns>从对象池获取的List实例</returns>
        public static List<T> Rent()
        {
            // 使用Unity 2021.1及以上版本提供的ListPool
            // 直接调用Unity内置的List对象池获取实例
            return UnityEngine.Pool.ListPool<T>.Get();
        }

        /// <summary>
        /// 将List实例返回到对象池并设置为null。
        /// 确保使用此方法返回通过 <see cref="Rent" /> 方法获取的实例。
        /// </summary>
        /// <param name="toRelease">要返回到对象池的List实例引用</param>
        public static void Return(ref List<T> toRelease)
        {
            // 检查List实例是否为null
            if (toRelease != null)
            {
                // 将List实例释放回Unity的List对象池中
                UnityEngine.Pool.ListPool<T>.Release(toRelease);
            }

            // 将引用设置为null，防止继续使用已返回到池中的实例
            toRelease = null;
        }
#else
        // Unity 2021.1以下版本使用自定义的对象池实现
        // 创建InternalObjectPool实例来管理List<T>对象池
        private static readonly InternalObjectPool<List<T>> s_ListPool =
            new InternalObjectPool<List<T>>(
                // 创建委托：当需要新实例时，创建一个空的List<T>
                () => new List<T>(), 
                // 验证委托：总是返回true，假设List实例总是有效的
                _ => true, 
                // 返回委托：在实例返回到池中前，清空List中的元素
                x => x.Clear()
            );

        /// <summary>
        /// 从对象池中租借一个List实例。
        /// 当不再需要时，使用 <see cref="Return" /> 方法将其返回到池中。
        /// </summary>
        /// <returns>从对象池获取的List实例</returns>
        public static List<T> Rent()
        {
            // 通过自定义对象池获取List实例
            return s_ListPool.Rent();
        }

        /// <summary>
        /// 将List实例返回到对象池并设置为null。
        /// 确保使用此方法返回通过 <see cref="Rent" /> 方法获取的实例。
        /// </summary>
        /// <param name="toRelease">要返回到对象池的List实例引用</param>
        public static void Return(ref List<T> toRelease)
        {
            // 通过自定义对象池返回List实例
            s_ListPool.Return(ref toRelease);
        }
#endif
    }
}
