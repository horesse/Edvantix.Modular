using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.Users.SetProfileImage;

/// <summary>
/// Устанавливает URL аватара аутентифицированного пользователя — обычно постоянный <c>publicUrl</c>,
/// возвращаемый из потока с предварительно подписанным URL модуля Files. Передача null/пустого URL
/// удаляет изображение. Конечная точка принудительно привязывает целевой идентификатор к
/// аутентифицированному пользователю; вызывающий не может установить аватар другого пользователя.
/// </summary>
public sealed record SetProfileImageCommand(string? ImageUrl) : ICommand<Unit>;