# MedicApp

MedicApp está en desarrollo y mantiene la versión **1.0.0**. Los cambios se acumulan mediante commits y publicaciones del sitio, sin generar una nueva Release por cada ajuste. Las entregas anteriores se conservan como historial.

Aplicación médica.

Aplicación ASP.NET Core MVC / Razor en .NET 10 para citas médicas e incapacidades de empleados. Comparte cuentas y personas con WFMCore; mantiene cookies, claves de protección, permisos y tablas médicas independientes. Interfaz propia sobre Bootstrap 5, Select2, DataTables y SweetAlert2 servidos localmente.

## Arranque local

Requisitos: SDK .NET 10, Windows con SQL LocalDB para las pruebas de integración y acceso a la base común ya instalada de WFMCore para operación. No se necesitan fuentes de Trainify; el paquete compartido versionado está incluido.

```powershell
dotnet restore MedicApp.slnx --configfile NuGet.Config
dotnet build MedicApp.slnx -c Release --no-restore --warnaserror
dotnet test MedicApp.slnx -c Release --no-restore
```

Configurar `ConnectionStrings:Workforce` fuera del repositorio; la base definitiva confirmada es sql1002 / db_acaa35_trainify. Revisar [instalación](docs/instalacion.md) para ejecutar los scripts, habilitar el primer administrador y levantar HTTPS. No hay contraseña predeterminada ni migración automática al inicio.

## Configuración integral 1.8.0

**Configuración** ahora reúne General, Citas y horarios, Documentos e incapacidades, Expediente clínico, Inventario, Administración y Datos DEMO. Permite editar el nombre visible y la zona horaria, las reglas de citas, los límites de archivos y documentos y la anticipación de alertas de caducidad. Los accesos a doctores, permisos, auditoría, expedientes e inventario se encuentran en la barra de menú superior. Solo administra accesos puede guardar. Los ajustes se validan, auditan y conservan al publicar. Ver [configuración integral](docs/configuracion-integral.md).

## Guía de uso 1.7.0

El menú **Ayuda y manual** y el acceso de Inicio permiten descargar un PowerPoint con 31 diapositivas y 29 capturas de la aplicación usando únicamente datos ficticios. La guía explica las funciones y botones para empleados, doctores, enfermería y administradores. La descarga requiere iniciar sesión y usa `Help/Download`; el archivo se publica desde `MedicApp/Guides`, fuera de wwwroot. Ver [guía y mantenimiento](docs/guia-de-uso.md).

## Expediente clínico e inventario 1.6.0

**Expediente clínico** permite buscar por nombre o número de control, registrar consultas, notas, recetas y signos vitales opcionales, y adjuntar archivos generales o por consulta. **Inventario médico** controla medicamentos/insumos por lote y caducidad, entradas, salidas y bajas con historial. Doctores y administradores tienen acceso; enfermería recibe permisos independientes desde **Accesos**. El registro no es obligatorio para usar citas o incapacidades. Ver [uso, permisos y migración](docs/expediente-inventario.md).

## Iconos e interfaz 1.5.1

La pantalla de acceso, inicio, menús, formularios, tablas y acciones utilizan iconos SVG locales consistentes. Las etiquetas siguen visibles y los iconos decorativos no se anuncian por lectores de pantalla. Sin cambios de reglas, permisos ni esquema.

## Evidencias y expediente documental 1.5.0

Registro incluye **Agregar incapacidad** para doctores y administración médica. Tras capturar se adjuntan documentos PDF/JPG/PNG desde la incapacidad; cada fila permite consultar sus evidencias y el expediente documental de la persona. Los archivos se cifran y conservan fuera de wwwroot; SQL almacena solo metadatos. El titular, el doctor responsable y los administradores médicos tienen acceso a la consulta. Solo el doctor responsable puede autorizar o cancelar el periodo. Ver [evidencias y expediente](docs/evidencias.md) para permisos, límites, migración y respaldos.

## Registro y consulta de incapacidades 1.4.1

El menú **Incapacidades** tiene dos opciones: **Registro de incapacidades**, donde el doctor captura y administra las incapacidades como antes, y **Calendario de incapacidades**, para consultar sus periodos por persona. El borrador guardado se autoriza desde Registro antes de aparecer en el calendario; se conservan permisos, acciones y búsqueda.

## Calendario de incapacidades 1.4.0

**Incapacidades** abre un calendario por persona con barras que cubren los días autorizados. Doctores y administradores buscan por nombre o número de control; el empleado consulta únicamente sus propios periodos. Se incluyen mes, semana, agenda y detalle con horas exactas. La búsqueda admite personas inactivas para consultar antecedentes. Ver [calendario de incapacidades](docs/calendario-incapacidades.md).

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

