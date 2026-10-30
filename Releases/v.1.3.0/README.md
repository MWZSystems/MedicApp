# MedicApp 1.3.0

Agenda operativa semanal con bloques de treinta minutos, horario recurrente por doctor y ocupación anónima compartida. ASP.NET Core MVC / Razor .NET 10, Identity compartido con WFMCore y tablas propias en medic.

- 63 pruebas aprobadas; compilación Release sin advertencias.
- Incluye horizonte inicial de siete días, bloques de treinta minutos configurables (máximo treinta), rangos completos en el selector, recurrencia por doctor, ocupación anónima de otras personas y modo de prueba explícito. El inicializador crea un consultorio DEMO y hasta veintiuna citas en la agenda principal, sin cuentas ni hechos laborales. Se conservan ejemplos anteriores. Migración aditiva de dos columnas booleanas en tablas médicas. Aplicar el script idempotente y revisar docs/agenda-operativa.md antes de desplegar. El ZIP no contiene datos ni la configuración particular del servidor.
- La aplicación efectiva de incapacidades en asistencia/overtime de WFMCore sigue pendiente de incorporar, probar y publicar en ese producto.
- El ZIP de aplicación se encuentra en los assets del release v1.3.0. Se conserva localmente junto a este manifiesto; no se duplica en el historial Git.
- El paquete no contiene datos ni credenciales de instalación. Usar docs/configuracion.md, docs/instalacion.md y scripts/Publicar-MedicApp.ps1 para configurar el destino correcto; no publicar el ZIP sin configuración de producción.

La versión es inmutable. Verificar SHA256SUMS.txt antes de desplegar. El SourceCommit del manifiesto identifica el código; el tag incluye también estos metadatos de entrega.
