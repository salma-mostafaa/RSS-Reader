const feedInput = document.getElementById("feedUrl");
const addFeedButton = document.getElementById("addFeedButton");
const feedUrlError = document.getElementById("feedUrlError");
const feedsDiv = document.getElementById("feeds");
const feedSearchInput = document.getElementById("feedSearchInput");
const feedCategoryFiltersDiv = document.getElementById("feedCategoryFilters");
const feedCategoryToggle = document.getElementById("feedCategoryToggle");
let feedCategoryFiltersExpanded = false;
const FEED_CATEGORY_COLLAPSED_HEIGHT = 30;
const feedCounter = document.getElementById("feedCounter");
const feedCountDisplay = document.getElementById("feedCountDisplay");
const feedDrawerToggle = document.getElementById("feedDrawerToggle");
const feedDrawerClose = document.getElementById("feedDrawerClose");
const feedDrawerBackdrop = document.getElementById("feedDrawerBackdrop");
let feedSearchTerm = "";
const articlesDiv = document.getElementById("articles");
const articleCountSpan = document.getElementById("articleCount");
const articleSearchInput = document.getElementById("articleSearch");
let searchTerm = "";
const refreshAllButton = document.getElementById("refreshAllButton");
const headerSignInButton = document.getElementById("headerSignInButton");
const themeToggle = document.getElementById("themeToggle");
const langToggle = document.getElementById("langToggle");
const aiSummaryText = document.getElementById("aiSummary");
const refreshSummaryButton = document.getElementById("refreshSummaryButton");
const shareSummaryButton = document.getElementById("shareSummaryButton");
const summaryToggle = document.getElementById("summaryToggle");
let summaryExpanded = false;
const SUMMARY_COLLAPSED_HEIGHT = 160;
const playlistToggle = document.getElementById("playlistToggle");
const playlistBadge = document.getElementById("playlistBadge");
const chatToggle = document.getElementById("chatToggle");
const chatPanel = document.getElementById("chatPanel");
const chatMessages = document.getElementById("chatMessages");
const chatInput = document.getElementById("chatInput");
const chatSendBtn = document.getElementById("chatSendBtn");
const chatCloseBtn = document.getElementById("chatCloseBtn");
const chatClearBtn = document.getElementById("chatClearBtn");
const dialogOverlay = document.getElementById("dialogOverlay");
const dialogBox = document.getElementById("dialogBox");
const trendingTopicsDiv = document.getElementById("trendingTopics");
const trendingToggle = document.getElementById("trendingToggle");
let trendingExpanded = false;
const TRENDING_COLLAPSED_HEIGHT = 38;

let allArticles = [];
let allFeeds = [];
let guestFeed = null; // one temp feed for non-signed-in visitors, lost on refresh
let activeCategoryFilters = new Set(); // categories to filter articles by (top filter bar)
let activeFeedFilters = new Set(); // feed titles to filter articles by (sidebar subscription list)
let playlistByArticleId = new Map(); // articleId -> { playlistItemId, playlistId }
let playlists = []; // all playlists
let showingPlaylist = false;
let currentPlaylistId = null;
let playlistArticleFilter = new Set(); // feed titles to filter within playlist
let playlistCategoryFilter = new Set(); // categories to filter within playlist
const ARTICLES_PER_PAGE = 24;
let currentPage = parseInt(sessionStorage.getItem("currentPage"), 10) || 1;

const accountToggle = document.getElementById("accountToggle");
let currentUser = null; // { id, email } once signed in, else null

const savedToggle = document.getElementById("savedToggle");
const savedBadge = document.getElementById("savedBadge");
const historyToggle = document.getElementById("historyToggle");
let showingSaved = false;
let showingHistory = false;
let savedItemByArticleId = new Map(); // articleId -> savedItemId
let viewedArticleIds = new Set(); // articleIds already opened, for the "already read" dim + History page
const articleSummaryCache = new Map(); // articleId -> { textEn, textAr } - avoids re-fetching within the same session
let viewRenderToken = 0; // guards async view renders: only the latest render may append after an await, so overlapping calls (e.g. applyLanguage + handleRoute on refresh) can't duplicate articles

/* ===== ROUTING ===== */

const ROUTE_HOME = "/";
const ROUTE_PLAYLIST = "/playlist";
const ROUTE_FAVORITES = "/favorites";
const ROUTE_HISTORY = "/history";
const ROUTE_LOGIN = "/login";
const ROUTE_REGISTER = "/register";
const ROUTE_FORGOT_PASSWORD = "/forgot-password";

const AUTH_ROUTES = [ROUTE_LOGIN, ROUTE_REGISTER, ROUTE_FORGOT_PASSWORD];

function getRouteFromPath(path) {
    if (path === ROUTE_PLAYLIST) return "playlist";
    if (path === ROUTE_FAVORITES) return "favorites";
    if (path === ROUTE_HISTORY) return "history";
    if (path === ROUTE_LOGIN) return "login";
    if (path === ROUTE_REGISTER) return "register";
    if (path === ROUTE_FORGOT_PASSWORD) return "forgot-password";
    return "home";
}

function navigateTo(path) {
    if (AUTH_ROUTES.includes(path)) {
        window.location.href = path + window.location.search;
        return;
    }
    history.pushState(null, "", path);
    handleRoute();
}

function getRedirectPath() {
    const params = new URLSearchParams(window.location.search);
    return params.get("redirect") || ROUTE_HOME;
}

/* ===== I18N ===== */

