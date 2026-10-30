# Trabajo en MedicApp

- Instrucción permanente del usuario, 30 de octubre de 2026: al cerrar cambios hacer commit, push y publish de MedicApp, sin volver a pedir confirmación. Verificar cada paso e informar el resultado real.
- Repositorio propio: https://github.com/MWZSystems/MedicApp. Conservar el trabajo remoto y local; no forzar ni sobrescribir historiales ajenos.
- Publicar exclusivamente MedicApp en su sitio site9 con `scripts/Publicar-MedicApp.ps1`; comprobar la raíz, /Account/Login y /health. No usar los sitios de WFMCore o Trainify.
- No guardar conexiones, contraseñas, perfiles privados, claves de sesión, datos ni respaldos en Git o en assets de Releases.
- Mantener entregas inmutables: versión, tag, ZIP, manifiesto y SHA-256. El código inicial tiene su entrega en Releases/v.1.0.0 y tag v1.0.0. Una corrección posterior publicada requiere otra versión.
- Ejecutar compilación y pruebas pertinentes antes de publicar. Documentar con claridad lo que está implementado, verificado y pendiente.
- No modificar ni publicar WFMCore o Trainify como efecto implícito de cambios de MedicApp. El contrato laboral está preparado; su integración efectiva requiere cambios coordinados y probados en WFMCore.
- Publicar no autoriza por sí solo restauraciones, borrados, restablecimientos de cuentas ni migraciones destructivas de la base compartida.
