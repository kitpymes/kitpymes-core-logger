using System;
using System.Collections.Generic;
using System.Linq;
using global::Serilog.Core;
using global::Serilog.Events;
using global::Serilog.Parsing;
using Kitpymes.Core.Logger.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Kitpymes.Core.Logger.Serilog.Tests
{
    /// <summary>
    /// Ocultación de datos sensibles (Logger 2.3.0): nombres, texto, valores de Serilog y el sink que falla cerrado.
    /// </summary>
    [TestClass]
    public class SensitiveDataTest
    {
        private const string Secreto = "s3cr3t";

        [TestMethod]
        [DataRow("Password")]
        [DataRow("password")]
        [DataRow("UserPassword")]
        [DataRow("user_password")]
        [DataRow("NEW-PASSWORD")]
        [DataRow("Contraseña")]
        [DataRow("contrasenya")]
        [DataRow("ClaveAcceso")]
        [DataRow("NuevaClave")]
        [DataRow("clau")]
        [DataRow("Pwd")]
        [DataRow("NewPwd")]
        [DataRow("pass")]
        [DataRow("Passphrase")]
        [DataRow("ApiKey")]
        [DataRow("apikey")]
        [DataRow("api_key")]
        [DataRow("X-Api-Key")]
        [DataRow("ConnectionString")]
        [DataRow("connection_string")]
        [DataRow("CadenaConexión")]
        [DataRow("ClientSecret")]
        [DataRow("secrets")]
        [DataRow("AccessToken")]
        [DataRow("refresh_token")]
        [DataRow("Authorization")]
        [DataRow("Cookie")]
        [DataRow("CreditCard")]
        [DataRow("CardNumber")]
        [DataRow("Cvv")]
        [DataRow("CVC")]
        [DataRow("IBAN")]
        [DataRow("CodigoPin")]
        [DataRow("OtpCode")]
        [DataRow("PrivateKey")]
        [DataRow("AWSAccessKey")]
        [DataRow("SecretKey")]
        [DataRow("Credentials")]
        [DataRow("Bearer")]
        [DataRow("user.password")]
        public void IsSensitive_NombresDeCredenciales(string nombre)
        {
            Assert.IsTrue(SensitiveDataNames.IsSensitive(nombre), nombre);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("  ")]
        [DataRow("UserName")]
        [DataRow("Name")]
        [DataRow("Email")]
        [DataRow("CancellationToken")]
        [DataRow("cancellation_token")]
        [DataRow("Secretaria")]
        [DataRow("SecretarioId")]
        [DataRow("Passenger")]
        [DataRow("Bypass")]
        [DataRow("Spinner")]
        [DataRow("Tokenizer")]
        [DataRow("Keyboard")]
        [DataRow("Key")]
        [DataRow("Api")]
        [DataRow("CardHolder")]
        [DataRow("Connection")]
        [DataRow("Id")]
        public void IsSensitive_NombresNormales(string? nombre)
        {
            Assert.IsFalse(SensitiveDataNames.IsSensitive(nombre), nombre);
        }

        [TestMethod]
        public void Mask_EsTresAsteriscos()
        {
            Assert.AreEqual("***", SensitiveDataNames.Mask);
        }

        [TestMethod]
        [DataRow("{\"UserName\":\"u\",\"Password\":\"s3cr3t\"}", "{\"UserName\":\"u\",\"Password\":\"***\"}")]
        [DataRow("{ \"password\" : \"s3cr3t\", \"id\": 1 }", "{ \"password\" : \"***\", \"id\": 1 }")]
        [DataRow("{\"pin\":1234,\"id\":1}", "{\"pin\":\"***\",\"id\":1}")]
        [DataRow("{'token':'s3cr3t'}", "{'token':\"***\"}")]
        [DataRow("{\"Password\":\"con \\\"comillas\\\" s3cr3t\"}", "{\"Password\":\"***\"}")]
        [DataRow("{\"UserName\":\"u\"}", "{\"UserName\":\"u\"}")]
        [DataRow("Credenciales { UserName = u, Password = s3cr3t }", "Credenciales { UserName = u, Password = *** }")]
        [DataRow("Server=db;User Id=sa;Password=s3cr3t;Encrypt=True", "Server=db;User Id=sa;Password=***;Encrypt=True")]
        [DataRow("https://api/x?token=s3cr3t&page=1", "https://api/x?token=***&page=1")]
        [DataRow("Authorization: Bearer eyJhbGciOi.s3cr3t-x_y", "Authorization: ***")]
        [DataRow("cabecera bearer eyJ.s3cr3t==", "cabecera bearer ***")]
        [DataRow("https://usuario:s3cr3t@servidor/ruta", "https://usuario:***@servidor/ruta")]
        [DataRow("contraseña: s3cr3t", "contraseña: ***")]
        [DataRow("usuario=ana password=s3cr3t modo=x", "usuario=ana password=*** modo=x")]
        [DataRow("Password = mi clave larga , Id = 1", "Password = *** , Id = 1")]
        [DataRow("token=s3cr3t\nsiguiente linea", "token=***\nsiguiente linea")]
        [DataRow("Hora: 10:20, Token: s3cr3t", "Hora: 10:20, Token: ***")]
        [DataRow("Pedido: 5, Estado: Pagado", "Pedido: 5, Estado: Pagado")]
        [DataRow("", "")]
        public void MaskText_OcultaLosValoresSensibles(string texto, string esperado)
        {
            Assert.AreEqual(esperado, SensitiveDataMasker.MaskText(texto));
        }

        [TestMethod]
        public void MaskText_Nulo_DevuelveNulo()
        {
            Assert.IsNull(SensitiveDataMasker.MaskText(null!));
        }

        [TestMethod]
        public void Mask_SinDatosSensibles_DevuelveElMismoEvento()
        {
            var evento = Evento("Pedido {Id} de {@Cliente}", ("Id", new ScalarValue(5)), ("Cliente", Estructura(("Name", new ScalarValue("Ana")))));

            Assert.AreSame(evento, SensitiveDataMasker.Mask(evento));
        }

        [TestMethod]
        public void Mask_PropiedadConNombreSensible()
        {
            var evento = Evento("Entra {UserName} con {Password}", ("UserName", new ScalarValue("u")), ("Password", new ScalarValue(Secreto)));

            var resultado = SensitiveDataMasker.Mask(evento);

            Assert.AreEqual("\"u\"", resultado.Properties["UserName"].ToString());
            Assert.AreEqual("\"***\"", resultado.Properties["Password"].ToString());
            Assert.AreEqual("Entra \"u\" con \"***\"", resultado.RenderMessage());
        }

        [TestMethod]
        public void Mask_TextoDeLaPlantilla()
        {
            // El patrón del core: el JSON serializado va como plantilla, no como propiedad.
            var evento = Evento("{EventName} => {\"UserName\":\"u\",\"Password\":\"s3cr3t\"}", ("EventName", new ScalarValue("Login")));

            var resultado = SensitiveDataMasker.Mask(evento);

            Assert.IsFalse(resultado.MessageTemplate.Text.Contains(Secreto, StringComparison.Ordinal));
            Assert.IsFalse(resultado.RenderMessage().Contains(Secreto, StringComparison.Ordinal));
            StringAssert.StartsWith(resultado.RenderMessage(), "\"Login\" => ");
        }

        [TestMethod]
        public void Mask_ConservaNivelFechaExcepcionYTraza()
        {
            var excepcion = new InvalidOperationException("x");
            var traza = System.Diagnostics.ActivityTraceId.CreateRandom();
            var tramo = System.Diagnostics.ActivitySpanId.CreateRandom();
            var fecha = new DateTimeOffset(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);
            var evento = new LogEvent(fecha, LogEventLevel.Warning, excepcion, new MessageTemplateParser().Parse("{Token}"), new[] { new LogEventProperty("Token", new ScalarValue(Secreto)) }, traza, tramo);

            var resultado = SensitiveDataMasker.Mask(evento);

            Assert.AreNotSame(evento, resultado);
            Assert.AreEqual(fecha, resultado.Timestamp);
            Assert.AreEqual(LogEventLevel.Warning, resultado.Level);
            Assert.AreSame(excepcion, resultado.Exception);
            Assert.AreEqual(traza, resultado.TraceId);
            Assert.AreEqual(tramo, resultado.SpanId);
        }

        [TestMethod]
        public void Mask_Estructuras_Listas_YDiccionarios()
        {
            var usuario = Estructura(("UserName", new ScalarValue("u")), ("Password", new ScalarValue(Secreto)), ("Datos", new ScalarValue("token=" + Secreto)));
            var lista = new SequenceValue(new LogEventPropertyValue[] { usuario, new ScalarValue("pwd: " + Secreto), new ScalarValue(1) });
            var diccionario = new DictionaryValue(new[]
            {
                new KeyValuePair<ScalarValue, LogEventPropertyValue>(new ScalarValue("api_key"), new ScalarValue(Secreto)),
                new KeyValuePair<ScalarValue, LogEventPropertyValue>(new ScalarValue("user"), Estructura(("Secret", new ScalarValue(Secreto)))),
                new KeyValuePair<ScalarValue, LogEventPropertyValue>(new ScalarValue(7), new ScalarValue("ok")),
            });
            var evento = Evento("{@Lista} {@Mapa}", ("Lista", lista), ("Mapa", diccionario));

            var resultado = SensitiveDataMasker.Mask(evento);

            var texto = resultado.RenderMessage();
            Assert.IsFalse(texto.Contains(Secreto, StringComparison.Ordinal), texto);
            StringAssert.Contains(texto, "UserName: \"u\"");
            StringAssert.Contains(texto, "Password: \"***\"");
            StringAssert.Contains(texto, "\"token=***\"");
            StringAssert.Contains(texto, "\"pwd: ***\"");
            StringAssert.Contains(texto, "(\"api_key\": \"***\")");
            StringAssert.Contains(texto, "(7: \"ok\")");
            Assert.AreEqual("Usuario", ((StructureValue)((SequenceValue)resultado.Properties["Lista"]).Elements[0]).TypeTag);
        }

        [TestMethod]
        public void Mask_ValoresSinDatosSensibles_SeConservanTalCual()
        {
            var lista = new SequenceValue(new LogEventPropertyValue[] { new ScalarValue("a"), new ScalarValue(2) });
            var diccionario = new DictionaryValue(new[] { new KeyValuePair<ScalarValue, LogEventPropertyValue>(new ScalarValue("user"), new ScalarValue("u")) });
            var estructura = Estructura(("Name", new ScalarValue("n")));
            var evento = Evento("{L} {D} {E} {T}", ("L", lista), ("D", diccionario), ("E", estructura), ("T", new ScalarValue("texto")));
            var otro = Evento("{L} {D} {E} {Password}", ("L", lista), ("D", diccionario), ("E", estructura), ("Password", new ScalarValue(Secreto)));

            var resultado = SensitiveDataMasker.Mask(otro);

            Assert.AreSame(evento, SensitiveDataMasker.Mask(evento));
            Assert.AreSame(lista, resultado.Properties["L"]);
            Assert.AreSame(diccionario, resultado.Properties["D"]);
            Assert.AreSame(estructura, resultado.Properties["E"]);
        }

        [TestMethod]
        public void Sink_EscribeElEventoOculto()
        {
            var destino = new SinkEnMemoria();
            using var sink = new SensitiveDataSink(destino);

            sink.Emit(Evento("{Password}", ("Password", new ScalarValue(Secreto))));

            Assert.AreEqual("\"***\"", destino.Eventos.Single().RenderMessage());
        }

        [TestMethod]
        public void Sink_SiNoPuedeOcultar_DescartaElEventoYNoEscribeElOriginal()
        {
            var destino = new SinkEnMemoria();
            using var sink = new SensitiveDataSink(destino, _ => throw new InvalidOperationException(Secreto));
            var evento = new LogEvent(DateTimeOffset.Now, LogEventLevel.Error, new ArgumentException(Secreto), new MessageTemplateParser().Parse("{Datos}"), new[] { new LogEventProperty("Datos", new ScalarValue(Secreto)) });

            sink.Emit(evento);

            var escrito = destino.Eventos.Single();
            Assert.AreEqual(LogEventLevel.Error, escrito.Level);
            Assert.AreEqual(evento.Timestamp, escrito.Timestamp);
            Assert.IsNull(escrito.Exception);
            Assert.AreEqual("Evento descartado: no se pudieron ocultar los datos sensibles (\"InvalidOperationException\").", escrito.RenderMessage());
            Assert.AreEqual(1, escrito.Properties.Count);
        }

        [TestMethod]
        public void Sink_AlLiberarse_LiberaLosSinksReales()
        {
            var destino = new SinkEnMemoria();

            new SensitiveDataSink(destino).Dispose();
            new SensitiveDataSink(new SinkEnMemoriaSinDispose()).Dispose();

            Assert.IsTrue(destino.Liberado);
        }

        [TestMethod]
        public void ShortName_DeLaCategoria()
        {
            Assert.AreEqual("Clase", SourceContextTitleEnricher.ShortName("Espacio.Nombres.Clase"));
            Assert.AreEqual("Clase", SourceContextTitleEnricher.ShortName("Clase"));
            Assert.AreEqual("RequestDataBehaviour", SourceContextTitleEnricher.ShortName("App.RequestDataBehaviour<App.A, App.B>"));
            Assert.AreEqual("RequestDataBehaviour", SourceContextTitleEnricher.ShortName("App.RequestDataBehaviour`2"));
            Assert.AreEqual("Generico", SourceContextTitleEnricher.ShortName("App.Generico[[App.A, App]]"));
            Assert.AreEqual("App.", SourceContextTitleEnricher.ShortName("App."));
            Assert.AreEqual("<Clase>", SourceContextTitleEnricher.ShortName("<Clase>"));
        }

        [TestMethod]
        public void SourceContextTitleEnricher_PoneElTituloSoloSiFalta()
        {
            var enricher = new SourceContextTitleEnricher();
            var conCategoria = Evento("x", ("SourceContext", new ScalarValue("App.Servicio")));
            var conTitulo = Evento("x", ("SourceContext", new ScalarValue("App.Servicio")), ("Title", new ScalarValue("Propio")));
            var sinCategoria = Evento("x");
            var categoriaNoTexto = Evento("x", ("SourceContext", new ScalarValue(3)));

            foreach (var e in new[] { conCategoria, conTitulo, sinCategoria, categoriaNoTexto })
            {
                enricher.Enrich(e, null!);
            }

            Assert.AreEqual("\"Servicio\"", conCategoria.Properties["Title"].ToString());
            Assert.AreEqual("\"Propio\"", conTitulo.Properties["Title"].ToString());
            Assert.IsFalse(sinCategoria.Properties.ContainsKey("Title"));
            Assert.IsFalse(categoriaNoTexto.Properties.ContainsKey("Title"));
        }

        private static StructureValue Estructura(params (string Nombre, LogEventPropertyValue Valor)[] miembros) =>
            new(miembros.Select(m => new LogEventProperty(m.Nombre, m.Valor)), "Usuario");

        private static LogEvent Evento(string plantilla, params (string Nombre, LogEventPropertyValue Valor)[] propiedades) =>
            new(DateTimeOffset.Now, LogEventLevel.Information, null, new MessageTemplateParser().Parse(plantilla), propiedades.Select(p => new LogEventProperty(p.Nombre, p.Valor)));

        private sealed class SinkEnMemoria : ILogEventSink, IDisposable
        {
            public List<LogEvent> Eventos { get; } = new();

            public bool Liberado { get; private set; }

            public void Emit(LogEvent logEvent) => Eventos.Add(logEvent);

            public void Dispose() => Liberado = true;
        }

        private sealed class SinkEnMemoriaSinDispose : ILogEventSink
        {
            public void Emit(LogEvent logEvent)
            {
            }
        }
    }
}
