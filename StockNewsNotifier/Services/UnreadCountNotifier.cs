using StockNewsNotifier.Services.Interfaces;

namespace StockNewsNotifier.Services;

/// <summary>
/// Simple event-based notifier for unread count changes.
/// </summary>
public class UnreadCountNotifier : IUnreadCountNotifier
{
    public event EventHandler<UnreadCountChangedEventArgs>? UnreadCountChanged;

    public void Publish(Guid watchItemId, int? delta = null, int? absoluteCount = null)
    {
        UnreadCountChanged?.Invoke(this, new UnreadCountChangedEventArgs(watchItemId, delta, absoluteCount));
    }
}
