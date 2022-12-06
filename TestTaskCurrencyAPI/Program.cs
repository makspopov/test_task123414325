using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Globalization;

var availableCurrencies = new List<string>() { "USD", "EUR", "RUB", "BTC" };

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IGetCurrencyValue>(x => new CurrencyGetter(new RandomValue()));

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
    public CurrencyValue(DateTime dt, double value, string currency, int amount, string inCurrency)
    {
        this.Date = dt;
        this.Value = value;
        this.Currency = currency;
        this.Amount = amount;
        this.InCurrency = inCurrency;
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
        if (currency == "BTC") return new CurrencyValue(dt, new Random().NextDouble(), currency, 1, "USD");
        else if (currency == "RUB") return new CurrencyValue(dt, new Random().NextDouble(), currency, 100, "BYN");
        else return new CurrencyValue(dt, new Random().NextDouble(), currency, 1, "BYN");
    }
}