using System;
using UnityEngine;
using UnityEngine.U2D;
#if UNITY_EDITOR
using System.Reflection;
#endif

namespace Coffee.UIParticleInternal
{
    /// <summary>
    /// Extension methods for Sprite class.
    /// </summary>
    internal static class SpriteExtensions
    {
#if UNITY_EDITOR
        // 反射获取Unity编辑器中SpriteEditorExtension类的类型
        // 首先尝试获取实验版U2D命名空间下的类，如果不存在则尝试获取正式版U2D命名空间下的类
        private static readonly Type s_SpriteEditorExtensionType = 
            Type.GetType("UnityEditor.Experimental.U2D.SpriteEditorExtension, UnityEditor")
            ?? Type.GetType("UnityEditor.U2D.SpriteEditorExtension, UnityEditor");

        // 通过反射创建获取精灵实际纹理的委托方法
        private static readonly Func<Sprite, Texture2D> s_GetActiveAtlasTextureMethod = 
            (Func<Sprite, Texture2D>)Delegate.CreateDelegate(typeof(Func<Sprite, Texture2D>), 
                s_SpriteEditorExtensionType
                    .GetMethod("GetActiveAtlasTexture", BindingFlags.Static | BindingFlags.NonPublic));

        // 通过反射创建获取精灵图集的委托方法
        private static readonly Func<Sprite, SpriteAtlas> s_GetActiveAtlasMethod = 
            (Func<Sprite, SpriteAtlas>)Delegate.CreateDelegate(typeof(Func<Sprite, SpriteAtlas>), 
                s_SpriteEditorExtensionType
                    .GetMethod("GetActiveAtlas", BindingFlags.Static | BindingFlags.NonPublic));

        /// <summary>
        /// 获取精灵在播放模式或编辑模式下的实际纹理。
        /// </summary>
        public static Texture2D GetActualTexture(this Sprite self)
        {
            // 如果精灵为空，返回null
            if (!self) return null;

            // 尝试通过编辑器扩展方法获取实际纹理
            var ret = s_GetActiveAtlasTextureMethod(self);
            // 如果获取成功则返回该纹理，否则返回精灵自身的texture属性
            return ret ? ret : self.texture;
        }

        /// <summary>
        /// 获取精灵在播放模式或编辑模式下的活动精灵图集。
        /// </summary>
        public static SpriteAtlas GetActiveAtlas(this Sprite self)
        {
            // 如果精灵为空，返回null
            if (!self) return null;

            // 通过编辑器扩展方法获取活动精灵图集
            return s_GetActiveAtlasMethod(self);
        }
#else
        /// <summary>
        /// 获取精灵在播放模式下的实际纹理。
        /// </summary>
        internal static Texture2D GetActualTexture(this Sprite self)
        {
            // 如果精灵不为空则返回其texture属性，否则返回null
            return self ? self.texture : null;
        }
#endif

    }
}
