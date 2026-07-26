using Mediator;
using System.Diagnostics;

namespace EDV.Framework.Web.Observability.OpenTelemetry;

/// <summary>
/// Создаёт спаны вокруг команд/запросов Mediator для улучшения видимости трассировки.
/// </summary>
public sealed class MediatorTracingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ActivitySource _activitySource;

    public MediatorTracingBehavior(ActivitySource activitySource)
    {
        _activitySource = activitySource;
    }

    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(next);

        using var activity = _activitySource.StartActivity(
            $"Mediator {typeof(TMessage).Name}",
            ActivityKind.Internal);

        if (activity is not null)
        {
            activity.SetTag("mediator.request_type", typeof(TMessage).FullName);
        }

        try
        {
            var response = await next(message, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return response;
        }
        // Широкий перехват intentional: трассировка должна записывать все типы исключений
        // в спан активности перед повторным выбросом вызывающему.
        catch (Exception ex)
        {
            if (activity is not null)
            {
                activity.SetStatus(ActivityStatusCode.Error);
                activity.SetTag("exception.type", ex.GetType().FullName);
                activity.SetTag("exception.message", ex.Message);
            }

            throw;
        }
    }
}