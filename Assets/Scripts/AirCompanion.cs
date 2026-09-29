using UnityEngine;
using UnityEngine.UI;

// A small, non-interactive companion. No camera or perception capability implied.
public sealed class AirCompanion : MonoBehaviour
{
    public string State = "";
    RectTransform character;
    Image[] bars;
    public static AirCompanion Create(Transform parent, float x, float y, float size, bool meter = false)
    {
        var root = AirWeatherHud.Rect(parent, "哇小兴", x, y, size, size);
        var companion = root.gameObject.AddComponent<AirCompanion>();
        companion.character = AirWeatherHud.Rect(root, "Character", 0, 0, size, size);
        var picture = companion.character.gameObject.AddComponent<RawImage>();
        picture.texture = Resources.Load<Texture2D>("Companion/Waxiaoxing");
        picture.raycastTarget = false;
        if (meter) {
            companion.bars = new Image[5];
            for (int i = 0; i < 5; i++) {
                var bar = AirWeatherHud.Rect(root, "Activity", (i - 2) * 8, -size / 2 + 4, 3, 6);
                companion.bars[i] = bar.gameObject.AddComponent<Image>();
                companion.bars[i].raycastTarget = false;
            }
        }
        return companion;
    }
    void Update()
    {
        bool speaking = State.Contains("回答"), listening = State.Contains("聆听"), thinking = State.Contains("思考") || State.Contains("连接");
        bool active = speaking || listening || thinking;
        character.anchoredPosition = new Vector2(0, speaking ? Mathf.Sin(Time.unscaledTime * 4) * 1.5f : 0);
        if (bars == null) return;
        for (int i = 0; i < bars.Length; i++) {
            bars[i].color = thinking ? new Color(.82f, .73f, 1f, .85f) : new Color(.72f, .95f, .5f, active ? .95f : .3f);
            bars[i].rectTransform.sizeDelta = new Vector2(3, active ? 5 + 9 * Mathf.Abs(Mathf.Sin(Time.unscaledTime * (speaking ? 4 : 1.5f) + i * .65f)) : 4);
        }
    }
}
