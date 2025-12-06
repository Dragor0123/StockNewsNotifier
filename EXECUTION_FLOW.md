# StockNewsNotifier 실행 흐름 요약

## 부팅 & 호스트 구성
- `App.OnStartup` → Serilog 파일 로거 설정(`%LocalAppData%/StockNewsNotifier/Logs`).
- `Host.CreateDefaultBuilder`에서 `appsettings.json`(실행 파일 위치) 로딩, DI/로그 구성.
- 서비스 등록: `AppDbContext`(SQLite `news.db`), `WatchlistService`, `NewsService`, `NotificationService`, `ChannelScheduler`, `NewsPollerHostedService`, `ISourceCrawler`(현재 YahooFinance).
- DB 경로: `%LocalAppData%/StockNewsNotifier/news.db`.
- `SourceSeeder.EnsureDefaultsAsync`에서 `Database.MigrateAsync()`로 최신 마이그레이션 적용 후 기본 소스(YahooFinance)와 WatchItem-Sources 관계 보강.
- 호스트 시작 후 `MainWindow` 생성 및 표시.

## UI 흐름(MainWindow)
- `MainWindow` 생성 시 `MainWindowViewModel`을 DI와 함께 설정, 트레이 아이콘 생성.
- 로드 시 `LoadWatchItemsAsync` 호출 → `WatchlistService.ListAsync`로 DB의 워치리스트 가져와 `WatchItemViewModel` 컬렉션 채움.
- 첫 로드에 항목이 있으면 각 워치아이템 ID를 `ChannelScheduler`에 enqueue하여 초기 크롤 실행을 예약.
- 사용자 액션:
  - 새 티커 추가 → `AddWatchDialog` → `WatchlistService.AddAsync`(없으면 생성, YahooFinance 소스 연결) → 리스트 재로드 후 해당 ID 크롤 enqueue.
  - 새로고침 → `LoadWatchItemsAsync`.
  - 행별 메뉴로 알림 토글/삭제/소스 편집 등 수행(서비스 호출 후 UI 갱신).
- 창 최소화 시 숨기고 트레이에서 복귀 가능, Exit 선택 시 종료.

## 백그라운드 처리
- `ChannelScheduler`가 워치아이템 ID를 중복 없이 큐에 적재, `NewsPollerHostedService`가 읽어 처리 후 완료 시 해제.
- `NewsPollerHostedService.ExecuteAsync`:
  1) 별도 루프 `PollWatchlistAsync`: 설정(`Polling:DefaultIntervalSeconds`, `JitterSeconds`) 주기로 전체 워치아이템을 다시 enqueue.
  2) 채널에서 ID를 읽어 `ProcessWatchItemAsync` 실행.
- `ProcessWatchItemAsync`:
  - 스코프 DB/서비스(HttpClientFactory, crawlers 등) 획득 후 워치아이템 + 활성 소스 로드.
  - 각 활성 소스에 대해 크롤러 선택(`ISourceCrawler.Name` 일치) 후 `CrawlSourceAsync`.
- `CrawlSourceAsync` 핵심 단계:
  - `CrawlState` 생성/동기화 및 레이트리밋 설정(RPS/분당, `RateLimits` 설정 또는 기본값).
  - `robots.txt` 캐시 확인/갱신(`Crawler:RobotsCacheHours`).
  - 레이트리밋 대기 후 `crawler.FetchAsync`로 기사 수집.
  - `NewsService.IngestAsync`로 새 기사만 저장(중복 URL/제목 해시 차단).
  - 새 기사가 있고 알림 허용 시 `NotificationService.NotifyAsync` 호출 후 플래그 저장.
  - 성공/실패에 따라 `CrawlState`에 최근 크롤 시점/오류 기록.

## 설정 포인트
- `appsettings.json`: Polling, RateLimits, Crawler 설정(실행 디렉터리).
- `global.json`: .NET 8 SDK 고정.
- 로그: `%LocalAppData%/StockNewsNotifier/Logs/app-*.log`.
- DB: `%LocalAppData%/StockNewsNotifier/news.db`(없으면 마이그레이션+시더가 생성).
