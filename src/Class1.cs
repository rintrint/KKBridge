using BepInEx;
using BepInEx.Logging;
using RootMotion.FinalIK;
using Studio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public static class QuaternionExtensions
{
    /// <summary>
    /// 使用方法:
    /// Log.LogInfo(new Quaternion(1, 2, 3, 4).sqrMagnitude());
    /// </summary>
    public static float sqrMagnitude(this Quaternion q)
    {
        return q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
    }
    /// <summary>
    /// 使用方法:
    /// Log.LogInfo(QuaternionExtensions.SqrMagnitude(new Quaternion(1, 2, 3, 4)));
    /// </summary>
    public static float SqrMagnitude(Quaternion q)
    {
        return q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
    }
    /// <summary>
    /// 使用方法:
    /// Log.LogInfo(new Quaternion(1, 2, 3, 4).magnitude());
    /// </summary>
    public static float magnitude(this Quaternion q)
    {
        return Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
    }
    /// <summary>
    /// 使用方法:
    /// Log.LogInfo(QuaternionExtensions.Magnitude(new Quaternion(1, 2, 3, 4)));
    /// </summary>
    public static float Magnitude(Quaternion q)
    {
        return Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
    }
    /// <summary>
    /// 使用方法:
    /// Log.LogInfo(new Quaternion(1, 2, 3, 4).normalized());
    /// </summary>
    public static Quaternion normalized(this Quaternion q)
    {
        float magnitude = Magnitude(q);

        if (magnitude > 1E-05f)
        {
            return new Quaternion(q.x / magnitude, q.y / magnitude, q.z / magnitude, q.w / magnitude);
        }
        return Quaternion.identity;
    }
    /// <summary>
    /// 使用方法:
    /// Log.LogInfo(QuaternionExtensions.Normalize(new Quaternion(1, 2, 3, 4)));
    /// </summary>
    public static Quaternion Normalize(Quaternion q)
    {
        float magnitude = Magnitude(q);

        if (magnitude > 1E-05f)
        {
            return new Quaternion(q.x / magnitude, q.y / magnitude, q.z / magnitude, q.w / magnitude);
        }
        return Quaternion.identity;
    }
    /// <summary>
    /// 使用方法:
    /// Quaternion q = new Quaternion(1, 2, 3, 4);
    /// q.Normalize();
    /// Log.LogInfo(q);
    /// </summary>
    public static void Normalize(this ref Quaternion q)
    {
        float magnitude = Magnitude(q);

        if (magnitude > 1E-05f)
        {
            q.Set(q.x / magnitude, q.y / magnitude, q.z / magnitude, q.w / magnitude);
        }
        else
        {
            q = Quaternion.identity;
        }
    }
    /// <summary>
    /// 使用方法:
    /// Log.LogInfo(new Quaternion(1, 2, 3, 4).conjugated());
    /// </summary>
    public static Quaternion conjugated(this Quaternion q)
    {
        return new Quaternion(-q.x, -q.y, -q.z, q.w);
    }
    /// <summary>
    /// 使用方法:
    /// Log.LogInfo(QuaternionExtensions.Conjugate(new Quaternion(1, 2, 3, 4)));
    /// </summary>
    public static Quaternion Conjugate(Quaternion q)
    {
        return new Quaternion(-q.x, -q.y, -q.z, q.w);
    }
    /// <summary>
    /// 使用方法:
    /// Quaternion q = new Quaternion(1, 2, 3, 4);
    /// q.Conjugate();
    /// Log.LogInfo(q);
    /// </summary>
    public static void Conjugate(this ref Quaternion q)
    {
        q.Set(-q.x, -q.y, -q.z, q.w);
    }
}

#region VMD 工具類別 (VMD Helper Classes)

/// <summary>
/// 骨骼名稱映射器，將 Koikatsu 的骨骼名稱翻譯為 MMD 標準名稱，並儲存其旋轉資訊
/// </summary>
public static class BoneMapper
{
    /// <summary>
    /// 儲存MMD骨骼名稱和其旋轉資訊
    /// </summary>
    public class BoneMappingInfo
    {
        public string MmdName { get; }
        public Quaternion RestPoseCorrection { get; }
        public Quaternion CoordinateConversion { get; }

        public BoneMappingInfo(string mmdName, Quaternion restPoseCorrection, Quaternion coordinateConversion)
        {
            MmdName = mmdName;
            RestPoseCorrection = restPoseCorrection;
            CoordinateConversion = coordinateConversion;
        }

        public BoneMappingInfo(string mmdName, Quaternion restPoseCorrection)
        {
            MmdName = mmdName;
            RestPoseCorrection = restPoseCorrection;
            CoordinateConversion = Quaternion.identity;
        }

        public BoneMappingInfo(string mmdName)
        {
            MmdName = mmdName;
            RestPoseCorrection = Quaternion.identity;
            CoordinateConversion = Quaternion.identity;
        }
    }

