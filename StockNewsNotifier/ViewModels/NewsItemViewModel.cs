using System;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using StockNewsNotifier.Data.Entities;

namespace StockNewsNotifier.ViewModels;

public class NewsItemViewModel : BaseViewModel
{
    public NewsItemViewModel(NewsItem entity)
    {
        Title = entity.Title;
        SourceName = entity.Source?.DisplayName ?? entity.Source?.Name ?? "Unknown";
        Timestamp = entity.PublishedUtc ?? entity.FetchedUtc;
        IsRead = entity.IsRead;
    }

    public string Title { get; }
    public string SourceName { get; }
    public DateTime? Timestamp { get; }
    public bool IsRead { get; }

    public string SourceAndTime => $"{SourceName} • {(Timestamp?.ToLocalTime().ToString("g") ?? "Unknown")}";
    public Brush Background => IsRead ? Brushes.WhiteSmoke : Brushes.White;
}
