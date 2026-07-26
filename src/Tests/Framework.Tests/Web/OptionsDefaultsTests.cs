using EDV.Framework.Web.Idempotency;
using EDV.Framework.Web.RateLimiting;
using EDV.Framework.Web.Security;

namespace Framework.Tests.Web;

public sealed class OptionsDefaultsTests
{
    #region SecurityHeadersOptions

    [Fact]
    public void SecurityHeadersOptions_Should_HaveSecureDefaults_When_Constructed()
    {
        // Действие
        var options = new SecurityHeadersOptions();

        // Проверка
        options.Enabled.ShouldBeTrue();
        options.AllowInlineStyles.ShouldBeTrue();
        options.ExcludedPaths.ShouldBe(["/scalar", "/openapi"]);
        options.ScriptSources.ShouldBeEmpty();
        options.StyleSources.ShouldBeEmpty();
    }

    #endregion

    #region RateLimitingOptions

    [Fact]
    public void RateLimitingOptions_Should_HaveDefaultPolicies_When_Constructed()
    {
        // Действие
        var options = new RateLimitingOptions();

        // Проверка
        options.Enabled.ShouldBeTrue();
        options.Tenant.PermitLimit.ShouldBe(1000);
        options.User.PermitLimit.ShouldBe(200);
        options.Ip.PermitLimit.ShouldBe(300);
        options.Auth.PermitLimit.ShouldBe(10);
        options.Tenant.WindowSeconds.ShouldBe(60);
        options.Auth.QueueLimit.ShouldBe(0);
    }

    [Fact]
    public void FixedWindowPolicyOptions_Should_HaveDefaults_When_Constructed()
    {
        // Действие
        var policy = new FixedWindowPolicyOptions();

        // Проверка
        policy.PermitLimit.ShouldBe(100);
        policy.WindowSeconds.ShouldBe(60);
        policy.QueueLimit.ShouldBe(0);
    }

    #endregion

    #region IdempotencyOptions

    [Fact]
    public void IdempotencyOptions_Should_HaveDefaults_When_Constructed()
    {
        // Действие
        var options = new IdempotencyOptions();

        // Проверка
        options.HeaderName.ShouldBe("Idempotency-Key");
        options.DefaultTtl.ShouldBe(TimeSpan.FromHours(24));
        options.MaxKeyLength.ShouldBe(128);
    }

    #endregion
}
