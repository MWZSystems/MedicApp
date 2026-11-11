# Changelog

## [Desarrollo 1.0.0] - 2026-11-11 (interfaz común)

- MedicApp adopta `MWZSystem.Ui` 1.2.0, la base de interfaz común de las aplicaciones de MWZSystems, con navegación superior: el menú con sus grupos, el menú de la cuenta, los avisos emergentes, la barra de carga y la ventana de confirmación son del paquete. Las opciones dependen de las mismas políticas de acceso.
- Letra Nunito Sans con base de 18 px y mínimo de 16 px (había textos de 9 a 15 px). Se conserva el color verde azulado.
- Los avisos conservan sus cuatro tipos y sus textos traducidos; `App.toast` y `Feedback` siguen funcionando igual. La confirmación ya no usa SweetAlert2.
- Se retiran SweetAlert2 y las copias locales de Bootstrap y jQuery. Select2, DataTables y FullCalendar se conservan.
- Sin cambios de datos, permisos ni migraciones.

## [Desarrollo 1.0.0] - 2026-11-11

- Se retira el CSS del template comercial Insove (`wwwroot/lib/insove`). La base visual ahora es propia: `wwwroot/css/medicapp-base.css`, escrita para MedicApp sobre Bootstrap (MIT).
- Sin cambios de pantallas, datos ni migraciones; se conservan colores, tipografía y componentes. `THIRD-PARTY-NOTICES.md`, `README.md` y la documentación reflejan que la interfaz ya no depende de licencias comerciales.
- `LICENSE` deja de ser MIT: derechos reservados a nombre de MWZSystems, por decisión del usuario, para poder comercializar la aplicación. Los componentes de terceros conservan sus licencias (`THIRD-PARTY-NOTICES.md`).
- El archivo retirado sigue en el historial de Git anterior a esta fecha.

## [Desarrollo 1.0.0] - 2026-11-10

- El publicador envía al sitio `ConnectionStrings:WorkforceSite` cuando existe en los secretos locales. `ConnectionStrings:Workforce` queda como conexión de despliegue y solo se usa desde este equipo.
- Rechaza una conexión del sitio que apunte a otra base o que use el mismo usuario que el despliegue. Mientras no exista, avisa y sigue con la de despliegue, como hasta ahora.
- Nuevo `scripts/Configurar-ConexionSitio.ps1`: guarda la conexión del sitio en Secret Manager después de comprobar que el usuario no es dueño de la base y que ya tiene roles `app_`.
- Lo hizo Claude por instrucción del usuario, con Codex detenido. Sin migraciones ni cambios de datos. Plan completo en `USUARIOS_SQL_POR_APLICACION.md` del repositorio Trainify.

## [Desarrollo 1.0.0] - 2026-11-04

- Paginación EF en servidor para los 12 listados, con búsqueda, filtros GET, tamaños 25/50/100, total filtrado, orden estable y conservación de página al guardar acciones de fila.
- Paging.cs de Trainify c0ec7fd sin cambios, metadatos en ViewData y prefijos independientes para consultas y documentos clínicos; se conservan los modelos de vistas y el esquema, sin migraciones.
- Paginador numérico sin flechas, búsqueda con limpiar, controles que conservan filtros e indicadores de todo el alcance; retirados los filtros locales de DataTables.
- CONTEXTO.md documenta pantallas, filtros y exclusiones, sin listados paginados en memoria.

## [Desarrollo 1.0.0] - 2026-11-03

- Inventario DEMO administrativo e idempotente: 15 artículos y 45 movimientos auditados, sin modificar existencias reales ni datos laborales.

- Generación administrativa e idempotente de citas e incapacidades visuales DEMO para una cuenta y dos meses, con auditoría de IDs para retirada posterior.
- Incapacidades ficticias visibles en calendario como borradores identificados, sin autorización, evidencias ni eventos laborales; no alteran datos existentes, permisos ni configuración.

- Limpieza global de pestañas y botones de navegación repetidos con la barra superior, incluida la navegación secundaria de incapacidades y los atajos generales de Inicio y Configuración.
- Configuración utiliza todo el ancho; se conservan acciones de trabajo y enlaces contextuales a personas y registros.

- Aumentada la letra y separación de los menús principales, desplegables y de usuario. Se conserva la barra única mediante el menú compacto en anchos menores a 1400 px.

