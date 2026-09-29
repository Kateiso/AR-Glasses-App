using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
public static class AirVoiceValidation
{
    [Serializable] class TextValue { public string text; }
    public static void Run()
    {
        var nmc = new AirReading();
        string fixture = "{\"station\":{\"code\":\"iEilj\",\"city\":\"大兴\"},\"publish_time\":\"2026-09-28 18:50\",\"weather\":{\"temperature\":0,\"humidity\":9999,\"info\":\"多云\"},\"wind\":{\"speed\":0,\"degree\":211}}";
        AirNmcWeather.Apply(nmc, fixture);
        if (nmc.temperature != "0" || nmc.weatherCode != 2 || nmc.weatherTime.Value.UtcDateTime.Hour != 10
            || !nmc.weather.Contains("湿度  —") || !nmc.weather.Contains("风速  0")) throw new Exception("NMC units/time/missing/zero failed");
        bool rejected = false;
        try { AirNmcWeather.Apply(nmc, fixture.Replace("iEilj", "wrong")); } catch (FormatException) { rejected = true; }
        if (!rejected) throw new Exception("Wrong NMC station accepted");
        rejected = false;
        try { AirNmcWeather.Apply(nmc, fixture.Replace("temperature\":0", "temperature\":9999")); } catch (FormatException) { rejected = true; }
        if (!rejected) throw new Exception("NMC missing temperature accepted");
        if (!AirNmcWeather.Supports(new AirPlace { configured = true, latitude = 39.7244, longitude = 116.336 })
            || AirNmcWeather.Supports(new AirPlace { configured = true, latitude = 31.2, longitude = 121.5 }))
            throw new Exception("Weather coverage boundary failed");
        var knowledge = Resources.Load<TextAsset>("DaxingEnvironmentKnowledge");
        if (knowledge == null || !knowledge.text.Contains("29.5") || !knowledge.text.Contains("核验日期"))
            throw new Exception("Daxing knowledge asset missing");
        string settingsFile = Path.GetTempFileName();
        try {
            File.WriteAllText(settingsFile, "old"); File.SetAttributes(settingsFile, FileAttributes.ReadOnly);
            AirSettingsStorage.Save(settingsFile, "new");
            if (File.ReadAllText(settingsFile) != "new") throw new Exception("Read-only settings replacement failed");
        } finally { File.SetAttributes(settingsFile, FileAttributes.Normal); File.Delete(settingsFile); }
        string sample = "北京\n\"test\"\\\t";
        if (JsonUtility.FromJson<TextValue>("{\"text\":" + AirVoiceProtocol.Quote(sample) + "}").text != sample)
            throw new Exception("Voice JSON escaping failed");
        if (AirVoiceProtocol.CleanPlace("大兴区生态环境局附近（预设）") != "大兴区生态环境局附近") throw new Exception("Place migration failed");
        string migrated = AirVoiceProtocol.MigrateSettings("{\"_preset\":{\"source\":\"keep\"},\"window\":{\"name\":\"地点（预设）\",\"latitude\":39.7}}");
        var migratedObject = Newtonsoft.Json.Linq.JObject.Parse(migrated);
        if ((string)migratedObject["_preset"]["source"] != "keep" || (double)migratedObject["window"]["latitude"] != 39.7 || (string)migratedObject["window"]["name"] != "地点")
            throw new Exception("Settings migration loses provenance or coordinates");
        var ready = JsonConvert.DeserializeObject<AirVoiceProtocol.Envelope>("{\"setupComplete\":{}}");
        if (ready.setupComplete == null || ready.serverContent != null) throw new Exception("Setup parsing failed");
        var envelope = JsonConvert.DeserializeObject<AirVoiceProtocol.Envelope>("{\"serverContent\":{\"interrupted\":true,\"turnComplete\":true,\"modelTurn\":{\"parts\":[{\"inlineData\":{\"mimeType\":\"audio/pcm;rate=24000\",\"data\":\"AAA=\"}}]}}}");
        if (!envelope.serverContent.interrupted || Convert.FromBase64String(envelope.serverContent.modelTurn.parts[0].inlineData.data).Length != 2)
            throw new Exception("Audio/interruption parsing failed");
        var config = new AirVoiceCredentials { project = "kateiso-core", location = "us-central1", model = "gemini-live-2.5-flash-native-audio", accessToken = "fixture", expiresAt = 1 };
        bool expired = false;
        try { config.Validate(); } catch (UnauthorizedAccessException) { expired = true; }
        if (!expired) throw new Exception("Expired token accepted");
        config.expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600; config.Validate();
        var go = new GameObject("Voice environment fixture");
        try {
            var data = go.AddComponent<AirEnvironmentData>();
            data.settings = new AirDemoSettings();
            data.settings.window.name = "窗外地点"; data.settings.target.name = "第二地点";
            data.readings[1].pm25 = "0"; data.readings[1].airTime = DateTimeOffset.UtcNow.AddHours(-4);
            var snapshot = JsonUtility.FromJson<AirEnvironmentData.EnvironmentSnapshot>(data.EnvironmentJson(1));
            if (snapshot.place != "第二地点" || !snapshot.airAvailable || !snapshot.airStale || snapshot.weatherAvailable || snapshot.pm25MicrogramsPerM3 != "0")
                throw new Exception("Environment tool loses missing/zero/stale/selection distinctions");
        } finally { UnityEngine.Object.DestroyImmediate(go); }
        Debug.Log("AR_VOICE_VALIDATION passed: credential expiry, escaping, parsing, place migration, selected context, missing/zero/stale");
    }
}
