using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
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
            YFormatter = value => String.Format("{0:0,0.0} BYN", value);
            SelectedCurrency = "USD";
        }

        public RelayCommand ShowDataCommand
        {
            get
            {
                return new RelayCommand(() =>
                {
                    SeriesCollection = new SeriesCollection
                    {
                        new LineSeries
                        {
                            Title = $"Курс {SelectedCurrency}",
                            Values = new ChartValues<int>(Enumerable.Range(0, (int)(RightCurrentDt - LeftCurrentDt).TotalDays).Select(x => new Random().Next()))
                        }
                    };
                    Labels = Enumerable.Range(0, (int)(RightCurrentDt - LeftCurrentDt).TotalDays).Select((v, i) => LeftCurrentDt.AddDays(i).ToShortDateString()).ToArray();
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
