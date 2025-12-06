using System;

namespace StockNewsNotifier.Services.Interfaces;

public interface IUnreadCountNotifier
{
    event EventHandler<UnreadCountChangedEventArgs>? UnreadCountChanged;

    void Publish(Guid watchItemId, int? delta = null, int? absoluteCount = null);
}

public sealed class UnreadCountChangedEventArgs : EventArgs
{
    public UnreadCountChangedEventArgs(Guid watchItemId, int? delta, int? absoluteCount)
    {
        WatchItemId = watchItemId;
        Delta = delta;
        AbsoluteCount = absoluteCount;
    }

    public Guid WatchItemId { get; }
    public int? Delta { get; }
    public int? AbsoluteCount { get; }
}
