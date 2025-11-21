# StockNewsNotifier – Implementation Status

## Project Snapshot
- Windows tray application (WPF + WinForms NotifyIcon) crawling stock news and displaying toast notifications.
- Stack: .NET 8, EF Core + SQLite, AngleSharp, Polly, Serilog, Microsoft.Toolkit.Uwp.Notifications.
- Repository layout:
  ```
  StockNewsNotifier/
  ├── StockNewsNotifier/            # Main WPF project
  │   ├── App.xaml(.cs)             # Generic host + DI bootstrap
  │   ├── MainWindow.xaml(.cs)      # Tray UI shell
  │   ├── BackgroundServices/       # NewsPollerHostedService
  │   ├── Commands/, Converters/, ViewModels/, Views/
  │   ├── Services/                 # EF-backed services + crawlers
  │   ├── Utilities/                # Dedupe, URL canonicalizer, etc.
  │   └── Data/                     # EF entities, DbContext, migrations
  └── tests/StockNewsNotifier.Tests # Fixture-based crawler smoke tests
  ```

## Phase Progress

| Phase | Status | Notes |
|-------|--------|-------|
| Phase 1 – Foundation | ✅ Complete | Project scaffolding, EF models, migrations, Serilog. |
| Phase 2 – Core Services | ✅ Complete | Watchlist/News services, utilities, interfaces. |
| Phase 3 – Yahoo Crawler | ✅ Complete | HttpClient + AngleSharp crawler, parser, retry policies. |
| Phase 3 – Testing | ✅ Complete | Fixture-based parser + crawler smoke tests (`tests/StockNewsNotifier.Tests`). |
| Phase 4-5 – Background Polling | 🚧 In progress | Scheduler + hosted service live; remaining work listed below. |
| Phase 6 – Tray/UI | 🚧 In progress | Tray icon & watchlist UI implemented; advanced views pending. |
| Phase 7 – Notifications | ❌ Not started | Placeholder logging implementation only. |

### Phase 3 Testing Highlights
- HttpClient requests mimic browser headers, include referrer/sec-fetch headers, and run through Polly retry/backoff.
- Yahoo parser handles multiple layouts (`[data-testid='storyitem']`, `li.js-stream-content`), canonicalizes URLs, and parses absolute/relative timestamps.
- Two offline smoke tests validate the HTML parser and crawler against saved Yahoo fixtures (`msft_news_sample.html`, `aapl_news_sample.html`) to catch regressions without network access.

### Phase 4-5 – Background Polling Status
- ✅ ChannelScheduler with duplicate suppression + `MarkCompleted` to avoid re-enqueue storms.
- ✅ `NewsPollerHostedService` polls watchlist on configurable interval/jitter, enqueues crawls, and processes results sequentially.
- ✅ CrawlState persistence tracks RPS/RPM, last crawl, errors, and cached robots.txt.
- ✅ Rate limits configurable per host (default + overrides) and enforced before each fetch.
- ✅ robots.txt fetched on demand and cached for `Crawler.RobotsCacheHours` (default 24h).
- ✅ App start seeds default sources (Yahoo, Reuters, Google Finance, Investing, WSJ) and backfills Yahoo for existing watch items. New watch items always attach Yahoo automatically, ensuring crawlers have an entry point.
- ✅ UI proactively enqueues crawler jobs: when the watchlist first loads and whenever a user adds a ticker, the ChannelScheduler queues those watch items immediately so `View News` has data without waiting for the background interval.
- ⏳ TODO: obey Disallow rules from cached robots.txt, add richer diagnostics/tests for rate limiting, and evaluate prioritization for multi-source watch queues.

### Phase 6 – Tray/UI Status
- ✅ NotifyIcon integration using `tray_icon.ico`, left-click restore, context menu (Open/Exit), hide-on-close/minimize, tidy shutdown.
- ✅ Main window now lists watch items with icon placeholder, `EXCHANGE:TICKER`, company name, bell glyph for alerts, and per-row “…” menu:
  - Toggle Alerts (calls `IWatchlistService.SetAlertsAsync`)
  - View News (opens `NewsViewWindow`, shows read/unread articles with source/time)
  - Edit Search Pool (opens `EditSourcePoolDialog` with checklist of Yahoo/Reuters/Google/Investing/WSJ)
  - Delete watch item (removes via `IWatchlistService.RemoveAsync`)
- ✅ “+” button launches `AddWatchDialog` to collect `EXCHANGE:TICKER` and add via `IWatchlistService.AddAsync`.
- ✅ News view window auto-refreshes from `INewsService`, and the add workflow now triggers an immediate crawl so newly added tickers populate news shortly after submission.
- ✅ View models (`MainWindowViewModel`, `WatchItemViewModel`, `NewsItemViewModel`) plus `RelayCommand` and `NullToVisibilityConverter` support binding and commands.
- ⏳ TODO: bind real company logos, implement search/filter UI, and flesh out news list interactions (mark read, open in browser, etc.).

### Phase 7 – Notifications (pending)
- Current `NotificationService` just logs events; Windows toast implementation still outstanding (including activation handling & throttling).

## Outstanding Tasks / Next Focus
1. **Robots.txt enforcement** – parse cached robots text to skip disallowed paths before invoking crawlers.
2. **Scheduler diagnostics** – add logging/tests verifying rate-limit delays and queue throughput under load.
3. **Watchlist UI polish** – display actual icons, add sorting/filter options, persist user preferences.
4. **News UX** – allow marking articles read/unread, open links in browser, and show summaries.
5. **Toast notifications** – implement Windows toast service per design (buttons for open/mark read, AUMID registration).

## Testing
- `tests/StockNewsNotifier.Tests` is a console runner executing the parser/crawler fixture smoke tests. Run with:
  ```
  dotnet run --project tests/StockNewsNotifier.Tests
  ```
- Manual testing: add watch items via tray window (`+`), verify crawler logs, ensure UI interactions work (alerts toggle, news dialog, source editing).

## Logging & Data Paths
- Logs: `%LocalAppData%\StockNewsNotifier\Logs\app-YYYYMMDD.log`
- SQLite DB: `%LocalAppData%\StockNewsNotifier\news.db`
- Debug Yahoo HTML: `%TEMP%\yahoo_finance_debug.html`
