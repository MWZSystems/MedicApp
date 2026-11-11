# Dependencias y procedencia

- ASP.NET Core, EF Core, Identity y bibliotecas Microsoft: MIT, versiones fijadas mediante NuGet.
- MWZ.Workforce.Shared 1.5.0: contrato interno proporcionado por el proyecto WFMCore; incluido sin modificación. Su paquete conserva las dependencias originales.
- Serilog.AspNetCore 10.0.0 y Serilog.Sinks.File 7.0.0: Apache-2.0.
- Bootstrap, Bootstrap Icons, jQuery, jQuery Validation y Unobtrusive (MIT) y el tipo de letra Nunito Sans (SIL Open Font License 1.1) llegan con el paquete propio `MWZSystem.Ui`, de MWZSystems; su detalle y versiones están en el `THIRD-PARTY-NOTICES.md` de ese repositorio. Select2, DataTables y FullCalendar (MIT) siguen como copias locales en `wwwroot/lib`. SweetAlert2 ya no se distribuye.
- xUnit v3 y runner: Apache-2.0; Microsoft.NET.Test.Sdk y ASP.NET Core MVC Testing: MIT.
- Diseño visual: hojas de estilo propias (`wwwroot/css/medicapp-base.css`, `medicapp.css` y `calendar.css`). El 11 de noviembre de 2026 se retiró del código y del paquete publicado el CSS del template comercial Insove (TemplatesJungle); la aplicación ya no depende de ninguna licencia comercial de interfaz. El archivo sigue en el historial de Git anterior a esa fecha.

No se cargan recursos desde CDN, Google Fonts ni servicios de terceros durante la operación de la interfaz.

- FullCalendar Standard 6.1.19: MIT, archivos oficiales locales de los paquetes npm fullcalendar y @fullcalendar/core. Licencia conservada en wwwroot/lib/fullcalendar/LICENSE.md. La configuración y los estilos del calendario son propios; no se incluye código, CSS ni recursos de templates comerciales. Documentación/licencia: https://fullcalendar.io/license.
- Iconos de interfaz: sprite SVG propio, sin fuentes externas ni dependencia de servicios remotos.
