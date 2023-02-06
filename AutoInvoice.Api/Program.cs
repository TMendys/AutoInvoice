using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication("cookie")
    .AddCookie("cookie", o =>
    {
        o.LoginPath = "/login";
        var del = o.Events.OnRedirectToAccessDenied;
        o.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/visma"))
            {
                return context.HttpContext.ChallengeAsync("visma");
            }

            return del(context);
        };
    })
    .AddOAuth("visma", o =>
    {
        o.SignInScheme = "cookie";
        o.ClientId = builder.Configuration["Auth0:ClientId"];
        o.ClientSecret = builder.Configuration["Auth0:ClientSecret"];

        o.AuthorizationEndpoint = "https://identity-sandbox.test.vismaonline.com/connect/authorize";
        o.TokenEndpoint = "https://identity-sandbox.test.vismaonline.com/connect/token";
        o.CallbackPath = "/callback";
        // o.SaveTokens = true;
        o.Scope.Add("ea:api");
        o.Scope.Add("ea:sales");
        o.Scope.Add("offline_access");

        o.Events.OnCreatingTicket = async context =>
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<Database>();

            var authHandlerProvider =
                context.HttpContext.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();

            var handler = await authHandlerProvider
                .GetHandlerAsync(context.HttpContext, "cookie");

            var authResult = await handler?.AuthenticateAsync()!;
            if (!authResult.Succeeded)
            {
                context.Fail("failed authentication");
                return;
            }

            var claimsPrincipal = authResult.Principal;
            var userId = claimsPrincipal.FindFirstValue("user_id");
            db[userId] = new TokenInfo(
                context.AccessToken,
                context.RefreshToken,
                DateTime.Now.AddSeconds(int.Parse(context.TokenResponse.ExpiresIn))
            );

            context.Principal = claimsPrincipal.Clone();
            var identity = context.Principal.Identities
                .First(x => x.AuthenticationType == "cookie");
            identity.AddClaim(new Claim("visma-token", "true"));
        };
    });

builder.Services.AddAuthorization(b =>
{
    b.AddPolicy("visma-enabled", pb =>
    {
        pb.AddAuthenticationSchemes("cookie")
            .RequireClaim("visma-token", "true")
            .RequireAuthenticatedUser();
    });
});

builder.Services
    .AddSingleton<Database>()
    .AddTransient<IClaimsTransformation, VismaTokenClaimsTransformation>();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", (HttpContext context) =>
{
    return context.User.Claims.Select(x => new { x.Type, x.Value }).ToList();
});

app.MapGet("/login", () =>
{
    var userPrincipal = new ClaimsPrincipal(
        new ClaimsIdentity(
            new[] { new Claim("user_id", Guid.NewGuid().ToString()) }, "cookie")
    );

    //Add AuthenticationProperties if needed later

    return Results.SignIn(userPrincipal, authenticationScheme: "cookie");
});

app.MapGet("/visma/customers",
    async (IHttpClientFactory clientFactory,
    HttpContext context) =>
{
    var accessToken = context.User.FindFirstValue("visma_access_token");
    var client = clientFactory.CreateClient();

    using var req = new HttpRequestMessage(
        HttpMethod.Get, "https://eaccountingapi-sandbox.test.vismaonline.com/v2/customers");
    req.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
    using var response = await client.SendAsync(req);
    return await response.Content.ReadAsStringAsync();
}).RequireAuthorization("visma-enabled");

app.Run();

// In memory database for testing
public class Database : Dictionary<string, TokenInfo> { }

public record class TokenInfo(string AccessToken, string RefreshToken, DateTime Expires);

public class VismaTokenClaimsTransformation : IClaimsTransformation
{
    private readonly Database db;

    public VismaTokenClaimsTransformation(Database db)
    {
        this.db = db;
    }
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue("user_id");
        if (!db.ContainsKey(userId))
        {
            return Task.FromResult(principal);
        }

        var principalClone = principal.Clone();
        var accessToken = db[userId].AccessToken;
        var refreshToken = db[userId].RefreshToken;
        var expires = db[userId].Expires.ToString();

        var identity = principalClone.Identities.First(x => x.AuthenticationType == "cookie");
        identity.AddClaim(new Claim("visma_access_token", accessToken));
        identity.AddClaim(new Claim("visma_refresh_token", refreshToken));
        identity.AddClaim(new Claim("visma_token_expires", expires));

        return Task.FromResult(principalClone);
    }
}