using Kitpymes.Core.Logger;
using Kitpymes.Core.Logger.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

var logger = Log
    .UseSerilog(serilog => serilog.AddMinimunLevel(LoggerLevel.Trace).AddConsole(LoggerLevel.Trace).AddFile("Logs/start_host/.log"))
    .CreateLogger<Program>();

try
{
    logger.Trace("Initialize WebApplication...");

    var builder = WebApplication.CreateBuilder(args);

    logger.Debug("ClearProviders Logging...");

    builder.Logging.ClearProviders();

    logger.Info("Configure Services...");

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    logger.Info("Add Logger Services...");

    //**Opción 1: configuración desde el appsettings**//
    builder.Services.LoadLogger();

    //**Opción 2: configuración manual con opciones**//
    /*
        builder.Services.LoadLogger(loggers =>
        {
            loggers.UseSerilog(serilog =>
            {
                serilog
                    .AddMinimunLevel(LoggerLevel.Trace)
                    .AddConsole
                    (

                    )
                    .AddFile
                    (
                        minimumLevel: Kitpymes.Core.Logger.Abstractions.LoggerLevel.Info
                    )
                    .AddSqlServer
                    (
                        autoCreateDb: true,
                        connectionString: "Data Source=aaaaaa;Initial Catalog=AppAuthDB;Integrated Security=True"connectionString: "Data Source=bbbbbb;Initial Catalog=LoggerDB;Integrated Security=True"
                    )
                    .AddEmail
                    (
                        userName: "admin@app.com",
                        password: "password",
                        server: "smtp.gmail.com",
                        from: "admin@app.com",
                        to: "error@app.com"
                    );
            });
        });
    */

    //**Opción 3: configuración manual**//
    /*
    builder.Services.LoadLogger(new LoggerSettings
    {
        Enabled = true,
        IsSerilogEnabled = true,
        SerilogSettings = new Kitpymes.Core.Logger.Serilog.SerilogSettings
        {
            Console = new Kitpymes.Core.Logger.Serilog.SerilogConsoleSettings
            {
                Enabled = true,
            }
        }
    });
    */


    logger.Info("Builds the WebApplication...");

    var application = builder.Build();

    // Configure the HTTP request pipeline.
    if (application.Environment.IsDevelopment())
    {
        application.UseSwagger();
        application.UseSwaggerUI();
    }

    application.UseHttpsRedirection();

    application.UseAuthorization();

    application.MapControllers();

    logger.Info("Started Host...");

    await application.RunAsync();
}
catch (Exception ex)
{
    logger.Error(ex);
}