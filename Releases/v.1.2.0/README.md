# MedicApp 1.2.0

Configuración administrativa de agenda, atención de 24 horas por defecto y calendario DEMO aislado. ASP.NET Core MVC / Razor .NET 10, Identity compartido con WFMCore y tablas propias en medic.

- 57 pruebas aprobadas; compilación Release sin advertencias.
- Incluye rango configurable de 1 a 365 días, horario general de 24 horas o turnos diurnos/nocturnos, duración sugerida y configuración persistente con auditoría. Generador explícito de cuatro personas DEMO, un doctor, once ventanas y hasta 33 citas, con calendario administrativo separado. Sin cuentas DEMO ni hechos laborales ni cambios SQL. Las personas sintéticas se identifican en el catálogo compartido; su inventario se guarda en auditoría para el retiro posterior. El ZIP no contiene datos.
- La aplicación efectiva de incapacidades en asistencia/overtime de WFMCore sigue pendiente de incorporar, probar y publicar en ese producto.
- El ZIP de aplicación se encuentra en los assets del release v1.2.0. Se conserva localmente junto a este manifiesto; no se duplica en el historial Git.
- El paquete no contiene datos ni credenciales de instalación. Usar docs/configuracion.md, docs/instalacion.md y scripts/Publicar-MedicApp.ps1 para configurar el destino correcto; no publicar el ZIP sin configuración de producción.

La versión es inmutable. Verificar SHA256SUMS.txt antes de desplegar. El SourceCommit del manifiesto identifica el código; el tag incluye también estos metadatos de entrega.
