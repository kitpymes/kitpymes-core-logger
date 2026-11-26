**Logeo de errores para múltiples proveedores**

[![Build Status](https://github.com/kitpymes/kitpymes-core-logger/actions/workflows/ci.yml/badge.svg)](https://github.com/kitpymes/kitpymes-core-logger/actions/workflows/ci.yml) [![NuGet Status](https://img.shields.io/nuget/v/Kitpymes.Core.Logger)](https://www.nuget.org/packages/Kitpymes.Core.Logger/) [![NuGet Download](https://img.shields.io/nuget/dt/Kitpymes.Core.Logger)](https://www.nuget.org/stats/packages/Kitpymes.Core.Logger?groupby=Version) [![License](https://img.shields.io/github/license/kitpymes/kitpymes-core-logger)](https://github.com/kitpymes/kitpymes-core-logger/blob/master/docs/LICENSE.txt) [![Size Repo](https://img.shields.io/github/repo-size/kitpymes/kitpymes-core-logger)](https://github.com/kitpymes/kitpymes-core-logger/) [![Last Commit](https://img.shields.io/github/last-commit/kitpymes/kitpymes-core-logger)](https://github.com/kitpymes/kitpymes-core-logger/) 

## ⌨️ Uso

Desde la 2.3.0, `ILogger<T>` de Microsoft con Serilog

```csharp
// Program.cs: arranque (antes de tener el contenedor)
using var arranque = KitpymesSerilog.CreateLoggerFactory(o => o.AddConsole());
var log = arranque.CreateLogger<Program>();

// Registro: lee la sección LoggerSettings de appsettings.json, como LoadLogger
builder.Logging.ClearProviders(); // opcional: sólo Serilog
builder.Logging.AddKitpymesLogging(builder.Configuration);

// Uso: ILogger<T> de Microsoft.Extensions.Logging, con plantillas (no interpolar)
public class Servicio(ILogger<Servicio> logger)
{
    public void Hacer(int id) => logger.LogInformation("Pedido {Id}", id);
}
```

Datos sensibles: antes de llegar a los sinks, se ocultan (`***`) las propiedades, miembros, claves JSON y `clave=valor` con nombre de credencial (password, token, apikey, connectionstring, secret, pin…), lo marcado con `[Sensitive]`, `Bearer` y las contraseñas en URL, también en el texto del mensaje. No cubre datos personales sin `[Sensitive]`, mensajes de excepciones ni texto libre sin clave. `ILoggerService`, `Log` y `LoadLogger`/`LoadSerilog` están obsoletos (KPY0009 a KPY0011) y se quitan en la 3.0.0.

## 📋 Requerimientos 

* Visual Studio 2026 o superior

* NET TargetFramework = net10.0

* SDK de .NET = 10.0.x

* C# = 12

## 🛠️ Construido con 

* [NET Core](https://dotnet.microsoft.com/download) - Framework de trabajo
* [C#](https://docs.microsoft.com/es-es/dotnet/csharp/) - Lenguaje de programación
* [Inserción de dependencias](https://docs.microsoft.com/es-es/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-3.0) - Patrón de diseño de software
* [MSTest](https://docs.microsoft.com/es-es/dotnet/core/testing/unit-testing-with-mstest) - Pruebas unitarias
* [Nuget](https://www.nuget.org/) - Manejador de dependencias
* [Visual Studio](https://visualstudio.microsoft.com/) - Entorno de programacion
* [Serilog](https://serilog.net/) - Proveedor de logeo de errores


## ✒️ Autores 

* **Sebastian R Ferrari** - *Trabajo Inicial* - [kitpymes](https://kitpymes.com)


## 📄 Licencia 

* Este proyecto está bajo la Licencia [LICENSE](https://raw.githubusercontent.com/kitpymes/kitpymes-core-logger/master/docs/LICENSE.txt)


## 🎁 Gratitud 

* Este proyecto fue diseñado para compartir, creemos que es la mejor forma de ayudar 📢
* Cada persona que contribuye será invitada a tomar una 🍺 
* Gracias a todos! 🤓

---
[Kitpymes](https://github.com/kitpymes) 😊