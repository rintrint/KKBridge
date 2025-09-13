using BepInEx;
using BepInEx.Logging;
using Studio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

#region VMD 工具類別 (VMD Helper Classes)

/// <summary>
/// 骨骼名稱映射器，將 Koikatsu 的骨骼名稱翻譯為 MMD 標準名稱
/// </summary>
public static class BoneMapper
{
    private static readonly Dictionary<string, string> KkToMmdMap = new Dictionary<string, string>
    {
        { "", "全ての親" },
        { "cf_j_hips", "センター" },
        { "cf_j_spine01", "上半身" },
        { "cf_j_spine02", "上半身2" },
        { "cf_j_neck", "首" },
        { "cf_j_head", "頭" },
        { "cf_j_shoulder_L", "左肩" },
        { "cf_j_shoulder_R", "右肩" },
        { "cf_j_arm00_L", "左腕" },
        { "cf_j_arm00_R", "右腕" },
        { "cf_j_forearm01_L", "左ひじ" },
        { "cf_j_forearm01_R", "右ひじ" },
        { "cf_j_hand_L", "左手首" },
        { "cf_j_hand_R", "右手首" },
        { "cf_j_thigh00_L", "左足" },
        { "cf_j_thigh00_R", "右足" },
        { "cf_j_leg01_L", "左ひざ" },
        { "cf_j_leg01_R", "右ひざ" },
        { "cf_j_leg03_L", "左足首" },
        { "cf_j_leg03_R", "右足首" },
        { "cf_j_thumb01_L", "左親指１" }, { "cf_j_thumb02_L", "左親指２" },
        { "cf_j_index01_L", "左人指１" }, { "cf_j_index02_L", "左人指２" }, { "cf_j_index03_L", "左人指３" },
        { "cf_j_middle01_L", "左中指１" }, { "cf_j_middle02_L", "左中指２" }, { "cf_j_middle03_L", "左中指３" },
        { "cf_j_ring01_L", "左薬指１" }, { "cf_j_ring02_L", "左薬指２" }, { "cf_j_ring03_L", "左薬指３" },
        { "cf_j_little01_L", "左小指１" }, { "cf_j_little02_L", "左小指２" }, { "cf_j_little03_L", "左小指３" },
        { "cf_j_thumb01_R", "右親指１" }, { "cf_j_thumb02_R", "右親指２" },
        { "cf_j_index01_R", "右人指１" }, { "cf_j_index02_R", "右人指２" }, { "cf_j_index03_R", "右人指３" },
        { "cf_j_middle01_R", "右中指１" }, { "cf_j_middle02_R", "右中指２" }, { "cf_j_middle03_R", "右中指３" },
        { "cf_j_ring01_R", "右薬指１" }, { "cf_j_ring02_R", "右薬指２" }, { "cf_j_ring03_R", "右薬指３" },
        { "cf_j_little01_R", "右小指１" }, { "cf_j_little02_R", "右小指２" }, { "cf_j_little03_R", "右小指３" },
    };

