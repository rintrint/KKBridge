using BepInEx;
using BepInEx.Logging;
using Studio;
using System.Linq;
using System.Text;
using UnityEngine;

// 將所有程式碼包裹在 KKBridge 這個命名空間中
namespace KKBridge
{
    // BepInEx 插件的標頭資訊
    [BepInPlugin(
        "com.rint.kkbridge",     // 插件的唯一 GUID
        "KKBridge Plugin",       // 插件名稱
        "1.0.0"                  // 插件版本
    )]
    public class KKBridgePlugin : BaseUnityPlugin
    {
        // 用於在控制台輸出日誌的實例
        internal static ManualLogSource Log;

        // Awake() 是插件的入口，當插件被載入時會被 BepInEx 自動呼叫
        private void Awake()
        {
            Log = base.Logger;
            Log.LogInfo("KKBridge Plugin loaded successfully! Press F7 to list characters and bones.");
        }

        // Update() 方法會在遊戲的每一影格被呼叫
        private void Update()
        {
            // 偵測玩家是否按下了 F7 鍵
            if (Input.GetKeyDown(KeyCode.F7))
            {
                Log.LogInfo("F7 key pressed, attempting to list characters and bones...");
                ListCharactersAndBones();
            }
        }

        /// <summary>
        /// 主要功能函式：尋找並列出所有角色的骨骼
        /// </summary>
        private void ListCharactersAndBones()
        {
            // 1. 獲取工作室的單例實例，這是存取場景中所有物件的入口
            var studioInstance = Singleton<Studio.Studio>.Instance;
            if (studioInstance == null || studioInstance.dicObjectCtrl == null)
            {
                Log.LogError("Could not get Studio instance. Are you in the main studio scene?");
                return;
            }

            // 2. 從所有物件中，篩選出類型為「角色」(OCIChar) 的物件
            var characters = studioInstance.dicObjectCtrl.Values.OfType<OCIChar>();

            if (!characters.Any())
            {
                Log.LogWarning("No characters found in the scene.");
                return;
            }

            Log.LogInfo($"Found {characters.Count()} character(s) in the scene.");

            // 3. 遍歷找到的每一個角色
            foreach (var ociChar in characters)
            {
                ChaControl chaCtrl = ociChar.charInfo;
                if (chaCtrl == null) continue;

                Log.LogInfo($"========== Character: {chaCtrl.chaFile.parameter.fullname} ==========");

                // 4. 使用能深入搜尋的 FindDeepChild 函式來找到角色骨架的根物件
                Transform boneRoot = FindDeepChild(chaCtrl.transform, "p_cf_body_bone");

                if (boneRoot == null)
                {
                    Log.LogWarning($"Could not find bone root 'p_cf_body_bone' for character {chaCtrl.chaFile.parameter.fullname}.");
                    continue;
                }

                // 5. 使用遞迴方法來遍歷並印出所有骨骼
                var boneListBuilder = new StringBuilder();
                TraverseBones(boneRoot, "", boneListBuilder);
                Log.LogInfo(boneListBuilder.ToString());
            }
        }

        /// <summary>
        /// 遞迴函式，用於遍歷一個 Transform 物件和它所有的子物件，並建立樹狀結構字串
        /// </summary>
        /// <param name="bone">當前要處理的骨骼 Transform</param>
        /// <param name="indent">用於視覺化層級的縮排字串</param>
        /// <param name="builder">用於高效建立完整列表的 StringBuilder</param>
        private void TraverseBones(Transform bone, string indent, StringBuilder builder)
        {
            if (bone == null) return;

            // 在骨骼名稱後方，附加本地座標、旋轉和縮放的資訊
            string boneInfo = $"{indent}{bone.name} | P: {bone.localPosition:F3} | R: {bone.localEulerAngles:F3} | S: {bone.localScale:F3}";
            builder.AppendLine(boneInfo);

            // 遍歷當前骨骼的所有子骨骼，並為每一個子骨骼呼叫自己，同時增加縮排
            foreach (Transform child in bone)
            {
                TraverseBones(child, indent + "  ", builder);
            }
        }

        /// <summary>
        /// 輔助函式：遞迴搜尋一個父物件下的所有層級，直到找到指定名稱的子物件
        /// </summary>
        /// <param name="parent">要開始搜尋的父物件</param>
        /// <param name="targetName">要尋找的子物件名稱</param>
        /// <returns>找到的 Transform 物件，如果沒找到則返回 null</returns>
        public static Transform FindDeepChild(Transform parent, string targetName)
        {
            // 檢查當前物件的所有直接子物件
            foreach (Transform child in parent)
            {
                // 如果找到目標，立即返回
                if (child.name == targetName)
                {
                    return child;
                }

                // 如果沒找到，則對這個子物件進行遞迴搜尋
                Transform result = FindDeepChild(child, targetName);
                if (result != null)
                {
                    return result; // 如果在更深層級找到了，就將結果一路傳回去
                }
            }
            // 如果遍歷完所有子物件都沒找到，返回 null
            return null;
        }
    }
}
