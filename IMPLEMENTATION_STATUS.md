# StockNewsNotifier - Implementation Status

## 프로젝트 개요
Windows 시스템 트레이 애플리케이션으로, 주식 뉴스를 실시간으로 크롤링하여 사용자에게 알림을 제공합니다.

**기술 스택:**
- .NET 8, WPF (UI) + WinForms NotifyIcon (트레이)
- EF Core + SQLite
- AngleSharp (HTML 파싱)
- Polly (복원력 패턴)
- Serilog (로깅)

**프로젝트 시작일:** 2025-11-16
**마지막 업데이트:** 2025-11-18

---

## ✅ Phase 1: Foundation (완료)

### Step 1.1: 프로젝트 설정
- ✅ 솔루션 및 WPF 프로젝트 생성
- ✅ NuGet 패키지 설치
  - Microsoft.EntityFrameworkCore.Sqlite (8.0.*)
  - Microsoft.EntityFrameworkCore.Design (8.0.*)
  - Microsoft.Extensions.Hosting (10.0.0)
  - Microsoft.Extensions.Configuration.Json (10.0.0)
  - Microsoft.Extensions.Http (10.0.0)
  - AngleSharp (1.4.0)
  - Serilog (4.3.0)
  - Serilog.Sinks.File (7.0.0)
  - Serilog.Extensions.Hosting (9.0.0)
  - Polly (8.6.4)
  - Microsoft.Toolkit.Uwp.Notifications (7.1.3)

### Step 1.2: Data Models
엔티티 클래스 생성 (`Data/Entities/`):
- ✅ **WatchItem.cs** - 감시 중인 주식 티커
  - Exchange, Ticker, CompanyName, IconUrl, AlertsEnabled, CreatedUtc
- ✅ **Source.cs** - 뉴스 소스
  - Name, BaseUrl, Enabled, DisplayName
- ✅ **WatchItemSource.cs** - WatchItem ↔ Source 다대다 관계
  - CustomQuery, Enabled
- ✅ **NewsItem.cs** - 크롤링된 뉴스 아이템
  - Title, Url, CanonicalUrl, TitleHash, SimHash64
  - PublishedUtc, FetchedUtc, IsRead, NotificationSent
- ✅ **CrawlState.cs** - 크롤링 상태 및 레이트 리미팅
  - LastCrawlUtc, RequestsPerSecond, RobotsTxt, ConsecutiveErrors

### Step 1.3: AppDbContext
- ✅ EF Core DbContext 구성
- ✅ Fluent API를 통한 엔티티 설정
- ✅ 인덱스 생성:
  - `IX_WatchItem_Exchange_Ticker` (unique)
  - `IX_NewsItem_CanonicalUrl` (unique)
  - `IX_NewsItem_WatchItemId_FetchedUtc`
  - 기타 성능 최적화 인덱스

### Step 1.4: Generic Host Bootstrap
- ✅ `App.xaml.cs` 수정
  - Microsoft.Extensions.Hosting 통합
  - Serilog 로깅 설정 (일별 로그 파일 롤링)
  - appsettings.json 구성 파일 로딩
  - DbContext DI 등록
  - 애플리케이션 라이프사이클 관리

### Step 1.5: appsettings.json
- ✅ 구성 파일 생성
  - Polling 설정 (240초 간격, 30초 지터)
  - RateLimits 설정 (기본 1 RPS, 10 RPM)
  - Notifications 설정
  - Serilog 로깅 레벨 설정

### 데이터베이스 마이그레이션
- ✅ `dotnet ef migrations add InitialCreate`
- ✅ `dotnet ef database update`
- ✅ 데이터베이스 위치: `%LocalAppData%\StockNewsNotifier\news.db`

---

## ✅ Phase 2: Core Services (완료)

### Step 2.1: 인터페이스 정의
`Services/Interfaces/` 폴더:
- ✅ **Ticker.cs** - `record Ticker(string Exchange, string Symbol)`
- ✅ **IWatchlistService.cs** - 감시 목록 관리
  - AddAsync, RemoveAsync, SetAlertsAsync, ListAsync
