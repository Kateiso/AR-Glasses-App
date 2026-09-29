using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

public sealed class AirEnvironmentDemo : MonoBehaviour
{
    public Transform head;
    public Font chineseFont;
    AirEnvironmentData data;
    AirDemoSettings settings;
    RectTransform panel, controls, launcher;
    AirWeatherHud hud;
    bool menuOpen;
    Button skipTarget;
    Text title, badge, body, foot, help;
    GameObject settingsPage, contentPage;
    InputField windowLat, windowLon, targetLat, targetLon, targetName;
    readonly Vector3[] anchors = new Vector3[2];
    readonly bool[] calibrated = new bool[2];
    int calibration = 0, selected = -1, candidate = -1;
    float candidateSince;
    bool editing, hidden, wasTracked, hadFocus, editingSimulation;
    string settingsPath;
    Button capture;
    static readonly Color Accent = new Color(.73f, .94f, .53f);

    void Start()
    {
        settingsPath = Path.Combine(Application.persistentDataPath, "air-settings.json");
        settings = new AirDemoSettings();
        try
        {
            if (File.Exists(settingsPath)) {
                string original = File.ReadAllText(settingsPath);
                string migrated = AirVoiceProtocol.MigrateSettings(original);
                settings = JsonUtility.FromJson<AirDemoSettings>(migrated);
                if (original != migrated) AirSettingsStorage.Save(settingsPath, migrated);
            }
        }
        catch (Exception e) { Debug.LogWarning("AR_AIR_SETTINGS " + e.GetType().Name); }
        if (settings == null || settings.window == null || settings.target == null) settings = new AirDemoSettings();
        settings.window.name = AirVoiceProtocol.CleanPlace(settings.window.name);
        settings.target.name = AirVoiceProtocol.CleanPlace(settings.target.name);
        data = gameObject.AddComponent<AirEnvironmentData>();
        BuildUI();
        gameObject.AddComponent<AirCapabilityProbe>();
        gameObject.AddComponent<AirInputMode>().font = chineseFont;
        data.Changed += Render;
        data.Reload(settings);
        var voice = gameObject.AddComponent<AirGeminiVoice>();
        voice.head = head; voice.font = chineseFont;
        voice.EnvironmentJson = () => data.EnvironmentJson(selected);
        Render();
        Debug.Log("AR_AIR_READY version=0.4.3 calibrationRequired=true automaticWindowRecognition=false");
    }

    RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    RectTransform Canvas(string name, float w, float h)
    {
        var rt = Rect(name, transform, 0, 0, w, h);
        var canvas = rt.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = head.GetComponent<Camera>();
        rt.localScale = Vector3.one * 0.001f;
        rt.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        var image = rt.gameObject.AddComponent<AirSoftPanel>();
        image.color = new Color(.04f, .075f, .05f, .94f);
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = Accent;
        outline.effectDistance = new Vector2(2, 2);
        return rt;
    }

    Text Label(Transform parent, string value, float x, float y, float w, float h, int size, Color color)
    {
        var rt = Rect("Label", parent, x, y, w, h);
        var text = rt.gameObject.AddComponent<Text>();
        text.font = chineseFont;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.supportRichText = false;
        text.raycastTarget = false;
        return text;
    }

