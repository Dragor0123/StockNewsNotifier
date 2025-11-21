using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockNewsNotifier.Data;
using StockNewsNotifier.Data.Entities;
using MessageBox = System.Windows.MessageBox;

namespace StockNewsNotifier.Views;

public partial class EditSourcePoolDialog : Window
{
    private static readonly SourceDefinition[] DefaultSources =
    {
        new("YahooFinance", "Yahoo Finance", "https://finance.yahoo.com"),
        new("Reuters", "Reuters", "https://www.reuters.com/"),
        new("GoogleFinance", "Google Finance", "https://www.google.com/finance/"),
        new("Investing", "Investing.com", "https://www.investing.com/"),
        new("WSJ", "Wall Street Journal", "https://www.wsj.com/")
    };

    private readonly IServiceProvider _services;
    private readonly Guid _watchItemId;
    private readonly ObservableCollection<SourceOption> _options = new();

    public EditSourcePoolDialog(IServiceProvider services, Guid watchItemId, string title)
    {
        _services = services;
        _watchItemId = watchItemId;

        InitializeComponent();
        HeaderText.Text = title;
        SourceList.ItemsSource = _options;

        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var sourceMap = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in DefaultSources)
            {
                var source = await db.Sources.FirstOrDefaultAsync(s => s.Name == definition.Name);
                if (source == null)
                {
                    source = new Source
                    {
                        Name = definition.Name,
                        DisplayName = definition.DisplayName,
                        BaseUrl = definition.BaseUrl,
                        Enabled = true
                    };
                    db.Sources.Add(source);
                    await db.SaveChangesAsync();
                }

                sourceMap[source.Name] = source;
            }

            var watchItem = await db.WatchItems
                .Include(w => w.WatchItemSources)
                .FirstOrDefaultAsync(w => w.Id == _watchItemId);

            _options.Clear();
            foreach (var source in sourceMap.Values.OrderBy(s => s.DisplayName))
            {
                var existing = watchItem?.WatchItemSources.FirstOrDefault(ws => ws.SourceId == source.Id);
                _options.Add(new SourceOption(source.Id, source.DisplayName ?? source.Name, existing?.Enabled ?? false));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load sources: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private async void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var watchItem = await db.WatchItems
                .Include(w => w.WatchItemSources)
                .FirstOrDefaultAsync(w => w.Id == _watchItemId);

            if (watchItem == null)
            {
                MessageBox.Show("Watch item not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            foreach (var option in _options)
            {
                var relation = watchItem.WatchItemSources.FirstOrDefault(ws => ws.SourceId == option.SourceId);
                if (relation == null)
                {
                    relation = new WatchItemSource
                    {
                        WatchItemId = watchItem.Id,
                        SourceId = option.SourceId,
                        Enabled = option.IsSelected
                    };
                    watchItem.WatchItemSources.Add(relation);
                }
                else
                {
                    relation.Enabled = option.IsSelected;
                }
            }

            await db.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save sources: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private sealed record SourceDefinition(string Name, string DisplayName, string BaseUrl);

    private sealed class SourceOption : INotifyPropertyChanged
    {
        private bool _isSelected;

        public SourceOption(int sourceId, string displayName, bool isSelected)
        {
            SourceId = sourceId;
            DisplayName = displayName;
            _isSelected = isSelected;
        }

        public int SourceId { get; }
        public string DisplayName { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
