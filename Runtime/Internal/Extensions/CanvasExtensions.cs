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
        /// Gets the view-projection matrix for a Canvas.
        /// </summary>
        public static void GetViewProjectionMatrix(this Canvas canvas, out Matrix4x4 vpMatrix)
        {
            canvas.GetViewProjectionMatrix(Camera.MonoOrStereoscopicEye.Mono, out vpMatrix);
        }

        /// <summary>
        /// Gets the view-projection matrix for a Canvas.
        /// </summary>
        public static void GetViewProjectionMatrix(this Canvas canvas, Camera.MonoOrStereoscopicEye eye,
            out Matrix4x4 vpMatrix)
        {
            if (FrameCache.TryGet(canvas, nameof(GetViewProjectionMatrix), out vpMatrix)) return;

            canvas.GetViewProjectionMatrix(eye, out var viewMatrix, out var projectionMatrix);
            vpMatrix = viewMatrix * projectionMatrix;
            FrameCache.Set(canvas, nameof(GetViewProjectionMatrix), vpMatrix);
        }

        /// <summary>
        /// Gets the view and projection matrices for a Canvas.
        /// </summary>
        public static void GetViewProjectionMatrix(this Canvas canvas, out Matrix4x4 vMatrix, out Matrix4x4 pMatrix)
        {
            canvas.GetViewProjectionMatrix(Camera.MonoOrStereoscopicEye.Mono, out vMatrix, out pMatrix);
        }

        /// <summary>
        /// Gets the view and projection matrices for a Canvas.
        /// </summary>
        public static void GetViewProjectionMatrix(this Canvas canvas, Camera.MonoOrStereoscopicEye eye,
            out Matrix4x4 vMatrix, out Matrix4x4 pMatrix)
        {
            if (FrameCache.TryGet(canvas, "GetViewMatrix", (int)eye, out vMatrix) &&
                FrameCache.TryGet(canvas, "GetProjectionMatrix", (int)eye, out pMatrix))
            {
                return;
            }

            // Get view and projection matrices.
            Profiler.BeginSample("(COF)[CanvasExt] GetViewProjectionMatrix");
            var rootCanvas = canvas.rootCanvas;
            var cam = rootCanvas.worldCamera;
            if (rootCanvas && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && cam)
            {
                if (eye == Camera.MonoOrStereoscopicEye.Mono)
                {
                    vMatrix = cam.worldToCameraMatrix;
                    pMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
                }
                else
                {
                    pMatrix = cam.GetStereoProjectionMatrix((Camera.StereoscopicEye)eye);
                    vMatrix = cam.GetStereoViewMatrix((Camera.StereoscopicEye)eye);
                    pMatrix = GL.GetGPUProjectionMatrix(pMatrix, false);
                }
            }
            else
            {
                var pos = rootCanvas.transform.position;
                vMatrix = Matrix4x4.TRS(
                    new Vector3(-pos.x, -pos.y, -1000),
                    Quaternion.identity,
                    new Vector3(1, 1, -1f));
                pMatrix = Matrix4x4.TRS(
                    new Vector3(0, 0, -1),
                    Quaternion.identity,
                    new Vector3(1 / pos.x, 1 / pos.y, -2 / 10000f));
            }

            FrameCache.Set(canvas, "GetViewMatrix", (int)eye, vMatrix);
            FrameCache.Set(canvas, "GetProjectionMatrix", (int)eye, pMatrix);

            Profiler.EndSample();
        }
    }
}
