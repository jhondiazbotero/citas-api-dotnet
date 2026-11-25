# 0006. JWT sin refresh tokens y usuarios solo del personal

Estado: aceptada.

## Contexto

La API tiene dos tipos de uso: el público consulta horarios libres sin identificarse, y el personal del consultorio (administración, recepción y los profesionales) gestiona agenda y citas. Necesitaba autenticación que se pudiera probar bien y que no dependiera de un servicio externo para correr el proyecto con un solo `docker compose up`.

## Decisión

- Los usuarios son solo del personal, en la tabla `staff_users`, con tres roles: `Admin`, `Reception` y `Professional`. Un usuario con rol profesional queda vinculado a un profesional. Los pacientes no inician sesión.
- `POST /api/v1/auth/token` cambia email y contraseña por un token JWT firmado con HS256. Dura 60 minutos por defecto (configurable, máximo 24 horas). La llave sale de la configuración (`Jwt__SigningKey`), debe tener al menos 32 bytes y la aplicación no arranca sin ella.
- Las contraseñas se guardan con `PasswordHasher` de `Microsoft.Extensions.Identity.Core`, detrás de un puerto propio. No uso ASP.NET Core Identity completo.
- Las políticas se declaran por endpoint y los permisos por recurso (un profesional solo ve su agenda y sus citas) los decide un único manejador de autorización. Si el recurso no existe, la respuesta es la misma que si no fuera suyo, así que no sirve para descubrir ids.
- El login responde igual (401) para correo desconocido, contraseña incorrecta o usuario desactivado, y verifica la contraseña siempre. Está limitado a 5 intentos por minuto por dirección IP, con 429 y `Retry-After`.

## Consecuencias

Estos son los límites, y los dejo escritos porque importan:

- No hay refresh tokens: cuando el token vence, la persona vuelve a iniciar sesión.
- No hay revocación. Un token ya emitido sirve hasta que vence, aunque el usuario se desactive o cambie de rol. Se reduce con una vida corta, pero no desaparece.
- El límite de intentos vive en la memoria de cada instancia. Con varias instancias, cada una cuenta por separado.
- Detrás de un proxy inverso hay que activar `ForwardedHeaders:Enabled` para que el límite vea la dirección real del cliente. Por eso `docker-compose.yml` publica el puerto solo en `127.0.0.1`: si se expusiera directo, cualquiera podría escribir `X-Forwarded-For` y esquivar el límite.
- Rotar la llave invalida todos los tokens vigentes.

## Alternativas descartadas

- Refresh tokens con almacenamiento y rotación: permiten sesiones largas y revocar, pero agregan una tabla, un endpoint y varios casos de borde que este proyecto no necesita.
- Sesiones en el servidor o una lista de tokens revocados: dan revocación inmediata y le quitan a la API la ventaja de no consultar nada por petición.
- Un proveedor de identidad externo (OpenID Connect): es lo que usaría en producción real, pero obliga a depender de un servicio aparte para correr y probar el proyecto.
- ASP.NET Core Identity completo: trae tablas, flujos y pantallas que aquí no se usan.
