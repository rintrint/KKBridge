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

#region VMD 工具類別 (VMD Helper Classes)

/// <summary>
/// 骨骼名稱映射器，將 Koikatsu 的骨骼名稱翻譯為 MMD 標準名稱，並儲存其初始旋轉
/// </summary>
public static class BoneMapper
{
    /// <summary>
    /// 儲存MMD骨骼名稱和其在Koikatsu中的初始旋轉資訊
    /// </summary>
    public class BoneMappingInfo
    {
        public string MmdName { get; }
        public Quaternion InitialRotation { get; }

        public BoneMappingInfo(string mmdName, Vector3 initialEulerAngles)
        {
            MmdName = mmdName;
            InitialRotation = Quaternion.Euler(initialEulerAngles);
        }

        public BoneMappingInfo(string mmdName)
        {
            MmdName = mmdName;
            InitialRotation = Quaternion.identity;
        }
    }

    private static readonly Dictionary<string, BoneMappingInfo> KkToMmdMap = new Dictionary<string, BoneMappingInfo>
    {
        // 根骨骼和中心
        { "chaF_", new BoneMappingInfo("全ての親") },
        // { "cf_j_root", new BoneMappingInfo("全ての親") },
        { "cf_j_hips", new BoneMappingInfo("センター") },

        // 首/頭
        { "cf_j_neck", new BoneMappingInfo("首") },
        { "cf_j_head", new BoneMappingInfo("頭") },

        // 軀幹
        { "cf_j_spine01", new BoneMappingInfo("上半身") },
        { "cf_j_spine02", new BoneMappingInfo("上半身2") },
        // { "cf_j_spine03", new BoneMappingInfo("上半身3") },
        { "cf_j_waist01", new BoneMappingInfo("下半身") },

        // 目
        // { "cf_J_hitomi_tx_L", new BoneMappingInfo("左目", new Vector3(1.4f, 2.2f, 7.1f)) },
        // { "cf_J_hitomi_tx_R", new BoneMappingInfo("右目") }, // txt中為0,0,0

        // 肩
        { "cf_j_shoulder_L", new BoneMappingInfo("左肩") },
        { "cf_j_shoulder_R", new BoneMappingInfo("右肩") },

        // 手臂
        { "cf_j_arm00_L", new BoneMappingInfo("左腕") },
        { "cf_j_arm00_R", new BoneMappingInfo("右腕") },
        { "cf_j_forearm01_L", new BoneMappingInfo("左ひじ") },
        { "cf_j_forearm01_R", new BoneMappingInfo("右ひじ") },
        { "cf_j_hand_L", new BoneMappingInfo("左手首") },
        { "cf_j_hand_R", new BoneMappingInfo("右手首") },

        // 足
        { "cf_j_thigh00_L", new BoneMappingInfo("左足") },
        { "cf_j_thigh00_R", new BoneMappingInfo("右足") },
        { "cf_j_leg01_L", new BoneMappingInfo("左ひざ") },
        { "cf_j_leg01_R", new BoneMappingInfo("右ひざ") },
        { "cf_j_leg03_L", new BoneMappingInfo("左足首") },
        { "cf_j_leg03_R", new BoneMappingInfo("右足首") },
        { "cf_j_toes_L", new BoneMappingInfo("左足先EX") },
        { "cf_j_toes_R", new BoneMappingInfo("右足先EX") },

        // 左手手指
        { "cf_j_thumb01_L", new BoneMappingInfo("左親指０", new Vector3(80.0f, 90.0f, 55.0f)) },
        { "cf_j_thumb02_L", new BoneMappingInfo("左親指１") },
        { "cf_j_thumb03_L", new BoneMappingInfo("左親指２") },
        { "cf_j_index01_L", new BoneMappingInfo("左人指１", new Vector3(3.5f, 5.3f, 5.0f)) },
        { "cf_j_index02_L", new BoneMappingInfo("左人指２") },
        { "cf_j_index03_L", new BoneMappingInfo("左人指３") },
        { "cf_j_middle01_L", new BoneMappingInfo("左中指１", new Vector3(357.0f, 359.7f, 5.0f)) },
        { "cf_j_middle02_L", new BoneMappingInfo("左中指２") },
        { "cf_j_middle03_L", new BoneMappingInfo("左中指３") },
        { "cf_j_ring01_L", new BoneMappingInfo("左薬指１", new Vector3(352.4f, 355.3f, 5.0f)) },
        { "cf_j_ring02_L", new BoneMappingInfo("左薬指２") },
        { "cf_j_ring03_L", new BoneMappingInfo("左薬指３") },
        { "cf_j_little01_L", new BoneMappingInfo("左小指１", new Vector3(344.8f, 350.6f, 5.1f)) },
        { "cf_j_little02_L", new BoneMappingInfo("左小指２") },
        { "cf_j_little03_L", new BoneMappingInfo("左小指３") },

        // 右手手指
        { "cf_j_thumb01_R", new BoneMappingInfo("右親指０", new Vector3(280.0f, 90.0f, 235.0f)) },
        { "cf_j_thumb02_R", new BoneMappingInfo("右親指１") },
        { "cf_j_thumb03_R", new BoneMappingInfo("右親指２") },
        { "cf_j_index01_R", new BoneMappingInfo("右人指１", new Vector3(356.5f, 174.7f, 185.0f)) },
        { "cf_j_index02_R", new BoneMappingInfo("右人指２") },
        { "cf_j_index03_R", new BoneMappingInfo("右人指３") },
        { "cf_j_middle01_R", new BoneMappingInfo("右中指１", new Vector3(3.0f, 180.3f, 185.0f)) },
        { "cf_j_middle02_R", new BoneMappingInfo("右中指２") },
        { "cf_j_middle03_R", new BoneMappingInfo("右中指３") },
        { "cf_j_ring01_R", new BoneMappingInfo("右薬指１", new Vector3(7.6f, 184.7f, 185.0f)) },
        { "cf_j_ring02_R", new BoneMappingInfo("右薬指２") },
        { "cf_j_ring03_R", new BoneMappingInfo("右薬指３") },
        { "cf_j_little01_R", new BoneMappingInfo("右小指１", new Vector3(15.2f, 189.4f, 185.1f)) },
        { "cf_j_little02_R", new BoneMappingInfo("右小指２") },
        { "cf_j_little03_R", new BoneMappingInfo("右小指３") },
    };

