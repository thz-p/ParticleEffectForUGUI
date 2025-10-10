using System;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#endif

namespace Coffee.UIParticleInternal
{
    public abstract class PreloadedProjectSettings : ScriptableObject
#if UNITY_EDITOR
    {
        // 资源后处理器类，继承自AssetPostprocessor
        // 用于在资源导入、删除、移动等操作后自动执行初始化逻辑
        private class Postprocessor : AssetPostprocessor
        {
            // 当所有资源处理完成后调用的静态方法
            // 参数使用下划线命名表示这些参数在当前实现中未被使用
            // _: 导入的资源路径数组
            // __: 删除的资源路径数组  
            // ___: 移动的资源路径数组（从）
            // ____: 移动的资源路径数组（到）
            private static void OnPostprocessAllAssets(string[] _, string[] __, string[] ___, string[] ____)
            {
                // 调用初始化方法，确保项目设置正确配置
                Initialize();
            }
        }


        private class PreprocessBuildWithReport : IPreprocessBuildWithReport
        {
            int IOrderedCallback.callbackOrder => 0;

            void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report)
            {
                Initialize();
            }
        }

        // 初始化所有预加载项目设置的静态方法
        // 该方法会在资源导入后和构建前自动调用，确保项目设置正确配置
        private static void Initialize()
        {
            // 获取所有继承自PreloadedProjectSettings<>的派生类型
            // 使用TypeCache提高性能，避免反射开销
            foreach (var t in TypeCache.GetTypesDerivedFrom(typeof(PreloadedProjectSettings<>)))
            {
                // 获取该类型的默认设置实例
                var defaultSettings = GetDefaultSettings(t);
                
                // 如果默认设置不存在，创建新的实例并设置为默认设置
                if (!defaultSettings)
                {
                    // 当创建新实例时，自动将其设置为默认设置
                    defaultSettings = CreateInstance(t) as PreloadedProjectSettings;
                    SetDefaultSettings(defaultSettings);
                }
                // 如果预加载设置数量不为1（可能为0或多于1），重新设置默认设置
                else if (GetPreloadedSettings(t).Length != 1)
                {
                    // 确保只有一个预加载设置实例
                    SetDefaultSettings(defaultSettings);
                }

                // 如果存在有效的默认设置，调用其初始化方法
                if (defaultSettings)
                {
                    defaultSettings.OnInitialize();
                }
            }
        }

        // 获取类型默认名称的受保护静态方法
        // 用于为项目设置文件生成合适的文件名
        protected static string GetDefaultName(Type type, bool nicify)
        {
            // 获取类型的原始名称（不包含命名空间）
            var typeName = type.Name;
            
            // 根据nicify参数决定是否美化名称
            // nicify为true时使用美化后的名称，适合显示给用户
            // nicify为false时使用原始名称，适合文件系统使用
            return nicify
                ? ObjectNames.NicifyVariableName(typeName)  // 美化变量名（如"MyClassName" -> "My Class Name"）
                : typeName;                                  // 原始类型名称
        }

        // 获取指定类型的预加载设置数组
        // 从Unity PlayerSettings中筛选出指定类型的预加载资源
        private static Object[] GetPreloadedSettings(Type type)
        {
            // 获取所有预加载资源，然后筛选出指定类型的有效资源
            return PlayerSettings.GetPreloadedAssets()
                .Where(x => x && x.GetType() == type)  // 过滤：非空且类型匹配
                .ToArray();                            // 转换为数组返回
        }