    private static readonly Dictionary<string, BoneMappingInfo> _kkToMmdMap = new Dictionary<string, BoneMappingInfo>
    {
        { "chaF_",              new BoneMappingInfo("全ての親", new Quaternion( 0.000f, 0.000f, 0.000f, 1.000f), new Quaternion( 0.000f, 0.000f, 1.000f, 0.000f)) },
        { "cf_j_hips",          new BoneMappingInfo("センター", new Quaternion( 0.000f, 0.000f, 0.000f, 1.000f), new Quaternion( 0.000f,-1.000f, 0.000f, 0.000f)) },
        { "EyeTargetL",         new BoneMappingInfo("左目",     new Quaternion( 0.000f, 0.000f, 0.000f, 1.000f), new Quaternion( 0.561f,-0.578f,-0.417f, 0.421f)) },
        { "EyeTargetR",         new BoneMappingInfo("右目",     new Quaternion( 0.000f, 0.000f, 0.000f, 1.000f), new Quaternion(-0.417f, 0.421f, 0.561f,-0.578f)) },
        { "cf_j_neck",          new BoneMappingInfo("首",       new Quaternion(-0.077f, 0.000f, 0.000f, 0.997f), new Quaternion( 0.000f, 0.005f, 1.000f, 0.000f)) },
        { "cf_j_head",          new BoneMappingInfo("頭",       new Quaternion( 0.005f, 0.000f, 0.000f, 1.000f), new Quaternion( 0.000f, 0.000f, 1.000f, 0.000f)) },
        { "cf_j_spine01",       new BoneMappingInfo("上半身",   new Quaternion( 0.022f, 0.000f, 0.000f, 1.000f), new Quaternion( 0.000f,-0.022f, 1.000f, 0.000f)) },
        { "cf_j_spine02",       new BoneMappingInfo("上半身2",  new Quaternion( 0.050f, 0.000f, 0.000f, 0.999f), new Quaternion( 0.000f,-0.072f, 0.997f, 0.000f)) },
        { "cf_j_waist01",       new BoneMappingInfo("下半身",   new Quaternion(-0.004f, 0.000f, 0.000f, 1.000f), new Quaternion( 0.000f,-1.000f, 0.004f, 0.000f)) },
        { "cf_j_shoulder_L",    new BoneMappingInfo("左肩",     new Quaternion(-0.124f,-0.072f, 0.016f, 0.990f), new Quaternion( 0.437f,-0.556f,-0.430f,-0.562f)) },
        { "cf_j_shoulder_R",    new BoneMappingInfo("右肩",     new Quaternion( 0.124f, 0.072f, 0.016f, 0.989f), new Quaternion(-0.430f,-0.562f, 0.437f,-0.556f)) },
        { "cf_j_arm00_L",       new BoneMappingInfo("左腕",     new Quaternion(-0.200f,-0.003f,-0.001f, 0.980f), new Quaternion( 0.317f,-0.632f,-0.309f,-0.636f)) },
        { "cf_j_arm00_R",       new BoneMappingInfo("右腕",     new Quaternion( 0.200f, 0.003f,-0.001f, 0.980f), new Quaternion(-0.309f,-0.636f, 0.317f,-0.632f)) },
        { "cf_j_forearm01_L",   new BoneMappingInfo("左ひじ",   new Quaternion( 0.002f, 0.000f,-0.015f, 1.000f), new Quaternion( 0.309f,-0.636f,-0.320f,-0.631f)) },
        { "cf_j_forearm01_R",   new BoneMappingInfo("右ひじ",   new Quaternion(-0.002f, 0.000f,-0.015f, 1.000f), new Quaternion(-0.320f,-0.631f, 0.309f,-0.636f)) },
        { "cf_j_hand_L",        new BoneMappingInfo("左手首",   new Quaternion(-0.020f, 0.000f, 0.031f, 0.999f), new Quaternion( 0.316f,-0.633f,-0.288f,-0.646f)) },
        { "cf_j_hand_R",        new BoneMappingInfo("右手首",   new Quaternion( 0.020f, 0.000f, 0.031f, 0.999f), new Quaternion(-0.288f,-0.646f, 0.316f,-0.633f)) },
        { "cf_j_thigh00_L",     new BoneMappingInfo("左足",     new Quaternion(-0.027f,-0.007f, 0.007f, 1.000f), new Quaternion( 0.007f,-0.999f, 0.032f, 0.007f)) },
        { "cf_j_thigh00_R",     new BoneMappingInfo("右足",     new Quaternion(-0.027f, 0.007f,-0.007f, 1.000f), new Quaternion(-0.007f,-0.999f, 0.032f,-0.007f)) },
        { "cf_j_leg01_L",       new BoneMappingInfo("左ひざ",   new Quaternion(-0.031f, 0.004f,-0.004f, 1.000f), new Quaternion( 0.003f,-0.998f, 0.062f, 0.003f)) },
        { "cf_j_leg01_R",       new BoneMappingInfo("右ひざ",   new Quaternion(-0.031f,-0.004f, 0.004f, 1.000f), new Quaternion(-0.003f,-0.998f, 0.062f,-0.003f)) },
        { "cf_j_leg03_L",       new BoneMappingInfo("左足首",   new Quaternion(-0.094f, 0.004f, 0.003f, 0.996f), new Quaternion( 0.000f,-0.809f,-0.588f, 0.000f)) },
        { "cf_j_leg03_R",       new BoneMappingInfo("右足首",   new Quaternion(-0.094f,-0.004f,-0.003f, 0.996f), new Quaternion( 0.000f,-0.809f,-0.588f, 0.000f)) },
        { "cf_j_toes_L",        new BoneMappingInfo("左足先EX", new Quaternion(-0.156f, 0.000f, 0.000f, 0.988f), new Quaternion( 0.707f, 0.000f, 0.000f, 0.707f)) },
        { "cf_j_toes_R",        new BoneMappingInfo("右足先EX", new Quaternion(-0.156f, 0.000f, 0.000f, 0.988f), new Quaternion( 0.707f, 0.000f, 0.000f, 0.707f)) },
        { "cf_j_thumb01_L",     new BoneMappingInfo("左親指０", new Quaternion(-0.328f,-0.631f,-0.140f, 0.689f), new Quaternion(-0.012f,-0.707f,-0.411f,-0.575f)) },
        { "cf_j_thumb02_L",     new BoneMappingInfo("左親指１", new Quaternion( 0.045f,-0.026f, 0.175f, 0.983f), new Quaternion( 0.148f,-0.694f,-0.335f,-0.620f)) },
        { "cf_j_thumb03_L",     new BoneMappingInfo("左親指２", new Quaternion( 0.028f,-0.007f,-0.016f, 0.999f), new Quaternion( 0.157f,-0.690f,-0.363f,-0.606f)) },
        { "cf_j_index01_L",     new BoneMappingInfo("左人指１", new Quaternion(-0.006f, 0.000f,-0.021f, 1.000f), new Quaternion( 0.298f,-0.641f,-0.297f,-0.642f)) },
        { "cf_j_index02_L",     new BoneMappingInfo("左人指２", new Quaternion( 0.005f, 0.000f,-0.015f, 1.000f), new Quaternion( 0.292f,-0.644f,-0.310f,-0.636f)) },
        { "cf_j_index03_L",     new BoneMappingInfo("左人指３", new Quaternion( 0.003f,-0.001f, 0.032f, 0.999f), new Quaternion( 0.314f,-0.634f,-0.291f,-0.644f)) },
        { "cf_j_middle01_L",    new BoneMappingInfo("左中指１", new Quaternion(-0.018f,-0.001f,-0.011f, 1.000f), new Quaternion( 0.297f,-0.642f,-0.283f,-0.648f)) },
        { "cf_j_middle02_L",    new BoneMappingInfo("左中指２", new Quaternion( 0.006f, 0.000f,-0.015f, 1.000f), new Quaternion( 0.291f,-0.645f,-0.297f,-0.642f)) },
        { "cf_j_middle03_L",    new BoneMappingInfo("左中指３", new Quaternion( 0.037f, 0.001f,-0.037f, 0.999f), new Quaternion( 0.290f,-0.643f,-0.344f,-0.620f)) },
        { "cf_j_ring01_L",      new BoneMappingInfo("左薬指１", new Quaternion(-0.014f, 0.000f,-0.038f, 0.999f), new Quaternion( 0.282f,-0.648f,-0.303f,-0.639f)) },
        { "cf_j_ring02_L",      new BoneMappingInfo("左薬指２", new Quaternion( 0.007f, 0.000f, 0.004f, 1.000f), new Quaternion( 0.289f,-0.645f,-0.305f,-0.638f)) },
        { "cf_j_ring03_L",      new BoneMappingInfo("左薬指３", new Quaternion( 0.018f, 0.001f,-0.039f, 0.999f), new Quaternion( 0.275f,-0.650f,-0.341f,-0.621f)) },
        { "cf_j_little01_L",    new BoneMappingInfo("左小指１", new Quaternion(-0.001f,-0.001f,-0.045f, 0.999f), new Quaternion( 0.286f,-0.647f,-0.315f,-0.632f)) },
        { "cf_j_little02_L",    new BoneMappingInfo("左小指２", new Quaternion( 0.043f, 0.001f, 0.001f, 0.999f), new Quaternion( 0.313f,-0.632f,-0.342f,-0.620f)) },
        { "cf_j_little03_L",    new BoneMappingInfo("左小指３", new Quaternion(-0.073f, 0.004f,-0.030f, 0.997f), new Quaternion( 0.246f,-0.662f,-0.315f,-0.633f)) },
        { "cf_j_thumb01_R",     new BoneMappingInfo("右親指０", new Quaternion(-0.126f,-0.690f,-0.319f, 0.637f), new Quaternion(-0.399f,-0.582f,-0.002f,-0.708f)) },
        { "cf_j_thumb02_R",     new BoneMappingInfo("右親指１", new Quaternion(-0.055f, 0.028f, 0.124f, 0.990f), new Quaternion(-0.362f,-0.607f, 0.130f,-0.696f)) },
        { "cf_j_thumb03_R",     new BoneMappingInfo("右親指２", new Quaternion(-0.019f, 0.007f, 0.019f, 1.000f), new Quaternion(-0.363f,-0.606f, 0.157f,-0.690f)) },
        { "cf_j_index01_R",     new BoneMappingInfo("右人指１", new Quaternion(-0.021f,-1.000f,-0.006f, 0.000f), new Quaternion(-0.297f,-0.642f, 0.298f,-0.641f)) },
        { "cf_j_index02_R",     new BoneMappingInfo("右人指２", new Quaternion(-0.005f, 0.000f,-0.015f, 1.000f), new Quaternion(-0.310f,-0.636f, 0.292f,-0.644f)) },
        { "cf_j_index03_R",     new BoneMappingInfo("右人指３", new Quaternion(-0.003f, 0.001f, 0.032f, 0.999f), new Quaternion(-0.291f,-0.644f, 0.314f,-0.634f)) },
        { "cf_j_middle01_R",    new BoneMappingInfo("右中指１", new Quaternion(-0.011f,-1.000f,-0.018f, 0.001f), new Quaternion(-0.283f,-0.648f, 0.297f,-0.642f)) },
        { "cf_j_middle02_R",    new BoneMappingInfo("右中指２", new Quaternion(-0.006f, 0.000f,-0.015f, 1.000f), new Quaternion(-0.297f,-0.642f, 0.291f,-0.645f)) },
        { "cf_j_middle03_R",    new BoneMappingInfo("右中指３", new Quaternion(-0.037f,-0.001f,-0.037f, 0.999f), new Quaternion(-0.344f,-0.620f, 0.290f,-0.643f)) },
        { "cf_j_ring01_R",      new BoneMappingInfo("右薬指１", new Quaternion(-0.038f,-0.999f,-0.014f, 0.000f), new Quaternion(-0.303f,-0.639f, 0.282f,-0.648f)) },
        { "cf_j_ring02_R",      new BoneMappingInfo("右薬指２", new Quaternion(-0.007f, 0.000f, 0.004f, 1.000f), new Quaternion(-0.305f,-0.638f, 0.289f,-0.645f)) },
        { "cf_j_ring03_R",      new BoneMappingInfo("右薬指３", new Quaternion(-0.018f,-0.001f,-0.039f, 0.999f), new Quaternion(-0.341f,-0.621f, 0.275f,-0.650f)) },
        { "cf_j_little01_R",    new BoneMappingInfo("右小指１", new Quaternion(-0.045f,-0.999f,-0.001f, 0.001f), new Quaternion(-0.315f,-0.632f, 0.286f,-0.647f)) },
        { "cf_j_little02_R",    new BoneMappingInfo("右小指２", new Quaternion(-0.043f,-0.001f, 0.001f, 0.999f), new Quaternion(-0.342f,-0.620f, 0.313f,-0.632f)) },
        { "cf_j_little03_R",    new BoneMappingInfo("右小指３", new Quaternion( 0.073f,-0.004f,-0.030f, 0.997f), new Quaternion(-0.315f,-0.633f, 0.246f,-0.662f)) },
    };

