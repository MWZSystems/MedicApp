# Validación de MedicApp 1.0.0

Fecha: 30 de octubre de 2026. Desarrollo y pruebas locales, sin ejecución de scripts sobre la base compartida.

- Compilación Release: sin errores ni advertencias, incluyendo analizadores.
- xUnit v3: 35 pruebas aprobadas, sin omisiones. Cubren reglas de intervalo y turnos horarios, antiforgery, rutas y páginas Razor, permisos por perfil y propietario, reservas idempotentes y concurrentes, transición de citas/incapacidades, empalmes, conflictos rowversion y rollback de auditoría/eventos.
- SQL Server LocalDB: bases aleatorias desechables con prefijo MedicApp_Test_. El instalador SQL real se ejecutó dos veces sobre una base nueva con el esquema compartido; se verificaron siete tablas médicas y ausencia de usuarios/personas creados por el instalador.
- Lector de WFMCore: compilado desde los archivos entregados y probado contra las vistas SQL reales, incluyendo autorización, cancelación y límites contiguos de intervalos. No se incorporó en el checkout/publicación de WFMCore.
- Modelo EF: no hay cambios pendientes respecto a la migración.
- NuGet: la auditoría de dependencias directas y transitivas no reportó paquetes vulnerables en las fuentes consultadas.
- Navegador: pantalla de acceso comprobada con HTTPS local y el certificado de desarrollo existente. Las páginas autenticadas se renderizaron y verificaron mediante WebApplicationFactory; no se realizó una sesión de navegador completa con usuarios reales.
- Recursos: CSS Insove y librerías locales; páginas HTTP verificadas sin CDN ni Google Fonts. No se incluye el archivo con clave de licencia del template.

No equivalen a verificaciones realizadas: calidad Sonar, revisión por otra persona, pruebas de carga, escaneo externo, recuperación de respaldo, pruebas en QA del cliente o publicación. Deben completarse antes de declarar esta versión lista para producción según la definición de terminado del estándar.

La entrega no modifica WFMCore ni Trainify, ni altera o carga la base compartida. La conexión, cuenta SQL propia, primer administrador e instalación del esquema siguen los pasos de docs/instalacion.md.
