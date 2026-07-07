const feedInput = document.getElementById("feedUrl");
const addFeedButton = document.getElementById("addFeedButton");
const feedsDiv = document.getElementById("feeds");
const feedsToggle = document.getElementById("feedsToggle");
const FEEDS_COLLAPSE_THRESHOLD = 2;
let feedsExpanded = false; //whether the subscriptions list is showing everything, persists across reloads this session
const articlesDiv = document.getElementById("articles");
const filtersDiv = document.getElementById("filters");
const articleCountSpan = document.getElementById("articleCount");
const refreshAllButton = document.getElementById("refreshAllButton");
const themeToggle = document.getElementById("themeToggle");

let allArticles = []; //cache of the last loaded articles, so filtering doesn't need a new fetch
let allFeeds = []; //cache of the last loaded feeds, so refresh-all doesn't need a new fetch
let activeFilters = new Set(); //currently selected feed filters, empty set = "All"
const ARTICLES_PER_PAGE = 25; //how many articles are shown per page
let currentPage = 1; //which page of the filtered articles is currently shown

/* ===== THEME ===== */

function initTheme() {
    const saved = localStorage.getItem("rss-theme");
    const prefersDark = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
    const theme = saved || (prefersDark ? "dark" : "light");
    applyTheme(theme);
}

function applyTheme(theme) {
    document.documentElement.setAttribute("data-theme", theme);
    themeToggle.setAttribute("aria-pressed", theme === "dark" ? "true" : "false");
    localStorage.setItem("rss-theme", theme);
}

themeToggle.addEventListener("click", () => {
    const current = document.documentElement.getAttribute("data-theme") === "dark" ? "dark" : "light";
    applyTheme(current === "dark" ? "light" : "dark");
});

/* ===== FEED COLOR CODING ===== */
//gives every feed a consistent accent color (derived from its title) so articles
//from the same source are easy to spot at a glance in the river of news

function feedColorVar(title) {
    let hash = 0;
    for (let i = 0; i < title.length; i++) {
        hash = (hash * 31 + title.charCodeAt(i)) >>> 0;
    }
    const index = hash % 8;
    return `var(--f${index})`;
}

/* ===== ADD FEED ===== */

addFeedButton.addEventListener("click", async () => {
    const url = feedInput.value;

    if (url === "") {
        alert("Enter a feed URL");
        return;
    }

    const response = await fetch("/feeds",
        {
            method: "POST",
            headers:
            {
                "Content-Type": "application/json" //i am sending json
            },
            body: JSON.stringify({ //what the endpoint expects, stringfy is similer to serialize
                url: url
            })
        });

    if (response.ok) { // response contains the server's reply, ok is true if status code is 200-299
        feedInput.value = ""; //clear the textbox after successfully adding the feed

        await loadFeeds(); //reload subscriptions so the new feed appears immediately
        await loadArticles(); //reload articles from all subscribed feeds
    }
    else {
        alert("Could not add feed");
    }
});

feedInput.addEventListener("keydown", (e) => {
    if (e.key === "Enter") {
        addFeedButton.click();
    }
});

/* ===== REFRESH ALL ===== */

refreshAllButton.addEventListener("click", async () => {
    if (allFeeds.length === 0) {
        return;
    }

    refreshAllButton.classList.add("spinning");
    refreshAllButton.disabled = true;

    const results = await Promise.allSettled(
        allFeeds.map(feed => fetch(`/feeds/${feed.id}/refresh`, { method: "POST" }))
    );

    const failed = results.filter(r => r.status === "rejected" || (r.value && !r.value.ok));

    await loadArticles(); //reload the river of news once every feed has finished refreshing

    refreshAllButton.classList.remove("spinning");
    refreshAllButton.disabled = false;

    if (failed.length > 0) {
        alert(`${failed.length} of ${allFeeds.length} feed(s) could not be refreshed`);
    }
});

/* ===== SUBSCRIPTIONS ===== */

feedsToggle.addEventListener("click", () => {
    feedsExpanded = !feedsExpanded;
    applyFeedsCollapseState(allFeeds.length);
});

