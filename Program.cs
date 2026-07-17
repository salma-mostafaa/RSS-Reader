using System.Security.Claims; // claims are as information about the currently logged-in user
using System.Threading.RateLimiting; // imports microsoft's rate limiting library for ai limit
using Microsoft.AspNetCore.Authentication;// name space contains authentication-realted classes
using Microsoft.AspNetCore.Authentication.Cookies; //without cookies, you'd have to login again every request
using Microsoft.AspNetCore.DataProtection; //asp.net encrypts sensitive data as without it every restart would invalidate all cookies
using Microsoft.AspNetCore.HttpOverrides; //
using Microsoft.AspNetCore.RateLimiting; //connect asp.net to the rate limiter
using RSS_Reader; //importing the wholde project
using RSS_Reader.Endpoints;
using RSS_Reader.Services;
using RSS_Reader.Storage;
using RSS_Reader.Helpers;

var builder = WebApplication.CreateBuilder(args); // creating the application everything starts here

//builder.Services... everything after it means whenever someone nees this object, create it for them, its called dependency injection
builder.Services.AddRateLimiter(options =>// registers a rate limiter service, so every thing goes here before reaching the ai endpoint
{
    options.RejectionStatusCode = 429; //if the limit is exceeded
    options.OnRejected = async (context, ct) => // when rejected, the context i sthe current http request that contains the request, response, user, cookies, headers, ip address, ... and ct is the cancellation token for the server to stop doing unncecsassry work
    {
        context.HttpContext.Response.StatusCode = 429; //response.staths code which inside the context
        context.HttpContext.Response.ContentType = "application/json"; //data type is json
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? (int)retry.TotalSeconds : 60;
        await context.HttpContext.Response.WriteAsync(
            $"{{\"error\":\"Too many AI requests. Please wait {retryAfter} seconds before trying again.\"}}", ct); // the server ask the rate limiter what is left before be able to use ai again so the metadata include the duartion left and we show the response as json to the  user
    };
    options.AddPolicy("ai", context => // here we are creating a policy/named rule named ai
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "anon"; //this determining who is making hte request as the rate llimiter needs an identity
        // check if the user is logged in if yes we got user id if not we use the ip address and if that isnt avalibile use anonymous key
        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        { // create a limiter for this specific user
            PermitLimit = 15,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0 // extra request are rejected no queuing
        });
    });
});

var keyDir = new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".keys"));
// directoyinfo create a class instead of just string "C:\\Projects\\.keys" so we can do keyDir.create(), keyDir.Exists and so on 
// enviroment : It knows information about where the application is running.
// Path.Combine() creates the correct path for whatever operating system the app is running on.
Console.WriteLine($"[KEYS] Directory: {keyDir.FullName}, Exists: {keyDir.Exists}");
if (!keyDir.Exists) keyDir.Create();
var keyFiles = keyDir.Exists ? keyDir.GetFiles() : Array.Empty<FileInfo>(); // if folder exists get all files otherwise returen empty array
Console.WriteLine($"[KEYS] Files: {string.Join(", ", keyFiles.Select(f => f.Name))}");

//.keys folder has nothing to do with your application data like feeds, playlists, or users. 
// It exists solely so ASP.NET can remember its encryption keys across restarts, allowing it to continue decrypting
// cookies, tokensok , and other protected data that it created earlier. This is why your code creates the folder before
// calling AddDataProtection().PersistKeysToFileSystem(keyDir).

builder.Services.AddDataProtection()
    .SetApplicationName("RSS_Reader")
    .PersistKeysToFileSystem(keyDir); // save permanently not in the ram only 

//-------------------------------------------------------------------------------------------------------------
//since preoduction servers usually look lik browser -> cloudfare-> --- --> asp.net the browser doesnt talk directly to the asp.net 
builder.Services.Configure<ForwardedHeadersOptions>(options => // forwardheader options like settings that contains options like should we trust this proxy , which header shoudl we read...etc
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto; // read the real api from the proxy and wherther the original request was http or https
    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Any, 0));
    options.KnownProxies.Clear();
    options.RequireHeaderSymmetry = false;
});

