using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

// Unity adaptation of the private Claude Design '窗息' prototype, 2026-09-22.
public sealed class AirWeatherHud : MonoBehaviour
{
    public RectTransform Root { get; private set; }
    public bool Expanded { get; private set; }
    public bool Optical { get; private set; } = true;
    Text temperature, condition, pollutant, place, provenance, weatherTime, airTime, detailText, detailLabel;
    AirWeatherGlyph glyph;
    RectTransform details;
    Image backing, detailBacking;
    CanvasGroup group;
    bool shown;
    public float Visibility => group == null ? 0 : group.alpha;
    public void Build(Camera camera, Font font)
    {
        Root = gameObject.AddComponent<RectTransform>();
        Root.sizeDelta = new Vector2(500, 330);
        Root.localScale = Vector3.one * .001f;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
        gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false;
        backing = Box(Root, "Optional tint", 0, 0, 500, 330, Color.clear);
        Color accent = new Color(.73f, .94f, .53f);
        Box(Root, "Anchor stem", -242, 50, 2, 190, new Color(.73f,.94f,.53f,.7f));
        Box(Root, "Anchor point", -242, 151, 6, 6, accent);
        Label(Root, font, "窗外 · 环境", -90, 140, 250, 30, 19, accent);
        Label(Root, font, "哇小兴", 157, 140, 122, 30, 19, accent);
        AirCompanion.Create(Root, 160, 69, 118);
        temperature = Label(Root, font, "—°", -89, 74, 250, 114, 76, Color.white, true);
        glyph = Rect(Root, "Weather icon", 66, 76, 48, 48).gameObject.AddComponent<AirWeatherGlyph>();
        glyph.color = new Color(.94f,.97f,.84f); glyph.raycastTarget = false;
        condition = Label(Root, font, "天气未知", -70, 5, 290, 38, 25, Color.white);
        Box(Root, "Divider", 0, -26, 436, 1, new Color(.73f,.94f,.53f,.35f));
        Label(Root, font, "PM2.5", -166, -64, 102, 38, 22, accent);
        pollutant = Label(Root, font, "— μg/m³", 14, -64, 242, 48, 30, Color.white, true);
        var button = Rect(Root, "Details button", 190, -64, 76, 50);
        var surface = button.gameObject.AddComponent<AirSoftPanel>(); surface.color = new Color(.22f,.35f,.22f,.65f);
        var detailsButton = button.gameObject.AddComponent<Button>(); detailsButton.targetGraphic = surface;
        detailsButton.onClick.AddListener(ToggleDetails);
        detailLabel = Label(button, font, "详情", 0, 0, 64, 42, 21, accent);
        detailLabel.alignment = TextAnchor.MiddleCenter;
        place = Label(Root, font, "窗外区域", 0, -108, 436, 34, 22, Color.white);
        provenance = Label(Root, font, "", 0, -142, 436, 30, 19, accent);
        details = Rect(Root, "Details", 0, -355, 610, 350);
        detailBacking = Box(details, "Optional detail tint", 0, 0, 610, 350, Color.clear);
        detailText = Label(details, font, "", 0, 0, 574, 332, 21, Color.white);
        details.gameObject.SetActive(false);
        weatherTime = Label(Root, font, "", 0, -193, 500, 33, 21, Color.white);
        airTime = Label(Root, font, "", 0, -226, 500, 33, 21, Color.white);
        SetOptical(true);
    }
    public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, y); return rt;
    }
    static Image Box(Transform parent, string name, float x, float y, float w, float h, Color color)
    { var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image; }
    static Text Label(Transform parent, Font font, string value, float x, float y, float w, float h, int size, Color color, bool bold = false)
    {
        var text = Rect(parent, value, x, y, w, h).gameObject.AddComponent<Text>();
        text.font = font; text.fontSize = size; text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.text = value; text.color = color; text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false; text.supportRichText = false; return text;
    }
    public void Display(AirPlace location, AirReading reading, bool simulation)
    {
        temperature.text = reading.temperature + "°";
        condition.text = reading.weatherTime.HasValue || simulation ? (reading.weatherSummary ?? AirEnvironmentData.Condition(reading.weatherCode)) : reading.weatherStatus;
        pollutant.text = reading.pm25 + " μg/m³";
        string name = AirVoiceProtocol.CleanPlace(location.name);
        place.text = name.Length > 18 ? name.Substring(0, 17) + "…" : name;
        provenance.text = simulation ? "演示数据 · 合成案例" : "空气：区域模型 · 仅供参考";
        weatherTime.text = "天气  " + reading.WeatherStamp;
        airTime.text = "空气  " + reading.AirStamp;
        glyph.SetWeather(reading.weatherCode, reading.isDay);
        detailText.text = reading.weather + "\n" + reading.air + "\n" + (simulation ? "合成案例 · 非当前环境" : reading.weatherSource + "\n空气：Open-Meteo / CAMS · CC BY 4.0 · 约45 km") + "\n测试锚点 · 未接入窗户自动识别";
    }
    public void ToggleDetails()
    {
        Expanded = !Expanded; details.gameObject.SetActive(Expanded);
        detailLabel.text = Expanded ? "收起" : "详情";
        // Expand alongside the card, so text never extends below the conservative headset FOV.
        details.anchoredPosition = new Vector2(0, -83);
        details.sizeDelta = new Vector2(610, 350);
        foreach (Transform child in Root) if (child != details && child.name != "Details button") child.gameObject.SetActive(!Expanded);
        // Keep a large, reachable close control above the details.
        detailLabel.transform.parent.gameObject.SetActive(true);
        ((RectTransform)detailLabel.transform.parent).anchoredPosition = Expanded ? new Vector2(245, 126) : new Vector2(190, -64);
    }
    public void SetOptical(bool value)
    { Optical = value; backing.color = detailBacking.color = value ? Color.clear : new Color(.045f, .075f, .055f, .84f); }
    public void SetVisible(bool value, bool immediate = false)
    {
        shown = value;
        if (!value && Expanded) ToggleDetails();
        group.blocksRaycasts = group.interactable = value;
        if (immediate) group.alpha = value ? 1 : 0;
    }
    void Update() { group.alpha = Mathf.MoveTowards(group.alpha, shown ? 1 : 0, Time.unscaledDeltaTime / .2f); }
}
