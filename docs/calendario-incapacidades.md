# Calendario de incapacidades 1.4.0

El menú **Incapacidades** abre `/Leaves/Calendar`. El listado existente sigue disponible en **Ver lista** y `/Leaves`; conserva las acciones del doctor para emitir, autorizar y cancelar incapacidades.

## Búsqueda y permisos

Doctores activos y cuentas con permisos médicos de administrar doctores o accesos buscan por nombre, apellidos o número de control (EmployeeNumber compartido). El selector solicita al menos dos caracteres, normaliza espacios, limita a veinte coincidencias ordenadas y muestra número y nombre. Incluye personas inactivas para consultar periodos históricos. Seleccionar una persona carga su calendario; también hay un botón Ver calendario. El personal de integración sin permisos médicos administrativos no recibe acceso general a personas.

El empleado accede directamente a sus propias incapacidades y no tiene selector de otras personas. La restricción se aplica en el servidor tanto al seleccionar una persona como al consultar eventos y buscar; modificar personId no permite leer otros periodos. Los endpoints requieren sesión y las respuestas conservan Cache-Control: no-store.

## Periodos y fechas

El calendario contiene solo registros disponibles y autorizados. Borradores y canceladas no representan un periodo vigente de incapacidad y se excluyen. La consulta devuelve únicamente identificador del periodo, título localizado, fechas de la barra, horas locales exactas y último día cubierto; omite folio, doctor, motivos, datos clínicos y rowversion.

Las barras abarcan todos los días afectados, con vistas de mes, semana y agenda. Al abrir un periodo aparecen inicio, fin exacto y último día cubierto. Las horas usan la zona configurada de MedicApp; se transportan como hora local de negocio y no se convierten a la zona del navegador.

Los intervalos almacenados son [inicio, fin). Un periodo del 25 de febrero a las 00:00 al 3 de marzo a las 00:00 marca del 25 de febrero al 2 de marzo inclusive; no pinta un día adicional. Un fin el 10 de marzo a las 14:00 sí incluye el 10 de marzo en la barra. FullCalendar recibe el final exclusivo de los días cubiertos, manteniendo las horas originales en el detalle.

Al buscar una persona, el calendario abre el mes actual si existe un periodo vigente. De otro modo muestra el próximo periodo autorizado o el más reciente del historial. Si no hay periodos, abre el mes actual y muestra el mensaje de ausencia de incapacidades en las fechas visibles. Las consultas de calendario están limitadas a 93 días por petición y hasta 5000 eventos; se filtran y ordenan en SQL.

## Validación y despliegue

Se agregaron once pruebas para fechas con término a medianoche, días parciales, cruce de mes, estados, empalme con rango visible, datos omitidos, búsqueda por nombre/control, personas inactivas, navegación inicial, permisos y flujo HTTP. Las muestras médicas se generan únicamente en una base LocalDB de prueba aleatoria que se retira al terminar. No se crean incapacidades de ejemplo en producción ni hechos laborales.

No hay migración ni cambios de esquema, configuración o datos en esta versión. Publicar con el script oficial sin InitializeCalendar para conservar la agenda semanal y el modo de prueba previamente configurados. Las entregas previas permanecen inmutables; la versión 1.4.0 tiene su propio ZIP y SHA-256. La integración de asistencia/overtime en WFMCore continúa requiriendo trabajo coordinado en ese producto.
