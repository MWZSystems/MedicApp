# Expediente clínico e inventario — 1.6.0

## Uso del expediente

Abrir **Expediente clínico**, buscar nombre completo o número de control y abrir a la persona. **Agregar consulta** propone la fecha y hora actual de la zona de la aplicación. Todos los campos clínicos son opcionales: motivo, exploración, diagnóstico, tratamiento, receta/medicamentos/dosis, notas, peso, estatura, temperatura, presión sistólica y diastólica, pulso, respiración, saturación y glucosa. Se registran unidades junto al campo. El servidor valida límites de captura; no calcula diagnósticos, dosis ni recomendaciones.

La consulta no puede ser futura (tolerancia de cinco minutos). Queda con autor y fecha de captura; las aclaraciones se agregan como nuevas consultas/notas de seguimiento y no sobrescriben lo anterior. Un reenvío con la misma clave no duplica la consulta. Las consultas no son requisito para citas ni incapacidades; tampoco modifican automáticamente su estado.

**Subir archivos al expediente** admite PDF, JPG y PNG generales o, desde **Adjuntar archivos a esta consulta**, asociados a una atención de la misma persona. Las categorías son general, receta, estudio/resultado, medicamento y otro; la descripción es opcional. El formulario de archivos es independiente de la consulta, por lo que no se pierde una atención al rechazar un archivo. Las consultas se paginan de diez en diez y los archivos de veinticinco en veinticinco.

El expediente de documentos de incapacidades conserva su módulo y permisos existentes. El acceso desde el expediente clínico solo se muestra a usuarios que tienen ese permiso. Los archivos previos no se trasladan ni duplican.

## Permisos

- Doctores activos y administradores de accesos médicos pueden utilizar ambos módulos. Un permiso de administrar únicamente el catálogo de doctores no concede por sí solo acceso a notas clínicas.
- En **Accesos**, la administración puede habilitar por separado **Expedientes clínicos (personal de enfermería)** e **Inventario médico (personal de enfermería)** para cuentas compartidas existentes. No se crean cuentas, contraseñas ni doctores ficticios para enfermería.
- El permiso clínico autoriza al personal a consultar y añadir registros de las personas. Cada apertura de expediente, alta de consulta, carga y descarga deja auditoría por identificador y usuario. Las notas y el contenido de archivos no se copian a la auditoría ni a logs.
- Los empleados sin esos permisos no acceden al módulo clínico ni a sus archivos, incluso cambiando URL/identificadores. El permiso de inventario no concede acceso clínico ni permiso para autorizar incapacidades.
- La búsqueda incluye personas inactivas para antecedentes; no permite agregar información a personas inactivas. Las personas DEMO no pueden recibir ni generar expedientes reales.

## Archivos y respaldos

Se reutiliza el almacenamiento cifrado de evidencias: archivos fuera de wwwroot, nombres GUID y protección con las claves persistentes de MedicApp, comprobación de extensión, firma, tamaño e integridad SHA-256. Las descargas autorizadas se sirven como adjuntos y con no-store. No hay enlace público ni archivos en Git o Releases.

Valores en `Documents` de `config/medical.defaults.json`: 10 MB/archivo, 5 archivos/carga, 20 MB/carga y `MaxDocumentsPerPatient` 200 documentos/persona (configurable de 1 a 1000 mediante la configuración de despliegue). No se recortan archivos existentes al reducir un límite. La operación usa transacción, clave idempotente y compensación de archivos si falla la escritura SQL. La copia de seguridad debe conservar **SQL + App_Data/Evidence + App_Data/Keys** juntos. El publish preserva App_Data.

## Inventario

En **Inventario médico → Agregar medicamento o insumo**, registrar código único por lote, nombre y unidad; lote/caducidad son opcionales para insumos que no los requieren. La existencia inicial es cero y se carga mediante una entrada. Registrar cada lote con un código distinto evita mezclar caducidades y saldos.

Desde **Historial de movimientos**, ingresar entradas, salidas/entregas o bajas/mermas, con cantidad y motivo/referencia. Se admiten hasta tres decimales. Se conserva autor, fecha, cantidad y saldo tras cada movimiento. No se edita ni elimina el historial: corregir mediante un movimiento compensatorio. La búsqueda admite código, nombre y lote; se señalan existencias bajas, lotes caducados y próximos a caducar en 30 días.

La transacción y un bloqueo SQL serializan movimientos simultáneos; claves únicas impiden duplicados. No se permiten saldos negativos ni entregar lotes caducados; sí es posible darlos de baja. Registrar una receta no descuenta existencias: la entrega se registra explícitamente. No hay inventario de prueba ni expedientes ficticios en producción. No se implementan compras, facturación o una receta electrónica certificada.

## Base de datos y despliegue

Migración aditiva `ClinicalRecordsAndInventory`: `medic.SY_ClinicalVisits`, `medic.SY_PatientDocuments`, `medic.CT_StockItems` y `medic.IX_StockMovements`, más dos permisos en `medic.SC_AccessProfiles` inicialmente desactivados. Claves foráneas restrictivas a los catálogos compartidos, índices, restricciones de rangos, rowversion y auditoría. No modifica personas, Identity, citas, incapacidades ni las vistas de integración laboral.

Compilar y probar, aplicar explícitamente `scripts/Actualizar-Esquema.ps1 -AdminUserName medicadmin` y publicar solo site9 con el script oficial, sin InitializeCalendar. El inicio web no ejecuta migraciones. El instalador SQL idempotente se regenera desde EF; si existe MedicAppRuntime, la migración deniega actualizar/borrar consultas, archivos y movimientos para conservar el historial. WfmMedicalReader conserva acceso exclusivamente a sus vistas laborales. La integración efectiva con asistencias/overtime en WFMCore sigue requiriendo trabajo coordinado en ese producto.
