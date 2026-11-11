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
