using LiveCharts;
using LiveCharts.Wpf;
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

        private DateTime periodFrom = DateTime.Today.AddDays(-10);
        private DateTime periodTo = DateTime.Today;
        public DateTime leftMinDt => DateTime.Today.AddYears(-5);
        public DateTime rightMaxDt => DateTime.Today;
        public DateTime LeftCurrentDt
        {
            get => periodFrom;
            set => Set(ref periodFrom, value);
        }
        public DateTime RightCurrentDt
        {
            get => periodTo;
            set => Set(ref periodTo, value);
        }
        private string inCurrency;
        public string InCurrency
        {
            get => inCurrency;
            set => Set(ref inCurrency, value);
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
            CurrencyNames = new ObservableCollection<string> { "RUB", "USD", "EUR", "BTC" };
            SelectedCurrency = "USD";
        }

        public RelayCommand ShowDataCommand
        {
            get
            {
                return new RelayCommand(() =>
                {
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
                            return;
                        }
                        try
                        {
                            response.EnsureSuccessStatusCode();
                        }
                        catch (System.Net.Http.HttpRequestException)
                        {
                            MessageBox.Show("Не удалось получить данные с сервера (localhost:5001)");
                            return;
                        }

                        json = response.Content.ReadAsStringAsync().Result;
                    }
                    var objs = JsonConvert.DeserializeObject<List<CurrencyValue>>(json).OrderBy(x => x.Date).ToList();

                    SeriesCollection = new SeriesCollection
                    {
                        new LineSeries
                        {
                            Title = $"Курс {(objs.FirstOrDefault()?.Amount + " " ?? "")}{SelectedCurrency}",
                            Values = new ChartValues<double>(objs.Select(y => y.Value).ToList())
                        }
                    };
                    Labels = objs.Select(y => y.Date.ToString("dd.MM.yy")).ToArray();
                    InCurrency = objs.FirstOrDefault()?.InCurrency ?? "";
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