`database/01_InstallMedicApp.sql` es el script idempotente generado desde las migraciones EF de MedicDbContext, con preflight. Crea ocho tablas en medic: CT_Doctors, SY_AvailabilityWindows, SY_Appointments, SY_MedicalLeaves, SC_AccessProfiles, AR_AuditEntries, IX_LaborEvents y SY_LeaveDocuments; además, dos vistas y su historial de migraciones. No crea ni altera tablas de WFMCore/Trainify.

## Alcance pendiente de despliegue

El código y los scripts están preparados para revisión e instalación. La base compartida no se migra ni se siembra automáticamente. La integración efectiva con cálculos de WFMCore requiere incorporar y probar el consumidor en su proyecto; consultar [contrato laboral](docs/integracion-wfmcore.md). No se incluyen expediente clínico, recetas, archivos clínicos, familiares/externos, importación histórica ni liquidación de nómina.

Las pruebas crean y eliminan exclusivamente una base LocalDB aleatoria `MedicApp_Test_GUID`. Las credenciales de prueba son sintéticas y generadas en memoria. Ver [validación](docs/validacion.md) y [decisiones](docs/adr/0001-compartir-identidad-y-separar-medic.md).

## Entrega 1.0.0

Repositorio: https://github.com/MWZSystems/MedicApp. Los metadatos y las instrucciones de la entrega están en `Releases/v.1.0.0`. El ZIP de aplicación y su checksum se distribuyen como assets del release `v1.0.0`, sin conexión SQL, perfiles privados, claves de sesión ni datos.

La integración automática con asistencia y overtime continúa pendiente de incorporar y publicar en WFMCore. El esquema propio y la cuenta administrativa de MedicApp ya se instalaron; publicar el sitio no vuelve a aplicar esos cambios.

## Calendario de citas 1.1.0

Citas abre el calendario compartido de la interfaz, con alcance por usuario. El empleado ve solo sus citas; cada doctor ve las propias y las asignadas. Reservar requiere fecha y hora. El empleado, doctor disponible, duración y auditoría se resuelven en el servidor. Las flechas, vistas, navegación por días y detalle permiten consultar y cancelar sin salir del calendario. La lista paginada sigue disponible como alternativa.

La disponibilidad depende de doctores activos y ventanas previamente configuradas en Mi agenda; no se inventan horarios ni se crean reservas fuera de agenda. El horizonte de reserva configurado se conserva. No hay cambio SQL en esta versión.

La versión de MedicApp se muestra en una barra inferior fija en todas las pantallas, incluido el acceso, y se obtiene del ensamblado de la aplicación. Durante el desarrollo permanece en v1.0.0.

## Avisos emergentes

MedicApp usa el mismo diseño de avisos de Trainify, adaptado a las variables y al catálogo de iconos de MedicApp. El componente común está en `Services/Feedback.cs`, `Views/Shared/_Feedback.cshtml`, `wwwroot/js/medicapp.js` y `wwwroot/css/medicapp.css`. Todos los módulos y el login comparten `_Layout`, que incluye la parcial antes de los scripts. La versión de desarrollo sigue siendo **1.0.0**.

| Tipo | Título | Cuándo usarlo | Cierre automático |
|---|---|---|---|
| `success` | Listo | Acción completada: guardar, registrar, cargar o generar | 5 segundos |
| `info` | Información | Sin cambios, por ejemplo una reserva reenviada que ya existía | 7 segundos |
| `warning` | Atención | Acción con consecuencia o algo pendiente, como cancelación o borrador | 9 segundos |
| `error` | Error | No se pudo completar, formulario inválido, regla o conflicto | No; botón Cerrar aviso |

### Uso en servidor y JavaScript

En un controlador que importe `MedicApp.Services`, agregar el aviso después de completar la operación y antes de redirigir:

```csharp
await servicio.GuardarAsync(input, ct);
this.Success("Saved"); // Clave de UiText.resx, traducida al renderizar.
return RedirectToAction(nameof(Index));
// También: this.Info("Sin cambios."), this.Warning("Falta adjuntar evidencia."), this.Error("UnexpectedError").
```

Para mensajes nuevos reutilizables, agregar una clave con texto breve en español a `Resources/UiText.resx`. `Feedback.Add(TempData, FeedbackKind.Success, texto)` está disponible cuando no se utiliza la extensión. La lista JSON en `TempData["Feedback"]` permite varios avisos tras un Post/Redirect/Get. Se ignoran textos vacíos, se eliminan duplicados y se consumen una sola vez. Las claves anteriores `Success`, `Info`, `Warning` y `Error` siguen admitidas.