    public static bool TryGetMappingInfo(string kkBoneName, out BoneMappingInfo mappingInfo)
    {
        // 先檢查字典中是否有完全匹配
        if (KkToMmdMap.TryGetValue(kkBoneName, out mappingInfo))
        {
            return true;
        }

        // 再檢查字典中是否有前綴匹配
        foreach (var kvp in KkToMmdMap)
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
        private static readonly List<string> RequiredBoneNameParts = new List<string>
        {
            "_j_",
            "_J_",
            "chaF_",
        };

        // 用於IK部位索引的字典 (參考自CharaPoseController.cs)
        private static readonly Dictionary<FullBodyBipedEffector, int> _effectorToIndex = new Dictionary<FullBodyBipedEffector, int>
        {
            { FullBodyBipedEffector.Body, 0 },
            { FullBodyBipedEffector.LeftShoulder, 1 },
            { FullBodyBipedEffector.LeftHand, 3 },
            { FullBodyBipedEffector.RightShoulder, 4 },
            { FullBodyBipedEffector.RightHand, 6 },
            { FullBodyBipedEffector.LeftThigh, 7 },
            { FullBodyBipedEffector.LeftFoot, 9 },
            { FullBodyBipedEffector.RightThigh, 10 },
            { FullBodyBipedEffector.RightFoot, 12 },
        };

        private static readonly Dictionary<FullBodyBipedChain, int> _chainToIndex = new Dictionary<FullBodyBipedChain, int>
        {
            { FullBodyBipedChain.LeftArm, 2 },
            { FullBodyBipedChain.RightArm, 5 },
            { FullBodyBipedChain.LeftLeg, 8 },
            { FullBodyBipedChain.RightLeg, 11 },
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

                // ----- 導出所有資料 -----
                Log.LogInfo("Now, exporting all data...");
                ExportAllData();
            }
        }

        // ******** NEW/MODIFIED CODE START ********
        /// <summary>
        /// 輔助方法：檢查指定的FK部位UI上的「燈」是否啟用。
        /// 這個方法會讀取角色存檔中的真實狀態，獨立於FK總開關。
        /// </summary>
        /// <param name="ociChar">角色物件</param>
        /// <param name="partGroup">要查詢的部位 (使用OIBoneInfo.BoneGroup列舉)</param>
        private bool IsFkPartActive(OCIChar ociChar, OIBoneInfo.BoneGroup partGroup)
        {
            if (ociChar == null) return false;

            // 1. 找到部位在 FKCtrl.parts 陣列中的索引
            int index = Array.FindIndex(FKCtrl.parts, part => part == partGroup);

            // 2. 如果找到了索引，就用它去 oiCharInfo.activeFK 陣列中取值
            if (index != -1 && index < ociChar.oiCharInfo.activeFK.Length)
            {
                // oiCharInfo.activeFK 是儲存UI上每個「燈」亮暗狀態的真實布林陣列
                return ociChar.oiCharInfo.activeFK[index];
            }

            return false;
        }

        /// <summary>
        /// 輔助方法：檢查指定的IK鏈（手臂/腿）是否啟用
        /// </summary>
        private bool IsIkChainActive(OCIChar ociChar, FullBodyBipedChain part)
        {
            if (ociChar == null || !ociChar.oiCharInfo.enableIK)
                return false;

            if (_chainToIndex.TryGetValue(part, out int index))
            {
                if (index < ociChar.listIKTarget.Count)
                {
                    return ociChar.listIKTarget[index].active;
                }
            }
            return false;
        }
        // ******** NEW/MODIFIED CODE END ********

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
            if (BoneMapper.TryGetMappingInfo(bone.name, out var mapInfo))
            {
                displayName = $"{bone.name} [{mapInfo.MmdName}]";
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
                    Display = true  // 設為true表示顯示網格（show mesh）打開
                };

