# MedicApp 1.0.0

Primera entrega de citas, agenda por doctor e incapacidades. ASP.NET Core MVC / Razor .NET 10, Identity compartido con WFMCore y tablas propias en medic.

- 35 pruebas aprobadas; compilación Release sin advertencias.
- Incluye permisos propios, auditoría, reservas concurrentes e idempotentes y contrato laboral de lectura.
- La aplicación efectiva de incapacidades en asistencia/overtime de WFMCore sigue pendiente de incorporar, probar y publicar en ese producto.
- El ZIP de aplicación se encuentra en los assets del release v1.0.0. Se conserva localmente junto a este manifiesto; no se duplica en el historial Git.
- El paquete no contiene datos ni credenciales de instalación. Usar docs/instalacion.md y scripts/Publicar-MedicApp.ps1 para configurar el destino correcto; no publicar el ZIP sin configuración de producción.

La versión es inmutable. Verificar SHA256SUMS.txt antes de desplegar. El SourceCommit del manifiesto identifica el código; el tag incluye también estos metadatos de entrega.