const i18n = {
    en: {
        title: "Personal RSS Reader",
        heading: "Personal RSS Reader",
        tagline: "Personal News Hub",
        langLabel: "AR",
        refreshAllTitle: "Refresh all feeds",
        refreshAll: "Refresh all",
        themeTitle: "Toggle dark mode",
        addFeed: "Add feed",
        feedUrlLabel: "RSS Feed URL",
        feedUrlPlaceholder: "https://example.com/feed.xml",
        feedUrlError: "Please enter a feed URL first",
        searchPlaceholder: "Search articles by title",
        searchFeedsPlaceholder: "Search feeds...",
        noFeedsMatchSearch: "No feeds match your search",
        browseFeeds: "Feeds",
        allCategories: "All",
        showMoreFeeds: "Show {0} more",
        subscriptions: "Subscriptions",
        dispatches: "Dispatches",
        showAll: "Show all",
        showLess: "Show less",
        showMore: "Show more",
        readArticle: "Read article \u2192",
        noSubscriptions: "No subscriptions yet. Add a feed URL above to get started.",
        noArticles: "No articles available.",
        all: "All",
        page: "Page",
        of: "of",
        previous: "Previous",
        next: "Next",
        jumpTo: "Jump to",
        refreshFeed: "Refresh this feed",
        copyFeedLink: "Copy this feed's link",
        deleteFeed: "Delete this feed",
        refreshingFeed: "Refreshing {0}\u2026",
        feedRefreshed: "{0} refreshed",
        couldNotRefreshFeed: "Could not refresh {0}",
        refreshingAll: "Refreshing all feeds\u2026",
        allRefreshed: "All feeds refreshed",
        feedsPartlyFailed: "{0} of {1} feed(s) could not be refreshed",
        couldNotRefreshAny: "Could not refresh any feeds",
        feedAlreadyExists: "You\u2019re already subscribed to this feed",
        invalidUrl: "That doesnt look like a valid URL",
        invalidFeed: "That link isnt a valid RSS or Atom feed",
        couldNotAddFeed: "Could not add feed",
        feedAdded: "Feed added",
        feedDeleted: "{0} deleted",
        couldNotDeleteFeed: "Could not delete {0}",
        addedToPlaylistToast: "Added to playlist",
        removedFromPlaylistToast: "Removed from playlist",
        loading: "Loading\u2026",
        addToPlaylist: "Add to playlist",
        removeFromPlaylist: "Remove from playlist",
        playlistTitle: "My Playlists",
        playlistEmpty: "Your playlist is empty. Bookmark articles to add them here.",
        playlistFeedUrlLabel: "Subscribe to your playlist in any RSS reader:",
        copyLink: "Copy link",
        linkCopied: "Link copied",
        backToArticles: "\u2190 Back to articles",
        refreshSummary: "Refresh summary",
        shareSummary: "Copy summary",
        summaryGenerating: "Generating summary\u2026",
        summaryUpdated: "Summary updated",
        summaryFailed: "Could not generate summary",
        welcomeHeading: "Welcome Back \ud83d\udc4b",
        welcomeSubtitle: "Stay updated with your favorite news.",
        aiPoweredBadge: "AI Powered",
        dailySummaryHeading: "Daily News Summary",
        dailySummaryPlaceholder: "Your personalized AI summary will appear here after refreshing your feeds.",
        statAvailable: "Available",
        statConnected: "Connected",
        trendingHeading: "Trending Topics",
        articles: "articles",
        article: "article",
        feeds: "Feeds",
        feed: "Feed",
        addToPlaylistDialog: "Choose a playlist",
        newPlaylist: "+ New playlist",
        newPlaylistName: "New playlist name",
        create: "Create",
        rename: "Rename",
        deletePlaylist: "Delete playlist",
        renamePlaylist: "Rename playlist",
        playlistNameLabel: "Playlist name",
        chatWithAI: "Chat with AI",
        chatPlaceholder: "Ask about your articles...",
        clearChat: "Clear chat",
        chatWelcome: "Ask me anything about your RSS feed articles! I can summarize, find trends, or answer questions.",
        shareArticle: "Copy article link",
        shareNative: "Share",
        noShareTargets: "No sharing apps available on this device - try Copy instead",
        shareBy: "Copy this",
        shareFailed: "Couldn't copy this",
        shareLabel: "Copy",
        saveLabel: "Playlist",
        savedLabel: "In Playlist",
        starLabel: "Save",
        starredLabel: "Saved",
        starArticle: "Save article",
        unstarArticle: "Remove from Saved",
        savedPageTitle: "Saved",
        savedHeading: "Saved Articles",
        emptySaved: "No saved articles yet",
        addedToSaved: "Saved",
        removedFromSaved: "Removed from Saved",
        historyPageTitle: "History",
        historyHeading: "Reading History",
        emptyHistory: "No reading history yet",
        clearHistory: "Clear history",
        historyCleared: "History cleared",
        confirmClearHistory: "Clear your entire reading history? This can't be undone.",
        cancel: "Cancel",
        confirm: "Confirm",
        accountMenu: "Account",
        signIn: "Sign in",
        createAccount: "Create account",
        signOut: "Sign out",
        back: "Back",
        emailPlaceholder: "Email",
        passwordPlaceholder: "Password (min. 8 characters)",
        signInHeading: "Sign in",
        createAccountHeading: "Create your account",
        signInRequired: "Sign in to use this feature",
        invalidCredentials: "Invalid email or password",
        registrationFailed: "Could not create account - try a different email or a longer password",
        networkError: "Network error - please try again",
        welcomePopupMessage: "Welcome! Sign in or create a free account to save articles, build playlists, and keep a reading history.",
        savedDemoTitle: "Save articles to read later",
        savedDemoDesc: "Tap the \u2606 star on any article to save it here.",
        savedDemoNote: "This is an example of a saved article, not a real one",
        historyDemoTitle: "Keep track of what you've read",
        historyDemoDesc: "Every time you open an article, it's logged here automatically.",
        historyDemoNote: "Example - read 2 hours ago, not a real entry",
        playlistDemoTitle: "Build custom reading playlists",
        playlistDemoDesc: "Group articles into playlists, each with its own personal RSS feed you can subscribe to elsewhere.",
        playlistDemoNote: "Example item in a playlist called \u201cWeekend Reads\u201d",
        demoFeedName: "Example Feed",
        demoArticleTitle1: "Local elections could reshape the city council",
        demoArticleTitle2: "Tech company unveils new product line",
        exampleBadge: "Example",
        signedInToast: "Signed in",
        accountCreatedToast: "Account created",
        signedOutToast: "Signed out",
        summarizeLabel: "Summarize",
        summarizing: "Summarizing...",
        couldNotSummarize: "Could not summarize this article",
        couldNotSaveArticle: "Could not save this article",
        backToArticlesShort: "Back",
        chatLabel: "Chat",
        noTopics: "No topics",
        filterByTopic: "Filter by category",
        filterByFeed: "Filter by feed",
        allFeeds: "All feeds",
        cat_politics: "Politics",
        cat_business: "Business",
        cat_technology: "Technology",
        cat_sports: "Sports",
        cat_health: "Health",
        cat_science: "Science",
        cat_entertainment: "Entertainment",
        cat_environment: "Environment",
        cat_world: "World",
        cat_general: "General",
        signInToManageFeeds: "Sign in to add your favorite feeds and delete unwanted ones",
        rateLimited: "Too many requests. Please wait a moment before trying again.",
        deleteAccount: "Delete account permanently",
        confirmDeleteAccount: "This will permanently delete your account, all your feeds, saved articles, playlists, and chat history. This cannot be undone. Are you sure?"
    },
    ar: {
        title: "\u0642\u0627\u0631\u0626 RSS \u0627\u0644\u0634\u062e\u0635\u064a",
        heading: "\u0642\u0627\u0631\u0626 RSS \u0627\u0644\u0634\u062e\u0635\u064a",
        tagline: "\u0645\u0631\u0643\u0632 \u0627\u0644\u0623\u062e\u0628\u0627\u0631 \u0627\u0644\u0634\u062e\u0635\u064a",
        langLabel: "EN",
        refreshAllTitle: "\u062a\u062d\u062f\u064a\u062b \u062c\u0645\u064a\u0639 \u0627\u0644\u0645\u0635\u0627\u062f\u0631",
        refreshAll: "\u062a\u062d\u062f\u064a\u062b \u0627\u0644\u0643\u0644",
        themeTitle: "\u062a\u0628\u062f\u064a\u0644 \u0627\u0644\u0648\u0636\u0639 \u0627\u0644\u0645\u0638\u0644\u0645",
        addFeed: "\u0625\u0636\u0627\u0641\u0629 \u0645\u0635\u062f\u0631",
        feedUrlLabel: "\u0631\u0627\u0628\u0637 \u062e\u0644\u0627\u0635\u0629 RSS",
        feedUrlPlaceholder: "https://example.com/feed.xml",
        feedUrlError: "\u0627\u0644\u0631\u062c\u0627\u0621 \u0625\u062f\u062e\u0627\u0644 \u0631\u0627\u0628\u0637 \u0627\u0644\u0645\u0635\u062f\u0631 \u0623\u0648\u0644\u0627\u064b",
        searchPlaceholder: "\u0627\u0628\u062d\u062b \u0641\u064a \u0627\u0644\u0645\u0642\u0627\u0644\u0627\u062a \u0628\u0627\u0644\u0639\u0646\u0648\u0627\u0646",
        searchFeedsPlaceholder: "\u0627\u0628\u062d\u062b \u0639\u0646 \u0627\u0644\u0645\u0635\u0627\u062f\u0631...",
        noFeedsMatchSearch: "\u0644\u0627 \u062a\u0648\u062c\u062f \u0645\u0635\u0627\u062f\u0631 \u0645\u0637\u0627\u0628\u0642\u0629",
        browseFeeds: "\u0627\u0644\u0645\u0635\u0627\u062f\u0631",
        allCategories: "\u0627\u0644\u0643\u0644",
        showMoreFeeds: "\u0639\u0631\u0636 {0} \u0623\u062e\u0631\u0649",
        subscriptions: "\u0627\u0644\u0627\u0634\u062a\u0631\u0627\u0643\u0627\u062a",
        dispatches: "\u0627\u0644\u0645\u0642\u0627\u0644\u0627\u062a",
        showAll: "\u0625\u0638\u0647\u0627\u0631 \u0627\u0644\u0643\u0644",
        showLess: "\u0625\u0638\u0647\u0627\u0631 \u0623\u0642\u0644",
        showMore: "\u0625\u0638\u0647\u0627\u0631 \u0627\u0644\u0645\u0632\u064a\u062f",
        readArticle: "\u0627\u0642\u0631\u0623 \u0627\u0644\u0645\u0642\u0627\u0644 \u2190",
        noSubscriptions: "\u0644\u0627 \u062a\u0648\u062c\u062f \u0627\u0634\u062a\u0631\u0627\u0643\u0627\u062a \u0628\u0639\u062f. \u0623\u0636\u0641 \u0631\u0627\u0628\u0637 \u0645\u0635\u062f\u0631 \u0644\u0644\u0628\u062f\u0621.",
        noArticles: "\u0644\u0627 \u062a\u0648\u062c\u062f \u0645\u0642\u0627\u0644\u0627\u062a.",
        all: "\u0627\u0644\u0643\u0644",
        page: "\u0627\u0644\u0635\u0641\u062d\u0629",
        of: "\u0645\u0646",
        previous: "\u0627\u0644\u0633\u0627\u0628\u0642",
        next: "\u0627\u0644\u062a\u0627\u0644\u064a",
        jumpTo: "\u0627\u0646\u062a\u0642\u0644 \u0625\u0644\u0649",
        refreshFeed: "\u062a\u062d\u062f\u064a\u062b \u0647\u0630\u0627 \u0627\u0644\u0645\u0635\u062f\u0631",
        copyFeedLink: "\u0646\u0633\u062e \u0631\u0627\u0628\u0637 \u0647\u0630\u0627 \u0627\u0644\u0645\u0635\u062f\u0631",
        deleteFeed: "\u062d\u0630\u0641 \u0647\u0630\u0627 \u0627\u0644\u0645\u0635\u062f\u0631",
        refreshingFeed: "\u062c\u0627\u0631\u064a \u062a\u062d\u062f\u064a\u062b {0}\u2026",
        feedRefreshed: "\u062a\u0645 \u062a\u062d\u062f\u064a\u062b {0}",
        couldNotRefreshFeed: "\u062a\u0639\u0630\u0631 \u062a\u062d\u062f\u064a\u062b {0}",
        refreshingAll: "\u062c\u0627\u0631\u064a \u062a\u062d\u062f\u064a\u062b \u062c\u0645\u064a\u0639 \u0627\u0644\u0645\u0635\u0627\u062f\u0631\u2026",
        allRefreshed: "\u062a\u0645 \u062a\u062d\u062f\u064a\u062b \u062c\u0645\u064a\u0639 \u0627\u0644\u0645\u0635\u0627\u062f\u0631",
        feedsPartlyFailed: "\u062a\u0639\u0630\u0631 \u062a\u062d\u062f\u064a\u062b {0} \u0645\u0646 \u0623\u0635\u0644 {1} \u0645\u0635\u062f\u0631",
        couldNotRefreshAny: "\u062a\u0639\u0630\u0631 \u062a\u062d\u062f\u064a\u062b \u0623\u064a \u0645\u0635\u062f\u0631",
        feedAlreadyExists: "\u0623\u0646\u062a \u0645\u0634\u062a\u0631\u0643 \u0628\u0627\u0644\u0641\u0639\u0644 \u0641\u064a \u0647\u0630\u0627 \u0627\u0644\u0645\u0635\u062f\u0631",
        invalidUrl: "\u0627\u0644\u0631\u0627\u0628\u0637 \u063a\u064a\u0631 \u0635\u0627\u0644\u062d",
        invalidFeed: "\u0647\u0630\u0627 \u0627\u0644\u0631\u0627\u0628\u0637 \u0644\u064a\u0633 RSS \u0623\u0648 Atom \u0635\u0627\u0644\u062d\u0627\u064b",
        couldNotAddFeed: "\u062a\u0639\u0630\u0631\u062a \u0625\u0636\u0627\u0641\u0629 \u0627\u0644\u0645\u0635\u062f\u0631",
        feedAdded: "\u062a\u0645\u062a \u0625\u0636\u0627\u0641\u0629 \u0627\u0644\u0645\u0635\u062f\u0631",
        feedDeleted: "\u062a\u0645 \u062d\u0630\u0641 {0}",
        couldNotDeleteFeed: "\u062a\u0639\u0630\u0631 \u062d\u0630\u0641 {0}",
        addedToPlaylistToast: "\u062a\u062a\u064a\u0645\u062a \u0627\u0644\u0625\u0636\u0627\u0641\u0629 \u0625\u0644\u0649 \u0642\u0627\u0626\u0645\u0629 \u0627\u0644\u062a\u0634\u063a\u064a\u0644",
        removedFromPlaylistToast: "\u062a\u0645\u062a \u0627\u0644\u0625\u0632\u0627\u0644\u0629 \u0645\u0646 \u0642\u0627\u0626\u0645\u0629 \u0627\u0644\u062a\u0634\u063a\u064a\u0644",
        loading: "\u062c\u0627\u0631\u064d \u0627\u0644\u062a\u062d\u0645\u064a\u0644\u2026",
        addToPlaylist: "\u0623\u0636\u0641 \u0625\u0644\u0649 \u0642\u0627\u0626\u0645\u0629 \u0627\u0644\u062a\u0634\u063a\u064a\u0644",
        removeFromPlaylist: "\u0625\u0632\u0627\u0644\u0629 \u0645\u0646 \u0642\u0627\u0626\u0645\u0629 \u0627\u0644\u062a\u0634\u063a\u064a\u0644",
        playlistTitle: "\u0642\u0648\u0627\u0626\u0645 \u0627\u0644\u062a\u0634\u063a\u064a\u0644",
        playlistEmpty: "\u0642\u0627\u0626\u0645\u0629 \u0627\u0644\u062a\u0634\u063a\u064a\u0644 \u0641\u0627\u0631\u063a\u0629. \u0623\u0636\u0641 \u0645\u0642\u0627\u0644\u0627\u062a \u0625\u0644\u064a\u0647\u0627 \u0628\u0627\u0644\u0636\u063a\u0637 \u0639\u0644\u0649 \u0623\u064a\u0642\u0648\u0646\u0629 \u0627\u0644\u062d\u0641\u0638.",
        playlistFeedUrlLabel: "\u0627\u0634\u062a\u0631\u0643 \u0641\u064a \u0642\u0627\u0626\u0645\u0629 \u0627\u0644\u062a\u0634\u063a\u064a\u0644 \u0645\u0646 \u0623\u064a \u0642\u0627\u0631\u0626 RSS:",
        copyLink: "\u0646\u0633\u062e \u0627\u0644\u0631\u0627\u0628\u0637",
        linkCopied: "\u062a\u0645 \u0646\u0633\u062e \u0627\u0644\u0631\u0627\u0628\u0637",
        backToArticles: "\u2192 \u0627\u0644\u0639\u0648\u062f\u0629 \u0625\u0644\u0649 \u0627\u0644\u0645\u0642\u0627\u0644\u0627\u062a",
        refreshSummary: "\u062a\u062d\u062f\u064a\u062b \u0627\u0644\u0645\u0644\u062e\u0635",
        shareSummary: "\u0646\u0633\u062e \u0627\u0644\u0645\u0644\u062e\u0635",
        summaryGenerating: "\u062c\u0627\u0631\u064d \u0625\u0646\u0634\u0627\u0621 \u0627\u0644\u0645\u0644\u062e\u0635\u2026",
        summaryUpdated: "\u062a\u0645 \u062a\u062d\u062f\u064a\u062b \u0627\u0644\u0645\u0644\u062e\u0635",
        summaryFailed: "\u062a\u0639\u0630\u0631 \u0625\u0646\u0634\u0627\u0621 \u0627\u0644\u0645\u0644\u062e\u0635",
        welcomeHeading: "\u0623\u0647\u0644\u0627\u064b \u0628\u0639\u0648\u062f\u062a\u0643 \ud83d\udc4b",
        welcomeSubtitle: "\u062a\u0627\u0628\u0639 \u0622\u062e\u0631 \u0623\u062e\u0628\u0627\u0631 \u0645\u0635\u0627\u062f\u0631\u0643 \u0627\u0644\u0645\u0641\u0636\u0644\u0629.",
        aiPoweredBadge: "\u0645\u062f\u0639\u0648\u0645 \u0628\u0627\u0644\u0630\u0643\u0627\u0621 \u0627\u0644\u0627\u0635\u0637\u0646\u0627\u0639\u064a",
        dailySummaryHeading: "\u0645\u0644\u062e\u0635 \u0627\u0644\u0623\u062e\u0628\u0627\u0631 \u0627\u0644\u064a\u0648\u0645\u064a",
        dailySummaryPlaceholder: "\u0633\u064a\u0638\u0647\u0631 \u0645\u0644\u062e\u0635\u0643 \u0627\u0644\u0645\u062e\u0635\u0635 \u0647\u0646\u0627 \u0628\u0639\u062f \u062a\u062d\u062f\u064a\u062b \u0645\u0635\u0627\u062f\u0631\u0643.",
        statAvailable: "\u0645\u062a\u0627\u062d\u0629",
        statConnected: "\u0645\u062a\u0635\u0644\u0629",
        trendingHeading: "\u0627\u0644\u0645\u0648\u0627\u0636\u064a\u0639 \u0627\u0644\u0631\u0627\u0626\u062c\u0629",
        articles: "\u0645\u0642\u0627\u0644\u0627\u062a",
        article: "\u0645\u0642\u0627\u0644\u0629",
        feeds: "\u0645\u0635\u0627\u062f\u0631",
        feed: "\u0645\u0635\u062f\u0631",
        addToPlaylistDialog: "\u0627\u062e\u062a\u0631 \u0642\u0627\u0626\u0645\u0629 \u062a\u0634\u063a\u064a\u0644",
        newPlaylist: "+ \u0642\u0627\u0626\u0645\u0629 \u062c\u062f\u064a\u062f\u0629",
        newPlaylistName: "\u0627\u0633\u0645 \u0627\u0644\u0642\u0627\u0626\u0645\u0629 \u0627\u0644\u062c\u062f\u064a\u062f\u0629",
        create: "\u0625\u0646\u0634\u0627\u0621",
        rename: "\u0625\u0639\u0627\u062f\u0629 \u062a\u0633\u0645\u064a\u0629",
        deletePlaylist: "\u062d\u0630\u0641 \u0627\u0644\u0642\u0627\u0626\u0645\u0629",
        renamePlaylist: "\u0625\u0639\u0627\u062f\u0629 \u062a\u0633\u0645\u064a\u0629 \u0627\u0644\u0642\u0627\u0626\u0645\u0629",
        playlistNameLabel: "\u0627\u0633\u0645 \u0627\u0644\u0642\u0627\u0626\u0645\u0629",
        chatWithAI: "\u0627\u0644\u062f\u0631\u062f\u0634\u0629 \u0645\u0639 \u0627\u0644\u0630\u0643\u0627\u0621 \u0627\u0644\u0627\u0635\u0637\u0646\u0627\u0639\u064a",
        chatPlaceholder: "\u0627\u0633\u0623\u0644 \u0639\u0646 \u0645\u0642\u0627\u0644\u0627\u062a\u0643...",
        clearChat: "\u0645\u0633\u062d \u0627\u0644\u062f\u0631\u062f\u0634\u0629",
        chatWelcome: "\u0627\u0633\u0623\u0644\u0646\u064a \u0623\u064a \u0634\u064a\u0621 \u0639\u0646 \u0645\u0642\u0627\u0644\u0627\u062a RSS \u0627\u0644\u062e\u0627\u0635\u0629 \u0628\u0643! \u064a\u0645\u0643\u0646\u0646\u064a \u0627\u0644\u062a\u0644\u062e\u064a\u0635 \u0648\u0627\u0643\u062a\u0634\u0627\u0641 \u0627\u0644\u0627\u062a\u062c\u0627\u0647\u0627\u062a \u0623\u0648 \u0627\u0644\u0625\u062c\u0627\u0628\u0629 \u0639\u0644\u0649 \u0627\u0644\u0623\u0633\u0626\u0644\u0629.",
        shareArticle: "\u0646\u0633\u062e \u0631\u0627\u0628\u0637 \u0627\u0644\u0645\u0642\u0627\u0644",
        shareNative: "\u0645\u0634\u0627\u0631\u0643\u0629",
        noShareTargets: "\u0644\u0627 \u062a\u0648\u062c\u062f \u062a\u0637\u0628\u064a\u0642\u0627\u062a \u0645\u0634\u0627\u0631\u0643\u0629 \u0645\u062a\u0627\u062d\u0629 \u0639\u0644\u0649 \u0647\u0630\u0627 \u0627\u0644\u062c\u0647\u0627\u0632 - \u062c\u0631\u0651\u0628 \u0632\u0631 \u0627\u0644\u0646\u0633\u062e \u0628\u062f\u0644\u0627\u064b",
        shareBy: "\u0646\u0633\u062e",
        shareFailed: "\u062a\u0639\u0630\u0631 \u0627\u0644\u0646\u0633\u062e",
        shareLabel: "\u0646\u0633\u062e",
        saveLabel: "\u0642\u0627\u0626\u0645\u0629 \u062a\u0634\u063a\u064a\u0644",
        savedLabel: "\u0641\u064a \u0642\u0627\u0626\u0645\u0629 \u0627\u0644\u062a\u0634\u063a\u064a\u0644",
        starLabel: "\u062d\u0641\u0638",
        starredLabel: "\u0645\u062d\u0641\u0648\u0638",
        starArticle: "\u062d\u0641\u0638 \u0627\u0644\u0645\u0642\u0627\u0644",
        unstarArticle: "\u0625\u0632\u0627\u0644\u0629 \u0645\u0646 \u0627\u0644\u0645\u062d\u0641\u0648\u0638\u0627\u062a",
        savedPageTitle: "\u0627\u0644\u0645\u062d\u0641\u0648\u0638\u0627\u062a",
        savedHeading: "\u0627\u0644\u0645\u0642\u0627\u0644\u0627\u062a \u0627\u0644\u0645\u062d\u0641\u0648\u0638\u0629",
        emptySaved: "\u0644\u0627 \u062a\u0648\u062c\u062f \u0645\u0642\u0627\u0644\u0627\u062a \u0645\u062d\u0641\u0648\u0638\u0629 \u0628\u0639\u062f",
        addedToSaved: "\u062a\u0645 \u0627\u0644\u062d\u0641\u0638",
        removedFromSaved: "\u062a\u0645\u062a \u0627\u0644\u0625\u0632\u0627\u0644\u0629 \u0645\u0646 \u0627\u0644\u0645\u062d\u0641\u0648\u0638\u0627\u062a",
        historyPageTitle: "\u0627\u0644\u0633\u062c\u0644",
        historyHeading: "\u0633\u062c\u0644 \u0627\u0644\u0642\u0631\u0627\u0621\u0629",
        emptyHistory: "\u0644\u0627 \u064a\u0648\u062c\u062f \u0633\u062c\u0644 \u0642\u0631\u0627\u0621\u0629 \u0628\u0639\u062f",
        clearHistory: "\u0645\u0633\u062d \u0627\u0644\u0633\u062c\u0644",
        historyCleared: "\u062a\u0645 \u0645\u0633\u062d \u0627\u0644\u0633\u062c\u0644",
        confirmClearHistory: "\u0647\u0644 \u062a\u0631\u064a\u062f \u0645\u0633\u062d \u0633\u062c\u0644 \u0627\u0644\u0642\u0631\u0627\u0621\u0629 \u0628\u0627\u0644\u0643\u0627\u0645\u0644\u061f \u0644\u0627 \u064a\u0645\u0643\u0646 \u0627\u0644\u062a\u0631\u0627\u062c\u0639 \u0639\u0646 \u0630\u0644\u0643.",
        cancel: "\u0625\u0644\u063a\u0627\u0621",
        confirm: "\u062a\u0623\u0643\u064a\u062f",
        accountMenu: "\u0627\u0644\u062d\u0633\u0627\u0628",
        signIn: "\u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062f\u062e\u0648\u0644",
        createAccount: "\u0625\u0646\u0634\u0627\u0621 \u062d\u0633\u0627\u0628",
        signOut: "\u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062e\u0631\u0648\u062c",
        back: "\u0631\u062c\u0648\u0639",
        emailPlaceholder: "\u0627\u0644\u0628\u0631\u064a\u062f \u0627\u0644\u0625\u0644\u0643\u062a\u0631\u0648\u0646\u064a",
        passwordPlaceholder: "\u0643\u0644\u0645\u0629 \u0627\u0644\u0645\u0631\u0648\u0631 (8 \u0623\u062d\u0631\u0641 \u0639\u0644\u0649 \u0627\u0644\u0623\u0642\u0644)",
        signInHeading: "\u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062f\u062e\u0648\u0644",
        createAccountHeading: "\u0625\u0646\u0634\u0627\u0621 \u062d\u0633\u0627\u0628\u0643",
        signInRequired: "\u0633\u062c\u0651\u0644 \u0627\u0644\u062f\u062e\u0648\u0644 \u0644\u0627\u0633\u062a\u062e\u062f\u0627\u0645 \u0647\u0630\u0647 \u0627\u0644\u0645\u064a\u0632\u0629",
        invalidCredentials: "\u0628\u0631\u064a\u062f \u0625\u0644\u0643\u062a\u0631\u0648\u0646\u064a \u0623\u0648 \u0643\u0644\u0645\u0629 \u0645\u0631\u0648\u0631 \u063a\u064a\u0631 \u0635\u062d\u064a\u062d\u0629",
        registrationFailed: "\u062a\u0639\u0630\u0631 \u0625\u0646\u0634\u0627\u0621 \u0627\u0644\u062d\u0633\u0627\u0628 - \u062c\u0631\u0651\u0628 \u0628\u0631\u064a\u062f\u0627 \u0625\u0644\u0643\u062a\u0631\u0648\u0646\u064a\u0627 \u0622\u062e\u0631 \u0623\u0648 \u0643\u0644\u0645\u0629 \u0645\u0631\u0648\u0631 \u0623\u0637\u0648\u0644",
        networkError: "\u062e\u0637\u0623 \u0641\u064a \u0627\u0644\u0634\u0628\u0643\u0629 - \u064a\u0631\u062c\u0649 \u0627\u0644\u0645\u062d\u0627\u0648\u0644\u0629 \u0645\u0631\u0629 \u0623\u062e\u0631\u0649",
        welcomePopupMessage: "\u0623\u0647\u0644\u0627\u064b! \u0633\u062c\u0651\u0644 \u0627\u0644\u062f\u062e\u0648\u0644 \u0623\u0648 \u0623\u0646\u0634\u0626 \u062d\u0633\u0627\u0628\u0627\u064b \u0645\u062c\u0627\u0646\u064a\u0627\u064b \u0644\u062d\u0641\u0638 \u0627\u0644\u0645\u0642\u0627\u0644\u0627\u062a \u0648\u0625\u0646\u0634\u0627\u0621 \u0642\u0648\u0627\u0626\u0645 \u062a\u0634\u063a\u064a\u0644 \u0648\u062d\u0641\u0638 \u0633\u062c\u0644 \u0642\u0631\u0627\u0621\u0629.",
        savedDemoTitle: "\u0627\u062d\u0641\u0638 \u0627\u0644\u0645\u0642\u0627\u0644\u0627\u062a \u0644\u0642\u0631\u0627\u0621\u062a\u0647\u0627 \u0644\u0627\u062d\u0642\u0627\u064b",
        savedDemoDesc: "\u0627\u0636\u063a\u0637 \u0639\u0644\u0649 \u0646\u062c\u0645\u0629 \u2606 \u0641\u064a \u0623\u064a \u0645\u0642\u0627\u0644 \u0644\u062d\u0641\u0638\u0647 \u0647\u0646\u0627.",
        savedDemoNote: "\u0647\u0630\u0627 \u0645\u062b\u0627\u0644 \u0644\u0645\u0642\u0627\u0644 \u0645\u062d\u0641\u0648\u0638\u060c \u0648\u0644\u064a\u0633 \u0645\u0642\u0627\u0644\u0627\u064b \u062d\u0642\u064a\u0642\u064a\u0627\u064b",
        historyDemoTitle: "\u062a\u062a\u0628\u0639 \u0645\u0627 \u0642\u0631\u0623\u062a\u0647",
        historyDemoDesc: "\u0643\u0644 \u0645\u0631\u0629 \u062a\u0641\u062a\u062d \u0641\u064a\u0647\u0627 \u0645\u0642\u0627\u0644\u0627\u064b\u060c \u064a\u064f\u0633\u062c\u0651\u0644 \u0647\u0646\u0627 \u062a\u0644\u0642\u0627\u0626\u064a\u0627\u064b.",
        historyDemoNote: "\u0645\u062b\u0627\u0644 - \u062a\u0645 \u0627\u0644\u0642\u0631\u0627\u0621\u0629 \u0642\u0628\u0644 \u0633\u0627\u0639\u062a\u064a\u0646\u060c \u0644\u064a\u0633 \u0625\u062f\u062e\u0627\u0644\u0627\u064b \u062d\u0642\u064a\u0642\u064a\u0627\u064b",
        playlistDemoTitle: "\u0623\u0646\u0634\u0626 \u0642\u0648\u0627\u0626\u0645 \u062a\u0634\u063a\u064a\u0644 \u062e\u0627\u0635\u0629 \u0628\u0643",
        playlistDemoDesc: "\u062c\u0645\u0651\u0639 \u0627\u0644\u0645\u0642\u0627\u0644\u0627\u062a \u0641\u064a \u0642\u0648\u0627\u0626\u0645 \u062a\u0634\u063a\u064a\u0644\u060c \u0644\u0643\u0644 \u0645\u0646\u0647\u0627 \u0631\u0627\u0628\u0637 RSS \u062e\u0627\u0635 \u064a\u0645\u0643\u0646\u0643 \u0627\u0644\u0627\u0634\u062a\u0631\u0627\u0643 \u0641\u064a\u0647 \u0641\u064a \u0645\u0643\u0627\u0646 \u0622\u062e\u0631.",
        playlistDemoNote: "\u0645\u062b\u0627\u0644 \u0644\u0639\u0646\u0635\u0631 \u0641\u064a \u0642\u0627\u0626\u0645\u0629 \u062a\u0634\u063a\u064a\u0644 \u0628\u0627\u0633\u0645 \u201c\u0642\u0631\u0627\u0621\u0627\u062a \u0646\u0647\u0627\u064a\u0629 \u0627\u0644\u0623\u0633\u0628\u0648\u0639\u201d",
        demoFeedName: "\u0645\u0635\u062f\u0631 \u062a\u062c\u0631\u064a\u0628\u064a",
        demoArticleTitle1: "\u0627\u0646\u062a\u062e\u0627\u0628\u0627\u062a \u0645\u062d\u0644\u064a\u0629 \u0642\u062f \u062a\u0639\u064a\u062f \u0631\u0633\u0645 \u0645\u062c\u0644\u0633 \u0627\u0644\u0645\u062f\u064a\u0646\u0629",
        demoArticleTitle2: "\u0634\u0631\u0643\u0629 \u062a\u0642\u0646\u064a\u0629 \u062a\u0643\u0634\u0641 \u0639\u0646 \u062e\u0637 \u0645\u0646\u062a\u062c\u0627\u062a \u062c\u062f\u064a\u062f",
        exampleBadge: "\u0645\u062b\u0627\u0644",
        signedInToast: "\u062a\u0645 \u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062f\u062e\u0648\u0644",
        accountCreatedToast: "\u062a\u0645 \u0625\u0646\u0634\u0627\u0621 \u0627\u0644\u062d\u0633\u0627\u0628",
        signedOutToast: "\u062a\u0645 \u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062e\u0631\u0648\u062c",
        summarizeLabel: "\u062a\u0644\u062e\u064a\u0635",
        summarizing: "\u062c\u0627\u0631\u064a \u0627\u0644\u062a\u0644\u062e\u064a\u0635...",
        couldNotSummarize: "\u062a\u0639\u0630\u0631 \u062a\u0644\u062e\u064a\u0635 \u0647\u0630\u0627 \u0627\u0644\u0645\u0642\u0627\u0644",
        couldNotSaveArticle: "\u062a\u0639\u0630\u0631 \u062d\u0641\u0638 \u0647\u0630\u0627 \u0627\u0644\u0645\u0642\u0627\u0644",
        backToArticlesShort: "\u0631\u062c\u0648\u0639",
        chatLabel: "\u0645\u062d\u0627\u062f\u062b\u0629",
        noTopics: "\u0628\u062f\u0648\u0646 \u0645\u0648\u0627\u0636\u064a\u0639",
        filterByTopic: "\u062a\u0635\u0641\u064a\u0629 \u062d\u0633\u0628 \u0627\u0644\u0641\u0626\u0629",
        filterByFeed: "\u062a\u0635\u0641\u064a\u0629 \u062d\u0633\u0628 \u0627\u0644\u0645\u0635\u062f\u0631",
        allFeeds: "\u0643\u0644 \u0627\u0644\u0645\u0635\u0627\u062f\u0631",
        cat_politics: "\u0633\u064a\u0627\u0633\u0629",
        cat_business: "\u0627\u0642\u062a\u0635\u0627\u062f",
        cat_technology: "\u062a\u0643\u0646\u0648\u0644\u0648\u062c\u064a\u0627",
        cat_sports: "\u0631\u064a\u0627\u0636\u0629",
        cat_health: "\u0635\u062d\u0629",
        cat_science: "\u0639\u0644\u0648\u0645",
        cat_entertainment: "\u0641\u0646 \u0648\u062a\u0631\u0641\u064a\u0647",
        cat_environment: "\u0628\u064a\u0626\u0629",
        cat_world: "\u0627\u0644\u0639\u0627\u0644\u0645",
        cat_general: "\u0639\u0627\u0645",
        signInToManageFeeds: "\u0633\u062c\u0651\u0644 \u0627\u0644\u062f\u062e\u0648\u0644 \u0644\u0625\u0636\u0627\u0641\u0629 \u0645\u0635\u0627\u062f\u0631\u0643 \u0627\u0644\u0645\u0641\u0636\u0644\u0629 \u0648\u062d\u0630\u0641 \u0627\u0644\u0645\u0635\u0627\u062f\u0631 \u063a\u064a\u0631 \u0627\u0644\u0645\u0631\u063a\u0648\u0628 \u0641\u064a\u0647\u0627",
        rateLimited: "\u0637\u0644\u0628\u0627\u062a \u0643\u062b\u064a\u0631\u0629 \u062c\u062f\u0627\u064b. \u0627\u0644\u0631\u062c\u0627\u0621 \u0627\u0644\u0627\u0646\u062a\u0638\u0627\u0631 \u0644\u062d\u0638\u0629 \u0642\u0628\u0644 \u0627\u0644\u0645\u062d\u0627\u0648\u0644\u0629 \u0645\u0631\u0629 \u0623\u062e\u0631\u0649.",
        deleteAccount: "\u062d\u0630\u0641 \u0627\u0644\u062d\u0633\u0627\u0628 \u0628\u0634\u0643\u0644 \u062f\u0627\u0626\u0645",
        confirmDeleteAccount: "\u0633\u064a\u0624\u062f\u064a \u0630\u0644\u0643 \u0625\u0644\u0649 \u062d\u0630\u0641 \u062d\u0633\u0627\u0628\u0643 \u0628\u0634\u0643\u0644 \u062f\u0627\u0626\u0645\u060c \u0648\u062c\u0645\u064a\u0639 \u0645\u0635\u0627\u062f\u0631\u0643 \u0648\u0645\u0642\u0627\u0644\u0627\u062a\u0643 \u0627\u0644\u0645\u062d\u0641\u0648\u0638\u0629 \u0648\u0642\u0648\u0627\u0626\u0645 \u0627\u0644\u062a\u0634\u063a\u064a\u0644 \u0648\u0633\u062c\u0644 \u0627\u0644\u062f\u0631\u062f\u0634\u0629. \u0644\u0627 \u064a\u0645\u0643\u0646 \u0627\u0644\u062a\u0631\u0627\u062c\u0639 \u0639\u0646 \u0630\u0644\u0643. \u0647\u0644 \u0623\u0646\u062a \u0645\u062a\u0623\u0643\u062f\u061f"
    }
};

