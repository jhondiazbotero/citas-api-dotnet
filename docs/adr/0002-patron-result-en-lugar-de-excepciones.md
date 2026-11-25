# 0002. Patrón Result para reglas de negocio en lugar de excepciones

Estado: aceptada.

## Contexto

Que una cita se cruce con otra, que se cancele tarde o que el profesional no atienda a esa hora no son fallas del programa: son respuestas esperadas del negocio. Tratarlas como excepciones las mezcla con los errores reales (base de datos caída, bug) y las hace difíciles de seguir en la firma de un método.

## Decisión

Toda operación del dominio y de la aplicación que puede fallar por una regla devuelve `Result` o `Result<T>` (`src/Citas.Domain/Common/Result.cs`). El fallo lleva un `Error` con un código estable y un mensaje corto (`Appointment.Overlap`, `Appointment.CancellationTooLate`). Hay conversiones implícitas desde el valor y desde el error, así que los métodos devuelven `return AppointmentErrors.Overlap;` sin ceremonia.

Los errores viven en catálogos por entidad (`src/Citas.Domain/Errors/`). La API no decide qué estado HTTP corresponde mientras escribe el caso de uso: un solo lugar, `ErrorStatus.For`, traduce el código a 400, 404, 409, 422, 401, 403 o 429, y `ProblemResult` lo escribe como ProblemDetails con el campo `code`.

Las excepciones se reservan para lo que no debería pasar: pedir `Value` de un resultado fallido, o crear un `Result` incoherente, lanza `InvalidOperationException`. Los cuerpos mal formados y los errores no previstos los atrapan manejadores de la API y salen también como ProblemDetails.

## Consecuencias

- La firma dice qué puede fallar, y cada regla tiene una prueba que compara el error exacto que devuelve.
- El cliente recibe un código que puede usar en su lógica, sin leer el mensaje.
- Hay que revisar `IsFailure` en cada llamada. El compilador no obliga a hacerlo, así que lo cubren las pruebas de cada manejador.
- El código es algo más largo que con `throw`, sobre todo en los casos de uso con varias reglas seguidas.

## Alternativas descartadas

- Excepciones de dominio con un filtro global que las convierta en HTTP: menos líneas, pero el flujo de negocio queda escondido y lanzar excepciones por casos normales cuesta más que devolver un valor.
- Una librería de resultados (ErrorOr, FluentResults, OneOf): el tipo que necesito cabe en unas cincuenta líneas y no quería una dependencia en el dominio.
