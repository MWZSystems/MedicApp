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

## Agenda operativa 1.3.0

El calendario abre en semana y muestra rangos completos de treinta minutos; el horizonte inicial es de siete días y ambos valores se ajustan en Configuración. Las citas de otras personas aparecen como **Ocupado · otra persona**, sin datos privados. El catálogo de doctores permite habilitar un horario recurrente basado en el horario global (24 horas inicialmente).

La instalación de prueba inicializa un consultorio DEMO y veintiuna citas en el calendario principal. Un aviso distingue las reservas de prueba. Desactivar el modo DEMO y habilitar doctores reales antes de operar con pacientes. Revisar [agenda operativa](docs/agenda-operativa.md) para migración, inicialización, permisos y posterior retiro.

## Configuración y ejemplos 1.2.0

El menú **Configuración** permite ajustar días disponibles, atención general (24 horas por defecto) y duración sugerida. Los valores base están en `MedicApp/config/medical.defaults.json`; los cambios administrativos se conservan en `App_Data/Configuration/medical.json` al volver a publicar. La disponibilidad sigue requiriendo agenda de un doctor.

Incluye un calendario DEMO administrativo y generación explícita de registros ficticios identificables, aislados de las reservas reales. No se crean cuentas ni eventos laborales. Revisar [configuración y retiro posterior de ejemplos](docs/configuracion.md).

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

## Calendario de citas 1.1.0

Citas abre el calendario compartido de la interfaz, con alcance por usuario. El empleado ve solo sus citas; cada doctor ve las propias y las asignadas. Reservar requiere fecha y hora. El empleado, doctor disponible, duración y auditoría se resuelven en el servidor. Las flechas, vistas, navegación por días y detalle permiten consultar y cancelar sin salir del calendario. La lista paginada sigue disponible como alternativa.

La disponibilidad depende de doctores activos y ventanas previamente configuradas en Mi agenda; no se inventan horarios ni se crean reservas fuera de agenda. El horizonte de reserva configurado se conserva. No hay cambio SQL en esta versión.
