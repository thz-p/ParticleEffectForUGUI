#if UNITY_2021_3_0 || UNITY_2021_3_1 || UNITY_2021_3_2 || UNITY_2021_3_3 || UNITY_2021_3_4 || UNITY_2021_3_5 || UNITY_2021_3_6 || UNITY_2021_3_7 || UNITY_2021_3_8 || UNITY_2021_3_9
#elif UNITY_2021_3_10 || UNITY_2021_3_11 || UNITY_2021_3_12 || UNITY_2021_3_13 || UNITY_2021_3_14 || UNITY_2021_3_15 || UNITY_2021_3_16 || UNITY_2021_3_17 || UNITY_2021_3_18 || UNITY_2021_3_19
#elif UNITY_2021_3_20 || UNITY_2021_3_21 || UNITY_2021_3_22 || UNITY_2021_3_23 || UNITY_2021_3_24 || UNITY_2021_3_25 || UNITY_2021_3_26 || UNITY_2021_3_27 || UNITY_2021_3_28 || UNITY_2021_3_29
#elif UNITY_2021_3_30 || UNITY_2021_3_31 || UNITY_2021_3_32 || UNITY_2021_3_33
#elif UNITY_2022_2_0 || UNITY_2022_2_1 || UNITY_2022_2_2 || UNITY_2022_2_3 || UNITY_2022_2_4 || UNITY_2022_2_5 || UNITY_2022_2_6 || UNITY_2022_2_7 || UNITY_2022_2_8 || UNITY_2022_2_9
#elif UNITY_2022_2_10 || UNITY_2022_2_11 || UNITY_2022_2_12 || UNITY_2022_2_13 || UNITY_2022_2_14
#elif UNITY_2021_3 || UNITY_2022_2 || UNITY_2022_3 || UNITY_2023_2_OR_NEWER
#define CANVAS_SUPPORT_ALWAYS_GAMMA
#endif

using UnityEngine;
using UnityEngine.Profiling;
#if UNITY_MODULE_VR
using UnityEngine.XR;
#endif

namespace Coffee.UIParticleInternal
{
    internal static class CanvasExtensions
    {
        /// <summary>
        /// 判断是否应该在Shader中执行Gamma空间到Linear空间的转换
        /// 该方法用于确定UI粒子效果在渲染时是否需要进行颜色空间转换
        /// </summary>
        /// <param name="canvas">要检查的Canvas组件</param>
        /// <returns>如果需要在Shader中执行Gamma到Linear的转换，则返回true；否则返回false</returns>
        public static bool ShouldGammaToLinearInShader(this Canvas canvas)
        {
            // 只有当当前颜色空间为线性空间时才需要考虑转换
            // 在支持vertexColorAlwaysGammaSpace属性的Unity版本中，还需检查该属性是否为true
            return QualitySettings.activeColorSpace == ColorSpace.Linear &&
#if CANVAS_SUPPORT_ALWAYS_GAMMA
                   canvas.vertexColorAlwaysGammaSpace; // Unity 2021.3+等版本支持的属性，指示顶点颜色始终在Gamma空间中
#else
                   false; // 不支持vertexColorAlwaysGammaSpace属性的旧版本Unity始终返回false
#endif
        }

        /// <summary>
        /// 判断是否应该在网格生成时执行Gamma空间到Linear空间的转换
        /// 该方法与ShouldGammaToLinearInShader互补，用于确定UI粒子效果在何时进行颜色空间转换
        /// </summary>
        /// <param name="canvas">要检查的Canvas组件</param>
        /// <returns>如果需要在网格生成时执行Gamma到Linear的转换，则返回true；否则返回false</returns>
        public static bool ShouldGammaToLinearInMesh(this Canvas canvas)
        {
            // 只有当当前颜色空间为线性空间时才需要考虑转换
            // 在支持vertexColorAlwaysGammaSpace属性的Unity版本中，当该属性为false时需要在网格生成时转换
            return QualitySettings.activeColorSpace == ColorSpace.Linear &&
#if CANVAS_SUPPORT_ALWAYS_GAMMA
                   !canvas.vertexColorAlwaysGammaSpace; // 当顶点颜色不总是在Gamma空间时，需要在网格生成时转换
#else
                   true; // 不支持vertexColorAlwaysGammaSpace属性的旧版本Unity始终需要在网格生成时转换
#endif
        }