let currentLang = localStorage.getItem("rss-lang") || "en";

function t(key) { return i18n[currentLang][key] || key; }

function tf(key, ...args) {
    let text = t(key);
    for (let i = 0; i < args.length; i++) text = text.replace("{" + i + "}", args[i]);
    return text;
}

function applyLanguage() {
    document.documentElement.lang = currentLang;
    document.documentElement.dir = currentLang === "ar" ? "rtl" : "ltr";
    langToggle.querySelector(".btn-label").textContent = t("langLabel");
    document.querySelectorAll("[data-i18n]").forEach(el => { el.textContent = t(el.getAttribute("data-i18n")); });
    document.querySelectorAll("[data-i18n-title]").forEach(el => { el.title = t(el.getAttribute("data-i18n-title")); });
    document.querySelectorAll("[data-i18n-placeholder]").forEach(el => { el.placeholder = t(el.getAttribute("data-i18n-placeholder")); });
    updateAccountIcon();
    loadFeeds().then(loadArticles);
    loadPlaylists().then(() => { if (showingPlaylist) renderPlaylistView(); });
    loadSaved().then(() => { if (showingSaved) renderSavedView(); });
    loadHistoryIds().then(() => { if (showingHistory) renderHistoryView(); });
    if (lastSummary) renderSummaryContent(lastSummary);
    loadSummary();
    loadChatHistory();
}

function isRtlLang(lang) {
    const rtl = ["ar", "he", "ur", "fa", "ps", "yi", "dv", "ku", "sd", "ug", "arc"];
    return rtl.includes(lang) || rtl.some(r => lang.startsWith(r + "-"));
}

var userSetLang = false;

langToggle.addEventListener("click", () => {
    currentLang = currentLang === "en" ? "ar" : "en";
    userSetLang = true;
    localStorage.setItem("rss-lang", currentLang);
    applyLanguage();
    setTimeout(() => { userSetLang = false; }, 500);
});

new MutationObserver(function () {
    if (userSetLang) return;
    var htmlLang = document.documentElement.lang;
    if (htmlLang && isRtlLang(htmlLang) && document.documentElement.dir !== "rtl") document.documentElement.dir = "rtl";
    if (htmlLang && !isRtlLang(htmlLang) && document.documentElement.dir === "rtl") document.documentElement.dir = "ltr";
}).observe(document.documentElement, { attributes: true, attributeFilter: ["lang"] });

function initTheme() {
    const saved = localStorage.getItem("rss-theme");
    const prefersDark = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
    applyTheme(saved || (prefersDark ? "dark" : "light"));
}

function applyTheme(theme) {
    document.documentElement.setAttribute("data-theme", theme);
    themeToggle.setAttribute("aria-pressed", theme === "dark" ? "true" : "false");
    document.getElementById("fabTheme").classList.toggle("active", theme === "dark");
    localStorage.setItem("rss-theme", theme);
}

themeToggle.addEventListener("click", () => {
    const current = document.documentElement.getAttribute("data-theme") === "dark" ? "dark" : "light";
    applyTheme(current === "dark" ? "light" : "dark");
});

const toastContainer = document.getElementById("toastContainer");

function showToast(message, type = "error", duration = 4000) {
    const toast = document.createElement("div");
    const icon = document.createElement("span");
    icon.className = "toast-icon";
    icon.setAttribute("aria-hidden", "true");
    const message_ = document.createElement("span");
    message_.className = "toast-message";
    const closeButton = document.createElement("button");
    closeButton.className = "toast-close";
    closeButton.type = "button";
    closeButton.textContent = "\u2715";
    closeButton.title = "Dismiss";
    let dismissTimer = null;
    const dismiss = () => {
        if (toast.classList.contains("closing")) return;
        toast.classList.add("closing");
        toast.addEventListener("animationend", () => toast.remove(), { once: true });
        // Fallback: in case the closing animation never fires (e.g. an RTL/LTR CSS
        // cascade conflict), make sure the toast still goes away.
        setTimeout(() => toast.remove(), 250);
    };
    closeButton.addEventListener("click", dismiss);
    toast.appendChild(icon);
    toast.appendChild(message_);
    toast.appendChild(closeButton);
    toastContainer.appendChild(toast);
    function set(newMessage, newType, newDuration = 4000) {
        toast.className = `toast ${newType}`;
        icon.textContent = newType === "error" ? "\u26A0" : newType === "loading" ? "\u21BB" : "\u2713";
        message_.textContent = newMessage;
        if (dismissTimer) clearTimeout(dismissTimer);
        if (newType !== "loading") dismissTimer = setTimeout(dismiss, newDuration);
    }
    set(message, type, duration);
    return { update: set, dismiss };
}