    public static bool TryGetMmdBoneName(string kkBoneName, out string mmdBoneName)
    {
        return KkToMmdMap.TryGetValue(kkBoneName, out mmdBoneName);
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

public class VmdExporter
{
    public void Export(List<VmdMotionFrame> frames, string modelName, string filePath)
    {
        var shiftJisEncoding = Encoding.GetEncoding("Shift_JIS");

        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(fs))
        {
            writer.Write(shiftJisEncoding.GetBytes("Vocaloid Motion Data 0002\0\0\0\0\0"));
            byte[] modelNameBytes = new byte[20];
            byte[] tempBytes = shiftJisEncoding.GetBytes(modelName);
            Buffer.BlockCopy(tempBytes, 0, modelNameBytes, 0, Math.Min(tempBytes.Length, 20));
            writer.Write(modelNameBytes);

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
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
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
        private static readonly List<string> RequiredBoneNameParts = new List<string>
        {
            "_j_",
            "_J_",
        };

        private void Awake()
        {
            Log = base.Logger;
            Log.LogInfo("KKBridge Plugin loaded! Press F7 to export all characters' VMD & bone info.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F7))
            {
                // ----- 打印當前選中的骨骼資訊 -----
                Log.LogInfo("F7 key pressed. Checking for selected bone...");
                PrintSelectedBoneInfo();

                // ----- 您原有的導出所有資料的功能 -----
                Log.LogInfo("Now, exporting all data...");
                ExportAllData();
            }
        }

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

        /// <summary>
        /// 將單一骨骼的詳細資訊打印到控制台
        /// </summary>
        private void PrintBoneInfo(Transform bone)
        {
            if (bone == null) return;

            string displayName;
            if (BoneMapper.TryGetMmdBoneName(bone.name, out string mmdBoneName))
            {
                displayName = $"{bone.name} [{mmdBoneName}]";
            }
            else
            {
                displayName = bone.name;
            }

            string boneInfo = $"\n" +
                              $"-------------------- SELECTED BONE INFO --------------------\n" +
                              $"Name: {displayName}\n" +
                              $"Local Position:  P{bone.localPosition:F4}\n" +
                              $"Local Rotation:  R{bone.localEulerAngles:F4}\n" +
                              $"Local Scale:     S{bone.localScale:F4}\n" +
                              $"------------------------------------------------------------";

            Log.LogInfo(boneInfo);
        }

        /// <summary>
        /// 按下F7後執行所有導出操作
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

                Transform boneRoot = FindDeepChild(chaCtrl.transform, "p_cf_body_bone");
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
                // 清理檔案名稱中的非法字元
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
                CollectBoneData(boneRoot, vmdFrames);

                string vmdFileName = $"{charIndex}_{charName}.vmd";
                // 再次清理檔名
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    vmdFileName = vmdFileName.Replace(c.ToString(), "");
                }
                string vmdFilePath = Path.Combine(outputDirectory, vmdFileName);

                try
                {
                    exporter.Export(vmdFrames, "KoikatsuModel", vmdFilePath);
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

        /// <summary>
        /// 遞迴收集符合白名單條件的骨骼數據用於VMD導出
        /// </summary>
        private void CollectBoneData(Transform bone, List<VmdMotionFrame> frameList)
        {
            if (bone == null) return;

            // 檢查當前骨骼是否符合白名單條件
            bool shouldExport = RequiredBoneNameParts.Any(requiredPart => bone.name.Contains(requiredPart));

            // 如果符合白名單，並且可以在 BoneMapper 中找到對應的 MMD 名稱，則導出其數據
            if (shouldExport && BoneMapper.TryGetMmdBoneName(bone.name, out string mmdBoneName))
            {
                var frame = new VmdMotionFrame(mmdBoneName);
                Vector3 localPos = bone.localPosition;
                Quaternion localRot = bone.localRotation;

                // 根據骨骼名稱應用 T-Pose 到 A-Pose 的旋轉校正
                switch (mmdBoneName)
                {
                    case "左肩":
                        localRot *= Quaternion.Euler(0, 0, -14.0f);
                        break;
                    case "右肩":
                        localRot *= Quaternion.Euler(0, 0, 14.0f);
                        break;
                    case "左腕":
                        localRot *= Quaternion.Euler(0, 0, -21.0f);
                        break;
                    case "右腕":
                        localRot *= Quaternion.Euler(0, 0, 21.0f);
                        break;
                }

                // const float scaleFactor = 12.5f;
                // frame.Position = new Vector3(-localPos.x * scaleFactor, localPos.y * scaleFactor, -localPos.z * scaleFactor);
                frame.Position = new Vector3(0, 0, 0);
                frame.Rotation = new Quaternion(-localRot.x, localRot.y, -localRot.z, localRot.w);
                frameList.Add(frame);
            }

            // 無論當前骨骼是否被導出，都繼續遞迴處理其所有子骨骼
            foreach (Transform child in bone)
            {
                CollectBoneData(child, frameList);
            }
        }

        /// <summary>
        /// 遞迴函式，遍歷骨骼並建立報告，只包含白名單內的骨骼
        /// </summary>
        private void TraverseBones(Transform bone, string indent, StringBuilder builder)
        {
            if (bone == null) return;

            // 檢查當前骨骼是否符合白名單條件
            bool shouldIncludeInReport = RequiredBoneNameParts.Any(requiredPart => bone.name.Contains(requiredPart));

            // 如果符合白名單，則將其資訊添加到報告中
            if (shouldIncludeInReport)
            {
                BoneMapper.TryGetMmdBoneName(bone.name, out string mmdBoneName);
                string displayName = string.IsNullOrEmpty(mmdBoneName)
                    ? bone.name
                    : $"{bone.name} [{mmdBoneName}]";

                string boneInfo = $"{indent}{displayName}                    P{bone.localPosition:F3} R{bone.localEulerAngles:F3} S{bone.localScale:F3}";
                builder.AppendLine(boneInfo);
            }

            // 無論當前骨骼是否被包含在報告中，都繼續遞迴處理其所有子骨骼
            foreach (Transform child in bone)
            {
                TraverseBones(child, indent + "  ", builder);
            }
        }

        public static Transform FindDeepChild(Transform parent, string targetName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == targetName) return child;
                Transform result = FindDeepChild(child, targetName);
                if (result != null) return result;
            }
            return null;
        }
    }
}
