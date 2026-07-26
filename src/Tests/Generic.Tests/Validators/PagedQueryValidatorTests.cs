using EDV.Modules.Auditing.Contracts.v1.GetAudits;
using EDV.Modules.Auditing.Features.v1.GetAudits;
using EDV.Modules.Identity.Contracts.v1.Users.SearchUsers;
using EDV.Modules.Identity.Features.v1.Users.SearchUsers;

namespace Generic.Tests.Validators;

/// <summary>
/// Тесты для общих правил валидации постраничных запросов (PageNumber, PageSize),
/// которые используются совместно во всех модулях, реализующих IPagedQuery.
/// </summary>
public sealed class PagedQueryValidatorTests
{
    public static TheoryData<IValidator, object> PagedQueryValidators => new()
    {
        { new GetAuditsQueryValidator(), new GetAuditsQuery() },
        { new SearchUsersQueryValidator(), new SearchUsersQuery() }
    };

    [Theory]
    [MemberData(nameof(PagedQueryValidators))]
    public void PageNumber_Should_Pass_When_Null(IValidator validator, object query)
    {
        // Подготовка — PageNumber по умолчанию null
        ArgumentNullException.ThrowIfNull(validator);

        // Действие
        var result = validator.Validate(new ValidationContext<object>(query));

        // Проверка
        result.Errors.ShouldNotContain(e => e.PropertyName == "PageNumber");
    }

    [Fact]
    public void PageNumber_Should_Pass_When_GreaterThanZero_Auditing()
    {
        // Подготовка
        var validator = new GetAuditsQueryValidator();
        var query = new GetAuditsQuery { PageNumber = 1 };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.Errors.ShouldNotContain(e => e.PropertyName == "PageNumber");
    }

    [Fact]
    public void PageNumber_Should_Pass_When_GreaterThanZero_Identity()
    {
        // Подготовка
        var validator = new SearchUsersQueryValidator();
        var query = new SearchUsersQuery { PageNumber = 5 };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.Errors.ShouldNotContain(e => e.PropertyName == "PageNumber");
    }

    [Fact]
    public void PageNumber_Should_Fail_When_Zero_Auditing()
    {
        // Подготовка
        var validator = new GetAuditsQueryValidator();
        var query = new GetAuditsQuery { PageNumber = 0 };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.Errors.ShouldContain(e => e.PropertyName == "PageNumber");
    }

    [Fact]
    public void PageNumber_Should_Fail_When_Zero_Identity()
    {
        // Подготовка
        var validator = new SearchUsersQueryValidator();
        var query = new SearchUsersQuery { PageNumber = 0 };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.Errors.ShouldContain(e => e.PropertyName == "PageNumber");
    }

    [Fact]
    public void PageNumber_Should_Fail_When_Negative_Auditing()
    {
        // Подготовка
        var validator = new GetAuditsQueryValidator();
        var query = new GetAuditsQuery { PageNumber = -1 };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.Errors.ShouldContain(e => e.PropertyName == "PageNumber");
    }

    [Fact]
    public void PageNumber_Should_Fail_When_Negative_Identity()
    {
        // Подготовка
        var validator = new SearchUsersQueryValidator();
        var query = new SearchUsersQuery { PageNumber = -5 };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.Errors.ShouldContain(e => e.PropertyName == "PageNumber");
    }

    [Fact]
    public void PageSize_Should_Pass_When_Null_Auditing()
    {
        // Подготовка
        var validator = new GetAuditsQueryValidator();
        var query = new GetAuditsQuery { PageSize = null };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.Errors.ShouldNotContain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void PageSize_Should_Pass_When_Null_Identity()
    {
        // Подготовка
        var validator = new SearchUsersQueryValidator();
        var query = new SearchUsersQuery { PageSize = null };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.Errors.ShouldNotContain(e => e.PropertyName == "PageSize");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void PageSize_Should_Pass_When_Between1And100_Auditing(int pageSize)
    {
        // Подготовка
        var validator = new GetAuditsQueryValidator();
        var query = new GetAuditsQuery { PageSize = pageSize };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.Errors.ShouldNotContain(e => e.PropertyName == "PageSize");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void PageSize_Should_Pass_When_Between1And100_Identity(int pageSize)
    {
        // Подготовка
        var validator = new SearchUsersQueryValidator();
        var query = new SearchUsersQuery { PageSize = pageSize };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.Errors.ShouldNotContain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void PageSize_Should_Fail_When_Zero_Auditing()
    {
        // Подготовка
        var validator = new GetAuditsQueryValidator();
        var query = new GetAuditsQuery { PageSize = 0 };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.Errors.ShouldContain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void PageSize_Should_Fail_When_Zero_Identity()
    {
        // Подготовка
        var validator = new SearchUsersQueryValidator();
        var query = new SearchUsersQuery { PageSize = 0 };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.Errors.ShouldContain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void PageSize_Should_Fail_When_GreaterThan100_Auditing()
    {
        // Подготовка
        var validator = new GetAuditsQueryValidator();
        var query = new GetAuditsQuery { PageSize = 101 };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.Errors.ShouldContain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void PageSize_Should_Fail_When_GreaterThan100_Identity()
    {
        // Подготовка
        var validator = new SearchUsersQueryValidator();
        var query = new SearchUsersQuery { PageSize = 150 };

        // Действие
        var result = validator.Validate(query);

        // Проверка
        result.Errors.ShouldContain(e => e.PropertyName == "PageSize");
    }
}