// so far data protection : encrypt cookies and tokens and save encryption keys in the .keys folder so they survive restarts
// forwarded headers : when your app is behind a reverse proxy (like nginx or cloudflare),  use the forwarded information to determine the user's real ip address and original protocol(Http/https)

//-----------------------------------------
builder.Services.AddHttpClient(); // we use it to download rss feeds, ai apis ,...
// its instead of doing new httpclient for each user which is too mush requests

builder.Services.AddSingleton(new Database("Data Source=rss.db")); // Addsingleton: One object. Shared everywhere as one database rss.db
builder.Services.AddSingleton<IFeedStore, FeedStore>(); //ifeedstroe is interface as methods as rules soo the feedstore promise to  implement every method required by ifeedstore and we make them seperate as if we changed the db so we keep ifeedstore the same
// Whenever someone asks for an IFeedStore, give them a single shared FeedStore object."
builder.Services.AddSingleton<IArticleStore, ArticleStore>();
builder.Services.AddSingleton<IPlaylistStore, PlaylistStore>();
builder.Services.AddSingleton<IChatStore, ChatStore>();
builder.Services.AddSingleton<ISummaryStore, SummaryStore>();
builder.Services.AddSingleton<ISavedStore, SavedStore>();
builder.Services.AddSingleton<IHistoryStore, HistoryStore>();
builder.Services.AddSingleton<IArticleSummaryStore, ArticleSummaryStore>();
builder.Services.AddSingleton<IUserStore, UserStore>();
builder.Services.AddSingleton<IPasswordResetStore, PasswordResetStore>();// for password reset tokens
// so each feature eventually reaches database--> rss.db through its own store


// ---------------------- sendgrid sectionn -----------------------------
var sendgridApiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY")
    ?? builder.Configuration["SendGrid:ApiKey"]; // try to find sendgrid api key either in the envvariable if deployed or in the
var sendgridFromEmail = Environment.GetEnvironmentVariable("SENDGRID_FROM_EMAIL")
    ?? builder.Configuration["SendGrid:FromEmail"] ?? "rssreaderxai@gmail.com";
var sendgridFromName = Environment.GetEnvironmentVariable("SENDGRID_FROM_NAME")
    ?? builder.Configuration["SendGrid:FromName"] ?? "RSS Reader";

if (!string.IsNullOrEmpty(sendgridApiKey))
{
    Console.WriteLine($"[EMAIL] SendGrid configured");
    builder.Services.AddSingleton<IEmailSender>( //register iemailsender service but this time we create the object ourselves that's why we didnt include emailsender
        new SendGridEmailSender(sendgridApiKey, sendgridFromEmail, sendgridFromName)); // create the object  manually instead of asp.net because the constructor needs values and aspnet doesnt know those values automatically
}
else
{
    Console.WriteLine($"[EMAIL] No SendGrid key — emails disabled");
}
//--------------------------------business logic /services-----------------------------------------------

builder.Services.AddSingleton<IFeedFetcher, FeedFetcher>(); //download rss feed
builder.Services.AddSingleton<FeedService>(); //no interface as nobody needs to swap it with another implementation, feedservice is like a manageer that cooredinate  doowsnload xml, parse xml, save articles,....
builder.Services.AddSingleton<AiSummaryService>(); //same idea, service that ai summarization
builder.Services.AddSingleton<AiRateTracker>(); //

// Standard ASP.NET Core cookie authentication rather than a hand-rolled session table -
// the same "boring, well-tested tool beats custom code" reasoning as using IConfiguration
// for secrets. Since this is an API consumed by fetch() (not server-rendered pages), the
// default "redirect to a login page" behavior is overridden to just return 401/403 JSON
// status codes instead - a redirect would otherwise confuse every fetch() call.
var googleClientId = Environment.GetEnvironmentVariable("Authentication__Google__ClientId")
    ?? builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = Environment.GetEnvironmentVariable("Authentication__Google__ClientSecret")
    ?? builder.Configuration["Authentication:Google:ClientSecret"];

var authBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme) //Use cookies to remember logged-in users.
    .AddCookie(options => //Configure how the cookie should behave
    {
        options.Cookie.Name = "rss_auth";
        options.Cookie.HttpOnly = true; //only the browser sends the cookie automatically , js cannot access it , this is a secuirty feature
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(1);// we keep login for 30 days
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.CallbackPath = "/signin-google";
        options.SaveTokens = false;
        options.Events.OnRemoteFailure = context =>
        {
            var errorMsg = context.Failure?.Message ?? "Google sign-in failed. Please try again.";
            Console.WriteLine($"[GOOGLE AUTH] Remote failure: {errorMsg}");
            if (context.Failure != null) Console.WriteLine($"[GOOGLE AUTH] Exception: {context.Failure}");
            context.Response.Redirect($"/login?error={Uri.EscapeDataString(errorMsg)}");
            context.HandleResponse();
            return Task.CompletedTask;
        };
        options.Events.OnCreatingTicket = async context =>
        { //a ticket is simply:The information that represents a logged-in user.
            var userStore = context.HttpContext.RequestServices.GetRequiredService<IUserStore>(); //becomes a UserStore object.
            var googleId = context.Identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = context.Identity?.FindFirst(ClaimTypes.Email)?.Value;

            Console.WriteLine($"[GOOGLE AUTH] OnCreatingTicket - googleId={googleId}, email={email}");

            if (string.IsNullOrEmpty(googleId) || string.IsNullOrEmpty(email))
            {
                Console.WriteLine("[GOOGLE AUTH] Missing googleId or email - failing");
                context.Fail("Google account information is missing.");
                return;
            }

            var user = userStore.GetByGoogleId(googleId);
            Console.WriteLine($"[GOOGLE AUTH] GetByGoogleId result: {(user != null ? user.Email : "null")}");
            if (user == null)
            {
                user = userStore.GetByEmail(email.ToLowerInvariant());
                Console.WriteLine($"[GOOGLE AUTH] GetByEmail result: {(user != null ? user.Email : "null")}");
                if (user != null)
                {
                    userStore.LinkGoogle(user.Id, googleId);
                    Console.WriteLine($"[GOOGLE AUTH] Linked Google account to existing user: {user.Email}");
                }
                else
                {
                    user = userStore.CreateFromGoogle(email.ToLowerInvariant(), googleId);
                    Console.WriteLine($"[GOOGLE AUTH] Created new user: {user.Email}");
                }
            }

            context.Identity!.RemoveClaim(context.Identity.FindFirst(ClaimTypes.NameIdentifier));
            context.Identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
            context.Properties.IsPersistent = true;
            Console.WriteLine($"[GOOGLE AUTH] Success - signing in as {user.Email}");

            await AuthEndpoints.SendSignInEmail(context.HttpContext, user);
        };
    });
}

//-------------------------the middleware pipeline---------------
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseForwardedHeaders(); //Figure out the user's real IP address.

app.UseRateLimiter(); //check Has this user sent too many requests?

app.Services.GetRequiredService<Database>().Initialize();

app.Use(async (context, next) =>
{
    Console.WriteLine($"[REQ] {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
    await next(); //to go to the next middleware
});

app.UseAuthentication(); //check Does this request contain the rss_auth cookie?
app.UseAuthorization(); //check Is this user allowed to access this endpoint?

app.Use(async (context, next) =>
{
    Console.WriteLine($"[AUTH] {context.Request.Path} — User: {(context.User.Identity?.IsAuthenticated == true ? context.User.FindFirstValue(ClaimTypes.Email) ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier) : "anonymous")}");
    await next();
});

//middleware like filters on a factory conveyor belt as its between the request and the endpoints
app.MapAuthEndpoints();
app.MapFeedEndpoints();
app.MapArticleEndpoints();
app.MapPlaylistEndpoints();
app.MapSummaryEndpoints();
app.MapChatEndpoints();
app.MapSavedEndpoints();
app.MapHistoryEndpoints();
app.MapArticleSummaryEndpoints();

app.Services.GetRequiredService<AiSummaryService>().StartDailyBackgroundLoop();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/login", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "login.html"));
});

app.MapGet("/register", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "login.html"));
});

app.MapFallbackToFile("index.html");

app.Run();