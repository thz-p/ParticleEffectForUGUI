using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace Coffee.UIParticleInternal
{
    internal static class Color32Extensions
    {
        /// <summary>
        /// 用于临时存储Color32类型的静态列表，避免频繁创建新的列表实例
        /// </summary>
        private static readonly List<Color32> s_Colors = new List<Color32>();
        
        /// <summary>
        /// 线性空间到伽马空间的颜色查找表，用于快速颜色空间转换
        /// 采用懒加载方式初始化，仅在首次使用时创建
        /// </summary>
        private static byte[] s_LinearToGammaLut;
        
        /// <summary>
        /// 伽马空间到线性空间的颜色查找表，与s_LinearToGammaLut配对使用
        /// </summary>
        private static byte[] s_GammaToLinearLut;

        /// <summary>
        /// 将线性空间的颜色值转换为伽马空间的颜色值
        /// 采用查找表优化，避免运行时重复计算，显著提高性能
        /// </summary>
        /// <param name="self">要转换的字节颜色值（范围0-255）</param>
        /// <returns>转换后的伽马空间颜色值（范围0-255）</returns>
        public static byte LinearToGamma(this byte self)
        {
            // 懒加载模式：仅在首次调用时初始化查找表
            if (s_LinearToGammaLut == null)
            {
                // 创建包含256个元素的查找表，覆盖所有可能的字节值（0-255）
                s_LinearToGammaLut = new byte[256];
                // 预计算所有线性空间值到伽马空间的映射
                for (var i = 0; i < 256; i++)
                {
                    // 1. 将字节值归一化到0-1范围
                    // 2. 使用Unity内置的LinearToGammaSpace函数进行颜色空间转换
                    // 3. 将结果重新映射回0-255范围并转换为字节类型
                    s_LinearToGammaLut[i] = (byte)(Mathf.LinearToGammaSpace(i / 255f) * 255f);
                }
            }

            // 直接从查找表中获取转换后的值，O(1)时间复杂度
            return s_LinearToGammaLut[self];
        }

        /// <summary>
        /// 将伽马空间的颜色值转换为线性空间的颜色值
        /// 采用查找表优化，避免运行时重复计算，显著提高性能
        /// 与LinearToGamma方法配对使用，共同构成完整的颜色空间转换体系
        /// </summary>
        /// <param name="self">要转换的字节颜色值（范围0-255）</param>
        /// <returns>转换后的线性空间颜色值（范围0-255）</returns>
        public static byte GammaToLinear(this byte self)
        {
            // 懒加载模式：仅在首次调用时初始化查找表
            if (s_GammaToLinearLut == null)
            {
                // 创建包含256个元素的查找表，覆盖所有可能的字节值（0-255）
                s_GammaToLinearLut = new byte[256];
                // 预计算所有伽马空间值到线性空间的映射
                for (var i = 0; i < 256; i++)
                {
                    // 1. 将字节值归一化到0-1范围
                    // 2. 使用Unity内置的GammaToLinearSpace函数进行颜色空间转换
                    // 3. 将结果重新映射回0-255范围并转换为字节类型
                    s_GammaToLinearLut[i] = (byte)(Mathf.GammaToLinearSpace(i / 255f) * 255f);
                }
            }

            // 直接从查找表中获取转换后的值，O(1)时间复杂度
            return s_GammaToLinearLut[self];
        }

        /// <summary>
        /// 将Mesh中所有顶点颜色从线性空间转换为伽马空间
        /// 这个扩展方法批量处理网格的所有顶点颜色，适用于需要对整个网格进行颜色空间转换的场景
        /// 在UI粒子系统中，这通常用于确保粒子网格的颜色在不同渲染设置下显示一致
        /// </summary>
        /// <param name="self">要处理的Mesh对象</param>
        public static void LinearToGamma(this Mesh self)
        {
            // 开始性能分析采样，用于监控此方法的执行性能
            Profiler.BeginSample("(COF)[ColorExt] LinearToGamma (Mesh)");
            
            // 使用静态列表获取网格的所有顶点颜色，避免每次调用都创建新的列表实例
            // s_Colors是类级别的静态变量，在多个方法间共享以减少内存分配
            self.GetColors(s_Colors);
            
            // 缓存顶点颜色数量，避免在循环中重复访问属性
            var count = s_Colors.Count;
            
            // 遍历所有顶点颜色，对每个颜色的RGB分量分别进行线性到伽马空间的转换
            for (var i = 0; i < count; i++)
            {
                // 获取当前顶点颜色
                var c = s_Colors[i];
                // 对红、绿、蓝三个颜色通道分别应用线性到伽马的转换
                c.r = c.r.LinearToGamma();
                c.g = c.g.LinearToGamma();
                c.b = c.b.LinearToGamma();
                // 将转换后的颜色存回列表
                s_Colors[i] = c;
            }

            // 将转换后的颜色列表重新设置回Mesh
            self.SetColors(s_Colors);
            
            // 结束性能分析采样
            Profiler.EndSample();
        }

        /// <summary>
        /// 将Mesh中所有顶点颜色从伽马空间转换为线性空间
        /// 这是UI粒子系统中颜色处理的重要方法，确保光照计算在正确的颜色空间中进行
        /// 与LinearToGamma方法配对使用，构成完整的颜色空间转换体系
        /// </summary>
        /// <param name="self">要处理的Mesh对象</param>
        public static void GammaToLinear(this Mesh self)
        {
            // 开始性能分析采样，用于监控此方法的执行性能
            Profiler.BeginSample("(COF)[ColorExt] GammaToLinear (Mesh)");
            
            // 使用静态列表获取网格的所有顶点颜色，避免每次调用都创建新的列表实例
            // s_Colors是类级别的静态变量，在多个方法间共享以减少内存分配
            self.GetColors(s_Colors);
            
            // 缓存顶点颜色数量，避免在循环中重复访问属性
            var count = s_Colors.Count;
            
            // 遍历所有顶点颜色，对每个颜色的RGB分量分别进行伽马到线性空间的转换
            for (var i = 0; i < count; i++)
            {
                // 获取当前顶点颜色
                var c = s_Colors[i];
                // 对红、绿、蓝三个颜色通道分别应用伽马到线性的转换
                c.r = c.r.GammaToLinear();
                c.g = c.g.GammaToLinear();
                c.b = c.b.GammaToLinear();
                // 将转换后的颜色存回列表
                s_Colors[i] = c;
            }

            // 将转换后的颜色列表重新设置回Mesh
            self.SetColors(s_Colors);
            
            // 结束性能分析采样
            Profiler.EndSample();
        }

    }
}
