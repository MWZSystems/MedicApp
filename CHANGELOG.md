# Changelog

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
