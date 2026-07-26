using EDV.Modules.Auditing.Contracts.v1.GetAudits;
using EDV.Modules.Auditing.Contracts.v1.GetAuditsByCorrelation;
using EDV.Modules.Auditing.Contracts.v1.GetAuditsByTrace;
using EDV.Modules.Auditing.Contracts.v1.GetAuditSummary;
using EDV.Modules.Auditing.Contracts.v1.GetExceptionAudits;
using EDV.Modules.Auditing.Contracts.v1.GetSecurityAudits;
using EDV.Modules.Auditing.Features.v1.GetAudits;
using EDV.Modules.Auditing.Features.v1.GetAuditsByCorrelation;
using EDV.Modules.Auditing.Features.v1.GetAuditsByTrace;
using EDV.Modules.Auditing.Features.v1.GetAuditSummary;
using EDV.Modules.Auditing.Features.v1.GetExceptionAudits;
using EDV.Modules.Auditing.Features.v1.GetSecurityAudits;

namespace Generic.Tests.Validators;

/// <summary>
/// Тесты для общих правил валидации диапазона дат (FromUtc меньше или равно ToUtc),
/// которые используются совместно в запросах с фильтрацией по дате.
/// </summary>
public sealed class DateRangeValidatorTests
{
    private static readonly DateTime BaseDate = new(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DateRange_Should_Pass_When_BothNull_GetAudits()
    {
        // Подготовка
        var validator = new GetAuditsQueryValidator();
        var query = new GetAuditsQuery { FromUtc = null, ToUtc = null };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void DateRange_Should_Pass_When_BothNull_GetAuditsByCorrelation()
    {
        // Подготовка
        var validator = new GetAuditsByCorrelationQueryValidator();
        var query = new GetAuditsByCorrelationQuery { CorrelationId = "test-id", FromUtc = null, ToUtc = null };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void DateRange_Should_Pass_When_BothNull_GetAuditsByTrace()
    {
        // Подготовка
        var validator = new GetAuditsByTraceQueryValidator();
        var query = new GetAuditsByTraceQuery { TraceId = "test-trace", FromUtc = null, ToUtc = null };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void DateRange_Should_Pass_When_BothNull_GetAuditSummary()
    {
        // Подготовка
        var validator = new GetAuditSummaryQueryValidator();
        var query = new GetAuditSummaryQuery { FromUtc = null, ToUtc = null };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void DateRange_Should_Pass_When_OnlyFromUtcSet_GetAudits()
    {
        // Подготовка
        var validator = new GetAuditsQueryValidator();
        var query = new GetAuditsQuery { FromUtc = BaseDate, ToUtc = null };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void DateRange_Should_Pass_When_OnlyToUtcSet_GetAudits()
    {
        // Подготовка
        var validator = new GetAuditsQueryValidator();
        var query = new GetAuditsQuery { FromUtc = null, ToUtc = BaseDate };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void DateRange_Should_Pass_When_FromUtcEqualsToUtc_GetAudits()
    {
        // Подготовка
        var validator = new GetAuditsQueryValidator();
        var query = new GetAuditsQuery { FromUtc = BaseDate, ToUtc = BaseDate };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void DateRange_Should_Pass_When_FromUtcBeforeToUtc_GetAudits()
    {
        // Подготовка
        var validator = new GetAuditsQueryValidator();
        var query = new GetAuditsQuery
        {
            FromUtc = BaseDate,
            ToUtc = BaseDate.AddDays(7)
        };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void DateRange_Should_Fail_When_FromUtcAfterToUtc_GetAudits()
    {
        // Подготовка
        var validator = new GetAuditsQueryValidator();
        var query = new GetAuditsQuery
        {
            FromUtc = BaseDate.AddDays(7),
            ToUtc = BaseDate
        };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("FromUtc должно быть меньше или равно ToUtc"));
    }

    [Fact]
    public void DateRange_Should_Fail_When_FromUtcAfterToUtc_GetAuditsByCorrelation()
    {
        // Подготовка
        var validator = new GetAuditsByCorrelationQueryValidator();
        var query = new GetAuditsByCorrelationQuery
        {
            CorrelationId = "test-id",
            FromUtc = BaseDate.AddDays(7),
            ToUtc = BaseDate
        };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("FromUtc должно быть меньше или равно ToUtc"));
    }

    [Fact]
    public void DateRange_Should_Fail_When_FromUtcAfterToUtc_GetAuditsByTrace()
    {
        // Подготовка
        var validator = new GetAuditsByTraceQueryValidator();
        var query = new GetAuditsByTraceQuery
        {
            TraceId = "test-trace",
            FromUtc = BaseDate.AddDays(7),
            ToUtc = BaseDate
        };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("FromUtc должно быть меньше или равно ToUtc"));
    }

    [Fact]
    public void DateRange_Should_Fail_When_FromUtcAfterToUtc_GetAuditSummary()
    {
        // Подготовка
        var validator = new GetAuditSummaryQueryValidator();
        var query = new GetAuditSummaryQuery
        {
            FromUtc = BaseDate.AddDays(7),
            ToUtc = BaseDate
        };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("FromUtc должно быть меньше или равно ToUtc"));
    }

    [Fact]
    public void DateRange_Should_Fail_When_FromUtcAfterToUtc_GetExceptionAudits()
    {
        // Подготовка
        var validator = new GetExceptionAuditsQueryValidator();
        var query = new GetExceptionAuditsQuery
        {
            FromUtc = BaseDate.AddDays(7),
            ToUtc = BaseDate
        };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("FromUtc должно быть меньше или равно ToUtc"));
    }

    [Fact]
    public void DateRange_Should_Fail_When_FromUtcAfterToUtc_GetSecurityAudits()
    {
        // Подготовка
        var validator = new GetSecurityAuditsQueryValidator();
        var query = new GetSecurityAuditsQuery
        {
            FromUtc = BaseDate.AddDays(7),
            ToUtc = BaseDate
        };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("FromUtc должно быть меньше или равно ToUtc"));
    }

    [Theory]
    [InlineData(1)]    // разница в 1 секунду
    [InlineData(60)]   // разница в 1 минуту
    [InlineData(3600)] // разница в 1 час
    public void DateRange_Should_Pass_When_FromUtcSlightlyBeforeToUtc(int secondsDiff)
    {
        // Подготовка
        var validator = new GetAuditsQueryValidator();
        var query = new GetAuditsQuery
        {
            FromUtc = BaseDate,
            ToUtc = BaseDate.AddSeconds(secondsDiff)
        };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.IsValid.ShouldBeTrue();
    }
}
