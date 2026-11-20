# Contexto de MedicApp

Actualizado: 4 de noviembre de 2026. Desarrollo v1.0.0; conservar esta versión. El trabajo se identifica por commits y se publica exclusivamente en MedicApp/site9. Consultar AGENTS.md, README.md y docs antes de modificar. No publicar Trainify ni WFMCore, ni registrar secretos, documentos privados o datos personales en Git.

## Paginación, búsqueda y filtros

Referencia: MWZSystems/Trainify, rama v1.0.0, commit c0ec7fd. El paquete MWZ.Workforce.Shared 1.5.0 instalado no contiene Paging; `MedicApp/Services/Paging.cs` incorpora **sin alteraciones** el archivo de ese commit bajo el mismo namespace. No se cambió el paquete ni el esquema. Cuando una actualización del paquete lo incluya, retirar la copia local para evitar tipos duplicados. La excepción de dos analizadores en .editorconfig está limitada a ese archivo original.

Los modelos de vistas se conservan. `ViewData["Paging:" + nombre]` contiene PageInfo; pagina/tamano/buscar son parámetros GET, con tamaños 25, 50 y 100 (25 predeterminado). Una página numérica fuera de rango se ajusta; texto inválido usa la primera. El tamaño inválido usa 25. Búsqueda limitada a 100 caracteres. Siempre se filtra el alcance autorizado antes de contar y paginar en SQL, con desempate por ID. `_Pager`, `_ListSearch` y `_ListFilters` conservan los demás parámetros; cambiar filtros o buscar elimina solo la página de ese listado. Anterior/Siguiente no usan flechas. Los indicadores de Inicio conservan sus conteos SQL globales autorizados; los dos encabezados del expediente cuentan todo el expediente independientemente de sus páginas y filtros.

| Pantalla/listado | Búsqueda y filtros GET | Paginación |
|---|---|---|
| Citas / List | Nombre o control del empleado o doctor; estado; desde/hasta (intervalo existente de hasta 93 días) | SQL |
| Registro de incapacidades | Folio, nombre o control; estado; desde/hasta (hasta 93 días) | SQL |
| Doctores | Nombre, control, especialidad, cédula; activos/inactivos; horario recurrente/manual | SQL |
| Permisos | Usuario, nombre o control; activos/inactivos; permiso asignado | SQL |
| Auditoría | Entidad, operación, motivo, usuario/nombre/control; entidad, operación y fechas de actualización | SQL |
| Agenda del doctor | Duración en minutos; disponibilidad; próximas/pasadas/todas | SQL (antes se truncaba a 100) |
| Inventario médico | Código, nombre o lote; disponibilidad; bajas/agotadas/con existencias; caducados/próximos/vigentes | SQL |
| Historial de movimientos del artículo | Motivo o usuario/nombre/control; entrada/salida/merma; fechas de registro | SQL |
| Expediente clínico: consultas | Motivo, hallazgos, diagnóstico, tratamiento, receta, notas; fechas de consulta | SQL, prefijo visits |
| Expediente clínico: documentos | Nombre o descripción; tipo documental; fechas de registro | SQL, prefijo files |
| Evidencias de una incapacidad | Nombre de archivo; PDF/imagen; fechas de registro | SQL (antes traía todos) |
| Expediente de incapacidades de una persona | Folio; estado; fechas de cobertura | SQL |

La ficha de un artículo inactivo permite consultar su historial sin el formulario de movimientos; las disponibilidades inactivas no ofrecen desactivar otra vez. Inventario y agenda conservan por defecto solo activos; agenda muestra próximas de forma predeterminada. Los filtros se basan en columnas y estados ya existentes. Ningún listado se pagina en memoria: no hay estados derivados ni tablas calculadas que lo requieran actualmente. Las variantes ToPage se mantienen por compatibilidad y están probadas, pero las pantallas anteriores usan ToPageAsync. Se retiraron los filtros y ordenamiento locales de DataTables para que no aparenten buscar solo entre los registros visibles.

Las búsquedas por identidad usan subconsultas EF componibles `Database.SqlQuery<T>` sobre columnas existentes de dbo.SY_People y dbo.SC_Users, y Contains parametrizado antes del COUNT/OFFSET. Los DTO son tipos no mapeados; no se incorporan al modelo ni requieren migraciones. Esto permite usar una sola consulta/contexto médico sin materializar todos los nombres ni combinar IQueryable de contextos distintos. Los nombres de las filas se resuelven únicamente para los IDs de la página.

Las acciones de fila vuelven por RedirectBack al Referer del mismo sitio y ruta de listado, conservando filtros y página; si no corresponde vuelven a una ruta local segura. En fichas con ID se utiliza una sobrecarga de RedirectBack que compara la ruta con valores explícitos, ya que MVC invalida IDs ambientales al cambiar de acción. Los formularios de creación que parten de una ficha separada conservan su destino contextual de detalle.

