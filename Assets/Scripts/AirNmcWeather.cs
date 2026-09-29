using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

// Public NMC website feed, not a contracted API. Coverage is intentionally bounded
// to the current Huangcun demo area; never silently substitute Daxing for another city.
public static class AirNmcWeather
{
    public const string Url = "https://www.nmc.cn/rest/real/iEilj";
    public const string Source = "中央气象台 · 大兴天气 · 区域参考，非眼镜现场测量";
    public static bool Supports(AirPlace place) => place.Valid
        && Math.Abs(place.latitude - 39.7244) <= .08 && Math.Abs(place.longitude - 116.336) <= .10;

    public static string Value(JToken value, double min, double max)
    {
        if (value == null || value.Type == JTokenType.Null ||
            !double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double n)
            || double.IsNaN(n) || double.IsInfinity(n) || n < min || n > max) return "—";
        return n.ToString("0.#", CultureInfo.InvariantCulture);
    }

    public static void Apply(AirReading reading, string json)
    {
        var root = JObject.Parse(json);
        if ((string)root["station"]?["code"] != "iEilj" || (string)root["station"]?["city"] != "大兴")
            throw new FormatException("Unexpected NMC station");
        if (!DateTime.TryParseExact((string)root["publish_time"], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var local)) throw new FormatException("Invalid NMC time");
        var time = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(8));
        if (time > DateTimeOffset.UtcNow.AddMinutes(15)) throw new FormatException("Future NMC time");
        var weather = root["weather"] ?? throw new FormatException("Missing NMC weather");
        string temp = Value(weather["temperature"], -90, 60);
        if (temp == "—") throw new FormatException("Missing NMC temperature");
        string condition = (string)weather["info"];
        if (string.IsNullOrEmpty(condition) || condition == "9999" || condition.Length > 12) condition = "天气未知";
        int code = condition == "晴" ? 0 : condition == "多云" ? 2 : condition == "阴" ? 3
            : condition.Contains("雷") ? 95 : condition.Contains("雪") ? 73
            : condition.Contains("雨") ? 61 : condition.Contains("雾") ? 45 : -1;
        reading.temperature = temp;
        reading.weatherCode = code;
        reading.weatherSummary = condition;
        reading.weatherTime = time;
        reading.weatherSource = Source;
        reading.weatherStatus = "大兴区域天气";
        if (DateTime.TryParse((string)root["sunriseSunset"]?["sunrise"], out var sunrise)
            && DateTime.TryParse((string)root["sunriseSunset"]?["sunset"], out var sunset))
            reading.isDay = local >= sunrise && local < sunset;
        reading.weather = "温度  " + temp + " °C    湿度  " + Value(weather["humidity"], 0, 100)
            + "%\n风速  " + Value(root["wind"]?["speed"], 0, 150) + " m/s    风向（来向） "
            + Value(root["wind"]?["degree"], 0, 360) + "°\n发布时刻 " + time.ToString("MM-dd HH:mm") + " 北京时间\n" + Source;
    }
}