function feedColorVar(title) {
    let hash = 0;
    for (let i = 0; i < title.length; i++) hash = (hash * 31 + title.charCodeAt(i)) >>> 0;
    return `var(--f${hash % 8})`;
}

// Fixed taxonomy: articles are classified into one of these real categories based on
// keyword hits in their title/summary, instead of surfacing raw extracted words (which
// produced junk "categories" like stray URLs, brand names, or filler words).
const CATEGORY_KEYWORDS = {
    politics: ["election", "president", "government", "senate", "congress", "minister", "parliament", "policy", "vote", "campaign", "diplomat", "legislation", "governor", "\u0627\u0646\u062a\u062e\u0627\u0628", "\u062d\u0643\u0648\u0645\u0629", "\u0631\u0626\u064a\u0633", "\u0648\u0632\u064a\u0631", "\u0628\u0631\u0644\u0645\u0627\u0646", "\u0633\u064a\u0627\u0633\u0629"],
    business: ["market", "stock", "economy", "economic", "company", "revenue", "ceo", "startup", "trade", "investment", "inflation", "bank", "shares", "earnings", "\u0627\u0642\u062a\u0635\u0627\u062f", "\u0633\u0648\u0642", "\u0634\u0631\u0643\u0629", "\u0627\u0633\u062a\u062b\u0645\u0627\u0631", "\u062a\u0636\u062e\u0645", "\u0628\u0646\u0643"],
    technology: ["software", "app", "artificial intelligence", " ai ", "tech ", "computer", "smartphone", "chip", "cyber", "robot", "internet", "google", "apple", "microsoft", "startup tech", "gadget", "\u062a\u0642\u0646\u064a\u0629", "\u062a\u0643\u0646\u0648\u0644\u0648\u062c\u064a\u0627", "\u0630\u0643\u0627\u0621 \u0627\u0635\u0637\u0646\u0627\u0639\u064a", "\u062a\u0637\u0628\u064a\u0642", "\u062d\u0627\u0633\u0648\u0628"],
    sports: ["match", "game", "team", "league", "championship", "tournament", "player", "coach", "goal", "score", "olympic", "football", "basketball", "tennis", "\u0645\u0628\u0627\u0631\u0627\u0629", "\u0641\u0631\u064a\u0642", "\u062f\u0648\u0631\u064a", "\u0628\u0637\u0648\u0644\u0629", "\u0644\u0627\u0639\u0628", "\u0645\u062f\u0631\u0628"],
    health: ["health", "medical", "hospital", "disease", "virus", "vaccine", "doctor", "treatment", "patient", "covid", "surgery", "\u0635\u062d\u0629", "\u0637\u0628\u064a", "\u0645\u0633\u062a\u0634\u0641\u0649", "\u0645\u0631\u0636", "\u0644\u0642\u0627\u062d", "\u0639\u0644\u0627\u062c"],
    science: ["research", "study", "scientist", "discovery", "space", "nasa", "physics", "biology", "astronomy", "\u0639\u0644\u0645", "\u0628\u062d\u062b", "\u062f\u0631\u0627\u0633\u0629", "\u0627\u0643\u062a\u0634\u0627\u0641", "\u0641\u0636\u0627\u0621"],
    entertainment: ["movie", "film", "music", "celebrity", "actor", "singer", "tv show", "series", "album", "concert", "hollywood", "\u0641\u064a\u0644\u0645", "\u0645\u0648\u0633\u064a\u0642\u0649", "\u0641\u0646\u0627\u0646", "\u0645\u0645\u062b\u0644", "\u0645\u0633\u0644\u0633\u0644"],
    environment: ["climate", "environment", "pollution", "wildlife", "renewable energy", "emissions", "drought", "\u0628\u064a\u0626\u0629", "\u0645\u0646\u0627\u062e", "\u062a\u0644\u0648\u062b", "\u0637\u0627\u0642\u0629 \u0645\u062a\u062c\u062f\u062f\u0629"],
    world: ["war", "conflict", "country", "nation", "international", "united nations", "diplomatic", "border", "refugee", "invasion", "\u062d\u0631\u0628", "\u062f\u0648\u0644\u0629", "\u0635\u0631\u0627\u0639", "\u0623\u0645\u0645 \u0645\u062a\u062d\u062f\u0629", "\u0644\u0627\u062c\u0626"]
};
const CATEGORY_ORDER = Object.keys(CATEGORY_KEYWORDS);

function categorizeArticle(article) {
    const text = ((article.title || "") + " " + (article.summary || "")).toLowerCase();
    let bestCategory = null;
    let bestScore = 0;
    for (const category of CATEGORY_ORDER) {
        let score = 0;
        for (const kw of CATEGORY_KEYWORDS[category]) {
            if (text.includes(kw)) score++;
        }
        if (score > bestScore) { bestScore = score; bestCategory = category; }
    }
    return bestCategory || "general";
}

function getCategoryCounts(articles) {
    const counts = new Map();
    for (const article of articles) {
        const cat = categorizeArticle(article);
        counts.set(cat, (counts.get(cat) || 0) + 1);
    }
    return [...counts.entries()].sort((a, b) => b[1] - a[1]);
}

function renderTrendingTopics() {
    const counts = getCategoryCounts(allArticles);
    trendingTopicsDiv.innerHTML = "";
    if (counts.length === 0) {
        const empty = document.createElement("span");
        empty.className = "topic";
        empty.textContent = t("noTopics");
        trendingTopicsDiv.appendChild(empty);
        applyTrendingCollapseState();
        return;
    }

    const allChip = document.createElement("button");
    allChip.className = "topic" + (activeCategoryFilters.size === 0 ? " active" : "");
    allChip.textContent = `${t("all")} (${allArticles.length})`;
    allChip.addEventListener("click", () => {
        activeCategoryFilters.clear();
        renderTrendingTopics();
        renderArticles();
    });
    trendingTopicsDiv.appendChild(allChip);

    for (const [category, count] of counts) {
        const chip = document.createElement("button");
        chip.className = "topic" + (activeCategoryFilters.has(category) ? " active" : "");
        chip.textContent = `${t("cat_" + category)} (${count})`;
        chip.addEventListener("click", () => {
            if (activeCategoryFilters.has(category)) activeCategoryFilters.delete(category);
            else activeCategoryFilters.add(category);
            renderTrendingTopics();
            renderArticles();
        });
        trendingTopicsDiv.appendChild(chip);
    }
    applyTrendingCollapseState();
}

addFeedButton.addEventListener("click", async () => {
    const url = feedInput.value.trim();
    if (url === "") { feedUrlError.hidden = false; feedInput.focus(); return; }
    feedUrlError.hidden = true;

    if (!currentUser) {
        if (guestFeed) {
            showToast(t("signInToManageFeeds"), "info", 5000);
            return;
        }
        const response = await fetch("/feeds", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ url: url })
        });
        if (response.ok) {
            guestFeed = await response.json();
            feedInput.value = "";
            await loadFeeds();
            await loadArticles();
            showToast(t("feedAdded"), "success", 3000);
        } else {
            let reason = "";
            try { reason = await response.json(); } catch { reason = ""; }
            const messages = { "Feed already exists": t("feedAlreadyExists"), "Invalid URL": t("invalidUrl"), "Invalid Rss or Atom feed": t("invalidFeed") };
            showToast(messages[reason] || t("couldNotAddFeed"), "error");
        }
        return;
    }

    const response = await fetch("/feeds", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ url: url })
    });
    if (response.ok) {
        feedInput.value = "";
        await loadFeeds();
        await loadArticles();
        showToast(t("feedAdded"), "success", 3000);
    } else {
        let reason = "";
        try { reason = await response.json(); } catch { reason = ""; }
        const messages = { "Feed already exists": t("feedAlreadyExists"), "Invalid URL": t("invalidUrl"), "Invalid Rss or Atom feed": t("invalidFeed") };
        showToast(messages[reason] || t("couldNotAddFeed"), "error");
    }
});

feedInput.addEventListener("keydown", (e) => { if (e.key === "Enter") addFeedButton.click(); });
feedInput.addEventListener("input", () => { feedUrlError.hidden = true; });
articleSearchInput.addEventListener("input", () => {
    searchTerm = articleSearchInput.value.trim().toLowerCase();
    currentPage = 1;
    sessionStorage.setItem("currentPage", currentPage);
    if (showingPlaylist) renderPlaylistView();
    else if (showingSaved) renderSavedView();
    else if (showingHistory) renderHistoryView();
    else renderArticles();
});

const AUTO_REFRESH_INTERVAL_MS = 10 * 60 * 1000;
let isRefreshing = false;

async function refreshAllFeeds(isAutomatic) {
    if (allFeeds.length === 0 || isRefreshing) return;
    isRefreshing = true;
    refreshAllButton.classList.add("spinning");
    refreshAllButton.disabled = true;
    const toast = isAutomatic ? null : showToast(t("refreshingAll"), "loading");
    try {
        const response = await fetch("/articles/refresh-all", { method: "POST" });
        if (response.ok) {
            await loadArticles();
            if (toast) toast.update(t("allRefreshed"), "success");
        } else {
            if (toast) toast.update(t("couldNotRefreshAny"), "error");
            else showToast(t("couldNotRefreshAny"), "error");
        }
    } catch (error) {
        if (toast) toast.update(t("couldNotRefreshAny"), "error");
        else showToast(t("couldNotRefreshAny"), "error");
    } finally {
        refreshAllButton.classList.remove("spinning");
        refreshAllButton.disabled = false;
        isRefreshing = false;
    }
}

refreshAllButton.addEventListener("click", () => refreshAllFeeds(false));

let autoRefreshTimer = setInterval(() => { if (document.visibilityState === "visible") refreshAllFeeds(true); }, AUTO_REFRESH_INTERVAL_MS);
let tabHiddenAt = null;
document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "hidden") tabHiddenAt = Date.now();
    else if (tabHiddenAt && Date.now() - tabHiddenAt > AUTO_REFRESH_INTERVAL_MS) refreshAllFeeds(true);
});

trendingToggle.addEventListener("click", () => {
    trendingExpanded = !trendingExpanded;
    applyTrendingCollapseState();
});

function applyTrendingCollapseState() {
    trendingTopicsDiv.classList.remove("expanded");
    const firstTopic = trendingTopicsDiv.querySelector(".topic");
    const collapsedHeight = firstTopic ? firstTopic.offsetHeight : TRENDING_COLLAPSED_HEIGHT;
    trendingTopicsDiv.style.setProperty("--trending-collapsed-height", `${collapsedHeight}px`);
    const isOverflowing = trendingTopicsDiv.scrollHeight > collapsedHeight + 4;
    if (!isOverflowing) { trendingToggle.hidden = true; return; }
    trendingToggle.hidden = false;
    trendingTopicsDiv.classList.toggle("expanded", trendingExpanded);
    trendingToggle.classList.toggle("expanded", trendingExpanded);
    trendingToggle.querySelector(".feeds-toggle-text").textContent = trendingExpanded ? t("showLess") : t("showMore");
}
if (document.fonts && document.fonts.ready) document.fonts.ready.then(() => { applyTrendingCollapseState(); applySummaryCollapseState(); applyFeedCategoryFiltersCollapseState(); });
let filtersResizeTimer = null;
window.addEventListener("resize", () => {
    clearTimeout(filtersResizeTimer);
    filtersResizeTimer = setTimeout(() => { applyTrendingCollapseState(); applySummaryCollapseState(); applyFeedCategoryFiltersCollapseState(); }, 150);
});

// Feed-level categorization: look at this feed's own articles and find its dominant
// category (a solid majority, not just a plurality) - articles are already categorized
// individually via categorizeArticle(). Feeds that don't clearly lean one way (e.g. a
// general-purpose outlet covering everything) fall back to "general" rather than being
// forced into a topic they don't really belong to.
const FEED_CATEGORY_DOMINANCE_THRESHOLD = 0.5;

// A feed's category is only ever used as an optional filter chip now - not a forced grouping.
// That matters because the classifier is a keyword heuristic and will sometimes get a feed
// wrong (e.g. a podcast that happens to mention "app" a lot reads as Technology); as a filter,
// an occasional miss just means one feed doesn't show up under a chip you clicked - as a
// forced grouping, the same miss meant the feed looked structurally misfiled. Same underlying
// signal, much lower cost when it's wrong.
function computeFeedCategory(feedTitle) {
    const feedArticles = allArticles.filter(a => a.feedTitle === feedTitle);
    if (feedArticles.length === 0) return "general";
    const counts = new Map();
    for (const a of feedArticles) {
        const cat = categorizeArticle(a);
        counts.set(cat, (counts.get(cat) || 0) + 1);
    }
    let bestCategory = "general";
    let bestCount = 0;
    for (const [cat, count] of counts) {
        if (count > bestCount) { bestCount = count; bestCategory = cat; }
    }
    return (bestCount / feedArticles.length) >= FEED_CATEGORY_DOMINANCE_THRESHOLD ? bestCategory : "general";
}

function buildFeedRowElement(feed) {
    const feedElement = document.createElement("div");
    feedElement.className = "feed-row" + (activeFeedFilters.has(feed.title) ? " active" : "");
    feedElement.dataset.feedTitle = feed.title;
    feedElement.style.cursor = "pointer";
    const identity = document.createElement("div");
    identity.className = "feed-identity";
    const swatch = document.createElement("span");
    swatch.className = "feed-swatch";
    swatch.style.background = feedColorVar(feed.title);
    const title = document.createElement("span");
    title.className = "feed-title";
    title.textContent = feed.title;
    identity.appendChild(swatch);
    identity.appendChild(title);

    // Listen on the whole row (not just the text) so clicking anywhere in the row's
    // padding/frame toggles the filter too.
    feedElement.addEventListener("click", () => {
        if (activeFeedFilters.has(feed.title)) activeFeedFilters.delete(feed.title);
        else activeFeedFilters.add(feed.title);
        updateFeedRowActiveStates();
        exitAnySpecialView();
        closeFeedDrawerIfOpen();
    });

    const refreshButton = document.createElement("button");
    refreshButton.textContent = "\u21BB";
    refreshButton.className = "refresh-btn";
    refreshButton.title = t("refreshFeed");
    refreshButton.addEventListener("click", async (e) => {
        e.stopPropagation();
        refreshButton.classList.add("spinning");
        const toast = showToast(tf("refreshingFeed", feed.title), "loading");
        const response = await fetch(`/feeds/${feed.id}/refresh`, { method: "POST" });
        refreshButton.classList.remove("spinning");
        if (response.ok) { await loadArticles(); toast.update(tf("feedRefreshed", feed.title), "success"); }
        else toast.update(tf("couldNotRefreshFeed", feed.title), "error");
    });

    const copyLinkButton = document.createElement("button");
    copyLinkButton.type = "button";
    copyLinkButton.textContent = "\uD83D\uDD17";
    copyLinkButton.className = "copy-link-btn";
    copyLinkButton.title = t("copyFeedLink");
    copyLinkButton.addEventListener("click", async (e) => {
        e.stopPropagation();
        await navigator.clipboard.writeText(feed.url);
        copyLinkButton.textContent = "\u2713";
        copyLinkButton.classList.add("copied");
        showToast(t("linkCopied"), "success", 2000);
        setTimeout(() => { copyLinkButton.textContent = "\uD83D\uDD17"; copyLinkButton.classList.remove("copied"); }, 1500);
    });

    const deleteButton = document.createElement("button");
    deleteButton.textContent = "\u2715";
    deleteButton.className = "delete-btn";
    deleteButton.title = t("deleteFeed");
    deleteButton.addEventListener("click", async (e) => {
        e.stopPropagation();
        if (!currentUser) {
            showToast(t("signInToManageFeeds"), "info", 5000);
            return;
        }
        allFeeds = allFeeds.filter(f => f.id !== feed.id);
        activeFeedFilters.delete(feed.title);
        renderFeedList();
        updateFeedCounters(allFeeds.length);
        const response = await fetch(`/feeds/${feed.id}`, { method: "DELETE" });
        if (response.ok) {
            await loadArticles();
            showToast(tf("feedDeleted", feed.title), "success", 2500);
        } else {
            await loadFeeds();
            showToast(tf("couldNotDeleteFeed", feed.title), "error");
        }
    });

    const actions = document.createElement("div");
    actions.className = "feed-actions";
    actions.appendChild(refreshButton);
    actions.appendChild(copyLinkButton);
    actions.appendChild(deleteButton);
    feedElement.appendChild(identity);
    feedElement.appendChild(actions);
    return feedElement;
}

