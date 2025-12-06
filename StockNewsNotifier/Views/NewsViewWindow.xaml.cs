using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using StockNewsNotifier.Services.Interfaces;
using StockNewsNotifier.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace StockNewsNotifier.Views;

public partial class NewsViewWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly Guid _watchItemId;
    private readonly ObservableCollection<NewsItemViewModel> _items = new();

    public NewsViewWindow(IServiceProvider services, Guid watchItemId, string title)
    {
        _services = services;
        _watchItemId = watchItemId;

        InitializeComponent();
        HeaderText.Text = title;
        NewsList.ItemsSource = _items;

        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var newsService = _services.GetRequiredService<INewsService>();
            var news = await newsService.ListAsync(_watchItemId, 7, unreadOnly: false, CancellationToken.None);
            var ordered = news
                .Select(n => new NewsItemViewModel(n))
                .OrderBy(n => n.IsRead) // unread (false) first
                .ThenByDescending(n => n.Timestamp ?? DateTime.MinValue)
                .ToList();

            _items.Clear();
            foreach (var item in ordered)
            {
                _items.Add(item);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load news: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void OnItemClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.ListViewItem item ||
            item.DataContext is not NewsItemViewModel vm)
        {
            return;
        }

        await OpenArticleAsync(vm);
    }

    private async Task OpenArticleAsync(NewsItemViewModel vm)
    {
        if (!string.IsNullOrWhiteSpace(vm.Url))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = vm.Url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open article: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        if (!vm.IsRead)
        {
            try
            {
                var newsService = _services.GetRequiredService<INewsService>();
                await newsService.MarkReadAsync(vm.Id, true, CancellationToken.None);
                vm.IsRead = true;
                ResortItems();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to mark as read: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ResortItems()
    {
        var ordered = _items
            .OrderBy(n => n.IsRead)
            .ThenByDescending(n => n.Timestamp ?? DateTime.MinValue)
            .ToList();

        _items.Clear();
        foreach (var item in ordered)
        {
            _items.Add(item);
        }
    }
}
