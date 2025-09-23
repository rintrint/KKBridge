using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public ICollection<VmdIkEnable> IkEnables { get; }

        public VmdIkFrame()
        {
            FrameNumber = 0;
            Display = true;
            IkEnables = new List<VmdIkEnable>();
        }
    }

    public static class VmdExporter
    {
        public static void Export(ICollection<VmdMotionFrame> frames, ICollection<VmdIkFrame> ikFrames, string modelName, string filePath)
        {
            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }
            if (ikFrames == null)
            {
                throw new ArgumentNullException(nameof(ikFrames));
            }

            var shiftJisEncoding = Encoding.GetEncoding("Shift_JIS");

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
}
