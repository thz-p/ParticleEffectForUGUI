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


        protected static void SetDefaultSettings(PreloadedProjectSettings asset)
        {
            if (!asset) return;

            var type = asset.GetType();
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)))
            {
                if (!AssetDatabase.IsValidFolder("Assets/ProjectSettings"))
                {
                    AssetDatabase.CreateFolder("Assets", "ProjectSettings");
                }

                var assetPath = $"Assets/ProjectSettings/{GetDefaultName(type, false)}.asset";
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                if (!File.Exists(assetPath))
                {
                    AssetDatabase.CreateAsset(asset, assetPath);
                    asset.OnCreateAsset();
                }
            }

            var preloadedAssets = PlayerSettings.GetPreloadedAssets();
            var projectSettings = GetPreloadedSettings(type);
            PlayerSettings.SetPreloadedAssets(preloadedAssets
                .Where(x => x)
                .Except(projectSettings.Except(new[] { asset }))
                .Append(asset)
                .Distinct()
                .ToArray());

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

    public abstract class PreloadedProjectSettings<T> : PreloadedProjectSettings
        where T : PreloadedProjectSettings<T>
    {
        private static T s_Instance;

#if UNITY_EDITOR
        private string _jsonText;

        public static bool hasInstance => s_Instance;

        public static T instance
        {
            get
            {
                if (s_Instance) return s_Instance;

                s_Instance = GetDefaultSettings(typeof(T)) as T;
                if (s_Instance) return s_Instance;

                s_Instance = CreateInstance<T>();
                if (!s_Instance)
                {
                    s_Instance = null;
                    return s_Instance;
                }

                SetDefaultSettings(s_Instance);
                return s_Instance;
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    _jsonText = EditorJsonUtility.ToJson(this);
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    if (_jsonText != null)
                    {
                        EditorJsonUtility.FromJsonOverwrite(_jsonText, this);
                        _jsonText = null;
                    }

                    break;
            }
        }
#else
        public static T instance => s_Instance ? s_Instance : s_Instance = CreateInstance<T>();
#endif

        /// <summary>
        /// This function is called when the object becomes enabled and active.
        /// </summary>
        protected virtual void OnEnable()
        {
#if UNITY_EDITOR
            var isDefaultSettings = !s_Instance || s_Instance == this || GetDefaultSettings(typeof(T)) == this;
            if (!isDefaultSettings)
            {
                DestroyImmediate(this, true);
                return;
            }

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif

            if (s_Instance) return;
            s_Instance = this as T;
        }

        /// <summary>
        /// This function is called when the behaviour becomes disabled.
        /// </summary>
        protected virtual void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
            if (s_Instance != this) return;

            s_Instance = null;
        }

#if UNITY_EDITOR
        protected sealed class PreloadedProjectSettingsProvider : SettingsProvider
        {
            private Editor _editor;
            private PreloadedProjectSettings<T> _target;

            public PreloadedProjectSettingsProvider(string path) : base(path, SettingsScope.Project)
            {
            }

            public override void OnGUI(string searchContext)
            {
                if (!_target)
                {
                    if (_editor)
                    {
                        DestroyImmediate(_editor);
                        _editor = null;
                    }

                    _target = instance;
                    _editor = Editor.CreateEditor(_target);
                }

                _editor.OnInspectorGUI();
            }
        }
#endif
    }
}
