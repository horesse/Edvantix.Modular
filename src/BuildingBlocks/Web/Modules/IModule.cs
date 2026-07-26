using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace EDV.Framework.Web.Modules;

public interface IModule
{
    // DI/Options/Health/и т.д. — не зависят от типов ASP.NET
    void ConfigureServices(IHostApplicationBuilder builder);

    // HTTP-связка — только Minimal API
    void MapEndpoints(IEndpointRouteBuilder endpoints);

    // Настройка промежуточного ПО — вызывается во время конфигурации конвейера.
    // Реализация по умолчанию ничего не делает, чтобы существующие модули не были обязаны реализовывать этот метод.
    void ConfigureMiddleware(IApplicationBuilder app) { }
}