const FEED_LIST_ROW_CAP = 30; // even in a single flat list, thousands of rows shouldn't all render at once
let activeFeedCategoryFilter = null; // null = no category filter applied ("All")
let feedListShowAll = false; // whether "Show N more" has been clicked for the current filter/search

function renderFeedCategoryChips(feeds) {
    feedCategoryFiltersDiv.innerHTML = "";
    if (feeds.length === 0) {
        feedCategoryToggle.hidden = true;
        return;
    }

    const presentCategories = new Set(feeds.map(f => computeFeedCategory(f.title)));
    if (presentCategories.size <= 1) {
        feedCategoryToggle.hidden = true;
        return; // nothing to narrow down if everything's one category
    }

    const allChip = document.createElement("button");
    allChip.className = "feed-category-chip" + (activeFeedCategoryFilter === null ? " active" : "");
    allChip.textContent = t("allCategories");
    allChip.addEventListener("click", () => {
        activeFeedCategoryFilter = null;
        feedListShowAll = false;
        renderFeedList();
    });
    feedCategoryFiltersDiv.appendChild(allChip);

    for (const cat of [...CATEGORY_ORDER, "general"]) {
        if (!presentCategories.has(cat)) continue;
        const chip = document.createElement("button");
        chip.className = "feed-category-chip" + (activeFeedCategoryFilter === cat ? " active" : "");
        chip.textContent = t("cat_" + cat);
        chip.addEventListener("click", () => {
            activeFeedCategoryFilter = activeFeedCategoryFilter === cat ? null : cat;
            feedListShowAll = false;
            renderFeedList();
        });
        feedCategoryFiltersDiv.appendChild(chip);
    }

    applyFeedCategoryFiltersCollapseState();
}

function applyFeedCategoryFiltersCollapseState() {
    feedCategoryFiltersDiv.classList.remove("expanded");
    const firstChip = feedCategoryFiltersDiv.querySelector(".feed-category-chip");
    const collapsedHeight = firstChip ? firstChip.offsetHeight : FEED_CATEGORY_COLLAPSED_HEIGHT;
    feedCategoryFiltersDiv.style.setProperty("--feed-category-collapsed-height", `${collapsedHeight}px`);
    const isOverflowing = feedCategoryFiltersDiv.scrollHeight > collapsedHeight + 4;
    if (!isOverflowing) { feedCategoryToggle.hidden = true; return; }
    feedCategoryToggle.hidden = false;
    feedCategoryFiltersDiv.classList.toggle("expanded", feedCategoryFiltersExpanded);
    feedCategoryToggle.classList.toggle("expanded", feedCategoryFiltersExpanded);
    feedCategoryToggle.querySelector(".feeds-toggle-text").textContent = feedCategoryFiltersExpanded ? t("showLess") : t("showMore");
}

feedCategoryToggle.addEventListener("click", () => {
    feedCategoryFiltersExpanded = !feedCategoryFiltersExpanded;
    applyFeedCategoryFiltersCollapseState();
});

function renderFeedList() {
    feedsDiv.innerHTML = "";
    if (allFeeds.length === 0) {
        feedCategoryFiltersDiv.innerHTML = "";
        const empty = document.createElement("p");
        empty.className = "feed-empty";
        empty.textContent = t("noSubscriptions");
        feedsDiv.appendChild(empty);
        updateFeedCounters(0);
        return;
    }

    const allFeedsRow = document.createElement("div");
    allFeedsRow.className = "feed-row feed-row-all" + (activeFeedFilters.size === 0 ? " active" : "");
    const allFeedsIdentity = document.createElement("div");
    allFeedsIdentity.className = "feed-identity";
    allFeedsIdentity.style.cursor = "pointer";
    const allFeedsSwatch = document.createElement("span");
    allFeedsSwatch.className = "feed-swatch";
    allFeedsSwatch.style.background = "var(--primary)";
    const allFeedsLabel = document.createElement("span");
    allFeedsLabel.className = "feed-title";
    allFeedsLabel.textContent = t("allFeeds");
    allFeedsIdentity.appendChild(allFeedsSwatch);
    allFeedsIdentity.appendChild(allFeedsLabel);
    allFeedsRow.appendChild(allFeedsIdentity);
    allFeedsRow.addEventListener("click", () => {
        activeFeedFilters.clear();
        updateFeedRowActiveStates();
        exitAnySpecialView();
        closeFeedDrawerIfOpen();
    });
    feedsDiv.appendChild(allFeedsRow);

    renderFeedCategoryChips(allFeeds);

    let feeds = allFeeds;
    if (feedSearchTerm) feeds = feeds.filter(f => f.title.toLowerCase().includes(feedSearchTerm));
    if (activeFeedCategoryFilter) feeds = feeds.filter(f => computeFeedCategory(f.title) === activeFeedCategoryFilter);

    if (feeds.length === 0) {
        const empty = document.createElement("p");
        empty.className = "feed-search-empty";
        empty.textContent = t("noFeedsMatchSearch");
        feedsDiv.appendChild(empty);
        updateFeedCounters(allFeeds.length);
        return;
    }

    const showFull = feedListShowAll || feeds.length <= FEED_LIST_ROW_CAP;
    const visibleFeeds = showFull ? feeds : feeds.slice(0, FEED_LIST_ROW_CAP);
    for (const feed of visibleFeeds) feedsDiv.appendChild(buildFeedRowElement(feed));

    if (!showFull) {
        const showMoreBtn = document.createElement("button");
        showMoreBtn.className = "feed-list-show-more";
        showMoreBtn.textContent = tf("showMoreFeeds", feeds.length - FEED_LIST_ROW_CAP);
        showMoreBtn.addEventListener("click", () => {
            feedListShowAll = true;
            renderFeedList();
        });
        feedsDiv.appendChild(showMoreBtn);
    }

    updateFeedCounters(allFeeds.length);
}

feedSearchInput.addEventListener("input", () => {
    feedSearchTerm = feedSearchInput.value.toLowerCase().trim();
    feedListShowAll = false;
    renderFeedList();
});

async function loadFeeds() {
    try {
        const response = await fetch("/feeds");
        allFeeds = await response.json();
        if (guestFeed && !allFeeds.some(f => f.id === guestFeed.id)) {
            allFeeds.push(guestFeed);
        }
    } catch { }
    renderFeedList();
}

function updateFeedRowActiveStates() {
    const allRow = feedsDiv.querySelector(".feed-row-all");
    if (allRow) allRow.classList.toggle("active", activeFeedFilters.size === 0);
    feedsDiv.querySelectorAll(".feed-row[data-feed-title]").forEach(row => {
        row.classList.toggle("active", activeFeedFilters.has(row.dataset.feedTitle));
    });
}

function updateFeedCounters(count) {
    feedCounter.textContent = count;
    feedCountDisplay.textContent = count + " " + (count === 1 ? t("feed") : t("feeds"));
}

// Mobile drawer: the sidebar is permanently visible on desktop, and only becomes an
// overlay drawer under the narrow-screen breakpoint (see CSS). These functions are safe
// to call regardless of screen size - on desktop the drawer classes simply have no effect.
function openFeedDrawer() {
    document.querySelector(".sidebar").classList.add("drawer-open");
    feedDrawerBackdrop.classList.add("visible");
}

function closeFeedDrawer() {
    document.querySelector(".sidebar").classList.remove("drawer-open");
    feedDrawerBackdrop.classList.remove("visible");
}

function closeFeedDrawerIfOpen() {
    if (document.querySelector(".sidebar").classList.contains("drawer-open")) closeFeedDrawer();
}

feedDrawerToggle.addEventListener("click", openFeedDrawer);
feedDrawerClose.addEventListener("click", closeFeedDrawer);
feedDrawerBackdrop.addEventListener("click", closeFeedDrawer);

async function loadArticles() {
    try {
        let url = "/articles";
        if (guestFeed) url += "?extraFeedIds=" + guestFeed.id;
        const response = await fetch(url);
        allArticles = await response.json();
    } catch {
        return;
    }
    if (allArticles.length === 0 && allFeeds.length > 0 && !isRefreshing) {
        renderTrendingTopics();
        renderArticles();
        await refreshAllFeeds(true);
        return;
    }
    renderTrendingTopics();
    renderFeedList();
    if (!showingPlaylist && !showingSaved && !showingHistory && !AUTH_ROUTES.includes(window.location.pathname)) renderArticles();
}

// Feed filtering (sidebar, multi-select) and category filtering (top bar / trending topics)
// are independent and combine with AND logic.
function matchFilters(articles) {
    return articles.filter(a => {
        if (activeFeedFilters.size > 0 && !activeFeedFilters.has(a.feedTitle)) return false;
        if (activeCategoryFilters.size > 0 && !activeCategoryFilters.has(categorizeArticle(a))) return false;
        return true;
    });
}

function renderArticles() {
    viewRenderToken++;
    articlesDiv.innerHTML = "";
    const filtered = matchFilters(allArticles).filter(a => a.title.toLowerCase().includes(searchTerm));
    articleCountSpan.textContent = filtered.length > 0
        ? filtered.length + " " + (filtered.length === 1 ? t("article") : t("articles")) : "";
    if (filtered.length === 0) {
        const empty = document.createElement("p");
        empty.className = "articles-empty";
        empty.textContent = t("noArticles");
        articlesDiv.appendChild(empty);
        return;
    }
    const totalPages = Math.max(1, Math.ceil(filtered.length / ARTICLES_PER_PAGE));
    if (currentPage > totalPages) currentPage = totalPages;
    const startIndex = (currentPage - 1) * ARTICLES_PER_PAGE;
    const visibleArticles = filtered.slice(startIndex, startIndex + ARTICLES_PER_PAGE);
    for (const article of visibleArticles) articlesDiv.appendChild(buildArticleCard(article));
    if (totalPages > 1) renderPagination(totalPages);
}

function renderPagination(totalPages) {
    const bar = document.createElement("div");
    bar.className = "pagination-bar";
    const prevButton = document.createElement("button");
    prevButton.type = "button";
    prevButton.className = "page-btn";
    prevButton.textContent = t("previous");
    prevButton.disabled = currentPage === 1;
    prevButton.addEventListener("click", () => { currentPage -= 1; sessionStorage.setItem("currentPage", currentPage); renderArticles(); window.scrollTo({ top: 0, behavior: "smooth" }); });
    const pageInfo = document.createElement("span");
    pageInfo.className = "page-info";
    pageInfo.textContent = t("page") + " " + currentPage + " " + t("of") + " " + totalPages;
    const nextButton = document.createElement("button");
    nextButton.type = "button";
    nextButton.className = "page-btn";
    nextButton.textContent = t("next");
    nextButton.disabled = currentPage === totalPages;
    nextButton.addEventListener("click", () => { currentPage += 1; sessionStorage.setItem("currentPage", currentPage); renderArticles(); window.scrollTo({ top: 0, behavior: "smooth" }); });
    const jumpTo = document.createElement("div");
    jumpTo.className = "jump-to";
    const jumpLabel = document.createElement("span");
    jumpLabel.textContent = t("jumpTo");
    const jumpInput = document.createElement("input");
    jumpInput.type = "number";
    jumpInput.min = "1";
    jumpInput.max = String(totalPages);
    jumpInput.placeholder = "#";
    jumpInput.setAttribute("aria-label", "Jump to page");
    function goToTypedPage() {
        const target = parseInt(jumpInput.value, 10);
        if (!Number.isNaN(target) && target >= 1 && target <= totalPages) {
            currentPage = target;
            sessionStorage.setItem("currentPage", currentPage);
            renderArticles();
            window.scrollTo({ top: 0, behavior: "smooth" });
        }
        jumpInput.value = "";
    }
    jumpInput.addEventListener("keydown", (e) => { if (e.key === "Enter") goToTypedPage(); });
    jumpInput.addEventListener("blur", () => { if (jumpInput.value !== "") goToTypedPage(); });
    jumpTo.appendChild(jumpLabel);
    jumpTo.appendChild(jumpInput);
    bar.appendChild(prevButton);
    bar.appendChild(pageInfo);
    bar.appendChild(nextButton);
    bar.appendChild(jumpTo);
    articlesDiv.appendChild(bar);
}

