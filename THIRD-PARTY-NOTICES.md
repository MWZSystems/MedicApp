# Dependencias y procedencia

- ASP.NET Core, EF Core, Identity y bibliotecas Microsoft: MIT, versiones fijadas mediante NuGet.
- MWZ.Workforce.Shared 1.5.0: contrato interno proporcionado por el proyecto WFMCore; incluido sin modificación. Su paquete conserva las dependencias originales.
- Serilog.AspNetCore 10.0.0 y Serilog.Sinks.File 7.0.0: Apache-2.0.
- Bootstrap, jQuery, jQuery Validation, Unobtrusive, Select2, DataTables y SweetAlert2: MIT. Copias locales procedentes del proyecto WFMCore; sus licencias se preservan junto a los recursos en wwwroot/lib.
- xUnit v3 y runner: Apache-2.0; Microsoft.NET.Test.Sdk y ASP.NET Core MVC Testing: MIT.
- Insove Medical Healthcare Pro, TemplatesJungle: template comercial proporcionado por el usuario. Se usa su CSS como base visual y se adapta la navegación a Razor. La vigencia y alcance de la licencia deben cubrir la instalación del cliente. No se distribuye el archivo License Key.txt ni ninguna clave comercial. Este aviso no sustituye la licencia del proveedor.

No se cargan recursos desde CDN, Google Fonts ni servicios de terceros durante la operación de la interfaz.
