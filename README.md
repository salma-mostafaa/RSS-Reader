
# Personal RSS Reader

A full-featured RSS/Atom/JSON Feed reader built with ASP.NET Core Minimal API. The application lets users subscribe to feeds, read articles in a clean "River of News" layout, refresh feeds on demand, filter and search articles, and manage everything through a simple web interface. On top of the core reader it now supports user accounts (email/password and Google sign-in), AI-powered digests and per-article summaries, an AI chat assistant grounded in your feeds, playlists with public RSS export, saved articles, and reading history.

**Live demo:** [https://rssreader.runasp.net/](https://rssreader.runasp.net/)

## Features

### Reader

* Add RSS, Atom, or JSON Feed subscriptions by URL
* Feed auto-discovery: paste a normal webpage URL and the app finds the `<link rel="alternate">` feed for you
* Validate feeds before adding them and prevent duplicate subscriptions
* View all subscribed feeds, each with a consistent color accent that carries over to its article cards
* Subscriptions list automatically collapses once more than two feeds are added, with a "Show all / Show less" toggle so it doesn't crowd out the article list
* Remove subscriptions
* Manually refresh a single feed, or refresh every feed at once with the "Refresh all" button
* Articles are sorted in reverse chronological order and paginated (25 per page, with a "Jump to page" input)
* Filter the article list by feed - filters are multi-select, so any combination of feeds can be viewed together
* Search articles by title
* Article summaries are cleaned from raw feed HTML into plain, readable text
* Right-to-left (RTL) text detection for Arabic/Hebrew content
* Deterministic article IDs, so the same article keeps its identity across refreshes (tracking parameters like `utm_*` are stripped when generating IDs)
* Toast notifications for feedback (success, error, and loading states)
* Responsive user interface
* Dark mode toggle (respects the system preference on first visit)
* Protection against rendering unsafe HTML content (XSS prevention)

### Accounts

* Register with email and password, or sign in with Google (accounts with the same email are linked automatically)
* Email verification on registration
* Forgot/reset password flow with expiring one-time tokens
* Sign-in notification emails (via SendGrid)
* Per-user subscriptions, articles, playlists, saved items, and history
* Account deletion

### AI (DeepSeek)

* AI digest of your feeds: the latest articles are summarized into categorized bullet points, generated in both English and Arabic, and refreshed periodically in the background
* Per-article AI summaries, also bilingual (English + Arabic)
* AI chat assistant that answers questions using the articles from your feeds as context (chat history is persisted for signed-in users)
* AI endpoints are rate-limited per user/IP, with clear "try again in N seconds" responses

### Collections (API)

* Playlists: create named collections of articles, and share any playlist as a public RSS 2.0 feed (`/playlist/{id}/feed.xml`) that external readers can subscribe to
* Saved articles: bookmark articles with a full snapshot, so they survive even after the article ages out of the feed
* Reading history: log viewed articles and fetch the list of already-read IDs

## Technology Stack

* ASP.NET Core Minimal API (.NET 10)
* C#
* HTML / CSS / JavaScript (vanilla, no frameworks)
* SQLite (Microsoft.Data.Sqlite) - users, playlists, chat history, password reset tokens
* JSON file storage - feeds, cached articles, summaries, saved items, history
* System.ServiceModel.Syndication (RSS/Atom parsing)
* HtmlAgilityPack (cleans HTML out of feed summaries, feed auto-discovery)
* ASP.NET Core Cookie Authentication + Google OAuth
* SendGrid (verification, password reset, and sign-in emails)
* DeepSeek API (AI summaries and chat)

## How to Run Locally

### Using Visual Studio (recommended)

1. Make sure you have [Visual Studio](https://visualstudio.microsoft.com/) installed with the **ASP.NET and web development** workload.
2. Open the solution (or the `.csproj` file) in Visual Studio.
3. Press **Run** / **F5**.
4. Visual Studio will build the project, start the server, and automatically open your default browser to the app.

### Using VS Code (alternative)

1. Make sure the [.NET SDK](https://dotnet.microsoft.com/download) is installed (this project targets .NET 10).
2. Open the `RSS_Reader` project folder (the one containing `Program.cs`) in VS Code.
3. Open a terminal (`` Ctrl+` ``) and restore dependencies:
```bash
   dotnet restore
```
4. Run the app:
```bash
   dotnet run
```
5. The terminal will print the URL it's listening on, something like:
```text
   Now listening on: https://localhost:7055
```
6. Open that URL manually in your browser (Chrome, Firefox, or Safari)

### Optional configuration

The core reader works with zero configuration. The extra integrations are enabled by setting these values (environment variables, user secrets, or `appsettings.json`):

| Setting | Enables |
|---|---|
| `DEEPSEEK_API_KEY` | AI digests, per-article summaries, and chat |
| `SENDGRID_API_KEY`, `SENDGRID_FROM_EMAIL`, `SENDGRID_FROM_NAME` | Verification, password reset, and sign-in emails |
| `Authentication__Google__ClientId`, `Authentication__Google__ClientSecret` | Sign in with Google |

If a key is missing, the related feature is simply disabled - the rest of the app runs normally.

## Project Structure

```text
RSS_Reader/
│
├── Endpoints/
│   ├── AuthEndpoints.cs
│   ├── FeedEndpoints.cs
│   ├── ArticleEndpoints.cs
│   ├── ArticleSummaryEndpoints.cs
│   ├── SummaryEndpoints.cs
│   ├── ChatEndpoints.cs
│   ├── PlaylistEndpoints.cs
│   ├── SavedEndpoints.cs
│   └── HistoryEndpoints.cs
│
├── Services/
│   ├── FeedService.cs
│   ├── FeedFetcher.cs / IFeedFetcher.cs
│   ├── AiSummaryService.cs
│   ├── AiRateTracker.cs
│   └── EmailSender.cs
│
├── Parsing/
│   ├── FeedParser.cs
│   ├── ArticleContentCleaner.cs
│   ├── ArticleIdGenerator.cs
│   ├── RtlDetector.cs
│   ├── ParsedFeed.cs
│   └── ParsedItem.cs
│
├── Strorage/               (interface + implementation per store)
│   ├── FeedStore, ArticleStore, SummaryStore, ArticleSummaryStore
│   ├── SavedStore, HistoryStore
│   └── UserStore, PasswordResetStore, PlaylistStore, ChatStore
│
├── Models/
│   ├── Feed.cs, Article.cs, User.cs
│   ├── Playlist.cs, PlaylistItem.cs, SavedArticle.cs, HistoryEntry.cs
│   └── AiSummary.cs, ArticleSummary.cs, ChatMessage.cs, PasswordResetToken.cs
│
├── Helpers/
│   └── PasswordHasher.cs
│
├── wwwroot/
│   ├── index.html
│   ├── login.html
│   ├── js/script.js
│   └── styles/style.css
│
├── Database.cs
├── Program.cs
├── rss.db
├── feeds.json / articles.json
└── README.md
```

## How It Works

### Feed Management

Users add a feed by entering its URL. The application:

1. Validates the URL.
2. Checks it isn't already subscribed to (before doing any network call).
3. Downloads the URL. If it isn't a feed, it parses the page HTML and auto-discovers the real feed URL.
4. Parses it as RSS 2.0, Atom, or JSON Feed v1.
5. Stores the subscription (per user when signed in) and merges the feed's initial articles into the article cache.

### Article Loading

Whenever articles are requested (`GET /articles`), the application returns the cached articles for the user's feeds, sorted by publication date (newest first). Refreshing - either one feed (`POST /feeds/{id}/refresh`) or everything (`POST /articles/refresh-all`) - downloads the feed(s) again, cleans HTML summaries into safe plain text, detects RTL content, resolves a preview image when possible, and merges fresh articles into the store. Cached articles are pruned after 14 days; saved articles and playlist items keep their own snapshots so they never disappear.

### Filtering, Search, and Pagination

Above the article list, the UI shows a row of filter buttons - one per subscribed feed, plus an "All" option - generated dynamically from the current subscriptions. Filters are multi-select and combine with the title search box. Everything happens on the frontend against the already-loaded article list, so switching filters is instant. The list is paginated at 25 articles per page, with Previous/Next buttons and a "Jump to" page input; the current page survives reloads via `sessionStorage`.

### Accounts and Authentication

Authentication uses standard ASP.NET Core cookie auth. Passwords are hashed with PBKDF2 (SHA-256, 100,000 iterations, random salt) and verified with a constant-time comparison. Registration sends a verification email; forgotten passwords are handled with one-time tokens that expire after an hour. Google sign-in is supported and is automatically linked to an existing account with the same email. Data Protection keys are persisted to disk so sessions survive application restarts.

### AI Features

All AI features call the DeepSeek chat completions API:

* **Feed digest** (`POST /summary/generate`, `GET /summary`): the 20 most recent articles are condensed into up to 5 categorized sections (politics, business, technology, sports, ...) of short bullet points, produced in both English and Arabic. A background loop regenerates the digest periodically.
* **Per-article summaries** (`POST /articles/{id}/summarize`): bilingual summaries cached per article.
* **Chat** (`POST /chat`): a conversational assistant that receives recent articles from your feeds as context. History is stored per user in SQLite.

AI endpoints are protected by two layers of rate limiting: a fixed-window ASP.NET Core rate limiter (15 requests/minute per user or IP) plus an application-level tracker (10 requests per 20 minutes for signed-in users, 5 for guests). `GET /ai-limit-status` reports how much quota remains.

### Playlists, Saved Articles, and History

Signed-in users can organize articles into named playlists, bookmark articles to a saved list, and record reading history. Each entry stores a full JSON snapshot of the article, so collections keep working even after the original article is pruned from the cache. Any playlist can be exported as a standard RSS 2.0 feed at `/playlist/{playlistId}/feed.xml`, which external RSS readers can subscribe to.

### Data Storage

Data is split between SQLite and JSON files:

* **SQLite (`rss.db`)**: users, password reset tokens, playlists and playlist items, chat messages.
* **JSON files**: subscriptions (`feeds_{userId}.json` / shared `feeds.json`), the article cache (`articles.json`), AI digests (`summary_{userId}.json`), per-article summaries, saved articles, and history.

Each store is a small interface + implementation pair registered in DI, so the backing storage can change without touching the endpoints.

## Security

The application includes several security measures:

* Feed URLs are validated before use, and invalid feeds are rejected.
* Duplicate subscriptions are prevented.
* Passwords are hashed with PBKDF2 (SHA-256, 100,000 iterations, per-user random salt) and compared in constant time.
* Authentication uses HttpOnly, SameSite cookies - JavaScript cannot read the session cookie.
* Password reset tokens are single-use and expire after one hour.
* AI endpoints are rate-limited per user/IP to prevent abuse.
* HTML contained inside RSS summaries is parsed with HtmlAgilityPack and reduced to plain text (tags stripped, HTML entities decoded) before being displayed.
* The frontend renders all feed-derived content (titles, summaries, feed names) using `textContent` rather than `innerHTML`, so even if a summary somehow retained markup it would be displayed as inert text, not executed.
* External article links open in a separate tab using `rel="noopener noreferrer"`.
* Data Protection keys are persisted and scoped to the application, and forwarded headers are handled correctly behind a reverse proxy.

These measures help protect against Cross-Site Scripting (XSS), credential theft, and other common issues when consuming external RSS feeds.

## Example Feed URLs

If you don't have an RSS URL handy to test with, you can paste any of these into the "Enter RSS Feed URL" box:

* BBC News: https://feeds.bbci.co.uk/news/world/rss.xml
* ABC News: US: https://abcnews.com/abcnews/usheadlines?ts=1783378172539
* Yahoo News: https://news.yahoo.com/rss/world
* BuzzFeed News: https://www.buzzfeed.com/politics.xml

## Future Improvements

Possible future enhancements include:

* Frontend UI for playlists, saved articles, history, and the AI features (currently exposed through the API)
* Read/Unread status surfaced in the article list
* Article tagging/categorization
* Keyboard shortcuts for navigation

## Author

Developed as an ASP.NET Core Minimal API project.
