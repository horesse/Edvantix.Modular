using EDV.Framework.Caching.Telemetry;
using Microsoft.Extensions.Caching.Hybrid;
using System.Diagnostics;

namespace EDV.Framework.Caching;

/// <summary>
/// Декоратор <see cref="HybridCache"/>, который записывает метрики OpenTelemetry и спаны для каждой
/// операции. Внутренний кэш — это обычный <see cref="HybridCache"/>, зарегистрированный через
/// <c>AddHybridCache</c>; этот тип прозрачно оборачивает его, поэтому потребители внедряют <c>HybridCache</c>
/// как обычно и получают наблюдаемость бесплатно.
/// </summary>
/// <remarks>
/// Определение попадания/промаха выполняется путём обёртывания фабричного делегата с флагом и записи
/// на основе того, был ли установлен флаг. Попадания в L1 и L2 считаются как "hit" — HybridCache
/// не сообщает, какой уровень обслужил чтение. Длительность выполнения фабрики записывается только при промахе.
/// </remarks>
internal sealed class ObservableHybridCache : HybridCache
{
    private readonly HybridCache _inner;

    public ObservableHybridCache(HybridCache inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using var activity = CachingTelemetry.ActivitySource.StartActivity(
            "cache.get_or_create");
        activity?.SetTag("cache.system", "edv.hybrid");
        activity?.SetTag("cache.key", key);

        // Оборачиваем фабрику, чтобы записывать попадание/промах и длительность фабрики без выделения
        // замыкания над состоянием вызывающего — состояние передаётся через параметр TState.
        var wrappedState = new FactoryWrapperState<TState, T>(state, factory, Invoked: false);
        var wrapperBox = new StrongBox<FactoryWrapperState<TState, T>>(wrappedState);

        T result;
        try
        {
            result = await _inner.GetOrCreateAsync(
                key,
                wrapperBox,
                static async (box, ct) =>
                {
                    var sw = ValueStopwatch.StartNew();
                    box.Value.Invoked = true;
                    try
                    {
                        return await box.Value.Factory(box.Value.State, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        CachingTelemetry.FactoryDurationMs.Record(sw.ElapsedMilliseconds);
                    }
                },
                options,
                tags,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }

        if (wrapperBox.Value.Invoked)
        {
            CachingTelemetry.Misses.Add(1);
            activity?.SetTag("cache.hit", false);
        }
        else
        {
            CachingTelemetry.Hits.Add(1);
            activity?.SetTag("cache.hit", true);
        }

        return result;
    }

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = CachingTelemetry.ActivitySource.StartActivity(
            "cache.set");
        activity?.SetTag("cache.system", "edv.hybrid");
        activity?.SetTag("cache.key", key);

        return _inner.SetAsync(key, value, options, tags, cancellationToken);
    }

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        using var activity = CachingTelemetry.ActivitySource.StartActivity(
            "cache.remove");
        activity?.SetTag("cache.system", "edv.hybrid");
        activity?.SetTag("cache.key", key);
        CachingTelemetry.Invalidations.Add(1);

        return _inner.RemoveAsync(key, cancellationToken);
    }

    public override ValueTask RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        CachingTelemetry.Invalidations.Add(1);
        return _inner.RemoveAsync(keys, cancellationToken);
    }

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        using var activity = CachingTelemetry.ActivitySource.StartActivity(
            "cache.remove_by_tag");
        activity?.SetTag("cache.system", "edv.hybrid");
        activity?.SetTag("cache.tag", tag);
        CachingTelemetry.Invalidations.Add(1);

        return _inner.RemoveByTagAsync(tag, cancellationToken);
    }

    public override ValueTask RemoveByTagAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        CachingTelemetry.Invalidations.Add(1);
        return _inner.RemoveByTagAsync(tags, cancellationToken);
    }

    // Состояние передаётся через TState, чтобы избежать замыкания на каждый вызов; StrongBox позволяет
    // наблюдать флаг "фабрика вызвана?" после внутреннего вызова (HybridCache не сообщает о попадании/промахе).
    private struct FactoryWrapperState<TState, T>
    {
        public FactoryWrapperState(TState state, Func<TState, CancellationToken, ValueTask<T>> factory, bool Invoked)
        {
            State = state;
            Factory = factory;
            this.Invoked = Invoked;
        }

        public TState State;
        public Func<TState, CancellationToken, ValueTask<T>> Factory;
        public bool Invoked;
    }

    /// <summary>Минимальная обёртка ссылочного типа, чтобы состояние структуры можно было наблюдать после вызова.</summary>
    private sealed class StrongBox<T>
    {
        public T Value;
        public StrongBox(T value) => Value = value;
    }

    /// <summary>Секундомер на основе структуры, чтобы избежать выделения <see cref="Stopwatch"/> на каждый вызов.</summary>
    private readonly struct ValueStopwatch
    {
        private static readonly double TimestampToMs = 1000.0 / Stopwatch.Frequency;
        private readonly long _start;

        private ValueStopwatch(long start) => _start = start;

        public static ValueStopwatch StartNew() => new(Stopwatch.GetTimestamp());

        public double ElapsedMilliseconds => (Stopwatch.GetTimestamp() - _start) * TimestampToMs;
    }
}