    public static bool TryGetMappingInfo(string kkBoneName, out BoneMappingInfo mappingInfo)
    {
        // 先檢查字典中是否有完全匹配
        if (_kkToMmdMap.TryGetValue(kkBoneName, out mappingInfo))
        {
            return true;
        }

        // 再檢查字典中是否有前綴匹配
        foreach (var kvp in _kkToMmdMap)
        {
            if (!string.IsNullOrEmpty(kvp.Key) && kkBoneName.StartsWith(kvp.Key))
            {
                mappingInfo = kvp.Value;
                return true;
            }
        }

        mappingInfo = null;
        return false;
    }
}

public class VmdMotionFrame
{
    public string BoneName { get; set; }
    public uint FrameNumber { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public static readonly byte[] DefaultInterpolation = {
        20,  20,   0,   0, 107, 107, 107, 107, 20,  20,  20,  20, 107, 107, 107, 107,
        20,  20,  20, 107, 107, 107, 107,  20, 20,  20,  20, 107, 107, 107, 107,   0,
        20,  20, 107, 107, 107, 107,  20,  20, 20,  20, 107, 107, 107, 107,   0,   0,
        20, 107, 107, 107, 107,  20,  20,  20, 20, 107, 107, 107, 107,   0,   0,   0,
    };

    public VmdMotionFrame(string boneName)
    {
        BoneName = boneName;
        FrameNumber = 0;
        Position = Vector3.zero;
        Rotation = Quaternion.identity;
    }
}

public class VmdIkEnable
{
    public string IkName { get; set; }
    public bool Enable { get; set; }