- ✅ **ISourceCrawler.cs** - 크롤러 인터페이스
  - Name, BaseHost, BuildQueryUrls, FetchAsync
- ✅ **RawArticle** - `record RawArticle(string Title, string Url, DateTime? PublishedUtc, string? Summary)`
- ✅ **INewsService.cs** - 뉴스 관리
  - IngestAsync, ListAsync, MarkReadAsync
- ✅ **INotificationService.cs** - 알림 서비스
- ✅ **IScheduler.cs** - 크롤링 스케줄러

### Step 2.2: 유틸리티 클래스
`Utilities/` 폴더:
- ✅ **DedupeHelper.cs**
  - `ComputeTitleHash()` - SHA256 해시 기반 중복 감지
  - `ComputeSimHash()` - MVP에서는 0 반환 (향후 구현 예정)
- ✅ **UrlCanonicalizer.cs**
  - 추적 파라미터 제거: utm_*, gclid, fbclid, msclkid, yclid, mc_cid, mc_eid, ref, src
  - 향후 appsettings.json으로 설정 가능하도록 설계
- ✅ **TimeParser.cs**
  - 상대 시간 파싱: "33m ago", "2h ago", "3d ago"
  - 절대 시간 파싱: ISO 8601 및 일반 DateTime 형식
- ✅ **PollyPolicies.cs**
  - 3회 재시도, 지수 백오프 (2^retry 초) + 지터
  - HttpClient 타임아웃: 10-15초 랜덤

### Step 2.3-2.4: 서비스 구현
`Services/` 폴더:
- ✅ **WatchlistService.cs**
  - 티커 추가 시 중복 체크
  - YahooFinance 소스 자동 연결
  - AlertsEnabled 토글
  - 전체 감시 목록 조회 (Sources 포함)
- ✅ **NewsService.cs**
  - 중복 제거 전략:
    1. CanonicalUrl 기반 체크
    2. TitleHash 기반 체크 (같은 제목, 다른 URL)
  - 날짜 범위 및 읽음/안읽음 필터링
  - 읽음 상태 업데이트

---

## ✅ Phase 3: Yahoo Finance Crawler (완료)

### 구현 내용
`Services/Crawlers/YahooFinanceCrawler.cs`:
- ✅ URL 생성: `https://finance.yahoo.com/quote/{TICKER}/news`
- ✅ HTML 파싱 로직:
  - CSS 셀렉터: `[data-testid="storyitem"]`
  - 제목 추출: `a.titles > h3`
  - URL 추출: `a.titles[href]`
  - 시간 추출: `div.publishing` (형식: "Motley Fool • 33m ago")
- ✅ HTTP 요청:
  - User-Agent 설정
  - 타임아웃 설정
  - 에러 핸들링
- ✅ 상세 로깅

---

## 🚧 현재 상태: 테스트 중

### 테스트 환경 구성
- ✅ `App.xaml.cs`에 임시 테스트 코드 추가
- ✅ 서비스 DI 등록:
  - WatchlistService (Scoped)
  - NewsService (Scoped)
  - YahooFinanceCrawler (Singleton)
  - HttpClient("crawler")
- ✅ `RunCrawlerTestAsync()` 메서드 구현
  - YahooFinance Source 생성
  - MSFT 티커 추가
  - 뉴스 크롤링 및 DB 저장
  - 결과를 MessageBox로 표시

### 🔴 현재 이슈: Yahoo Finance 404 에러

**문제:**
```
GET https://finance.yahoo.com/quote/MSFT/news
Response: 404 (Not Found)
```

**로그:**
```
2025-11-18 12:43:57.496 [INF] Fetching Yahoo Finance news from https://finance.yahoo.com/quote/MSFT/news
2025-11-18 12:43:59.053 [INF] Received HTTP response headers after 1535.455ms - 404
2025-11-18 12:43:59.124 [ERR] HTTP error fetching from https://finance.yahoo.com/quote/MSFT/news
System.Net.Http.HttpRequestException: Response status code does not indicate success: 404 (Not Found).
```

