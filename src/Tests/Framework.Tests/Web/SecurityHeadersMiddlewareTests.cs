using EDV.Framework.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Framework.Tests.Web;

public sealed class SecurityHeadersMiddlewareTests
{
    private static async Task<HttpContext> InvokeAsync(SecurityHeadersOptions options, string path = "/api/v1/users", bool https = false)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (https)
        {
            context.Request.Scheme = "https";
        }

        var nextInvoked = false;
        var middleware = new SecurityHeadersMiddleware(
            _ => { nextInvoked = true; return Task.CompletedTask; },
            Options.Create(options));

        await middleware.InvokeAsync(context);
        context.Items["__nextInvoked"] = nextInvoked;
        return context;
    }

    #region Основной сценарий

    [Fact]
    public async Task InvokeAsync_Should_SetBaseHeaders_When_Enabled()
    {
        // Действие
        var context = await InvokeAsync(new SecurityHeadersOptions());
        var headers = context.Response.Headers;

        // Проверка
        headers["X-Content-Type-Options"].ToString().ShouldBe("nosniff");
        headers["X-Frame-Options"].ToString().ShouldBe("DENY");
        headers["Referrer-Policy"].ToString().ShouldBe("strict-origin-when-cross-origin");
        headers["X-XSS-Protection"].ToString().ShouldBe("0");
        headers["Content-Security-Policy"].ToString().ShouldContain("default-src 'self'");
        ((bool)context.Items["__nextInvoked"]!).ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_Should_AddHsts_When_RequestIsHttps()
    {
        // Действие
        var context = await InvokeAsync(new SecurityHeadersOptions(), https: true);

        // Проверка
        context.Response.Headers["Strict-Transport-Security"].ToString()
            .ShouldBe("max-age=31536000; includeSubDomains");
    }

    [Fact]
    public async Task InvokeAsync_Should_OmitHsts_When_RequestIsHttp()
    {
        // Действие
        var context = await InvokeAsync(new SecurityHeadersOptions(), https: false);

        // Проверка
        context.Response.Headers.ContainsKey("Strict-Transport-Security").ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_Should_IncludeUnsafeInlineStyles_When_AllowInlineStylesTrue()
    {
        // Действие
        var context = await InvokeAsync(new SecurityHeadersOptions { AllowInlineStyles = true });

        // Проверка
        context.Response.Headers["Content-Security-Policy"].ToString().ShouldContain("'unsafe-inline'");
    }

    [Fact]
    public async Task InvokeAsync_Should_OmitUnsafeInlineStyles_When_AllowInlineStylesFalse()
    {
        // Действие
        var context = await InvokeAsync(new SecurityHeadersOptions { AllowInlineStyles = false });
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();

        // Проверка — style-src присутствует, но без unsafe-inline
        csp.ShouldContain("style-src 'self'");
        csp.ShouldNotContain("'unsafe-inline'");
    }

    [Fact]
    public async Task InvokeAsync_Should_AppendCustomSources_When_Provided()
    {
        // Подготовка
        var options = new SecurityHeadersOptions
        {
            ScriptSources = ["https://cdn.example.com"],
            StyleSources = ["https://styles.example.com"]
        };

        // Действие
        var context = await InvokeAsync(options);
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();

        // Проверка
        csp.ShouldContain("https://cdn.example.com");
        csp.ShouldContain("https://styles.example.com");
    }

    #endregion

    #region Граничные случаи

    [Fact]
    public async Task InvokeAsync_Should_SkipHeaders_When_Disabled()
    {
        // Действие
        var context = await InvokeAsync(new SecurityHeadersOptions { Enabled = false });

        // Проверка
        context.Response.Headers.ContainsKey("X-Content-Type-Options").ShouldBeFalse();
        context.Response.Headers.ContainsKey("Content-Security-Policy").ShouldBeFalse();
        ((bool)context.Items["__nextInvoked"]!).ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_Should_SkipHeaders_When_PathExcluded()
    {
        // Действие
        var context = await InvokeAsync(new SecurityHeadersOptions(), path: "/scalar/index.html");

        // Проверка
        context.Response.Headers.ContainsKey("X-Content-Type-Options").ShouldBeFalse();
        ((bool)context.Items["__nextInvoked"]!).ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_Should_NotOverwriteCsp_When_AlreadyPresent()
    {
        // Подготовка
        var context = new DefaultHttpContext();
        context.Request.Path = "/api";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'none'";
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, Options.Create(new SecurityHeadersOptions()));

        // Действие
        await middleware.InvokeAsync(context);

        // Проверка
        context.Response.Headers["Content-Security-Policy"].ToString().ShouldBe("default-src 'none'");
    }

    #endregion
}
