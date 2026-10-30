# MedicApp

Aplicación médica.

Aplicación ASP.NET Core MVC / Razor en .NET 10 para citas médicas e incapacidades de empleados. Comparte cuentas y personas con WFMCore; mantiene cookies, claves de protección, permisos y tablas médicas independientes. Interfaz basada en Insove con Bootstrap 5, Select2, DataTables y SweetAlert2 servidos localmente.

## Arranque local

Requisitos: SDK .NET 10, Windows con SQL LocalDB para las pruebas de integración y acceso a la base común ya instalada de WFMCore para operación. No se necesitan fuentes de Trainify; el paquete compartido versionado está incluido.

```powershell
dotnet restore MedicApp.slnx --configfile NuGet.Config
dotnet build MedicApp.slnx -c Release --no-restore --warnaserror
dotnet test MedicApp.slnx -c Release --no-restore
```

Configurar `ConnectionStrings:Workforce` fuera del repositorio; la base definitiva confirmada es sql1002 / db_acaa35_trainify. Revisar [instalación](docs/instalacion.md) para ejecutar los scripts, habilitar el primer administrador y levantar HTTPS. No hay contraseña predeterminada ni migración automática al inicio.

## Funciones

- Inicio de sesión con cuenta o número de empleado compartido; cambio de contraseña y bloqueo por intentos con Identity. Sin registro público ni autenticación solo por gafete.
- Empleado: horarios libres, reserva propia, consulta y cancelación de citas futuras, consulta de sus incapacidades.
- Doctor: ventanas de agenda, consulta de citas asignadas, atención/inasistencia/cancelación; emisión de incapacidades en borrador, autorización y cancelación con motivo. No se permite emitir una incapacidad propia.
- Administración independiente de doctores y permisos por cuenta; auditoría paginada.
- Intervalos UTC, zona configurable, control de empalmes, concurrencia con rowversion, reservas idempotentes, transacciones y hechos laborales atómicos.
- Listados paginados en SQL, búsqueda por columna y columnas visibles sobre la página cargada, selectores AJAX de hasta 20 personas/cuentas, confirmaciones y recursos en español. No se almacenan datos personales en localStorage.
- Dos vistas SQL y API protegida para integración laboral sin información clínica.

## Base de datos

`database/01_InstallMedicApp.sql` es el script idempotente generado desde las migraciones EF de MedicDbContext, con preflight. Crea siete tablas en medic: CT_Doctors, SY_AvailabilityWindows, SY_Appointments, SY_MedicalLeaves, SC_AccessProfiles, AR_AuditEntries e IX_LaborEvents; además, dos vistas y su historial de migraciones. No crea ni altera tablas de WFMCore/Trainify.

## Alcance pendiente de despliegue

El código y los scripts están preparados para revisión e instalación. La base compartida no se migra ni se siembra automáticamente. La integración efectiva con cálculos de WFMCore requiere incorporar y probar el consumidor en su proyecto; consultar [contrato laboral](docs/integracion-wfmcore.md). No se incluyen expediente clínico, recetas, archivos clínicos, familiares/externos, importación histórica ni liquidación de nómina.

Las pruebas crean y eliminan exclusivamente una base LocalDB aleatoria `MedicApp_Test_GUID`. Las credenciales de prueba son sintéticas y generadas en memoria. Ver [validación](docs/validacion.md) y [decisiones](docs/adr/0001-compartir-identidad-y-separar-medic.md).

## Entrega 1.0.0

Repositorio: https://github.com/MWZSystems/MedicApp. Los metadatos y las instrucciones de la entrega están en `Releases/v.1.0.0`. El ZIP de aplicación y su checksum se distribuyen como assets del release `v1.0.0`, sin conexión SQL, perfiles privados, claves de sesión ni datos.

La integración automática con asistencia y overtime continúa pendiente de incorporar y publicar en WFMCore. El esquema propio y la cuenta administrativa de MedicApp ya se instalaron; publicar el sitio no vuelve a aplicar esos cambios.
