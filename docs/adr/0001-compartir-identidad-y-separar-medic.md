# ADR 0001: identidad común, datos médicos separados por esquema

Estado: implementado. Esquema medic instalado en la base compartida el 30 de octubre de 2026.

Se reutiliza el contrato binario versionado MWZ.Workforce.Shared 1.5.0 de WFMCore. TrainifyDbContext se utiliza solo para identidad y consulta de personas; MedicDbContext administra el esquema medic y su propio historial. SharedPerson y SharedUser son referencias mínimas excluidas de migraciones, con FK físicas hacia dbo.SY_People.Id y dbo.SC_Users.Id.

Las nuevas PK son INT IDENTITY, con prefijos CT/SC/SY/AR/IX, FK indexadas, restricciones de fechas/estado, índices únicos, Available, LastUpdated UTC y rowversion. Excepción de compatibilidad al estándar: FKUserUpdater es NVARCHAR(450), porque las cuentas compartidas existentes usan Identity string. Convertirla a INT o crear identidades duplicadas rompería el vínculo maestro. Se documenta expresamente la excepción y se conserva la FK física.

Empleado es el perfil base de toda cuenta compartida activa. Doctor requiere registro médico activo. La administración de doctores, permisos y lectura de integración son concesiones propias por cuenta con tres capacidades explícitas. No se hereda automáticamente el rol global Administrador de otra aplicación ni se cambia el catálogo de permisos compartido; ampliar a una matriz por rol se hará en una evolución coordinada del contrato común.

Cada doctor opera sus citas e incapacidades; la administración técnica no obtiene acceso clínico por administrar permisos. No se capturan diagnósticos ni expediente clínico. El registro de auditoría conserva entidad, operación, motivo, responsable y fecha; los hechos de incapacidad no se editan después de autorizados: se cancelan y se emite otro folio.

Las ventanas son fechas/horas explícitas; no se replican cargas Excel/SSIS ni dependencias de dominio Windows del sistema antiguo. La reserva tiene clave de idempotencia y una transacción serializable con bloqueo corto compartido de aplicación, para evitar dobles reservas concurrentes. Debe medirse su capacidad antes de escalar a volúmenes altos.

Insove se adapta a Razor y Bootstrap con su CSS original local y una capa propia de aplicación. Se preserva la procedencia del template proporcionado por el usuario. No se incluyen sus claves de licencia ni recursos remotos. Los scripts opcionales, blogs, precios y marketing no pertenecen a la aplicación operativa.