```javascript
window.App.toast("success", "Cambios guardados correctamente.");
window.App.toast("info", "No hubo cambios.");
window.App.toast("warning", "Falta adjuntar evidencia.");
window.App.toast("error", "No se pudo guardar. Intenta nuevamente.");
```

Los textos se insertan como texto, nunca como HTML. Las respuestas JSON del calendario pueden añadir `kind` y `message`; no se encolan también en TempData, para no repetir el mismo aviso en la siguiente navegación. No mostrar mensajes de excepciones, SQL, contraseñas, archivos ni detalles clínicos en un aviso.

### Acciones cubiertas

| Acción | Aviso |
|---|---|
| Iniciar sesión | Éxito; advertencia si debe cambiar contraseña; error si se rechaza el acceso |
| Cerrar sesión | Éxito |
| Cambiar contraseña | Éxito o error de validación |
| Agregar horario de agenda | Éxito |
| Desactivar horario | Éxito; error si no se permite |
| Guardar/activar doctor | Éxito |
| Desactivar doctor | Advertencia por impedir nuevas citas |
| Guardar permisos | Éxito |
| Reservar cita, formulario y calendario | Éxito; información si el mismo RequestKey ya estaba reservado |
| Cambiar estado de cita | Éxito; advertencia al cancelar |
| Registrar incapacidad | Éxito y advertencia de borrador pendiente de aprobación |
| Aprobar incapacidad | Éxito |
| Cancelar incapacidad | Advertencia |
| Adjuntar evidencia a incapacidad | Éxito |
| Registrar consulta en expediente | Éxito |
| Subir documentos al expediente | Éxito |
| Agregar artículo al inventario | Éxito |
| Registrar movimiento de inventario | Éxito |
| Guardar configuración | Éxito |
| Generar datos DEMO, solo con la acción explícita existente | Éxito |
| Formulario inválido, permisos rechazados, reglas, concurrencia y error inesperado | Error amable sin detalles técnicos; se conservan los estados HTTP |
| Calendario sin disponibilidad / fallos de solicitudes | Información de disponibilidad / error |

No se agregan acciones de eliminación ni se cambian reglas, permisos, transacciones, reservas, datos o esquema. En los formularios se conservan los errores junto a los campos; los errores generales se muestran también como toast. Si solo hay errores de campo se muestra «Revisa los datos marcados en el formulario.». Los POST HTML rechazados con 400 mantienen ese estado y presentan una página con el aviso. Los errores globales HTML mantienen el layout y la versión; las API conservan ProblemDetails. El entorno Development puede mostrar su página de diagnóstico de ASP.NET para excepciones; Testing y producción usan el manejador global.

### Comportamiento y accesibilidad

Los avisos se ubican debajo de la cabecera y se recolocan al abrir menús o cambiar el tamaño. En teléfono ocupan el ancho disponible con márgenes. Los errores aparecen primero; los restantes conservan el orden. Todos tienen cierre con etiqueta accesible y foco de teclado. Error y advertencia usan `role="alert"`; éxito e información `role="status"`; el contenedor tiene `aria-live="polite"`. El temporizador conserva el tiempo restante al pasar el cursor, enfocar o dejar la pestaña en segundo plano. Los errores permanecen hasta cerrarlos. La animación respeta `prefers-reduced-motion`. Las variables `--app-toast-*` definen colores con contraste y variantes para `data-bs-theme="dark"`; no se agrega un nuevo modo oscuro al producto.

Las comprobaciones incluyen pruebas unitarias de prioridad, consumo, duplicados y compatibilidad; pruebas HTTP de PRG, login, formularios inválidos, status 400 y errores HTML/JSON; y revisión local en escritorio y a 390 px con datos ficticios. No hay pantalla ni ruta de demostración de toasts en producción.

## Navegación por categorías

La cabecera reúne en una sola barra el logo a la izquierda, las categorías Inicio, Atención médica, Recursos médicos, Administración y Configuración al centro, y el menú de usuario a la derecha. El avatar y nombre abren Mi sesión con Ayuda y manual, Cambiar contraseña y Cerrar sesión. El cierre conserva su formulario POST protegido. Cada categoría muestra únicamente las opciones autorizadas. Por debajo de 1400 px los apartados se recogen en el botón Menú; en teléfono el logo, el botón y el avatar comparten la misma fila. Los avisos emergentes respetan también los paneles desplegados.

