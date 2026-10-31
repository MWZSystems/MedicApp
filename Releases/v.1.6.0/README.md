# MedicApp 1.6.0

Expediente clínico por nombre/control con consultas, notas, recetas, nueve signos vitales opcionales y archivos PDF/JPG/PNG generales o por consulta. Inventario médico por lote/caducidad, entradas/salidas/bajas, alertas de mínimos y caducidad e historial de saldos. Permisos independientes para enfermería desde Accesos. Los registros clínicos son opcionales y no descuentan inventario automáticamente.

- 104 pruebas aprobadas; compilación Release sin advertencias y formato verificado.
- Requiere migración aditiva ClinicalRecordsAndInventory: cuatro tablas y dos permisos médicos. Aplicar explícitamente mediante scripts/Actualizar-Esquema.ps1; el inicio web no migra ni genera datos.
- Revisar docs/expediente-inventario.md. Publicar con el script oficial sin InitializeCalendar para conservar las reglas actuales.
- Respaldo conjunto de SQL, App_Data/Evidence y App_Data/Keys. El publish conserva App_Data.
- El ZIP excluye documentos, datos, claves, conexiones y perfiles privados. La integración efectiva con asistencias/overtime de WFMCore sigue pendiente en ese producto.

Entrega inmutable. Verificar SHA256SUMS.txt antes de desplegar. SourceCommit identifica el código; la etiqueta incluye los metadatos de entrega.
