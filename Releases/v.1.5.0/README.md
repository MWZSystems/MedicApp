# MedicApp 1.5.0

Calendario de incapacidades por persona, con búsqueda por nombre completo o número de control. Barras de varios días, vistas de mes, semana y agenda, y detalle con horas exactas. Consulta únicamente periodos disponibles y autorizados. Los empleados acceden a sus propios periodos; doctores y administradores médicos pueden buscar personas, incluyendo antecedentes de personas inactivas.

- 87 pruebas aprobadas; compilación Release sin advertencias y formato verificado.
- Migración aditiva LeaveDocumentEvidence: medic.SY_LeaveDocuments. No genera incapacidades, archivos o eventos laborales de ejemplo en producción.
- Revisar docs/evidencias.md. Aplicar Actualizar-Esquema.ps1 antes de publicar y conservar conjuntamente App_Data/Evidence, App_Data/Keys y SQL. Publicar sin InitializeCalendar para conservar las reglas actuales de citas.
- La integración de asistencias y overtime en WFMCore continúa pendiente en ese producto.
- El ZIP de aplicación está en los assets de v1.5.0; excluye datos, secretos, perfiles, claves y configuración particular del servidor.

Entrega inmutable. Verificar SHA256SUMS.txt antes de desplegar. SourceCommit identifica el código; la etiqueta incluye los metadatos de entrega.
