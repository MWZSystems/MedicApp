# Guía de uso descargable

La presentación de la versión 1.7.0 contiene 31 diapositivas editables, con 29 capturas de las pantallas de MedicApp. Los datos, cuentas, documentos y registros que se ven en ellas son ficticios y pertenecen a una base LocalDB aislada. No se utilizaron cuentas ni expedientes de producción.

## Acceso

Entrar a MedicApp y abrir **Ayuda y manual**, disponible en el menú y también en Inicio. **Descargar presentación (.pptx)** entrega el archivo para abrirlo con PowerPoint o un visor compatible. Todos los perfiles autenticados tienen acceso a la guía. El documento explica permisos y no concede acceso adicional a ningún módulo.

## Contenido

Acceso y visibilidad de contraseña; Inicio; calendario, reserva, detalle, cancelación y lista de citas; registro y calendario de incapacidades; evidencias y expediente documental; atención y agenda del doctor; captura y autorización de incapacidades; carga de documentos IMSS; expediente clínico, signos vitales, notas, recetas y archivos; inventario por lote, altas y movimientos; doctores; permisos de enfermería y administración; configuración, datos DEMO, auditoría, contraseña y descarga de la guía.

La presentación distingue las acciones del empleado, del doctor responsable y de la administración. Los datos clínicos y archivos son opcionales. Explica que una receta no descuenta inventario y que la integración laboral en WFMCore requiere su consumidor correspondiente.

## Publicación y mantenimiento

`MedicApp/Guides/MedicApp-Guia-de-Uso.pptx` es contenido versionado, copiado al output y al publish mediante el proyecto. Se conserva fuera de wwwroot y el controlador usa un nombre fijo, sin aceptar rutas. La respuesta usa el tipo MIME de PowerPoint, Content-Disposition attachment y las cabeceras existentes de no-store y nosniff.

Al cambiar la interfaz, actualizar las diapositivas afectadas con capturas de una base de pruebas y revisar todas las páginas de la presentación antes de reemplazar el archivo. Nunca incluir cuentas, contraseñas o expedientes reales. Mantener actualizada la versión que se indica en Ayuda. El archivo no requiere migraciones ni inicialización de datos para su descarga.

Verificación: prueba HTTP de redirección sin sesión, acceso con empleado, adjunto y tipo MIME correctos, archivo PPTX válido con 31 diapositivas y al menos 29 imágenes, y ausencia de exposición directa en /Guides. La presentación incluye validación de integridad, geometría y fuentes y revisión visual de sus diapositivas.
