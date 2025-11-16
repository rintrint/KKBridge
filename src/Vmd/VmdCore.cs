using BepInEx.Logging;
using KKBridge.Extensions;
using SimpleJSON;
using Studio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace KKBridge.Vmd
{
    #region VMD 工具類別 (VMD Helper Classes)

    /// <summary>
    /// 儲存單一骨骼完整映射資訊的數據結構
    /// </summary>
    public class BoneMapEntry
    {
        public string KkName { get; }
        public string MmdName { get; }
        public string MmdParentName { get; } // 它在MMD裡的Parent是誰
        public Quaternion RestPoseCorrection { get; }
        public Quaternion CoordinateConversion { get; }

        public BoneMapEntry(string kkName, string mmdName, string mmdParentName, Quaternion restPoseCorrection, Quaternion coordinateConversion)
        {
            KkName = kkName;
            MmdName = mmdName;
            MmdParentName = mmdParentName; // 全ての親的Parent設為null
            RestPoseCorrection = restPoseCorrection;
            CoordinateConversion = coordinateConversion;
        }

        public BoneMapEntry(string kkName, string mmdName, string mmdParentName)
            : this(kkName, mmdName, mmdParentName, Quaternion.identity, Quaternion.identity) { }
    }

    public static class BoneMapper
    {
        // ================================================================
        // 1. 單一事實來源 (Single Source of Truth)
        // ================================================================
        private static readonly List<BoneMapEntry> _boneMapDatabase = new List<BoneMapEntry>
        {
            // 親子關係是
            // chaF/M_001 BodyTop p_cf_body_bone cf_j_root cf_n_height cf_j_hips ...
            // 選擇cf_j_hips的直接Parent:"cf_n_height"來映射到"全ての親"，以獲得最可靠的結果，避免中間骨骼因任何意外有局部變換導致計算出現偏差
            new BoneMapEntry( "cf_n_height",        "全ての親", null,       new Quaternion( 0.000000022f, 0.000000000f, 0.000000000f, 1.000000000f), new Quaternion( 0.000000000f, 0.000000000f, 1.000000000f, 0.000000000f)),
            new BoneMapEntry( "cf_j_hips",          "センター", "全ての親", new Quaternion(-0.000000044f, 0.000000044f, 0.000000000f, 1.000000000f), new Quaternion( 0.000000000f,-1.000000000f, 0.000000000f,-0.000000044f)),
            new BoneMapEntry( "EyeTargetL",         "左目",     "頭",       new Quaternion( 0.000000000f, 0.000000000f, 0.000000000f, 1.000000000f), new Quaternion( 0.561309993f,-0.577732265f,-0.416540653f, 0.421486050f)),
            new BoneMapEntry( "EyeTargetR",         "右目",     "頭",       new Quaternion( 0.000000000f, 0.000000000f, 0.000000000f, 1.000000000f), new Quaternion(-0.416540563f, 0.421486050f, 0.561309993f,-0.577732265f)),
            new BoneMapEntry( "cf_j_neck",          "首",       "上半身2",  new Quaternion(-0.076898634f, 0.000000000f, 0.000000000f, 0.997038901f), new Quaternion( 0.000000000f, 0.004926377f, 0.999987841f, 0.000000000f)),
            new BoneMapEntry( "cf_j_head",          "頭",       "首",       new Quaternion( 0.004926383f, 0.000000000f, 0.000000000f, 0.999987900f), new Quaternion( 0.000000000f, 0.000000000f, 1.000000000f, 0.000000000f)),
            new BoneMapEntry( "cf_j_spine01",       "上半身",   "センター", new Quaternion( 0.021985279f, 0.000000044f, 0.000000001f, 0.999758303f), new Quaternion( 0.000000000f,-0.021985229f, 0.999758303f, 0.000000000f)),
            new BoneMapEntry( "cf_j_spine03",       "上半身2",  "上半身",   new Quaternion( 0.050040327f, 0.000000000f, 0.000000000f, 0.998747230f), new Quaternion( 0.000000000f,-0.071985893f, 0.997405648f, 0.000000000f)),
            new BoneMapEntry( "cf_j_waist01",       "下半身",   "センター", new Quaternion(-0.004499444f,-0.000000044f, 0.000000000f, 0.999989867f), new Quaternion( 0.000000000f,-0.999989927f, 0.004499471f, 0.000000000f)),
            new BoneMapEntry( "cf_j_shoulder_L",    "左肩",     "上半身2",  new Quaternion(-0.124162689f,-0.072256744f, 0.015808962f, 0.989501238f), new Quaternion( 0.437412977f,-0.555561662f,-0.429743975f,-0.561552525f)),
            new BoneMapEntry( "cf_j_shoulder_R",    "右肩",     "上半身2",  new Quaternion( 0.124163061f, 0.072277412f, 0.015806366f, 0.989499748f), new Quaternion(-0.429734826f,-0.561540902f, 0.437421978f,-0.555573404f)),
            new BoneMapEntry( "cf_j_arm00_L",       "左腕",     "左肩",     new Quaternion(-0.199890256f,-0.002602628f,-0.000835290f, 0.979814529f), new Quaternion( 0.316989094f,-0.632075906f,-0.309348643f,-0.635847032f)),
            new BoneMapEntry( "cf_j_arm00_R",       "右腕",     "右肩",     new Quaternion( 0.199890271f, 0.002579150f,-0.000838769f, 0.979814529f), new Quaternion(-0.309349507f,-0.635848999f, 0.316988230f,-0.632073879f)),
            new BoneMapEntry( "cf_j_forearm01_L",   "左ひじ",   "左腕",     new Quaternion( 0.001809260f,-0.000001746f,-0.015104761f, 0.999884307f), new Quaternion( 0.308556050f,-0.636232197f,-0.320060164f,-0.630526245f)),
            new BoneMapEntry( "cf_j_forearm01_R",   "右ひじ",   "右腕",     new Quaternion(-0.001809394f, 0.000006890f,-0.015104770f, 0.999884307f), new Quaternion(-0.320059568f,-0.630524814f, 0.308556795f,-0.636233449f)),
            new BoneMapEntry( "cf_j_hand_L",        "左手首",   "左ひじ",   new Quaternion(-0.019762015f,-0.000251624f, 0.030722883f, 0.999332547f), new Quaternion( 0.315517008f,-0.632811487f,-0.287824154f,-0.645876110f)),
            new BoneMapEntry( "cf_j_hand_R",        "右手首",   "右ひじ",   new Quaternion( 0.019762039f, 0.000248441f, 0.030722860f, 0.999332547f), new Quaternion(-0.287824422f,-0.645876825f, 0.315516800f,-0.632810652f)),
            new BoneMapEntry( "cf_j_thigh00_L",     "左足",     "下半身",   new Quaternion(-0.027437627f,-0.007275635f, 0.007340898f, 0.999570072f), new Quaternion( 0.007308564f,-0.999436498f, 0.031934876f, 0.007308592f)),
            new BoneMapEntry( "cf_j_thigh00_R",     "右足",     "下半身",   new Quaternion(-0.027437627f, 0.007275635f,-0.007340898f, 0.999570072f), new Quaternion(-0.007308564f,-0.999436498f, 0.031934876f,-0.007308592f)),
            new BoneMapEntry( "cf_j_leg01_L",       "左ひざ",   "左足",     new Quaternion(-0.030566327f, 0.003512908f,-0.004206586f, 0.999517739f), new Quaternion( 0.003436361f,-0.998034835f, 0.062473599f, 0.003436406f)),
            new BoneMapEntry( "cf_j_leg01_R",       "右ひざ",   "右足",     new Quaternion(-0.030566327f,-0.003512908f, 0.004206586f, 0.999517739f), new Quaternion(-0.003436361f,-0.998034835f, 0.062473599f,-0.003436406f)),
            new BoneMapEntry( "cf_j_leg03_L",       "左足首",   "左ひざ",   new Quaternion( 0.064457990f, 0.003096197f,-0.047878154f, 0.996766388f), new Quaternion(-0.000000360f,-0.808708966f,-0.588209033f, 0.000000344f)),
            new BoneMapEntry( "cf_j_leg03_R",       "右足首",   "右ひざ",   new Quaternion( 0.064457990f,-0.003096197f, 0.047878154f, 0.996766388f), new Quaternion( 0.000000360f,-0.808708966f,-0.588209033f,-0.000000344f)),
            new BoneMapEntry( "cf_j_toes_L",        "左足先EX", "左足首",   new Quaternion(-0.017452359f, 0.000000000f, 0.000000000f, 0.999847651f), new Quaternion( 0.707106769f,-0.000000031f,-0.000000031f, 0.707106769f)),
            new BoneMapEntry( "cf_j_toes_R",        "右足先EX", "右足首",   new Quaternion(-0.017452359f, 0.000000000f, 0.000000000f, 0.999847651f), new Quaternion( 0.707106769f,-0.000000031f,-0.000000031f, 0.707106769f)),
            new BoneMapEntry( "cf_j_thumb01_L",     "左親指０", "左手首",   new Quaternion(-0.327803075f,-0.631274223f,-0.139706567f, 0.688854218f), new Quaternion(-0.012291201f,-0.706999958f,-0.410868526f,-0.575488508f)),
            new BoneMapEntry( "cf_j_thumb02_L",     "左親指１", "左親指０", new Quaternion( 0.044960849f,-0.025782889f, 0.175409645f, 0.983130336f), new Quaternion( 0.148398563f,-0.693593800f,-0.335095286f,-0.620174646f)),
            new BoneMapEntry( "cf_j_thumb03_L",     "左親指２", "左親指１", new Quaternion( 0.027835172f,-0.006796804f,-0.015709771f, 0.999465942f), new Quaternion( 0.156963393f,-0.690442502f,-0.362956792f,-0.605734289f)),
            new BoneMapEntry( "cf_j_index01_L",     "左人指１", "左手首",   new Quaternion(-0.006491279f,-0.000130614f,-0.020633643f, 0.999766052f), new Quaternion( 0.298231035f,-0.641126335f,-0.296934605f,-0.641751587f)),
            new BoneMapEntry( "cf_j_index02_L",     "左人指２", "左人指１", new Quaternion( 0.005409196f,-0.000092753f,-0.014974540f, 0.999873281f), new Quaternion( 0.292091519f,-0.643964350f,-0.309947193f,-0.635551095f)),
            new BoneMapEntry( "cf_j_index03_L",     "左人指３", "左人指２", new Quaternion( 0.002893500f,-0.000918513f, 0.031723246f, 0.999492109f), new Quaternion( 0.314495474f,-0.634058118f,-0.291223079f,-0.643624187f)),
            new BoneMapEntry( "cf_j_middle01_L",    "左中指１", "左手首",   new Quaternion(-0.018059295f,-0.000522695f,-0.010839730f, 0.999778032f), new Quaternion( 0.297073841f,-0.641626596f,-0.283168316f,-0.647980034f)),
            new BoneMapEntry( "cf_j_middle02_L",    "左中指２", "左中指１", new Quaternion( 0.005511056f,-0.000026596f,-0.015363707f, 0.999866843f), new Quaternion( 0.290755153f,-0.644561946f,-0.296614081f,-0.641889036f)),
            new BoneMapEntry( "cf_j_middle03_L",    "左中指３", "左中指２", new Quaternion( 0.036712706f, 0.001243827f,-0.037268702f, 0.998629928f), new Quaternion( 0.289531291f,-0.642827034f,-0.344155341f,-0.620082438f)),
            new BoneMapEntry( "cf_j_ring01_L",      "左薬指１", "左手首",   new Quaternion(-0.014452359f,-0.000076194f,-0.037964627f, 0.999174595f), new Quaternion( 0.281919628f,-0.648476541f,-0.302937299f,-0.638927639f)),
            new BoneMapEntry( "cf_j_ring02_L",      "左薬指２", "左薬指１", new Quaternion( 0.007118759f,-0.000203615f, 0.004275586f, 0.999965489f), new Quaternion( 0.289292574f,-0.645222366f,-0.304754019f,-0.638061821f)),
            new BoneMapEntry( "cf_j_ring03_L",      "左薬指３", "左薬指２", new Quaternion( 0.017955238f, 0.000932477f,-0.039400462f, 0.999061763f), new Quaternion( 0.274771392f,-0.649948359f,-0.341462880f,-0.620863020f)),
            new BoneMapEntry( "cf_j_little01_L",    "左小指１", "左手首",   new Quaternion(-0.001362753f,-0.000835652f,-0.045042392f, 0.998983800f), new Quaternion( 0.286053449f,-0.647312045f,-0.315497398f,-0.632156730f)),
            new BoneMapEntry( "cf_j_little02_L",    "左小指２", "左小指１", new Quaternion( 0.042786274f, 0.001087955f, 0.001210743f, 0.999082923f), new Quaternion( 0.313279182f,-0.632185340f,-0.342449993f,-0.620423973f)),
            new BoneMapEntry( "cf_j_little03_L",    "左小指３", "左小指２", new Quaternion(-0.073293120f, 0.003686803f,-0.030188911f, 0.996846676f), new Quaternion( 0.246470928f,-0.662461221f,-0.314920157f,-0.633421242f)),
            new BoneMapEntry( "cf_j_thumb01_R",     "右親指０", "右手首",   new Quaternion(-0.126267761f,-0.690293312f,-0.318826735f, 0.637103736f), new Quaternion(-0.399135053f,-0.582401693f,-0.001784034f,-0.708163977f)),
            new BoneMapEntry( "cf_j_thumb02_R",     "右親指１", "右親指０", new Quaternion(-0.055157650f, 0.028019739f, 0.124256089f, 0.990319610f), new Quaternion(-0.362015009f,-0.606614590f, 0.129534423f,-0.695833802f)),
            new BoneMapEntry( "cf_j_thumb03_R",     "右親指２", "右親指１", new Quaternion(-0.019364225f, 0.007150123f, 0.018893316f, 0.999608397f), new Quaternion(-0.362960368f,-0.605733037f, 0.156965420f,-0.690441191f)),
            new BoneMapEntry( "cf_j_index01_R",     "右人指１", "右手首",   new Quaternion(-0.020633664f,-0.999766052f,-0.006490113f, 0.000153542f), new Quaternion(-0.296928465f,-0.641737401f, 0.298238158f,-0.641140103f)),
            new BoneMapEntry( "cf_j_index02_R",     "右人指２", "右人指１", new Quaternion(-0.005408927f, 0.000062573f,-0.014974901f, 0.999873281f), new Quaternion(-0.309949964f,-0.635556221f, 0.292089105f,-0.643959045f)),
            new BoneMapEntry( "cf_j_index03_R",     "右人指３", "右人指２", new Quaternion(-0.002891559f, 0.000931297f, 0.031723637f, 0.999492109f), new Quaternion(-0.291220456f,-0.643621922f, 0.314495802f,-0.634061456f)),
            new BoneMapEntry( "cf_j_middle01_R",    "右中指１", "右手首",   new Quaternion(-0.010839756f,-0.999778092f,-0.018058669f, 0.000523570f), new Quaternion(-0.283168763f,-0.647979975f, 0.297074258f,-0.641626239f)),
            new BoneMapEntry( "cf_j_middle02_R",    "右中指２", "右中指１", new Quaternion(-0.005510436f, 0.000029086f,-0.015363645f, 0.999866784f), new Quaternion(-0.296613365f,-0.641887426f, 0.290755928f,-0.644563437f)),
            new BoneMapEntry( "cf_j_middle03_R",    "右中指３", "右中指２", new Quaternion(-0.036725931f,-0.001243911f,-0.037269317f, 0.998629391f), new Quaternion(-0.344163388f,-0.620076597f, 0.289539963f,-0.642824411f)),
            new BoneMapEntry( "cf_j_ring01_R",      "右薬指１", "右手首",   new Quaternion(-0.037964605f,-0.999174595f,-0.014453045f, 0.000076566f), new Quaternion(-0.302937061f,-0.638928175f, 0.281919181f,-0.648476303f)),
            new BoneMapEntry( "cf_j_ring02_R",      "右薬指２", "右薬指１", new Quaternion(-0.007120486f, 0.000202013f, 0.004275593f, 0.999965489f), new Quaternion(-0.304755330f,-0.638062954f, 0.289292693f,-0.645220578f)),
            new BoneMapEntry( "cf_j_ring03_R",      "右薬指３", "右薬指２", new Quaternion(-0.017960541f,-0.000929163f,-0.039401311f, 0.999061644f), new Quaternion(-0.341467202f,-0.620860159f, 0.274775475f,-0.649947166f)),
            new BoneMapEntry( "cf_j_little01_R",    "右小指１", "右手首",   new Quaternion(-0.045042362f,-0.998983800f,-0.001362931f, 0.000835410f), new Quaternion(-0.315497667f,-0.632157445f, 0.286053091f,-0.647311211f)),
            new BoneMapEntry( "cf_j_little02_R",    "右小指２", "右小指１", new Quaternion(-0.042786356f,-0.001088962f, 0.001210560f, 0.999082923f), new Quaternion(-0.342450678f,-0.620425403f, 0.313278526f,-0.632183909f)),
            new BoneMapEntry( "cf_j_little03_R",    "右小指３", "右小指２", new Quaternion( 0.073282309f,-0.003687347f,-0.030189514f, 0.996847391f), new Quaternion(-0.314928681f,-0.633419812f, 0.246476576f,-0.662456393f)),
        };

        // ================================================================
        // 2. O(1) 查找表 (自動建立)
        // ================================================================
        private static readonly Dictionary<string, BoneMapEntry> _mapByKkName;
        private static readonly Dictionary<string, BoneMapEntry> _mapByMmdName;

        static BoneMapper()
        {
            _mapByKkName = new Dictionary<string, BoneMapEntry>(_boneMapDatabase.Count);
            _mapByMmdName = new Dictionary<string, BoneMapEntry>(_boneMapDatabase.Count);

            foreach (var entry in _boneMapDatabase)
            {
                if (!_mapByKkName.ContainsKey(entry.KkName))
                {
                    _mapByKkName.Add(entry.KkName, entry);
                }
                if (!_mapByMmdName.ContainsKey(entry.MmdName))
                {
                    _mapByMmdName.Add(entry.MmdName, entry);
                }
            }
        }

        // ================================================================
        // 3. 公共訪問介面 (Public API)
        // ================================================================
        public static IEnumerable<BoneMapEntry> GetAllEntries()
        {
            return _boneMapDatabase;
        }

        public static bool TryGetEntryByMmdName(string mmdName, out BoneMapEntry entry)
        {
            return _mapByMmdName.TryGetValue(mmdName, out entry);
        }

        /// <summary>
        /// 查找映射條目。
        /// </summary>
        public static bool TryGetMatchEntry(Transform bone, out BoneMapEntry entry)
        {
            if (bone == null)
            {
                entry = null;
                return false;
            }

            return _mapByKkName.TryGetValue(bone.name, out entry);
        }
    }

    public class VmdBoneFrame
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

        public VmdBoneFrame(string boneName)
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
        public ICollection<VmdIkEnable> IkEnables { get; }

        /// <summary>
        /// 預設 IK 骨骼名稱列表。
        /// </summary>
        public static readonly string[] DefaultIkBoneNames = {
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

        public VmdIkFrame()
        {
            FrameNumber = 0;
            Display = true;
            IkEnables = new List<VmdIkEnable>();
        }

        /// <summary>
        /// 創建一個包含所有預設 IK 骨骼並將其全部禁用的 VMD IK 框架。
        /// </summary>
        /// <returns>一個設定好的 VmdIkFrame 物件。</returns>
        public static VmdIkFrame CreateDefault()
        {
            var frame = new VmdIkFrame { FrameNumber = 0, Display = true };
            foreach (string ikName in DefaultIkBoneNames)
            {
                frame.IkEnables.Add(new VmdIkEnable(ikName, false));
            }
            return frame;
        }
    }

    public static class VmdExporter
    {
        /// <summary>
        /// 通用的關鍵幀後處理與過濾輔助函數，移除多餘的關鍵幀以優化 VMD 檔案。
        /// </summary>
        /// <typeparam name="T">幀的類型 (例如 VmdBoneFrame 或 VmdMorphFrame)。</typeparam>
        /// <param name="allFrames">所有已錄製的原始幀數據集合。</param>
        /// <param name="getNameFunc">獲取名稱的函式：一個用於從幀物件中獲取其名稱 (骨骼名或表情名) 的函式。</param>
        /// <param name="getFrameNumberFunc">獲取幀號的函式：一個用於從幀物件中獲取其幀號的函式。</param>
        /// <param name="areEqualFunc">比較相等的函式：一個用於比較兩個幀的數據是否實質上相等的函式。</param>
        /// <param name="isDefaultFunc">判斷是否為預設值的函式：一個用於判斷一個幀是否處於其預設/零狀態的函式。</param>
        /// <returns>返回經過優化處理後的最終幀列表。</returns>
        public static List<T> PostProcessKeyframes<T>(
            ICollection<T> allFrames,
            Func<T, string> getNameFunc,
            Func<T, uint> getFrameNumberFunc,
            Func<T, T, bool> areEqualFunc,
            Func<T, bool> isDefaultFunc)
        {
            if (allFrames == null || allFrames.Count == 0)
            {
                return new List<T>();
            }

            // 1. 將所有幀按其名稱 (骨骼/表情) 進行分組
            var groupedFrames = allFrames.GroupBy(getNameFunc);

            var finalFrames = new List<T>();
            finalFrames.Capacity = allFrames.Count;

            // 2. 對每一組 (每一個骨骼或表情) 獨立進行過濾
            foreach (var group in groupedFrames)
            {
                // 必須先按幀號排序，後續的邏輯才能正確運作
                var frames = group.OrderBy(f => getFrameNumberFunc(f)).ToList();
                if (frames.Count == 0)
                {
                    continue;
                }

                // --- 過濾規則 1: 移除連續且相同的的中間幀 ---
                var stage1Frames = new List<T>();
                if (frames.Count > 2)
                {
                    stage1Frames.Add(frames.First()); // 總是保留第一幀
                    for (int i = 1; i < frames.Count - 1; ++i)
                    {
                        // 如果一個幀和它前後的幀都相等，它就是可移除的中間幀
                        if (!(areEqualFunc(frames[i - 1], frames[i]) && areEqualFunc(frames[i], frames[i + 1])))
                        {
                            stage1Frames.Add(frames[i]);
                        }
                    }
                    stage1Frames.Add(frames.Last()); // 總是保留最後一幀
                }
                else
                {
                    stage1Frames.AddRange(frames); // 幀數小於等於2，沒有中間幀可移除
                }

                // --- 過濾規則 2: 如果只剩下頭尾兩幀且它們相等，則只保留第一幀 ---
                var stage2Frames = new List<T>();
                if (stage1Frames.Count == 2 && areEqualFunc(stage1Frames[0], stage1Frames[1]))
                {
                    stage2Frames.Add(stage1Frames[0]);
                }
                else
                {
                    stage2Frames.AddRange(stage1Frames);
                }

                // --- 過濾規則 3: 如果最終只剩下一幀，且該幀為預設值，則完全移除這個骨骼/表情的所有幀 ---
                if (stage2Frames.Count == 1 && isDefaultFunc(stage2Frames[0]))
                {
                    // 不做任何事，即刪除這個骨骼/表情的所有關鍵幀
                }
                else
                {
                    finalFrames.AddRange(stage2Frames);
                }
            }

            // 3. 最終結果按幀號排序，確保 VMD 檔案的順序正確
            finalFrames.Sort((a, b) => getFrameNumberFunc(a).CompareTo(getFrameNumberFunc(b)));

            return finalFrames;
        }

        public static void Export(
            ICollection<VmdBoneFrame> boneFrames,
            ICollection<VmdMorphFrame> morphFrames,
            ICollection<VmdIkFrame> ikFrames,
            string modelName,
            string filePath)
        {
            if (boneFrames == null)
            {
                throw new ArgumentNullException(nameof(boneFrames));
            }
            if (ikFrames == null)
            {
                throw new ArgumentNullException(nameof(ikFrames));
            }

            var shiftJisEncoding = Encoding.GetEncoding(932);

            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(fileStream, shiftJisEncoding))
            {
                // 寫入VMD標頭
                writer.Write(shiftJisEncoding.GetBytes("Vocaloid Motion Data 0002\0\0\0\0\0"));

                // 寫入模型名稱
                byte[] modelNameBytes = new byte[20];
                byte[] tempBytes = shiftJisEncoding.GetBytes(modelName);
                Buffer.BlockCopy(tempBytes, 0, modelNameBytes, 0, Math.Min(tempBytes.Length, 20));
                writer.Write(modelNameBytes);

                // 寫入骨骼框架
                writer.Write(boneFrames.Count);
                foreach (var frame in boneFrames.OrderBy(f => f.FrameNumber).ThenBy(f => f.BoneName))
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
                    writer.Write(VmdBoneFrame.DefaultInterpolation);
                }

                // 寫入表情框架
                writer.Write(morphFrames.Count);
                foreach (var frame in morphFrames.OrderBy(f => f.FrameNumber).ThenBy(f => f.MorphName))
                {
                    byte[] nameBytes = new byte[15];
                    tempBytes = shiftJisEncoding.GetBytes(frame.MorphName);
                    Buffer.BlockCopy(tempBytes, 0, nameBytes, 0, Math.Min(tempBytes.Length, 15));
                    writer.Write(nameBytes);
                    writer.Write(frame.FrameNumber);
                    writer.Write(frame.Weight);
                }

                // 寫入相機框架 (空)
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

    /// <summary>
    /// 負責將 Koikatsu 骨骼數據轉換為 VMD 格式的處理器
    /// </summary>
    public class VmdBoneProcessor
    {
        private readonly ManualLogSource _logger;

        public VmdBoneProcessor(ManualLogSource logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 為單一角色收集所有骨骼數據的啟動函數。
        /// </summary>
        public List<VmdBoneFrame> ProcessCharacter(Transform instanceRootTf, Dictionary<string, Transform> boneCache)
        {
            var frameList = new List<VmdBoneFrame>();
            if (instanceRootTf == null)
            {
                _logger?.LogError("Character root transform is null. Skipping.");
                return frameList;
            }
            ProcessBoneRecursive(instanceRootTf, boneCache, frameList);
            return frameList;
        }

        /// <summary>
        /// 遞迴收集骨骼數據。
        /// 採用統一計算邏輯，數據和規則由 BoneMapper 提供。
        /// </summary>
        private void ProcessBoneRecursive(Transform bone, Dictionary<string, Transform> boneCache, List<VmdBoneFrame> frameList)
        {
            if (bone == null) return;

            // 使用智能查找獲取當前骨骼的映射規則
            if (BoneMapper.TryGetMatchEntry(bone, out BoneMapEntry currentEntry))
            {
                var frame = new VmdBoneFrame(currentEntry.MmdName);
                Quaternion finalRot;

                // --- 統一旋轉計算邏輯 ---
                if (currentEntry.MmdParentName == null)
                {
                    // 情況1: 是根骨骼 (父物件名為 null)，直接使用世界旋轉
                    finalRot = bone.rotation;
                }
                else
                {
                    // 情況2: 是子骨骼，查找其父物件並計算相對旋轉
                    if (BoneMapper.TryGetEntryByMmdName(currentEntry.MmdParentName, out var parentEntry) &&
                        boneCache.TryGetValue(parentEntry.KkName, out var parentTf))
                    {
                        // 應用通用公式
                        finalRot = Quaternion.Inverse(parentTf.rotation) * bone.rotation;
                    }
                    else
                    {
                        // Fallback: 如果因故找不到父物件，記錄日誌並退回使用局部旋轉
                        finalRot = bone.localRotation;
                        _logger?.LogWarning($"Could not find parent transform for '{currentEntry.MmdName}'. Parent MMD name: '{currentEntry.MmdParentName}'. Falling back to localRotation.");
                    }
                }

                // --- 位置計算邏輯 ---
                Vector3 finalPos = Vector3.zero;
                const float mmdScaleFactor = 12.5f;
                if (currentEntry.MmdName == "全ての親")
                {
                    if (boneCache.TryGetValue("cf_j_hips", out var hipsTf))
                    {
                        // 根骨骼用 World Position
                        finalPos = bone.position;

                        // 應用 Pivot 補正
                        // PMX的全ての親在腳後跟(嚴格T-pose姿勢)
                        //  KK的全ての親在腳中間(嚴格T-pose姿勢)
                        float tposeOffsetZ = 0.055f * hipsTf.lossyScale.z;
                        Vector3 v = new Vector3(0, 0, tposeOffsetZ);
                        finalPos += (finalRot * v) - v;

                        finalPos = new Vector3(-finalPos.x, finalPos.y, -finalPos.z) * mmdScaleFactor;
                    }
                }
                else if (currentEntry.MmdName == "センター")
                {
                    // "センター"的位置是相對於"全ての親"的
                    if (boneCache.TryGetValue("cf_n_height", out var rootTf))
                    {
                        finalPos = Quaternion.Inverse(rootTf.rotation) * (bone.position - rootTf.position);

                        // 正規化: Koikatsu的"センター"比"全ての親"高1.1435 * scale
                        // 減去 Koikatsu 靜止姿勢的基礎偏移，讓 finalPos 只剩下「VMD需要的純粹的動畫位移」
                        float tposeOffsetY = 1.1435f * bone.lossyScale.y;
                        float tposeOffsetZ = 0.055f * bone.lossyScale.z;
                        finalPos -= new Vector3(0, tposeOffsetY, tposeOffsetZ);

                        // 應用 Pivot 補正
                        // 進行旋轉中心 (Pivot) 的補正計算，補正因旋轉中心不同而產生的動態位移
                        // PMX的センター高度:0.64
                        //  KK的センター高度:1.1435 * scale
                        // PMX的センター在腳後跟(嚴格T-pose姿勢)
                        //  KK的センター在腳中間(嚴格T-pose姿勢)
                        // Vy = P_pmx(0.64) - P_kk(1.1435 * scale)
                        // 不同PMX的模型センター高度略微不同，使用VMD前需要手動調整到0.64
                        Vector3 v = new Vector3(0, 0.64f - tposeOffsetY, -tposeOffsetZ);
                        finalPos += (finalRot * v) - v;

                        finalPos = new Vector3(-finalPos.x, finalPos.y, -finalPos.z) * mmdScaleFactor;
                    }
                }
                // else 其他骨骼只能旋轉不能移動，維持 Vector3.zero

                // --- 統一應用修正數據 ---
                switch (currentEntry.MmdName)
                {
                    case "全ての親":
                        {
                            finalRot = new Quaternion(finalRot.x, -finalRot.y, -finalRot.z, finalRot.w);
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
                                Quaternion rawRotation = finalRot;
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
                                finalRot = Quaternion.Euler(scaledX, scaledY, normalizedZ);
                            }
                            finalRot = finalRot.conjugated();
                            finalRot = ConvertRotation(Quaternion.Euler(90, 0, -90), finalRot);
                            break;
                        }
                    case "首":
                    case "頭":
                    case "上半身":
                    case "上半身2":
                        {
                            finalRot = new Quaternion(finalRot.x, -finalRot.y, -finalRot.z, finalRot.w);
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
                            finalRot = new Quaternion(-finalRot.x, finalRot.y, -finalRot.z, finalRot.w);
                            finalRot = ConvertRotation(Quaternion.Euler(0, 0, -90), finalRot);
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
                            finalRot = ConvertRotation(Quaternion.Euler(0, 90, 90), finalRot);
                            break;
                        }
                    case "左肩":
                    case "左腕":
                    case "左ひじ":
                    case "左手首":
                        {
                            finalRot = ConvertRotation(Quaternion.Euler(0, 90, 90), finalRot);
                            break;
                        }
                    case "右親指０":
                    case "右親指１":
                    case "右親指２":
                        {
                            finalRot = new Quaternion(-finalRot.x, finalRot.y, -finalRot.z, finalRot.w);
                            finalRot = ConvertRotation(Quaternion.Euler(0, 0, 90), finalRot);
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
                            finalRot = new Quaternion(-finalRot.x, -finalRot.y, finalRot.z, finalRot.w);
                            finalRot = ConvertRotation(Quaternion.Euler(0, -90, 90), finalRot);
                            break;
                        }
                    case "右肩":
                    case "右腕":
                    case "右ひじ":
                    case "右手首":
                        {
                            finalRot = new Quaternion(-finalRot.x, finalRot.y, -finalRot.z, finalRot.w);
                            finalRot = ConvertRotation(Quaternion.Euler(0, -90, 90), finalRot);
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
                            finalRot = ConvertRotation(Quaternion.Euler(90, 0, 0), finalRot);
                            break;
                        }
                    case "左足先EX":
                    case "右足先EX":
                        {
                            finalRot = new Quaternion(-finalRot.x, -finalRot.y, finalRot.z, finalRot.w);
                            finalRot = ConvertRotation(Quaternion.Euler(90, 0, 0), finalRot);
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }

                finalRot = currentEntry.RestPoseCorrection * finalRot;
                finalRot = ConvertRotation(currentEntry.CoordinateConversion, finalRot);

                frame.Rotation = finalRot.normalized();
                frame.Position = finalPos;
                frameList.Add(frame);
            }

            // 遞迴遍歷所有子物件
            // 遞迴遍歷所有子物件
            foreach (Transform child in bone)
            {
                ProcessBoneRecursive(child, boneCache, frameList);
            }
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
        /// 將四元數旋轉轉換為VMD格式的旋轉
        /// 使用四元數運算進行坐標變換
        /// </summary>
        /// <param name="transformXyzw">變換四元數，用於坐標變換</param>
        /// <param name="rotationXyzw">原始旋轉四元數</param>
        /// <returns>轉換後的VMD旋轉四元數</returns>
        public static Quaternion ConvertRotation(Quaternion transformXyzw, Quaternion rotationXyzw)
        {
            // 坐標變換
            // C = A * B * A.Inverse();
            // 優化: 單位四元數共軛和逆等價，可用共軛取代逆，更高效。
            // C = A * B * A.conjugated();
            Quaternion result = transformXyzw * rotationXyzw * transformXyzw.conjugated();
            return result;
        }
    }
    #endregion

    // 儲存單一 Morph 目標的資訊
    public class MorphMapping
    {
        public string Morph { get; }
        public float MappingWeight { get; }

        public MorphMapping(string morph, float weight)
        {
            Morph = morph;
            MappingWeight = weight;
        }
    }

    // 儲存整個設定檔的結構
    public class MorphMappingConfig
    {
        // 字典的值是一個 MorphMapping 的列表 (List)
        public Dictionary<string, List<MorphMapping>> BsToMorphMappings { get; set; }
        public Dictionary<string, Dictionary<string, string>> PtnToMorphMappings { get; set; }
    }

    public class VmdMorphFrame
    {
        public string MorphName { get; set; }
        public uint FrameNumber { get; set; }
        public float Weight { get; set; }
    }

    public class VmdMorphProcessor
    {
        private readonly ManualLogSource _logger;
        private MorphMappingConfig _config;
        private Dictionary<string, SkinnedMeshRenderer> _rendererCache = new Dictionary<string, SkinnedMeshRenderer>();
        private string _settingsFilePath;

        public VmdMorphProcessor(ManualLogSource logger = null)
        {
            _logger = logger;
            InitializeAndLoad();
        }

        /// <summary>
        /// 初始化設定檔路徑並載入設定
        /// </summary>
        private void InitializeAndLoad()
        {
            try
            {
                // 決定設定檔在使用者端的最終路徑
                // 通常是與插件 DLL 相同的目錄
                string pluginDirectory = Path.Combine(BepInEx.Paths.PluginPath, "KKBridge");
                _settingsFilePath = Path.Combine(pluginDirectory, "config.json");

                // 確保設定檔存在 (如果不存在，就從內嵌資源創建一個)
                EnsureSettingsFileExists();

                // 載入設定
                LoadMappings();
            }
            catch (System.Exception ex)
            {
                _logger?.LogError($"An error occurred during settings initialization: {ex.ToString()}");
            }
        }

        /// <summary>
        /// 確保設定檔存在於目標路徑。如果不存在，則從 DLL 的內嵌資源中創建它。
        /// </summary>
        private void EnsureSettingsFileExists()
        {
            if (File.Exists(_settingsFilePath))
            {
                // 檔案已存在，無需任何操作
                return;
            }

            _logger?.LogInfo($"Configuration file not found. Creating a default one at: {_settingsFilePath}");

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = "KKBridge.Config.config.json";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        _logger?.LogError($"Could not find the embedded resource: '{resourceName}'. Make sure the file's Build Action is set to 'Embedded Resource' and the name is correct.");
                        return;
                    }

                    using (var reader = new StreamReader(stream))
                    {
                        string defaultSettings = reader.ReadToEnd();
                        File.WriteAllText(_settingsFilePath, defaultSettings);
                        _logger?.LogInfo("Default configuration file created successfully.");
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger?.LogError($"Failed to create default configuration file: {ex.ToString()}");
            }
        }

        /// <summary>
        /// 從外部設定檔載入並解析映射。
        /// </summary>
        private void LoadMappings()
        {
            if (!File.Exists(_settingsFilePath))
            {
                _logger?.LogError($"Load failed: Settings file '{_settingsFilePath}' does not exist.");
                _config = new MorphMappingConfig { BsToMorphMappings = new Dictionary<string, List<MorphMapping>>() };
                return;
            }

            try
            {
                string jsonContent = File.ReadAllText(_settingsFilePath);
                if (string.IsNullOrEmpty(jsonContent))
                {
                    _logger?.LogError("Load failed: config.json is empty.");
                    _config = new MorphMappingConfig { BsToMorphMappings = new Dictionary<string, List<MorphMapping>>() };
                    return;
                }

                // --- 使用 SimpleJSON 進行解析 ---
                var jsonNode = JSON.Parse(jsonContent);

                if (jsonNode == null || !jsonNode.HasKey("BsToMorphMappings") || !jsonNode["BsToMorphMappings"].IsObject)
                {
                    _logger?.LogWarning("Failed to parse mappings from config.json. 'BsToMorphMappings' key not found or it's not an object.");
                    _config = new MorphMappingConfig { BsToMorphMappings = new Dictionary<string, List<MorphMapping>>() };
                    return;
                }

                // 以下是新的解析邏輯
                var newMappingsDict = new Dictionary<string, List<MorphMapping>>();
                if (jsonNode.HasKey("BsToMorphMappings") && jsonNode["BsToMorphMappings"].IsObject)
                {
                    JSONObject mappingsObject = jsonNode["BsToMorphMappings"].AsObject;

                    // 遍歷所有 BlendShape (例如 "eye_face.f00_def_cl")
                    foreach (KeyValuePair<string, JSONNode> kvp in mappingsObject)
                    {
                        string kkBlendshapeName = kvp.Key;
                        JSONNode valueNode = kvp.Value;

                        // 預期值永遠是一個陣列
                        if (valueNode.IsArray)
                        {
                            var mappingList = new List<MorphMapping>();
                            // 遍歷陣列中的每一個目標 Morph 物件
                            foreach (JSONNode itemNode in valueNode.AsArray)
                            {
                                if (itemNode.IsObject)
                                {
                                    JSONObject mappingInfo = itemNode.AsObject;
                                    if (mappingInfo.HasKey("Morph") && mappingInfo.HasKey("MappingWeight"))
                                    {
                                        string pmxMorphName = mappingInfo["Morph"].Value;
                                        float weight = mappingInfo["MappingWeight"].AsFloat;

                                        // 只有 Morph 名稱不是空字串時才加入
                                        if (!string.IsNullOrEmpty(pmxMorphName))
                                        {
                                            mappingList.Add(new MorphMapping(pmxMorphName, weight));
                                        }
                                    }
                                }
                            }

                            // 如果這個 BlendShape 至少有一個有效的目標 Morph，才將其加入字典
                            if (mappingList.Count > 0 && !newMappingsDict.ContainsKey(kkBlendshapeName))
                            {
                                newMappingsDict.Add(kkBlendshapeName, mappingList);
                            }
                        }
                    }
                }
                else
                {
                    _logger?.LogWarning("'BsToMorphMappings' key not found in config.json.");
                }

                var newPtnMappings = new Dictionary<string, Dictionary<string, string>>();
                if (jsonNode.HasKey("PtnToMorphMappings") && jsonNode["PtnToMorphMappings"].IsObject)
                {
                    JSONObject ptnMappingsObject = jsonNode["PtnToMorphMappings"].AsObject;

                    // 遍歷 "Eyebrow", "Eye", "Mouth"
                    foreach (KeyValuePair<string, JSONNode> categoryKvp in ptnMappingsObject)
                    {
                        string categoryName = categoryKvp.Key; // "Eyebrow", "Eye", "Mouth"
                        if (categoryKvp.Value.IsObject)
                        {
                            var innerMap = new Dictionary<string, string>();
                            JSONObject numberMapObject = categoryKvp.Value.AsObject;

                            // 遍歷 "1": "真面目", "30": "はぅ"
                            foreach (KeyValuePair<string, JSONNode> ptnKvp in numberMapObject)
                            {
                                string ptnNumberString = ptnKvp.Key; // "1", "30"
                                string vmdMorphName = ptnKvp.Value.Value; // "真面目", "はぅ"

                                if (!string.IsNullOrEmpty(vmdMorphName))
                                {
                                    innerMap[ptnNumberString] = vmdMorphName;
                                }
                            }
                            newPtnMappings[categoryName] = innerMap;
                        }
                    }
                    _logger?.LogInfo($"Successfully loaded {newPtnMappings.Count} Ptn mapping categories.");
                }
                else
                {
                    _logger?.LogWarning("'PtnToMorphMappings' key not found in config.json. Ptn-based morphs will be disabled.");
                }

                _config = new MorphMappingConfig
                {
                    BsToMorphMappings = newMappingsDict,
                    PtnToMorphMappings = newPtnMappings,
                };
                _logger?.LogInfo($"Successfully loaded {_config.BsToMorphMappings.Count} BsToMorphMappings.");
                _logger?.LogInfo($"Successfully loaded {_config.PtnToMorphMappings.Count} PtnToMorphMappings.");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"An unexpected error occurred while parsing config.json: {ex.ToString()}");
                // 發生錯誤時初始化為空
                _config = new MorphMappingConfig
                {
                    BsToMorphMappings = new Dictionary<string, List<MorphMapping>>(),
                    PtnToMorphMappings = new Dictionary<string, Dictionary<string, string>>()
                };
            }
        }

        /// <summary>
        /// 為單一角色在當前影格生成所有 VMD 表情數據
        /// </summary>
        public List<VmdMorphFrame> ProcessCharacter(OCIChar ociChar, uint frameNumber)
        {
            var frameList = new List<VmdMorphFrame>();
            if (_config == null || ociChar == null || ociChar.charInfo == null) return frameList;

            var tempFrames = new Dictionary<string, VmdMorphFrame>();

            // --- 步驟 1: 處理 Ptn 映射並設定優先級旗標 ---
            bool eyePtnActive = false;
            bool eyebrowPtnActive = false;
            bool mouthPtnActive = false;

            if (_config.PtnToMorphMappings != null)
            {
                // 獲取當前的 Ptn 編號
                int eyebrowPtn = ociChar.charInfo.GetEyebrowPtn();
                int eyesPtn = ociChar.charInfo.GetEyesPtn();
                int mouthPtn = ociChar.charInfo.GetMouthPtn();

                // 查找並應用 眉 (Eyebrow)
                if (_config.PtnToMorphMappings.TryGetValue("Eyebrow", out var eyebrowMap) &&
                    eyebrowMap.TryGetValue(eyebrowPtn.ToString(), out var eyebrowMorphName))
                {
                    tempFrames[eyebrowMorphName] = new VmdMorphFrame
                    {
                        MorphName = eyebrowMorphName,
                        FrameNumber = frameNumber,
                        Weight = 1.0f // Ptn 映射總是 1.0
                    };
                    eyebrowPtnActive = true; // *** 設定旗標 ***
                }

                // 查找並應用 目 (Eye)
                if (_config.PtnToMorphMappings.TryGetValue("Eye", out var eyeMap) &&
                    eyeMap.TryGetValue(eyesPtn.ToString(), out var eyeMorphName))
                {
                    if (tempFrames.TryGetValue(eyeMorphName, out var existingFrame))
                    {
                        existingFrame.Weight = Mathf.Max(existingFrame.Weight, 1.0f);
                    }
                    else
                    {
                        tempFrames[eyeMorphName] = new VmdMorphFrame
                        {
                            MorphName = eyeMorphName,
                            FrameNumber = frameNumber,
                            Weight = 1.0f
                        };
                    }
                    eyePtnActive = true; // *** 設定旗標 ***
                }

                // 查找並應用 口 (Mouth)
                if (_config.PtnToMorphMappings.TryGetValue("Mouth", out var mouthMap) &&
                    mouthMap.TryGetValue(mouthPtn.ToString(), out var mouthMorphName))
                {
                    if (tempFrames.TryGetValue(mouthMorphName, out var existingFrame))
                    {
                        existingFrame.Weight = Mathf.Max(existingFrame.Weight, 1.0f);
                    }
                    else
                    {
                        tempFrames[mouthMorphName] = new VmdMorphFrame
                        {
                            MorphName = mouthMorphName,
                            FrameNumber = frameNumber,
                            Weight = 1.0f
                        };
                    }
                    mouthPtnActive = true; // *** 設定旗標 ***
                }
            }

            // --- 步驟 2: 處理 BlendShape 映射 (帶優先級檢查) ---
            if (_config.BsToMorphMappings == null)
            {
                frameList.AddRange(tempFrames.Values);
                _rendererCache.Clear();
                return frameList;
            }

            // 為提高效率，先快取一次角色的 SkinnedMeshRenderer
            BuildRendererCache(ociChar.charInfo.transform);

            // 主迴圈遍歷 BlendShape 設定
            foreach (KeyValuePair<string, List<MorphMapping>> mappingEntry in _config.BsToMorphMappings)
            {
                string kkBlendshapeName = mappingEntry.Key;

                // --- 優先級檢查 ---
                if (eyePtnActive && kkBlendshapeName.StartsWith("eye_"))
                {
                    continue; // Ptn 映射優先，跳過這個 Eye BlendShape
                }
                if (eyebrowPtnActive && kkBlendshapeName.StartsWith("mayuge."))
                {
                    continue; // Ptn 映射優先，跳過這個 Eyebrow BlendShape
                }
                if (mouthPtnActive && kkBlendshapeName.StartsWith("kuti_"))
                {
                    continue; // Ptn 映射優先，跳過這個 Mouth BlendShape
                }
                // --- 檢查結束 ---

                List<MorphMapping> targetMorphs = mappingEntry.Value;

                float currentWeight = 0.0f;
                bool blendshapeFound = false;

                foreach (var renderer in _rendererCache.Values)
                {
                    if (renderer == null || renderer.sharedMesh == null) continue;

                    int index = renderer.sharedMesh.GetBlendShapeIndex(kkBlendshapeName);
                    if (index != -1)
                    {
                        // 讀取 KK 角色當前的 BlendShape 權重 (範圍 0-100)
                        currentWeight = renderer.GetBlendShapeWeight(index);
                        blendshapeFound = true;
                        break;
                    }
                }

                if (blendshapeFound)
                {
                    // 將 KK 的權重 (0-100) 轉換為 VMD 的權重 (0.0-1.0)
                    float baseVmdWeight = currentWeight / 100.0f;
                    foreach (var morphInfo in targetMorphs)
                    {
                        string pmxMorphName = morphInfo.Morph;
                        float weightMultiplier = morphInfo.MappingWeight;
                        float finalVmdWeight = baseVmdWeight * weightMultiplier;

                        if (tempFrames.TryGetValue(pmxMorphName, out var existingFrame))
                        {
                            existingFrame.Weight = Mathf.Max(existingFrame.Weight, finalVmdWeight);
                        }
                        else
                        {
                            tempFrames.Add(pmxMorphName, new VmdMorphFrame
                            {
                                MorphName = pmxMorphName,
                                FrameNumber = frameNumber,
                                Weight = finalVmdWeight
                            });
                        }
                    }
                }
            }

            frameList.AddRange(tempFrames.Values);
            _rendererCache.Clear();
            return frameList;
        }

        private void BuildRendererCache(Transform root)
        {
            _rendererCache.Clear();
            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                // 使用 cf_O_face 這種物件名稱作為 key
                if (!_rendererCache.ContainsKey(renderer.name))
                {
                    _rendererCache.Add(renderer.name, renderer);
                }
            }
        }
    }
}
