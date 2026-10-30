# Calendario y reserva simplificada — MedicApp 1.1.0

Referencia proporcionada por el usuario: AppStack 3.3.1 bootstrap5, docs/calendar.html. Se adapta el patrón de FullCalendar a MedicApp, con sus estilos y librerías actuales, sin incorporar el bundle completo del template. Se distribuye FullCalendar Standard 6.1.19 y su localización española desde archivos locales, conservando MIT.

La pantalla Citas ofrece mes, semana, día y agenda. En móvil abre agenda semanal y coloca el calendario antes del formulario; el acceso Reservar cita lleva al formulario. Hay indicadores de estado y horarios libres por fecha. La lista SQL paginada se conserva en Appointments/List.

El empleado elige únicamente fecha y hora. No se aceptan persona, doctor, duración ni estado desde el formulario nuevo. La reserva toma la identidad de sesión, elige determinísticamente el primer recurso activo disponible, deriva su duración y valida la ventana. Se comprueban tanto disponibilidad médica como empalmes del propio empleado. Todas las rutas de reserva usan la misma transacción y bloqueo; la clave de idempotencia evita duplicar un envío.

El endpoint Events restringe las citas por persona actual o doctor asignado. La disponibilidad global no devuelve nombres ni identificadores de otros pacientes. Los nombres de empleados aparecen solo en la agenda autorizada del doctor. No se conceden permisos clínicos a administradores por mostrar el calendario.

Los intervalos del feed son fechas de negocio (from inclusiva, to exclusiva, máximo 93 días). Las citas se proyectan como fechas/horas locales de la zona configurada. FullCalendar utiliza UTC como contenedor de esas horas de pared, sin reinterpretarlas en la zona del navegador. La reserva envía Date y Time separados; el servidor convierte con MedicalClock, rechaza horas ambiguas/inexistentes y conserva UTC en SQL. No se envía una hora UTC ficticia como instante de negocio.

El detalle permite cancelar citas futuras propias; un doctor puede registrar atención/inasistencia según las reglas vigentes. Los cambios conservan antiforgery, propietario, rowversion y auditoría. No se habilita arrastrar eventos porque requiere una política de reprogramación separada.

La interfaz usa iconos SVG locales con etiquetas visibles. Se añaden en navegación, agenda, incapacidades y administración, sin fuentes de iconos externas.

Validación: 45 pruebas de reglas, SQL LocalDB y HTTP. Incluyen selección automática, concurrencia, reenvío idempotente, intento de suplantar persona/doctor en el formulario, ocultamiento de citas ajenas, rangos y horarios ocupados. Revisión en navegador con empleado y doctor ficticios, reserva/cancelación de una cita desechable y comprobación móvil a 360 px sin desbordamiento horizontal. Las muestras se crean solo en una LocalDB aleatoria y se retiran al terminar. No se modifican WFMCore, Trainify ni el esquema compartido.
