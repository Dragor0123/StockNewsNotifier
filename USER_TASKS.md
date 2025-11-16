# 사용자 수행 작업 목록 (USER_TASKS.md)

## 프로젝트 시작 전 준비사항

### ✅ 이미 완료된 사항
- [x] Visual Studio 설치 및 환경 설정
- [x] ChatGPT와 시스템 설계 논의 완료
- [x] 프로젝트 요구사항 확정

---

## 1단계: GitHub 리포지토리 설정 (지금 수행)

### 1.1 GitHub에서 리포지토리 생성
1. https://github.com 접속 및 로그인
2. 우측 상단 "+" 버튼 → "New repository" 클릭
3. 설정:
   - **Repository name**: `StockNewsNotifier`
   - **Description**: "Windows system tray app for real-time stock news notifications"
   - **Visibility**: Public 또는 Private (선택)
   - **Initialize**: 
     - ✅ Add a README file (체크)
     - ✅ Add .gitignore: **VisualStudio** 선택
     - [ ] Choose a license: MIT 또는 Apache 2.0 (선택사항)
4. "Create repository" 클릭

### 1.2 로컬에 클론
```bash
# 터미널 또는 Git Bash에서 실행
git clone https://github.com/[YOUR_USERNAME]/StockNewsNotifier.git
cd StockNewsNotifier
```

---

## 2단계: 개발 환경 확인

### 2.1 필수 소프트웨어 확인
- [x] Visual Studio 2022 (Community 이상)
- [ ] .NET 8 SDK 설치 확인
  ```bash
  dotnet --version
  # 출력: 8.x.x 이상이어야 함
  ```
  - 만약 .NET 8이 없다면: https://dotnet.microsoft.com/download/dotnet/8.0 에서 다운로드

### 2.2 Visual Studio 워크로드 확인
Visual Studio Installer에서 다음이 설치되어 있는지 확인:
- [ ] .NET desktop development
- [ ] .NET Core cross-platform development

---

## 3단계: Claude에게 프로젝트 구현 요청

Claude가 작성한 `CLAUDE.md` 파일을 리포지토리에 추가한 후, Claude에게 다음과 같이 요청:

```
CLAUDE.md 파일의 지침에 따라 StockNewsNotifier 프로젝트를 구현해주세요.
우선 Phase 1 (Foundation)부터 시작해서 MVP를 완성해주세요.
```

---

## 4단계: 프로젝트 진행 중 사용자 작업

### 4.1 아이콘 파일 준비 (선택사항)
프로젝트 루트에 `app.ico` 파일 추가:
- 크기: 256x256 픽셀
- 형식: .ico
- 용도: 시스템 트레이 아이콘
- 무료 아이콘 다운로드: https://www.flaticon.com/ 또는 직접 제작

### 4.2 실제 웹사이트 HTML 구조 확인 (중요!)
Claude가 크롤러를 구현할 때, **반드시** 다음 작업 수행:

#### Yahoo Finance HTML 확인 (예시)
1. 브라우저에서 https://finance.yahoo.com/quote/MSFT/news 접속
2. F12 (개발자 도구) 열기
3. Elements 탭에서 뉴스 항목의 HTML 구조 확인
4. Claude에게 실제 HTML 구조 전달:
   ```
   Yahoo Finance의 뉴스 항목 HTML 구조가 다음과 같습니다:
   [여기에 복사한 HTML 붙여넣기]
   
   이에 맞게 YahooFinanceCrawler의 CSS 셀렉터를 수정해주세요.
   ```

**왜 필요한가?**
- 웹사이트 구조는 자주 변경됨
- Claude의 초기 셀렉터는 placeholder일 수 있음
- 실제 HTML을 확인해야 정확한 크롤링 가능

### 4.3 데이터베이스 초기 데이터 추가 (옵션)
프로젝트 실행 후 Source 테이블에 기본 데이터 추가:
```sql
INSERT INTO Source (Name, BaseUrl, Enabled) VALUES 
('YahooFinance', 'https://finance.yahoo.com', 1),
('Reuters', 'https://www.reuters.com', 1),
('GoogleFinance', 'https://www.google.com/finance', 1),
('Investing', 'https://www.investing.com', 1),
('WSJ', 'https://www.wsj.com', 1);
```

또는 Claude에게 요청:
```
데이터베이스 마이그레이션에 Source 테이블 초기 데이터를 추가하는 코드를 작성해주세요.
```

---

## 5단계: 테스트 및 디버깅

### 5.1 첫 실행 전 체크리스트
- [ ] 솔루션 빌드 성공 확인
- [ ] 데이터베이스 마이그레이션 실행됨
- [ ] appsettings.json 설정 확인
- [ ] 로그 폴더 생성 확인 (`%LOCALAPPDATA%\StockNewsNotifier\Logs`)

