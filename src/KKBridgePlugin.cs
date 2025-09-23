// ================================================================
// 上下文
//
// === 單位四元數運算 ===
// 共軛等價於逆(必須是單位四元數)
// 共軛:    q = (-x, -y, -z, w);
// XY鏡像:  q = (-x, -y,  z, w);
// XZ鏡像:  q = (-x,  y, -z, w);
// YZ鏡像:  q = ( x, -y, -z, w);
// 坐標映射: C = A * B * A.conjugated();
//
// 共軛反交換律
// (A * B).conjugated() = B.conjugated() * A.conjugated()
// 坐標變換+共軛
// C = (A * B * A.conjugated()).conjugated()
// C = (B * A.conjugated()).conjugated() * A.conjugated()
// C = A * B.conjugated() * A.conjugated()
// C = A * (A * B).conjugated()
//
// Blender  坐標是Z向上 骨骼主軸是Y
// Koikatsu 坐標是Y向上 骨骼主軸是X
//
// Koikatsu的Unity版本太舊，Quaternion少了一些重要方法
// 所以自行實現QuaternionExtensions補足
//
// 專案使用.NET Framework 3.5
//
// 直接讀取FK骨的動作數據，不使用changeAmount
// 如果改成讀取changeAmount就沒辦法烘焙IK到FK了，等於沒有意義
//
// CharaStudio將人物改成T-pose靜止姿勢的方法:
// 關閉IK，打開所有FK將旋轉通通重置為0
// 眼睛難以調整成靜止姿勢，[正面][相機][閃躲][固定][操作]5個選項旋轉都不是0
//
// Koikatsu使用T-pose，且骨骼嚴格平行於三軸
// 右手手指是反的，扭轉成指甲朝下
// 眼睛是貼圖移動，將眼睛骨骼旋轉角度做線性變換得到貼圖坐標偏移
//
// PMX對照模型
// Tda式初音ミク・アペンドVer1.10
// https://bowlroll.net/file/4576
// PMX使用A-pose，部分骨骼有扭轉
//
// 1.在Blender中使用Python腳本預先計算出「T-Pose修正」和「坐標變換」所需的四元數數據
// 2.手動修正Koikatsu特殊軸向問題，套用針對特定骨骼的硬編碼鏡像或旋轉處理
// 3.使用預先計算好的「T-Pose修正」和「坐標變換」數據計算出正確VMD旋轉數據
//
// Koikatsu原始局部空間旋轉數據 -> 手動軸向修正 -> T-Pose修正 -> 坐標變換 -> VMD旋轉數據
//
// Python和C#腳本都手動正確處理了骨骼T-Pose朝向和扭轉問題
//
// Python腳本路徑: tools/generate_bone_map.py
//
// 坐標變換的四種方法:
// 1.歐拉角(有萬向鎖問題)
// 2.四元數運算
// 3.軸角表示法
// 4.旋轉矩陣
//
// 注意Blender四元數是wxyz，Unity和VMD四元數是xyzw
//
// ================================================================

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using KKBridge.Compatibility;
using KKBridge.Extensions;
using KKBridge.Vmd;
using Studio;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KKBridge
{
    /// <summary>
    /// 輔助類別，處理 UGUI 視窗的拖動
    /// </summary>
    public class DraggableWindow : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        private Vector2 _offset;
        public RectTransform TargetRect { get; set; }

        private void Awake()
        {
            // 如果未指定拖動目標，則預設為父物件
            if (TargetRect == null)
            {
                TargetRect = transform.parent.GetComponent<RectTransform>();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(TargetRect, eventData.position, eventData.pressEventCamera, out _offset);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData == null) return;
            if (TargetRect == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(TargetRect.parent as RectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPointerPosition);
            TargetRect.localPosition = localPointerPosition - _offset;
        }
    }

    [BepInPlugin("com.rintrint.kkbridge", "KKBridge", "0.0.1")]
    public class KKBridgePlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        private bool _isExporting = false;
        private GameObject _uiPanel;
        private static Vector2 _uiPanelPosition = new Vector2(-Screen.width * 0.5f + 300, 100f);
        private Text _exportButtonText;
        private ConfigEntry<KeyboardShortcut> _toggleWindowHotkey;
        private ConfigEntry<string> _outputDirectory;

        private void Awake()
        {
            Log = base.Logger;

            // --- 設定快捷鍵等東西 ---
            {
                _toggleWindowHotkey = Config.Bind(
                    "Hotkeys Settings", // 設定的小分類
                    "Toggle Window", // 設定的名稱
                    new KeyboardShortcut(KeyCode.F7), // 預設值
                    "Toggles the KKBridge window." // 滑鼠懸停時顯示的說明文字
                );

                string defaultOutputPath = Path.Combine(BepInEx.Paths.PluginPath, "KKBridge");
                defaultOutputPath = Path.Combine(defaultOutputPath, "out");
                _outputDirectory = Config.Bind(
                    "Export Settings", // 設定的小分類
                    "Output Directory", // 設定的名稱
                    defaultOutputPath, // 預設值
                    "Export destination folder." // 滑鼠懸停時顯示的說明文字
                );
            }

            Log.LogInfo("KKBridge Plugin loaded!");
        }

        /// <summary>
        /// 插件載入後立即執行。這是 Unity 的生命週期函式，在腳本啟用時會被呼叫一次。
        /// </summary>
        private void Start()
        {
            // 強制銷毀熱重載時可能殘留的舊視窗，執行兩次來避免自動打開新視窗
            ToggleKkBridgeWindow();
            ToggleKkBridgeWindow();

            StartCoroutine(CreateKKBridgeButton_Coroutine());
        }

        private void Update()
        {
            if (_toggleWindowHotkey.Value.IsDown())
            {
                ToggleKkBridgeWindow();
            }
        }

        private void ToggleKkBridgeWindow()
        {
            // 在打開/創建視窗前，先檢查並銷毀所有舊的
            GameObject[] existingPanels = GameObject.FindObjectsOfType<GameObject>().Where(go => go.name == "KKBridgeCanvas").ToArray();
            if (existingPanels.Length > 0)
            {
                // 記住最後一個面板的位置
                GameObject lastPanel = existingPanels[existingPanels.Length - 1];
                Transform panelTransform = lastPanel.transform.Find("KKBridgeBorderPanel");
                if (panelTransform != null)
                {
                    RectTransform panelRect = panelTransform.GetComponent<RectTransform>();
                    if (panelRect != null)
                    {
                        // 記住關閉前的視窗位置
                        _uiPanelPosition = panelRect.anchoredPosition;
                    }
                }

                foreach (GameObject panel in existingPanels)
                {
                    Destroy(panel);
                }
                _uiPanel = null; // 清空引用
                return;
            }

            // 如果面板不存在 (或剛被銷毀)，就創建它
            if (_uiPanel == null)
            {
                CreateUIPanel();
            }
            else // 這個分支在當前邏輯下不會被觸發，但保留以備未來修改
            {
                _uiPanel.SetActive(!_uiPanel.activeSelf);
            }
        }

        #region UI 圓角效果相關方法

        /// <summary>
        /// 為 UI 元素替換圓角 Sprite (純視覺，無裁剪功能)
        /// </summary>
        private void ApplyRoundedSprite(GameObject target, float cornerRadius)
        {
            // 獲取或添加 Image 元件
            Image image = target.GetComponent<Image>();
            if (image == null)
            {
                image = target.AddComponent<Image>();
            }

            // 將 Image 的 Sprite 設置為我們創建的圓角 Sprite
            image.sprite = CreateRoundedSprite((int)cornerRadius);
            image.type = Image.Type.Sliced; // 使用 Sliced 模式確保圓角在縮放時不變形
        }

        /// <summary>
        /// 創建圓角 Sprite
        /// </summary>
        private Sprite CreateRoundedSprite(int cornerRadius)
        {
            int size = cornerRadius * 4;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            // 創建像素陣列
            Color32[] pixels = new Color32[size * size];

            // 填充圓角矩形
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool shouldFill = true;

                    // 檢查四個角
                    Vector2 center = Vector2.zero;
                    float distance = 0f;

                    // 左下角
                    if (x < cornerRadius && y < cornerRadius)
                    {
                        center = new Vector2(cornerRadius - 0.5f, cornerRadius - 0.5f);
                        distance = Vector2.Distance(new Vector2(x, y), center);
                        shouldFill = distance <= cornerRadius;
                    }
                    // 右下角
                    else if (x >= size - cornerRadius && y < cornerRadius)
                    {
                        center = new Vector2(size - cornerRadius - 0.5f, cornerRadius - 0.5f);
                        distance = Vector2.Distance(new Vector2(x, y), center);
                        shouldFill = distance <= cornerRadius;
                    }
                    // 左上角
                    else if (x < cornerRadius && y >= size - cornerRadius)
                    {
                        center = new Vector2(cornerRadius - 0.5f, size - cornerRadius - 0.5f);
                        distance = Vector2.Distance(new Vector2(x, y), center);
                        shouldFill = distance <= cornerRadius;
                    }
                    // 右上角
                    else if (x >= size - cornerRadius && y >= size - cornerRadius)
                    {
                        center = new Vector2(size - cornerRadius - 0.5f, size - cornerRadius - 0.5f);
                        distance = Vector2.Distance(new Vector2(x, y), center);
                        shouldFill = distance <= cornerRadius;
                    }

                    // 直角部分設為完全透明
                    pixels[y * size + x] = shouldFill ? Color.white : Color.clear;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            // 創建帶有邊框設定的 Sprite
            Vector4 border = new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
        #endregion

        #region UI 創建相關方法

        private void CreateUIPanel()
        {
            Color panelBackgroundColor = new Color32(214, 214, 214, 255);
            Color titleBarColor = new Color32(82, 82, 78, 255);
            Color borderColor = new Color32(60, 60, 60, 255);
            Color buttonBackgroundColor = new Color32(125, 125, 125, 255);
            Color titleTextColor = Color.white;
            Color buttonTextColor = Color.white;

            // 1. 創建 Canvas
            GameObject canvasObj = new GameObject("KKBridgeCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            _uiPanel = canvasObj;

            // 視窗大小
            int panelWidth = 300;
            int panelHeight = 200;
            int titleBarHeight = 35;

            // 2. 創建外邊框 (使用最簡單的父子結構)
            GameObject borderObj = new GameObject("KKBridgeBorderPanel");
            borderObj.transform.SetParent(canvasObj.transform, false);
            Image borderImage = borderObj.AddComponent<Image>();
            borderImage.color = borderColor;
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.sizeDelta = new Vector2(panelWidth, panelHeight);
            borderRect.anchoredPosition = _uiPanelPosition;

            // 3. 創建主面板
            GameObject panelObj = new GameObject("KKBridgePanel");
            panelObj.transform.SetParent(borderObj.transform, false);
            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = panelBackgroundColor;
            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(panelWidth - 6, panelHeight - 6);
            panelRect.anchoredPosition = Vector2.zero;

            // 4. 創建 TitleBar
            GameObject titleBarObj = new GameObject("TitleBar");
            titleBarObj.transform.SetParent(panelObj.transform, false);
            Image titleBarImage = titleBarObj.AddComponent<Image>();
            titleBarImage.color = titleBarColor;
            var dragger = titleBarObj.AddComponent<DraggableWindow>();
            dragger.TargetRect = borderRect;
            RectTransform titleBarRect = titleBarObj.GetComponent<RectTransform>();
            titleBarRect.anchorMin = new Vector2(0, 1);
            titleBarRect.anchorMax = new Vector2(1, 1);
            titleBarRect.pivot = new Vector2(0.5f, 1);
            titleBarRect.sizeDelta = new Vector2(0, titleBarHeight);
            titleBarRect.anchoredPosition = new Vector2(0, 0);

            // 標題文字
            GameObject titleTextObj = new GameObject("TitleText");
            titleTextObj.transform.SetParent(titleBarObj.transform, false);
            Text titleText = titleTextObj.AddComponent<Text>();
            titleText.text = "KKBridge";
            titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleText.resizeTextForBestFit = true;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = titleTextColor;
            RectTransform titleTextRect = titleTextObj.transform as RectTransform;
            titleTextRect.anchorMin = Vector2.zero;
            titleTextRect.anchorMax = Vector2.one;
            titleTextRect.sizeDelta = Vector2.zero;
            titleTextRect.anchoredPosition = Vector2.zero;

            // 5. 創建按鈕
            var exportAnimButtonObj = CreateUIPanelButton("ExportAnimButton", panelObj.transform, "Export Timeline to VMD", new Vector2(0, 70 - titleBarHeight), buttonBackgroundColor, buttonTextColor, () => { StartCoroutine(ExportTimelineAnimation_Coroutine()); });
            _exportButtonText = exportAnimButtonObj.GetComponentInChildren<Text>();

            // 6. 創建關閉按鈕
            GameObject closeButtonObj = new GameObject("CloseButton");
            closeButtonObj.transform.SetParent(panelObj.transform, false);
            Image closeButtonImage = closeButtonObj.AddComponent<Image>();
            closeButtonImage.color = buttonBackgroundColor;
            Button closeButton = closeButtonObj.AddComponent<Button>();
            closeButton.onClick.AddListener(() => { _uiPanel.SetActive(false); });
            RectTransform closeButtonRect = closeButtonObj.GetComponent<RectTransform>();
            closeButtonRect.anchorMin = new Vector2(1, 1);
            closeButtonRect.anchorMax = new Vector2(1, 1);
            closeButtonRect.pivot = new Vector2(1, 1);
            closeButtonRect.sizeDelta = new Vector2(24, 24);
            closeButtonRect.anchoredPosition = new Vector2(-5, -5);

            GameObject closeTextObj = new GameObject("CloseText");
            closeTextObj.transform.SetParent(closeButtonObj.transform, false);
            Text closeText = closeTextObj.AddComponent<Text>();
            closeText.text = "X";
            closeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            closeText.resizeTextForBestFit = true;
            closeText.alignment = TextAnchor.MiddleCenter;
            closeText.color = titleTextColor;
            RectTransform closeTextRect = closeTextObj.transform as RectTransform;
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.sizeDelta = Vector2.zero;

            // 7. 應用圓角 Sprite
            ApplyRoundedSprite(borderObj, 6f); // 邊框圓角 (半徑可自行調整)
            ApplyRoundedSprite(panelObj, 4f); // 主面板圓角
            ApplyRoundedSprite(titleBarObj, 4f); // 標題欄圓角 (只會圓潤頂部，因為底部被面板覆蓋)
            ApplyRoundedSprite(closeButtonObj, 2f); // 關閉按鈕圓角
        }

        /// <summary>
        /// 創建面板按鈕的輔助方法
        /// </summary>
        private GameObject CreateUIPanelButton(string name, Transform parent, string buttonText, Vector2 position, Color bgColor, Color textColor, Action onClickAction)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent, false);

            Image image = buttonObj.AddComponent<Image>();
            image.color = bgColor;

            Button button = buttonObj.AddComponent<Button>();
            button.onClick.AddListener(() => { onClickAction?.Invoke(); });

            RectTransform rect = buttonObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(280, 40);
            rect.anchoredPosition = position;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            Text text = textObj.AddComponent<Text>();
            text.text = buttonText;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.resizeTextForBestFit = true;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = textColor;
            RectTransform textRect = textObj.transform as RectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            // 為按鈕應用圓角 Sprite
            ApplyRoundedSprite(buttonObj, 6f);

            return buttonObj;
        }
        #endregion

        private System.Collections.IEnumerator CreateKKBridgeButton_Coroutine()
        {
            yield return null;

            string buttonPath = "StudioScene/Canvas System Menu/01_Button/KKBridge Button";
            GameObject existingButton = GameObject.Find(buttonPath);
            if (existingButton != null)
            {
                Destroy(existingButton);
                yield return null;
            }

            try
            {
                Transform parentPanel = GameObject.Find("StudioScene/Canvas System Menu/01_Button")?.transform;
                if (parentPanel == null || parentPanel.childCount == 0)
                {
                    Log.LogError("[KKBridgeButton] ERROR: Could not find the UI panel '.../01_Button'.");
                    yield break;
                }

                Texture2D iconTexture = LoadImageFromAssembly("KKBridge.Resources.Icon.png");
                if (iconTexture == null)
                {
                    Log.LogError("[KKBridgeButton] ERROR: Failed to load embedded resource 'KKBridge.Resources.Icon.png' from DLL.");
                    yield break;
                }

                GameObject templateButtonObj = parentPanel.GetChild(0).gameObject;
                GameObject kkBridgeButtonObj = Instantiate(templateButtonObj);
                kkBridgeButtonObj.name = "KKBridge Button";

                Image kkBridgeButtonImage = kkBridgeButtonObj.GetComponent<Image>();
                if (kkBridgeButtonImage != null)
                {
                    kkBridgeButtonImage.sprite = Sprite.Create(iconTexture, new Rect(0, 0, iconTexture.width, iconTexture.height), new Vector2(0.5f, 0.5f));
                    kkBridgeButtonImage.color = Color.white;
                }

                kkBridgeButtonObj.transform.SetParent(parentPanel, true);
                kkBridgeButtonObj.transform.localScale = templateButtonObj.transform.localScale;

                var childButtons = new List<RectTransform>();
                foreach (Transform child in parentPanel) childButtons.Add(child as RectTransform);
                Vector2 basePosition = new Vector2(0f, float.MaxValue);
                foreach (RectTransform buttonRect in childButtons)
                {
                    if (buttonRect.anchoredPosition.x >= basePosition.x && buttonRect.anchoredPosition.y <= buttonRect.anchoredPosition.y)
                        basePosition = buttonRect.anchoredPosition;
                }
                while (childButtons.Any(c => c.name != kkBridgeButtonObj.name && (c.anchoredPosition - basePosition).sqrMagnitude < 4f))
                {
                    basePosition.y += 40f;
                }
                ((RectTransform)kkBridgeButtonObj.transform).anchoredPosition = basePosition;

                Button buttonComponent = kkBridgeButtonObj.GetComponent<Button>();
                if (buttonComponent != null)
                {
                    buttonComponent.interactable = true;

                    buttonComponent.onClick = new Button.ButtonClickedEvent();
                    buttonComponent.onClick.AddListener(ToggleKkBridgeWindow);
                }
            }
            catch (Exception e)
            {
                Log.LogError($"[KKBridgeButton] CRITICAL ERROR: An error occurred while adding the button: {e.ToString()}");
            }
        }

        /// <summary>
        /// 從當前 Assembly (DLL) 的嵌入式資源中載入圖片
        /// </summary>
        /// <param name="resourceName">資源的完整名稱 (專案名稱.檔案名稱)</param>
        /// <returns>載入後的 Texture2D 物件</returns>
        private Texture2D LoadImageFromAssembly(string resourceName)
        {
            try
            {
                // 獲取當前正在執行的 Assembly (也就是 KKBridge.dll)
                Assembly assembly = Assembly.GetExecutingAssembly();

                // 讀取嵌入式資源的數據流
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return null;

                    // 將數據流讀入位元組陣列
                    byte[] buffer = new byte[stream.Length];
                    stream.Read(buffer, 0, buffer.Length);

                    // 從位元組陣列創建 Texture2D
                    // 尺寸參數 (2, 2) 不重要，LoadImage 會自動調整
                    Texture2D texture = new Texture2D(2, 2);
                    texture.LoadImage(buffer); // 這會自動解析PNG並載入

                    return texture;
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Error loading image from Assembly: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 導出 Timeline 動畫的協程
        /// </summary>
        private System.Collections.IEnumerator ExportTimelineAnimation_Coroutine()
        {
            if (_isExporting)
            {
                // 如果正在導出時再次點擊，則應視為取消操作
                // 停止 Timeline 播放
                if (TimelineCompatibility.GetIsPlaying())
                {
                    TimelineCompatibility.Play(); // Play() 在播放時會觸發暫停
                }
                _isExporting = false;
                Log.LogInfo("Export cancelled by user.");
                yield break;
            }

            int? originalCaptureFramerate = null;
            try
            {
                // --- 階段一：準備工作 ---
                _isExporting = true;
                if (_exportButtonText != null) _exportButtonText.text = "Exporting...";
                Log.LogInfo("Starting Timeline animation export...");

                if (!TimelineCompatibility.Init())
                {
                    Log.LogError("Timeline plugin not found or failed to initialize. Cannot export animation.");
                    _isExporting = false;
                    if (_exportButtonText != null) _exportButtonText.text = "Export Timeline to VMD";
                    yield break;
                }

                var studioInstance = Singleton<Studio.Studio>.Instance;
                var characters = studioInstance.dicObjectCtrl.Values.OfType<OCIChar>().ToList();
                if (!characters.Any())
                {
                    Log.LogWarning("No characters in the scene, export cancelled.");
                    _isExporting = false;
                    if (_exportButtonText != null) _exportButtonText.text = "Export Timeline to VMD";
                    yield break;
                }

                // 設定通用導出參數
                const int fps = 30;
                float timelineDuration = TimelineCompatibility.GetDuration();
                if (timelineDuration <= 0)
                {
                    Log.LogWarning("Timeline duration is 0 or invalid, export cancelled.");
                    _isExporting = false;
                    if (_exportButtonText != null) _exportButtonText.text = "Export Timeline to VMD";
                    yield break;
                }
                Log.LogInfo($"Timeline duration: {timelineDuration:F2} seconds (at {fps}fps) for {characters.Count} character(s).");

                // 儲存並設定 Time.captureFramerate
                originalCaptureFramerate = Time.captureFramerate;
                Time.captureFramerate = fps;

                // --- 階段二：初始化資料結構並開始錄製 ---
                var allCharactersFrames = new Dictionary<OCIChar, List<VmdMotionFrame>>();
                var characterBoneCaches = new Dictionary<OCIChar, Dictionary<string, Transform>>();
                foreach (var ociChar in characters)
                {
                    // 為每個角色創建一個 VMD 影格列表
                    allCharactersFrames[ociChar] = new List<VmdMotionFrame>();

                    // 為每個角色預先建立並儲存骨骼快取
                    var boneCacheForChar = new Dictionary<string, Transform>();
                    Transform instanceRootTf = ociChar.charInfo.transform;
                    if (instanceRootTf != null)
                    {
                        foreach (var entry in BoneMapper.GetAllEntries())
                        {
                            Transform boneTf = FindDescendant(instanceRootTf, entry.KkName);
                            if (boneTf != null)
                            {
                                boneCacheForChar[entry.KkName] = boneTf;
                            }
                        }
                    }
                    characterBoneCaches[ociChar] = boneCacheForChar;
                }

                // 將 Timeline 開始播放
                if (!TimelineCompatibility.GetIsPlaying())
                {
                    TimelineCompatibility.Play();
                }

                // ** 核心循環：推進影格，並在每一影格內處理所有角色 **
                int currentFrame = 0;
                float previousTime = -1f;

                while (_isExporting) // 增加一個開關，以便可以從外部停止
                {
                    // 1. 等待下一影格
                    yield return new WaitForEndOfFrame();

                    // 2. 獲取當前播放時間
                    float currentTime = TimelineCompatibility.GetPlaybackTime();

                    // 如果用戶在錄製過程中點擊了取消，_isExporting 會變為 false
                    if (!_isExporting)
                    {
                        Log.LogInfo("Recording stopped by cancellation.");
                        break;
                    }

                    // 3. 檢查播放是否暫停、結束、循環、倒退
                    if (currentTime <= previousTime)
                    {
                        Log.LogInfo($"Detected Timeline pause/end/loop/rewind. Stopping recording at frame {currentFrame}.");
                        break;
                    }

                    // 4. 在這一影格內，遍歷所有角色並收集數據
                    foreach (var ociChar in characters)
                    {
                        var singleFrameBoneData = new List<VmdMotionFrame>();
                        Transform instanceRootTf = ociChar.charInfo.transform;

                        if (characterBoneCaches.TryGetValue(ociChar, out var currentBoneCache) && instanceRootTf != null)
                        {
                            CollectAllBoneDataForCharacter(instanceRootTf, currentBoneCache, singleFrameBoneData);
                        }

                        foreach (var boneFrame in singleFrameBoneData)
                        {
                            boneFrame.FrameNumber = (uint)currentFrame;
                        }
                        // 將當前影格的數據添加到對應角色的列表中
                        allCharactersFrames[ociChar].AddRange(singleFrameBoneData);
                    }

                    if (currentFrame % 100 == 0)
                    {
                        Log.LogInfo($"Recording frame: {currentFrame}, Time: {currentTime:F2}s / {timelineDuration:F2}s");
                    }

                    previousTime = currentTime;
                    currentFrame++;
                }

                Log.LogInfo($"Finished collecting frame data for all characters ({currentFrame} frames recorded).");

                // --- 階段三：導出所有角色的 VMD 檔案 ---
                string outputDirectory = _outputDirectory.Value;
                Directory.CreateDirectory(outputDirectory);

                int charIndex = 1;
                foreach (var ociChar in characters)
                {
                    ChaControl chaCtrl = ociChar.charInfo;
                    string charName = chaCtrl.chaFile.parameter.fullname;
                    List<VmdMotionFrame> framesForThisChar = allCharactersFrames[ociChar];

                    Log.LogInfo($"--- Exporting VMD for character {charIndex}: {charName} ---");

                    if (framesForThisChar.Count == 0)
                    {
                        Log.LogWarning($"No frames were recorded for character {charName}. Skipping VMD export for this character.");
                        charIndex++;
                        continue;
                    }

                    var ikFrames = new List<VmdIkFrame> { new VmdIkFrame { FrameNumber = 0, Display = true } };
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
                        ikFrames[0].IkEnables.Add(new VmdIkEnable(ikName, false));
                    }

                    string vmdFileName = CreateSafeFileName(charIndex, "_", charName, "_timeline", ".vmd");
                    string vmdFilePath = Path.Combine(outputDirectory, vmdFileName);

                    try
                    {
                        VmdExporter.Export(framesForThisChar, ikFrames, "KoikatsuModel", vmdFilePath);
                        Log.LogInfo($"Successfully exported VMD animation to: {vmdFilePath}");
                    }
                    catch (Exception e)
                    {
                        Log.LogError($"Failed to export VMD for character {charName}: {e.Message}");
                    }
                    charIndex++;
                }
            }
            finally
            {
                // --- 階段四：無論成功或失敗，都執行清理工作 ---
                if (originalCaptureFramerate.HasValue)
                {
                    Time.captureFramerate = originalCaptureFramerate.Value; // 恢復影格率
                }

                // 確保 Timeline 停止播放
                if (TimelineCompatibility.GetIsPlaying())
                {
                    TimelineCompatibility.Play();
                }

                // 重置狀態
                _isExporting = false;
                if (_exportButtonText != null)
                {
                    _exportButtonText.text = "Export Timeline to VMD";
                }
                Log.LogInfo("Timeline animation export process finished.");
            }
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
        /// 尋找指定名稱的後代 Transform (廣度優先 BFS)，更穩健。
        /// </summary>
        private Transform FindDescendant(Transform parent, string name)
        {
            if (parent == null) return null;

            var queue = new Queue<Transform>();
            queue.Enqueue(parent);

            while (queue.Count > 0)
            {
                Transform current = queue.Dequeue();

                if (current.name == name)
                {
                    return current;
                }

                foreach (Transform child in current)
                {
                    queue.Enqueue(child);
                }
            }
            return null; // 沒找到
        }

        /// <summary>
        /// 為單一角色收集所有骨骼數據的啟動函數。
        /// 負責動態識別根骨骼、建立快取，並啟動遞迴。
        /// </summary>
        private void CollectAllBoneDataForCharacter(Transform instanceRootTf, Dictionary<string, Transform> boneCache, List<VmdMotionFrame> frameList)
        {
            if (instanceRootTf == null)
            {
                Log.LogError("Character root transform is null. Skipping.");
                return;
            }
            CollectBoneDataRecursive(instanceRootTf, boneCache, frameList);
        }

        /// <summary>
        /// 遞迴收集骨骼數據。
        /// 採用統一計算邏輯，數據和規則由 BoneMapper 提供。
        /// </summary>
        private void CollectBoneDataRecursive(Transform bone, Dictionary<string, Transform> boneCache, List<VmdMotionFrame> frameList)
        {
            if (bone == null) return;

            // 使用智能查找獲取當前骨骼的映射規則
            if (BoneMapper.TryGetMatchEntry(bone, out BoneMapEntry currentEntry))
            {
                var frame = new VmdMotionFrame(currentEntry.MmdName);
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
                        Log.LogWarning($"Could not find parent transform for '{currentEntry.MmdName}'. Parent MMD name: '{currentEntry.MmdParentName}'. Falling back to localRotation.");
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
            foreach (Transform child in bone)
            {
                CollectBoneDataRecursive(child, boneCache, frameList);
            }
        }

        #region Helper Methods (輔助函式區)

        /// <summary>
        /// 將一系列物件 (字串、數字等) 組合並清理成一個安全的檔案名稱。
        /// </summary>
        /// <param name="parts">要組合成檔名的各個部分。</param>
        /// <returns>一個不包含任何無效檔案字元的安全檔名。</returns>
        private static string CreateSafeFileName(params object[] parts)
        {
            // --- 步驟 1: 將所有傳入的參數組合成一個單一字串 ---
            // 使用 StringBuilder 提高效率
            var rawNameBuilder = new StringBuilder();
            foreach (object part in parts)
            {
                if (part != null)
                {
                    // 使用 CultureInfo.InvariantCulture 確保數字等類型轉換為字串時格式一致
                    rawNameBuilder.Append(string.Format(CultureInfo.InvariantCulture, "{0}", part));
                }
            }
            string rawFileName = rawNameBuilder.ToString();

            // --- 步驟 2: 移除組合後字串中的所有無效檔案字元 ---
            var safeNameBuilder = new StringBuilder(rawFileName.Length);
            // 預先獲取一次無效字元陣列
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in rawFileName)
            {
                // 如果字元不是無效字元，則附加到最終結果中
                if (Array.IndexOf(invalidChars, c) < 0)
                {
                    safeNameBuilder.Append(c);
                }
            }

            return safeNameBuilder.ToString();
        }
        #endregion
    }
}