    public VmdIkEnable(string ikName, bool enable)
    {
        IkName = ikName;
        Enable = enable;
    }
}

public class VmdIkFrame
{
    public uint FrameNumber { get; set; }
    public bool Display { get; set; }
    public List<VmdIkEnable> IkEnables { get; set; }

    public VmdIkFrame()
    {
        FrameNumber = 0;
        Display = true;
        IkEnables = new List<VmdIkEnable>();
    }
}

public class VmdExporter
{
    public void Export(List<VmdMotionFrame> frames, List<VmdIkFrame> ikFrames, string modelName, string filePath)
    {
        var shiftJisEncoding = Encoding.GetEncoding("Shift_JIS");

        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(fs))
        {
            // 寫入VMD標頭
            writer.Write(shiftJisEncoding.GetBytes("Vocaloid Motion Data 0002\0\0\0\0\0"));

            // 寫入模型名稱
            byte[] modelNameBytes = new byte[20];
            byte[] tempBytes = shiftJisEncoding.GetBytes(modelName);
            Buffer.BlockCopy(tempBytes, 0, modelNameBytes, 0, Math.Min(tempBytes.Length, 20));
            writer.Write(modelNameBytes);

            // 寫入骨骼框架
            writer.Write(frames.Count);
            foreach (var frame in frames.OrderBy(f => f.FrameNumber).ThenBy(f => f.BoneName))
            {
                byte[] nameBytes = new byte[15];
                tempBytes = shiftJisEncoding.GetBytes(frame.BoneName);
                Buffer.BlockCopy(tempBytes, 0, nameBytes, 0, Math.Min(tempBytes.Length, 15));
                writer.Write(nameBytes);
                writer.Write(frame.FrameNumber);
                writer.Write(frame.Position.x);
                writer.Write(frame.Position.y);
                writer.Write(frame.Position.z);
                writer.Write(frame.Rotation.x);
                writer.Write(frame.Rotation.y);
                writer.Write(frame.Rotation.z);
                writer.Write(frame.Rotation.w);
                writer.Write(VmdMotionFrame.DefaultInterpolation);
            }

            // 寫入表情框架 (空)
            writer.Write(0);

            // 寫入攝影機框架 (空)
            writer.Write(0);

            // 寫入燈光框架 (空)
            writer.Write(0);

            // 寫入自陰影資料 (空)
            writer.Write(0);

            // 寫入IK框架
            writer.Write(ikFrames.Count);
            foreach (var ikFrame in ikFrames.OrderBy(f => f.FrameNumber))
            {
                writer.Write(ikFrame.FrameNumber);
                writer.Write((byte)(ikFrame.Display ? 1 : 0));
                writer.Write(ikFrame.IkEnables.Count);

                foreach (var ikEnable in ikFrame.IkEnables)
                {
                    // IK名稱字段是20字節 (15字節名稱 + 5字節填充)
                    byte[] ikNameBytes = new byte[20];
                    tempBytes = shiftJisEncoding.GetBytes(ikEnable.IkName);
                    Buffer.BlockCopy(tempBytes, 0, ikNameBytes, 0, Math.Min(tempBytes.Length, 15));
                    writer.Write(ikNameBytes);
                    writer.Write((byte)(ikEnable.Enable ? 1 : 0));
                }
            }
        }
    }
}
#endregion

namespace KKBridge
{
    [BepInPlugin("com.rint.kkbridge", "KKBridge Plugin", "1.0.0")]
    public class KKBridgePlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        // 白名單: 只導出名稱包含以下任何關鍵字的骨骼
        private static readonly List<string> _requiredBoneNameParts = new List<string>
        {
            "_j_",
            "_J_",
            "chaF_",
            "EyeTarget",
        };

        private void Awake()
        {
            Log = base.Logger;
            Log.LogInfo("KKBridge Plugin loaded! Press F7 to export all characters' VMD & bone info.");
        }

        private void Start()
        {
            // 插件載入後立即執行，和LateUpdate的方案只能二選一
            PrintSelectedBoneInfo();
            ExportAllData();
        }

        private void Update()
        {
        }