- Cabecera unificada con logo, categorías y menú de usuario en una sola barra. Contraseña, ayuda y cierre de sesión agrupados bajo el avatar.
- Menú móvil desplegable sin una segunda fila permanente y avisos recolocados debajo de los paneles abiertos.

- Navegación principal organizada en categorías desplegables, una fila en escritorio y menú compacto en teléfono y tableta.
- Accesos agrupados por atención médica, recursos, administración, configuración y ayuda/cuenta, respetando permisos y manteniendo el módulo activo.

## [Desarrollo 1.0.0] - 2026-11-02

- Avisos emergentes unificados de éxito, información, advertencia y error con diseño de Trainify, accesibilidad y adaptación a teléfono.
- Mensajes para todas las acciones de escritura, PRG, formularios y errores globales HTML; respuestas JSON conservadas.
- Documentación de tipos, acciones y ejemplos para agregar avisos. Sin cambios de esquema ni nueva Release.

## [1.8.1] - 2026-11-01

- Retiradas las flechas decorativas de los botones y enlaces de Inicio y eliminado su icono del catálogo común.
- Sin cambios de datos, permisos ni configuración.

## [1.8.0] - 2026-11-01

- Menú de Configuración con accesos por apartado y navegación interna adaptable a móvil.
- General: nombre visible de la aplicación y zona horaria. Citas: horizonte, duración, horario de atención y modo de reservas de prueba.
- Documentos: tamaños, archivos por carga y cupos por incapacidad y por expediente clínico. Inventario: días de anticipación de avisos de caducidad.
- Accesos a doctores, permisos, auditoría, incapacidades, expediente, inventario y datos DEMO según las autorizaciones existentes.
- Opciones tipadas por solicitud, validación de rangos y relaciones, persistencia compatible con archivos anteriores, revisión de concurrencia y auditoría por ajuste.
- Sin cambios de esquema ni inicialización de datos. El publish conserva los ajustes existentes.


## [1.7.0] - 2026-11-01

- Guía PowerPoint de 31 diapositivas y 29 capturas reales con datos ficticios para empleados, doctores, enfermería y administración.
- Nuevo menú Ayuda y manual, acceso desde Inicio y descarga autenticada del PPTX como adjunto. Se incluye el archivo en el publish fuera de wwwroot.
- Instrucciones de funciones y botones para citas, incapacidades, evidencias, expediente clínico, inventario, doctores, permisos, configuración y auditoría.
- Actualizado el texto de privacidad de Inicio para reflejar el expediente clínico actual. Sin cambios de esquema, datos o configuración.


## [1.6.1] - 2026-10-31

- Botón accesible con icono y texto para mostrar/ocultar la contraseña del login. Inicia oculta y vuelve a ocultarse al enviar el formulario o salir de la página.
- Recurso JavaScript local, sin guardar contraseñas ni cambiar autenticación, permisos, esquema o configuración.
- Actualizada la comprobación del instalador idempotente para las doce tablas médicas introducidas hasta 1.6.0.


## [1.6.0] - 2026-10-30

- Expediente clínico por nombre/control con consultas, recetas, notas y nueve signos vitales opcionales; autor/fecha y registros conservados sin sobrescribir.
- Archivos PDF/JPG/PNG cifrados por persona o consulta, categorías, límites configurables, auditoría, descarga autorizada e idempotencia.
- Inventario médico por lote/caducidad, mínimos, alertas, entradas/salidas/bajas con historial y saldos; evita negativos, duplicados y entrega de lotes caducados.
- Permisos independientes para enfermería en Accesos y nuevos menús con iconos. No afecta el flujo de citas/incapacidades ni descuenta medicamentos al registrar recetas.
- Migración aditiva de cuatro tablas y dos permisos médicos, sin cambios al esquema compartido de WFMCore.


## [1.5.1] - 2026-10-30

### Interfaz
- Iconos locales de acceso, identificación y contraseña en la entrada; distintivos en inicio y tarjetas de resumen.
- Iconos consistentes en menús, formularios, tablas, filtros, navegación y documentos; estilos adaptados a móvil.
- Se conservan etiquetas visibles y los SVG son decorativos para lectores de pantalla. Sin cambios de permisos, datos o reglas de negocio.

## [1.5.0] - 2026-10-30