function applyFeedsCollapseState(feedCount) {
    const shouldOffercollapse = feedCount > FEEDS_COLLAPSE_THRESHOLD;

    if (!shouldOffercollapse) {
        feedsDiv.classList.remove("collapsed");
        feedsToggle.hidden = true;
        return;
    }

    feedsToggle.hidden = false;
    feedsDiv.classList.toggle("collapsed", !feedsExpanded);
    feedsToggle.classList.toggle("expanded", feedsExpanded);
    feedsToggle.querySelector(".feeds-toggle-text").textContent = feedsExpanded
        ? "Show less"
        : `Show all (${feedCount})`;
}

async function loadFeeds() {
    const response = await fetch("/feeds");
    // we dont define get as its default

    allFeeds = await response.json(); //convert the JSON returned by the backend into JavaScript objects

    feedsDiv.innerHTML = ""; //clear previous subscriptions before displaying them again

    if (allFeeds.length === 0) {
        const empty = document.createElement("p");
        empty.className = "feed-empty";
        empty.textContent = "No subscriptions yet. Add a feed URL above to get started.";
        feedsDiv.appendChild(empty);
        applyFeedsCollapseState(0);
        return;
    }

    for (const feed of allFeeds) {
        const feedElement = document.createElement("div");
        feedElement.className = "feed-row";

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

        const refreshButton = document.createElement("button");
        refreshButton.textContent = "\u21BB";
        refreshButton.className = "refresh-btn";
        refreshButton.title = "Refresh this feed";

        refreshButton.addEventListener("click", async () => {
            refreshButton.classList.add("spinning");

            const response = await fetch(`/feeds/${feed.id}/refresh`,
                {
                    method: "POST"
                });

            refreshButton.classList.remove("spinning");

            if (response.ok) {
                await loadArticles(); //reload the river of news so the freshly fetched articles for this feed show up
            }
            else {
                alert("Could not refresh feed");
            }
        });

        const deleteButton = document.createElement("button");
        deleteButton.textContent = "\u2715";
        deleteButton.className = "delete-btn";
        deleteButton.title = "Delete this feed";

        deleteButton.addEventListener("click", async () => {
            const response = await fetch(`/feeds/${feed.id}`,
                {
                    method: "DELETE"
                });

            if (response.ok) {
                await loadFeeds(); //refresh subscriptions after deletion
                await loadArticles(); //refresh articles because one feed was removed
            }
        });

        const actions = document.createElement("div");
        actions.className = "feed-actions";
        actions.appendChild(refreshButton);
        actions.appendChild(deleteButton);

        feedElement.appendChild(identity);
        feedElement.appendChild(actions);

        feedsDiv.appendChild(feedElement);
    }

    applyFeedsCollapseState(allFeeds.length);
}

/* ===== ARTICLES ===== */

async function loadArticles() {
    const response = await fetch("/articles");
    allArticles = await response.json(); //convert JSON into JavaScript objects, cache for filtering

    renderFilters();
    renderArticles();
}

function renderFilters() {
    filtersDiv.innerHTML = ""; //clear previous filter buttons before rebuilding

    const feedTitles = [...new Set(allArticles.map(a => a.feedTitle))]; //unique feed names present right now

    if (feedTitles.length === 0) {
        return; //no articles yet, nothing to filter by
    }

    //drop any selected feeds that no longer have articles (e.g. the feed was deleted)
    for (const selected of [...activeFilters]) {
        if (!feedTitles.includes(selected)) {
            activeFilters.delete(selected);
        }
    }

    const options = ["All", ...feedTitles];

    for (const option of options) {
        const isActive = option === "All"
            ? activeFilters.size === 0
            : activeFilters.has(option);

        const button = document.createElement("button");
        button.textContent = option;
        button.className = "filter-btn" + (isActive ? " active" : "");

        button.addEventListener("click", () => {
            if (option === "All") {
                activeFilters.clear(); //"All" resets any multi-selection
            }
            else if (activeFilters.has(option)) {
                activeFilters.delete(option); //clicking an active filter again deselects it
            }
            else {
                activeFilters.add(option); //add this feed to the current multi-selection
            }

            renderFilters(); //rebuild so the "active" highlight(s) move to the clicked button(s)
            renderArticles();
        });

        filtersDiv.appendChild(button);
    }
}