        // // 我們使用 LateUpdate 來捕獲按鍵，確保在當前影格的邏輯循環中
        // private void LateUpdate()
        // {
        //     if (Input.GetKeyDown(KeyCode.F7))
        //     {
        //         // 按下F7時，不要立刻執行，而是啟動我們的協程
        //         Log.LogInfo("F7 key pressed. Waiting for end of frame to export...");
        //         StartCoroutine(ExportAtEndOfFrame());
        //     }
        // }
        // // 這是一個協程，它會在一個影格的最後時刻執行
        // private System.Collections.IEnumerator ExportAtEndOfFrame()
        // {
        //     // 關鍵！等待，直到所有 Update、LateUpdate 和渲染都完成
        //     yield return new WaitForEndOfFrame();
        //     // 在這個時間點，所有 Transform 的數據都是最終的、穩定的
        //     Log.LogInfo("End of frame reached. Executing data export...");
        //     PrintSelectedBoneInfo();
        //     ExportAllData();
        // }

        /// <summary>
        /// 獲取並打印當前在工作室中選中的骨骼資訊
        /// </summary>
        private void PrintSelectedBoneInfo()
        {
            // 透過 Singleton 獲取 GuideObject 管理器實例
            var guideObjectManager = Singleton<GuideObjectManager>.Instance;
            if (guideObjectManager == null)
            {
                Log.LogWarning("GuideObjectManager not found. Cannot determine selected bone.");
                return;
            }

            // 獲取當前選中的 GuideObject
            GuideObject selectedGuideObject = guideObjectManager.selectObject;

            // 檢查是否有選中的物件，並且該物件有控制一個 Transform (骨骼)
            if (selectedGuideObject != null && selectedGuideObject.transformTarget != null)
            {
                Transform selectedBone = selectedGuideObject.transformTarget;
                PrintBoneInfo(selectedBone);
            }
            else
            {
                Log.LogInfo("No bone is currently selected in the studio.");
            }
        }

        // 新增輔助方法：找到 armature 根部
        private Transform FindArmatureRoot(Transform bone)
        {
            Transform current = bone;

            // 向上遍歷找到角色根部或包含 "chaF_" 的節點
            while (current != null)
            {
                // 檢查是否是角色根部的常見標識
                if (current.name.StartsWith("chaF_") ||
                    current.name.Contains("armature") ||
                    current.name.Contains("Armature") ||
                    current.name == "BodyTop" ||
                    (current.parent != null && current.parent.name.StartsWith("chaF_")))
                {
                    return current;
                }
                current = current.parent;
            }

            // 如果沒找到特定的根部，嘗試找到最上層有 ChaControl 組件的物件
            current = bone;
            while (current != null)
            {
                if (current.GetComponent<ChaControl>() != null)
                {
                    return current;
                }
                current = current.parent;
            }

            // 最後退而求其次，返回最頂層的 Transform
            current = bone;
            while (current.parent != null)
            {
                current = current.parent;
            }

            return current;
        }

        /// <summary>
        /// 將單一骨骼的詳細資訊打印到控制台
        /// </summary>
        private void PrintBoneInfo(Transform bone)
        {
            if (bone == null) return;

            string displayName;
            if (BoneMapper.TryGetMappingInfo(bone.name, out var mapInfo))
            {
                displayName = $"{bone.name} [{mapInfo.MmdName}]";
            }
            else
            {
                displayName = bone.name;
            }

            // 獲取Transform的完整路徑
            string fullTransformPath = bone.name;
            Transform parent = bone.parent;
            while (parent != null)
            {
                fullTransformPath = parent.name + "/" + fullTransformPath;
                parent = parent.parent;
            }

            // 計算軸角表示法
            Vector3 localAxis, worldAxis;
            float localAngle, worldAngle;
            bone.localRotation.ToAngleAxis(out localAngle, out localAxis);
            bone.rotation.ToAngleAxis(out worldAngle, out worldAxis);

            // 計算四元數的大小和平方大小
            float localQuatMag = bone.localRotation.magnitude();
            float worldQuatMag = bone.rotation.magnitude();
            float localQuatSqrMag = bone.localRotation.sqrMagnitude();
            float worldQuatSqrMag = bone.rotation.sqrMagnitude();

            // 計算正規化的四元數
            Quaternion localNormalized = bone.localRotation.normalized();
            Quaternion worldNormalized = bone.rotation.normalized();

            // 計算共軛四元數
            Quaternion localConjugate = bone.localRotation.conjugated();
            Quaternion worldConjugate = bone.rotation.conjugated();

            // 計算逆四元數
            Quaternion localInverse = Quaternion.Inverse(bone.localRotation);
            Quaternion worldInverse = Quaternion.Inverse(bone.rotation);

            // 計算變換矩陣的行列式 (用於檢測是否有翻轉)
            Matrix4x4 localToWorldMatrix = bone.localToWorldMatrix;
            Matrix4x4 worldToLocalMatrix = bone.worldToLocalMatrix;

            // 計算與父骨骼的相對資訊
            string parentInfo = "None";
            string relativeInfo = "";
            if (bone.parent != null)
            {
                parentInfo = bone.parent.name;
                Vector3 relativePos = bone.position - bone.parent.position;
                Quaternion relativeRot = Quaternion.Inverse(bone.parent.rotation) * bone.rotation;
                Vector3 relativeRotEuler;
                float relativeAngle;
                Vector3 relativeAxis;
                relativeRot.ToAngleAxis(out relativeAngle, out relativeAxis);
                relativeRotEuler = relativeRot.eulerAngles;

                relativeInfo = $"Relative to Parent Position: P{relativePos.ToString("F3")}\n" +
                              $"Relative to Parent Rotation: R{relativeRotEuler.ToString("F3")}\n";
            }

            // 計算子骨骼數量
            int childCount = bone.childCount;
            string childrenInfo = childCount > 0 ? $"Children: {childCount} bones" : "Children: None";
            // 找到 armature 根部
            Transform armatureRoot = FindArmatureRoot(bone);

            // 計算相對於 armature 根部的變換
            string armatureRelativeInfo = "";
            if (armatureRoot != null)
            {
                // 計算相對位置
                Vector3 relativeToArmaturePos = armatureRoot.InverseTransformPoint(bone.position);

                // 計算相對旋轉
                Quaternion relativeToArmatureRot = Quaternion.Inverse(armatureRoot.rotation) * bone.rotation;
                Vector3 relativeToArmatureEuler = relativeToArmatureRot.eulerAngles;

                // 計算軸角表示法
                Vector3 armatureAxis;
                float armatureAngle;
                relativeToArmatureRot.ToAngleAxis(out armatureAngle, out armatureAxis);

                armatureRelativeInfo = $"\n--- RELATIVE TO ARMATURE ROOT ({armatureRoot.name}) ---\n" +
                                      $"Armature Relative Position: P{relativeToArmaturePos.ToString("F3")}\n" +
                                      $"Armature Relative Rotation: R{relativeToArmatureEuler.ToString("F3")}\n";
            }

            string boneInfo = $"\n" +
                              $"==================== SELECTED BONE INFO ====================\n" +
                              $"Name: {displayName}\n" +
                              $"Full Path: {fullTransformPath}\n" +
                              $"Parent: {parentInfo}\n" +
                              $"{childrenInfo}\n" +
                              $"\n--- POSITION ---\n" +
                              $"Local Position:     P{bone.localPosition.ToString("F3")}\n" +
                              $"World Position:     P{bone.position.ToString("F3")}\n" +
                              $"\n--- ROTATION (EULER ANGLES) ---\n" +
                              $"Local Rotation:     R{bone.localEulerAngles.ToString("F3")}\n" +
                              $"World Rotation:     R{bone.eulerAngles.ToString("F3")}\n" +
                              $"\n--- ROTATION (QUATERNION) ---\n" +
                              $"World Rotation:     R{bone.localRotation.ToString("F3")}\n" +
                              $"World Rotation:     R{bone.rotation.ToString("F3")}\n" +
                              $"\n--- RELATIVE TO PARENT ---\n" +
                              $"{relativeInfo}" +
                              $"{armatureRelativeInfo}" + // 加入 armature 相對資訊
                              $"\n--- ADDITIONAL INFO ---\n" +
                              $"Transform Right:    {bone.right.ToString("F3")}\n" +
                              $"Transform Up:       {bone.up.ToString("F3")}\n" +
                              $"Transform Forward:  {bone.forward.ToString("F3")}\n" +
                              $"=============================================================";

            Log.LogInfo(boneInfo);
        }

