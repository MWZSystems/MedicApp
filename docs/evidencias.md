# Evidencias de incapacidades y expediente documental 1.5.0

## Uso

En Incapacidades > Registro, **Agregar incapacidad** permite capturar persona, folio y fechas. Los doctores registran con su propia identidad clínica; la administración médica debe seleccionar un doctor responsable activo del catálogo. Los doctores DEMO están excluidos de este selector y no pueden emitir incapacidades. Si falta el responsable, Configurar doctores responsables permite activar a la persona correcta; no se crean personas clínicas ficticias ni se modifica una cuenta para simular un doctor.

Al guardar se abre **Evidencias de la incapacidad**, donde se adjuntan los documentos del IMSS y otros comprobantes. Este segundo paso conserva la captura válida si un documento es rechazado; las evidencias del envío se guardan completas o se compensan si falla SQL. El registro permanece en borrador hasta la autorización del doctor responsable. La administración puede capturar y archivar, pero no obtiene permiso para autorizar o cancelar.

Cada fila del Registro ofrece **Documentos (n)** y **Expediente documental**. El expediente agrupa todas las incapacidades disponibles y sus evidencias por persona, paginadas de 25 en 25, con búsqueda por nombre o número de control. Incluye borradores, autorizadas y canceladas; el calendario continúa mostrando solo periodos autorizados. Los comprobantes permanecen asociados aunque la incapacidad se cancele. Consultar / descargar devuelve el archivo original como attachment, para abrirlo con el visor del usuario; PDF y otras cargas no se ejecutan como contenido HTML de la aplicación.

## Permisos

- Doctor responsable: captura, agrega documentos y consulta los archivos de sus registros.
- Administración médica (ManageDoctors o ManageAccess): captura asignando responsable, consulta y adjunta evidencias en los registros; conserva sus facultades administrativas sin recibir aprobación clínica.
- Titular: consulta y descarga sus propios documentos. No puede adjuntar evidencias ni modificar las de otras personas.
- Otros doctores: el calendario puede mostrar las fechas autorizadas según la política existente, pero el expediente y los archivos se limitan a sus registros o a su condición de titular. Usuarios de integración no reciben acceso general a estos archivos.

Los controles se aplican en cada acceso, carga y descarga, no solo al ocultar botones. El archivo se identifica por su ID en un endpoint autenticado; no hay rutas públicas a Evidence. Las respuestas no se almacenan en caché. Cargas y descargas se auditan por usuario y documento, sin registrar contenido ni nombres de archivo en logs de aplicación. No se ofrece borrado o sustitución de evidencias en esta versión.

## Formatos y límites

Documents en config/medical.defaults.json configura MaxFileSizeMb=10, MaxBatchSizeMb=20, MaxFilesPerUpload=5 y MaxDocumentsPerLeave=20. Los máximos admitidos por el código son 10 MB por archivo, 20 MB por envío y cinco archivos por envío; el límite del expediente puede configurarse entre 1 y 100 documentos por incapacidad. Las variables de entorno Documents__... también se admiten; reiniciar la aplicación al modificarlas. Estos límites no alteran las reglas Medical del calendario de citas.

Solo PDF, JPG/JPEG y PNG. Se comprueba extensión y firma del contenido, se ignora el MIME declarado por el navegador, se retiran rutas y caracteres de control del nombre y se controla el tamaño real al leer. Las comprobaciones no sustituyen la evaluación del documento IMSS por el personal autorizado. El límite HTTP del endpoint es 24 MiB para dejar margen al formulario; el total de los archivos se restringe a 20 MiB. La misma clave de solicitud y sus reintentos no duplican documentos. Un bloqueo por incapacidad y un índice único protegen concurrencia.

## Almacenamiento y respaldo

Los originales se cifran mediante ASP.NET Core Data Protection con propósito propio y clave opaca por documento. Se guardan bajo Medical:DataDirectory/Evidence/<prefijo>/<GUID>.dat, fuera de wwwroot. La base compartida recibe solo metadatos, tamaño, SHA-256, referencia a la incapacidad y auditoría; no blobs ni documentos del IMSS. Al recuperar se verifica la protección criptográfica y el hash/tamaño. Un archivo corrupto o inaccesible no se entrega.

Respaldar conjuntamente la base de datos, App_Data/Evidence y App_Data/Keys, con acceso limitado a la cuenta del sitio y administradores autorizados. La retención de claves es necesaria para descifrar los documentos; no borrar Keys al publicar o cambiar servidores. El publicador utiliza DoNotDeleteRule, conserva estas carpetas y excluye App_Data de Git y de los ZIP de Release. No restaurar archivos o claves desde un ZIP de código. En despliegues con varias instancias se requiere almacenamiento privado y claves compartidos entre esas instancias de MedicApp.

## Esquema y despliegue

LeaveDocumentEvidence agrega únicamente medic.SY_LeaveDocuments, con FK a la incapacidad y al usuario, restricciones de tamaño y ordinal, índices y rowversion. No modifica datos o tablas de WFMCore/Trainify. database/01_InstallMedicApp.sql incorpora la migración idempotente y conserva el preflight. database/02_RuntimeRoles.sql mantiene el rol de integración limitado a sus vistas y deniega UPDATE/DELETE de documentos al rol de ejecución.

Tras compilar Release, aplicar explícitamente:

```powershell
./scripts/Actualizar-Esquema.ps1 -AdminUserName medicadmin
```

El script comprueba sql1002 / db_acaa35_trainify y que el actor activo administra accesos; aplica migraciones sin generar datos, cambiar usuarios ni reconfigurar agenda. El inicio web no aplica migraciones. Para una instalación con roles restringidos, el operador aplica 02_RuntimeRoles.sql tras la actualización. Publicar luego con el perfil de site9 sin InitializeCalendar. La salud del sitio comprueba que la nueva tabla sea accesible.

No se cargan documentos ni incapacidades ficticias en producción. Las pruebas crean datos y archivos sintéticos en carpetas/base LocalDB con GUID exclusivo y los retiran al terminar. Se verifican carga/descarga, cifrado, auditoría, permisos, captura administrativa, idempotencia concurrente, rechazo de formatos/tamaños, cuotas, corrupción y compensación de archivos cuando falla SQL. La integración efectiva de asistencia/overtime sigue pendiente en WFMCore.