## Elementos sin paginación

- Calendarios de citas e incapacidades y horarios disponibles: cuadrículas por intervalo que deben verse completas; mantienen sus límites de rango y permisos.
- Inicio: indicadores agregados en SQL; no contiene un listado ni gráfica de todas las personas.
- Fichas de una persona, consulta seleccionada, artículo o incapacidad, y formularios de creación/edición: un solo registro. Sus historiales asociados sí se paginan.
- Configuración y sus secciones, tipos de documentos, estados y catálogo fijo de tamaños/días de atención: editores o catálogos pequeños.
- Autocompletados de empleados/cuentas/doctores: búsquedas AJAX limitadas a 20 sugerencias, no listados completos con paginador.
- API Labor/Events: contrato de integración con cursor after y limit (máximo 500), no interfaz de listado. Labor/Restrictions devuelve el intervalo autorizado completo (máximo 93 días) para no omitir restricciones; se conserva su contrato.
- Ayuda/manual descargable, acceso y cambio de contraseña: pantallas sin listados.

## Validación

Pruebas de parámetros, límites y dos prefijos independientes; LocalDB verifica búsqueda con total correcto y SQL COUNT/OFFSET/FETCH; páginas renderizadas, filtros, retorno de movimientos y rechazo de Referer externo. La revisión visual utiliza exclusivamente datos sintéticos en LocalDB. No requiere migración ni carga de nuevos datos de producción.

Validación local final: compilación Release con advertencias como errores (0 advertencias/0 errores), 153 pruebas aprobadas sin omisiones, JavaScript válido y revisión de página 2, filtro y búsqueda en navegador con datos sintéticos. xUnit se ejecutó con dotnet sobre MedicApp.Tests.dll porque la política de Windows bloqueó el apphost; no se modificaron protecciones del equipo. Paging coincide exactamente con el archivo de referencia y no hay cambios en modelos de vistas, entidades ni migraciones.

## Conexión del sitio 2026-11-10

- El publicador envía al sitio `ConnectionStrings:WorkforceSite` cuando existe en los secretos locales. `ConnectionStrings:Workforce` queda como conexión de despliegue y solo se usa desde este equipo.
- Rechaza una conexión del sitio que apunte a otra base o que use el mismo usuario que el despliegue. Mientras no exista, avisa y sigue con la de despliegue, como hasta ahora.
- Nuevo `scripts/Configurar-ConexionSitio.ps1`: guarda la conexión del sitio en Secret Manager después de comprobar que el usuario no es dueño de la base y que ya tiene roles `app_`.
- Lo hizo Claude por instrucción del usuario, con Codex detenido. Sin migraciones ni cambios de datos. Plan completo en `USUARIOS_SQL_POR_APLICACION.md` del repositorio Trainify.


## Interfaz común MWZSystem.Ui — 2026-11-11

MedicApp usa el paquete `MWZSystem.Ui` 1.2.0 (carpeta `packages`, versión central en `Directory.Packages.props`). `_Layout.cshtml` decide qué opciones ve la cuenta con las mismas políticas (`ClinicalRecords`, `Inventory`, `Doctor`, `Doctors`, `Access`) y delega el marcado en la estructura común con navegación superior. Se retiraron `_MainNavigation`, `_AccountMenu` y `_Feedback`.

- Los textos de la estructura (avisos, confirmación, etiquetas) se pasan traducidos con `MwzTexts` desde `UiText`; para eso el paquete subió a 1.2.0.
- `medicapp.js` lee sus textos de `#medic-labels` (antes del `<body>`), conserva `App.toast` como puente y confirma con `mwz.confirmar`.
- Los iconos del menú son Bootstrap Icons; dentro de las pantallas sigue `medic-icon`.
- Verificación: 153 pruebas correctas (se ajustaron las que comprobaban el marcado anterior de avisos y del menú). En local se midió la página de acceso a 1280 px y 375 px: 18 px, Nunito Sans, sin desbordes, textos traducidos.
- No verificado: las pantallas con sesión a la vista. La base DEMO local no tiene el esquema de MedicApp y no se pudo iniciar sesión ahí; tampoco se pudieron tomar capturas.

## Tablero de Inicio — 2026-11-11

Subproyecto 2 (tableros y reportes); diseño y plan en el repositorio Trainify, `docs/superpowers`. `MedicalDashboardService` calcula solo conteos por estado y fecha en SQL: lo propio (misma definición que tenía Inicio), la consulta del médico (hoy y semana), la clínica para perfiles `ManageAccess` o `ManageDoctors` (mes por estado, seis semanas, incapacidades vigentes, médicos disponibles) y artículos bajo el mínimo para quien usa inventario. No consulta expedientes, visitas ni documentos. Si falla, Inicio se muestra sin tablero y se registra el error. Textos nuevos en `UiText.resx` (claves `Dash*`); la aplicación solo sirve `es-MX`, no hay recurso en inglés. Se consume `MWZSystem.Ui` 1.4.0. Sin migraciones.

