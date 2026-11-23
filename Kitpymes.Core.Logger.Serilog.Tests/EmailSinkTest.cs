using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Kitpymes.Core.Logger.Serilog.Tests
{
    /// <summary>
    /// Desde la 2.0.1, el log por email usa Serilog.Sinks.Email 4.x (MailKit 4.16 o superior) en lugar de la 2.4.0
    /// (MailKit 2.6.0, con vulnerabilidades conocidas). Con MailKit 4 y el sink antiguo, el envío fallaba en ejecución
    /// (SmtpClient.SendAsync cambió de firma). Estos tests comprueban con un servidor SMTP falso en local que el envío
    /// funciona y se comporta como con la 2.4.0: destinatarios, asunto, cuerpo, lotes y SSL.
    /// </summary>
    [TestClass]
    public class EmailSinkTest
    {
        [TestMethod]
        [Timeout(120000)]
        public void AddEmail_Passing_ServidorInalcanzable_Returns_IntentaEnviarSinFallosDeCompatibilidad()
        {
            var mensajes = new List<string>();

            global::Serilog.Debugging.SelfLog.Enable(m =>
            {
                lock (mensajes)
                {
                    mensajes.Add(m);
                }
            });

            try
            {
                var settings = Settings(port: 1, enableSsl: false);

                using (var logger = new global::Serilog.LoggerConfiguration().AddEmail(settings).CreateLogger())
                {
                    logger.Error("Prueba de envío");
                }
            }
            finally
            {
                global::Serilog.Debugging.SelfLog.Disable();
            }

            string todo;

            lock (mensajes)
            {
                todo = string.Join("\n", mensajes);
            }

            Assert.IsFalse(string.IsNullOrEmpty(todo), "El sink no intentó enviar el correo al cerrar el logger.");
            Assert.IsFalse(new[] { "MissingMethodException", "TypeLoadException", "MissingFieldException", "FileNotFoundException" }.Any(todo.Contains), todo);
        }

        [TestMethod]
        [Timeout(120000)]
        public void AddEmail_Passing_SinSsl_Returns_UnCorreoConLosEventosAgrupadosAsuntoYDestinatarios()
        {
            using var smtp = new SmtpFalso(responder: true);

            var settings = Settings(port: smtp.Port, enableSsl: false);
            settings.Subject = "Asunto {Level}";
            settings.OutputTemplate = "Cuerpo: {Message}{NewLine}";

            var logger = new global::Serilog.LoggerConfiguration().AddEmail(settings).CreateLogger();

            logger.Error("Primero");
            logger.Error("Segundo");

            // Como con la 2.4.0: no se envía el primer evento al momento, se espera al lote (hasta 100 eventos o 30 s).
            Assert.IsFalse(smtp.Conectado.Wait(3000), "Se envió el correo al momento, sin esperar al lote.");

            logger.Dispose();

            Assert.IsTrue(smtp.Terminado.Wait(30000), "El correo no se envió al cerrar el logger.");

            var comandos = smtp.Comandos();

            CollectionAssert.Contains(comandos, "MAIL FROM:<origen@app.com>", string.Join(" | ", comandos));
            CollectionAssert.Contains(comandos, "RCPT TO:<uno@app.com>", string.Join(" | ", comandos));
            CollectionAssert.Contains(comandos, "RCPT TO:<dos@app.com>", string.Join(" | ", comandos));
            Assert.IsTrue(comandos.Any(c => c.StartsWith("AUTH ", StringComparison.OrdinalIgnoreCase)), "No se autenticó con las credenciales.");
            Assert.AreEqual(1, comandos.Count(c => c == "DATA"), "Los dos eventos tenían que ir en un solo correo.");
            StringAssert.Contains(smtp.Datos, "Subject: Asunto Error");
            StringAssert.Contains(smtp.Datos, "Cuerpo: Primero");
            StringAssert.Contains(smtp.Datos, "Cuerpo: Segundo");
        }

        [TestMethod]
        [Timeout(120000)]
        public void AddEmail_Passing_ConSsl_Returns_ConexionTlsDesdeElPrincipio()
        {
            using var smtp = new SmtpFalso(responder: false);

            var logger = new global::Serilog.LoggerConfiguration().AddEmail(Settings(port: smtp.Port, enableSsl: true)).CreateLogger();

            logger.Error("Prueba");
            logger.Dispose();

            Assert.IsTrue(smtp.Terminado.Wait(30000), "El sink no se conectó al servidor.");

            // 0x16 es el inicio de un mensaje de negociación TLS: con EnableSsl, SSL al conectar (como useSsl de MailKit).
            Assert.AreEqual(0x16, smtp.PrimerByte);
        }

        [TestMethod]
        [Timeout(120000)]
        public void AddEmail_Passing_SinSsl_Returns_EsperaElSaludoDelServidorSinTls()
        {
            using var smtp = new SmtpFalso(responder: true);

            var logger = new global::Serilog.LoggerConfiguration().AddEmail(Settings(port: smtp.Port, enableSsl: false)).CreateLogger();

            logger.Error("Prueba");
            logger.Dispose();

            Assert.IsTrue(smtp.Terminado.Wait(30000), "El correo no se envió al cerrar el logger.");

            // Sin EnableSsl: STARTTLS sólo si el servidor lo ofrece. Este no lo ofrece, así que la conversación va en claro.
            Assert.IsTrue(smtp.Comandos().First().StartsWith("EHLO", StringComparison.OrdinalIgnoreCase), string.Join(" | ", smtp.Comandos()));
            CollectionAssert.DoesNotContain(smtp.Comandos(), "STARTTLS");
        }

        private static SerilogEmailSettings Settings(int port, bool enableSsl) => new()
        {
            Enabled = true,
            UserName = "usuario",
            Password = "clave",
            Server = "127.0.0.1",
            Port = port,
            EnableSsl = enableSsl,
            From = "origen@app.com",
            To = "uno@app.com; dos@app.com",
        };

        /// <summary>
        /// Servidor SMTP mínimo para una sola conexión. Con <c>responder</c> a false sólo lee el primer byte que envía el cliente.
        /// </summary>
        private sealed class SmtpFalso : IDisposable
        {
            private readonly TcpListener listener;
            private readonly Thread hilo;
            private readonly bool responder;
            private readonly List<string> comandos = new();

            public SmtpFalso(bool responder)
            {
                this.responder = responder;
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                hilo = new Thread(Atender) { IsBackground = true };
                hilo.Start();
            }

            public int Port { get; }

            public ManualResetEventSlim Conectado { get; } = new();

            public ManualResetEventSlim Terminado { get; } = new();

            public string Datos { get; private set; } = string.Empty;

            public int? PrimerByte { get; private set; }

            public List<string> Comandos()
            {
                lock (comandos)
                {
                    return comandos.ToList();
                }
            }

            public void Dispose()
            {
                listener.Stop();
                hilo.Join(5000);
            }

            private void Atender()
            {
                try
                {
                    using var cliente = listener.AcceptTcpClient();
                    Conectado.Set();
                    var stream = cliente.GetStream();
                    stream.ReadTimeout = 15000;

                    if (!responder)
                    {
                        PrimerByte = stream.ReadByte();
                        return;
                    }

                    var lector = new StreamReader(stream, Encoding.ASCII);
                    var escritor = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };

                    escritor.WriteLine("220 localhost ESMTP");

                    string? linea;

                    while ((linea = lector.ReadLine()) != null)
                    {
                        lock (comandos)
                        {
                            comandos.Add(linea);
                        }

                        var comando = linea.ToUpperInvariant();

                        if (comando.StartsWith("EHLO", StringComparison.Ordinal))
                        {
                            escritor.Write("250-localhost\r\n250 AUTH PLAIN\r\n");
                        }
                        else if (comando.StartsWith("AUTH", StringComparison.Ordinal))
                        {
                            escritor.WriteLine("235 2.7.0 Autenticado");
                        }
                        else if (comando == "DATA")
                        {
                            escritor.WriteLine("354 Adelante");
                            var datos = new StringBuilder();
                            string? d;

                            while ((d = lector.ReadLine()) != null && d != ".")
                            {
                                datos.AppendLine(d);
                            }

                            Datos = datos.ToString();
                            escritor.WriteLine("250 2.0.0 Recibido");
                        }
                        else if (comando == "QUIT")
                        {
                            escritor.WriteLine("221 Adios");
                            break;
                        }
                        else
                        {
                            escritor.WriteLine("250 OK");
                        }
                    }
                }
                catch (IOException)
                {
                    // El cliente cierra la conexión (p. ej. tras el primer byte de TLS): no es un fallo del test.
                }
                catch (SocketException)
                {
                    // El listener se para en Dispose mientras espera una conexión.
                }
                catch (ObjectDisposedException)
                {
                    // El listener se para en Dispose mientras espera una conexión.
                }
                finally
                {
                    Terminado.Set();
                }
            }
        }
    }
}