function buildArticleCard(article) {
    const articleElement = document.createElement("div");
    articleElement.className = "article-card" + (viewedArticleIds.has(article.id) ? " read" : "");
    articleElement.style.setProperty("--card-accent", feedColorVar(article.feedTitle));
    articleElement.dir = article.isRtl ? "rtl" : "ltr";
    const meta = document.createElement("div");
    meta.className = "article-meta";
    meta.textContent = article.feedTitle;
    const title = document.createElement("h3");
    title.textContent = article.title;
    articleElement.appendChild(meta);
    articleElement.appendChild(title);
    if (article.imageUrl) {
        const img = document.createElement("img");
        img.className = "article-img";
        img.src = article.imageUrl;
        img.alt = article.title;
        img.loading = "lazy";
        img.onerror = function () { this.remove(); };
        articleElement.appendChild(img);
    }
    if (article.enclosureUrl && article.enclosureType && article.enclosureType.startsWith("audio/")) {
        const player = document.createElement("audio");
        player.className = "article-audio";
        player.controls = true;
        player.preload = "none";
        const source = document.createElement("source");
        source.src = article.enclosureUrl;
        source.type = article.enclosureType;
        player.appendChild(source);
        // Each <audio> element is otherwise fully independent - the browser has no built-in
        // notion of "only one at a time" across separate elements, so without this, starting
        // a second podcast just plays on top of whatever's already playing. Pausing every
        // other player when this one starts fixes that everywhere this card renders
        // (home feed, playlists), since they all go through this same builder.
        player.addEventListener("play", () => {
            document.querySelectorAll(".article-audio").forEach(other => {
                if (other !== player && !other.paused) other.pause();
            });
        });
        articleElement.appendChild(player);
    }
    const summary = document.createElement("p");
    summary.className = "summary";
    summary.textContent = article.summary;
    articleElement.appendChild(summary);

    // Per-article AI summary: hidden until requested. Note this summarizes the article's own
    // text (title/description) - for podcasts that's the episode description, not a
    // transcription of the actual audio, which would be a much heavier separate feature.
    const summarizeButton = document.createElement("button");
    summarizeButton.type = "button";
    summarizeButton.className = "summarize-btn";
    summarizeButton.innerHTML = `<span class="btn-icon">✨</span><span class="btn-text">${t("summarizeLabel")}</span>`;
    summarizeButton.addEventListener("click", (e) => {
        e.stopPropagation();
        summarizeArticle(article, summarizeButton, aiSummaryBox);
    });
    articleElement.appendChild(summarizeButton);

    const aiSummaryBox = document.createElement("div");
    aiSummaryBox.className = "article-ai-summary hidden";
    articleElement.appendChild(aiSummaryBox);
    const cachedSummary = articleSummaryCache.get(article.id);
    if (cachedSummary) renderArticleSummaryBox(aiSummaryBox, cachedSummary);

    const footer = document.createElement("div");
    footer.className = "article-footer";
    const link = document.createElement("a");
    link.className = "read-link";
    link.textContent = t("readArticle");
    link.href = article.link;
    link.target = "_blank";
    link.rel = "noopener noreferrer";
    link.addEventListener("click", () => {
        // Fire-and-forget - opening in a new tab shouldn't be blocked waiting on this.
        logHistoryView(article);
        if (!viewedArticleIds.has(article.id)) {
            viewedArticleIds.add(article.id);
            articleElement.classList.add("read");
        }
    });

    const copyButton = document.createElement("button");
    copyButton.type = "button";
    copyButton.className = "share-btn icon-only-btn";
    copyButton.innerHTML = `<span class="btn-icon">📋</span>`;
    copyButton.title = t("shareArticle");
    copyButton.addEventListener("click", (e) => { e.stopPropagation(); copyArticleLink(article); });

    const nativeShareButton = document.createElement("button");
    nativeShareButton.type = "button";
    nativeShareButton.className = "native-share-btn icon-only-btn";
    nativeShareButton.innerHTML = `<span class="btn-icon">📤</span>`;
    nativeShareButton.title = t("shareNative");
    const shareData = { title: article.title, text: article.summary };
    if (article.link && /^https?:\/\//i.test(article.link)) shareData.url = article.link;
    // Only shown when the browser actually has a real share sheet to offer - otherwise
    // "Copy" above already covers getting the link out, no point showing a dead button.
    nativeShareButton.hidden = !canActuallyShare(shareData);
    nativeShareButton.addEventListener("click", (e) => {
        e.stopPropagation();
        navigator.share(shareData).catch(err => {
            if (err && err.name === "AbortError") return; // person closed the share sheet themselves - not a failure
            console.error("navigator.share failed:", err);
            showToast(t("noShareTargets"), "error");
        });
    });

    const starButton = document.createElement("button");
    starButton.type = "button";
    starButton.className = "star-btn";
    updateStarButton(starButton, article.id);
    starButton.addEventListener("click", (e) => { e.stopPropagation(); toggleSaved(article, starButton); });

    const bookmarkButton = document.createElement("button");
    bookmarkButton.type = "button";
    bookmarkButton.className = "bookmark-btn";
    updateBookmarkButton(bookmarkButton, article.id);
    bookmarkButton.addEventListener("click", () => togglePlaylist(article, bookmarkButton));

    footer.appendChild(link);
    footer.appendChild(copyButton);
    footer.appendChild(nativeShareButton);
    footer.appendChild(starButton);
    footer.appendChild(bookmarkButton);
    articleElement.appendChild(footer);
    return articleElement;
}

function canActuallyShare(data) {
    if (!navigator.share) return false;
    if (navigator.canShare) return navigator.canShare(data);
    return true;
}

function updateBookmarkButton(button, articleId) {
    const inPlaylist = playlistByArticleId.has(articleId);
    button.classList.toggle("active", inPlaylist);
    button.title = inPlaylist ? t("removeFromPlaylist") : t("addToPlaylist");
    button.innerHTML = `<span class="btn-icon">\uD83D\uDD16</span><span class="btn-text">${inPlaylist ? t("savedLabel") : t("saveLabel")}</span>`;
}

function updateStarButton(button, articleId) {
    const isSaved = savedItemByArticleId.has(articleId);
    button.classList.toggle("active", isSaved);
    button.title = isSaved ? t("unstarArticle") : t("starArticle");
    button.innerHTML = `<span class="btn-icon">${isSaved ? "\u2B50" : "\u2606"}</span><span class="btn-text">${isSaved ? t("starredLabel") : t("starLabel")}</span>`;
}

async function summarizeArticle(article, button, box) {
    const cached = articleSummaryCache.get(article.id);
    if (cached) { renderArticleSummaryBox(box, cached); return; }

    button.disabled = true;
    const originalHtml = button.innerHTML;
    button.innerHTML = `<span class="btn-icon">✨</span><span class="btn-text">${t("summarizing")}</span>`;
    try {
        const response = await fetch(`/articles/${article.id}/summarize`, { method: "POST" });
        updateAiLimitBar(response);
        if (response.ok) {
            const data = await response.json();
            const result = { textEn: data.textEn, textAr: data.textAr };
            articleSummaryCache.set(article.id, result);
            renderArticleSummaryBox(box, result);
        } else {
            if (response.status === 429) {
                showAiLimitReached(response);
            } else {
                const bodyText = await response.text();
                console.error(`Summarize failed: HTTP ${response.status}`, bodyText);
                let detail = null;
                try { const parsed = JSON.parse(bodyText); detail = typeof parsed === "string" ? parsed : parsed?.detail; } catch { }
                if (response.status === 404) showToast(detail || bodyText || t("couldNotSummarize"), "error");
                else showToast(detail || t("couldNotSummarize"), "error");
            }
        }
    } catch (err) {
        console.error("Summarize request failed:", err);
        showToast(t("couldNotSummarize"), "error");
    }
    button.disabled = false;
    button.innerHTML = originalHtml;
}

function renderArticleSummaryBox(box, result) {
    const text = currentLang === "ar" ? (result.textAr || result.textEn) : (result.textEn || result.textAr);
    box.textContent = text;
    box.classList.remove("hidden");
}



function showDialog(html) {
    dialogBox.innerHTML = html;
    dialogOverlay.classList.remove("hidden");
}

function hideDialog() {
    dialogOverlay.classList.add("hidden");
    dialogBox.innerHTML = "";
}

// A styled replacement for the browser's native confirm() - onConfirm runs only if the
// person actually clicks the confirm button.
function showConfirmDialog(message, onConfirm) {
    showDialog(`
        <p class="dialog-confirm-message">${escHtml(message)}</p>
        <div class="dialog-confirm-actions">
            <button id="dialogConfirmCancelBtn" class="dialog-cancel-btn">${t("cancel")}</button>
            <button id="dialogConfirmOkBtn" class="dialog-confirm-btn">${t("confirm")}</button>
        </div>`);
    document.getElementById("dialogConfirmCancelBtn").addEventListener("click", hideDialog);
    document.getElementById("dialogConfirmOkBtn").addEventListener("click", () => {
        hideDialog();
        onConfirm();
    });
}

dialogOverlay.addEventListener("click", (e) => { if (e.target === dialogOverlay) hideDialog(); });

async function togglePlaylist(article, button) {
    if (!requireAuth()) return;
    if (playlistByArticleId.has(article.id)) {
        const info = playlistByArticleId.get(article.id);
        await removeFromPlaylist(info.playlistItemId, article, button);
        return;
    }
    await loadPlaylists();
    if (playlists.length === 0) {
        // bootstrap a default playlist so there's always at least one option in the dialog below
        const pl = await createPlaylist("My Playlist");
        if (pl) playlists = [pl];
    }
    let html = `<h3>${t("addToPlaylistDialog")}</h3><div class="dialog-playlist-list">`;
    for (const pl of playlists) {
        html += `<button class="dialog-playlist-btn" data-plid="${pl.id}">${escHtml(pl.name)}</button>`;
    }
    html += `</div><div class="dialog-new-pl"><input id="dialogNewPlName" placeholder="${t("newPlaylistName")}"><button id="dialogCreatePlBtn">${t("create")}</button></div>
        <button id="dialogCancelBtn" class="dialog-cancel-btn">${t("backToArticles")}</button>`;
    showDialog(html);
    document.getElementById("dialogCancelBtn").addEventListener("click", hideDialog);
    document.querySelectorAll(".dialog-playlist-btn").forEach(btn => {
        btn.addEventListener("click", async () => {
            hideDialog();
            await addToPlaylistDirect(btn.dataset.plid, article, button);
        });
    });
    document.getElementById("dialogCreatePlBtn").addEventListener("click", async () => {
        const name = document.getElementById("dialogNewPlName").value.trim();
        if (!name) return;
        const pl = await createPlaylist(name);
        if (pl) { hideDialog(); await addToPlaylistDirect(pl.id, article, button); }
    });
}

async function addToPlaylistDirect(playlistId, article, button) {
    button.disabled = true;
    try {
        const response = await fetch(`/playlist/${playlistId}/${article.id}`, { method: "POST" });
        if (response.ok) {
            const entry = await response.json();
            playlistByArticleId.set(article.id, { playlistItemId: entry.id, playlistId: playlistId });
            updatePlaylistBadge();
            showToast(t("addedToPlaylistToast"), "success", 2500);
            if (showingPlaylist) renderPlaylistView();
            else { button.disabled = false; updateBookmarkButton(button, article.id); }
        }
    } catch { }
}

async function removeFromPlaylist(playlistItemId, article, button) {
    button.disabled = true;
    try {
        const response = await fetch(`/playlist/${playlistItemId}`, { method: "DELETE" });
        if (response.ok) {
            playlistByArticleId.delete(article.id);
            updatePlaylistBadge();
            showToast(t("removedFromPlaylistToast"), "success", 2500);
            if (showingPlaylist) renderPlaylistView();
            else { button.disabled = false; updateBookmarkButton(button, article.id); }
        }
    } catch { }
}

async function createPlaylist(name) {
    const response = await fetch("/playlists", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name })
    });
    if (response.ok) return await response.json();
    return null;
}

let playlistItemCounts = new Map(); // playlistId -> item count, used to default to a non-empty playlist and label tabs

async function loadPlaylists() {
    if (!currentUser) { playlists = []; playlistByArticleId = new Map(); playlistItemCounts = new Map(); updatePlaylistBadge(); return; }
    try {
        const response = await fetch("/playlists");
        playlists = await response.json();
        const map = new Map();
        const counts = new Map();
        for (const pl of playlists) {
            const itemsRes = await fetch(`/playlist/${pl.id}/items`);
            const items = await itemsRes.json();
            counts.set(pl.id, items.length);
            for (const item of items) map.set(item.article.id, { playlistItemId: item.id, playlistId: pl.id });
        }
        playlistByArticleId = map;
        playlistItemCounts = counts;
        updatePlaylistBadge();
    } catch { }
}

function updatePlaylistBadge() {
    const count = playlistByArticleId.size;
    playlistBadge.textContent = count;
    playlistBadge.hidden = count === 0;
    playlistToggle.classList.toggle("active", showingPlaylist);
    document.getElementById("fabPlaylistBadge").textContent = count;
    document.getElementById("fabPlaylistBadge").hidden = count === 0;
    document.getElementById("fabPlaylist").classList.toggle("active", showingPlaylist);
}

/* ===== AUTH ===== */

async function initAuth() {
    try {
        const response = await fetch("/auth/me");
        currentUser = response.ok ? await response.json() : null;
        if (!response.ok) console.log("[AUTH] initAuth: /auth/me returned", response.status);
    } catch (e) {
        currentUser = null;
        console.log("[AUTH] initAuth: fetch failed", e);
    }
    console.log("[AUTH] initAuth complete, currentUser:", currentUser);
    initAiLimitBar();
    updateAccountIcon();
}

function initAiLimitBar() {
    fetch("/ai-limit-status").then(r => r.json()).then(d => {
        if (d.remaining <= 0) {
            aiLimitLabel.textContent = "AI 0/" + d.limit;
            aiLimitFill.style.width = "0%";
        } else {
            aiLimitLabel.textContent = "AI " + d.remaining + "/" + d.limit;
            aiLimitFill.style.width = (d.remaining / d.limit * 100) + "%";
        }
        aiLimitBar.classList.toggle("low", d.remaining <= 3);
        aiLimitBar.classList.toggle("empty", d.remaining <= 0);
        aiLimitBar.style.display = "flex";
    }).catch(() => {});
}

function updateAccountIcon() {
    accountToggle.textContent = currentUser ? currentUser.email.charAt(0).toUpperCase() : "\uD83D\uDC64";
    accountToggle.classList.toggle("signed-in", !!currentUser);
    accountToggle.title = currentUser ? currentUser.email : t("accountMenu");

    // The small profile icon alone wasn't an obvious enough entry/exit point for signing
    // in or out, so this same slot (Refresh shrunk to make room underneath it) always shows
    // a clearly-labeled button - "Sign in" when signed out, "Log out" when signed in - so
    // there's never a question of "where do I even do that".
    headerSignInButton.textContent = currentUser ? t("signOut") : t("signIn");
    headerSignInButton.classList.toggle("logout", !!currentUser);
}

headerSignInButton.addEventListener("click", () => {
    if (currentUser) handleSignOut();
    else navigateTo(ROUTE_LOGIN);
});
accountToggle.addEventListener("click", () => {
    if (currentUser) openAccountDialog();
    else navigateTo(ROUTE_LOGIN);
});

// The single gate every personal feature (Save, Playlist, History) runs through - if signed
// in, does nothing and lets the caller proceed; if not, tells the person why and opens the
// dialog to fix it, then stops the caller from proceeding.
function requireAuth() {
    if (currentUser) return true;
    showToast(t("signInRequired"), "info", 4000);
    return false;
}

// Reuses the app's existing dialog-overlay (already proven reliable elsewhere - the
// add-to-playlist and confirm-clear-history dialogs use the exact same thing) instead of a
// custom-positioned panel, which kept breaking in new ways across desktop/mobile.
function openAccountDialog({ welcome = false } = {}) {
    renderAccountDialog(welcome);
}

function renderAccountDialog(welcome = false) {
    if (currentUser) {
        showDialog(`
            <button id="dialogCancelBtn" class="dialog-close-x" aria-label="Close">✕</button>
            <p class="account-email">${escHtml(currentUser.email)}</p>
            <button id="signOutBtn" class="account-action-btn danger">${t("signOut")}</button>
            <button id="deleteAccountBtn" class="account-action-btn danger" style="margin-top:4px;">${t("deleteAccount")}</button>`);
        document.getElementById("dialogCancelBtn").addEventListener("click", hideDialog);
        document.getElementById("signOutBtn").addEventListener("click", handleSignOut);
        document.getElementById("deleteAccountBtn").addEventListener("click", () => {
            showDialog(`
                <p>${t("confirmDeleteAccount")}</p>
                <div class="dialog-buttons">
                    <button id="confirmDeleteBtn" class="account-action-btn danger">${t("deleteAccount")}</button>
                    <button id="cancelDeleteBtn" class="account-action-btn">${t("cancel")}</button>
                </div>`);
            document.getElementById("confirmDeleteBtn").addEventListener("click", handleDeleteAccount);
            document.getElementById("cancelDeleteBtn").addEventListener("click", () => renderAccountDialog());
        });
        return;
    }

    navigateTo(ROUTE_LOGIN);
}

async function handleSignOut() {
    console.log("[AUTH] handleSignOut called");
    await fetch("/auth/logout", { method: "POST" });
    currentUser = null;
    guestFeed = null;
    initAiLimitBar();
    updateAccountIcon();
    hideDialog();
    showToast(t("signedOutToast"), "success", 2000);
    anonChatHistory = [];
    if (!chatPanel.classList.contains("hidden")) renderChatMessages(anonChatHistory);
    await Promise.all([loadFeeds(), loadArticles(), loadPlaylists(), loadSaved(), loadHistoryIds()]);
    navigateTo(ROUTE_HOME);
}

async function handleDeleteAccount() {
    const response = await fetch("/auth/account", { method: "DELETE" });
    if (response.ok) {
        currentUser = null;
        guestFeed = null;
        updateAccountIcon();
        hideDialog();
        showToast("Account deleted permanently", "success", 3000);
        anonChatHistory = [];
        if (!chatPanel.classList.contains("hidden")) renderChatMessages(anonChatHistory);
        await Promise.all([loadFeeds(), loadArticles(), loadPlaylists(), loadSaved(), loadHistoryIds()]);
        navigateTo(ROUTE_HOME);
    } else {
        showToast("Could not delete account", "error");
    }
}

/* ===== SAVED ARTICLES ===== */

async function loadSaved() {
    if (!currentUser) { savedItemByArticleId = new Map(); updateSavedBadge(); return; }
    try {
        const response = await fetch("/saved");
        const items = await response.json();
        savedItemByArticleId = new Map(items.map(item => [item.articleId, item.id]));
        updateSavedBadge();
    } catch { }
}

function updateSavedBadge() {
    const count = savedItemByArticleId.size;
    savedBadge.textContent = count;
    savedBadge.hidden = count === 0;
    savedToggle.classList.toggle("active", showingSaved);
    document.getElementById("fabSavedBadge").textContent = count;
    document.getElementById("fabSavedBadge").hidden = count === 0;
    document.getElementById("fabSaved").classList.toggle("active", showingSaved);
}

