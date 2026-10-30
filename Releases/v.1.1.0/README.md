# MedicApp 1.1.0

Calendario visual de citas para empleados y doctores, reserva con fecha/hora e iconos intuitivos. ASP.NET Core MVC / Razor .NET 10, Identity compartido con WFMCore y tablas propias en medic.

- 45 pruebas aprobadas; compilación Release sin advertencias.
- Incluye vistas de mes, semana, día y agenda, asignación automática de doctor disponible, consulta/cancelación desde el calendario e iconos SVG locales. Conserva permisos, auditoría y reservas concurrentes e idempotentes. Sin cambios SQL.
- La aplicación efectiva de incapacidades en asistencia/overtime de WFMCore sigue pendiente de incorporar, probar y publicar en ese producto.
- El ZIP de aplicación se encuentra en los assets del release v1.1.0. Se conserva localmente junto a este manifiesto; no se duplica en el historial Git.
- El paquete no contiene datos ni credenciales de instalación. Usar docs/instalacion.md y scripts/Publicar-MedicApp.ps1 para configurar el destino correcto; no publicar el ZIP sin configuración de producción.

La versión es inmutable. Verificar SHA256SUMS.txt antes de desplegar. El SourceCommit del manifiesto identifica el código; el tag incluye también estos metadatos de entrega.
