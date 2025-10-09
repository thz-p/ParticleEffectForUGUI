using UnityEngine;

namespace Coffee.UIParticleInternal
{
    internal static class Vector3Extensions
    {
        /// <summary>
        /// 计算Vector3向量每个分量的倒数，特别处理了接近0的值以避免除以零错误
        /// </summary>
        /// <param name="self">当前Vector3实例</param>
        /// <returns>计算后的倒数向量</returns>
        public static Vector3 Inverse(this Vector3 self)
        {
            // 如果x分量接近0，则设置为1；否则取倒数
            self.x = Mathf.Approximately(self.x, 0) ? 1 : 1 / self.x;
            // 如果y分量接近0，则设置为1；否则取倒数
            self.y = Mathf.Approximately(self.y, 0) ? 1 : 1 / self.y;
            // 如果z分量接近0，则设置为1；否则取倒数
            self.z = Mathf.Approximately(self.z, 0) ? 1 : 1 / self.z;
            // 返回处理后的向量
            return self;
        }

        /// <summary>
        /// 向量缩放扩展方法 - 将当前向量的每个分量与另一个向量的对应分量相乘
        /// </summary>
        /// <param name="self">当前Vector3实例</param>
        /// <param name="other1">用于缩放的向量</param>
        /// <returns>缩放后的向量</returns>
        public static Vector3 GetScaled(this Vector3 self, Vector3 other1)
        {
            // 对当前向量应用缩放操作，将self的每个分量乘以other1的对应分量
            self.Scale(other1);
            // 返回缩放后的向量
            return self;
        }

        public static Vector3 GetScaled(this Vector3 self, Vector3 other1, Vector3 other2)
        {
            self.Scale(other1);
            self.Scale(other2);
            return self;
        }

        public static Vector3 GetScaled(this Vector3 self, Vector3 other1, Vector3 other2, Vector3 other3)
        {
            self.Scale(other1);
            self.Scale(other2);
            self.Scale(other3);
            return self;
        }

        /// <summary>
        /// 检查3D向量是否可见（非零向量）
        /// </summary>
        /// <param name="self">当前Vector3实例</param>
        /// <returns>如果向量的体积（x*y*z的绝对值）大于0，则返回true，表示向量可见；否则返回false</returns>
        public static bool IsVisible(this Vector3 self)
        {
            // 计算向量各分量乘积的绝对值，并判断是否大于0
            // 这用于确定向量是否表示一个有效的3D空间体积（非零向量）
            return 0 < Mathf.Abs(self.x * self.y * self.z);
        }

        /// <summary>
        /// 检查向量在2D平面上是否可见（非零向量）
        /// </summary>
        /// <param name="self">当前Vector3实例</param>
        /// <returns>如果向量在XY平面上的面积（x*y的绝对值）大于0，则返回true，表示向量在2D空间可见；否则返回false</returns>
        public static bool IsVisible2D(this Vector3 self)
        {
            // 计算向量XY分量乘积的绝对值，并判断是否大于0
            // 这用于确定向量是否在2D空间中表示一个有效的面积（忽略Z轴）
            return 0 < Mathf.Abs(self.x * self.y);
        }
    }
}
