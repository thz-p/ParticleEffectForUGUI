using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace Coffee.UIParticleInternal
{
    /// <summary>
    /// 快速动作（事件/委托）的基类。
    /// 提供高性能的委托管理机制，使用对象池优化内存分配。
    /// </summary>
    /// <typeparam name="T">委托类型，通常是Action或Action<T>等委托类型</typeparam>
    internal class FastActionBase<T>
    {
        // 使用对象池来管理LinkedListNode<T>实例，避免频繁的节点创建和销毁
        private static readonly InternalObjectPool<LinkedListNode<T>> s_NodePool =
            new InternalObjectPool<LinkedListNode<T>>(
                // 创建委托：创建新的链表节点，初始值为default(T)
                () => new LinkedListNode<T>(default),
                // 验证委托：总是返回true，假设节点总是有效的
                _ => true,
                // 返回委托：在节点返回到池中前，将节点的值重置为默认值
                x => x.Value = default
            );

        // 使用双向链表来存储委托，提供高效的添加、删除和遍历操作
        private readonly LinkedList<T> _delegates = new LinkedList<T>();

        /// <summary>
        /// 向动作中添加一个委托。
        /// </summary>
        /// <param name="rhs">要添加的委托实例</param>
        public void Add(T rhs)
        {
            // 检查委托是否为null，避免添加无效委托
            if (rhs == null) return;

            // 开始性能分析，标记为添加操作
            Profiler.BeginSample("(COF)[FastAction] Add Action");

            // 从对象池中租借一个链表节点
            var node = s_NodePool.Rent();

            // 设置节点的值为要添加的委托
            node.Value = rhs;

            // 将节点添加到链表的末尾
            _delegates.AddLast(node);

            // 结束性能分析
            Profiler.EndSample();
        }

        /// <summary>
        /// 从动作中移除一个委托。
        /// </summary>
        /// <param name="rhs">要移除的委托实例</param>
        public void Remove(T rhs)
        {
            // 检查委托是否为null，避免无效操作
            if (rhs == null) return;

            // 开始性能分析，标记为移除操作
            Profiler.BeginSample("(COF)[FastAction] Remove Action");

            // 在链表中查找包含指定委托的节点
            var node = _delegates.Find(rhs);

            // 如果找到对应的节点
            if (node != null)
            {
                // 从链表中移除该节点
                _delegates.Remove(node);

                // 将节点返回到对象池中，以便复用
                s_NodePool.Return(ref node);
            }

            // 结束性能分析
            Profiler.EndSample();
        }

        /// <summary>
        /// 使用回调函数调用动作中的所有委托。
        /// 这是一个受保护的方法，由派生类调用具体的委托类型。
        /// </summary>
        /// <param name="callback">用于调用每个委托的回调函数</param>
        protected void Invoke(Action<T> callback)
        {
            // 从链表的第一个节点开始遍历
            var node = _delegates.First;

            // 遍历链表中的所有节点
            while (node != null)
            {
                try
                {
                    // 使用回调函数调用当前节点的委托
                    callback(node.Value);
                }
                catch (Exception e)
                {
                    // 捕获并记录调用过程中可能出现的异常
                    // 避免一个委托的异常影响其他委托的执行
                    Debug.LogException(e);
                }

                // 移动到链表的下一个节点
                node = node.Next;
            }
        }

        /// <summary>
        /// 清空动作中的所有委托。
        /// </summary>
        public void Clear()
        {
            // 清空链表，移除所有节点
            _delegates.Clear();
        }
    }

    /// <summary>
    /// 无参数快速动作类。
    /// 继承自FastActionBase<Action>，专门用于管理无参数的Action委托。
    /// 在Unity粒子系统中用于高性能的事件处理，避免GC分配。
    /// </summary>
    internal class FastAction : FastActionBase<Action>
    {
        /// <summary>
        /// 调用所有已注册的无参数委托。
        /// 使用基类的Invoke方法，通过lambda表达式调用每个Action。
        /// </summary>
        /// <example>
        /// 使用示例：
        /// var action = new FastAction();
        /// action.Add(() => Debug.Log("Hello"));
        /// action.Add(() => particleSystem.Play());
        /// action.Invoke(); // 依次执行所有注册的委托
        /// </example>
        public void Invoke()
        {
            // 调用基类的Invoke方法，传入lambda表达式来执行每个Action
            // action => action.Invoke() 表示对每个Action调用其Invoke方法
            Invoke(action => action.Invoke());
        }
    }

}
