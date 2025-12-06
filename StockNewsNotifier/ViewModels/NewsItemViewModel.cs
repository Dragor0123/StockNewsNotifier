using System;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using StockNewsNotifier.Data.Entities;

namespace StockNewsNotifier.ViewModels;

public class NewsItemViewModel : BaseViewModel
{
    public NewsItemViewModel(NewsItem entity)
    {
        Id = entity.Id;
        WatchItemId = entity.WatchItemId;
        Url = entity.Url;
        Title = entity.Title;
        SourceName = entity.Source?.DisplayName ?? entity.Source?.Name ?? "Unknown";
        Timestamp = entity.PublishedUtc ?? entity.FetchedUtc;
        _isRead = entity.IsRead;
    }

    public Guid Id { get; }
    public Guid WatchItemId { get; }
    public string Url { get; }
    public string Title { get; }
    public string SourceName { get; }
    public DateTime? Timestamp { get; }
    private bool _isRead;
    public bool IsRead
    {
        get => _isRead;
        set
        {
            if (SetProperty(ref _isRead, value))
            {
                OnPropertyChanged(nameof(Background));
            }
        }
    }

    public string SourceAndTime => $"{SourceName} • {(Timestamp?.ToLocalTime().ToString("g") ?? "Unknown")}";
    public Brush Background => IsRead ? Brushes.WhiteSmoke : Brushes.White;
}
