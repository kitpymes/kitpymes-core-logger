using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using global::Serilog;
using global::Serilog.Events;
using Kitpymes.Core.Logger.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Kitpymes.Core.Logger.Serilog.Tests
{
    /// <summary>
    /// Ajustes, opciones, conversiones y extensiones de LoggerConfiguration: valores por defecto, lo que ignoran y lo que lanzan.
    /// </summary>
    [TestClass]
    public class SettingsTest
    {
        [TestMethod]
        public void Ajustes_PorDefecto()
        {
            var s = new SerilogSettings();

            Assert.AreEqual("Info", s.MinimumLevel);
            Assert.IsNull(s.MinimumLevelOverride);
            Assert.AreEqual(false, s.Console.Enabled);
            Assert.AreEqual("Info", s.Console.MinimumLevel);
            Assert.AreEqual(SerilogConsoleSettings.DefaultOutputTemplate, s.Console.OutputTemplate);
            Assert.AreEqual(false, s.File.Enabled);
            Assert.AreEqual(SerilogFileSettings.DefaultFilePath, s.File.FilePath);
            Assert.AreEqual("Error", s.File.MinimumLevel);
            Assert.AreEqual("Day", s.File.Interval);
            Assert.AreEqual(false, s.Email.Enabled);
            Assert.AreEqual(true, s.Email.EnableSsl);
            Assert.AreEqual(465, s.Email.Port);
            Assert.AreEqual(false, s.Email.IsBodyHtml);
            Assert.AreEqual("Log Error", s.Email.Subject);
            Assert.AreEqual("Error", s.Email.MinimumLevel);
            Assert.AreEqual(SerilogEmailSettings.DefaultOutputTemplate, s.Email.OutputTemplate);
            Assert.AreEqual(false, s.SqlServer.Enabled);
            Assert.AreEqual("dbo", s.SqlServer.SchemaName);
            Assert.AreEqual("Log", s.SqlServer.TableName);
            Assert.AreEqual("Error", s.SqlServer.MinimumLevel);
            Assert.AreEqual(false, s.SqlServer.AutoCreateDB);
        }

        [TestMethod]
        public void Ajustes_NulosOVacios_ConservanElValorAnterior()
        {
            var s = new SerilogSettings();
            s.MinimumLevel = "Debug";
            s.Console.Enabled = true;
            s.Console.MinimumLevel = "Debug";
            s.Console.OutputTemplate = "{Message}";
            s.File.Enabled = true;
            s.File.FilePath = "a.log";
            s.File.MinimumLevel = "Debug";
            s.File.Interval = "Hour";
            s.Email.Enabled = true;
            s.Email.EnableSsl = false;
            s.Email.Port = 25;
            s.Email.IsBodyHtml = true;
            s.Email.Subject = "Asunto";
            s.Email.MinimumLevel = "Debug";
            s.Email.OutputTemplate = "{Message}";
            s.SqlServer.Enabled = true;
            s.SqlServer.SchemaName = "log";
            s.SqlServer.TableName = "Eventos";
            s.SqlServer.MinimumLevel = "Debug";
            s.SqlServer.AutoCreateDB = true;

            s.MinimumLevel = " ";
            s.Console.Enabled = null;
            s.Console.MinimumLevel = "";
            s.Console.OutputTemplate = null;
            s.File.Enabled = null;
            s.File.FilePath = " ";
            s.File.MinimumLevel = null;
            s.File.Interval = "";
            s.Email.Enabled = null;
            s.Email.EnableSsl = null;
            s.Email.Port = null;
            s.Email.IsBodyHtml = null;
            s.Email.Subject = " ";
            s.Email.MinimumLevel = null;
            s.Email.OutputTemplate = "";
            s.SqlServer.Enabled = null;
            s.SqlServer.SchemaName = null;
            s.SqlServer.TableName = " ";
            s.SqlServer.MinimumLevel = "";
            s.SqlServer.AutoCreateDB = null;

            Assert.AreEqual("Debug", s.MinimumLevel);
            Assert.AreEqual(true, s.Console.Enabled);
            Assert.AreEqual("Debug", s.Console.MinimumLevel);
            Assert.AreEqual("{Message}", s.Console.OutputTemplate);
            Assert.AreEqual(true, s.File.Enabled);
            Assert.AreEqual("a.log", s.File.FilePath);
            Assert.AreEqual("Debug", s.File.MinimumLevel);
            Assert.AreEqual("Hour", s.File.Interval);
            Assert.AreEqual(true, s.Email.Enabled);
            Assert.AreEqual(false, s.Email.EnableSsl);
            Assert.AreEqual(25, s.Email.Port);
            Assert.AreEqual(true, s.Email.IsBodyHtml);
            Assert.AreEqual("Asunto", s.Email.Subject);
            Assert.AreEqual("Debug", s.Email.MinimumLevel);
            Assert.AreEqual("{Message}", s.Email.OutputTemplate);
            Assert.AreEqual(true, s.SqlServer.Enabled);
            Assert.AreEqual("log", s.SqlServer.SchemaName);
            Assert.AreEqual("Eventos", s.SqlServer.TableName);
            Assert.AreEqual("Debug", s.SqlServer.MinimumLevel);
            Assert.AreEqual(true, s.SqlServer.AutoCreateDB);
        }

        [TestMethod]
        public void SerilogOptions_RellenaLosAjustes()
        {
            var o = new SerilogOptions()
                .AddMinimunLevel(LoggerLevel.Debug)
                .AddConsole(LoggerLevel.Warn, "{Message}")
                .AddFile("f.log", LoggerLevel.Info, LoggerFileInterval.Month)
                .AddEmail("u", "p", "smtp", "de@x", "a@x", enableSsl: false, port: 587, subject: "S", isBodyHtml: true, loggerMinimumLevel: LoggerLevel.Trace, outputTemplate: "{Level}")
                .AddSqlServer("Server=x", "log", "T", autoCreateDb: true, loggerMinimumLevel: LoggerLevel.Warn);
            var s = o.SerilogSettings;

            Assert.AreEqual("Debug", s.MinimumLevel);
            Assert.AreEqual(true, s.Console.Enabled);
            Assert.AreEqual("Warn", s.Console.MinimumLevel);
            Assert.AreEqual("{Message}", s.Console.OutputTemplate);
            Assert.AreEqual(true, s.File.Enabled);
            Assert.AreEqual("f.log", s.File.FilePath);
            Assert.AreEqual("Info", s.File.MinimumLevel);
            Assert.AreEqual("Month", s.File.Interval);
            Assert.AreEqual(true, s.Email.Enabled);
            Assert.AreEqual("u", s.Email.UserName);
            Assert.AreEqual("p", s.Email.Password);
            Assert.AreEqual("smtp", s.Email.Server);
            Assert.AreEqual("de@x", s.Email.From);
            Assert.AreEqual("a@x", s.Email.To);
            Assert.AreEqual(false, s.Email.EnableSsl);
            Assert.AreEqual(587, s.Email.Port);
            Assert.AreEqual("S", s.Email.Subject);
            Assert.AreEqual(true, s.Email.IsBodyHtml);
            Assert.AreEqual("Trace", s.Email.MinimumLevel);
            Assert.AreEqual("{Level}", s.Email.OutputTemplate);
            Assert.AreEqual(true, s.SqlServer.Enabled);
            Assert.AreEqual("Server=x", s.SqlServer.ConnectionString);
            Assert.AreEqual("log", s.SqlServer.SchemaName);
            Assert.AreEqual("T", s.SqlServer.TableName);
            Assert.AreEqual(true, s.SqlServer.AutoCreateDB);
            Assert.AreEqual("Warn", s.SqlServer.MinimumLevel);
        }

        [TestMethod]
        public void SerilogOptions_PorDefecto()
        {
            var s = new SerilogOptions().AddMinimunLevel().AddConsole().AddFile().AddSqlServer("Server=x").SerilogSettings;

            Assert.AreEqual("Info", s.MinimumLevel);
            Assert.AreEqual("Info", s.Console.MinimumLevel);
            Assert.AreEqual(SerilogFileSettings.DefaultFilePath, s.File.FilePath);
            Assert.AreEqual("Error", s.File.MinimumLevel);
            Assert.AreEqual("Day", s.File.Interval);
            Assert.AreEqual("dbo", s.SqlServer.SchemaName);
            Assert.AreEqual("Log", s.SqlServer.TableName);
            Assert.AreEqual(false, s.SqlServer.AutoCreateDB);
            Assert.AreEqual("Error", s.SqlServer.MinimumLevel);
        }

        [TestMethod]
        public void LoggerSettings_YOpciones()
        {
            var vacio = new LoggerSettings { Enabled = true, IsSerilogEnabled = true };
            vacio.Enabled = null;
            vacio.IsSerilogEnabled = null;
            var sinSerilog = new LoggerSettings();
            var conOpciones = new LoggerOptions().UseSerilog(s => s.AddConsole()).LoggerSettings;
            var conNulo = new LoggerOptions().UseSerilog(null!).LoggerSettings;

            Assert.AreEqual(true, vacio.Enabled);
            Assert.AreEqual(true, vacio.IsSerilogEnabled);
            Assert.IsNotNull(vacio.SerilogSettings);
            Assert.AreEqual(false, sinSerilog.Enabled);
            Assert.AreEqual(false, sinSerilog.IsSerilogEnabled);
            Assert.IsNull(sinSerilog.SerilogSettings);
            Assert.AreEqual(true, conOpciones.Enabled);
            Assert.AreEqual(true, conOpciones.SerilogSettings!.Console.Enabled);
            Assert.AreEqual(true, conNulo.Enabled);
            Assert.AreEqual(false, conNulo.SerilogSettings!.Console.Enabled);
        }

        [TestMethod]
        [DataRow("Infinite", RollingInterval.Infinite)]
        [DataRow("Year", RollingInterval.Year)]
        [DataRow("Month", RollingInterval.Month)]
        [DataRow("Day", RollingInterval.Day)]
        [DataRow("Hour", RollingInterval.Hour)]
        [DataRow("Minute", RollingInterval.Minute)]
        [DataRow("otro", RollingInterval.Day)]
        [DataRow(null, RollingInterval.Day)]
        public void ToRollingInterval_Texto(string? intervalo, RollingInterval esperado)
        {
            Assert.AreEqual(esperado, intervalo.ToRollingInterval());
        }

        [TestMethod]
        public void ToRollingInterval_Enumerado()
        {
            CollectionAssert.AreEqual(
                new[] { RollingInterval.Infinite, RollingInterval.Year, RollingInterval.Month, RollingInterval.Day, RollingInterval.Hour, RollingInterval.Minute },
                Enum.GetValues<LoggerFileInterval>().Select(i => i.ToRollingInterval()).ToArray());
            Assert.AreEqual(RollingInterval.Hour, "otro".ToRollingInterval(RollingInterval.Hour));
            Assert.AreEqual(LogEventLevel.Error, "otro".ToMinimumLevel(LogEventLevel.Error));
        }

        [TestMethod]
        public void ToMinimumLevel_Enumerado()
        {
            CollectionAssert.AreEqual(
                new[] { LogEventLevel.Verbose, LogEventLevel.Debug, LogEventLevel.Information, LogEventLevel.Warning, LogEventLevel.Error },
                Enum.GetValues<LoggerLevel>().Select(l => l.ToMinimumLevel()).ToArray());
        }

        [TestMethod]
        public void AddConsole_Activado_EscribeEnLaConsolaConSuNivel()
        {
            var anterior = Console.Out;
            var salida = new StringWriter();
            Console.SetOut(salida);
            try
            {
                var settings = new SerilogConsoleSettings { Enabled = true, MinimumLevel = "Warn", OutputTemplate = "[{Level}] {Message}" };
                using (var logger = new LoggerConfiguration().MinimumLevel.Verbose().AddConsole(settings).CreateLogger())
                {
                    logger.Information("no-sale");
                    logger.Warning("si-sale");
                }

                using (var apagado = new LoggerConfiguration().AddConsole(new SerilogConsoleSettings()).AddConsole(null).CreateLogger())
                {
                    apagado.Error("apagado");
                }
            }
            finally
            {
                Console.SetOut(anterior);
            }

            var texto = salida.ToString();
            StringAssert.Contains(texto, "[Warning] si-sale");
            Assert.IsFalse(texto.Contains("no-sale", StringComparison.Ordinal));
            Assert.IsFalse(texto.Contains("apagado", StringComparison.Ordinal));
        }

        [TestMethod]
        public void AddFile_FicheroCompartido_OtroProcesoPuedeEscribirlo()
        {
            var directorio = Path.Combine(Path.GetTempPath(), "kpy-logger-" + Guid.NewGuid().ToString("N"));
            var settings = new SerilogFileSettings { Enabled = true, FilePath = Path.Combine(directorio, "app.log"), Interval = "Infinite", MinimumLevel = "Info" };

            using (var logger = new LoggerConfiguration().AddFile(settings).CreateLogger())
            {
                logger.Information("uno");
                var fichero = Path.Combine(directorio, "app.log");
                var reloj = Stopwatch.StartNew();
                while (!File.Exists(fichero) && reloj.ElapsedMilliseconds < 5000)
                {
                    Thread.Sleep(20);
                }

                // shared: true abre con FileShare.ReadWrite; sin él, otra escritura daría IOException.
                using var otro = new FileStream(fichero, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                Assert.IsTrue(otro.CanWrite);
            }

            Directory.Delete(directorio, recursive: true);
        }

        [TestMethod]
        public void Extensiones_ConArgumentosNulosOVacios_Lanzan()
        {
            LoggerConfiguration nula = null!;
            var correo = new SerilogEmailSettings { Enabled = true, UserName = "u", Password = "p", Server = "s", From = "f@x", To = "t@x" };

            Assert.ThrowsExactly<ArgumentNullException>(() => nula.AddDefaultSettings("t"));
            Assert.ThrowsExactly<ArgumentNullException>(() => nula.AddConsole(new SerilogConsoleSettings { Enabled = true }));
            Assert.ThrowsExactly<ArgumentNullException>(() => nula.AddFile(new SerilogFileSettings { Enabled = true }));
            Assert.ThrowsExactly<ArgumentNullException>(() => nula.AddEmail(correo));
            Assert.ThrowsExactly<ArgumentNullException>(() => nula.AddSqlserver(new SerilogSqlServerSettings { Enabled = true, ConnectionString = "Server=x" }));
            Assert.ThrowsExactly<ArgumentNullException>(() => ((global::Serilog.Configuration.LoggerEnrichmentConfiguration)null!).WithTitle("t"));
            Assert.ThrowsExactly<ArgumentNullException>(() => ((global::Serilog.Configuration.LoggerEnrichmentConfiguration)null!).WithThread());
            Assert.ThrowsExactly<ArgumentNullException>(() => ((global::Serilog.Configuration.LoggerEnrichmentConfiguration)null!).WithProcess());
            Assert.ThrowsExactly<ArgumentNullException>(() => ((global::Serilog.Configuration.LoggerEnrichmentConfiguration)null!).WithMachineName());
            Assert.ThrowsExactly<ArgumentNullException>(() => new LoggerConfiguration().AddSqlserver(new SerilogSqlServerSettings { Enabled = true }));
            Assert.ThrowsExactly<ArgumentException>(() => new LoggerConfiguration().AddSqlserver(new SerilogSqlServerSettings { Enabled = true, ConnectionString = " " }));

            foreach (var campo in new[] { "UserName", "Password", "Server", "From", "To" })
            {
                var malo = new SerilogEmailSettings { Enabled = true, UserName = "u", Password = "p", Server = "s", From = "f@x", To = "t@x" };
                typeof(SerilogEmailSettings).GetProperty(campo)!.SetValue(malo, " ");
                Assert.ThrowsExactly<ArgumentException>(() => new LoggerConfiguration().AddEmail(malo), campo);
            }
        }

        [TestMethod]
        public void Enriquecedores_PorExtension_AnadenSusPropiedades()
        {
            var eventos = new System.Collections.Generic.List<LogEvent>();
            var hilo = new Thread(() =>
            {
                using var logger = new LoggerConfiguration()
                    .Enrich.WithThread().Enrich.WithProcess().Enrich.WithMachineName().Enrich.WithTitle("T")
                    .WriteTo.Sink(new Recolector(eventos)).CreateLogger();
                logger.Information("x");
            })
            { Name = "HiloDePrueba" };
            hilo.Start();
            hilo.Join();

            var e = eventos.Single();
            Assert.AreEqual($"\"{hilo.ManagedThreadId} - HiloDePrueba\"", e.Properties["Thread"].ToString());
            using var proceso = Process.GetCurrentProcess();
            Assert.AreEqual($"\"{proceso.Id} - {proceso.ProcessName}\"", e.Properties["Process"].ToString());
            Assert.AreEqual($"\"{Environment.MachineName}\"", e.Properties["MachineName"].ToString());
            Assert.AreEqual("\"T\"", e.Properties["Title"].ToString());
        }

        [TestMethod]
        public void ThreadEnricher_SinNombre_SoloElId()
        {
            var eventos = new System.Collections.Generic.List<LogEvent>();
            var hilo = new Thread(() =>
            {
                using var logger = new LoggerConfiguration().Enrich.WithThread().WriteTo.Sink(new Recolector(eventos)).CreateLogger();
                logger.Information("x");
            });
            hilo.Start();
            hilo.Join();

            Assert.AreEqual($"\"{hilo.ManagedThreadId}\"", eventos.Single().Properties["Thread"].ToString());
        }

        private sealed class Recolector(System.Collections.Generic.List<LogEvent> eventos) : global::Serilog.Core.ILogEventSink
        {
            public void Emit(LogEvent logEvent) => eventos.Add(logEvent);
        }
    }
}
