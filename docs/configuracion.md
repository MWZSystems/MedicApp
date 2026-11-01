# Configuración de MedicApp

La configuración vigente y todos sus apartados se describen en [configuración integral 1.8.0](configuracion-integral.md). Esta página conserva el detalle histórico de versiones anteriores.

Para los valores vigentes y la agenda operativa 1.3.0 consultar [agenda operativa](agenda-operativa.md). El horizonte inicial ahora es siete días; la duración inicial es treinta minutos, con máximo de treinta. La configuración incluye DemoBookingEnabled y el catálogo permite horario recurrente. El resto de esta sección documenta la configuración introducida en 1.2.0.


El menú **Configuración** (`/Configuration`) requiere el permiso de administración de accesos. Permite cambiar el rango de reserva, horario general y duración sugerida al crear disponibilidades. El empleado sigue capturando únicamente fecha y hora.

## Valores iniciales

| Clave Medical | Predeterminado | Regla |
|---|---|---|
| BookingHorizonDays | 10 | De 1 a 365 días desde el momento actual |
| Open24Hours | true | Todos los días, las 24 horas |
| OpeningTime | 08:00 | Se aplica solo al desactivar 24 horas |
| ClosingTime | 18:00 | Un cierre menor a apertura termina al día siguiente |
| DefaultSlotMinutes | 15 | De 5 a 120; sugerencia para nuevas disponibilidades |

La apertura global no crea turnos de doctores automáticamente. Los horarios ofrecidos son la intersección de este rango, el horario general, las ventanas del doctor, las personas activas y la ausencia de empalmes. La cita completa debe caber en el horario. Las citas existentes conservan su fecha, duración y estado al modificar la configuración.

Los valores base están en `MedicApp/config/medical.defaults.json`. Al guardar desde la interfaz se escribe `<Medical:DataDirectory>/Configuration/medical.json`, fuera de wwwroot y de Git. El valor normal de DataDirectory es App_Data. El archivo tiene únicamente la sección Medical y las cinco claves de agenda de la tabla; nunca conexiones o contraseñas. Las opciones tipadas validan al arranque; cada nueva solicitud lee el archivo persistente. Se conserva al volver a publicar con el script oficial (DoNotDeleteRule); respaldarlo junto con App_Data. Solo la cuenta del proceso IIS y administradores deben tener acceso de escritura.

Las variables de entorno y secretos conservan la configuración de conexión, zona, nombre y directorio. Para las cinco reglas de agenda el archivo persistente tiene prioridad; editarlo requiere JSON y valores válidos. El formulario valida rangos, registra la configuración en auditoría y compara una revisión para evitar sobrescribir cambios de otra sesión. Si falla la confirmación de auditoría se restaura el archivo anterior. No se requieren migraciones SQL.

## Datos DEMO

La administración dispone de **Ver calendario DEMO** y **Generar datos DEMO**. La generación es explícita, idempotente, con transacción compartida entre los contextos de personas y médico. También se puede ejecutar, tras compilar Release:

```powershell
./scripts/Generar-DatosDemo.ps1 -AdminUserName medicadmin
```

El usuario indicado debe existir, estar activo y administrar accesos; no se crea ni restablece ninguna cuenta. Se generan cuatro personas del catálogo compartido: MEDIC-DEMO-D1 y MEDIC-DEMO-E1/E2/E3; un doctor con licencia MEDIC-DEMO-DOCTOR-001; once ventanas diarias alrededor de la fecha de generación; y hasta 33 citas con estados programada, atendida, cancelada e inasistencia. Los ejemplos aparecen en un calendario administrativo independiente, de solo lectura. La agenda real excluye a doctores DEMO, incluso si se intenta reservar directamente con su ventana. Los participantes DEMO no pueden generar incapacidades ni hechos laborales. No se crean cuentas ni contraseñas de demostración.

Las cuatro personas existen en la base compartida y podrían aparecer como DEMO en consultas generales de otros productos. No se modifica código, asistencia ni overtime de WFMCore. Conservar sus números de empleado y licencia sin editarlos, ya que identifican el lote. La generación no se ejecuta al arrancar o publicar, ni se renueva automáticamente después de vencer las fechas.

## Retiro posterior

Esta versión no borra datos automáticamente. La auditoría `EntityType=DemoBatch` registra el doctor y cuatro inventarios (`CreatePeople`, `CreateDoctor`, `CreateWindows`, `CreateAppointments`) con los identificadores exactos generados. Antes de retirarlos, verificar marcadores e inventarios, y comprobar referencias añadidas por otros productos o usuarios. El orden de retiro revisado será citas, ventanas, doctor y personas, en una transacción, preservando la auditoría del retiro. Si existen referencias ajenas, detener el borrado y revisar; no usar borrados globales por fecha, nombre o UpdatedById. No se eliminarán cuentas reales ni se restaurará la base compartida.
