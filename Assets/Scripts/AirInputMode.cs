using System.Collections;
using Unity.XR.XREAL;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Phone-only mode controls. XRI handles hand rays, hover, pinch and tracking loss.
public sealed class AirInputMode : MonoBehaviour
{
    public Font font;
    Text status;
    Button handsButton;
    RectTransform safe;
    bool supported, hands, leftTracked, rightTracked;
    bool lastLeftPinch, lastRightPinch;
    float nextProbe;
    IEnumerator Start()
    {
        var root = new GameObject("Phone Input Mode", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.targetDisplay = 0; canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 2340); scaler.matchWidthOrHeight = 0;
        safe = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>(); safe.SetParent(root.transform, false);
        safe.offsetMin = safe.offsetMax = Vector2.zero;
        var panel = Rect("Mode Panel", safe, 0, -175, 960, 310);
        panel.gameObject.AddComponent<Image>().color = new Color(.025f, .065f, .09f, .98f);
        status = Label(panel, "正在检查手势能力…", 0, 90, 880, 70, 40);
        handsButton = Button(panel, "手势交互", -225, -45, () => Select(true));
        Button(panel, "手机射线", 225, -45, () => Select(false));
        handsButton.interactable = false;
        yield return new WaitForSecondsRealtime(2);
        supported = XREALPlugin.IsHandTrackingSupported(); handsButton.interactable = supported;
        Debug.Log("AR_INPUT hand_supported=" + supported + " phone_orientation=" + Screen.orientation);
        Select(supported);
    }
    static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1); rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(width, height); return rt;
    }
    Text Label(Transform parent, string text, float x, float y, float width, float height, int size)
    {
        var rt = Rect(text, parent, x, y, width, height); rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        var label = rt.gameObject.AddComponent<Text>(); label.font = font; label.fontSize = size; label.text = text;
        label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; return label;
    }
    Button Button(Transform parent, string text, float x, float y, UnityEngine.Events.UnityAction action)
    {
        var rt = Rect(text, parent, x, y, 420, 144); rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        rt.gameObject.AddComponent<Image>().color = new Color(.08f, .28f, .30f);
        var button = rt.gameObject.AddComponent<Button>(); button.onClick.AddListener(action); Label(rt, text, 0, 0, 400, 130, 48); return button;
    }
    void Select(bool useHands)
    {
        bool ok = XREALPlugin.SetInputSource(useHands ? InputSource.Hands : InputSource.Controller);
        if (ok) hands = useHands;
        status.text = ok ? (hands ? "抬手指向按钮 · 拇指与食指捏合" : "手机射线模式 · 点击触控区选择") : "模式切换失败，请重试或使用系统菜单";
        Debug.Log("AR_INPUT requested=" + (useHands ? "Hands" : "Controller") + " success=" + ok);
    }
    void Update()
    {
        if (safe != null && Screen.width > 0 && Screen.height > 0) {
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        }
        bool l = false, r = false, lp = false, rp = false;
        foreach (var device in InputSystem.devices) if (device is XREALHandTracking hand) {
            bool tracked = hand.isTracked.isPressed;
            bool left = false; foreach (var usage in hand.usages) if (usage == UnityEngine.InputSystem.CommonUsages.LeftHand) left = true;
            if (left) { l = tracked; lp = tracked && hand.indexPressed.isPressed; }
            else { r = tracked; rp = tracked && hand.indexPressed.isPressed; }
        }
        if (l != leftTracked || r != rightTracked) { Debug.Log("AR_INPUT hands_tracked left=" + l + " right=" + r); leftTracked = l; rightTracked = r; }
        if (lp && !lastLeftPinch || rp && !lastRightPinch) Debug.Log("AR_INPUT pinch=true");
        lastLeftPinch = lp; lastRightPinch = rp;
        if (status != null && hands && Time.unscaledTime > nextProbe) {
            status.text = l || r ? "手部已跟踪 · 指向按钮后捏合" : "手势模式 · 将手抬到眼镜前方"; nextProbe = Time.unscaledTime + 1;
        }
    }
}
