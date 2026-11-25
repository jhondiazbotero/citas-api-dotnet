# 0001. Clean Architecture con cuatro proyectos y pruebas de arquitectura

Estado: aceptada.

## Contexto

La API tiene reglas de negocio con peso (cruces de horario, cancelación con 24 horas, permisos por recurso) y una base de datos que también hace cumplir parte de ellas. Quería que las reglas se pudieran probar sin levantar ni un servidor ni una base de datos, y que cambiar de librería de acceso a datos o de framework web no obligara a tocarlas.

## Decisión

La solución tiene cuatro proyectos con la dependencia apuntando siempre hacia adentro:

- `Citas.Domain`: entidades, objetos de valor, reglas y catálogos de errores. No referencia nada del resto.
- `Citas.Application`: un manejador por caso de uso, validación con FluentValidation y los puertos (interfaces de repositorios, reloj, hash de contraseñas, emisor de tokens). Referencia solo el dominio.
- `Citas.Infrastructure`: EF Core con PostgreSQL, repositorios, migraciones, datos de demo, hash de contraseñas. Implementa los puertos.
- `Citas.Api`: Minimal APIs, autenticación, ProblemDetails y la documentación. Es la raíz de composición y por eso es el único que conoce a los tres.

Para que la regla no dependa de la disciplina de cada persona, `tests/Citas.Architecture.Tests` la comprueba con NetArchTest y además revisa las referencias entre ensamblados: el dominio no puede depender de las otras capas, de EF Core ni de Npgsql; la aplicación no puede depender de infraestructura, de la API, de EF Core ni de ASP.NET Core; ninguna de las dos puede traer librerías de tokens. Si alguien agrega una referencia prohibida, la suite falla.

## Consecuencias

- Las pruebas del dominio y de la aplicación corren en milisegundos, sin contenedores. Las de infraestructura y de la API usan PostgreSQL real.
- Hay más archivos y algo de mapeo entre capas (las entidades no salen por HTTP, salen respuestas propias de la capa de aplicación).
- Algunas decisiones de diseño siguen de aquí: `Appointment.Create` es `internal` para que solo el servicio de agendamiento pueda crear citas, y la hora actual entra por un puerto (`IClinicClock`) en lugar de leerse dentro del dominio.
- Para un servicio de este tamaño es más estructura de la que haría falta en producción. La elegí a propósito: es lo que el proyecto quiere enseñar y se puede ver verificada.

## Alternativas descartadas

- Un solo proyecto con carpetas: más rápido de armar, pero la separación quedaría en convención y nada impediría que el dominio usara EF Core.
- Cortes verticales por funcionalidad en un solo proyecto: encajan bien con casos de uso pequeños, pero no dejan probar el dominio aislado de la base de datos con una regla que se pueda verificar.
- Más proyectos (uno por contrato, uno por cada adaptador): no hay un segundo adaptador que lo justifique.
