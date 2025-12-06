# StockNewsNotifier – News Window & Notification Specification

## 1. Overview
This document defines the functional and UI/UX requirements for the News Window,
Unread Article Management, Notification Badge Logic, and Polling Behavior
for StockNewsNotifier.

## 2. News Window Behavior
### 2.1 Article Interaction
- When the user clicks an article row, the system must open the article URL
  using the system default browser.
- Upon clicking, the article state transitions to **Read**.

### 2.2 Read / Unread State Handling
- Articles never clicked are **Unread**.
- Articles opened via click become **Read**.
- Read articles must display with a **gray shaded row style** (temporary design).

### 2.3 Sorting Rules
Whenever the News Window:
- opens,
- refreshes,
- or an article is clicked (changing state),

the following sort order must be enforced:

1. **Unread articles first**
2. Within the same category (Unread / Read), sort by timestamp (descending).

## 3. Unread Count Badge (Red Number)
The badge must always display the count of articles where:
`IsRead = false`.

### Rules:
- When the user reads articles → count decrements.
- When new articles are ingested → count increments.
- The badge value must always remain synchronized with DB state.

## 4. Notification Toggle (Bell Icon)
- If the bell icon is **ON** for a stock:
  - The unread count badge updates in real-time.
- If the bell icon is **OFF**:
  - Unread count is maintained internally but **UI does not update live**.

## 5. Stock Price Polling
- Stock price polling is **independent** of bell icon state.
- Price updates must occur at the same interval for **all stocks**, regardless of notification settings.

## 6. Polling Cycle
- Maintain the current polling interval for now.
- The interval may be adjusted after system testing.
- Use different variables for the polling cycle for collecting news articles and the polling cycle for checking real-time stock prices.
- 
## 7. Implementation Guidance
- Ensure DB fields support read-state tracking.
- Ensure UI refresh triggers sort logic deterministically.
- Ensure ingestion pipeline increments unread count only for enabled sources.
