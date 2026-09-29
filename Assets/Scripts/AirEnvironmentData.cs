using System;
using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public sealed class AirPlace
{
    public string name;
    public bool configured;
    public double latitude;
    public double longitude;
    public bool Valid => configured && !double.IsNaN(latitude) && !double.IsNaN(longitude)
        && Math.Abs(latitude) <= 90 && Math.Abs(longitude) <= 180;
}

[Serializable]
public sealed class AirDemoSettings
{
    public bool simulation;
    public AirPlace window = new AirPlace { name = "窗外区域" };
    public AirPlace target = new AirPlace { name = "目标区域（待设置）" };
}

public sealed class AirReading
{
    public string weather = "尚未获取";
    public string air = "尚未获取";
    public string weatherSummary, weatherSource = AirNmcWeather.Source;
    public string temperature = "—", pm25 = "—";
    public int weatherCode = -1;
    public bool isDay = true;
    public string weatherStatus = "等待天气", airStatus = "等待空气数据";
    public DateTimeOffset? weatherTime, airTime;

    public void ClearWeather(string status)
    { weatherSummary = null; temperature = "—"; weatherCode = -1; weatherTime = null; weatherStatus = status; }
    public void ClearAir(string status)
    { pm25 = "—"; airTime = null; airStatus = status; }
    public string WeatherStamp => Stamp(weatherTime, weatherStatus) + (weatherStatus.StartsWith("刷新失败") ? " · 刷新失败" : "");
    public string AirStamp => Stamp(airTime, airStatus);
    static string Stamp(DateTimeOffset? time, string status) => time.HasValue
        ? time.Value.ToLocalTime().ToString("MM-dd HH:mm") + (Math.Abs((DateTimeOffset.UtcNow - time.Value).TotalHours) > 3 ? " · 过期" : "") : status;
}

// All requests use explicitly configured WGS84 coordinates. No IP-based location fallback.
public sealed class AirEnvironmentData : MonoBehaviour
{
    public AirDemoSettings settings;
    public readonly AirReading[] readings = { new AirReading(), new AirReading() };
    public event Action Changed;
    Coroutine refresh;

    [Serializable] public sealed class EnvironmentSnapshot
    {
        public string place, locationSource, source, temperatureC, pm25MicrogramsPerM3;
        public string weatherTime, airTime, weatherStatus, airStatus, weatherDetails, airDetails;
        public bool simulation, placeConfigured, selectedByGaze, weatherAvailable, airAvailable, weatherStale, airStale;
        public double latitude, longitude;
    }
    public string EnvironmentJson(int selected)
    {
        int index = selected == 1 ? 1 : 0;
        var place = index == 1 ? settings.target : settings.window;
        var reading = readings[index];
        var snapshot = new EnvironmentSnapshot {
            place = AirVoiceProtocol.CleanPlace(place.name), locationSource = "手动设置，非实时 GPS",
            latitude = place.latitude, longitude = place.longitude, placeConfigured = place.Valid,
            selectedByGaze = selected >= 0, simulation = settings.simulation,
            source = settings.simulation ? "合成演示数据" : reading.weatherSource + "；空气：Open-Meteo / CAMS 区域模型，非监测站实测，约45km分辨率",
            temperatureC = reading.temperature, pm25MicrogramsPerM3 = reading.pm25,
            weatherTime = reading.weatherTime?.ToString("o") ?? "", airTime = reading.airTime?.ToString("o") ?? "",
            weatherStatus = reading.weatherStatus, airStatus = reading.airStatus,
            weatherAvailable = reading.temperature != "—", airAvailable = reading.pm25 != "—",
            weatherStale = reading.weatherTime.HasValue && Math.Abs((DateTimeOffset.UtcNow - reading.weatherTime.Value).TotalHours) > 3,
            airStale = reading.airTime.HasValue && Math.Abs((DateTimeOffset.UtcNow - reading.airTime.Value).TotalHours) > 3,
            weatherDetails = reading.weather, airDetails = reading.air
        };
        return JsonUtility.ToJson(snapshot);
    }

    public void Reload(AirDemoSettings value)
    {
        if (refresh != null) StopCoroutine(refresh);
        settings = value;
        foreach (var reading in readings)
        {
            reading.ClearWeather("等待天气");
            reading.ClearAir("等待空气数据");
            reading.weather = "等待获取天气…";
            reading.air = "等待获取空气数据…";
        }
        refresh = StartCoroutine(RefreshLoop());
    }

