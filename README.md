# Personal RSS Reader

A lightweight RSS/Atom reader built with ASP.NET Core Minimal API. The application allows users to subscribe to RSS feeds, read articles in a clean "River of News" layout, manually refresh individual feeds, filter articles by feed, and manage their subscriptions through a simple web interface.

## Features

* Add RSS/Atom feeds by URL
* Validate feeds before adding them
* Prevent duplicate subscriptions
* View all subscribed feeds
* Remove subscriptions
* Manually refresh a single feed to pull its latest articles
* Display articles from all subscribed feeds
* Filter the article list by feed, or view all feeds combined
* Articles are sorted in reverse chronological order
* Article summaries are cleaned from raw feed HTML into plain, readable text
* Responsive user interface
* Protection against rendering unsafe HTML content (XSS prevention)

## Technology Stack

* ASP.NET Core Minimal API
* C#
* HTML
* CSS
* JavaScript
* System.ServiceModel.Syndication
* HtmlAgilityPack (cleans HTML out of feed summaries)
* JSON file storage


## How to Run Locally
 
### Using Visual Studio (recommended)
 
1. Make sure you have [Visual Studio](https://visualstudio.microsoft.com/) installed with the **ASP.NET and web development** workload.
2. Open `RSS_Reader.sln` (or the `.csproj` file) in Visual Studio.
3. Press **Run** / **F5**.
4. Visual Studio will build the project, start the server, and automatically open your default browser to the app.
   
### Using VS Code (alternative)
 
1. Make sure the [.NET SDK](https://dotnet.microsoft.com/download) is installed (this project targets .NET 8 or later).
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

## Project Structure

```text
RSS_Reader/
│
├── Models/
│   ├── Feed.cs
│   └── Article.cs
│
├── wwwroot/
│   ├── index.html
│   ├── js/
│   │   └── script.js
│   └── styles/
│       └── style.css
│
├── feeds.json
├── Program.cs
└── README.md
```

## How It Works

### Feed Management

Users add an RSS or Atom feed by entering its URL. The application:

1. Validates the URL.
2. Checks it isn't already subscribed to (before doing any network call).
3. Downloads the feed.
4. Verifies that it is a valid RSS/Atom feed.
5. Stores the subscription inside `feeds.json`.

Only the subscription list is persisted.

### Article Loading

Whenever all articles are requested (`GET /articles`):

1. The application reads all subscribed feeds from `feeds.json`.
2. Downloads each feed.
3. Parses the RSS/Atom XML.
4. Extracts article information.
5. Cleans HTML summaries into safe plain text.
6. Sorts articles by publication date (newest first).
7. Returns the results to the frontend.

### Manual Feed Refresh

Each subscription in the UI has a refresh button. Clicking it calls `POST /feeds/{id}/refresh`, which downloads and parses only that one feed and returns its latest articles. The frontend then reloads the combined article list so the refreshed content is reflected immediately. Refreshing is always manual - there is no background/automatic polling.

### Filtering Articles

Above the article list, the UI shows a row of filter buttons - one per subscribed feed, plus an "All" option. These buttons are generated dynamically from whatever feeds are currently in `feeds.json` (nothing is hardcoded). Filtering happens entirely on the frontend against the already-loaded article list, so switching filters is instant and requires no additional network request.

### Data Storage

Subscriptions are stored inside a single JSON file:

```json
[
  {
    "id": "...",
    "title": "BBC News",
    "url": "https://feeds.bbci.co.uk/news/rss.xml"
  }
]
```

The application does not cache downloaded articles. Instead, articles are fetched directly from their original RSS feeds whenever they are requested (or when a single feed is refreshed), ensuring users always receive the latest available content.

## Security

The application includes several security measures:

* Feed URLs are validated before use.
* Invalid RSS/Atom feeds are rejected.
* Duplicate subscriptions are prevented.
* HTML contained inside RSS summaries is parsed with HtmlAgilityPack and reduced to plain text (tags stripped, HTML entities decoded) before being displayed.
* The frontend renders all feed-derived content (titles, summaries, feed names) using `textContent` rather than `innerHTML`, so even if a summary somehow retained markup it would be displayed as inert text, not executed.
* External article links open in a separate tab using `rel="noopener noreferrer"`.

These measures help protect against Cross-Site Scripting (XSS) and other common issues when consuming external RSS feeds.

## Example Feed URLs
 
If you don't have an RSS URL handy to test with, you can paste any of these into the "Enter RSS Feed URL" box:
 
* BBC News: https://feeds.bbci.co.uk/news/world/rss.xml
* ABC News: US: https://abcnews.com/abcnews/usheadlines?ts=1783378172539
* Yahoo News: https://news.yahoo.com/rss/world
* BuzzFeed News: https://www.buzzfeed.com/politics.xml

## Future Improvements

Possible future enhancements include:

* Search articles by title
* Article caching for faster loading
* Read/Unread status
* Favorite feeds
* Pagination or infinite scrolling
* Dark mode

## Author

Developed as an ASP.NET Core Minimal API project.
