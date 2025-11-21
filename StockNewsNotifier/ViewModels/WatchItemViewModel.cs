using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using StockNewsNotifier.Commands;
using StockNewsNotifier.Data.Entities;
using StockNewsNotifier.Services.Interfaces;
using StockNewsNotifier.Views;
using MessageBox = System.Windows.MessageBox;
using WpfApplication = System.Windows.Application;

namespace StockNewsNotifier.ViewModels;

/// <summary>
/// View model representing a single watch item row in the tray window.
/// </summary>
public class WatchItemViewModel : BaseViewModel
{
    private readonly IServiceProvider _services;
    private readonly MainWindowViewModel _parent;
    private bool _alertsEnabled;

    public WatchItemViewModel(MainWindowViewModel parent, IServiceProvider services, WatchItem entity)
    {
        _parent = parent;
        _services = services;

        Id = entity.Id;
        Exchange = entity.Exchange;
        Ticker = entity.Ticker;
        CompanyName = entity.CompanyName;
        IconUrl = entity.IconUrl;
        _alertsEnabled = entity.AlertsEnabled;

        ToggleAlertsCommand = new RelayCommand(async () => await ToggleAlertsAsync());
        ViewNewsCommand = new RelayCommand(async () => await ViewNewsAsync());
        EditSourcesCommand = new RelayCommand(async () => await EditSourcesAsync());
        DeleteCommand = new RelayCommand(async () => await DeleteAsync());
    }

    public Guid Id { get; }
    public string Exchange { get; }
    public string Ticker { get; }
    public string? CompanyName { get; }
    public string? IconUrl { get; }

    public bool AlertsEnabled
    {
        get => _alertsEnabled;
        set
        {
            if (SetProperty(ref _alertsEnabled, value))
            {
                OnPropertyChanged(nameof(AlertGlyph));
                OnPropertyChanged(nameof(AlertToolTip));
                OnPropertyChanged(nameof(AlertMenuText));
            }
        }
    }

    public string SymbolDisplay => $"{Exchange}:{Ticker}";
    public string AlertGlyph => AlertsEnabled ? "🔔" : "🔕";
    public string AlertToolTip => AlertsEnabled ? "Alerts enabled" : "Alerts disabled";
    public string AlertMenuText => AlertsEnabled ? "Turn alerts off" : "Turn alerts on";

    public RelayCommand ToggleAlertsCommand { get; }
    public RelayCommand ViewNewsCommand { get; }
    public RelayCommand EditSourcesCommand { get; }
    public RelayCommand DeleteCommand { get; }

    private async Task ToggleAlertsAsync()
    {
        try
        {
            var watchlistService = _services.GetRequiredService<IWatchlistService>();
            await watchlistService.SetAlertsAsync(Id, !AlertsEnabled);
            AlertsEnabled = !AlertsEnabled;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to toggle alerts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private Task ViewNewsAsync()
    {
        try
        {
            var window = new NewsViewWindow(_services, Id, SymbolDisplay);
            window.Owner = WpfApplication.Current.MainWindow;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open news view: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        return Task.CompletedTask;
    }

    private Task EditSourcesAsync()
    {
        try
        {
            var dialog = new EditSourcePoolDialog(_services, Id, SymbolDisplay);
            dialog.Owner = WpfApplication.Current.MainWindow;
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to edit sources: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        return Task.CompletedTask;
    }

    private async Task DeleteAsync()
    {
        if (MessageBox.Show($"Stop watching {SymbolDisplay}?", "Remove Watch",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var watchlistService = _services.GetRequiredService<IWatchlistService>();
            await watchlistService.RemoveAsync(Id);
            _parent.RemoveWatchItem(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to remove watch item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
