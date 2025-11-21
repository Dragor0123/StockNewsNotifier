using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using StockNewsNotifier.Commands;
using StockNewsNotifier.Services.Interfaces;
using StockNewsNotifier.Views;
using MessageBox = System.Windows.MessageBox;
using WpfApplication = System.Windows.Application;

namespace StockNewsNotifier.ViewModels;

public class MainWindowViewModel : BaseViewModel
{
    private readonly IServiceProvider _services;
    private bool _isBusy;
    private bool _initialQueueCompleted;

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;
        WatchItems = new ObservableCollection<WatchItemViewModel>();

        RefreshCommand = new RelayCommand(async _ => await LoadWatchItemsAsync(), _ => !IsBusy);
        AddWatchCommand = new RelayCommand(async _ => await AddWatchItemAsync(), _ => !IsBusy);
    }

    public ObservableCollection<WatchItemViewModel> WatchItems { get; }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand AddWatchCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
                AddWatchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task LoadWatchItemsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var watchlistService = _services.GetRequiredService<IWatchlistService>();
            var items = await watchlistService.ListAsync();

            WatchItems.Clear();
            foreach (var item in items)
            {
                WatchItems.Add(new WatchItemViewModel(this, _services, item));
            }

            if (!_initialQueueCompleted && WatchItems.Count > 0)
            {
                QueueCrawlsFor(WatchItems.Select(w => w.Id));
                _initialQueueCompleted = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load watchlist: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void RemoveWatchItem(WatchItemViewModel item)
    {
        WatchItems.Remove(item);
    }

    private async Task AddWatchItemAsync()
    {
        try
        {
            var dialog = new AddWatchDialog
            {
                Owner = WpfApplication.Current.MainWindow
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var exchange = dialog.Exchange?.Trim().ToUpperInvariant();
            var ticker = dialog.Ticker?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(exchange) || string.IsNullOrWhiteSpace(ticker))
            {
                MessageBox.Show("Please enter both exchange and ticker.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var watchlist = _services.GetRequiredService<IWatchlistService>();
            var added = await watchlist.AddAsync(new Ticker(exchange, ticker));

            // reload list to include navigation properties
            await LoadWatchItemsAsync();

            QueueCrawlsFor(new[] { added.Id });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to add watch item: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void QueueCrawlsFor(IEnumerable<Guid> watchItemIds)
    {
        var scheduler = _services.GetRequiredService<IScheduler>();
        foreach (var id in watchItemIds)
        {
            scheduler.EnqueueCrawl(id);
        }
    }
}
