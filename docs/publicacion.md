# Publicación de MedicApp 1.0.0

30 de octubre de 2026. Destino propio: https://mwzsystems-001-site9.etempurl.com/.

Se publicó mediante el perfil Web Deploy de site9, con IIS fuera de proceso. La configuración privada de producción incluye ConnectionStrings:Workforce y el AllowedHosts del sitio; se generó fuera de Git y no forma parte del ZIP de release. La configuración temporal local se retiró al finalizar.

Verificado después de la publicación: la raíz redirige al inicio de sesión, /Account/Login responde 200 y /health responde 200 Healthy. La página predeterminada del proveedor ya no aparece. Las 35 pruebas de aplicación pasaron antes de publicar.

El repositorio conserva el commit inicial remoto, su README original como descripción del producto, la licencia y las exclusiones anteriores. El ZIP de entrega no incluye perfiles, conexiones, claves de sesión, datos ni respaldos.

La integración automática de incapacidades con los cálculos de WFMCore continúa pendiente en ese producto; esta publicación no modifica WFMCore ni Trainify ni ejecuta migraciones SQL.
