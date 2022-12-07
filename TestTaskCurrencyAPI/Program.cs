using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;

var availableCurrencies = new List<string>() { "USD", "EUR", "RUB"/*, "BTC"*/ };

var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddSingleton<IGetCurrencyValue>(x => new CurrencyGetter(new RandomValue()));
builder.Services.AddSingleton<IGetCurrencyValue>(x => new CurrencyGetter(new ExternalAPI()));

var app = builder.Build();

app.Environment.EnvironmentName = "Production";

app.UseHttpsRedirection();

app.MapGet("/currency", () =>
{
    return app.Services.GetService<IGetCurrencyValue>()?.GetCurrencyValues(new DateTime(2022, 12, 1), DateTime.Today, "USD");
});

app.MapGet("/currency/{stdt:datetime}-{enddt:datetime}-{curr}", (DateTime stdt, DateTime enddt, string curr) =>
{
    if (!availableCurrencies.Contains(curr)) throw new Exception();
    return app.Services.GetService<IGetCurrencyValue>()?.GetCurrencyValues(stdt, enddt, curr);
});

app.Run();

interface IGetCurrencyValue
{
    List<CurrencyValue> GetCurrencyValues(DateTime startDate, DateTime endDate, string currency);
}

public class CurrencyValue
{
    [JsonProperty("Date")]
    private string dateJson { get; set; }
    [JsonIgnore]
    public DateTime Date
    {
        get
        {
            if (DateTime.TryParseExact(dateJson, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                return parsed;
            else return DateTime.ParseExact(dateJson, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            //return DateTime.ParseExact(dateJson, "dd.MM.yyyy", CultureInfo.InvariantCulture);
        }
        set
        {
            dateJson = value.ToString("dd.MM.yyyy");
        }
    }

    public double Value { get; }
    public string Currency { get; }
    public int Amount { get; }
    public string InCurrency { get; }
    public int StatusCode { get; }
    public CurrencyValue(DateTime dt, double value, string currency, int amount, string inCurrency, int statusCode)
    {
        this.Date = dt;
        this.Value = value;
        this.Currency = currency;
        this.Amount = amount;
        this.InCurrency = inCurrency;
        this.StatusCode = statusCode;
    }
}

interface IGetCurrencyForDate
{
    CurrencyValue getValueForDate(DateTime dt, string currency);
}

class CurrencyGetter : IGetCurrencyValue
{
    IGetCurrencyForDate currencyGetter;
    List<CurrencyValue> currencyValues;
    public CurrencyGetter(IGetCurrencyForDate currency)
    {
        this.currencyGetter = currency;
        if (File.Exists(Path.Combine(AppContext.BaseDirectory, "cache.json")))
        {
            var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "cache.json"));
            this.currencyValues = JsonConvert.DeserializeObject<List<CurrencyValue>>(json);
        }
        else
        {
            this.currencyValues = new List<CurrencyValue>();
        }
    }

    public List<CurrencyValue> GetCurrencyValues(DateTime startDate, DateTime endDate, string currency)
    {
        var allDaysBetween = Enumerable.Range(0, 1 + endDate.Subtract(startDate).Days).Select(offset => startDate.AddDays(offset)).ToList();
        //var dctCurrency = currencyValues.GroupBy(x => x.currency).ToDictionary(x => x.Key, x => x.GroupBy(y => y.dt).ToDictionary(y => y.Key, y => y.Single()));
        var toGet = allDaysBetween.Where(x => currencyValues.FirstOrDefault(y => y.Currency == currency & y.Date == x) == null).ToList();
        if (toGet.Count > 0)
        {
            toGet.ForEach(x => currencyValues.Add(currencyGetter.getValueForDate(x, currency)));
            var json = JsonConvert.SerializeObject(currencyValues, Formatting.Indented);
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "cache.json"), json.ToString());
        }
        return allDaysBetween.Select(x => currencyValues.First(y => y.Currency == currency & y.Date == x)).ToList();
    }
}