        /// <summary>
        /// 检查Canvas是否应该以立体方式渲染（用于VR/AR环境）
        /// 该方法用于确定UI粒子效果是否需要为VR/AR环境进行特殊处理
        /// </summary>
        /// <param name="canvas">要检查的Canvas组件</param>
        /// <returns>如果Canvas需要以立体方式渲染，则返回true；否则返回false</returns>
        public static bool IsStereoCanvas(this Canvas canvas)
        {
#if UNITY_MODULE_VR
            // 尝试从帧缓存中获取结果，避免重复计算
            if (FrameCache.TryGet<bool>(canvas, nameof(IsStereoCanvas), out var stereo)) return stereo;

            // 判断是否为立体Canvas的条件：
            // 1. Canvas不为空
            // 2. 渲染模式不是ScreenSpaceOverlay（该模式不支持立体渲染）
            // 3. 有设置世界相机
            // 4. XR设置已启用
            // 5. 已加载XR设备
            stereo =
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null
                && XRSettings.enabled && !string.IsNullOrEmpty(XRSettings.loadedDeviceName);
            
            // 将结果存入帧缓存，供后续快速访问
            FrameCache.Set(canvas, nameof(IsStereoCanvas), stereo);
            return stereo;
#else
            // 没有VR模块时，始终返回false
            return false;
#endif
        }

        /// <summary>
        /// 获取Canvas的视图投影矩阵（单眼模式）
        /// 这个方法是一个便捷重载，默认使用单眼渲染模式获取视图投影矩阵
        /// 视图投影矩阵用于将Canvas中的UI元素正确投影到屏幕上
        /// </summary>
        /// <param name="canvas">要获取矩阵的Canvas组件</param>
        /// <param name="vpMatrix">输出参数：返回计算得到的视图投影矩阵</param>
        public static void GetViewProjectionMatrix(this Canvas canvas, out Matrix4x4 vpMatrix)
        {
            // 调用带有眼睛参数的重载版本，默认使用单眼模式
            // Camera.MonoOrStereoscopicEye.Mono表示非VR/AR环境下的标准渲染模式
            canvas.GetViewProjectionMatrix(Camera.MonoOrStereoscopicEye.Mono, out vpMatrix);
        }


        /// <summary>
        /// 获取Canvas的视图投影矩阵（支持VR/AR立体渲染）
        /// 这个重载版本允许指定眼睛参数，用于VR/AR环境下的立体渲染场景
        /// 视图投影矩阵将Canvas的局部空间坐标转换为屏幕空间坐标，是UI渲染的关键环节
        /// </summary>
        /// <param name="canvas">要获取矩阵的Canvas组件</param>
        /// <param name="eye">指定用于渲染的眼睛（单眼、左眼或右眼），在VR/AR场景中用于区分左右眼视图</param>
        /// <param name="vpMatrix">输出参数：返回计算得到的视图投影矩阵</param>
        public static void GetViewProjectionMatrix(this Canvas canvas, Camera.MonoOrStereoscopicEye eye,
            out Matrix4x4 vpMatrix)
        {
            // 尝试从帧缓存中获取已经计算过的视图投影矩阵，避免重复计算提高性能
            if (FrameCache.TryGet(canvas, nameof(GetViewProjectionMatrix), out vpMatrix)) return;

            // 获取视图矩阵和投影矩阵
            canvas.GetViewProjectionMatrix(eye, out var viewMatrix, out var projectionMatrix);
            // 计算视图投影矩阵：视图矩阵 × 投影矩阵
            // 这个矩阵用于将物体从世界空间转换到裁剪空间，是渲染管线中的关键步骤
            vpMatrix = viewMatrix * projectionMatrix;
            // 将计算结果存入帧缓存，供后续快速访问
            FrameCache.Set(canvas, nameof(GetViewProjectionMatrix), vpMatrix);
        }

        /// <summary>
        /// 获取Canvas的视图矩阵和投影矩阵（单眼模式）
        /// 这个便捷重载方法同时返回视图矩阵和投影矩阵，默认使用单眼渲染模式
        /// 分别获取这两个矩阵而不是直接获取组合后的视图投影矩阵，适用于需要对这两个矩阵单独进行操作的场景
        /// </summary>
        /// <param name="canvas">要获取矩阵的Canvas组件</param>
        /// <param name="vMatrix">输出参数：返回计算得到的视图矩阵</param>
        /// <param name="pMatrix">输出参数：返回计算得到的投影矩阵</param>
        public static void GetViewProjectionMatrix(this Canvas canvas, out Matrix4x4 vMatrix, out Matrix4x4 pMatrix)
        {
            // 调用带有眼睛参数的重载版本，默认使用单眼模式
            // Camera.MonoOrStereoscopicEye.Mono表示标准非VR渲染模式
            canvas.GetViewProjectionMatrix(Camera.MonoOrStereoscopicEye.Mono, out vMatrix, out pMatrix);
        }