**확인 사항:**
- ✅ 브라우저에서는 해당 URL이 정상 작동
- ❌ HttpClient에서는 404 에러 발생

**가능한 원인:**
1. **봇 감지 및 차단** (가장 가능성 높음)
   - Yahoo Finance가 HttpClient의 요청을 봇으로 인식
   - User-Agent만으로는 부족할 가능성
2. 추가 헤더 필요 (Accept, Accept-Language, Referer 등)
3. 쿠키 또는 세션 필요
4. JavaScript 렌더링 필요 (동적 콘텐츠)

**시도한 해결책:**
- ✅ User-Agent 헤더 추가
- ⏳ 추가 브라우저 헤더 필요 (다음 단계)

---

## 📂 프로젝트 구조

```
StockNewsNotifier/
├── StockNewsNotifier.sln
├── CLAUDE.md                      # 구현 가이드
├── IMPLEMENTATION_STATUS.md       # 이 문서
├── README.md
└── StockNewsNotifier/
    ├── StockNewsNotifier.csproj
    ├── App.xaml
    ├── App.xaml.cs                # Generic Host 설정 + 테스트 코드
    ├── appsettings.json           # 구성 파일
    ├── Data/
    │   ├── AppDbContext.cs        # EF Core DbContext
    │   ├── Entities/
    │   │   ├── WatchItem.cs
    │   │   ├── Source.cs
    │   │   ├── WatchItemSource.cs
    │   │   ├── NewsItem.cs
    │   │   └── CrawlState.cs
    │   └── Migrations/
    │       └── 20251116103459_InitialCreate.cs
    ├── Services/
    │   ├── Interfaces/
    │   │   ├── Ticker.cs
    │   │   ├── IWatchlistService.cs
    │   │   ├── ISourceCrawler.cs
    │   │   ├── INewsService.cs
    │   │   ├── INotificationService.cs
    │   │   └── IScheduler.cs
    │   ├── WatchlistService.cs
    │   ├── NewsService.cs
    │   └── Crawlers/
    │       └── YahooFinanceCrawler.cs
    ├── Utilities/
    │   ├── DedupeHelper.cs
    │   ├── UrlCanonicalizer.cs
    │   ├── TimeParser.cs
    │   └── PollyPolicies.cs
    └── Views/
        └── MainWindow.xaml        # 기본 빈 창 (UI 미구현)
```

---

## 📊 구현 진행률

| Phase | 상태 | 완료율 |
|-------|------|--------|
| Phase 1: Foundation | ✅ 완료 | 100% |
| Phase 2: Core Services | ✅ 완료 | 100% |
| Phase 3: Yahoo Finance Crawler | ✅ 완료 | 100% |
| **Phase 3 테스트** | 🚧 **진행 중** | **65%** |
| Phase 4-5: Background Polling | ❌ 미착수 | 0% |
| Phase 6: UI Implementation | ❌ 미착수 | 0% |
| Phase 7: Notifications | ❌ 미착수 | 0% |

---

## 🎯 다음 단계

### 즉시 해결 필요 (Phase 3 테스트 완료)
1. **Yahoo Finance 404 에러 해결**
   - ✅ HttpClient 기본 헤더/타임아웃을 브라우저와 유사하게 구성 (Accept, Accept-Language, Accept-Encoding 등)
   - ✅ 요청마다 Referrer/UA/SEC-FETCH 헤더를 포함하는 `BuildRequestMessage` 도입
   - ✅ Polly 재시도 파이프라인 적용으로 5xx/네트워크 오류 자동 재시도
   - ⏳ 필요 시 Selenium/Puppeteer나 대체 소스 고려
