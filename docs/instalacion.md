# Instalación de MedicApp 1.0.0

Destino común confirmado por el usuario: `sql1002.site4now.net / db_acaa35_trainify`. No usar el destino de la captura inicial (`sql1003 / trainifychdb`). La contraseña y la cuenta SQL de ejecución se configuran fuera del código.

1. Instalar SDK .NET 10 y SQL Server 2019+ para desarrollo. La instancia existente debe tener el contrato compartido de WFMCore compatible con `MWZ.Workforce.Shared` 1.5.0. El paquete está incluido; no se compila ni modifica Trainify.
2. Restaurar y compilar desde la raíz:
   ```powershell
   dotnet restore MedicApp.slnx --configfile NuGet.Config
   dotnet build MedicApp.slnx -c Release --no-restore --warnaserror
   dotnet test MedicApp.slnx -c Release --no-restore
   ```
3. Guardar `ConnectionStrings:Workforce` en User Secrets de `MedicApp/MedicApp.csproj`, o en `ConnectionStrings__Workforce` del servidor. Usar una cuenta de servicio propia para MedicApp, cifrado obligatorio y verificación del certificado según el proveedor. Nunca publicar con la cuenta administrativa del instalador.
4. El DBA verifica un respaldo recuperable, ejecuta `database/00_Preflight.sql` y revisa `database/01_InstallMedicApp.sql` en modo SQLCMD en el destino confirmado. El script idempotente instala únicamente `medic`, sus doce tablas, dos vistas y su historial de migraciones. La aplicación no migra al arrancar. La migración no cambia tablas compartidas, ni activa RCSI en una base en uso; el DBA revisa ese ajuste de instancia por separado.
5. Revisar `database/02_RuntimeRoles.sql`. Asociar el usuario SQL propio al rol `MedicAppRuntime`; dar a la cuenta WFM el rol `WfmMedicalReader` solo al habilitar la integración. El instalador conserva los permisos DDL; la cuenta web no los necesita.
6. Seleccionar una cuenta compartida activa con contraseña ya actualizada. Configurar `Bootstrap:UserName` mediante secretos y ejecutar una sola vez:
   ```powershell
   dotnet run --project MedicApp/MedicApp.csproj -- --grant-admin
   ```
   Esto concede administración exclusiva de MedicApp a esa cuenta, con auditoría. No crea cuentas, personas, roles globales ni contraseñas. Si ya hay un administrador, los cambios se realizan por la pantalla Permisos.
7. Configurar `Medical:DisplayName`, `Medical:TimeZoneId`, `Medical:BookingHorizonDays`, `Medical:DataDirectory` y `AllowedHosts`. La zona predeterminada sigue WFMCore (`America/Chihuahua`), no el reloj del servidor. `DataDirectory` debe quedar fuera de wwwroot, con acceso exclusivo del proceso; respaldar claves de Data Protection y logs según retención. En IIS usar un directorio persistente fuera de la carpeta de publicación y proteger las claves con ACL y cifrado del volumen/proveedor.
8. Iniciar con HTTPS:
   ```powershell
   dotnet run --project MedicApp/MedicApp.csproj --launch-profile https
   ```
   Ingresar con la cuenta compartida. Registrar doctores, su especialidad y cédula, y crear ventanas explícitas de disponibilidad desde Mi agenda. Una fecha sin agenda no ofrece citas.
9. Publicar en un sitio independiente de WFMCore y Trainify. Configurar Hosting Bundle .NET 10, HTTPS, variables privadas y un AllowedHosts del sitio. Verificar `/health`, login, reservas, permisos, cancelaciones y cierres de sesión independientes. No se entregó ni utilizó un perfil de publicación de MedicApp.

No se cargan personas, cuentas ni citas antiguas automáticamente. Los XML entregados son evidencia de análisis, no un respaldo restaurable. La réplica de familiares, externos, Excel/SSIS o históricos requiere un mapeo específico; esta entrega cubre empleados, doctores, agenda e incapacidades.

## Publicación de esta instalación

El perfil de MedicApp corresponde exclusivamente a site9. La configuración de desarrollo en User Secrets no se publica automáticamente. Desde la raíz del proyecto:

```powershell
.\scripts\Publicar-MedicApp.ps1 -ProfilePath '.\MedicApp\Properties\PublishProfiles\mwzsystems-001-site9 - Web Deploy.pubxml'
```

El script genera web.config para IIS fuera de proceso, prepara el dominio permitido y ConnectionStrings:Workforce en un archivo de producción temporal fuera del repositorio, y comprueba /health y /Account/Login después de publicar. El archivo temporal con credenciales se retira al terminar. Se preservan App_Data y las claves del servidor; no se ejecutan migraciones ni se siembran datos al publicar.

El esquema medic y la cuenta inicial de administración ya fueron instalados el 30 de octubre de 2026. No volver a crear ni restablecer cuentas por publicar otra compilación.