async function toggleSaved(article, button) {
    if (!requireAuth()) return;
    button.disabled = true;
    try {
        if (savedItemByArticleId.has(article.id)) {
            const savedItemId = savedItemByArticleId.get(article.id);
            const response = await fetch(`/saved/${savedItemId}`, { method: "DELETE" });
            if (response.ok) {
                savedItemByArticleId.delete(article.id);
                updateSavedBadge();
                showToast(t("removedFromSaved"), "success", 2000);
                if (showingSaved) renderSavedView(); else updateStarButton(button, article.id);
            } else {
                console.error(`Remove-from-saved failed: HTTP ${response.status}`, await response.text());
                showToast(response.status === 404 ? "Saved endpoint not found (is the backend deployed?)" : t("couldNotSaveArticle"), "error");
            }
        } else {
            const response = await fetch(`/saved/${article.id}`, { method: "POST" });
            if (response.ok) {
                const entry = await response.json();
                savedItemByArticleId.set(article.id, entry.id);
                updateSavedBadge();
                showToast(t("addedToSaved"), "success", 2000);
                updateStarButton(button, article.id);
            } else {
                console.error(`Save failed: HTTP ${response.status}`, await response.text());
                showToast(response.status === 404 ? "Saved endpoint not found (is the backend deployed?)" : t("couldNotSaveArticle"), "error");
            }
        }
    } catch (err) {
        console.error("Save request failed:", err);
        showToast(t("couldNotSummarize"), "error");
    }
    button.disabled = false;
}

function exitSavedView() {
    showingSaved = false;
    savedToggle.classList.remove("active");
    navigateTo(ROUTE_HOME);
}

async function enterSavedView() {
    navigateTo(ROUTE_FAVORITES);
}

savedToggle.addEventListener("click", () => {
    if (showingSaved) exitSavedView();
    else enterSavedView();
});

async function renderSavedView() {
    const renderToken = ++viewRenderToken;
    articlesDiv.innerHTML = "";
    const banner = document.createElement("div");
    banner.className = "playlist-banner centered-banner";
    const backButton = document.createElement("button");
    backButton.className = "playlist-back-btn";
    backButton.textContent = `\u2190 ${t("backToArticlesShort")}`;
    backButton.addEventListener("click", () => exitSavedView());
    const heading = document.createElement("h2");
    heading.textContent = t("savedHeading");
    banner.appendChild(backButton);
    banner.appendChild(heading);
    articlesDiv.appendChild(banner);

    if (!currentUser) {
        renderGuestFeatureDemo("savedDemoTitle", "savedDemoDesc", [
            { title: t("demoArticleTitle1"), meta: t("demoFeedName"), note: t("savedDemoNote") }
        ]);
        return;
    }

    try {
        const response = await fetch("/saved");
        const items = await response.json();
        if (renderToken !== viewRenderToken) return;
        if (items.length === 0) {
            const empty = document.createElement("p");
            empty.className = "articles-empty";
            empty.textContent = t("emptySaved");
            articlesDiv.appendChild(empty);
            return;
        }
        const grid = document.createElement("div");
        grid.className = "playlist-grid";
        for (const item of items) grid.appendChild(buildArticleCard(item.article));
        articlesDiv.appendChild(grid);
    } catch { }
}

// Shared by Saved/History/Playlist: a guest can visit the page and see what it's for and
// what it'll look like, without any real (nonexistent, since they're not signed in) data -
// clearly labeled as an example, never mistakeable for real content.
function renderGuestFeatureDemo(titleKey, descKey, demoCards) {
    const intro = document.createElement("div");
    intro.className = "guest-demo-intro";
    const title = document.createElement("h3");
    title.textContent = t(titleKey);
    const desc = document.createElement("p");
    desc.textContent = t(descKey);
    const ctaBtn = document.createElement("button");
    ctaBtn.className = "account-action-btn";
    ctaBtn.textContent = t("signIn");
    ctaBtn.addEventListener("click", () => navigateTo(ROUTE_LOGIN));
    intro.appendChild(title);
    intro.appendChild(desc);
    intro.appendChild(ctaBtn);
    articlesDiv.appendChild(intro);

    const grid = document.createElement("div");
    grid.className = "playlist-grid guest-demo-grid";
    for (const card of demoCards) grid.appendChild(buildDemoCard(card));
    articlesDiv.appendChild(grid);
}

function buildDemoCard({ title, meta, note }) {
    const card = document.createElement("div");
    card.className = "article-card demo-card";
    const badge = document.createElement("span");
    badge.className = "demo-badge";
    badge.textContent = t("exampleBadge");
    const metaEl = document.createElement("div");
    metaEl.className = "article-meta";
    metaEl.textContent = meta;
    const h3 = document.createElement("h3");
    h3.textContent = title;
    const p = document.createElement("p");
    p.className = "summary";
    p.textContent = note;
    card.appendChild(badge);
    card.appendChild(metaEl);
    card.appendChild(h3);
    card.appendChild(p);
    return card;
}

/* ===== READING HISTORY ===== */

async function loadHistoryIds() {
    if (!currentUser) { viewedArticleIds = new Set(); return; }
    try {
        const response = await fetch("/history/ids");
        const ids = await response.json();
        viewedArticleIds = new Set(ids);
    } catch { }
}

function logHistoryView(article) {
    if (!currentUser) return; // not signed in - nothing to log, and no naggy prompt needed for an implicit background action
    fetch(`/history/${article.id}`, { method: "POST" }).catch(() => { });
}

function exitHistoryView() {
    showingHistory = false;
    historyToggle.classList.remove("active");
    navigateTo(ROUTE_HOME);
}

function enterHistoryView() {
    navigateTo(ROUTE_HISTORY);
}

historyToggle.addEventListener("click", () => {
    if (showingHistory) exitHistoryView();
    else enterHistoryView();
});

async function renderHistoryView() {
    const renderToken = ++viewRenderToken;
    articlesDiv.innerHTML = "";
    const banner = document.createElement("div");
    banner.className = "playlist-banner centered-banner";
    const backButton = document.createElement("button");
    backButton.className = "playlist-back-btn";
    backButton.textContent = `\u2190 ${t("backToArticlesShort")}`;
    backButton.addEventListener("click", () => exitHistoryView());
    const heading = document.createElement("h2");
    heading.textContent = t("historyHeading");
    banner.appendChild(backButton);
    banner.appendChild(heading);

    if (!currentUser) {
        articlesDiv.appendChild(banner);
        renderGuestFeatureDemo("historyDemoTitle", "historyDemoDesc", [
            { title: t("demoArticleTitle2"), meta: t("demoFeedName"), note: t("historyDemoNote") }
        ]);
        return;
    }

    const clearButton = document.createElement("button");
    clearButton.className = "page-btn";
    clearButton.textContent = t("clearHistory");
    clearButton.style.color = "#ba1a1a";
    clearButton.addEventListener("click", () => {
        showConfirmDialog(t("confirmClearHistory"), async () => {
            await fetch("/history", { method: "DELETE" });
            viewedArticleIds = new Set();
            showToast(t("historyCleared"), "success", 2000);
            renderHistoryView();
        });
    });
    banner.appendChild(clearButton);
    articlesDiv.appendChild(banner);

    try {
        const response = await fetch("/history/items");
        const items = await response.json();
        if (renderToken !== viewRenderToken) return;
        if (items.length === 0) {
            const empty = document.createElement("p");
            empty.className = "articles-empty";
            empty.textContent = t("emptyHistory");
            articlesDiv.appendChild(empty);
            return;
        }
        const grid = document.createElement("div");
        grid.className = "playlist-grid";
        for (const item of items) grid.appendChild(buildArticleCard(item.article));
        articlesDiv.appendChild(grid);
    } catch { }
}

// The filter bar and trending topics belong to the home/articles view. They used to stay
// visible (and clickable) while the playlist view was open, so clicking one would silently
// re-render the articles list underneath the playlist banner without ever updating
// showingPlaylist, the URL hash, or the playlist toggle button - leaving all three out of
// sync. Now playlist mode explicitly hides that chrome, and there's one single place
// (enter/exitPlaylistView) that flips all of the related state together.
function setHomeChromeVisible(visible) {
    const dashboardGrid = document.getElementById("dashboardGrid");
    const trendingCard = document.querySelector(".trending-card");
    if (dashboardGrid) dashboardGrid.style.display = visible ? "" : "none";
    if (trendingCard) trendingCard.style.display = visible ? "" : "none";
}

function exitPlaylistView() {
    showingPlaylist = false;
    playlistToggle.classList.remove("active");
    navigateTo(ROUTE_HOME);
}

async function enterPlaylistView() {
    navigateTo(ROUTE_PLAYLIST);
}

playlistToggle.addEventListener("click", () => {
    if (showingPlaylist) exitPlaylistView();
    else enterPlaylistView();
});

// Exits whichever of playlist/saved/history is currently open (if any) and lands back on the
// home article list - used by anything that "picks" a feed/category from the sidebar or
// filter bar, since those actions only make sense against the home view.
function exitAnySpecialView() {
    navigateTo(ROUTE_HOME);
}

async function renderPlaylistView() {
    const renderToken = ++viewRenderToken;
    articlesDiv.innerHTML = "";

    const banner = document.createElement("div");
    banner.className = "playlist-banner";

    const backButton = document.createElement("button");
    backButton.type = "button";
    backButton.className = "playlist-back-btn";
    backButton.textContent = t("backToArticles");
    backButton.addEventListener("click", () => {
        exitPlaylistView();
    });
    banner.appendChild(backButton);

    if (!currentUser) {
        banner.classList.add("centered-banner");
        const heading = document.createElement("h2");
        heading.textContent = t("playlistTitle");
        banner.appendChild(heading);
        articlesDiv.appendChild(banner);
        renderGuestFeatureDemo("playlistDemoTitle", "playlistDemoDesc", [
            { title: t("demoArticleTitle1"), meta: t("demoFeedName"), note: t("playlistDemoNote") }
        ]);
        return;
    }

    const defaultPlaylistId = (() => {
        // Prefer the first playlist that actually has something in it, rather than blindly
        // defaulting to playlists[0] - that was the source of "badge says 5 but this looks
        // empty": whichever playlist was created first isn't necessarily the one with content.
        const nonEmpty = playlists.find(pl => (playlistItemCounts.get(pl.id) || 0) > 0);
        return (nonEmpty || playlists[0])?.id;
    })();

    const playlistTabs = document.createElement("div");
    playlistTabs.className = "playlist-tabs";
    for (const pl of playlists) {
        const tab = document.createElement("button");
        tab.className = "playlist-tab" + (pl.id === currentPlaylistId || (!currentPlaylistId && defaultPlaylistId === pl.id) ? " active" : "");
        tab.textContent = `${pl.name} (${playlistItemCounts.get(pl.id) || 0})`;
        tab.addEventListener("click", async () => {
            currentPlaylistId = pl.id;
            renderPlaylistView();
        });
        playlistTabs.appendChild(tab);
    }

    banner.appendChild(playlistTabs);

    if (playlists.length > 0) {
        const activePlId = currentPlaylistId || defaultPlaylistId;
        currentPlaylistId = activePlId;

        // Playlist creation happens from the bookmark dialog on the articles page, not here.
        // This page is for viewing/managing an existing playlist: delete it, filter its
        // contents, and grab its RSS feed link.
        const manageRow = document.createElement("div");
        manageRow.className = "playlist-manage-row";

        const deleteBtn = document.createElement("button");
        deleteBtn.className = "page-btn";
        deleteBtn.textContent = t("deletePlaylist");
        deleteBtn.style.color = "#ba1a1a";
        deleteBtn.addEventListener("click", async () => {
            await fetch(`/playlists/${activePlId}`, { method: "DELETE" });
            currentPlaylistId = null;
            await loadPlaylists();
            renderPlaylistView();
        });

        const feedUrlRow = document.createElement("div");
        feedUrlRow.className = "playlist-feed-url-row";
        const feedUrlLabel = document.createElement("span");
        feedUrlLabel.textContent = t("playlistFeedUrlLabel");
        const feedUrlBox = document.createElement("div");
        feedUrlBox.className = "playlist-feed-url-box";
        const feedUrlInput = document.createElement("input");
        feedUrlInput.type = "text";
        feedUrlInput.readOnly = true;
        feedUrlInput.value = `${window.location.origin}/playlist/${activePlId}/feed.xml`;
        const copyButton = document.createElement("button");
        copyButton.type = "button";
        copyButton.className = "page-btn";
        copyButton.textContent = t("copyLink");
        copyButton.addEventListener("click", async () => {
            await navigator.clipboard.writeText(feedUrlInput.value);
            copyButton.textContent = t("linkCopied");
            setTimeout(() => { copyButton.textContent = t("copyLink"); }, 2000);
        });
        feedUrlBox.appendChild(feedUrlInput);
        feedUrlBox.appendChild(copyButton);
        feedUrlRow.appendChild(feedUrlLabel);
        feedUrlRow.appendChild(feedUrlBox);
        manageRow.appendChild(deleteBtn);
        banner.appendChild(manageRow);
        banner.appendChild(feedUrlRow);

        try {
            const itemsRes = await fetch(`/playlist/${activePlId}/items`);
            const items = await itemsRes.json();
            if (renderToken !== viewRenderToken) return;

            const feedTitles = [...new Set(items.map(i => i.article.feedTitle))];
            const categoryCounts = getCategoryCounts(items.map(i => i.article));

            if (feedTitles.length > 0 || categoryCounts.length > 0) {
                const plFilters = document.createElement("div");
                plFilters.className = "playlist-filters";

                if (feedTitles.length > 0) {
                    const feedFilterRow = document.createElement("div");
                    feedFilterRow.className = "playlist-filter-row";
                    const feedFilterLabel = document.createElement("span");
                    feedFilterLabel.className = "playlist-filter-label";
                    feedFilterLabel.textContent = t("filterByFeed") + ": ";
                    feedFilterRow.appendChild(feedFilterLabel);
                    for (const ft of feedTitles) {
                        const fb = document.createElement("button");
                        fb.className = "filter-btn" + (playlistArticleFilter.has(ft) ? " active" : "");
                        fb.textContent = ft;
                        fb.addEventListener("click", () => {
                            if (playlistArticleFilter.has(ft)) playlistArticleFilter.delete(ft);
                            else playlistArticleFilter.add(ft);
                            renderPlaylistView();
                        });
                        feedFilterRow.appendChild(fb);
                    }
                    plFilters.appendChild(feedFilterRow);
                }

                if (categoryCounts.length > 0) {
                    const categoryFilterRow = document.createElement("div");
                    categoryFilterRow.className = "playlist-filter-row";
                    const topicFilterLabel = document.createElement("span");
                    topicFilterLabel.className = "playlist-filter-label";
                    topicFilterLabel.textContent = t("filterByTopic") + ": ";
                    categoryFilterRow.appendChild(topicFilterLabel);
                    for (const [category] of categoryCounts) {
                        const tb = document.createElement("button");
                        tb.className = "filter-btn" + (playlistCategoryFilter.has(category) ? " active" : "");
                        tb.textContent = t("cat_" + category);
                        tb.addEventListener("click", () => {
                            if (playlistCategoryFilter.has(category)) playlistCategoryFilter.delete(category);
                            else playlistCategoryFilter.add(category);
                            renderPlaylistView();
                        });
                        categoryFilterRow.appendChild(tb);
                    }
                    plFilters.appendChild(categoryFilterRow);
                }

                banner.appendChild(plFilters);
            }

            let filtered = items.filter(i => i.article.title.toLowerCase().includes(searchTerm));
            if (playlistArticleFilter.size > 0) filtered = filtered.filter(i => playlistArticleFilter.has(i.article.feedTitle));
            if (playlistCategoryFilter.size > 0) {
                filtered = filtered.filter(i => playlistCategoryFilter.has(categorizeArticle(i.article)));
            }

            articlesDiv.appendChild(banner);
            if (filtered.length === 0) {
                const empty = document.createElement("p");
                empty.className = "articles-empty";
                empty.textContent = t("playlistEmpty");
                articlesDiv.appendChild(empty);
                return;
            }
            const grid = document.createElement("div");
            grid.className = "playlist-grid";
            for (const entry of filtered) grid.appendChild(buildArticleCard(entry.article));
            articlesDiv.appendChild(grid);
        } catch { }
    } else {
        articlesDiv.appendChild(banner);
        const empty = document.createElement("p");
        empty.className = "articles-empty";
        empty.textContent = t("playlistEmpty");
        articlesDiv.appendChild(empty);
    }
}

let lastSummary = null; // cached summary object with both languages, so switching UI language never re-hits the AI API

// The backend now generates and stores BOTH languages in a single AI call, structured as
// category-grouped sections (sectionsEn/sectionsAr) rather than one mixed paragraph.
// Falls back to the old flat text fields for compatibility with an older cached summary.json.
function pickSummarySections(summary) {
    if (!summary) return [];
    const sections = currentLang === "ar" ? summary.sectionsAr : summary.sectionsEn;
    return Array.isArray(sections) ? sections : [];
}