### Agregado
- Botón Agregar incapacidad para doctores y administración médica; el administrador asigna un doctor responsable, sin autorización clínica automática.
- Carga de PDF/JPG/PNG en cada incapacidad, consulta/descarga protegida y expediente documental por nombre o número de control.
- Almacenamiento cifrado fuera de wwwroot, metadatos y SHA-256 en medic.SY_LeaveDocuments; auditoría de carga y descarga.
- Validación de extensiones, firmas y límites; idempotencia, bloqueo por incapacidad y compensación de archivos si falla SQL.
- Migración aditiva LeaveDocumentEvidence y actualizador de esquema explícito sin generación de datos.

### Seguridad
- Lectura para titular, doctor responsable y administradores médicos; cargas para responsables y administración, sin URLs públicas ni borrado desde la interfaz.
- Hasta 10 MB por archivo, 5 archivos/20 MB por envío y 20 documentos por incapacidad inicialmente. Configuración tipada Documents.

## [1.4.1] - 2026-10-30

### Corregido
- Incapacidades ofrece dos entradas explícitas: Registro de incapacidades y Calendario de incapacidades.
- Registro conserva la captura del doctor y las acciones de autorización/cancelación; el calendario es de consulta.
- Navegación entre ambas secciones y enlace al calendario desde cada periodo autorizado.
- Mensajes de captura explican el flujo de borrador a autorización antes de mostrarse en el calendario.
- Se conservan los permisos y la búsqueda por nombre o número de control, sin cambios de esquema o datos.

## [1.4.0] - 2026-10-30

### Agregado
- Calendario de incapacidades autorizadas con barras de varios días, vistas de mes, semana y agenda y detalle de horas exactas.
- Búsqueda por nombre completo o número de control, incluyendo personas inactivas para consultas históricas autorizadas.
- Navegación inicial al periodo vigente, próximo o más reciente de la persona seleccionada.

### Seguridad
- Los empleados solo consultan sus propios periodos; doctores y administradores médicos pueden buscar personas. Validación también en los endpoints.
- Respuesta limitada a fechas de incapacidades autorizadas; no incluye folios clínicos, motivos de cancelación ni versiones.
- Sin migraciones, generación de incapacidades ni cambios de configuración de agenda.

## [1.3.0] - 2026-10-30

### Corregido
- La agenda principal puede reservar en un consultorio DEMO identificado, con disponibilidad recurrente y horarios iniciales cargados.
- Selector con rangos completos (08:00–08:30), carga al abrir la página y vista semanal por defecto.

### Agregado
- Rango inicial de siete días y bloques recurrentes de treinta minutos; configurables, con máximo de treinta minutos para nuevas reservas.
- Horario recurrente por doctor desde el catálogo, sin capturar cada día ni generar disponibilidad al consultar.
- Citas ajenas visibles como intervalos ocupados anónimos, sin identidad, identificador clínico, versión ni acciones.
- Inicializador explícito con veintiuna citas de ejemplo, permisos auditados para la cuenta indicada y modo DEMO desactivable.
- Migración aditiva de Generated y UseRecurringSchedule exclusivamente en tablas médicas.

## [1.2.0] - 2026-10-30

### Agregado
- Administración de configuración persistente: rango de días, atención de 24 horas por defecto, turnos diurnos/nocturnos y duración sugerida de nuevas citas.
- Archivo config/medical.defaults.json y override protegido en App_Data/Configuration/medical.json, validación y auditoría.
- Generador idempotente de cuatro personas DEMO, un doctor, once ventanas y hasta 33 citas; calendario administrativo independiente.

### Seguridad
- Horario validado al consultar y reservar; permisos de administración y protección antiforgery.
- Identificadores DEMO registrados para su retiro posterior. Sin cuentas nuevas, incapacidades, eventos laborales ni migraciones SQL.
- Exclusión del doctor DEMO de las reservas reales. Las personas DEMO se identifican en el catálogo compartido.

## [1.1.0] - 2026-10-30

### Agregado
- Calendario de citas con vistas de mes, semana, día y agenda, basado en la referencia AppStack.
- Reserva con fecha/hora y asignación automática de un doctor disponible; identidad tomada de la sesión.
- Consulta y cancelación desde el detalle de la cita, con actualización del calendario y horarios.
- Iconos SVG locales en navegación, formularios y acciones.

### Seguridad
- Alcance propio del empleado y agenda asignada del doctor; validación de disponibilidad, idempotencia y concurrencia en servidor.
- Sin cambios de esquema ni migraciones de datos.


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