    Button ActionButton(Transform parent, string value, float x, float y, float w, Action action)
    {
        var rt = Rect(value, parent, x, y, w, 64);
        var image = rt.gameObject.AddComponent<AirSoftPanel>();
        image.color = new Color(.14f, .24f, .16f);
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => action());
        Label(rt, value, 0, 0, w - 20, 58, 29, Color.white).alignment = TextAnchor.MiddleCenter;
        return button;
    }

    InputField Field(Transform parent, string label, float x, float y, bool numeric)
    {
        Label(parent, label, x, y + 51, 430, 44, 24, Accent);
        var rt = Rect(label, parent, x, y, 430, 56);
        rt.gameObject.AddComponent<Image>().color = new Color(.10f, .16f, .11f);
        var text = Label(rt, "", 0, 0, 404, 54, 29, Color.white);
        var field = rt.gameObject.AddComponent<InputField>();
        field.textComponent = text;
        field.characterLimit = numeric ? 14 : 24;
        field.contentType = numeric ? InputField.ContentType.DecimalNumber : InputField.ContentType.Standard;
        return field;
    }

    void BuildUI()
    {
        var hudObject = new GameObject("Window Atmosphere HUD");
        hudObject.transform.SetParent(transform, false);
        hud = hudObject.AddComponent<AirWeatherHud>();
        hud.Build(head.GetComponent<Camera>(), chineseFont);
        panel = Canvas("Air Environment Setup", 1040, 680);
        title = Label(panel, "", -66, 278, 818, 64, 41, Color.white);
        AirCompanion.Create(panel, 426, 270, 100);
        badge = Label(panel, "", 0, 216, 950, 45, 27, Accent);
        contentPage = Rect("Readings", panel, 0, 0, 1040, 480).gameObject;
        body = Label(contentPage.transform, "", 0, 22, 950, 350, 31, Color.white);
        foot = Label(contentPage.transform, "", 0, -202, 950, 110, 23, new Color(0.73f, 0.84f, 0.89f));
        capture = ActionButton(contentPage.transform, "确认当前方向", 0, -285, 320, CaptureDirection);
        skipTarget = ActionButton(contentPage.transform, "只测试窗户", 345, -285, 300, () =>
        {
            calibrated[1] = false; calibration = 2; selected = candidate = -1; Render();
        });
        settingsPage = Rect("Place Settings", panel, 0, 0, 1040, 480).gameObject;
        windowLat = Field(settingsPage.transform, "天气地点 · 纬度", -240, 116, true);
        windowLon = Field(settingsPage.transform, "天气地点 · 经度", 240, 116, true);
        targetLat = Field(settingsPage.transform, "目标地点 · 纬度", -240, 16, true);
        targetLon = Field(settingsPage.transform, "目标地点 · 经度", 240, 16, true);
        targetName = Field(settingsPage.transform, "目标地点名称", -240, -84, false);
        ActionButton(settingsPage.transform, "真实 / 模拟", 240, -84, 430, () =>
        {
            editingSimulation = !editingSimulation;
            badge.text = editingSimulation ? "演示数据 · 合成案例" : "在线数据 · 手动位置";
        });
        Label(settingsPage.transform, "坐标：WGS84。无坐标可留空，不能直接填高德偏移坐标。\n在线空气数据为约 45 km 区域模型，不代表街道实测。", 0, -178, 950, 85, 25, Color.white);
        ActionButton(settingsPage.transform, "保存并刷新", 0, -285, 320, SaveSettings);
        settingsPage.SetActive(false);

        controls = Canvas("Air Controls", 1040, 160);
        launcher = Canvas("HUD Menu", 160, 66);
        Destroy(launcher.GetComponent<Outline>());
        ActionButton(launcher, "菜单", 0, 0, 160, () => { menuOpen = !menuOpen; controls.gameObject.SetActive(menuOpen); });
        help = Label(controls, "", -65, 45, 850, 40, 24, Accent);
        ActionButton(controls, "收起", 425, 43, 130, () => { menuOpen = false; });
        ActionButton(controls, "地点 / 数据设置", -335, -24, 310, OpenSettings);
        ActionButton(controls, "重新校准", 0, -24, 280, ResetCalibration);
        ActionButton(controls, "透视 / 淡底", 335, -24, 280, () => hud.SetOptical(!hud.Optical));
    }

    public void ResetCalibration()
    {
        if (panel == null) return;
        calibrated[0] = calibrated[1] = false;
        calibration = 0;
        selected = candidate = -1;
        editing = hidden = menuOpen = false;
        hud.SetVisible(false, true);
        Render();
    }

    void CaptureDirection()
    {
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.1f) return;
        if (calibration == 1 && Vector3.Angle(forward, anchors[0] - head.position) < 35)
        {
            help.text = "两个方向太接近，请转头至少约 35° 后确认";
            return;
        }
        anchors[calibration] = head.position + forward * 2 + Vector3.up * 0.1f;
        calibrated[calibration] = true;
        Debug.Log($"AR_AIR_CALIBRATED target={calibration}");
        calibration++;
        menuOpen = false;
        Render();
    }

    void OpenSettings()
    {
        editing = true;
        editingSimulation = settings.simulation;
        hidden = false;
        windowLat.text = settings.window.Valid ? settings.window.latitude.ToString(CultureInfo.InvariantCulture) : "";
        windowLon.text = settings.window.Valid ? settings.window.longitude.ToString(CultureInfo.InvariantCulture) : "";
        targetLat.text = settings.target.Valid ? settings.target.latitude.ToString(CultureInfo.InvariantCulture) : "";
        targetLon.text = settings.target.Valid ? settings.target.longitude.ToString(CultureInfo.InvariantCulture) : "";
        targetName.text = settings.target.name;
        Render();
    }

    static bool ReadPlace(InputField lat, InputField lon, AirPlace place)
    {
        if (string.IsNullOrWhiteSpace(lat.text) && string.IsNullOrWhiteSpace(lon.text)) { place.configured = false; return true; }
        if (!double.TryParse(lat.text, NumberStyles.Float, CultureInfo.InvariantCulture, out double a)
            || !double.TryParse(lon.text, NumberStyles.Float, CultureInfo.InvariantCulture, out double b)
            || double.IsNaN(a) || double.IsNaN(b) || Math.Abs(a) > 90 || Math.Abs(b) > 180) return false;
        place.latitude = a; place.longitude = b; place.configured = true;
        return true;
    }

    void SaveSettings()
    {
        var next = JsonUtility.FromJson<AirDemoSettings>(JsonUtility.ToJson(settings));
        next.simulation = editingSimulation;
        if (!ReadPlace(windowLat, windowLon, next.window) || !ReadPlace(targetLat, targetLon, next.target))
        { help.text = "经纬度无效：纬度 -90～90，经度 -180～180"; return; }
        next.target.name = string.IsNullOrWhiteSpace(targetName.text) ? "目标区域" : targetName.text.Trim();
        try { AirSettingsStorage.Save(settingsPath, JsonUtility.ToJson(next, true)); }
        catch (Exception) { help.text = "保存失败，请重试"; return; }
        settings = next;
        editing = false;
        menuOpen = false;
        data.Reload(settings);
        Render();
    }

    void Update()
    {
        if (panel == null) return;
        var device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        bool knownTracking = device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked, out _);
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!knownTracking)
        {
            if (wasTracked) ResetCalibration();
            wasTracked = false;
            panel.gameObject.SetActive(false); hud.SetVisible(false, true);
            controls.gameObject.SetActive(false); launcher.gameObject.SetActive(false);
            return;
        }
