using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using global::Serilog;
using global::Serilog.Core;
using global::Serilog.Events;
using Kitpymes.Core.Logger.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Kitpymes.Core.Logger.Serilog.Tests
{
    /// <summary>
    /// Logger de Serilog de Kitpymes (3.0.0): un logger de Serilog por fábrica, título por categoría, sin esperas,
    /// eventos vaciados al liberar, niveles y enriquecedores.
    /// </summary>
    [TestClass]
    public class SerilogTest
    {
        private string directorio = string.Empty;

        [TestInitialize]
        public void Inicializar()
        {
            directorio = Path.Combine(Path.GetTempPath(), "kpy-logger-" + Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void Limpiar()
        {
            try
            {
                if (Directory.Exists(directorio))
                {
                    Directory.Delete(directorio, recursive: true);
                }
            }
            catch (IOException)
            {
                // Un fichero aún abierto no debe tapar el resultado del test.
            }
        }

        [TestMethod]
        public void CreateLoggerFactory_CadaCategoriaLlevaSuTitulo()
        {
            using (var factory = KitpymesSerilog.CreateLoggerFactory(ConFichero()))
            {
                factory.CreateLogger("App.TituloA").LogError("mensaje-a");
                factory.CreateLogger<SerilogTest>().LogError("mensaje-b");
            }

            var eventos = LeerEventos();
            Assert.AreEqual("TituloA", eventos.Single(e => Mensaje(e) == "mensaje-a").GetProperty("Title").GetString());
            Assert.AreEqual(nameof(SerilogTest), eventos.Single(e => Mensaje(e) == "mensaje-b").GetProperty("Title").GetString());
        }

        [TestMethod]
        public void CreateLogger_SinCategoria_UsaElNombreDelProceso()
        {
            using (var logger = KitpymesSerilog.CreateLogger(ConFichero()))
            {
                logger.Error("sin-titulo");
            }

            var evento = LeerEventos().Single(e => Mensaje(e) == "sin-titulo");
            Assert.AreEqual(Process.GetCurrentProcess().ProcessName, evento.GetProperty("Title").GetString());
        }

        [TestMethod]
        public void Dispose_VaciaLosEventosPendientes()
        {
            using (var factory = KitpymesSerilog.CreateLoggerFactory(ConFichero()))
            {
                var logger = factory.CreateLogger("Vaciado");

                for (var i = 0; i < 20; i++)
                {
                    logger.LogError("evento-{Numero}", i);
                }
            }

            Assert.AreEqual(20, LeerEventos().Count(e => Mensaje(e) == "evento-{Numero}"));
        }

        [TestMethod]
        public void Log_NoBloqueaElHiloEnCadaLlamada()
        {
            using var factory = KitpymesSerilog.CreateLoggerFactory(new SerilogSettings());
            var logger = factory.CreateLogger("Rapido");
            var reloj = Stopwatch.StartNew();

            for (var i = 0; i < 5; i++)
            {
                logger.LogError("e");
                logger.LogInformation("i");
                logger.LogWarning("w");
                logger.LogDebug("d");
                logger.LogTrace("t");
            }

            reloj.Stop();

            Assert.IsTrue(reloj.ElapsedMilliseconds < 1000, $"Tardó {reloj.ElapsedMilliseconds} ms.");
        }

        [TestMethod]
        public void CreateLogger_NoCreaUnLoggerDeSerilogPorCategoria()
        {
            // El sink de fichero va envuelto en Serilog.Sinks.Async, que abre un hilo por logger de Serilog.
            using var factory = KitpymesSerilog.CreateLoggerFactory(ConFichero());
            factory.CreateLogger("calentar").LogError("calentar");
            var antes = Process.GetCurrentProcess().Threads.Count;

            for (var i = 0; i < 60; i++)
            {
                factory.CreateLogger("t" + i).LogError("x");
            }

            var despues = Process.GetCurrentProcess().Threads.Count;

            Assert.IsTrue(despues - antes < 20, $"Hilos antes: {antes}, después: {despues}.");
        }

        [TestMethod]
        public void Error_ConExcepcion_LaGuardaComoExcepcionDelEvento()
        {
            using (var factory = KitpymesSerilog.CreateLoggerFactory(ConFichero()))
            {
                factory.CreateLogger("Excepciones").LogError(new ArgumentException("fallo-dos"), "contexto");
            }

            var evento = LeerEventos().Single();
            StringAssert.Contains(evento.GetProperty("@x").GetString(), "System.ArgumentException: fallo-dos");
            Assert.AreEqual("contexto", Mensaje(evento));
        }

        [TestMethod]
        public void Log_NivelesDeMicrosoftEnSerilog()
        {
            var settings = ConFichero();
            settings.MinimumLevel = "Trace";
            settings.File.MinimumLevel = "Trace";

            using (var factory = KitpymesSerilog.CreateLoggerFactory(settings))
            {
                var logger = factory.CreateLogger("Niveles");
                logger.LogTrace("t");
                logger.LogDebug("d");
                logger.LogInformation("i");
                logger.LogWarning("w");
                logger.LogError("e");
                logger.LogCritical("c");
            }

            CollectionAssert.AreEqual(
                new[] { "Verbose", "Debug", null, "Warning", "Error", "Fatal" },
                LeerEventos().Select(e => e.TryGetProperty("@l", out var l) ? l.GetString() : null).ToArray());
        }

        [TestMethod]
        public void Log_RespetaElNivelMinimoWarn()
        {
            var settings = ConFichero();
            settings.MinimumLevel = "Warn";
            settings.File.MinimumLevel = "Trace";

            using (var factory = KitpymesSerilog.CreateLoggerFactory(settings))
            {
                var logger = factory.CreateLogger("Warn");
                logger.LogInformation("no");
                logger.LogWarning("si");
            }

            Assert.AreEqual("si", Mensaje(LeerEventos().Single()));
        }

        [TestMethod]
        public void Log_ConSqlServerCaido_NoLanzaAlQueLlama()
        {
            var settings = new SerilogSettings();
            settings.SqlServer.Enabled = true;
            settings.SqlServer.ConnectionString = "Server=127.0.0.1,1;Database=Log;User Id=u;Password=p;Connect Timeout=1;TrustServerCertificate=True";

            using var factory = KitpymesSerilog.CreateLoggerFactory(settings);

            factory.CreateLogger("Sql").LogError("la base de datos no responde");
        }

        [TestMethod]
        [DataRow("Warn")]
        [DataRow("Warning")]
        [DataRow("warn")]
        [DataRow("WARNING")]
        public void ToMinimumLevel_Warn_EsWarning(string nivel)
        {
            Assert.AreEqual(LogEventLevel.Warning, nivel.ToMinimumLevel());
        }
        [TestMethod]
        public void ToMinimumLevel_LoggerLevelWarn_EsWarning()
        {
            Assert.AreEqual(LogEventLevel.Warning, LoggerLevel.Warn.ToMinimumLevel());
        }
        [TestMethod]
        [DataRow("Trace", LogEventLevel.Verbose)]
        [DataRow("Debug", LogEventLevel.Debug)]
        [DataRow("Info", LogEventLevel.Information)]
        [DataRow("Error", LogEventLevel.Error)]
        [DataRow("Fatal", LogEventLevel.Fatal)]
        [DataRow("Verbose", LogEventLevel.Verbose)]
        [DataRow("information", LogEventLevel.Information)]
        [DataRow("error", LogEventLevel.Error)]
        [DataRow("otro", LogEventLevel.Information)]
        [DataRow(null, LogEventLevel.Information)]
        public void ToMinimumLevel_ConvierteLosDemasNiveles(string? nivel, LogEventLevel esperado)
        {
            Assert.AreEqual(esperado, nivel.ToMinimumLevel());
        }
        [TestMethod]
        public void TitleEnricher_AnadeElTituloATodosLosEventos()
        {
            var enricher = new TitleEnricher("Titulo");
            var primero = Evento();
            var segundo = Evento();

            enricher.Enrich(primero, null!);
            enricher.Enrich(segundo, null!);

            Assert.AreEqual("\"Titulo\"", primero.Properties[TitleEnricher.TitlePropertyName].ToString());
            Assert.AreEqual("\"Titulo\"", segundo.Properties[TitleEnricher.TitlePropertyName].ToString());
        }
        [TestMethod]
        public void AddDefaultSettings_NoDejaElTituloEnElContextoDelHilo()
        {
            _ = new LoggerConfiguration().AddDefaultSettings("TituloFiltrado");
            var sink = new SinkEnMemoria();
            using var otro = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();

            otro.Information("otro logger");

            Assert.IsFalse(sink.Eventos.Single().Properties.ContainsKey("Title"));
        }
        [TestMethod]
        public void AddDefaultSettings_PoneElTituloEnLosEventosDeEseLogger()
        {
            var sink = new SinkEnMemoria();
            using var logger = new LoggerConfiguration().AddDefaultSettings("TituloPropio").WriteTo.Sink(sink).CreateLogger();

            logger.Information("uno");
            logger.Information("dos");

            Assert.IsTrue(sink.Eventos.All(e => e.Properties["Title"].ToString() == "\"TituloPropio\""));
            Assert.AreEqual(2, sink.Eventos.Count);
        }
        [TestMethod]
        public void Enrichers_AnadenSuPropiedadATodosLosEventos()
        {
            var enrichers = new ILogEventEnricher[] { new MachineNameEnricher(), new ProcessEnricher(), new ThreadEnricher() };
            var primero = Evento();
            var segundo = Evento();

            foreach (var enricher in enrichers)
            {
                enricher.Enrich(primero, new FabricaDePropiedades());
                enricher.Enrich(segundo, new FabricaDePropiedades());
            }

            foreach (var nombre in new[] { "MachineName", "Process", "Thread" })
            {
                Assert.IsTrue(primero.Properties.ContainsKey(nombre), "Primer evento sin " + nombre);
                Assert.IsTrue(segundo.Properties.ContainsKey(nombre), "Segundo evento sin " + nombre);
            }
        }
        [TestMethod]
        public void AddDefaultSettings_NoSustituyeElSelfLogDeLaAplicacion()
        {
            var escrito = new StringWriter();
            global::Serilog.Debugging.SelfLog.Enable(escrito);
            try
            {
                _ = new LoggerConfiguration().AddDefaultSettings("SelfLog");
                global::Serilog.Debugging.SelfLog.WriteLine("mensaje de la aplicacion");

                StringAssert.Contains(escrito.ToString(), "mensaje de la aplicacion");
            }
            finally
            {
                global::Serilog.Debugging.SelfLog.Disable();
            }
        }
        private static LogEvent Evento() =>
            new(DateTimeOffset.Now, LogEventLevel.Information, null, MessageTemplate.Empty, Array.Empty<LogEventProperty>());

        private static string Mensaje(JsonElement evento) => evento.GetProperty("@mt").GetString() ?? string.Empty;

        private SerilogSettings ConFichero()
        {
            var settings = new SerilogSettings();
            settings.File.Enabled = true;
            settings.File.FilePath = Path.Combine(directorio, ".log");
            settings.File.Interval = "Infinite";
            return settings;
        }

        private List<JsonElement> LeerEventos()
        {
            if (!Directory.Exists(directorio))
            {
                return new List<JsonElement>();
            }

            return Directory.GetFiles(directorio)
                .SelectMany(f =>
                {
                    using var stream = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
                })
                .Select(l => JsonDocument.Parse(l).RootElement.Clone())
                .ToList();
        }

        private sealed class FabricaDePropiedades : ILogEventPropertyFactory
        {
            public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
                new(name, new ScalarValue(value));
        }

        private sealed class SinkEnMemoria : ILogEventSink
        {
            public List<LogEvent> Eventos { get; } = new();

            public void Emit(LogEvent logEvent) => Eventos.Add(logEvent);
        }
    }
}