        /// <summary>
        /// 執行所有導出操作
        /// </summary>
        private void ExportAllData()
        {
            var studioInstance = Singleton<Studio.Studio>.Instance;
            if (studioInstance == null || studioInstance.dicObjectCtrl == null)
            {
                Log.LogError("Could not get Studio instance. Are you in the main studio scene?");
                return;
            }

            var characters = studioInstance.dicObjectCtrl.Values.OfType<OCIChar>().ToList();
            if (!characters.Any())
            {
                Log.LogWarning("No characters found in the scene to export.");
                return;
            }

            Log.LogInfo($"Found {characters.Count} character(s). Starting export process...");

            // --- 準備VMD導出 ---
            var exporter = new VmdExporter();
            string outputDirectory = "C:\\Users\\user\\Desktop\\out";
            try
            {
                // 步驟 1: 確保目標資料夾存在，如果不存在就建立它
                Directory.CreateDirectory(outputDirectory);
                // 步驟 2: 清空資料夾內的所有內容
                DirectoryInfo di = new DirectoryInfo(outputDirectory);
                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.GetDirectories())
                {
                    dir.Delete(true);
                }
            }
            catch (Exception e)
            {
                Log.LogError($"Could not create or clear output directory '{outputDirectory}'. Error: {e.Message}");
                return;
            }

            int charIndex = 1;
            // --- 遍歷所有角色，為每個角色分別導出 VMD 和 TXT ---
            foreach (var ociChar in characters)
            {
                ChaControl chaCtrl = ociChar.charInfo;
                if (chaCtrl == null) continue;

                string charName = chaCtrl.chaFile.parameter.fullname;
                Log.LogInfo($"Processing Character {charIndex}: {charName}");

                Transform boneRoot = chaCtrl.transform;
                if (boneRoot == null)
                {
                    Log.LogWarning($"Could not find bone root for character {charName}. Skipping.");
                    charIndex++;
                    continue;
                }

                // --- 1. 導出骨骼資訊到獨立的 TXT 檔案 ---
                var boneReportBuilder = new StringBuilder();
                TraverseBones(boneRoot, "", boneReportBuilder);

                string txtFileName = $"{charIndex}_{charName}.txt";
                // 清理txt檔案名稱中的非法字元
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    txtFileName = txtFileName.Replace(c.ToString(), "");
                }
                string txtFilePath = Path.Combine(outputDirectory, txtFileName);

                try
                {
                    File.WriteAllText(txtFilePath, boneReportBuilder.ToString(), Encoding.UTF8);
                    Log.LogInfo($"Successfully wrote bone info to: {txtFilePath}");
                }
                catch (Exception e)
                {
                    Log.LogError($"Failed to write bone info file for {charName}: {e.Message}");
                }

                // --- 2. 導出 VMD 檔案 ---
                var vmdFrames = new List<VmdMotionFrame>();
                CollectBoneData(ociChar, boneRoot, vmdFrames);

                // 創建IK框架，將所有IK關閉但顯示網格打開
                var ikFrames = new List<VmdIkFrame>();
                var ikFrame = new VmdIkFrame()
                {
                    FrameNumber = 0,
                    Display = true  // 設為true表示顯示網格 (show mesh) 打開
                };

                // 添加需要關閉的IK
                string[] ikNames = {
                    "左腕ＩＫ",
                    "右腕ＩＫ",
                    "左足ＩＫ",
                    "右足ＩＫ",
                    "左つま先ＩＫ",
                    "右つま先ＩＫ",

                    "ﾈｸﾀｲＩＫ",
                    "右髪ＩＫ",
                    "左髪ＩＫ",
                    "しっぽＩＫ",
                    "右腰ベルトＩＫ",
                    "左腰ベルトＩＫ",
                };
                foreach (string ikName in ikNames)
                {
                    ikFrame.IkEnables.Add(new VmdIkEnable(ikName, false)); // 設定為false來關閉IK
                }
                ikFrames.Add(ikFrame);