    IEnumerator RefreshLoop()
    {
        while (true)
        {
            for (int i = 0; i < 2; i++)
            {
                var place = i == 0 ? settings.window : settings.target;
                if (settings.simulation)
                {
                    readings[i].temperature = "24";
                    readings[i].weatherCode = 2;
                    readings[i].pm25 = i == 0 ? "28" : "36";
                    readings[i].weatherStatus = readings[i].airStatus = "演示数据";
                    readings[i].weather = "模拟温度  24 °C    模拟湿度  48%\n模拟风速  2.1 m/s · 东北风\n模拟时刻  14:00 · 无实时采样";
                    readings[i].air = $"模拟 PM2.5  {(i == 0 ? 28 : 36)} μg/m³\n模拟 PM10  46 μg/m³    O3  91 μg/m³\n来源：合成案例 · 不代表该地点实际状况";
                }
                else if (!place.Valid)
                {
                    readings[i].ClearWeather("地点未设置");
                    readings[i].ClearAir("地点未设置");
                    readings[i].weather = "尚未设置地点\n请在设置中输入 WGS84 经纬度";
                    readings[i].air = "暂无空气数据\n定位来源：手动设置（未接入 GPS / RTK）";
                }
                else
                {
                    if (!readings[i].weatherTime.HasValue) readings[i].weather = "正在获取天气…";
                    readings[i].air = "正在获取区域空气数据…";
                    Changed?.Invoke();
                    yield return FetchWeather(i, place);
                    yield return Fetch(i, place, true);
                }
                Changed?.Invoke();
            }
            yield return new WaitForSecondsRealtime(300);
        }
    }

