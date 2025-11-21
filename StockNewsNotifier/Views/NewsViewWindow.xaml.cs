using System;
using System.Collections.ObjectModel;
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
            _items.Clear();
            foreach (var item in news)
            {
                _items.Add(new NewsItemViewModel(item));
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
}
