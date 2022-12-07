using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;

namespace TestTaskCurrencyDynamicsViewer
{
    public class ViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private string logPath;
        protected virtual void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
        protected virtual bool Set<T>(ref T field, T value, [CallerMemberName] string PropertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(PropertyName);
            return true;
        }

        void SaveSettings(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "WindowHeight") Properties.Settings.Default.WindowHeight = WindowHeight;
            else if (e.PropertyName == "WindowWidth") Properties.Settings.Default.WindowWidth = WindowWidth;
            else if (e.PropertyName == "WindowTop") Properties.Settings.Default.WindowTop = WindowTop;
            else if (e.PropertyName == "WindowLeft") Properties.Settings.Default.WindowLeft = WindowLeft;
            else if (e.PropertyName == "LeftCurrentDt") Properties.Settings.Default.LeftDate = LeftCurrentDt;
            else if (e.PropertyName == "RightCurrentDt") Properties.Settings.Default.RightDate = RightCurrentDt;
            else if (e.PropertyName == "SelectedCurrency") Properties.Settings.Default.Currency = SelectedCurrency;
            Properties.Settings.Default.Save();
        }

        void LogChanges(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "LeftCurrentDt") File.AppendAllLines(logPath, new List<string>() { $"Начальная дата изменена на {LeftCurrentDt}" });
            else if (e.PropertyName == "RightCurrentDt") File.AppendAllLines(logPath, new List<string>() { $"Конечная дата изменена на {RightCurrentDt}" });
            else if (e.PropertyName == "SelectedCurrency") File.AppendAllLines(logPath, new List<string>() { $"Выбранная валюта изменена на {SelectedCurrency}" });
        }

        private int _windowHeight;
        public int WindowHeight
        {
            get => _windowHeight;
            set => Set(ref _windowHeight, value);
        }
        private int _windowWidth;
        public int WindowWidth
        {
            get => _windowWidth;
            set => Set(ref _windowWidth, value);
        }

        private int _windowTop;
        public int WindowTop
        {
            get => _windowTop;
            set => Set(ref _windowTop, value);
        }
        private int _windowLeft;
        public int WindowLeft
        {
            get => _windowLeft;
            set => Set(ref _windowLeft, value);
        }

        private DateTime periodFrom = DateTime.Today.AddDays(-10);
        private DateTime periodTo = DateTime.Today;
        public DateTime leftMinDt => DateTime.Today.AddYears(-5);
        public DateTime rightMaxDt => DateTime.Today;
        public DateTime LeftCurrentDt
        {
            get => periodFrom;
            set
            {
                if (value >= leftMinDt & value <= RightCurrentDt)
                    Set(ref periodFrom, value);
            }
        }
        public DateTime RightCurrentDt
        {
            get => periodTo;
            set
            {
                if (value >= LeftCurrentDt & value <= rightMaxDt)
                    Set(ref periodTo, value);
            }
        }
        private string inCurrency;
        public string InCurrency
        {
            get => inCurrency;
            set => Set(ref inCurrency, value);
        }
        string _title;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        string currentCurrency;
        public string SelectedCurrency
        {
            get => currentCurrency;
            set => Set(ref currentCurrency, value);
        }

        public ObservableCollection<string> CurrencyNames { get; set; }

        public ViewModel()
        {
            CurrencyNames = new ObservableCollection<string> { "RUB", "USD", "EUR" };
            Title = "Просмотр курсов валют";
            SelectedCurrency = Properties.Settings.Default.Currency;
            LeftCurrentDt = Properties.Settings.Default.LeftDate;
            RightCurrentDt = Properties.Settings.Default.RightDate;
            WindowHeight = Properties.Settings.Default.WindowHeight;
            WindowWidth = Properties.Settings.Default.WindowWidth;
            WindowTop = Properties.Settings.Default.WindowTop;
            WindowLeft = Properties.Settings.Default.WindowLeft;
            logPath = Path.Combine(AppContext.BaseDirectory, "clientLog.txt");
            PropertyChanged += SaveSettings;
            PropertyChanged += LogChanges;
        }

        public RelayCommand ShowDataCommand
        {
            get
            {
                return new RelayCommand(() =>
                {
                    if (LeftCurrentDt > RightCurrentDt)
                    {
                        MessageBox.Show("Начало периода должно быть раньше, чем конец!");
                        return;
                    }
                    List<CurrencyValue> objs = new List<CurrencyValue>();
                    Title = "Просмотр курсов валют - получение данных из API";
                    File.AppendAllLines(logPath, new List<string>() { $"Получение данных с сервера: с {LeftCurrentDt} по {RightCurrentDt} для валюты {SelectedCurrency}." });
                    string json = "";
                    using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }))
                    {
                        client.BaseAddress = new Uri($"https://localhost:5001/currency/");
                        HttpResponseMessage response;
                        try
                        {
                            response = client.GetAsync($"{LeftCurrentDt.ToString("MM.dd.yyyy")}-{RightCurrentDt.ToString("MM.dd.yyyy")}-{SelectedCurrency}").Result;
                        }
                        catch (AggregateException ex)
                        {
                            string notificationText = "Сервер по адресу localhost:5001 не доступен. Убедитесь, что TestTaskCurrencyAPI.exe работает. ";
                            File.AppendAllLines(logPath, new List<string>() { notificationText });
                            MessageBox.Show(notificationText);
                            Title = "Просмотр курсов валют";
                            return;
                        }
                        try
                        {
                            response.EnsureSuccessStatusCode();
                        }
                        catch (System.Net.Http.HttpRequestException)
                        {
                            string notificationText = "Не удалось получить данные с сервера (localhost:5001)";
                            File.AppendAllLines(logPath, new List<string>() { notificationText });
                            MessageBox.Show(notificationText);
                            Title = "Просмотр курсов валют";
                            return;
                        }

                        json = response.Content.ReadAsStringAsync().Result;
                    }
                    objs = JsonConvert.DeserializeObject<List<CurrencyValue>>(json).OrderBy(x => x.Date).ToList();
                    var notFound = objs.Where(x => x.StatusCode == 404).ToList();
                    if (notFound.Count > 0)
                    {
                        string notificationText = $"Для указанного диапазона дат данные с {notFound.Min(x => x.Date)} до {notFound.Max(x => x.Date)} не доступны в API НБ РБ!";
                        File.AppendAllLines(logPath, new List<string>() { notificationText });
                        MessageBox.Show(notificationText);
                    }

                    SeriesCollection = new SeriesCollection
                    {
                        new LineSeries
                        {
                            Title = $"Курс {(objs.FirstOrDefault(x => x.StatusCode != 404)?.Amount + " " ?? "")}{SelectedCurrency}",
                            Values = new ChartValues<double>(objs.Select(y => y.Value).ToList())
                        },
                        new LineSeries
                        {
                            Title = $"Минимальный курс",
                            Values = new ChartValues<double>(objs.Select(y => objs.Min(q => q.Value)).ToList())
                        },
                        new LineSeries
                        {
                            Title = $"Максимальный курс",
                            Values = new ChartValues<double>(objs.Select(y => objs.Max(q => q.Value)).ToList())
                        }
                    };
                    Labels = objs.Select(y => y.Date.ToString("dd.MM.yy")).ToArray();
                    InCurrency = objs.FirstOrDefault(x => x.StatusCode != 404)?.InCurrency ?? "";
                    Title = "Просмотр курсов валют";
                });
            }
        }

        private SeriesCollection _services;
        public SeriesCollection SeriesCollection
        {
            get => _services;
            set => Set(ref _services, value);
        }
        private string[] _labels;
        public string[] Labels { get => _labels; set => Set(ref _labels, value); }
        public Func<double, string> YFormatter { get; set; }
    }

    public class RelayCommand : ICommand
    {
        Action execute;
        Func<object?, bool>? canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action execute, Func<object?, bool>? canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return canExecute == null || canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            execute();
        }
    }
}
