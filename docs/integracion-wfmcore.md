# Contrato laboral con WFMCore

La base definitiva es la compartida de WFMCore. MedicApp conserva los IDs existentes de personas; no vincula registros por nombre ni número de empleado editable.

## Fuentes de lectura

- `medic.vw_ApprovedLaborRestrictions`: estado vigente autorizado, con PKMedicalLeave, FKPerson, StartsAtUtc, EndsAtUtc, Revision y LastUpdated.
- `medic.vw_LaborEvents`: cambios confirmados de autorización/cancelación, sin diagnósticos, motivos ni cédulas. El cursor usa PKLaborEvent; puede tener huecos. Las transacciones de cambio se serializan con un bloqueo de aplicación hasta commit, para conservar el orden de confirmación de los eventos.
- `/Labor/Events?after=0&limit=100`: alternativa HTTP con permiso propio ReadIntegration. Su sesión es de MedicApp y no acepta la cookie de WFMCore. Para un servicio sin usuario se prefiere SQL con el rol WfmMedicalReader; no se ha creado ni almacenado una credencial de servicio.
- `/Labor/Restrictions?personId=...&from=...Z&to=...Z`: restricciones actuales para un intervalo UTC de hasta 92 días, con el mismo permiso. Respuestas sin caché.

`Integrations/WFMCore/MedicalRestrictionReader.cs` y su record acompañante proporcionan el lector SQL parametrizado y asíncrono. Se compilan y verifican contra SQL real en las pruebas de MedicApp; todavía no están incorporados ni activados en el checkout/publicación de WFMCore.

## Incorporación en WFMCore

1. Añadir los dos archivos al proyecto WFMCore y registrar `AddScoped<MedicalRestrictionReader>()`. Configurar acceso a las dos vistas con su cuenta propia; verificar primero `/health` de ambos productos.
2. En `AttendanceService.SavePunchAsync`, una vez convertidas entrada y salida a UTC y antes de guardar, consultar el intervalo y rechazar una nueva marcación incompatible con incapacidad autorizada. Si no hay salida, evaluar el intervalo provisional de la jornada; no inferir jornada completa desde un instante.
3. En `OvertimeValidationService`, añadir un bloqueo al resultado por persona cuando su intervalo solicitado intersecte una restricción. Revalidar también al aprobar y antes de conciliar; una vista previa anterior no autoriza una operación posterior automáticamente.
4. Al leer la hoja de horas, presentar el tiempo de incapacidad separado del trabajado, limitado por el turno real y cruzando medianoche correctamente. No sumar duración natural como horas pagadas. No modificar marcaciones reales ni cerrar/reabrir períodos automáticamente.
5. Al recibir cambios de `vw_LaborEvents`, guardar la última revisión aplicada por incapacidad en una tabla propia WFM con índice único. Aplicar cambios y avanzar el cursor en la misma transacción del consumidor; reintentos no duplican efectos. Una cancelación retira la restricción, conservando el hecho y el historial.
6. Si se detecta una incapacidad sobre horas ya registradas, solicitudes aprobadas o períodos cerrados, registrar una discrepancia para revisión autorizada. La integración no decide por sí sola cambiar nómina o destruir registros anteriores.
7. Si no se puede consultar la fuente, informar indisponibilidad. Nunca interpretar un error SQL como ausencia de incapacidad. Para impedir carreras entre ambas aplicaciones, las aprobaciones/conciliaciones deben compartir un protocolo de bloqueo y revisión con MedicApp; una consulta aislada previa al guardado no garantiza consistencia entre aplicaciones.

No se modificó ni publicó WFMCore en esta entrega. El efecto automático sobre asistencia/overtime permanece pendiente de incorporar, probar y publicar esos puntos en su proyecto. El lector y las vistas dejan preparado el contrato, pero no se anuncian como integración activa.
