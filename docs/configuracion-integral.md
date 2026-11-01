# Configuración integral — 1.8.0

El menú **Configuración** abre una lista de accesos a cada apartado y a **Todos los ajustes**. La página tiene navegación lateral en escritorio y enlaces que se acomodan en móvil. **Guardar toda la configuración** guarda el formulario completo. **Recargar configuración** descarta la captura de la página y vuelve a leer los valores actuales.

## Apartados

| Apartado | Valores y acciones |
|---|---|
| General | Nombre visible de 1 a 80 caracteres y zona horaria válida. Se ofrecen zonas de México, UTC y la zona actual si es otra compatible con el servidor. |
| Citas y horarios | Horizonte de 1 a 365 días, duración de 5 a 30 minutos, atención 24 horas o apertura/cierre con turnos nocturnos y reservas de prueba DEMO. |
| Documentos e incapacidades | Tamaño por archivo de 1 a 10 MB, carga de 1 a 20 MB, de 1 a 5 archivos por carga y de 1 a 100 documentos por incapacidad. Enlaces al registro y al expediente documental. |
| Expediente clínico | De 1 a 1000 documentos por persona. Comparte tamaño y cantidad por carga con Documentos. Acceso al módulo clínico. |
| Inventario | Alerta de caducidad entre 1 y 365 días de anticipación, inicialmente 30. Acceso al catálogo; cada artículo define su mínimo al darse de alta. |
| Administración | Doctores cuando la cuenta tiene ese permiso, Permisos, Auditoría y Ayuda y manual. |
| Datos de demostración | Calendario DEMO administrativo y generación explícita de ejemplos. Este bloque está fuera del formulario de ajustes y no se ejecuta al guardarlo. |

La carga máxima debe admitir al menos un archivo de su tamaño máximo. El cupo por incapacidad debe admitir el número de archivos permitido por carga. Los formatos siguen siendo PDF/JPG/PNG y se mantienen las comprobaciones de contenido. Reducir límites no borra registros ni archivos existentes. Las descargas continúan disponibles con su autorización habitual.

Las consultas, recetas, notas y signos vitales siguen siendo opcionales. Los permisos de enfermería se asignan en Permisos. La configuración no concede permisos ni modifica citas, incapacidades, saldos o movimientos existentes.

La zona horaria se usa al mostrar fechas y al calcular nuevos horarios. Los registros conservan sus instantes UTC guardados. Al cambiarla, revisar las fechas que se muestran y la disponibilidad futura. El nombre visible no modifica cuentas ni el nombre del producto en el manual histórico 1.7.0.

## Permisos, persistencia y validación

El controlador y el servicio de configuración verifican el permiso médico de administrar accesos. Se exige antifalsificación en los cambios. Empleados y doctores sin ese permiso no ven el menú ni pueden guardar a través de la URL.

Los valores se guardan de forma atómica en `App_Data/Configuration/medical.json` (o bajo el DataDirectory privado configurado), fuera de wwwroot y de Git. Se conservan al publicar con el script oficial sin InitializeCalendar. El archivo contiene Medical (nombre, zona y agenda), Documents e Inventory. Conexiones, claves, directorios privados y configuración de autenticación siguen administrándose mediante el despliegue; no se reciben del formulario ni se muestran.

Los archivos anteriores que solo contienen Medical siguen siendo válidos, incluso en UTF-8 con BOM. Mientras no existan los nuevos apartados se conservan sus valores base o de despliegue. Un envío antiguo que solo incluye agenda preserva los nuevos apartados actuales. Las opciones de documentos e inventario ahora se leen por solicitud, por lo que una siguiente petición utiliza los valores guardados sin reiniciar la aplicación.

La revisión SHA-256 corresponde al mismo contenido que muestra el formulario. Si cambia desde otra sesión, se devuelve un conflicto y se pide recargar, conservando la captura para revisar. Se audita la revisión del guardado y cada valor que realmente cambió con anterior y nuevo, respetando el tamaño de auditoría existente. Si falla el archivo se revierte la auditoría; si falla la confirmación SQL tras reemplazar el archivo, se restaura su contenido anterior.

El archivo y el control de exclusión corresponden al despliegue IIS actual de una instancia. Si se distribuye MedicApp en varias instancias independientes será necesario coordinar un almacén de configuración común. No se requieren migraciones ni cambios de datos de WFMCore o Trainify para esta entrega.

## Verificación

Se prueban guardado y recarga de todos los apartados, marca HTML escapada, zona horaria, aplicación de límites de documentos y alertas en siguientes solicitudes, entradas inválidas, permisos, antifalsificación, archivo legado con BOM, envío antiguo de agenda, conflicto por revisión y reversión de auditoría cuando falla la escritura de archivo. Se mantienen las pruebas previas de citas, documentos, clínica, inventario y descarga del manual.