function pickSummaryText(summary) {
    if (!summary) return "";
    if (currentLang === "ar") return summary.textAr || summary.text || "";
    return summary.textEn || summary.text || "";
}

function renderSummaryContent(summary) {
    const sections = pickSummarySections(summary);
    aiSummaryText.innerHTML = "";

    if (sections.length === 0) {
        // Older cached summary with no structured sections yet - fall back to flat text.
        const p = document.createElement("p");
        p.textContent = pickSummaryText(summary) || t("dailySummaryPlaceholder");
        aiSummaryText.appendChild(p);
        applySummaryCollapseState();
        return;
    }

    for (const section of sections) {
        const category = CATEGORY_ORDER.includes(section.category) ? section.category : "general";
        const points = Array.isArray(section.points) ? section.points : [];
        if (points.length === 0) continue;

        const wrap = document.createElement("div");
        wrap.className = "ai-summary-section";

        const title = document.createElement("div");
        title.className = "ai-summary-section-title";
        title.textContent = t("cat_" + category);
        wrap.appendChild(title);

        const ul = document.createElement("ul");
        for (const point of points) {
            const li = document.createElement("li");
            li.textContent = point;
            ul.appendChild(li);
        }
        wrap.appendChild(ul);
        aiSummaryText.appendChild(wrap);
    }
    applySummaryCollapseState();
}

// Plain-text version for the clipboard - not just the rendered DOM's textContent, so the
// copied text keeps clear line breaks and bullet dashes instead of everything run together.
function summaryToPlainText(summary) {
    const sections = pickSummarySections(summary);
    if (sections.length === 0) return pickSummaryText(summary);
    return sections.map(section => {
        const category = CATEGORY_ORDER.includes(section.category) ? section.category : "general";
        const points = Array.isArray(section.points) ? section.points : [];
        return `${t("cat_" + category)}:\n` + points.map(p => `- ${p}`).join("\n");
    }).join("\n\n");
}

function applySummaryCollapseState() {
    aiSummaryText.classList.remove("expanded");
    aiSummaryText.style.setProperty("--summary-collapsed-height", `${SUMMARY_COLLAPSED_HEIGHT}px`);
    const isOverflowing = aiSummaryText.scrollHeight > SUMMARY_COLLAPSED_HEIGHT + 4;
    if (!isOverflowing) { summaryToggle.hidden = true; return; }
    summaryToggle.hidden = false;
    aiSummaryText.classList.toggle("expanded", summaryExpanded);
    summaryToggle.classList.toggle("expanded", summaryExpanded);
    summaryToggle.querySelector(".feeds-toggle-text").textContent = summaryExpanded ? t("showLess") : t("showMore");
}
summaryToggle.addEventListener("click", () => {
    summaryExpanded = !summaryExpanded;
    applySummaryCollapseState();
});

async function loadSummary() {
    try {
        const response = await fetch("/summary");
        const summary = await response.json();
        if (summary.generatedAt && summary.generatedAt !== "0001-01-01T00:00:00") {
            lastSummary = summary;
            renderSummaryContent(summary);
            shareSummaryButton.hidden = false;
        } else if (allFeeds.length > 0) {
            generateSummary(false);
        }
    } catch { }
}

refreshSummaryButton.addEventListener("click", () => generateSummary(true));

async function generateSummary(showToast_) {
    refreshSummaryButton.classList.add("spinning");
    refreshSummaryButton.disabled = true;
    const toast = showToast_ ? showToast(t("summaryGenerating"), "loading") : null;
    try {
        // No ?lang param needed anymore - the backend generates both languages in one call.
        const response = await fetch("/summary/generate", { method: "POST" });
        updateAiLimitBar(response);
        if (response.ok) {
            const summary = await response.json();
            lastSummary = summary;
            renderSummaryContent(summary);
            shareSummaryButton.hidden = false;
            if (toast) toast.update(t("summaryUpdated"), "success");
        } else {
            const problem = await response.json().catch(() => null);
            if (response.status === 429) {
                if (toast) toast.dismiss();
                showAiLimitReached(response);
            } else {
                if (toast) toast.update(problem?.detail || t("summaryFailed"), "error");
            }
        }
    } catch { if (toast) toast.update(t("summaryFailed"), "error"); }
    refreshSummaryButton.classList.remove("spinning");
    refreshSummaryButton.disabled = false;
}

shareSummaryButton.addEventListener("click", () => {
    if (!lastSummary) return;
    const text = summaryToPlainText(lastSummary);
    if (!text) return;
    copySummaryText(t("dailySummaryHeading"), text);
});

function copyArticleLink(article) {
    copyToClipboardWithFallback(article.link || article.title);
}

function copySummaryText(title, text) {
    copyToClipboardWithFallback(`${title}\n\n${text}`);
}

// navigator.clipboard needs a secure context (https, or localhost) - on plain http (e.g. testing
// over the local network on a phone) it's simply undefined, and the old code called
// .writeText() on it anyway with no try/catch, so sharing failed completely silently. This
// falls back to a manual copy, and always tells the user one way or another whether it worked.
function copyToClipboardWithFallback(text) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text)
            .then(() => showToast(t("linkCopied"), "success", 2000))
            .catch(() => showToast(t("shareFailed"), "error"));
        return;
    }
    try {
        const textarea = document.createElement("textarea");
        textarea.value = text;
        textarea.style.position = "fixed";
        textarea.style.opacity = "0";
        document.body.appendChild(textarea);
        textarea.focus();
        textarea.select();
        const ok = document.execCommand("copy");
        document.body.removeChild(textarea);
        if (ok) showToast(t("linkCopied"), "success", 2000);
        else showToast(t("shareFailed"), "error");
    } catch {
        showToast(t("shareFailed"), "error");
    }
}

/* ===== CHAT ===== */

let anonChatHistory = []; // in-memory only for guests - never sent to any storage, so a page refresh naturally clears it

function renderChatMessages(list) {
    chatMessages.innerHTML = "";
    for (const msg of list) renderChatBubble(msg.role, msg.content);
    if (list.length === 0) {
        const welcome = document.createElement("p");
        welcome.className = "chat-welcome";
        welcome.textContent = t("chatWelcome");
        chatMessages.appendChild(welcome);
    }
    chatMessages.scrollTop = chatMessages.scrollHeight;
}

async function loadChatHistory() {
    if (!currentUser) { renderChatMessages(anonChatHistory); return; }
    try {
        const response = await fetch("/chat/messages");
        const messages = await response.json();
        renderChatMessages(messages);
    } catch { }
}

function escapeHtml(str) {
    return str
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

// Inline formatting only - operates on already-escaped text, so none of these patterns can
// introduce real HTML tags from user/AI-controlled text.
function inlineMarkdown(escapedText) {
    let out = escapedText;
    out = out.replace(/`([^`]+)`/g, "<code>$1</code>");
    out = out.replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>");
    out = out.replace(/\*([^*]+)\*/g, "<em>$1</em>");
    out = out.replace(/\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/g, '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>');
    return out;
}

// A small, deliberately limited markdown-to-HTML renderer for chat replies: headings, bold,
// italics, inline code, links, and bullet/numbered lists. Escapes HTML first, then only ever
// builds tags itself (regex replacements above never let raw input become a tag), so this
// can't be turned into an XSS vector regardless of what the text contains.
function renderMarkdownToHtml(raw) {
    const lines = (raw || "").split("\n");
    let html = "";
    let inUl = false, inOl = false;
    const closeLists = () => {
        if (inUl) { html += "</ul>"; inUl = false; }
        if (inOl) { html += "</ol>"; inOl = false; }
    };

    for (const rawLine of lines) {
        const escaped = escapeHtml(rawLine);
        const trimmed = escaped.trim();

        const bulletMatch = trimmed.match(/^[-*]\s+(.*)$/);
        const numberedMatch = trimmed.match(/^\d+\.\s+(.*)$/);
        const headingMatch = trimmed.match(/^#{1,4}\s+(.*)$/);

        if (bulletMatch) {
            if (!inUl) { closeLists(); html += "<ul>"; inUl = true; }
            html += `<li>${inlineMarkdown(bulletMatch[1])}</li>`;
        } else if (numberedMatch) {
            if (!inOl) { closeLists(); html += "<ol>"; inOl = true; }
            html += `<li>${inlineMarkdown(numberedMatch[1])}</li>`;
        } else if (headingMatch) {
            closeLists();
            html += `<p class="chat-md-heading">${inlineMarkdown(headingMatch[1])}</p>`;
        } else if (trimmed === "") {
            closeLists();
        } else {
            closeLists();
            html += `<p>${inlineMarkdown(escaped)}</p>`;
        }
    }
    closeLists();
    return html;
}

function renderChatBubble(role, content) {
    const bubble = document.createElement("div");
    bubble.className = `chat-bubble ${role}`;
    if (role === "assistant") {
        bubble.innerHTML = renderMarkdownToHtml(content);
    } else {
        bubble.textContent = content;
    }
    chatMessages.appendChild(bubble);
}

function toggleChatPanel() {
    if (chatPanel.classList.contains("hidden")) {
        loadChatHistory();
    }
    chatPanel.classList.toggle("hidden");
    floatingActions.classList.toggle("chat-open", !chatPanel.classList.contains("hidden"));
    chatMessages.scrollTop = chatMessages.scrollHeight;
}

chatToggle.addEventListener("click", toggleChatPanel);

chatCloseBtn.addEventListener("click", () => { chatPanel.classList.add("hidden"); floatingActions.classList.remove("chat-open"); });
chatClearBtn.addEventListener("click", async () => {
    if (currentUser) {
        await fetch("/chat/messages", { method: "DELETE" });
    } else {
        anonChatHistory = [];
    }
    renderChatMessages([]);
});

async function sendChatMessage() {
    const message = chatInput.value.trim();
    if (!message) return;
    chatInput.value = "";
    renderChatBubble("user", message);
    chatMessages.scrollTop = chatMessages.scrollHeight;

    const typing = document.createElement("div");
    typing.className = "chat-bubble assistant chat-typing";
    typing.textContent = "...";
    chatMessages.appendChild(typing);

    try {
        const body = currentUser
            ? { message }
            : { message, history: anonChatHistory };
        const response = await fetch("/chat", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body)
        });
        updateAiLimitBar(response);
        typing.remove();
        if (response.ok) {
            const data = await response.json();
            renderChatBubble("assistant", data.reply);
            if (!currentUser) {
                // Nothing persisted server-side for a guest, so this in-memory list is the
                // only place the conversation exists - and it's gone the moment the tab
                // refreshes, since it's just a plain JS variable, not localStorage.
                anonChatHistory.push({ role: "user", content: message });
                anonChatHistory.push({ role: "assistant", content: data.reply });
            }
        } else {
            if (response.status === 429) {
                showAiLimitReached(response);
                renderChatBubble("assistant", t("rateLimited"));
            } else {
                renderChatBubble("assistant", t("summaryFailed"));
            }
        }
    } catch {
        typing.remove();
        renderChatBubble("assistant", t("summaryFailed"));
    }
    chatMessages.scrollTop = chatMessages.scrollHeight;
}

chatSendBtn.addEventListener("click", sendChatMessage);
chatInput.addEventListener("keydown", (e) => { if (e.key === "Enter") sendChatMessage(); });

function handleRoute() {
    const path = window.location.pathname;
    const route = getRouteFromPath(path);

    if (AUTH_ROUTES.includes(path)) {
        window.location.href = path + window.location.search;
        return;
    }

    const isHomePage = path === ROUTE_HOME || path === "/";

    showingPlaylist = route === "playlist";
    showingSaved = route === "favorites";
    showingHistory = route === "history";

    playlistToggle.classList.toggle("active", showingPlaylist);
    savedToggle.classList.toggle("active", showingSaved);
    historyToggle.classList.toggle("active", showingHistory);

    document.getElementById("fabPlaylist").classList.toggle("active", showingPlaylist);
    document.getElementById("fabSaved").classList.toggle("active", showingSaved);
    document.getElementById("fabHistory").classList.toggle("active", showingHistory);

    setHomeChromeVisible(isHomePage);

    if (showingPlaylist) {
        loadPlaylists().then(renderPlaylistView);
    } else if (showingSaved) {
        loadSaved().then(renderSavedView);
    } else if (showingHistory) {
        renderHistoryView();
    } else {
        currentPage = parseInt(sessionStorage.getItem("currentPage"), 10) || 1;
        renderArticles();
    }
}

function escHtml(str) {
    const div = document.createElement("div");
    div.textContent = str;
    return div.innerHTML;
}

initTheme();
initAuth().then(() => {
    applyLanguage();
    handleRoute();
});

document.getElementById("logoCircle").addEventListener("click", () => navigateTo(ROUTE_HOME));

const fabToggle = document.getElementById("fabToggle");
const fabMenu = document.getElementById("fabMenu");
const floatingActions = document.getElementById("floatingActions");
fabToggle.addEventListener("click", () => {
    const open = fabMenu.hidden;
    fabMenu.hidden = !open;
    fabToggle.classList.toggle("open", open);
});
document.addEventListener("click", (e) => {
    if (!floatingActions.contains(e.target)) {
        fabMenu.hidden = true;
        fabToggle.classList.remove("open");
    }
});
function closeFabMenu() {
    fabMenu.hidden = true;
    fabToggle.classList.remove("open");
}

document.getElementById("fabHome").addEventListener("click", () => { closeFabMenu(); navigateTo(ROUTE_HOME); });
document.getElementById("fabChat").addEventListener("click", () => { closeFabMenu(); toggleChatPanel(); });
document.getElementById("fabPlaylist").addEventListener("click", () => { closeFabMenu(); if (showingPlaylist) exitPlaylistView(); else enterPlaylistView(); });
document.getElementById("fabSaved").addEventListener("click", () => { closeFabMenu(); if (showingSaved) exitSavedView(); else enterSavedView(); });
document.getElementById("fabHistory").addEventListener("click", () => { closeFabMenu(); if (showingHistory) exitHistoryView(); else enterHistoryView(); });
document.getElementById("fabTheme").addEventListener("click", () => { closeFabMenu(); themeToggle.click(); });

window.addEventListener("popstate", handleRoute);

const scrollTopButton = document.getElementById("scrollTopButton");
let lastScrollY = window.scrollY;
let headerHidden = false;

window.addEventListener("scroll", () => {
    var currentScrollY = window.scrollY;
    scrollTopButton.classList.toggle("visible", currentScrollY > 400);

    if (window.innerWidth <= 1000) {
        if (currentScrollY > lastScrollY && currentScrollY > 80) {
            if (!headerHidden) {
                document.querySelector(".topbar").classList.add("head-hidden");
                headerHidden = true;
            }
        } else {
            if (headerHidden) {
                document.querySelector(".topbar").classList.remove("head-hidden");
                headerHidden = false;
            }
        }
    }

    lastScrollY = currentScrollY;
});
scrollTopButton.addEventListener("click", () => { window.scrollTo({ top: 0, behavior: "smooth" }); });

const aiLimitBar = document.getElementById("aiLimitBar");
const aiLimitLabel = document.getElementById("aiLimitLabel");
const aiLimitFill = document.getElementById("aiLimitFill");
let aiLimitTimer = null;

function updateAiLimitBar(response) {
    const remaining = parseInt(response.headers.get("X-RateLimit-Remaining"));
    const limit = parseInt(response.headers.get("X-RateLimit-Limit"));
    if (isNaN(remaining) || isNaN(limit)) return;

    if (remaining <= 0) {
        aiLimitLabel.textContent = "AI 0/" + limit;
        aiLimitFill.style.width = "0%";
    } else {
        aiLimitLabel.textContent = "AI " + remaining + "/" + limit;
        aiLimitFill.style.width = (remaining / limit * 100) + "%";
    }
    aiLimitBar.classList.toggle("low", remaining <= 3);
    aiLimitBar.classList.toggle("empty", remaining <= 0);
    aiLimitBar.style.display = "flex";
}

function showAiLimitReached(response) {
    updateAiLimitBar(response);
    showToast("AI limit reached. Try again later.", "warning", 10000);
}
