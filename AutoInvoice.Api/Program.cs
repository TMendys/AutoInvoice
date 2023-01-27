using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
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
        o.Scope.Add("ea:api");
        o.Scope.Add("ea:sales");
        o.Scope.Add("offline_access");
        o.SaveTokens = false;

        o.Events.OnCreatingTicket = async context =>
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<Database>();
            var authHandlerProvider =
                context.HttpContext.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
            var handler = await authHandlerProvider.GetHandlerAsync(context.HttpContext, "cookie");
            var authResult = await handler.AuthenticateAsync();
            if (!authResult.Succeeded)
            {
                context.Fail("failed authentication");
                return;
            }
            var cp = authResult.Principal;
            var userId = cp.FindFirstValue("user_id");
            db[userId] = context.AccessToken;

            context.Principal = cp.Clone();
            var identity = context.Principal.Identities.First(x => x.AuthenticationType == "cookie");
            identity.AddClaim(new Claim("visma-token", "y"));
        };
    });

builder.Services.AddAuthorization(b =>
{
    b.AddPolicy("visma-enabled", pb =>
    {
        pb.AddAuthenticationSchemes("cookie")
            .RequireClaim("visma-token", "y")
            .RequireAuthenticatedUser();
    });
});
builder.Services.AddSingleton<Database>()
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

    //AuthenticationProperties

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
public class Database : Dictionary<string, string> { }

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

        var cp = principal.Clone();
        var accessToken = db[userId];

        var identity = cp.Identities.First(x => x.AuthenticationType == "cookie");
        identity.AddClaim(new Claim("visma_access_token", accessToken));

        return Task.FromResult(cp);
    }
}