# Agenda operativa 1.3.0

## Configuración actual

- Horizonte: próximos siete días (168 horas desde el momento actual), ajustable de 1 a 365.
- Bloques recurrentes: treinta minutos inicialmente, ajustables de 5 a 30 minutos. El selector muestra inicio y fin, por ejemplo 08:30–09:00. En cada nueva reserva se valida la duración, horizonte, horario y ausencia de empalmes en servidor.
- Atención global: 24 horas, todos los días; Configuración permite turnos diurnos o nocturnos.
- Calendario inicial: semana; mantiene mes, día y agenda. La navegación permite revisar fechas fuera del horizonte, pero solo las fechas con horarios dentro del horizonte se pueden reservar.
- Modo de prueba: se habilita explícitamente para esta instalación. En Configuración se puede desactivar. La configuración base de una instalación nueva mantiene el modo DEMO desactivado para evitar asignaciones involuntarias.

## Catálogo y disponibilidad recurrente

En Doctores, **Usar horario recurrente de la configuración** habilita horarios diarios con duración y atención globales. Las consultas calculan ventanas virtuales sin escribir en la base. Al reservar se materializa una ventana diaria dentro de la misma transacción serializable que la cita, con bloqueo de reserva. La agenda sigue teniendo horarios al pasar la semana inicial; no requiere repetir una carga de fechas. Las citas existentes conservan su duración cuando se cambia la configuración.

Los doctores con horario recurrente desactivado usan las ventanas manuales de Mi agenda. Sus ventanas Generated no se presentan como ventanas manuales. No se ofrecen ventanas manuales mayores de treinta minutos; las reservas ya existentes se conservan. El horario global restringe los bloques completos.

## Privacidad y ocupación

La persona ve detalles y acciones de sus propias citas. El doctor ve las citas asignadas. Los demás eventos programados de los consultorios disponibles se devuelven como **Ocupado · otra persona**, con inicio y fin y sin identificador de cita, empleado, doctor, rowversion ni permisos de acción. No se publican cancelaciones o datos clínicos ajenos. Si existen varios doctores, un intervalo puede estar ocupado con uno y seguir disponible con otro; el servidor asigna al doctor libre. Los empalmes del propio empleado siempre se bloquean.

## Instalación y carga explícita

La migración RecurringDoctorCalendar agrega únicamente dos columnas booleanas: medic.CT_Doctors.UseRecurringSchedule y medic.SY_AvailabilityWindows.Generated. Conserva registros y valores anteriores (false); no modifica tablas compartidas. El script database/01_InstallMedicApp.sql es idempotente y conserva el preflight del destino. El arranque web nunca migra ni genera ejemplos.

Tras compilar Release, el operador autorizado puede ejecutar:

```powershell
./scripts/Preparar-Agenda.ps1 -AdminUserName medicadmin -CalendarAdminUserName administrador
```

El script valida sql1002 / db_acaa35_trainify. El actor debe administrar accesos. Aplica la migración aditiva y crea un consultorio con licencia MEDIC-DEMO-BOOKING, persona MEDIC-DEMO-CONSULTORIO y agenda recurrente. Reutiliza los tres empleados ficticios MEDIC-DEMO-E1/E2/E3 o los crea si faltan. Genera hasta veintiuna citas de treinta minutos a las 08:00, 10:00 y 14:00 de hoy y los seis días siguientes. Las pasadas se marcan atendidas. Los request keys deterministas evitan duplicados; cualquier cita existente que ocupe un intervalo se respeta. La cuenta indicada como CalendarAdminUserName debe existir y recibe solo los permisos médicos de administrar doctores y configuración/accesos, con auditoría; no se cambian credenciales ni roles de WFMCore.

Para esta inicialización solicitada, publicar una vez con `scripts/Publicar-MedicApp.ps1 -ProfilePath <perfil> -InitializeCalendar`. Este parámetro escribe explícitamente la configuración del servidor: siete días, treinta minutos, atención 24 horas y DemoBookingEnabled=true. Publicaciones posteriores deben omitirlo para conservar cambios administrativos. El paquete de Release excluye App_Data y contiene solo los valores base, sin configuración particular ni datos.

El lote anterior de 33 citas se conserva en su calendario administrativo de lectura. Los ejemplos del consultorio nuevo se muestran en Citas y permiten reservar con la sesión real, siempre bajo el aviso MODO DE PRUEBA. No se crean cuentas nuevas ni se generan incapacidades o eventos laborales. Antes de operar con pacientes reales, configurar doctores reales y desactivar DemoBookingEnabled; los doctores DEMO quedan excluidos de la oferta real.

## Retiro y verificación

La auditoría BookingDemoBatch registra los IDs de personas, ventanas y citas generadas; el doctor se identifica por la licencia indicada. GenerateRecurring y las citas posteriores también tienen auditoría. Para retirar los ejemplos posteriormente hay que incluir las nuevas reservas asociadas a este doctor y revisar referencias antes de cualquier borrado. Las personas del lote anterior son compartidas: no borrarlas si tienen referencias externas. No se eliminan registros automáticamente ni se restablecen cuentas.

Se verificaron 63 pruebas: intervalos completos, siete días, recurrencia más allá del lote inicial, cambio de duración sin alterar citas previas, concurrencia, ocupación anónima, permisos y selección/reserva/cancelación por HTTP. El script idempotente se ejecuta dos veces en LocalDB. La comprobación del sitio publicado incluye inicio, login, salud y recursos del calendario.