                string vmdFileName = $"{charIndex}_{charName}.vmd";
                // 清理vmd檔案名稱中的非法字元
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    vmdFileName = vmdFileName.Replace(c.ToString(), "");
                }
                string vmdFilePath = Path.Combine(outputDirectory, vmdFileName);

                try
                {
                    exporter.Export(vmdFrames, ikFrames, "KoikatsuModel", vmdFilePath);
                    Log.LogInfo($"Successfully exported VMD for character {charIndex} to: {vmdFilePath}");
                }
                catch (Exception e)
                {
                    Log.LogError($"Failed to export VMD for character {charIndex}: {e.Message}");
                }

                charIndex++;
            }

            Log.LogInfo("All export tasks finished.");
        }



        public static Quaternion CalculateBoneConverter(Quaternion A)
        {
            // --- 等價的矩陣實現步驟 ---
            // 1. mat = A.to_matrix()
            // 2. mat[1], mat[2] = mat[2], mat[1]
            // 3. mat.transpose()
            // 4. mat.invert()
            // 5. B = mat.to_quaternion()
            const float c = 0.707106781186547524400844362104849039284835937688474f;
            float x = A.x;
            float y = A.y;
            float z = A.z;
            float w = A.w;
            // 根據推導出的線性變換公式直接計算新四元數 B 的分量
            Quaternion B = new Quaternion(
                c * (-y - z),
                -c * (w - x),
                c * (w + x),
                c * (y - z)
            );
            return B;
        }
        public static Quaternion ConvertRotation(Quaternion transformXyzw, Quaternion rotationXyzw)
        {
            // 坐標變換
            // C = A * B * A.Inverse();
            // 優化：單位四元數共軛和逆等價，可用共軛取代逆，更高效。
            // C = A * B * A.conjugated();
            Quaternion result = transformXyzw * rotationXyzw * transformXyzw.conjugated();
            return result;
        }
        /// <summary>
        /// 將四元數旋轉轉換為VMD格式的旋轉
        /// 使用軸角表示法進行坐標變換
        /// </summary>
        /// <param name="transformXyzw">變換四元數，用於坐標變換</param>
        /// <param name="rotationXyzw">原始旋轉四元數</param>
        /// <returns>轉換後的VMD旋轉四元數</returns>
        public static Quaternion ConvertRotationLegacy(Quaternion transformXyzw, Quaternion rotationXyzw)
        {
            Vector3 axis;
            float angle;
            // 將四元數轉換為軸角表示法
            rotationXyzw.ToAngleAxis(out angle, out axis);
            // 使用變換四元數轉換旋轉軸
            Vector3 finalAxis = transformXyzw * axis;
            // 使用轉換後的軸和原始角度重建四元數
            Quaternion convertedRotation = Quaternion.AngleAxis(angle, finalAxis.normalized);
            return convertedRotation;
        }



        /// <summary>
        /// 將 0-360 度的歐拉角轉換為 -180-180 度的範圍，以便進行比較和限制。
        /// </summary>
        private float NormalizeAngle(float angle)
        {
            while (angle > 180f)
                angle -= 360f;
            while (angle < -180f)
                angle += 360f;
            return angle;
        }

        /// <summary>
        /// 遞迴收集符合白名單條件的骨骼數據用於VMD導出
        /// </summary>
        private void CollectBoneData(OCIChar ociChar, Transform bone, List<VmdMotionFrame> frameList)
        {
            if (bone == null) return;

            // 檢查當前骨骼是否符合白名單條件
            bool shouldExport = _requiredBoneNameParts.Any(requiredPart => bone.name.Contains(requiredPart));

            // 如果符合白名單，並且可以在 BoneMapper 中找到對應的 MMD 名稱，則導出其數據
            if (shouldExport && BoneMapper.TryGetMappingInfo(bone.name, out var mapInfo))
            {
                var frame = new VmdMotionFrame(mapInfo.MmdName);

                Vector3 relativePos;
                if (mapInfo.MmdName == "全ての親")
                {
                    // 根骨骼用 World Position
                    const float mmdScaleFactor = 12.5f;
                    Vector3 worldPos = bone.position;
                    relativePos = new Vector3(-worldPos.x * mmdScaleFactor, worldPos.y * mmdScaleFactor, -worldPos.z * mmdScaleFactor);
                }
                else
                {
                    // 其他骨骼只能旋轉不能移動
                    relativePos = new Vector3(0, 0, 0);
                }

                Quaternion relativeRot;
                if (mapInfo.MmdName == "全ての親")
                {
                    // 根骨骼用 World Rotation
                    relativeRot = bone.rotation;
                }
                else
                {
                    // 其他骨骼用 Local Rotation
                    relativeRot = bone.localRotation;
                }

                // 坐標變換
                switch (mapInfo.MmdName)
                {
                    case "全ての親":
                        {
                            relativeRot = new Quaternion(relativeRot.x, -relativeRot.y, -relativeRot.z, relativeRot.w);
                            break;
                        }
                    case "センター":
                        {
                            break;
                        }
                    case "左目":
                    case "右目":
                        {
                            {
                                // 根據EyeLookController，EyeLookCalc，EyeLookMaterialControll的代碼
                                // Koikatsu眼睛是貼圖移動，角度轉像素偏移的線性變換，需測量等價縮放因子

                                // 為垂直(上/下)和水平(左/右)設定完全獨立的縮放因子
                                // 根據實驗觀察微調這三個數值
                                const float eyeIntensityFactorX_Up = 0.55f;     // 垂直向上看的縮放
                                const float eyeIntensityFactorX_Down = 1.0f;    // 垂直向下看的縮放
                                const float eyeIntensityFactorY = 0.45f;        // 水平方向(左右看)的縮放

                                // 獲取 EyeTarget 的原始局部旋轉
                                Quaternion rawRotation = relativeRot;
                                Vector3 rawEuler = rawRotation.eulerAngles;

                                // 1. 將原始歐拉角標準化到 -180 ~ 180 度範圍
                                float normalizedX = NormalizeAngle(rawEuler.x);
                                float normalizedY = NormalizeAngle(rawEuler.y);
                                float normalizedZ = NormalizeAngle(rawEuler.z);

                                // 2. 判斷向上還是向下看，並應用不同的縮放因子
                                float scaledX;
                                if (normalizedX < 0) // 角度為負，是向上看
                                {
                                    scaledX = normalizedX * eyeIntensityFactorX_Up;
                                }
                                else // 角度為正或零，是向下看
                                {
                                    scaledX = normalizedX * eyeIntensityFactorX_Down;
                                }

                                // 3. 獨立縮放水平方向的角度
                                float scaledY = normalizedY * eyeIntensityFactorY;

                                // 4. 將縮放後的歐拉角重新組合成四元數
                                relativeRot = Quaternion.Euler(scaledX, scaledY, normalizedZ);
                            }
                            relativeRot = relativeRot.conjugated();
                            relativeRot = ConvertRotation(Quaternion.Euler(90, 0, -90), relativeRot);
                            break;
                        }
                    case "首":
                    case "頭":
                    case "上半身":
                    case "上半身2":
                        {
                            relativeRot = new Quaternion(relativeRot.x, -relativeRot.y, -relativeRot.z, relativeRot.w);
                            break;
                        }
                    case "下半身":
                        {
                            break;
                        }
                    case "左親指０":
                    case "左親指１":
                    case "左親指２":
                        {
                            relativeRot = new Quaternion(-relativeRot.x, relativeRot.y, -relativeRot.z, relativeRot.w);
                            relativeRot = ConvertRotation(Quaternion.Euler(0, 0, -90), relativeRot);
                            break;
                        }
                    case "左人指１":
                    case "左人指２":
                    case "左人指３":
                    case "左中指１":
                    case "左中指２":
                    case "左中指３":
                    case "左薬指１":
                    case "左薬指２":
                    case "左薬指３":
                    case "左小指１":
                    case "左小指２":
                    case "左小指３":
                        {
                            relativeRot = ConvertRotation(Quaternion.Euler(0, 90, 90), relativeRot);
                            break;
                        }
                    case "左肩":
                    case "左腕":
                    case "左ひじ":
                    case "左手首":
                        {
                            relativeRot = ConvertRotation(Quaternion.Euler(0, 90, 90), relativeRot);
                            break;
                        }
                    case "右親指０":
                    case "右親指１":
                    case "右親指２":
                        {
                            relativeRot = new Quaternion(-relativeRot.x, relativeRot.y, -relativeRot.z, relativeRot.w);
                            relativeRot = ConvertRotation(Quaternion.Euler(0, 0, 90), relativeRot);
                            break;
                        }
                    case "右人指１":
                    case "右人指２":
                    case "右人指３":
                    case "右中指１":
                    case "右中指２":
                    case "右中指３":
                    case "右薬指１":
                    case "右薬指２":
                    case "右薬指３":
                    case "右小指１":
                    case "右小指２":
                    case "右小指３":
                        {
                            relativeRot = new Quaternion(-relativeRot.x, -relativeRot.y, relativeRot.z, relativeRot.w);
                            relativeRot = ConvertRotation(Quaternion.Euler(0, -90, 90), relativeRot);
                            break;
                        }
                    case "右肩":
                    case "右腕":
                    case "右ひじ":
                    case "右手首":
                        {
                            relativeRot = new Quaternion(-relativeRot.x, relativeRot.y, -relativeRot.z, relativeRot.w);
                            relativeRot = ConvertRotation(Quaternion.Euler(0, -90, 90), relativeRot);
                            break;
                        }
                    case "左足":
                    case "右足":
                    case "左ひざ":
                    case "右ひざ":
                        {
                            break;
                        }
                    case "左足首":
                    case "右足首":
                        {
                            relativeRot = ConvertRotation(Quaternion.Euler(90, 0, 0), relativeRot);
                            break;
                        }
                    case "左足先EX":
                    case "右足先EX":
                        {
                            relativeRot = new Quaternion(-relativeRot.x, -relativeRot.y, relativeRot.z, relativeRot.w);
                            relativeRot = ConvertRotation(Quaternion.Euler(90, 0, 0), relativeRot);
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
                // 應用 RestPoseCorrection 和 CoordinateConversion
                relativeRot = mapInfo.RestPoseCorrection * relativeRot;
                relativeRot = ConvertRotation(mapInfo.CoordinateConversion, relativeRot);

                frame.Position = relativePos;
                // 只在結尾正規化，解決精度誤差導致magnitude在0.999和1.001之間浮動
                frame.Rotation = relativeRot.normalized();
                // 檢查正規化結果
                // Log.LogInfo(frame.Rotation.magnitude().ToString("F3"));

                frameList.Add(frame);
            }

            // 無論當前骨骼是否被導出，都繼續遞迴處理其所有子骨骼
            foreach (Transform child in bone)
            {
                CollectBoneData(ociChar, child, frameList);
            }
        }

        /// <summary>
        /// 遞迴函式，遍歷骨骼並建立報告，只包含白名單內的骨骼
        /// </summary>
        private void TraverseBones(Transform bone, string indent, StringBuilder builder)
        {
            if (bone == null) return;

            // 檢查當前骨骼是否符合白名單條件
            bool shouldExport = _requiredBoneNameParts.Any(requiredPart => bone.name.Contains(requiredPart));

            // 如果符合白名單，則將其資訊添加到報告中
            if (shouldExport)
            {
                string displayName;
                if (BoneMapper.TryGetMappingInfo(bone.name, out var mapInfo))
                {
                    displayName = $"{bone.name} [{mapInfo.MmdName}]";
                }
                else
                {
                    displayName = bone.name;
                }

                string boneInfo = $"{indent}{displayName}                    P{bone.localPosition.ToString("F3")} R{bone.localEulerAngles.ToString("F3")} S{bone.localScale.ToString("F3")}";
                builder.AppendLine(boneInfo);
            }

            // 無論當前骨骼是否被包含在報告中，都繼續遞迴處理其所有子骨骼
            foreach (Transform child in bone)
            {
                TraverseBones(child, indent + "  ", builder);
            }
        }
    }
}