        // 获取指定类型的默认设置实例
        // 优先从预加载设置中获取，如果不存在则从AssetDatabase中查找
        protected static PreloadedProjectSettings GetDefaultSettings(Type type)
        {
            // 使用空合并运算符(??)实现优先级查找：
            // 1. 首先尝试从预加载设置中获取第一个匹配的实例
            // 2. 如果预加载设置中没有，则从AssetDatabase中查找
            return GetPreloadedSettings(type).FirstOrDefault() as PreloadedProjectSettings
                   // 从预加载设置中获取第一个匹配项，如果没有则返回null
                   ?? AssetDatabase.FindAssets($"t:{nameof(PreloadedProjectSettings)}")
                       // 在AssetDatabase中查找所有PreloadedProjectSettings类型的资源
                       .Select(AssetDatabase.GUIDToAssetPath)
                       // 将GUID转换为资源路径
                       .Select(AssetDatabase.LoadAssetAtPath<PreloadedProjectSettings>)
                       // 加载路径对应的资源
                       .FirstOrDefault(x => x && x.GetType() == type);
                       // 获取第一个非空且类型匹配的资源
        }


        // 设置指定项目设置资源为默认设置
        // 该方法负责将指定的PreloadedProjectSettings实例设置为项目的默认设置
        // 主要功能包括：创建资源文件、配置预加载设置、确保设置唯一性
        protected static void SetDefaultSettings(PreloadedProjectSettings asset)
        {
            // 安全检查：如果传入的资源为空，直接返回
            if (!asset) return;

            // 获取资源的实际类型，用于后续的类型相关操作
            var type = asset.GetType();
            
            // 检查资源是否已经存在于AssetDatabase中
            // 如果资源还没有对应的资产路径（即尚未保存为.asset文件）
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)))
            {
                // 确保ProjectSettings文件夹存在
                // 检查Assets/ProjectSettings文件夹是否存在，如果不存在则创建
                if (!AssetDatabase.IsValidFolder("Assets/ProjectSettings"))
                {
                    // 创建ProjectSettings文件夹，用于存放项目设置相关的资源文件
                    AssetDatabase.CreateFolder("Assets", "ProjectSettings");
                }

                // 生成资源文件路径
                // 使用类型名称生成默认的文件名，nicify参数为false表示使用原始类型名
                var assetPath = $"Assets/ProjectSettings/{GetDefaultName(type, false)}.asset";
                // 确保路径唯一，避免文件名冲突
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                
                // 检查文件是否已存在，避免重复创建
                if (!File.Exists(assetPath))
                {
                    // 在指定路径创建资源文件
                    AssetDatabase.CreateAsset(asset, assetPath);
                    // 调用资源的创建后处理回调
                    asset.OnCreateAsset();
                }
            }

            // 配置预加载设置
            // 获取当前所有的预加载资源
            var preloadedAssets = PlayerSettings.GetPreloadedAssets();
            // 获取当前类型的所有预加载设置实例
            var projectSettings = GetPreloadedSettings(type);
            
            // 更新预加载资源列表：
            // 1. 过滤掉所有空引用
            // 2. 排除当前类型中除了指定asset之外的所有其他实例（确保类型唯一性）
            // 3. 添加指定的asset到列表末尾
            // 4. 去重处理，确保列表中没有重复项
            // 5. 转换为数组并设置回PlayerSettings
            PlayerSettings.SetPreloadedAssets(preloadedAssets
                .Where(x => x)  // 过滤空引用
                .Except(projectSettings.Except(new[] { asset }))  // 排除当前类型的其他实例，保留指定asset
                .Append(asset)   // 添加指定asset到列表
                .Distinct()      // 去重处理
                .ToArray());     // 转换为数组

            // 刷新AssetDatabase，确保所有更改生效
            // 这会使Unity编辑器重新加载和显示更新后的资源状态
            AssetDatabase.Refresh();
        }


        protected virtual void OnCreateAsset()
        {
        }

        protected virtual void OnInitialize()
        {
        }
    }
#else
    {
    }
