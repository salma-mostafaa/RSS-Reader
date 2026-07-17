using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using RSS_Reader.Helpers;
using RSS_Reader.Models;
using RSS_Reader.Services;
using RSS_Reader.Storage;

namespace RSS_Reader.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/register", async (HttpContext http, RegisterRequest req, IUserStore userStore) =>
        {
            var email = (req.Email ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                return Results.Problem("A valid email is required", statusCode: 400);
            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
                return Results.Problem("Password must be at least 8 characters", statusCode: 400);
            if (userStore.GetByEmail(email) != null)
                return Results.Problem("An account with this email already exists", statusCode: 400);

            var user = userStore.Create(email, PasswordHasher.Hash(req.Password));

            var verifyLink = $"{http.Request.Scheme}://{http.Request.Host}/auth/verify?token={user.VerificationToken}";
            var plainBody = "Hi there,\n\nWelcome to RSS Reader — your personal news hub.\n\nPlease verify your email address by clicking the link below to activate your account:\n" + verifyLink + "\n\nIf you did not create this account, you can safely ignore this email.\n\n— RSS Reader";
            var htmlBody = EmailTemplate(
                "Verify your email",
                $"Hi there,<br>Welcome to RSS Reader — your personal news hub.",
                "Click the button below to verify your email address and activate your account.",
                $"<a href='{verifyLink}' style='display:inline-block;background:#f97316;color:#fff;padding:14px 28px;border-radius:6px;text-decoration:none;font-weight:600;margin:16px 0;font-size:15px'>Verify Email Address</a>",
                "You received this email because you created an account on RSS Reader. If you did not sign up, please ignore this message."
            );
            var emailSender = http.RequestServices.GetService<Services.IEmailSender>();
            if (emailSender != null)
            {
                try
                {
                    await emailSender.SendAsync(user.Email, "Verify your email — RSS Reader", plainBody, htmlBody);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EMAIL] Verification email failed: {ex.Message}");
                }
            }

            return Results.Ok(new { email = user.Email, verified = false, message = "Check your email to verify your account." });
        });

        app.MapPost("/auth/login", async (HttpContext http, LoginRequest req, IUserStore userStore) =>
        {
            var email = (req.Email ?? "").Trim().ToLowerInvariant();
            var user = userStore.GetByEmail(email);
            if (user == null || user.PasswordHash == null || !PasswordHasher.Verify(req.Password ?? "", user.PasswordHash))
                return Results.Problem("Invalid email or password", statusCode: 401);

            await SignInUser(http, user);
            await SendSignInEmail(http, user);
            return Results.Ok(new { id = user.Id, email = user.Email });
        });

        app.MapPost("/auth/forgot-password", async (ForgotPasswordRequest req, IUserStore userStore, IPasswordResetStore resetStore, HttpContext http) =>
        {
            var email = (req.Email ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                return Results.Problem("A valid email is required", statusCode: 400);

            var user = userStore.GetByEmail(email);
            if (user != null)
            {
                var entry = resetStore.Create(user.Id);
                var resetLink = $"{http.Request.Scheme}://{http.Request.Host}/login?token={entry.Token}";
                var plainBody = "Hi there,\n\nWe received a request to reset the password for your RSS Reader account.\n\nClick the link below to choose a new password:\n" + resetLink + "\n\nThis link expires in 1 hour. If you did not request a password reset, you can safely ignore this email — your password will not be changed.\n\n— RSS Reader";
                var htmlBody = EmailTemplate(
                    "Reset your password",
                    "Hi there,<br>We received a request to reset the password for your RSS Reader account.",
                    "Click the button below to choose a new password. This link expires in 1 hour.",
                    $"<a href='{resetLink}' style='display:inline-block;background:#f97316;color:#fff;padding:14px 28px;border-radius:6px;text-decoration:none;font-weight:600;margin:16px 0;font-size:15px'>Reset Password</a>",
                    "You received this email because a password reset was requested for your account. If you did not make this request, no action is needed."
                );
                var emailSender = http.RequestServices.GetService<Services.IEmailSender>();
                if (emailSender != null)
                {
                    try
                    {
                        await emailSender.SendAsync(email, "Reset your password — RSS Reader", plainBody, htmlBody);
                        Console.WriteLine($"[PASSWORD RESET] Email sent to {email}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PASSWORD RESET] Failed to send email to {email}: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"[PASSWORD RESET] No email service configured. Reset link for {email}: {resetLink}");
                }
            }

            return Results.Ok(new { message = "If an account with that email exists, a reset link has been sent." });
        });

        app.MapPost("/auth/reset-password", (ResetPasswordRequest req, IUserStore userStore, IPasswordResetStore resetStore) =>
        {
            if (string.IsNullOrWhiteSpace(req.Token))
                return Results.Problem("Reset token is required", statusCode: 400);
            if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
                return Results.Problem("Password must be at least 8 characters", statusCode: 400);

            var entry = resetStore.Get(req.Token);
            if (entry == null || entry.Used || entry.ExpiresAt < DateTime.UtcNow)
                return Results.Problem("This reset link is invalid or has expired.", statusCode: 400);

            var user = userStore.GetById(entry.UserId);
            if (user == null)
                return Results.Problem("User not found.", statusCode: 400);

            resetStore.MarkUsed(req.Token);
            userStore.UpdatePassword(user.Id, PasswordHasher.Hash(req.NewPassword));
            return Results.Ok(new { message = "Password has been reset. You can now sign in." });
        });

        app.MapPost("/auth/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        app.MapDelete("/auth/account", async (HttpContext http, IUserStore userStore) =>
        {
            var userId = GetUserId(http);
            if (!userId.HasValue) return Results.Unauthorized();

            userStore.Delete(userId.Value);

            var feedFile = $"feeds_{userId.Value}.json";
            if (File.Exists(feedFile)) File.Delete(feedFile);

            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        app.MapGet("/auth/me", (HttpContext http, IUserStore userStore) =>
        {
            var userId = GetUserId(http);
            var user = userId.HasValue ? userStore.GetById(userId.Value) : null;
            if (user == null) return Results.Unauthorized();
            return Results.Ok(new { id = user.Id, email = user.Email, emailVerified = user.EmailVerified });
        });

        app.MapGet("/auth/debug", (HttpContext http) =>
        {
            var hasCookie = http.Request.Cookies.ContainsKey("rss_auth");
            var isAuth = http.User.Identity?.IsAuthenticated == true;
            var userId = GetUserId(http);
            return Results.Ok(new
            {
                hasCookie,
                isAuthenticated = isAuth,
                userId = userId?.ToString(),
                scheme = http.Request.Scheme,
                host = http.Request.Host.ToString()
            });
        });

        app.MapGet("/ai-limit-status", (HttpContext http, AiRateTracker tracker) =>
        {
            var key = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? http.Connection.RemoteIpAddress?.ToString() ?? "anon";
            var isAuth = http.User.Identity?.IsAuthenticated == true;
            var (remaining, limit) = tracker.GetStatus(key, isAuth);
            http.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
            http.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
            return Results.Ok(new { remaining, limit });
        });

        app.MapGet("/auth/verify", async (string token, IUserStore userStore, HttpContext http) =>
        {
            var user = userStore.GetByVerificationToken(token);
            if (user == null)
                return Results.Problem("Invalid or expired verification link.", statusCode: 400);

            userStore.VerifyEmail(token);
            await SignInUser(http, user);
            http.Response.Redirect("/");
            return Results.Empty;
        });

        app.MapGet("/auth/users", (IUserStore userStore) =>
        {
            var users = userStore.GetAll().Select(u => new
            {
                id = u.Id,
                email = u.Email,
                hasPassword = !string.IsNullOrEmpty(u.PasswordHash),
                hasGoogle = !string.IsNullOrEmpty(u.GoogleId),
                createdAt = u.CreatedAt
            });
            return Results.Ok(users);
        });

        app.MapGet("/auth/google/login", (HttpContext http, IAuthenticationSchemeProvider schemeProvider) =>
        {
            var scheme = schemeProvider.GetSchemeAsync("Google").Result;
            if (scheme == null)
                return Results.Problem("Google sign-in is not configured.", statusCode: 501);

            var redirectUrl = http.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Results.Challenge(properties, new[] { "Google" });
        });
    }

    private static async Task SignInUser(HttpContext http, User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties { IsPersistent = true };
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), authProperties);
    }

    public static Guid? GetUserId(HttpContext http)
    {
        var idStr = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idStr, out var id) ? id : null;
    }

    public static async Task SendSignInEmail(HttpContext http, User user)
    {
        var emailSender = http.RequestServices.GetService<Services.IEmailSender>();
        if (emailSender == null) return;

        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC";
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var plain = "Hi there,\n\nA new sign-in to your RSS Reader account was just detected.\n\nTime: " + now + "\nIP address: " + ip + "\n\nIf this was you, no action is needed. If you did not sign in, please change your password immediately to secure your account.\n\n— RSS Reader";
        var html = EmailTemplate(
            "New sign-in detected",
            "Hi there,<br>A new sign‑in to your RSS Reader account was just detected.",
            $"<strong>Time:</strong> {now}<br><strong>IP address:</strong> {ip}",
            "",
            "You received this email because a sign‑in occurred on your RSS Reader account. If this wasn't you, change your password immediately."
        );
        try
        {
            await emailSender.SendAsync(user.Email, "New sign-in to your RSS Reader account", plain, html);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EMAIL] Sign-in notification failed: {ex.Message}");
        }
    }

    private static string EmailTemplate(string title, string greeting, string body, string cta, string footerReason)
    {
        return $@"<!DOCTYPE html><html><head><meta charset='utf-8'></head>
<body style='font-family:system-ui,-apple-system,sans-serif;background:#f5f5f5;margin:0;padding:30px'>
<table width='100%' cellpadding='0' cellspacing='0'><tr><td align='center'>
<table width='520' cellpadding='0' cellspacing='0' style='background:#fff;border-radius:10px;overflow:hidden;border:1px solid #e0e0e0'>
<tr><td style='background:#f97316;padding:24px 32px;color:#fff;font-size:20px;font-weight:700'>RSS Reader</td></tr>
<tr><td style='padding:28px 32px 16px'>
<h2 style='margin:0 0 8px;font-size:20px;color:#111'>{title}</h2>
<p style='margin:0 0 16px;color:#444;line-height:1.6'>{greeting}</p>
<p style='margin:0 0 8px;color:#444;line-height:1.6'>{body}</p>
{cta}
</td></tr>
<tr><td style='padding:16px 32px 24px;color:#888;font-size:0.8em;line-height:1.5;border-top:1px solid #eee'>
{footerReason}<br>
&copy; {DateTime.UtcNow.Year} RSS Reader. All rights reserved.
</td></tr>
</table></td></tr></table></body></html>";
    }
}

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);