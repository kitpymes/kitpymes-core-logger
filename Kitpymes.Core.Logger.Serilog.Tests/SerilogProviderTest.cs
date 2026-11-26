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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Kitpymes.Core.Logger.Serilog.Tests
{
    /// <summary>
    /// Ciclo de vida del proveedor de Serilog (Logger 2.2.0): un logger de Serilog por proveedor, un logger
    /// independiente por cada CreateLogger, sin esperas en cada llamada y con los eventos vaciados al liberar.
    /// </summary>
    [TestClass]
    public class SerilogProviderTest
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
        public void LoadSerilog_RegistraUnSingletonYDevuelveLaInstanciaQueResuelveElContenedor()
        {
            var services = new ServiceCollection();

            var devuelto = services.LoadSerilog(new SerilogSettings());

            var descriptor = services.Single(d => d.ServiceType == typeof(ILoggerService));
            Assert.AreEqual(ServiceLifetime.Singleton, descriptor.Lifetime);
            using var provider = services.BuildServiceProvider();
            Assert.IsNotNull(devuelto);
            Assert.AreSame(devuelto, provider.GetRequiredService<ILoggerService>());
            Assert.AreSame(devuelto, provider.GetRequiredService<ILoggerService>());
        }

        [TestMethod]
        public void LoadSerilog_SiYaHabiaUnLoggerService_NoRegistraOtroYDevuelveElExistente()
        {
            var services = new ServiceCollection();
            var primero = services.LoadSerilog(new SerilogSettings());

            var segundo = services.LoadSerilog(new SerilogSettings());

            Assert.AreEqual(1, services.Count(d => d.ServiceType == typeof(ILoggerService)));
            Assert.AreSame(primero, segundo);
        }

        [TestMethod]
        public void CreateLogger_DevuelveUnLoggerNuevoEnCadaLlamada()
        {
            var service = new SerilogProvider(new SerilogSettings());

            var a = service.CreateLogger("A");
            var b = service.CreateLogger("B");

            Assert.AreNotSame(a, b);
            Assert.AreNotSame(service, a);
            Assert.AreNotSame(service, b);
        }

        [TestMethod]
        public void CreateLogger_CadaLoggerConservaSuTitulo()
        {
            var service = new SerilogProvider(ConFichero());

            var a = service.CreateLogger("TituloA");
            var b = service.CreateLogger<SerilogProviderTest>();
            a.Error("mensaje-a");
            b.Error("mensaje-b");
            Liberar(service);

            var eventos = LeerEventos();
            Assert.AreEqual("TituloA", eventos.Single(e => Mensaje(e) == "mensaje-a").GetProperty("Title").GetString());
            Assert.AreEqual(nameof(SerilogProviderTest), eventos.Single(e => Mensaje(e) == "mensaje-b").GetProperty("Title").GetString());
        }

        [TestMethod]
        public void CreateLogger_ConTituloVacio_UsaElNombreDelProceso()
        {
            var service = new SerilogProvider(ConFichero());

            service.CreateLogger(" ").Error("sin-titulo");
            Liberar(service);

            var evento = LeerEventos().Single(e => Mensaje(e) == "sin-titulo");
            Assert.AreEqual(Process.GetCurrentProcess().ProcessName, evento.GetProperty("Title").GetString());
        }

        [TestMethod]
        public void Dispose_VaciaLosEventosPendientesDeTodosLosLoggers()
        {
            var service = new SerilogProvider(ConFichero());
            var logger = service.CreateLogger("Vaciado");

            for (var i = 0; i < 20; i++)
            {
                logger.Error("evento-" + i);
            }

            Liberar(service);

            Assert.AreEqual(20, LeerEventos().Count(e => Mensaje(e).StartsWith("evento-", StringComparison.Ordinal)));
        }

        [TestMethod]
        public void Dispose_DosVeces_NoLanza()
        {
            var service = new SerilogProvider(ConFichero());
            service.CreateLogger("x").Error("uno");

            Liberar(service);
            Liberar(service);

            Assert.AreEqual(1, LeerEventos().Count);
        }

        [TestMethod]
        public void Log_NoBloqueaElHiloEnCadaLlamada()
        {
            var logger = new SerilogProvider(new SerilogSettings()).CreateLogger("Rapido");
            var reloj = Stopwatch.StartNew();

            for (var i = 0; i < 5; i++)
            {
                logger.Error("e").Info("i").Warning("w").Debug("d").Trace("t");
            }

            reloj.Stop();

            // 2.1.0 esperaba 1 s en cada Error y 50 ms en los demás: más de 6 s para estas 25 llamadas.
            Assert.IsTrue(reloj.ElapsedMilliseconds < 1000, $"Tardó {reloj.ElapsedMilliseconds} ms.");
        }

        [TestMethod]
        public void CreateLogger_NoCreaUnLoggerDeSerilogEnCadaLlamada()
        {
            // El sink de fichero va envuelto en Serilog.Sinks.Async, que abre un hilo propio por logger de Serilog.
            // 2.1.0 creaba uno en cada CreateLogger y no lo liberaba nunca: los hilos crecían sin límite.
            var service = new SerilogProvider(ConFichero());
            service.CreateLogger("calentar").Error("calentar");
            var antes = Process.GetCurrentProcess().Threads.Count;

            for (var i = 0; i < 60; i++)
            {
                service.CreateLogger("t" + i).Error("x");
            }

            var despues = Process.GetCurrentProcess().Threads.Count;
            Liberar(service);

            Assert.IsTrue(despues - antes < 20, $"Hilos antes: {antes}, después: {despues}.");
        }

        [TestMethod]
        public void LoggerDelServicio_SinCreateLogger_NoEscribeNiLanza()
        {
            var service = new SerilogProvider(ConFichero());

            ((Abstractions.ILogger)service).Error("raiz").Info("raiz").Error(new InvalidOperationException("raiz"));
            Liberar(service);

            Assert.AreEqual(0, LeerEventos().Count);
        }

        [TestMethod]
        public void Error_ConExcepcion_LaGuardaComoExcepcionDelEvento()
        {
            var service = new SerilogProvider(ConFichero());
            var logger = service.CreateLogger("Excepciones");

            logger.Error(new InvalidOperationException("fallo-uno"));
            logger.Error("contexto", new ArgumentException("fallo-dos"));
            Liberar(service);

            var eventos = LeerEventos();
            Assert.AreEqual(2, eventos.Count);
            StringAssert.Contains(eventos[0].GetProperty("@x").GetString(), "System.InvalidOperationException: fallo-uno");
            StringAssert.Contains(eventos[1].GetProperty("@x").GetString(), "System.ArgumentException: fallo-dos");
            StringAssert.StartsWith(eventos[1].GetProperty("@mt").GetString(), "contexto");
        }

        [TestMethod]
        public void Log_ConDatosYPlantilla_EscribeElNivelYLasPropiedades()
        {
            var settings = ConFichero();
            settings.File.MinimumLevel = "Trace";
            settings.MinimumLevel = "Trace";
            var service = new SerilogProvider(settings);
            var logger = service.CreateLogger("Niveles");

            logger
                .Trace("t", 1).Trace("t0")
                .Debug("d", 2).Debug("d0")
                .Info("i", 3).Info("i0").Info("evento-i", "valor {Valor}", 30)
                .Warning("w", 4).Warning("w0")
                .Error("e", 5).Error("e0").Error("evento-e", "valor {Valor}", 50);
            Liberar(service);

            var eventos = LeerEventos();
            CollectionAssert.AreEqual(
                new[] { "Verbose", "Verbose", "Debug", "Debug", null, null, null, "Warning", "Warning", "Error", "Error", "Error" },
                eventos.Select(e => e.TryGetProperty("@l", out var l) ? l.GetString() : null).ToArray());
            CollectionAssert.AreEqual(
                new[] { 1, 0, 2, 0, 3, 0, 0, 4, 0, 5, 0, 0 },
                eventos.Select(e => e.TryGetProperty("Data", out var d) ? d.GetInt32() : 0).ToArray());
            Assert.AreEqual("evento-i", eventos[6].GetProperty("EventName").GetString());
            Assert.AreEqual(30, eventos[6].GetProperty("Valor").GetInt32());
            Assert.AreEqual("evento-e", eventos[11].GetProperty("EventName").GetString());
            Assert.AreEqual(50, eventos[11].GetProperty("Valor").GetInt32());
        }

        [TestMethod]
        public void Log_RespetaElNivelMinimoWarn()
        {
            var settings = ConFichero();
            settings.MinimumLevel = "Warn";
            settings.File.MinimumLevel = "Trace";
            var service = new SerilogProvider(settings);
            var logger = service.CreateLogger("Warn");

            logger.Info("no").Warning("si");
            Liberar(service);

            Assert.AreEqual("si", Mensaje(LeerEventos().Single()));
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
        public void CreateLogger_CadaEventoLlevaElNombreDeLaMaquina()
        {
            var service = new SerilogProvider(ConFichero());
            var logger = service.CreateLogger("Maquina");

            logger.Error("uno").Error("dos").Error("tres");
            Liberar(service);

            var eventos = LeerEventos();
            Assert.AreEqual(3, eventos.Count);
            Assert.IsTrue(eventos.All(e => e.GetProperty("MachineName").GetString() == Environment.MachineName));
        }

        [TestMethod]
        public void Log_ConSqlServerCaido_NoLanzaAlQueLlama()
        {
            var settings = new SerilogSettings();
            settings.SqlServer.Enabled = true;
            settings.SqlServer.ConnectionString = "Server=127.0.0.1,1;Database=Log;User Id=u;Password=p;Connect Timeout=1;TrustServerCertificate=True";
            var service = new SerilogProvider(settings);
            var logger = service.CreateLogger("Sql");

            logger.Error("la base de datos no responde");
            Liberar(service);
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

        [TestMethod]
        public void LoadLogger_ConUseSerilog_RegistraElLogger()
        {
            var services = new ServiceCollection();

            services.LoadLogger(options => options.UseSerilog(_ => { }));

            Assert.AreEqual(1, services.Count(d => d.ServiceType == typeof(ILoggerService)));
        }

        private static LogEvent Evento() =>
            new(DateTimeOffset.Now, LogEventLevel.Information, null, MessageTemplate.Empty, Array.Empty<LogEventProperty>());

        // Tolerante: contra 2.1.0 (sin IDisposable) los tests fallan por su comprobación, no por el cast.
        private static void Liberar(object service) => (service as IDisposable)?.Dispose();

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
