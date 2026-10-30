# Changelog

## [1.2.0] - 2026-10-30

### Agregado
- Administración de configuración persistente: rango de días, atención de 24 horas por defecto, turnos diurnos/nocturnos y duración sugerida de nuevas citas.
- Archivo config/medical.defaults.json y override protegido en App_Data/Configuration/medical.json, validación y auditoría.
- Generador idempotente de cuatro personas DEMO, un doctor, once ventanas y hasta 33 citas; calendario administrativo independiente.

### Seguridad
- Horario validado al consultar y reservar; permisos de administración y protección antiforgery.
- Identificadores DEMO registrados para su retiro posterior. Sin cuentas nuevas, incapacidades, eventos laborales ni migraciones SQL.
- Exclusión del doctor DEMO de las reservas reales. Las personas DEMO se identifican en el catálogo compartido.

## [1.1.0] - 2026-10-30

### Agregado
- Calendario de citas con vistas de mes, semana, día y agenda, basado en la referencia AppStack.
- Reserva con fecha/hora y asignación automática de un doctor disponible; identidad tomada de la sesión.
- Consulta y cancelación desde el detalle de la cita, con actualización del calendario y horarios.
- Iconos SVG locales en navegación, formularios y acciones.

### Seguridad
- Alcance propio del empleado y agenda asignada del doctor; validación de disponibilidad, idempotencia y concurrencia en servidor.
- Sin cambios de esquema ni migraciones de datos.


## [1.0.0] - 2026-10-30

### Agregado
- Agenda por doctor, reserva de empleados e incapacidades con autorización/cancelación.
- Identidad compartida, sesión propia, permisos explícitos y auditoría.
- Migraciones EF propias, script SQL idempotente, preflight y roles SQL.
- Interfaz Razor adaptada de Insove y librerías locales utilizadas en WFMCore.
- Contrato laboral SQL/HTTP y lector preparado para WFMCore.
- Pruebas de reglas, SQL LocalDB y rutas HTTP con xUnit v3.

### Seguridad
- Antiforgery global, política de acceso por defecto, control de propietario, rowversion, transacciones, validación servidor y secretos externos.

### Publicación
- Publicador IIS para MedicApp/site9 con configuración privada de producción y verificación HTTP.
- Repositorio GitHub y paquete de entrega versionado en Releases/v.1.0.0.