function renderArticles() {
    articlesDiv.innerHTML = ""; //remove previously displayed articles

    const articles = activeFilters.size === 0
        ? allArticles
        : allArticles.filter(a => activeFilters.has(a.feedTitle));

    articleCountSpan.textContent = articles.length > 0
        ? `${articles.length} article${articles.length === 1 ? "" : "s"}`
        : "";

    if (articles.length === 0) {
        const empty = document.createElement("p");
        empty.className = "articles-empty";
        empty.textContent = "No articles available.";
        articlesDiv.appendChild(empty);
        return;
    }

    const totalPages = Math.max(1, Math.ceil(articles.length / ARTICLES_PER_PAGE));

    if (currentPage > totalPages) {
        currentPage = totalPages; //clamp in case the article count shrank (e.g. a feed was deleted)
    }

    const startIndex = (currentPage - 1) * ARTICLES_PER_PAGE;
    const visibleArticles = articles.slice(startIndex, startIndex + ARTICLES_PER_PAGE); //only this page's articles

    for (const article of visibleArticles) {
        const articleElement = document.createElement("div");
        articleElement.className = "article-card";
        articleElement.style.setProperty("--card-accent", feedColorVar(article.feedTitle));

        const meta = document.createElement("div");
        meta.className = "article-meta";
        meta.textContent = article.feedTitle;

        const title = document.createElement("h3");
        title.textContent = article.title;

        const summary = document.createElement("p");
        summary.className = "summary";
        summary.textContent = article.summary;

        const link = document.createElement("a");
        link.className = "read-link";
        link.textContent = "Read article \u2192";
        link.href = article.link;
        link.target = "_blank";
        link.rel = "noopener noreferrer";

        //link.rel = "noopener noreferrer"; in JavaScript is a critical best practice when opening external links in a new tab (e.g., using target="_blank"). It improves security by preventing malicious pages from controlling your original page and enhances privacy by hiding your site from analytics referrers.
        //noopener prevents the newly opened tab from gaining access to the original page , noreferrer stops the browser from sending the http referer header to the destination site

        articleElement.appendChild(meta);
        articleElement.appendChild(title);
        articleElement.appendChild(summary);
        articleElement.appendChild(link);

        articlesDiv.appendChild(articleElement); //make it visible
    }

    if (totalPages > 1) {
        const bar = document.createElement("div");
        bar.className = "pagination-bar";

        const prevButton = document.createElement("button");
        prevButton.type = "button";
        prevButton.className = "page-btn";
        prevButton.textContent = "Previous";
        prevButton.disabled = currentPage === 1;

        prevButton.addEventListener("click", () => {
            currentPage -= 1;
            renderArticles();
            window.scrollTo({ top: 0, behavior: "smooth" });
        });

        const pageInfo = document.createElement("span");
        pageInfo.className = "page-info";
        pageInfo.textContent = `Page ${currentPage} of ${totalPages}`;

        const nextButton = document.createElement("button");
        nextButton.type = "button";
        nextButton.className = "page-btn";
        nextButton.textContent = "Next";
        nextButton.disabled = currentPage === totalPages;

        nextButton.addEventListener("click", () => {
            currentPage += 1;
            renderArticles();
            window.scrollTo({ top: 0, behavior: "smooth" });

        });

        const jumpTo = document.createElement("div");
        jumpTo.className = "jump-to";

        const jumpLabel = document.createElement("span");
        jumpLabel.textContent = "Jump to";

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
                renderArticles();
                window.scrollTo({ top: 0, behavior: "smooth" });
            }

            jumpInput.value = ""; //clear so it always shows the "#" placeholder again
        }

        jumpInput.addEventListener("keydown", (e) => {
            if (e.key === "Enter") {
                goToTypedPage();
            }
        });

        jumpInput.addEventListener("blur", () => {
            if (jumpInput.value !== "") {
                goToTypedPage();
            }
        });

        jumpTo.appendChild(jumpLabel);
        jumpTo.appendChild(jumpInput);

        bar.appendChild(prevButton);
        bar.appendChild(pageInfo);
        bar.appendChild(nextButton);
        bar.appendChild(jumpTo);

        articlesDiv.appendChild(bar);
    }

}

initTheme(); //apply saved or system theme before anything renders
loadFeeds(); //load subscriptions as soon as the page opens
loadArticles(); //load articles as soon as the page opens
