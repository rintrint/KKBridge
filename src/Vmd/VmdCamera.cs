using BepInEx.Logging;
using Studio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace KKBridge.Vmd
{
    /// <summary>
    /// VMD 相機幀資料結構
    /// </summary>
    public class VmdCameraFrame
    {
        public uint FrameNumber { get; set; }
        public float Distance { get; set; }       // 與目標點的距離
        public Vector3 TargetPosition { get; set; } // 目標點的世界坐標 (MMD坐標系)
        public Vector3 Rotation { get; set; }     // 相機旋轉 (歐拉角，度數)
        public int Fov { get; set; }              // 視野 (FOV)
        public byte[] Interpolation { get; set; } // 補間曲線 (24 bytes)

        public VmdCameraFrame()
        {
            // 預設線性補間
            Interpolation = new byte[24];
            for (int i = 0; i < 24; i++) Interpolation[i] = 20;
        }
    }

    /// <summary>
    /// 處理相機數據的錄製與導出
    /// </summary>
    public class VmdCameraProcessor
    {
        private readonly ManualLogSource _logger;
        private object _cameraDataObj;
        private FieldInfo _posField;
        private const float MmdScale = 12.5f;

        public VmdCameraProcessor(ManualLogSource logger)
        {
            _logger = logger;
            InitializeReflection();
        }

        /// <summary>
        /// 初始化反射，獲取 Studio 內部的目標點資料欄位
        /// </summary>
        private void InitializeReflection()
        {
            var studio = Singleton<Studio.Studio>.Instance;
            if (studio != null && studio.cameraCtrl != null)
            {
                try
                {
                    // 1. 取得 protected cameraData
                    FieldInfo dataField = typeof(Studio.CameraControl).GetField("cameraData", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
                    if (dataField != null)
                    {
                        _cameraDataObj = dataField.GetValue(studio.cameraCtrl);

                        if (_cameraDataObj != null)
                        {
                            // 2. 取得 cameraData 內部的 'pos' (目標點) 欄位
                            // 直接取 Vector3 型別，避免數值轉型錯誤
                            _posField = _cameraDataObj.GetType().GetField("pos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"[VmdCamera] Reflection init failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 處理單一影格
        /// </summary>
        public VmdCameraFrame ProcessFrame(uint frameNumber)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return null;

            var frame = new VmdCameraFrame
            {
                FrameNumber = frameNumber,
                Fov = (int)mainCam.fieldOfView
            };

            // --- 1. 計算目標點與距離 ---
            Vector3 camPosUnity = mainCam.transform.position;
            Vector3 targetPosUnity;

            // 嘗試從反射獲取 Studio 的 Target 位置
            if (_posField != null && _cameraDataObj != null)
            {
                try
                {
                    targetPosUnity = (Vector3)_posField.GetValue(_cameraDataObj);
                }
                catch
                {
                    // 反射取值失敗，退回計算：假設 Target 在相機前方 5m
                    targetPosUnity = camPosUnity + mainCam.transform.forward * 5.0f;
                }
            }
            else
            {
                // 無 Studio 環境，退回計算
                targetPosUnity = camPosUnity + mainCam.transform.forward * 5.0f;
            }

            // 計算距離 (解決之前的轉型錯誤，手動算最穩)
            float dist = Vector3.Distance(camPosUnity, targetPosUnity);
            frame.Distance = -dist * MmdScale; // MMD 相機距離通常為負值 (在目標後方)

            // --- 2. 坐標轉換 (Unity -> MMD) ---
            // 位置：X軸反轉 (左右)，Z軸反轉 (前後)
            frame.TargetPosition = new Vector3(-targetPosUnity.x, targetPosUnity.y, -targetPosUnity.z) * MmdScale;

            // --- 3. 旋轉轉換 ---
            // Unity (左手系) -> MMD (右手系 + 鏡像)
            Vector3 euler = mainCam.transform.eulerAngles;

            // 正規化角度 (-180 ~ 180)
            float rx = NormalizeAngle(euler.x);
            float ry = NormalizeAngle(euler.y);
            float rz = NormalizeAngle(euler.z);

            // MMD 相機轉換公式：
            // X (Pitch) = -Unity X (修正仰角/俯角反轉)
            // Y (Yaw)   = -Unity Y + 180 (因為相機通常看向背面)
            // Z (Roll)  = Unity Z (Roll 通常不需要反轉，若發現歪頭方向反了再加負號)
            frame.Rotation = new Vector3(-rx, -ry + 180f, rz);

            return frame;
        }

        private float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }

        /// <summary>
        /// 導出相機 VMD 檔案
        /// </summary>
        public static void Export(List<VmdCameraFrame> frames, string filePath)
        {
            if (frames == null || frames.Count == 0) return;

            using (var fs = new FileStream(filePath, FileMode.Create))
            using (var writer = new BinaryWriter(fs))
            {
                // Header (30 bytes)
                writer.Write(Encoding.ASCII.GetBytes("Vocaloid Motion Data 0002\0\0\0\0\0"));

                // Model Name (20 bytes) - 相機 VMD 此處通常為空或特定字串
                byte[] nameBytes = new byte[20];
                Encoding.GetEncoding(932).GetBytes("カメラ・照明").CopyTo(nameBytes, 0); // Shift-JIS
                writer.Write(nameBytes);

                // Bone Frames (0)
                writer.Write(0);
                // Morph Frames (0)
                writer.Write(0);

                // Camera Frames Count
                writer.Write(frames.Count);

                // Write Frames
                foreach (var f in frames)
                {
                    writer.Write(f.FrameNumber);
                    writer.Write(f.Distance);
                    writer.Write(f.TargetPosition.x);
                    writer.Write(f.TargetPosition.y);
                    writer.Write(f.TargetPosition.z);

                    // VMD 檔案中，相機旋轉必須存為「弧度 (Radians)」
                    writer.Write(f.Rotation.x * Mathf.Deg2Rad);
                    writer.Write(f.Rotation.y * Mathf.Deg2Rad);
                    writer.Write(f.Rotation.z * Mathf.Deg2Rad);

                    writer.Write(f.Interpolation); // 24 bytes
                    writer.Write(f.Fov);
                    writer.Write((byte)0); // Perspective (0: On)
                }

                // Light (0)
                writer.Write(0);
                // Shadow (0)
                writer.Write(0);
                // IK (0)
                writer.Write(0);
            }
        }
    }
}
