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


        public static byte GammaToLinear(this byte self)
        {
            if (s_GammaToLinearLut == null)
            {
                s_GammaToLinearLut = new byte[256];
                for (var i = 0; i < 256; i++)
                {
                    s_GammaToLinearLut[i] = (byte)(Mathf.GammaToLinearSpace(i / 255f) * 255f);
                }
            }

            return s_GammaToLinearLut[self];
        }

        public static void LinearToGamma(this Mesh self)
        {
            Profiler.BeginSample("(COF)[ColorExt] LinearToGamma (Mesh)");
            self.GetColors(s_Colors);
            var count = s_Colors.Count;
            for (var i = 0; i < count; i++)
            {
                var c = s_Colors[i];
                c.r = c.r.LinearToGamma();
                c.g = c.g.LinearToGamma();
                c.b = c.b.LinearToGamma();
                s_Colors[i] = c;
            }

            self.SetColors(s_Colors);
            Profiler.EndSample();
        }

        public static void GammaToLinear(this Mesh self)
        {
            Profiler.BeginSample("(COF)[ColorExt] GammaToLinear (Mesh)");
            self.GetColors(s_Colors);
            var count = s_Colors.Count;
            for (var i = 0; i < count; i++)
            {
                var c = s_Colors[i];
                c.r = c.r.GammaToLinear();
                c.g = c.g.GammaToLinear();
                c.b = c.b.GammaToLinear();
                s_Colors[i] = c;
            }

            self.SetColors(s_Colors);
            Profiler.EndSample();
        }
    }
}