class RandomValue : IGetCurrencyForDate
{
    public CurrencyValue getValueForDate(DateTime dt, string currency)
    {
        if (currency == "BTC") return new CurrencyValue(dt, new Random().NextDouble(), currency, 1, "USD", 200);
        else if (currency == "RUB") return new CurrencyValue(dt, new Random().NextDouble(), currency, 100, "BYN", 200);
        else return new CurrencyValue(dt, new Random().NextDouble(), currency, 1, "BYN", 200);
    }
}

class ExternalAPI : IGetCurrencyForDate
{
    public CurrencyValue getValueForDate(DateTime dt, string currency)
    {
        if (currency == "RUB")
        {
            string json = "";
            using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }))
            {
                client.BaseAddress = new Uri($"https://www.nbrb.by/api/exrates/rates/");
                HttpResponseMessage response;
                response = client.GetAsync($"298?ondate={dt.Year}-{dt.Month}-{dt.Day}").Result;
                if (response.StatusCode == HttpStatusCode.NotFound) 
                    return new CurrencyValue(dt, 0, currency, 0, "", 404);
                json = response.Content.ReadAsStringAsync().Result;
            }
            var rate = JsonConvert.DeserializeObject<Rate>(json);
            return new CurrencyValue(dt, (double)rate.Cur_OfficialRate, currency, rate.Cur_Scale, "BYN", 200);
        }
        else if (currency == "USD")
        {
            string json = "";
            using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }))
            {
                client.BaseAddress = new Uri($"https://www.nbrb.by/api/exrates/rates/");
                HttpResponseMessage response;
                response = client.GetAsync($"145?ondate={dt.Year}-{dt.Month}-{dt.Day}").Result;
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return new CurrencyValue(dt, 0, currency, 0, "", 404);
                json = response.Content.ReadAsStringAsync().Result;
            }
            var rate = JsonConvert.DeserializeObject<Rate>(json);
            return new CurrencyValue(dt, (double)rate.Cur_OfficialRate, currency, rate.Cur_Scale, "BYN", 200);
        }
        else if (currency == "EUR")
        {
            string json = "";
            using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }))
            {
                client.BaseAddress = new Uri($"https://www.nbrb.by/api/exrates/rates/");
                HttpResponseMessage response;
                response = client.GetAsync($"292?ondate={dt.Year}-{dt.Month}-{dt.Day}").Result;
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return new CurrencyValue(dt, 0, currency, 0, "", 404);
                json = response.Content.ReadAsStringAsync().Result;
            }
            var rate = JsonConvert.DeserializeObject<Rate>(json);
            return new CurrencyValue(dt, (double)rate.Cur_OfficialRate, currency, rate.Cur_Scale, "BYN", 200);
        }
        else if (currency == "BTC")
        {
            //string json = "";
            //using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }))
            //{
            //    client.BaseAddress = new Uri($"https://rest.coinapi.io/v1/exchangerate/");
            //    HttpResponseMessage response;
            //    response = client.GetAsync($"BTC?invert=false292?ondate={dt.Year}-{dt.Month}-{dt.Day}").Result;
            //    if (response.StatusCode == HttpStatusCode.NotFound)
            //        return new CurrencyValue(dt, 0, currency, 0, "", 404);
            //    json = response.Content.ReadAsStringAsync().Result;
            //}
            //var rate = JsonConvert.DeserializeObject<Rate>(json);
            //return new CurrencyValue(dt, (double)rate.Cur_OfficialRate, currency, rate.Cur_Scale, "BYN", 200);
            return new CurrencyValue(dt, new Random().NextDouble(), currency, 1, "USD", 200);
        }
        else
        {
            throw new Exception("Неизвестная валюта!"); 
        }
    }
}

public class Rate
{
    [Key]
    public int Cur_ID { get; set; }
    public DateTime Date { get; set; }
    public string Cur_Abbreviation { get; set; }
    public int Cur_Scale { get; set; }
    public string Cur_Name { get; set; }
    public decimal? Cur_OfficialRate { get; set; }
}