Verificación: `dotnet build MedicApp.slnx` sin errores; 157 pruebas (`DashboardTests`: empleado, médico contra otro médico, perfil de administración, inventario y pantalla por HTTP). No verificado: la pantalla en un navegador, porque la DEMO local no tiene las tablas del esquema `medic` y la aplicación no arranca ahí; tampoco una sesión real en el sitio publicado. No se agregaron descargas a Excel por privacidad clínica (decisión del usuario).

Cierre: fuente `1206aae` en `Releases/v1.0.0` y `main`, publicada en site9 con `scripts/Publicar-MedicApp.ps1 -ProfilePath` (`MEDICAPP_PUBLICADO_Y_VERIFICADO`, sin migraciones). Comprobado de forma anónima: `/` redirige al acceso, `/Account/Login` y `/health` responden 200.

Segunda revisión — 2026-11-12. Subproyecto 4, hallazgos acotados: se borró la vista `Home/Privacy` de la plantilla, que ninguna ruta abría. Verificación: compilación sin advertencias y 157 pruebas con LocalDB. Fuente `58c1df6` en `Releases/v1.0.0` y `main`, publicada solo en site9 (`MEDICAPP_PUBLICADO_Y_VERIFICADO`).

## Modern Web Guidance — 2026-11-20

Revisión contra la guía de Chrome (developer.chrome.com/docs/modern-web-guidance; la CLI `npx modern-web-guidance` no se ejecutó en este equipo). Aplicado por indicación del usuario (propuestas 1 a 4):

- `Program.cs`: `MapStaticAssets().AllowAnonymous()` reemplaza `UseStaticFiles` (la política de respaldo exige sesión, por eso el AllowAnonymous). El middleware pone `no-store` solo si la respuesta no trae su propio Cache-Control, así que páginas, descargas y `/health` siguen sin caché. El paquete incluye 62 archivos .br y 62 .gz.
- DataTables retirado del layout y de `wwwroot/lib`; Select2 y FullCalendar se conservan.
- `Services/ValidationMessages.cs` da claves de UiText (`ValidationMaxLength`, `ValidationMinLength`, `ValidationRange`, `ValidationChoose`) a las reglas de largo y rango sin `ErrorMessage`. No asigna nada a otras reglas: asignar null hace que el atributo falle.
- `medicapp.js`: un aviso por envío inválido.
- Prueba `Login_StaticAssetsAreCacheableAndRulesAreSpanish`.

Pendientes propuestos, sin aplicar: Select2 solo en las páginas que lo usan; CSP sin `'unsafe-inline'` (probar antes en modo reporte), `Permissions-Policy`, `Cross-Origin-Opener-Policy`, quitar `X-Powered-By` y HSTS de un año; `<dialog>` en los detalles de citas e incapacidades; `<meta name="color-scheme">` en MWZSystem.Ui (paquete compartido).

## Librerías por página — MWZSystem.Ui 1.5.0, 2026-11-20

`_Layout` usa `Libraries = MwzLibraries.None` y sigue con `LoadSelect2 = false` (copia propia 4.1.0) y `LoadDataTables = false`. La copia propia de Select2 se carga solo si `shell.Uses(Context, MwzLibraries.Select2)`. Cada vista pide lo suyo en su segunda línea con `MwzShell.Use(Context, ...)`:

- Validation: Account/Login, Account/ChangePassword, Agenda/Index, ClinicalRecords/Create, Inventory/Create, LeaveDocuments/Index.
- Select2: Admin/Access, Admin/Doctors, Appointments/List, Leaves/Calendar, Leaves/Index, LeaveDocuments/Record.
- Las dos: ClinicalRecords/Index, Configuration/Index, Inventory/Item, Leaves/Create.

Una vista nueva con `asp-for` validado o con un `<select>` sin `data-select2="off"` debe pedir su librería; sin ella el formulario sigue validándose en el servidor y el `<select>` queda nativo. Prueba `Pages_LoadOnlyTheLibrariesTheyUse`.

Medido con las páginas renderizadas en pruebas y los tamaños del paquete publicado (comprimidos con Brotli): Inicio y Auditoría pasan de 16 archivos y 123 KB a 10 y 95 KB; Login de 17 y 123 KB a 14 y 105 KB; listados con filtros a 13 y 114 KB; Registrar incapacidad se queda en 16 y 123 KB. En el servidor de pruebas (`WebApplicationFactory`, ambiente Testing) los archivos `_content` de los paquetes responden 500 porque no están en disco; en el paquete publicado sí están y se sirven comprimidos.
