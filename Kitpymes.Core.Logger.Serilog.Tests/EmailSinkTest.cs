using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Kitpymes.Core.Logger.Serilog.Tests
{
    /// <summary>
    /// Desde la 2.0.1, el log por email usa Serilog.Sinks.Email 4.x (MailKit 4.16 o superior) en lugar de la 2.4.0
    /// (MailKit 2.6.0, con vulnerabilidades conocidas). Con MailKit 4 y el sink antiguo, el envío fallaba en ejecución
    /// (SmtpClient.SendAsync cambió de firma), así que se prueba que el sink intenta enviar y no falla por compatibilidad.
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
                var settings = new SerilogEmailSettings
                {
                    Enabled = true,
                    UserName = "usuario",
                    Password = "clave",
                    Server = "127.0.0.1",
                    Port = 1,
                    EnableSsl = false,
                    From = "origen@app.com",
                    To = "destino@app.com; otro@app.com",
                };

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
    }
}
