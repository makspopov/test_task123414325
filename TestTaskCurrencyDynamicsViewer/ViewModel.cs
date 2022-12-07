using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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

        private int _windowHeight; 
        public int WindowHeight
        {
            get => _windowHeight;
            set
            {
                Properties.Settings.Default.WindowHeight = value;
                Properties.Settings.Default.Save();
                Set(ref _windowHeight, value);
            }
        }
        private int _windowWidth;
        public int WindowWidth
        {
            get => _windowWidth;
            set
            {
                Properties.Settings.Default.WindowWidth = value;
                Properties.Settings.Default.Save();
                Set(ref _windowWidth, value);
            }
        }

        private int _windowTop;
        public int WindowTop
        {
            get => _windowTop;
            set
            {
                Properties.Settings.Default.WindowTop = value;
                Properties.Settings.Default.Save(); 
                Set(ref _windowTop, value);
            }
        }
        private int _windowLeft;
        public int WindowLeft
        {
            get => _windowTop;
            set
            {
                Properties.Settings.Default.WindowLeft = value;
                Properties.Settings.Default.Save();
                Set(ref _windowLeft, value);
            }
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
                {
                    Properties.Settings.Default.LeftDate = value;
                    Properties.Settings.Default.Save();
                    Set(ref periodFrom, value);
                }                    
            }
        }
        public DateTime RightCurrentDt
        {
            get => periodTo;
            set
            {
                if (value >= LeftCurrentDt & value <= rightMaxDt)
                {
                    Properties.Settings.Default.RightDate = value;
                    Properties.Settings.Default.Save();
                    Set(ref periodTo, value);
                }                
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
            set
            {
                Properties.Settings.Default.Currency = value;
                Properties.Settings.Default.Save(); 
                Set(ref currentCurrency, value);
            }
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
                            MessageBox.Show("Сервер по адресу localhost:5001 не доступен. Убедитесь, что TestTaskCurrencyAPI.exe работает. ");
                            Title = "Просмотр курсов валют";
                            return;
                        }
                        try
                        {
                            response.EnsureSuccessStatusCode();
                        }
                        catch (System.Net.Http.HttpRequestException)
                        {
                            MessageBox.Show("Не удалось получить данные с сервера (localhost:5001)");
                            Title = "Просмотр курсов валют";
                            return;
                        }

                        json = response.Content.ReadAsStringAsync().Result;
                    }
                    objs = JsonConvert.DeserializeObject<List<CurrencyValue>>(json).OrderBy(x => x.Date).ToList();
                    var notFound = objs.Where(x => x.StatusCode == 404).ToList();
                    if (notFound.Count > 0)
                        MessageBox.Show($"Для указанного диапазона дат данные с {notFound.Min(x => x.Date)} до {notFound.Max(x => x.Date)} не доступны в API НБ РБ!");

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
