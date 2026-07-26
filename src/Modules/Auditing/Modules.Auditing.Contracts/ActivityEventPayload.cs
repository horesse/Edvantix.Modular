namespace EDV.Modules.Auditing.Contracts;

public sealed record ActivityEventPayload(
    ActivityKind Kind,
    string Name,                 // шаблон маршрута, имя команды/запроса, id задания
    int? StatusCode,
    int DurationMs,
    BodyCapture Captured,        // Request/Response/Both/None
    int RequestSize,
    int ResponseSize,
    object? RequestPreview,      // усечённый/отфильтрованный снимок (JSON-совместимый)
    object? ResponsePreview
);