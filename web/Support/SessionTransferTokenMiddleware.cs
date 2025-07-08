using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace SampleMvcApp.Support;

/// <summary>
/// Middleware that does session transfer - an experimental native to web SSO feature of Auth0.
///
/// It checks for the `session_transfer_token` query parameter in the request URL, and if present,
/// sets an equally named cookie with the value of the query parameter.
/// 
/// This is used to transfer the session from a native app (e.g., iOS) to a web app, i.e., no re-login is required.
///
/// When the user is not authenticated, the user is redirected to the Auth0 login page, and when the cookie is
/// present, Auth0 will use it to authenticate the user without requiring them to log in again.
///
/// NOTE: The session transfer middleware is only needed for iOS SSO, as `SFSafariViewController` cannot inject cookies
/// (right?), whereas Android on the other hand shares cookies between the native app's login session and the web app
/// rendered inside it.
///
/// More: https://auth0.com/docs/authenticate/single-sign-on/native-to-web/configure-implement-native-to-web
/// </summary>
public class SessionTransferTokenMiddleware(RequestDelegate next)
{
    public const string SessionTransferTokenCookieName = "auth0_session_transfer_token";
    public const string SessionTransferTokenQueryParamName = "session_transfer_token";

    public async Task InvokeAsync(HttpContext context)
    {
        // iOS `SFSafariViewController` can't inject a cookie, so we use a query param
        var sessionTransferToken = context.Request.Query[SessionTransferTokenQueryParamName].ToString();

        if (!string.IsNullOrEmpty(sessionTransferToken))
        {
            context.Response.Cookies.Append(
                SessionTransferTokenCookieName,
                sessionTransferToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None
                });

            var url = GetUrlWithoutSessionTransferTokenParam(context);
            Console.WriteLine("[OIDC] Removed 'session_transfer_token' from query param,  redirecting to: " + url);
            context.Response.Redirect(url);
            return;
        }

        await next(context);
    }

    private static string GetUrlWithoutSessionTransferTokenParam(HttpContext context)
    {
        var query = QueryHelpers.ParseQuery(context.Request.QueryString.ToString());
        query.Remove(SessionTransferTokenQueryParamName);

        var url = new UriBuilder
        {
            Scheme = context.Request.Scheme,
            Port = context.Request.Host.Port ?? (context.Request.Scheme == "https" ? 443 : 80),
            Host = context.Request.Host.Host,
            Path = context.Request.Path,
        }.ToString();

        url = QueryHelpers.AddQueryString(url, query);
        return url;
    }
}