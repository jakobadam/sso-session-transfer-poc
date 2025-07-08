using System;
using Auth0.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SampleMvcApp.Support;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddAuth0WebAppAuthentication(options =>
{
    options.Domain = builder.Configuration["Auth0:Domain"] ?? throw new InvalidOperationException();
    options.ClientId = builder.Configuration["Auth0:ClientId"] ?? throw new InvalidOperationException();
    
    // Used for PKCE backchannel communication, i.e., exchanging the authorization code for tokens.
    options.ClientSecret = builder.Configuration["Auth0:ClientSecret"];
    
    options.OpenIdConnectEvents = new Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectEvents
    {
        OnAuthorizationCodeReceived = c =>
        {
            Console.WriteLine($"[OIDC] OnAuthorizationCodeReceived: {c.ProtocolMessage?.Code}");
            return Task.CompletedTask;
        },
        OnRedirectToIdentityProvider = context =>
        {
            Console.WriteLine($"[OIDC] OnRedirectToIdentityProvider: Redirecting to {context.ProtocolMessage?.IssuerAddress}");
            var sessionTransferToken = context.HttpContext.Request.Cookies[SessionTransferTokenMiddleware.SessionTransferTokenCookieName];

            if (!string.IsNullOrEmpty(sessionTransferToken))
            {
                // Hmm. The session transfer cookie is not honoured by Auth0 /authorize?!? 
                // Adding a query param as a workaround.
                context.ProtocolMessage.SetParameter(SessionTransferTokenMiddleware.SessionTransferTokenQueryParamName, sessionTransferToken);
                Console.WriteLine("[OIDC] session_transfer_token included in query param");
            }
            
            return Task.CompletedTask;
        },
        OnAccessDenied = c =>
        {
            Console.WriteLine($"[OIDC] OnAccessDenied:");
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = c =>
        {
            Console.WriteLine($"[OIDC] OnAuthenticationFailed: {c.Exception?.Message}");
            return Task.CompletedTask;
        },
        OnRemoteFailure = c =>
        {
            Console.WriteLine($"[OIDC] OnRemoteFailure: {c.Failure?.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = c =>
        {
            Console.WriteLine($"[OIDC] OnTokenValidated: {c.SecurityToken?.Id}");
            return Task.CompletedTask;
        },
        OnTicketReceived = c =>
        {
            Console.WriteLine($"[OIDC] OnTicketReceived");
            return Task.CompletedTask;
        },
        OnMessageReceived = c =>
        {
            // var sessionTransferToken = context.Request.Query[SessionTransferTokenQueryParamName].ToString();
            // if (!string.IsNullOrEmpty(cookie))
            // {
            //     c.ProtocolMessage.SetParameter("session_transfer_token", cookie);
            //     Console.WriteLine($"[OIDC] Added session_transfer_token to authorize request: {cookie}");
            // }
            Console.WriteLine($"[OIDC] OnMessageReceived");
            return Task.CompletedTask;
        },
        OnRemoteSignOut = c =>
        {
            Console.WriteLine($"[OIDC] OnRemoteSignOut");
            return Task.CompletedTask;
        },
        OnTokenResponseReceived = c =>
        {
            Console.WriteLine($"[OIDC] OnTokenResponseReceived");
            return Task.CompletedTask;
        },
        OnUserInformationReceived = c =>
        {
            Console.WriteLine($"[OIDC] OnUserInformationReceived");
            return Task.CompletedTask;
        },
        OnSignedOutCallbackRedirect = c =>
        {
            Console.WriteLine($"[OIDC] OnSignedOutCallbackRedirect");
            return Task.CompletedTask;
        },
        OnRedirectToIdentityProviderForSignOut = c =>
        {
            Console.WriteLine($"[OIDC] OnRedirectToIdentityProviderForSignOut");
            return Task.CompletedTask;
        },
    };
}).WithAccessToken(x =>
{
    x.UseRefreshTokens = true;
});

// Configure the HTTP request pipeline.
builder.Services.ConfigureSameSiteNoneCookies();
var app = builder.Build();

// Enable some logging for the OpenID Connect flow.
// Show Personally Identifiable Information (PII) for debugging purposes.
// Note: This should only be enabled in development environments.
IdentityModelEventSource.ShowPII = true;

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStaticFiles();
app.UseCookiePolicy();

// Register custom middleware before authentication
app.UseMiddleware<SessionTransferTokenMiddleware>();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseEndpoints(endpoints =>
{
    endpoints.MapDefaultControllerRoute();
});

app.Run();