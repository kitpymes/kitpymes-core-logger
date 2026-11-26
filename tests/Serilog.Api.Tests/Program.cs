using Kitpymes.Core.Logger;
using Kitpymes.Core.Logger.Abstractions;
using Kitpymes.Core.Logger.Serilog;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

// Arranque: una fábrica propia hasta que existe el contenedor. Al liberarla se vacían los eventos pendientes.
using var arranque = KitpymesSerilog.CreateLoggerFactory(serilog => serilog
    .AddMinimunLevel(LoggerLevel.Trace)
    .AddConsole(LoggerLevel.Trace)
    .AddFile("Logs/start_host/.log"));

var logger = arranque.CreateLogger<Program>();

try
{
    logger.LogTrace("Initialize WebApplication...");

    var builder = WebApplication.CreateBuilder(args);

    // Sólo Serilog: sin esto, la consola de Microsoft escribe además cada evento.
    builder.Logging.ClearProviders();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    //**Opción 1: configuración desde el appsettings (sección LoggerSettings)**//
    builder.Logging.AddKitpymesLogging(builder.Configuration);

    //**Opción 2: configuración manual con opciones**//
    /*
        builder.Logging.AddKitpymesLogging(loggers => loggers.UseSerilog(serilog => serilog
            .AddMinimunLevel(LoggerLevel.Trace)
            .AddConsole()
            .AddFile(minimumLevel: LoggerLevel.Info)));
    */

    //**Opción 3: sólo el adaptador de Serilog**//
    /*
        builder.Logging.AddKitpymesSerilog(new SerilogSettings { Console = new SerilogConsoleSettings { Enabled = true } });
    */

    var application = builder.Build();

    if (application.Environment.IsDevelopment())
    {
        application.UseSwagger();
        application.UseSwaggerUI();
    }

    application.UseHttpsRedirection();
    application.UseAuthorization();
    application.MapControllers();

    logger.LogInformation("Started Host...");

    await application.RunAsync();
}
catch (Exception ex)
{
    logger.LogCritical(ex, "El host no ha podido arrancar");
}