### 5.2 기본 테스트 시나리오
1. **앱 실행**
   - 시스템 트레이에 아이콘이 나타나는지 확인
   - 트레이 아이콘 클릭 시 메인 윈도우 표시 확인

2. **주식 종목 추가**
   - (+) 버튼 클릭
   - `NASDAQ:MSFT` 입력
   - 목록에 추가되는지 확인

3. **뉴스 크롤링 확인**
   - 로그 파일 확인: `%LOCALAPPDATA%\StockNewsNotifier\Logs`
   - 데이터베이스 확인 (DB Browser for SQLite 등 사용)
   - NewsItem 테이블에 데이터가 들어오는지 확인

4. **알림 테스트**
   - 새 뉴스가 수집되면 Windows 토스트 알림이 나타나는지 확인
   - 알림 클릭 시 브라우저에서 기사가 열리는지 확인

### 5.3 문제 발생 시 확인 사항
- [ ] 로그 파일 내용 확인
- [ ] Visual Studio Output 창의 에러 메시지 확인
- [ ] 네트워크 연결 확인
- [ ] 크롤링 대상 웹사이트 접속 가능 여부 확인

---

## 6단계: 추가 기능 구현 (MVP 완성 후)

Claude에게 다음 작업을 순차적으로 요청:

### 6.1 추가 크롤러 구현
```
Reuters 크롤러를 구현해주세요. 
실제 Reuters 웹사이트의 HTML 구조를 확인했습니다: [HTML 구조 붙여넣기]
```

### 6.2 UI 개선
```
뉴스 보기 창(NewsViewWindow)을 구현해주세요.
- Today/Yesterday/Older로 그룹핑
- 읽음/안읽음 음영 처리
- 우클릭 컨텍스트 메뉴 (읽음 표시, 링크 복사)
```

### 6.3 검색 Pool 편집 기능
```
EditSourcePoolDialog를 구현해주세요.
사용자가 각 종목별로 크롤링할 소스를 체크박스로 선택할 수 있어야 합니다.
```

---

## 7단계: 배포 준비

### 7.1 릴리스 빌드
Visual Studio에서:
1. 빌드 구성: Release로 변경
2. 플랫폼: x64 선택
3. 빌드 → 솔루션 다시 빌드

### 7.2 실행 파일 패키징 (옵션)
```bash
# Self-contained 단일 파일로 퍼블리시
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

생성된 파일 위치: `bin\Release\net8.0-windows\win-x64\publish\`

### 7.3 설치 프로그램 제작 (선택사항)
- WiX Toolset 사용
- MSIX 패키징
- 또는 Squirrel.Windows 사용

---

## 주요 파일 위치 참고

### 개발 중
- **소스 코드**: `StockNewsNotifier\src\StockNewsNotifier\`
- **로그**: Visual Studio Output 창
- **데이터베이스**: 프로젝트 폴더 내 `news.db`

### 실행 파일 설치 후
- **데이터베이스**: `%LOCALAPPDATA%\StockNewsNotifier\news.db`
- **로그 파일**: `%LOCALAPPDATA%\StockNewsNotifier\Logs\`
- **설정 파일**: `appsettings.json` (실행 파일과 같은 폴더)

---

## 자주 묻는 질문 (FAQ)

### Q1: "dotnet: command not found" 에러가 발생합니다.
**A**: .NET 8 SDK를 설치하고 시스템 PATH에 추가되었는지 확인하세요.

### Q2: 뉴스가 크롤링되지 않습니다.
**A**: 
1. 로그 파일 확인
2. 웹사이트 HTML 구조가 변경되었을 수 있음 → CSS 셀렉터 업데이트 필요
3. Rate limiting으로 차단되었을 수 있음 → 폴링 간격 증가

### Q3: 토스트 알림이 나타나지 않습니다.
**A**: Windows 알림 설정에서 앱 알림이 허용되어 있는지 확인

### Q4: 데이터베이스 "locked" 에러가 발생합니다.
**A**: 여러 인스턴스가 실행 중일 수 있음. 프로세스 종료 후 재시작

---

## 다음 단계

1. ✅ GitHub 리포지토리 생성
2. ⏳ 로컬에 클론
3. ⏳ Claude에게 CLAUDE.md 파일 공유 및 구현 요청
4. ⏳ 웹사이트 HTML 구조 확인 후 크롤러 수정
5. ⏳ 테스트 및 디버깅
6. ⏳ 추가 기능 구현
7. ⏳ 배포

---

**중요**: 각 단계마다 Claude와 긴밀히 협력하여 진행하세요. 
막히는 부분이 있으면 언제든 Claude에게 질문하고 도움을 요청하세요!
