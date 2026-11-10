# Trabajo en MedicApp

- Instrucción permanente del usuario, 30 de octubre de 2026: al cerrar cambios hacer commit, push y publish de MedicApp, sin volver a pedir confirmación. Verificar cada paso e informar el resultado real.
- Repositorio propio: https://github.com/MWZSystems/MedicApp. Conservar el trabajo remoto y local; no forzar ni sobrescribir historiales ajenos.
- Publicar exclusivamente MedicApp en su sitio site9 con `scripts/Publicar-MedicApp.ps1`; comprobar la raíz, /Account/Login y /health. No usar los sitios de WFMCore o Trainify.
- No guardar conexiones, contraseñas, perfiles privados, claves de sesión, datos ni respaldos en Git o en assets de Releases.
- Instrucción permanente del usuario, 1 de noviembre de 2026: MedicApp sigue en desarrollo y permanece en v1.0.0. Mantener Version en 1.0.0 y acumular los cambios de desarrollo sin incrementar versiones ni crear tags, ramas o Releases por cada cambio. Continuar commit, push y publish. Solo cambiar la versión o crear otra entrega cuando el usuario lo solicite. Los commits identifican cada actualización; las entregas ya publicadas quedan como historial, sin reescribir tags ni borrar assets.
- Ejecutar compilación y pruebas pertinentes antes de publicar. Documentar con claridad lo que está implementado, verificado y pendiente.
- No modificar ni publicar WFMCore o Trainify como efecto implícito de cambios de MedicApp. El contrato laboral está preparado; su integración efectiva requiere cambios coordinados y probados en WFMCore.
- Publicar no autoriza por sí solo restauraciones, borrados, restablecimientos de cuentas ni migraciones destructivas de la base compartida.

- Conexión del sitio, 10 de noviembre de 2026: por instrucción del usuario, Claude separó la conexión del sitio (`ConnectionStrings:WorkforceSite`) de la de despliegue, con Codex detenido. Cuando exista la conexión del sitio, el sitio nunca debe recibir la de despliegue; migraciones, catálogo y datos DEMO siguen con la de despliegue desde este equipo. El usuario SQL de cada sitio se crea en el panel de SmarterASP y recibe sus roles con `database/seguridad/RolesPorAplicacion.sql` de Trainify (`USUARIOS_SQL_POR_APLICACION.md`).

## Rama de desarrollo — Releases/v1.0.0
Instrucción del usuario, 2026-11-10: `main` es la rama principal. Todas las aplicaciones siguen en desarrollo en la versión 1.0.0 y todavía no se libera ninguna, así que:
- Cada cambio se hace primero en `Releases/v1.0.0`: commit y push.
- Después se lleva el mismo commit a `main` y se hace push. Solo avance directo (`git push origin Releases/v1.0.0:main`), sin forzar; si no es avance directo, detenerse y avisar.
- Cuando el usuario indique que la versión salió a producción, las versiones nuevas tendrán su propia rama `Releases/vX.Y.Z`.
- No crear ramas ni tags de otras versiones ni borrar los existentes sin que el usuario lo pida.