                // 添加需要關閉的IK
                string[] ikNames = { "左腕ＩＫ", "右腕ＩＫ", "左足ＩＫ", "右足ＩＫ", "左つま先ＩＫ", "右つま先ＩＫ" };
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

        /// <summary>
        /// 遞迴收集符合白名單條件的骨骼數據用於VMD導出
        /// </summary>
        private void CollectBoneData(OCIChar ociChar, Transform bone, List<VmdMotionFrame> frameList)
        {
            if (bone == null) return;

            // 檢查當前骨骼是否符合白名單條件
            bool shouldExport = RequiredBoneNameParts.Any(requiredPart => bone.name.Contains(requiredPart));

            // 如果符合白名單，並且可以在 BoneMapper 中找到對應的 MMD 名稱，則導出其數據
            if (shouldExport && BoneMapper.TryGetMappingInfo(bone.name, out var mapInfo))
            {
                var frame = new VmdMotionFrame(mapInfo.MmdName);

                Vector3 localPos = bone.localPosition;
                Quaternion localRot = bone.localRotation;

                // 減去骨骼的初始旋轉，得到相對於T-Pose的純淨旋轉
                // 公式為: FinalRotation = Inverse(InitialRotation) * CurrentRotation
                Quaternion initialRot = mapInfo.InitialRotation;
                Quaternion relativeRot = Quaternion.Inverse(initialRot) * localRot;

                bool shouldApplyAPoseCorrectionL =
                    (!ociChar.oiCharInfo.enableIK) || // IK總開關是關的 -> 需要校正
                    (ociChar.oiCharInfo.enableIK && !IsIkChainActive(ociChar, FullBodyBipedChain.LeftArm)); // IK總開關是開的但左手IK是關的 -> 需要校正
                bool shouldApplyAPoseCorrectionR =
                    (!ociChar.oiCharInfo.enableIK) || // IK總開關是關的 -> 需要校正
                    (ociChar.oiCharInfo.enableIK && !IsIkChainActive(ociChar, FullBodyBipedChain.RightArm)); // IK總開關是關的但右手IK是關的 -> 需要校正

                // Log.LogInfo("ociChar.oiCharInfo.enableFK:" + ociChar.oiCharInfo.enableFK);
                // Log.LogInfo("ociChar.oiCharInfo.enableIK:" + ociChar.oiCharInfo.enableIK);
                // Log.LogInfo("IsFkPartActive(ociChar, OIBoneInfo.BoneGroup.Body):" + IsFkPartActive(ociChar, OIBoneInfo.BoneGroup.Body));
                // Log.LogInfo("IsIkChainActive(ociChar, FullBodyBipedChain.LeftArm):" + IsIkChainActive(ociChar, FullBodyBipedChain.LeftArm));
                // Log.LogInfo("IsIkChainActive(ociChar, FullBodyBipedChain.RightArm):" + IsIkChainActive(ociChar, FullBodyBipedChain.RightArm));
                // Log.LogInfo("---");
                // Log.LogInfo("shouldApplyAPoseCorrection:" + shouldApplyAPoseCorrectionL);
                // Log.LogInfo("shouldApplyAPoseCorrection:" + shouldApplyAPoseCorrectionR);

                if (shouldApplyAPoseCorrectionL)
                {
                    // 根據骨骼名稱應用 T-Pose 到 A-Pose 的旋轉校正
                    switch (mapInfo.MmdName)
                    {
                        case "左肩":
                            relativeRot *= Quaternion.Euler(0, 0, -14.0f);
                            break;
                        case "左腕":
                            relativeRot *= Quaternion.Euler(0, 0, -21.0f);
                            break;
                    }
                }
                if (shouldApplyAPoseCorrectionR)
                {
                    // 根據骨骼名稱應用 T-Pose 到 A-Pose 的旋轉校正
                    switch (mapInfo.MmdName)
                    {
                        case "右肩":
                            relativeRot *= Quaternion.Euler(0, 0, 14.0f);
                            break;
                        case "右腕":
                            relativeRot *= Quaternion.Euler(0, 0, 21.0f);
                            break;
                    }
                }

                // const float scaleFactor = 12.5f;
                // frame.Position = new Vector3(-localPos.x * scaleFactor, localPos.y * scaleFactor, -localPos.z * scaleFactor);
                frame.Position = new Vector3(0, 0, 0);
                frame.Rotation = new Quaternion(-relativeRot.x, relativeRot.y, -relativeRot.z, relativeRot.w);

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
            bool shouldIncludeInReport = RequiredBoneNameParts.Any(requiredPart => bone.name.Contains(requiredPart));

            // 如果符合白名單，則將其資訊添加到報告中
            if (shouldIncludeInReport)
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

                string boneInfo = $"{indent}{displayName}                    P{bone.localPosition:F3} R{bone.localEulerAngles:F3} S{bone.localScale:F3}";
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