2. **파서 안정화**
   - ✅ AngleSharp `HtmlParser`로 교체하고 `[data-testid='storyitem']` + `li.js-stream-content` 폴백 셀렉터 추가
   - ✅ `a.titles`, `h3 a`, `a[data-ylk]` 등 다양한 링크 패턴 지원
   - ✅ 상대 URL 보정 및 발행시각 파싱 로깅 강화
   - ⏳ 실제 HTML 캡처 기반 단위 테스트 작성

### Phase 4-5: Background Polling (다음 우선순위)
- ChannelScheduler 구현
- NewsPollerHostedService 구현
- 레이트 리미팅 적용
- CrawlState 관리

### Phase 6: UI Implementation
- MainWindow 구현
- NotifyIcon 트레이 아이콘
- 감시 목록 UI
- 뉴스 목록 UI

### Phase 7: Notifications
- Windows Toast 알림 구현
- 알림 빈도 제한
- 알림 클릭 처리

---

## 🔍 주요 결정 사항

### 중복 제거 전략
- **Phase 2**: CanonicalUrl + TitleHash 사용
- **SimHash64**: 필드만 존재, 항상 0 저장 (향후 구현)
- **이유**: MVP 단계에서는 간단한 중복 제거만 구현

### URL 정규화
- **추적 파라미터**: 확장 리스트 사용
  - UTM: utm_source, utm_medium, utm_campaign, utm_term, utm_content, utm_id
  - 광고: gclid, fbclid, msclkid, yclid
  - 이메일: mc_cid, mc_eid
  - 기타: ref, src
- **향후 계획**: appsettings.json으로 설정 이동

### Polly 재시도 정책
- **재시도**: 3회
- **백오프**: 지수 (2^retry 초) + 지터
- **타임아웃**: 10-15초 랜덤

### 시간 파싱
- **Phase 2**: Xm/Xh/Xd ago + DateTime 형식만 지원
- **미지원 형식**: PublishedUtc = null, FetchedUtc 사용

---

## 📝 로그 및 디버깅

### 로그 위치
```
%LocalAppData%\StockNewsNotifier\Logs\app-YYYYMMDD.log
```

### 데이터베이스 위치
```
%LocalAppData%\StockNewsNotifier\news.db
```

### 디버그 HTML (테스트 중)
```
%TEMP%\yahoo_finance_debug.html
```

---

## 🐛 알려진 이슈

1. **Yahoo Finance 404 에러**
   - 브라우저에서는 정상 작동
   - HttpClient에서는 404 반환
   - 봇 감지 차단으로 추정

2. **UI 미구현**
   - 현재 빈 MainWindow만 표시
   - Phase 6에서 구현 예정

3. **알림 미구현**
   - Phase 7에서 구현 예정

---

## 📚 참고 문서

- [CLAUDE.md](./CLAUDE.md) - 전체 구현 가이드
- [EF Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
- [AngleSharp Documentation](https://anglesharp.github.io/)
- [Polly Documentation](https://github.com/App-vNext/Polly)
- [Serilog Documentation](https://serilog.net/)

---

**마지막 업데이트:** 2025-11-18 12:44 (KST)

---

### Recent Work Summary (2025-11-19)
- Introduced a channel-based scheduler and `NewsPollerHostedService`, enabling automated polling of the watchlist and crawl job processing. The service reads polling intervals from `appsettings.json`, applies jitter, and enqueues every watch item for crawling.
- Added `NotificationService` as a temporary logger-backed implementation of `INotificationService`, paving the way for Windows toast notifications in Phase 7.
- Refined the Yahoo Finance crawler: URL builder now encodes symbols (`https://finance.yahoo.com/quote/{ticker}/news?p={ticker}`), HttpClient uses `SocketsHttpHandler` with automatic decompression, and parsing logic is extracted into `YahooFinanceHtmlParser`.
- Created a lightweight console-based HTML fixture smoke test under `tests/StockNewsNotifier.Tests`. It verifies the parser with saved Yahoo Finance HTML without requiring third-party test frameworks, ensuring builds succeed in network-restricted environments.