Los menús principales usan texto de 18 px e iconos de 20 px; los desplegables y el menú de usuario usan 17 px, con filas de al menos 52 px y más separación entre opciones. En anchos menores a 1400 px se utiliza el botón Menú para conservar esta legibilidad sin comprimir la cabecera.

## Pantallas sin navegación duplicada

Los accesos generales a módulos y a los apartados de Configuración se encuentran en la barra superior. Las pantallas no repiten sus pestañas ni botones de navegación general. Se mantienen las acciones de trabajo (agregar, guardar, reservar, adjuntar, descargar), cancelar formularios, paginar, alternar vistas que no están en el menú y abrir un expediente, evidencia o calendario ya filtrado por persona. Inicio conserva sus indicadores y la acción de reservar cita. Configuración aprovecha todo el ancho para sus campos y conserva guardar/recargar y las operaciones DEMO existentes.

## Datos visuales DEMO por cuenta

El generador administrativo `scripts/Generar-Datos-Visuales.ps1` agrega un lote de dos meses a una cuenta existente: 12 citas de 30 minutos (6 por mes) y 4 incapacidades en borrador (2 por mes). Comprueba simultáneamente usuario y número de control, exige un actor activo con permiso de administrar accesos y reutiliza el doctor ficticio del lote DEMO. No crea cuentas, personas, permisos, consultas ni documentos, ni modifica registros existentes o la configuración de reservas.

```powershell
.\scripts\Generar-Datos-Visuales.ps1 -UserName 'empleado.ejemplo' -EmployeeNumber '0000' -ActorUserName 'cuenta.admin' -FirstMonth '2026-11-01' -TimeZoneId 'America/Chihuahua'
```

Verificar primero la zona del sitio y compilar Release. El script utiliza la conexión privada de UserSecrets y valida el destino de base común. No ejecutar el generador para una persona sin su solicitud o autorización. No existe un endpoint web ni una generación automática al publicar.

Las citas del doctor ficticio llevan el prefijo DEMO en el calendario. Las incapacidades se identifican por `MEDIC-DEMO-VISUAL-`, mantienen estado Borrador y se muestran como ejemplos en el calendario de la persona; los borradores ordinarios siguen sin mostrarse. El detalle advierte que son ficticias. Su autorización, cancelación y carga de evidencias están bloqueadas. No se insertan eventos laborales, por lo que no generan efectos en asistencia u overtime. Las disponibilidades auxiliares son generadas y no se ofrecen como nuevos horarios manuales.

El lote se guarda con una transacción y bloqueo, con claves deterministas para las citas. Repetir cuenta y mes inicial conserva el lote sin duplicarlo. La auditoría registra `VisualSampleBatch`, `VisualSampleAppointment`, `VisualSampleLeave` y `VisualSampleWindow`, con el mismo identificador de lote y los IDs exactos para preparar una retirada posterior revisada. No borrar por rangos de fechas ni incluir datos de personas o resultados de ejecución en Git. Las muestras de una persona mantienen los permisos habituales de lectura; no quedan visibles para otros empleados.

## Inventario médico de demostración

Después de compilar Release, ejecutar `scripts/Generar-InventarioDemo.ps1 -ActorUserName 'cuenta.admin'` con un administrador activo. Utiliza la conexión privada y comprueba la base común autorizada. Crea 15 artículos con códigos `MEDIC-DEMO-STOCK-`, nombres y lotes DEMO: jeringas, material de curación, medicamentos e instrumentos. Incluye 45 movimientos ficticios (entrada, salida y merma por artículo), con saldos coherentes y ejemplos de existencia baja, agotado, próximo a caducar y caducado. Son ejemplos de pantalla, sin entrega real ni indicaciones de uso clínico.

La generación es transaccional e idempotente: repetirla no repone existencias ni duplica el lote; un código reservado previo sin auditoría aborta la carga. No modifica artículos existentes, personas, expedientes, citas, incapacidades ni eventos laborales. La auditoría `InventorySampleBatch`, `InventorySampleItem` e `InventorySampleMovement` registra los IDs exactos para una retirada posterior revisada. No se ejecuta automáticamente al publicar y no crea un endpoint web. Buscar DEMO en Recursos médicos / Inventario médico para verlos.

## Listados paginados

Los listados usan búsqueda y filtros desde el servidor, con páginas de 25, 50 o 100 registros y conservación de filtros al navegar o guardar una fila. La base solo devuelve la página solicitada. El expediente clínico mantiene controles independientes para consultas y documentos. Ver [CONTEXTO.md](CONTEXTO.md) para pantallas, filtros, referencia Trainify c0ec7fd y excepciones. No requiere cambios de esquema ni migraciones.