#endif
        if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked))
        {
            if (wasTracked && !tracked) ResetCalibration();
            wasTracked = tracked;
            if (!tracked)
            {
                panel.gameObject.SetActive(false); hud.SetVisible(false, true);
                controls.gameObject.SetActive(false); launcher.gameObject.SetActive(false);
                help.text = "追踪暂不可用，请检查眼镜"; return;
            }
        }
        launcher.gameObject.SetActive(calibration >= 2 && !editing);
        launcher.position = head.position + head.rotation * new Vector3(.39f, -.35f, 2f);
        launcher.rotation = head.rotation;
        controls.gameObject.SetActive(menuOpen || editing || calibration < 2);
        if (menuOpen) launcher.gameObject.SetActive(false);
        controls.position = head.position + head.rotation * new Vector3(0, -0.36f, 2f);
        controls.rotation = head.rotation;
        if (editing || calibration < 2)
        {
            hud.SetVisible(false, true);
            panel.position = head.position + head.rotation * new Vector3(0, 0.1f, 2f);
            panel.rotation = head.rotation;
            panel.gameObject.SetActive(!hidden);
            return;
        }
        panel.gameObject.SetActive(false);
        int next = -1;
        float closest = 20;
        for (int i = 0; i < 2; i++)
        {
            if (!calibrated[i]) continue;
            float angle = Vector3.Angle(head.forward, anchors[i] - head.position);
            if (angle < closest) { next = i; closest = angle; }
        }
        if (selected >= 0 && Vector3.Angle(head.forward, anchors[selected] - head.position) < 28 && next == -1)
            next = selected;
        if (candidate != next) { candidate = next; candidateSince = Time.unscaledTime; }
        if (candidate != selected && Time.unscaledTime - candidateSince > 0.3f)
        {
            selected = candidate;
            Render();
            Debug.Log($"AR_AIR_SELECTION target={selected}");
        }
        hud.SetVisible(!menuOpen && !hidden && selected >= 0);
        if (selected >= 0)
        {
            hud.Root.position = anchors[selected];
            hud.Root.rotation = Quaternion.LookRotation(hud.Root.position - head.position, Vector3.up);
        }
    }

    void Render()
    {
        if (panel == null) return;
        settingsPage.SetActive(editing);
        contentPage.SetActive(!editing);
        capture.gameObject.SetActive(!editing && calibration < 2);
        skipTarget.gameObject.SetActive(!editing && calibration == 1);
        badge.text = (editing ? editingSimulation : settings.simulation) ? "演示数据 · 合成案例" : "在线环境数据 · 手动设置位置";
        help.text = "手势捏合 / 手机射线 · 锚点仅本次有效";
        if (editing) { title.text = "地点与数据设置"; return; }
        if (calibration < 2)
        {
            title.text = calibration == 0 ? "01 / 放置窗边测试卡" : "02 / 可选：第二个方向";
            body.text = calibration == 0
                ? "请看向窗框旁希望放卡片的位置。\n\n用 Beam Pro 点击「确认当前方向」。\n卡片会固定在前方约 2 米处。"
                : "请转向你要展示的目标地点方向。\n\n保持头部平稳，再点击「确认当前方向」。\n这一步不自动识别地理方位。";
            foot.text = "这是 UI 测试锚点，未接入窗户自动识别。\n天气使用已设置的地点；可跳过第二个方向。";
            return;
        }
        if (selected < 0) { help.text = "看向已校准的窗户或目标方向，停留片刻显示信息"; return; }
        var place = selected == 0 ? settings.window : settings.target;
        hud.Display(place, data.readings[selected], settings.simulation);
        title.text = selected == 0 ? "窗外大气环境" : place.name;
        body.text = data.readings[selected].weather + "\n\n" + data.readings[selected].air;
        foot.text = settings.simulation ? "演示数据，不代表当前环境 · 头部朝向触发"
            : "天气：中央气象台 · 大兴区域参考；空气：CAMS / Open-Meteo（约 45 km）\n空气为模型数据，非监测站实测 · CC BY 4.0\n" + (place.Valid ? $"手动位置 {place.latitude:F4}, {place.longitude:F4} · 5 分钟刷新" : "地点未设置 · GPS / RTK 未接入");
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) { hadFocus = true; if (hud != null) hud.SetVisible(false, true); }
        else if (hadFocus) ResetCalibration();
    }

    void OnDestroy() { if (data != null) data.Changed -= Render; }
}
