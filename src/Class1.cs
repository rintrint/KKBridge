// 引用 BepInEx 核心功能所需的基本命名空間
using BepInEx;
using BepInEx.Logging;

// 將您的所有程式碼包裹在 KKBridge 這個命名空間中
namespace KKBridge
{
    // 這是插件的進入點，所有 BepInEx 插件都以此為基礎
    [BepInPlugin(
        "com.rint.kkbridge",     // 插件的唯一 GUID，建議與 namespace 相關
        "KKBridge Plugin",       // 插件名稱
        "1.0.0"                  // 插件版本
    )]
    public class KKBridgePlugin : BaseUnityPlugin
    {
        // 建立一個靜態的日誌來源實例，方便在插件的任何地方調用
        internal static ManualLogSource Log;

        // Awake() 是 BepInEx 插件的入口方法
        // 當插件被載入時，這個方法會被 BepInEx 自動呼叫
        private void Awake()
        {
            // 1. 初始化日誌來源，讓它與您的插件關聯起來
            Log = base.Logger;

            // 2. 使用日誌來源輸出訊息到控制台
            Log.LogInfo("KKBridge Plugin loaded successfully!");
        }
    }
}
