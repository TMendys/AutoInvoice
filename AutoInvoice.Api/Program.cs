using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication("jwt")
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
    .AddJwtBearer("jwt", o =>
    {
        // // var del = o.Events.OnForbidden;
        // o.Events.OnForbidden = context =>
        // {
        //     if (context.Request.Path.StartsWithSegments("/visma"))
        //     {
        //         return context.HttpContext.ChallengeAsync("visma");
        //     }
        //     return Task.CompletedTask;
        //     // return del(context);
        // };
        o.RequireHttpsMetadata = false;
        o.SaveToken = true;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true
        };
    })
    .AddOAuth("visma", o =>
    {
        o.SignInScheme = "jwt";
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
                .GetHandlerAsync(context.HttpContext, "jwt");

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
                .First(x => x.AuthenticationType == "jwt");
            identity.AddClaim(new Claim("visma-token", "true"));
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("visma-enabled", pb =>
    {
        pb.AddAuthenticationSchemes("jwt")
            .RequireClaim("visma-token", "true")
            .RequireAuthenticatedUser();
    });
    o.AddPolicy("jwt-test", pb =>
    {
        pb.AddAuthenticationSchemes("jwt")
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

app.MapGet("/jwt-test", (HttpContext context) =>
{
    return context.User.Claims.Select(x => new { x.Type, x.Value }).ToList();
}).RequireAuthorization("jwt-test");

app.MapGet("/jwt", (IConfiguration config) =>
{
    var handler = new JsonWebTokenHandler();
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]));
    var token = handler.CreateToken(new SecurityTokenDescriptor()
    {
        Issuer = config["Jwt:Issuer"],
        Audience = config["Jwt:Audience"],
        Subject = new ClaimsIdentity(new[]
        {
                new Claim("user_id", Guid.NewGuid().ToString()),
        }),
        SigningCredentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256Signature)
    });

    return token;
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

app.MapGet("/visma", (HttpContext context, string token) =>
{
    // context.Request.Headers.Authorization;
    return context.ChallengeAsync("visma");
});

app.MapGet("/visma/customers",
    async (IHttpClientFactory httpClientFactory,
    HttpContext context) =>
{
    var accessToken = context.User.FindFirstValue("visma_access_token");
    var client = httpClientFactory.CreateClient();

    using var request = new HttpRequestMessage(
        HttpMethod.Get, "https://eaccountingapi-sandbox.test.vismaonline.com/v2/customers");
    request.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
    using var response = await client.SendAsync(request);
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

        var identity = principalClone.Identities.First(x => x.AuthenticationType == "jwt");
        identity.AddClaim(new Claim("visma_access_token", accessToken));
        identity.AddClaim(new Claim("visma_refresh_token", refreshToken));
        identity.AddClaim(new Claim("visma_token_expires", expires));

        return Task.FromResult(principalClone);
    }
}