    IEnumerator FetchWeather(int index, AirPlace place)
    {
        var reading = readings[index];
        if (!AirNmcWeather.Supports(place)) {
            reading.ClearWeather("天气区域未接入");
            reading.weatherSource = "该地点尚未接入天气源";
            reading.weather = "当前天气源仅覆盖黄村演示区域，请配置对应区域数据源。";
            Changed?.Invoke(); yield break;
        }
        for (int attempt = 0; attempt < 3; attempt++) {
            bool ok = false;
            using (var request = UnityWebRequest.Get(AirNmcWeather.Url)) {
                request.timeout = 12;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success) {
                    try { AirNmcWeather.Apply(reading, request.downloadHandler.text); ok = true; }
                    catch (Exception) { /* Malformed data is unavailable, not a zero reading. */ }
                }
                Debug.Log($"AR_WEATHER source=NMC place={index} attempt={attempt + 1} http={request.responseCode} valid={ok} temperature={(ok ? reading.temperature : "missing")}");
            }
            if (ok) { Changed?.Invoke(); yield break; }
            if (attempt < 2) yield return new WaitForSecondsRealtime(2 * (attempt + 1));
        }
        if (!reading.weatherTime.HasValue) reading.ClearWeather("天气暂不可用");
        else reading.weatherStatus = "刷新失败 · 上次数据";
        reading.weather = "天气刷新失败 · 稍后自动重试\n" +
            (reading.weatherTime.HasValue ? "保留上次温度 " + reading.temperature + " °C · " + reading.WeatherStamp : "暂无有效温度") + "\n" + AirNmcWeather.Source;
        Changed?.Invoke();
    }

    IEnumerator Fetch(int index, AirPlace place, bool air)
    {
        string coordinates = "latitude=" + place.latitude.ToString("F5", CultureInfo.InvariantCulture)
            + "&longitude=" + place.longitude.ToString("F5", CultureInfo.InvariantCulture);
        string url = air
            ? "https://air-quality-api.open-meteo.com/v1/air-quality?" + coordinates + "&current=pm2_5,pm10,ozone&domains=cams_global&timezone=UTC"
            : "https://api.open-meteo.com/v1/forecast?" + coordinates + "&current=temperature_2m,relative_humidity_2m,wind_speed_10m,wind_direction_10m,weather_code,is_day&wind_speed_unit=ms&timezone=UTC";
        using (var request = UnityWebRequest.Get(url))
        {
            request.timeout = 18;
            yield return request.SendWebRequest();
            string text;
            if (air) readings[index].ClearAir("正在获取"); else readings[index].ClearWeather("正在获取");
            if (request.result != UnityWebRequest.Result.Success)
                text = "数据获取失败 · 5 分钟后重试\n请检查网络，或在设置中刷新";
            else
            {
                try
                {
                    text = Describe(request.downloadHandler.text, air);
                    Apply(readings[index], request.downloadHandler.text, air);
                }
                catch (Exception) { text = "数据格式异常 · 本次读数不可用"; }
            }
            if (air) { readings[index].air = text; if (!readings[index].airTime.HasValue) readings[index].ClearAir("暂无空气数据"); }
            else { readings[index].weather = text; if (!readings[index].weatherTime.HasValue) readings[index].ClearWeather("暂无天气数据"); }
            Debug.Log($"AR_AIR_DATA place={index} type={(air ? "air" : "weather")} result={request.result}");
            Changed?.Invoke();
        }
    }

    // The API's current object is flat. Explicit numeric tokens keep null/missing distinct from zero.
    public static string Number(string current, string field)
    {
        var match = Regex.Match(current, "\"" + Regex.Escape(field) + "\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)(?=\\s*[,}])");
        return match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            && !double.IsInfinity(value) ? value.ToString("0.#", CultureInfo.InvariantCulture) : "—";
    }

    public static void Apply(AirReading reading, string json, bool air)
    {
        // Validate the complete current payload before changing any displayed values.
        Describe(json, air);
        var current = Regex.Match(json, "\"current\"\\s*:\\s*\\{([^{}]*)\\}");
        string body = current.Groups[1].Value + "}";
        var time = Regex.Match(body, "\"time\"\\s*:\\s*\"([^\"]+)\"");
        DateTimeOffset observed = DateTimeOffset.Parse(time.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        if (air) { reading.pm25 = Number(body, "pm2_5"); reading.airTime = observed; reading.airStatus = "区域模型"; }
        else
        {
            reading.weatherSummary = null;
            reading.temperature = Number(body, "temperature_2m");
            reading.weatherCode = int.TryParse(Number(body, "weather_code"), out int code) ? code : -1;
            reading.isDay = Number(body, "is_day") != "0";
            reading.weatherTime = observed;
            reading.weatherStatus = "天气模型";
        }
    }

    public static string Condition(int code)
    {
        switch (code)
        {
            case 0: return "晴";
            case 1: return "晴间多云";
            case 2: return "多云";
            case 3: return "阴";
            case 45: case 48: return "雾";
            case 51: case 53: case 55: return "毛毛雨";
            case 56: case 57: case 66: case 67: return "冻雨";
            case 61: case 63: case 65: return "雨";
            case 71: case 73: case 75: case 77: return "雪";
            case 80: case 81: case 82: return "阵雨";
            case 85: case 86: return "阵雪";
            case 95: case 96: case 99: return "雷雨";
            default: return "天气未知";
        }
    }

    public static string Describe(string json, bool air)
    {
        var current = Regex.Match(json, "\"current\"\\s*:\\s*\\{([^{}]*)\\}");
        if (!current.Success) throw new FormatException();
        string body = current.Groups[1].Value + "}";
        var time = Regex.Match(body, "\"time\"\\s*:\\s*\"([^\"]+)\"");
        if (!DateTimeOffset.TryParse(time.Groups[1].Value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var observed)) throw new FormatException();
        string age = Math.Abs((DateTimeOffset.UtcNow - observed).TotalHours) > 3 ? " · 时效待核实" : "";
        string stamp = observed.ToLocalTime().ToString("MM-dd HH:mm") + age;
        if (air) return "PM2.5  " + Number(body, "pm2_5") + " μg/m³    PM10  " + Number(body, "pm10")
            + " μg/m³\nO3  " + Number(body, "ozone") + " μg/m³\n模型时刻 " + stamp + " · 区域参考，非现场测量";
        return "温度  " + Number(body, "temperature_2m") + " °C    湿度  " + Number(body, "relative_humidity_2m")
            + "%\n风速  " + Number(body, "wind_speed_10m") + " m/s    风向（来向） " + Number(body, "wind_direction_10m")
            + "°\n模型时刻 " + stamp;
    }
}