        /// <summary>
        /// 获取Canvas的视图矩阵和投影矩阵（支持VR/AR立体渲染）
        /// 这是整个Canvas扩展方法中最核心的实现，同时支持标准渲染和VR/AR立体渲染场景
        /// 视图矩阵和投影矩阵是UI粒子系统正确渲染的基础，用于计算粒子在屏幕上的最终位置
        /// </summary>
        /// <param name="canvas">要获取矩阵的Canvas组件</param>
        /// <param name="eye">指定用于渲染的眼睛（单眼、左眼或右眼），在VR/AR场景中区分左右眼视图</param>
        /// <param name="vMatrix">输出参数：返回计算得到的视图矩阵，将世界坐标转换为相机本地坐标</param>
        /// <param name="pMatrix">输出参数：返回计算得到的投影矩阵，将相机本地坐标转换为裁剪空间坐标</param>
        public static void GetViewProjectionMatrix(this Canvas canvas, Camera.MonoOrStereoscopicEye eye,
            out Matrix4x4 vMatrix, out Matrix4x4 pMatrix)
        {
            // 尝试从帧缓存中获取已经计算过的视图矩阵和投影矩阵
            // 使用眼睛类型作为缓存键的一部分，以区分不同眼睛的矩阵
            // 缓存机制避免了每帧重复计算，显著提高性能
            if (FrameCache.TryGet(canvas, "GetViewMatrix", (int)eye, out vMatrix) &&
                FrameCache.TryGet(canvas, "GetProjectionMatrix", (int)eye, out pMatrix))
            {
                return;
            }

            // 开始性能分析采样，用于监控此方法的性能开销
            Profiler.BeginSample("(COF)[CanvasExt] GetViewProjectionMatrix");
            
            // 获取根Canvas和关联的世界相机
            var rootCanvas = canvas.rootCanvas;
            var cam = rootCanvas.worldCamera;
            
            // 根据Canvas的渲染模式和是否存在世界相机，采用不同的矩阵计算策略
            if (rootCanvas && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && cam)
            {
                // 策略1：使用实际相机进行计算（适用于WorldSpace和ScreenSpaceCamera模式）
                if (eye == Camera.MonoOrStereoscopicEye.Mono)
                {
                    // 标准单眼渲染模式
                    // 使用相机的世界到相机变换矩阵作为视图矩阵
                    vMatrix = cam.worldToCameraMatrix;
                    // 获取相机的投影矩阵并转换为GPU兼容格式
                    pMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
                }
                else
                {
                    // VR/AR立体渲染模式
                    // 获取指定眼睛的立体投影矩阵和视图矩阵
                    pMatrix = cam.GetStereoProjectionMatrix((Camera.StereoscopicEye)eye);
                    vMatrix = cam.GetStereoViewMatrix((Camera.StereoscopicEye)eye);
                    // 将投影矩阵转换为GPU兼容格式
                    pMatrix = GL.GetGPUProjectionMatrix(pMatrix, false);
                }
            }
            else
            {
                // 策略2：手动构建矩阵（适用于ScreenSpaceOverlay模式或没有世界相机的情况）
                // 这种情况下，Canvas直接绘制在屏幕上，需要模拟相机和投影
                var pos = rootCanvas.transform.position;
                
                // 构建视图矩阵：平移Canvas位置到原点，Z轴翻转以符合Unity UI惯例
                vMatrix = Matrix4x4.TRS(
                    new Vector3(-pos.x, -pos.y, -1000),
                    Quaternion.identity,
                    new Vector3(1, 1, -1f));
                
                // 构建投影矩阵：设置适当的缩放因子，使UI元素正确映射到屏幕
                pMatrix = Matrix4x4.TRS(
                    new Vector3(0, 0, -1),
                    Quaternion.identity,
                    new Vector3(1 / pos.x, 1 / pos.y, -2 / 10000f));
            }

            // 将计算结果存入帧缓存，供后续同一帧内的调用快速访问
            FrameCache.Set(canvas, "GetViewMatrix", (int)eye, vMatrix);
            FrameCache.Set(canvas, "GetProjectionMatrix", (int)eye, pMatrix);

            // 结束性能分析采样
            Profiler.EndSample();
        }

    }
}
