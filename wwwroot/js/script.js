const feedInput = document.getElementById("feedUrl");
const addFeedButton = document.getElementById("addFeedButton");
const feedsDiv = document.getElementById("feeds");
const articlesDiv = document.getElementById("articles");
const filtersDiv = document.getElementById("filters");

let allArticles = []; //cache of the last loaded articles, so filtering doesn't need a new fetch
let activeFilter = "All"; //currently selected feed filter

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

async function loadFeeds() {
    const response = await fetch("/feeds");
    // we dont define get as its default

    const feeds = await response.json(); //convert the JSON returned by the backend into JavaScript objects

    feedsDiv.innerHTML = ""; //clear previous subscriptions before displaying them again

    if (feeds.length === 0) {
        feedsDiv.textContent = "No subscriptions yet."; //show a message instead of an empty section
        return;
    }

    for (const feed of feeds) {
        const feedElement = document.createElement("div");

        const title = document.createElement("span");
        title.textContent = feed.title;

        const refreshButton = document.createElement("button");
        refreshButton.textContent = "🔄";
        refreshButton.className = "refresh-btn";

        refreshButton.addEventListener("click", async () => {
            const response = await fetch(`/feeds/${feed.id}/refresh`,
                {
                    method: "POST"
                });

            if (response.ok) {
                await loadArticles(); //reload the river of news so the freshly fetched articles for this feed show up
            }
            else {
                alert("Could not refresh feed");
            }
        });

        const deleteButton = document.createElement("button");
        deleteButton.textContent = "🗑️";
        deleteButton.className = "delete-btn";

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

        feedElement.appendChild(title);
        feedElement.appendChild(actions);

        feedsDiv.appendChild(feedElement);
    }

}

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

    const options = ["All", ...feedTitles];

    if (!options.includes(activeFilter)) {
        activeFilter = "All"; //the previously selected feed no longer has articles (e.g. it was deleted)
    }

    for (const option of options) {
        const button = document.createElement("button");
        button.textContent = option;
        button.className = "filter-btn" + (option === activeFilter ? " active" : "");

        button.addEventListener("click", () => {
            activeFilter = option;
            renderFilters(); //rebuild so the "active" highlight moves to the clicked button
            renderArticles();
        });

        filtersDiv.appendChild(button);
    }
}

function renderArticles() {
    articlesDiv.innerHTML = ""; //remove previously displayed articles

    const articles = activeFilter === "All"
        ? allArticles
        : allArticles.filter(a => a.feedTitle === activeFilter);

    if (articles.length === 0) {
        articlesDiv.textContent = "No articles available."; //show a friendly message when there are no articles
        return;
    }

    for (const article of articles) {
        const articleElement = document.createElement("div"); //Fixed: createElement (was createElemt)

        const title = document.createElement("h3");
        title.textContent = article.title;

        const feedName = document.createElement("p");
        feedName.textContent = "Feed: " + article.feedTitle;

        const summary = document.createElement("p");
        summary.textContent = article.summary;

        const link = document.createElement("a");
        link.textContent = "Read Article";
        link.href = article.link;
        link.target = "_blank";
        link.rel = "noopener noreferrer";

        //link.rel = "noopener noreferrer"; in JavaScript is a critical best practice when opening external links in a new tab (e.g., using target="_blank"). It improves security by preventing malicious pages from controlling your original page and enhances privacy by hiding your site from analytics referrers.
        //noopener prevents the newly opened tab from gaining access to the original page , noreferrer stops the browser from sending the http referer header to the destination site

        const line = document.createElement("hr");

        articleElement.appendChild(title);
        articleElement.appendChild(feedName);
        articleElement.appendChild(summary);
        articleElement.appendChild(link);
        articleElement.appendChild(line);

        articlesDiv.appendChild(articleElement); //make it visible
    }

}

loadFeeds(); //load subscriptions as soon as the page opens
loadArticles(); //load articles as soon as the page opens