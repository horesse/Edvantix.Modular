using EDV.Framework.Persistence.Specifications;
using System.Linq.Expressions;

namespace Framework.Tests.Persistence;

public sealed class SpecificationTests
{
    #region Тестовые дублёры

    private sealed class Person
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public Person? Manager { get; set; }
    }

    // Тестовая спецификация, раскрывающая защищённые вспомогательные методы композиции.
    private sealed class TestSpec : Specification<Person>
    {
        public void AddWhere(Expression<Func<Person, bool>> predicate) => Where(predicate);
        public void AddInclude(Expression<Func<Person, object>> include) => Include(include);
        public void AddInclude(string include) => Include(include);
        public void AddOrderBy(Expression<Func<Person, object>> key) => OrderBy(key);
        public void AddOrderByDescending(Expression<Func<Person, object>> key) => OrderByDescending(key);
        public void AddThenBy(Expression<Func<Person, object>> key) => ThenBy(key);

        public void Sort(string? expr, Action defaultOrdering, IReadOnlyDictionary<string, Expression<Func<Person, object>>> mappings)
            => ApplySortingOverride(expr, defaultOrdering, mappings);
    }

    private static readonly Person[] People =
    [
        new() { Id = 1, Name = "Alice", Age = 30 },
        new() { Id = 2, Name = "Bob", Age = 25 },
        new() { Id = 3, Name = "Carol", Age = 30 },
    ];

    #endregion

    #region Значения по умолчанию

    [Fact]
    public void Specification_Should_DefaultToNoTracking_When_Constructed()
    {
        // Подготовка и действие
        var spec = new TestSpec();

        // Проверка
        spec.AsNoTracking.ShouldBeTrue();
        spec.AsSplitQuery.ShouldBeFalse();
        spec.IgnoreQueryFilters.ShouldBeFalse();
        spec.Criteria.ShouldBeNull();
        spec.Includes.ShouldBeEmpty();
        spec.OrderExpressions.ShouldBeEmpty();
    }

    #endregion

    #region Критерии

    [Fact]
    public void Criteria_Should_CombineWithLogicalAnd_When_MultipleWhereAdded()
    {
        // Подготовка
        var spec = new TestSpec();
        spec.AddWhere(p => p.Age >= 30);
        spec.AddWhere(p => p.Name.StartsWith('A'));

        // Действие — компилируем объединённые критерии и выполняем на данных в памяти.
        var predicate = spec.Criteria.ShouldNotBeNull().Compile();
        var matches = People.Where(predicate).ToList();

        // Проверка — только Alice удовлетворяет обоим условиям.
        matches.Count.ShouldBe(1);
        matches[0].Name.ShouldBe("Alice");
    }

    [Fact]
    public void Criteria_Should_ReturnSingleExpression_When_OneWhereAdded()
    {
        // Подготовка
        var spec = new TestSpec();
        spec.AddWhere(p => p.Age == 25);

        // Действие
        var predicate = spec.Criteria.ShouldNotBeNull().Compile();

        // Проверка
        People.Count(predicate).ShouldBe(1);
    }

    #endregion

    #region Includes

    [Fact]
    public void Include_Should_RegisterTypedAndStringIncludes_When_Added()
    {
        // Подготовка
        var spec = new TestSpec();
        spec.AddInclude(p => p.Manager!);
        spec.AddInclude("Manager.Manager");

        // Проверка
        spec.Includes.Count.ShouldBe(1);
        spec.IncludeStrings.ShouldHaveSingleItem();
        spec.IncludeStrings[0].ShouldBe("Manager.Manager");
    }

    #endregion

    #region Сортировка

    [Fact]
    public void OrderExpressions_Should_RecordDirection_When_OrderHelpersUsed()
    {
        // Подготовка
        var spec = new TestSpec();
        spec.AddOrderByDescending(p => p.Age);
        spec.AddThenBy(p => p.Name);

        // Проверка
        spec.OrderExpressions.Count.ShouldBe(2);
        spec.OrderExpressions[0].Descending.ShouldBeTrue();
        spec.OrderExpressions[1].Descending.ShouldBeFalse();
    }

    [Fact]
    public void ApplySortingOverride_Should_UseClientSort_When_ValidExpressionProvided()
    {
        // Подготовка
        var spec = new TestSpec();
        var defaultCalled = false;
        var mappings = new Dictionary<string, Expression<Func<Person, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = p => p.Name,
            ["age"] = p => p.Age,
        };

        // Действие — "-age,name" => убывание по age, затем возрастание по name.
        spec.Sort("-age,name", () => defaultCalled = true, mappings);

        // Проверка
        defaultCalled.ShouldBeFalse();
        spec.OrderExpressions.Count.ShouldBe(2);
        spec.OrderExpressions[0].Descending.ShouldBeTrue();
        spec.OrderExpressions[1].Descending.ShouldBeFalse();
    }

    [Fact]
    public void ApplySortingOverride_Should_FallBackToDefault_When_ExpressionBlank()
    {
        // Подготовка
        var spec = new TestSpec();
        var defaultCalled = false;
        var mappings = new Dictionary<string, Expression<Func<Person, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = p => p.Name,
        };

        // Действие
        spec.Sort("   ", () => defaultCalled = true, mappings);

        // Проверка
        defaultCalled.ShouldBeTrue();
    }

    [Fact]
    public void ApplySortingOverride_Should_FallBackToDefault_When_AllKeysInvalid()
    {
        // Подготовка
        var spec = new TestSpec();
        var defaultCalled = false;
        var mappings = new Dictionary<string, Expression<Func<Person, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = p => p.Name,
        };

        // Действие — ни один из запрошенных ключей не входит в белый список.
        spec.Sort("unknown,-bogus", () => defaultCalled = true, mappings);

        // Проверка
        defaultCalled.ShouldBeTrue();
        spec.OrderExpressions.ShouldBeEmpty();
    }

    #endregion
}
