using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ConsoleApp_MAF;

public class OpenMeteo
{
    [Description("依照經緯度取得現在地的天氣")]
    async public Task<ToolResponse> GetCurrent([Description("經度")] double Long, [Description("緯度")] double Lat)
    {
        var resp = new ToolResponse();
        try
        {
            using var client = new HttpClient();
            string url = $"https://api.open-meteo.com/v1/forecast?latitude={Lat}&longitude={Long}&current_weather=true";
            string response = await client.GetStringAsync(url);
            var weather = JsonSerializer.Deserialize<WeatherResponse>(response);
            if(weather != null)
            {
                resp.Data = weather.CurrentWeather.Temperature.ToString();
            }
            else
            {
                resp.FailMessgae = "not find";
            }
        }
        catch (Exception ex)
        {
            resp.FailMessgae = ex.Message;
        }

        return resp;
    }
}


public class WeatherResponse
{
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("generationtime_ms")]
    public double GenerationTimeMs { get; set; }

    [JsonPropertyName("utc_offset_seconds")]
    public int UtcOffsetSeconds { get; set; }

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; }

    [JsonPropertyName("timezone_abbreviation")]
    public string TimezoneAbbreviation { get; set; }

    [JsonPropertyName("elevation")]
    public double Elevation { get; set; }

    [JsonPropertyName("current_weather_units")]
    public CurrentWeatherUnits CurrentWeatherUnits { get; set; }

    [JsonPropertyName("current_weather")]
    public CurrentWeather CurrentWeather { get; set; }
}

public class CurrentWeatherUnits
{
    [JsonPropertyName("time")]
    public string Time { get; set; }

    [JsonPropertyName("interval")]
    public string Interval { get; set; }

    [JsonPropertyName("temperature")]
    public string Temperature { get; set; }

    [JsonPropertyName("windspeed")]
    public string Windspeed { get; set; }

    [JsonPropertyName("winddirection")]
    public string WindDirection { get; set; }

    [JsonPropertyName("is_day")]
    public string IsDay { get; set; }

    [JsonPropertyName("weathercode")]
    public string WeatherCode { get; set; }
}

public class CurrentWeather
{
    [JsonPropertyName("time")]
    public DateTime Time { get; set; }

    [JsonPropertyName("interval")]
    public int Interval { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("windspeed")]
    public double Windspeed { get; set; }

    [JsonPropertyName("winddirection")]
    public int WindDirection { get; set; }

    [JsonPropertyName("is_day")]
    public int IsDay { get; set; }

    [JsonPropertyName("weathercode")]
    public int WeatherCode { get; set; }
}

