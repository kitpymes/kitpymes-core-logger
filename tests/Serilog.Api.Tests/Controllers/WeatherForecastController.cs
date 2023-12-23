using Microsoft.AspNetCore.Mvc;

using Kitpymes.Core.Logger.Abstractions;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Serilog.Api.Tests.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController(ILoggerService logger) : ControllerBase
    {
        private static readonly string[] Summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

        //private readonly ILogger<WeatherForecastController> _logger;
        private Kitpymes.Core.Logger.Abstractions.ILogger Logger => logger.CreateLogger<WeatherForecastController>();

        //public WeatherForecastController(ILogger<WeatherForecastController> logger)
        //{
        //    _logger = logger;
        //}

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            Logger.Info("All Summaries", "Template: {0}\r\n", JsonSerializer.Serialize(Summaries));

            Logger
                .Trace("Trace Summary 1:", Summaries[0])
                .Debug("Debug Summary 2:", Summaries[1])
                .Info("Info Summary 3:", Summaries[2])
                .Warning("Warning Summary 4:", Summaries[3])
                .Error("Error Summary 5:", Summaries[4]);

            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}
