using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Kitpymes.Core.Logger.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Kitpymes.Core.Logger.Serilog.Tests
{
    /// <summary>
    /// API nueva de Logger 2.3.0: ILogger&lt;T&gt; de Microsoft con Serilog y los datos sensibles ocultos en lo que llega a los sinks.
    /// </summary>
    [TestClass]
    public class LoggingBuilderTest
    {
        private const string Secreto = "s3cr3t";

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
        public void AddKitpymesSerilog_EscribeConILoggerDeMicrosoft_YLoLiberaElContenedor()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddKitpymesSerilog(ConFichero()));

            using (var provider = services.BuildServiceProvider())
            {
                var logger = provider.GetRequiredService<ILogger<LoggingBuilderTest>>();
                logger.LogInformation("Pedido {Id}", 5);
                logger.LogError(new InvalidOperationException("fallo"), "Con excepción");
            }

            var eventos = LeerEventos();
            Assert.AreEqual(2, eventos.Count);
            Assert.AreEqual(5, eventos[0].GetProperty("Id").GetInt32());
            Assert.AreEqual(nameof(LoggingBuilderTest), eventos[0].GetProperty("Title").GetString());
            Assert.AreEqual(typeof(LoggingBuilderTest).FullName, eventos[0].GetProperty("SourceContext").GetString());
            Assert.AreEqual(Environment.MachineName, eventos[0].GetProperty("MachineName").GetString());
            StringAssert.Contains(eventos[1].GetProperty("@x").GetString(), "System.InvalidOperationException: fallo");
        }

        [TestMethod]
        public void AddKitpymesSerilog_NoEscribeNiUnDatoSensible()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddKitpymesSerilog(ConFichero()));

            using (var provider = services.BuildServiceProvider())
            {
                var logger = provider.GetRequiredService<ILogger<LoggingBuilderTest>>();
                logger.LogInformation("Login {UserName} {Password}", "u", Secreto);
                logger.LogInformation("Petición {@Request}", new { UserName = "u", Password = Secreto, Datos = new { ApiKey = Secreto } });
                logger.LogInformation("Petición {Request}", new Credenciales("u", Secreto));
                logger.LogInformation("Mapa {Mapa}", new Dictionary<string, string> { ["user"] = "u", ["token"] = Secreto });
                logger.LogInformation("Lista {@Lista}", new[] { new { Pin = Secreto } });
                logger.LogInformation("Conexión {Cadena}", "Server=db;Password=" + Secreto + ";");
                logger.LogInformation("Cabecera Bearer " + Secreto);
                logger.LogInformation("JSON {Json}", "{\"password\":\"" + Secreto + "\"}");
                logger.LogInformation("Cliente {@Cliente}", new Cliente { Name = "Ana", Dni = Secreto });
            }

            var texto = LeerTexto();
            Assert.AreEqual(9, LeerEventos().Count);
            Assert.IsFalse(texto.Contains(Secreto, StringComparison.Ordinal), texto);
            StringAssert.Contains(texto, "\"Name\":\"Ana\"");
        }

        [TestMethod]
        public void JsonComoPlantilla_TambienSeOculta()
        {
            // El patrón del core (RequestDataBehaviour): el JSON de la petición va como texto del mensaje.
            using (var factory = KitpymesSerilog.CreateLoggerFactory(ConFichero()))
            {
#pragma warning disable CA2254, CA2017 // Es justo el caso que se prueba: una plantilla que no es fija (y sus llaves no son huecos).
                factory.CreateLogger("Login").LogInformation("LoginUserRequest => {\"UserName\":\"u\",\"Password\":\"" + Secreto + "\"}");
#pragma warning restore CA2254, CA2017
            }

            var texto = LeerTexto();
            Assert.AreEqual(1, LeerEventos().Count);
            Assert.IsFalse(texto.Contains(Secreto, StringComparison.Ordinal), texto);
        }

        [TestMethod]
        public void MinimumLevelOverride_PorDefectoMicrosoftYSystemEnWarning()
        {
            using (var factory = KitpymesSerilog.CreateLoggerFactory(ConFichero()))
            {
                factory.CreateLogger("Microsoft.AspNetCore.Hosting").LogInformation("ruido");
                factory.CreateLogger("System.Net.Http").LogInformation("ruido");
                factory.CreateLogger("Microsoft.AspNetCore.Hosting").LogWarning("aviso");
                factory.CreateLogger("App.Servicio").LogInformation("propio");
            }

            CollectionAssert.AreEqual(new[] { "aviso", "propio" }, LeerEventos().Select(e => e.GetProperty("@mt").GetString()).ToArray());
        }

        [TestMethod]
        public void MinimumLevelOverride_DeLaConfiguracionCambiaElDefecto()
        {
            var settings = ConFichero();
            settings.MinimumLevelOverride = new Dictionary<string, string> { ["Microsoft"] = "Information", ["App"] = "Error" };

            using (var factory = KitpymesSerilog.CreateLoggerFactory(settings))
            {
                factory.CreateLogger("Microsoft.AspNetCore.Hosting").LogInformation("microsoft");
                factory.CreateLogger("System.Net.Http").LogInformation("system");
                factory.CreateLogger("App.Servicio").LogWarning("app");
                factory.CreateLogger("Otra").LogInformation("otra");
            }

            CollectionAssert.AreEqual(new[] { "microsoft", "otra" }, LeerEventos().Select(e => e.GetProperty("@mt").GetString()).ToArray());
        }

        [TestMethod]
        public void CreateLoggerFactory_ConOpciones()
        {
            var ruta = Path.Combine(directorio, ".log");

            using (var factory = KitpymesSerilog.CreateLoggerFactory(o => o.AddMinimunLevel(LoggerLevel.Trace).AddFile(ruta, LoggerLevel.Trace, LoggerFileInterval.Infinite)))
            {
                factory.CreateLogger<LoggingBuilderTest>().LogTrace("traza");
            }

            Assert.AreEqual("traza", LeerEventos().Single().GetProperty("@mt").GetString());
        }

        [TestMethod]
        public void AddKitpymesSerilog_ConOpciones()
        {
            var ruta = Path.Combine(directorio, ".log");
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddKitpymesSerilog(o => o.AddFile(ruta, LoggerLevel.Info, LoggerFileInterval.Infinite)));

            using (var provider = services.BuildServiceProvider())
            {
                provider.GetRequiredService<ILogger<LoggingBuilderTest>>().LogInformation("info");
                provider.GetRequiredService<ILogger<LoggingBuilderTest>>().LogDebug("debug");
            }

            Assert.AreEqual("info", LeerEventos().Single().GetProperty("@mt").GetString());
        }

        [TestMethod]
        public void AddKitpymesSerilog_NoCreaElLoggerHastaQueSePide()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddKitpymesSerilog(ConFichero()));

            using (services.BuildServiceProvider())
            {
            }

            Assert.IsFalse(Directory.Exists(directorio));
        }

        [TestMethod]
        public void AddKitpymesLogging_ConfiguracionActivada_RegistraSerilog()
        {
            var configuration = Configuracion(enabled: "true", serilog: "true");
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddKitpymesLogging(configuration));

            using (var provider = services.BuildServiceProvider())
            {
                provider.GetRequiredService<ILogger<LoggingBuilderTest>>().LogError("desde appsettings");
            }

            Assert.AreEqual("desde appsettings", LeerEventos().Single().GetProperty("@mt").GetString());
        }

        [TestMethod]
        [DataRow("false", "true")]
        [DataRow("true", "false")]
        [DataRow(null, null)]
        public void AddKitpymesLogging_SinActivar_NoRegistraNada(string? enabled, string? serilog)
        {
            var services = new ServiceCollection();

            services.AddLogging(builder => builder.AddKitpymesLogging(Configuracion(enabled, serilog)));

            Assert.IsFalse(services.Any(d => d.ServiceType == typeof(ILoggerProvider)));
        }

        [TestMethod]
        public void AddKitpymesLogging_ConOpcionesYSettings()
        {
            var services = new ServiceCollection();

            services.AddLogging(builder => builder
                .AddKitpymesLogging(o => o.UseSerilog(s => s.AddConsole()))
                .AddKitpymesLogging((LoggerSettings?)null)
                .AddKitpymesLogging(new LoggerSettings { Enabled = true, IsSerilogEnabled = true }));

            // Serilog se registra una sola vez aunque se pida dos.
            Assert.AreEqual(1, services.Count(d => d.ServiceType == typeof(ILoggerProvider)));
        }

        [TestMethod]
        public void AddKitpymesSerilog_DosVeces_EscribeCadaEventoUnaVez()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddKitpymesSerilog(ConFichero()).AddKitpymesSerilog(ConFichero()).AddKitpymesLogging(new LoggerSettings { Enabled = true, IsSerilogEnabled = true }));

            using (var provider = services.BuildServiceProvider())
            {
                provider.GetRequiredService<ILogger<LoggingBuilderTest>>().LogError("una vez");
            }

            Assert.AreEqual(1, LeerEventos().Count);
        }

        [TestMethod]
        public void BeginScope_NoEscribeElSecretoDelScope()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddKitpymesSerilog(ConFichero()));

            using (var provider = services.BuildServiceProvider())
            {
                var logger = provider.GetRequiredService<ILogger<LoggingBuilderTest>>();
                using (logger.BeginScope("Usuario {UserName} con {Pwd:D} y {Otro}", "ana", Secreto, 5))
                using (logger.BeginScope(new Dictionary<string, object> { ["Token"] = Secreto, ["Pedido"] = 7 }))
                using (logger.BeginScope("token=" + Secreto))
                {
                    logger.LogInformation("Dentro del scope");
                }
            }

            var texto = LeerTexto();
            var evento = LeerEventos().Single();
            Assert.IsFalse(texto.Contains(Secreto, StringComparison.Ordinal), texto);
            Assert.AreEqual("ana", evento.GetProperty("UserName").GetString());
            Assert.AreEqual(7, evento.GetProperty("Pedido").GetInt32());
            StringAssert.Contains(texto, "Usuario ana con *** y 5");
        }

        [TestMethod]
        public void BeginScope_HuecoDetrasDeClave_YTextoComoMicrosoft()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddKitpymesSerilog(ConFichero()));

            using (var provider = services.BuildServiceProvider())
            {
                var logger = provider.GetRequiredService<ILogger<LoggingBuilderTest>>();
                using (logger.BeginScope("pwd={Valor}", Secreto))
                using (logger.BeginScope("{Id,5}|{Lista}|{Decimal}|{Nulo}", 7, new List<int> { 1, 2 }, 1.5, null!))
                {
                    logger.LogInformation("Dentro");
                }
            }

            var texto = LeerTexto();
            Assert.IsFalse(texto.Contains(Secreto, StringComparison.Ordinal), texto);
            StringAssert.Contains(texto, "    7|1, 2|1.5|(null)");
        }

        [TestMethod]
        public void BeginScope_ConservaLosScopesDeActividad()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder
                .Configure(o => o.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId)
                .AddKitpymesSerilog(ConFichero()));
            using var actividad = new System.Diagnostics.Activity("Prueba").Start();

            using (var provider = services.BuildServiceProvider())
            {
                var logger = provider.GetRequiredService<ILogger<LoggingBuilderTest>>();
                using (logger.BeginScope("token={Valor}", Secreto))
                {
                    logger.LogInformation("Con actividad");
                }
            }

            var evento = LeerEventos().Single();
            Assert.AreEqual(actividad.TraceId.ToString(), evento.GetProperty("TraceId").GetString());
            Assert.AreEqual(actividad.SpanId.ToString(), evento.GetProperty("SpanId").GetString());
            Assert.IsFalse(LeerTexto().Contains(Secreto, StringComparison.Ordinal), LeerTexto());
        }

        [TestMethod]
        public void MinimumLevelOverride_NivelQueNoExiste_Lanza()
        {
            var settings = ConFichero();
            settings.MinimumLevelOverride = new Dictionary<string, string> { ["Microsoft"] = "Warnig" };

            var error = Assert.ThrowsExactly<ArgumentException>(() => KitpymesSerilog.CreateLogger(settings));

            StringAssert.Contains(error.Message, "Warnig");
            Assert.IsFalse(Directory.Exists(directorio));
        }

        [TestMethod]
        public void DefaultMinimumLevelOverride_NoSePuedeCambiar()
        {
            Assert.IsFalse(KitpymesSerilog.DefaultMinimumLevelOverride is IDictionary<string, string> { IsReadOnly: false });
            Assert.AreEqual("Warning", KitpymesSerilog.DefaultMinimumLevelOverride["Microsoft"]);
            Assert.AreEqual("Warning", KitpymesSerilog.DefaultMinimumLevelOverride["System"]);
            Assert.AreEqual(2, KitpymesSerilog.DefaultMinimumLevelOverride.Count);
        }

        [TestMethod]
        public void SensitiveHeredado_EnUnaPropiedadSobrescrita_SeOculta()
        {
            using (var factory = KitpymesSerilog.CreateLoggerFactory(ConFichero()))
            {
                factory.CreateLogger("Herencia").LogInformation("Cliente {@Cliente}", new ClienteDerivado { Dni = Secreto, Nombre = "Ana" });
            }

            var texto = LeerTexto();
            Assert.IsFalse(texto.Contains(Secreto, StringComparison.Ordinal), texto);
            StringAssert.Contains(texto, "\"Nombre\":\"Ana\"");
        }

        [TestMethod]
        public void Argumentos_Nulos_Lanzan()
        {
            var services = new ServiceCollection();
            ILoggingBuilder? builder = null;
            services.AddLogging(b => builder = b);

            Assert.ThrowsExactly<ArgumentNullException>(() => KitpymesSerilog.CreateLogger(null!));
            Assert.ThrowsExactly<ArgumentNullException>(() => KitpymesSerilog.CreateLoggerFactory((Action<SerilogOptions>)null!));
            Assert.ThrowsExactly<ArgumentNullException>(() => builder!.AddKitpymesSerilog((SerilogSettings)null!));
            Assert.ThrowsExactly<ArgumentNullException>(() => builder!.AddKitpymesSerilog((Action<SerilogOptions>)null!));
            Assert.ThrowsExactly<ArgumentNullException>(() => ((ILoggingBuilder)null!).AddKitpymesSerilog(new SerilogSettings()));
            Assert.ThrowsExactly<ArgumentNullException>(() => builder!.AddKitpymesLogging((IConfiguration)null!));
            Assert.ThrowsExactly<ArgumentNullException>(() => builder!.AddKitpymesLogging((Action<LoggerOptions>)null!));
            Assert.ThrowsExactly<ArgumentNullException>(() => ((ILoggingBuilder)null!).AddKitpymesLogging(new LoggerSettings()));
        }

        private IConfiguration Configuracion(string? enabled, string? serilog) => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LoggerSettings:Enabled"] = enabled,
                ["LoggerSettings:IsSerilogEnabled"] = serilog,
                ["LoggerSettings:SerilogSettings:File:Enabled"] = "true",
                ["LoggerSettings:SerilogSettings:File:Interval"] = "Infinite",
                ["LoggerSettings:SerilogSettings:File:FilePath"] = Path.Combine(directorio, ".log"),
            })
            .Build();

        private SerilogSettings ConFichero()
        {
            var settings = new SerilogSettings { MinimumLevel = "Trace" };
            settings.File.Enabled = true;
            settings.File.MinimumLevel = "Trace";
            settings.File.FilePath = Path.Combine(directorio, ".log");
            settings.File.Interval = "Infinite";
            return settings;
        }

        private string LeerTexto() => !Directory.Exists(directorio)
            ? string.Empty
            : string.Concat(Directory.GetFiles(directorio).Select(f =>
            {
                using var stream = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }));

        private List<JsonElement> LeerEventos() => LeerTexto()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => JsonDocument.Parse(l).RootElement.Clone())
            .ToList();

        private sealed record Credenciales(string UserName, string Password);

        private class ClienteBase
        {
            [Sensitive]
            public virtual string Dni { get; set; } = string.Empty;

            public string Nombre { get; set; } = string.Empty;
        }

        private sealed class ClienteDerivado : ClienteBase
        {
            public override string Dni { get; set; } = string.Empty;
        }

        private sealed class Cliente
        {
            public string Name { get; set; } = string.Empty;

            [Sensitive]
            public string Dni { get; set; } = string.Empty;
        }
    }
}
