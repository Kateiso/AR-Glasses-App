using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Editor-only checks and snapshots: never added to the device player.
public static class AirDemoValidation
{
    public static void ValidateAndBuild()
    {
        Run();
        AirVoiceValidation.Run();
        EnvironmentBuild.BuildAir();
    }

    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object obj, string key, object value) => obj.GetType().GetField(key, Private).SetValue(obj, value);
    static object Get(object obj, string key) => obj.GetType().GetField(key, Private).GetValue(obj);
    static void Call(object obj, string method) => obj.GetType().GetMethod(method, Private).Invoke(obj, null);

    public static void Run()
    {
        if (AirEnvironmentData.Number("\"pm2_5\":null}", "pm2_5") != "—"
            || AirEnvironmentData.Number("\"pm10\":4}", "pm2_5") != "—"
            || AirEnvironmentData.Number("\"pm2_5\":0}", "pm2_5") != "0")
            throw new Exception("Missing pollutant values must stay distinct from zero.");
        if (new AirPlace { configured = true, latitude = double.NaN }.Valid)
            throw new Exception("Invalid location accepted.");

        var reading = new AirReading();
        string now = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm");
        AirEnvironmentData.Apply(reading, "{\"current\":{\"time\":\"" + now + "\",\"temperature_2m\":0,\"weather_code\":95,\"is_day\":0}}", false);
        if (reading.temperature != "0" || reading.weatherCode != 95 || reading.isDay || AirEnvironmentData.Condition(reading.weatherCode) != "雷雨")
            throw new Exception("Weather values or icon mapping incorrect.");
        AirEnvironmentData.Apply(reading, "{\"current\":{\"time\":\"" + now + "\",\"pm2_5\":null}}", true);
        if (reading.pm25 != "—") throw new Exception("Missing PM2.5 rendered as a measurement.");
        reading.ClearWeather("暂无天气数据");
        if (reading.temperature != "—" || reading.weatherCode != -1 || reading.weatherTime.HasValue)
            throw new Exception("Failed weather retains a valid icon or timestamp.");

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Preview Camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.13f, 0.16f, 0.18f);
        camera.fieldOfView = 26;
        var demo = new GameObject("Preview").AddComponent<AirEnvironmentDemo>();
        demo.head = camera.transform;
        demo.chineseFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKsc-Regular.otf");
        var settings = new AirDemoSettings();
        Set(demo, "settings", settings);
        var data = demo.gameObject.AddComponent<AirEnvironmentData>();
        Set(demo, "data", data);
        Call(demo, "BuildUI");
        Call(demo, "Render");
        Snapshot(demo, camera, "calibration");

        Call(demo, "CaptureDirection");
        camera.transform.rotation = Quaternion.Euler(0, 65, 0);
        Call(demo, "CaptureDirection");
        if ((int)Get(demo, "calibration") != 2) throw new Exception("Calibration did not finish.");
        camera.transform.rotation = Quaternion.identity;
        Set(demo, "selected", 0);
        data.readings[0].weather = "温度  24 °C    湿度  48%\n风速  2.1 m/s    风向（来向） 45°\n模型时刻 09-21 20:15 · 时效待核实";
        data.readings[0].air = "PM2.5  28 μg/m³    PM10  46 μg/m³\nO3  91 μg/m³\n模型时刻 09-21 20:00 · 区域参考，非现场测量";
        data.readings[0].temperature = "24";
        data.readings[0].pm25 = "28";
        data.readings[0].weatherCode = 2;
        data.readings[0].weatherTime = DateTimeOffset.UtcNow;
        data.readings[0].airTime = DateTimeOffset.UtcNow;
        settings.window.name = "大兴区生态环境局附近（预设）";
        Call(demo, "Render");
        var hud = (AirWeatherHud)Get(demo, "hud");
        ((RectTransform)Get(demo, "panel")).gameObject.SetActive(false);
        ((RectTransform)Get(demo, "controls")).gameObject.SetActive(false);
        ((RectTransform)Get(demo, "launcher")).gameObject.SetActive(false);
        hud.Root.position = new Vector3(0, .05f, 2);
        hud.SetVisible(true, true);
        Snapshot(demo, camera, "hud_optical_fixture");
        var voice = demo.gameObject.AddComponent<AirGeminiVoice>();
        voice.head = camera.transform; voice.font = demo.chineseFont;
        Call(voice, "BuildUI");
        var voiceRoot = (RectTransform)Get(voice, "ui");
        voiceRoot.position = new Vector3(-.12f, -.30f, 2f);
        ((Text)Get(voice, "state")).text = "正在回答";
        ((Text)Get(voice, "subtitle")).text = "当前温度24度，PM2.5为28微克每立方米。";
        ((Text)Get(voice, "startLabel")).text = "结束";
        ((Button)Get(voice, "muteButton")).gameObject.SetActive(true);
        Snapshot(demo, camera, "hud_voice_fixture");
        UnityEngine.Object.DestroyImmediate(voice);
        UnityEngine.Object.DestroyImmediate(voiceRoot.gameObject);
        var glyph = hud.GetComponentInChildren<AirWeatherGlyph>();
        if (glyph == null || glyph.GetComponent<CanvasRenderer>() == null)
            throw new Exception("Weather glyph has no CanvasRenderer.");
        var mesh = glyph.GetComponent<CanvasRenderer>().GetMesh();
        if (mesh == null || mesh.vertexCount == 0) throw new Exception("Weather glyph produced no visible geometry.");
        camera.backgroundColor = new Color(.75f, .81f, .85f);
        Snapshot(demo, camera, "hud_bright_fixture");
        camera.backgroundColor = new Color(.13f, .16f, .18f);
        hud.SetOptical(false);
        Snapshot(demo, camera, "hud_tint_fixture");
        hud.ToggleDetails();
        Snapshot(demo, camera, "hud_details_fixture");
        hud.SetVisible(false, true);
        if (hud.Expanded || hud.Visibility != 0 || hud.GetComponent<CanvasGroup>().blocksRaycasts)
            throw new Exception("Hidden HUD still expanded or interactive.");
        ((RectTransform)Get(demo, "panel")).gameObject.SetActive(true);
        ((RectTransform)Get(demo, "controls")).gameObject.SetActive(true);
        Call(demo, "OpenSettings");
        Snapshot(demo, camera, "settings");
        demo.ResetCalibration();
        if ((int)Get(demo, "calibration") != 0 || (int)Get(demo, "selected") != -1)
            throw new Exception("Recalibration retained a selected direction.");
        Debug.Log("AR_AIR_VALIDATION passed: missing/zero, invalid coordinates, calibration/reset, structured weather, hidden HUD interaction, optical/tint/detail text layout.");
    }

    static void Snapshot(AirEnvironmentDemo demo, Camera camera, string name)
    {
        var panel = (RectTransform)Get(demo, "panel");
        var controls = (RectTransform)Get(demo, "controls");
        panel.position = new Vector3(0, 0.1f, 2);
        panel.rotation = Quaternion.identity;
        controls.position = new Vector3(0, -0.36f, 2);
        controls.rotation = Quaternion.identity;
        Canvas.ForceUpdateCanvases();
        foreach (var surface in demo.GetComponentsInChildren<AirSoftPanel>()) {
            var renderer = surface.GetComponent<CanvasRenderer>();
            if (renderer == null || renderer.GetMesh() == null || renderer.GetMesh().vertexCount == 0)
                throw new Exception("Rounded surface has no visible geometry: " + surface.name);
        }
        var corners = new Vector3[4];
        foreach (var rect in new[] { panel, controls, ((AirWeatherHud)Get(demo, "hud")).Root })
        {
            if (!rect.gameObject.activeInHierarchy) continue;
            if (rect.GetComponent<CanvasGroup>() != null && rect.GetComponent<CanvasGroup>().alpha == 0) continue;
            rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = camera.WorldToViewportPoint(corner);
                if (point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1)
                    throw new Exception("Panel outside conservative 26-degree vertical viewport.");
            }
        }
        foreach (var text in demo.GetComponentsInChildren<Text>())
        {
            var group = text.GetComponentInParent<CanvasGroup>();
            if (group != null && group.alpha == 0) continue;
            if (text.preferredHeight > text.rectTransform.rect.height + 1)
                throw new Exception("Text overflow in " + name + ": " + text.text + " height=" + text.preferredHeight + " available=" + text.rectTransform.rect.height);
        }
        Directory.CreateDirectory("Logs/air-preview");
        var rt = new RenderTexture(1920, 1080, 24);
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        texture.Apply();
        File.WriteAllBytes("Logs/air-preview/" + name + ".png", texture.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(rt);
    }
}