#endif

    // 泛型项目设置基类
    // 提供类型安全的单例模式实现，用于管理特定类型的项目设置
    // 泛型约束：T必须是PreloadedProjectSettings<T>的子类（CRTP模式）
    public abstract class PreloadedProjectSettings<T> : PreloadedProjectSettings
        where T : PreloadedProjectSettings<T>
    {
        // 静态单例实例，确保每种类型只有一个实例存在
        private static T s_Instance;

#if UNITY_EDITOR
        // 编辑器模式下用于保存JSON序列化文本，用于播放模式状态切换时的数据恢复
        private string _jsonText;

        // 检查是否存在实例的便捷属性
        public static bool hasInstance => s_Instance;

        // 单例实例访问器
        // 实现懒加载模式，按需创建和获取设置实例
        public static T instance
        {
            get
            {
                // 如果已有实例，直接返回
                if (s_Instance) return s_Instance;

                // 第一步：尝试从默认设置中获取实例
                // 使用基类的GetDefaultSettings方法查找已存在的设置
                s_Instance = GetDefaultSettings(typeof(T)) as T;
                if (s_Instance) return s_Instance;

                // 第二步：如果默认设置不存在，创建新的实例
                s_Instance = CreateInstance<T>();
                // 安全检查：确保实例创建成功
                if (!s_Instance)
                {
                    s_Instance = null;
                    return s_Instance;
                }

                // 第三步：将新创建的实例设置为默认设置
                SetDefaultSettings(s_Instance);
                return s_Instance;
            }
        }

        // 播放模式状态变化事件处理
        // 用于在编辑模式和播放模式之间切换时保存和恢复设置状态
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    // 退出编辑模式前，将当前实例序列化为JSON文本保存
                    _jsonText = EditorJsonUtility.ToJson(this);
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    // 退出播放模式后，从保存的JSON文本恢复实例状态
                    if (_jsonText != null)
                    {
                        EditorJsonUtility.FromJsonOverwrite(_jsonText, this);
                        _jsonText = null;  // 清理保存的文本
                    }
                    break;
            }
        }
#else
        // 运行时版本的单例实现（简化版）
        // 使用条件运算符实现懒加载单例模式
        public static T instance => s_Instance ? s_Instance : s_Instance = CreateInstance<T>();
#endif

        /// <summary>
        /// This function is called when the object becomes enabled and active.
        /// </summary>
        protected virtual void OnEnable()
        {
#if UNITY_EDITOR
            // 编辑器模式下的实例验证逻辑
            // 检查当前实例是否应该成为默认设置实例
            var isDefaultSettings = !s_Instance || s_Instance == this || GetDefaultSettings(typeof(T)) == this;
            if (!isDefaultSettings)
            {
                // 如果不是默认设置实例，立即销毁当前实例
                // 确保每种类型只有一个活跃的默认实例
                DestroyImmediate(this, true);
                return;
            }

            // 注册播放模式状态变化事件监听
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif

            // 设置单例实例（如果尚未设置）
            if (s_Instance) return;
            s_Instance = this as T;
        }

        /// <summary>
        /// This function is called when the behaviour becomes disabled.
        /// </summary>
        protected virtual void OnDisable()
        {
#if UNITY_EDITOR
            // 取消注册播放模式状态变化事件监听
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
            // 如果当前实例是单例实例，清理单例引用
            if (s_Instance != this) return;

            s_Instance = null;
        }

#if UNITY_EDITOR
        // 项目设置提供者内部类
        // 用于在Unity编辑器的Project Settings窗口中显示设置界面
        protected sealed class PreloadedProjectSettingsProvider : SettingsProvider
        {
            // 编辑器实例和目标设置实例
            private Editor _editor;
            private PreloadedProjectSettings<T> _target;

            // 构造函数，设置提供者的路径和作用域
            public PreloadedProjectSettingsProvider(string path) : base(path, SettingsScope.Project)
            {
            }

            // 绘制设置界面的GUI方法
            public override void OnGUI(string searchContext)
            {
                // 如果目标实例不存在或已失效，重新初始化
                if (!_target)
                {
                    // 清理旧的编辑器实例
                    if (_editor)
                    {
                        DestroyImmediate(_editor);
                        _editor = null;
                    }

                    // 获取单例实例作为目标
                    _target = instance;
                    // 为目标实例创建编辑器
                    _editor = Editor.CreateEditor(_target);
                }

                // 使用创建的编辑器绘制Inspector界面
                _editor.OnInspectorGUI();
            }
        }
#endif
    }

}
