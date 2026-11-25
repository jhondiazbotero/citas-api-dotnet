# 0005. Minimal APIs con handlers propios, sin MediatR

Estado: aceptada.

## Contexto

Cada caso de uso (agendar, reprogramar, cancelar, consultar horarios libres) recibe un comando o una consulta y devuelve un `Result`. Necesitaba un punto de entrada claro por caso de uso y endpoints delgados, sin que el framework web se meta en la capa de aplicación.

## Decisión

Cada caso de uso es una clase con un método `HandleAsync` y sus dependencias por constructor (`ScheduleAppointmentHandler`, `CancelAppointmentHandler`, y así). Se registran en el contenedor y los endpoints, escritos como Minimal APIs, reciben el handler por inyección, arman el comando y convierten el `Result` en respuesta HTTP con `ToOk`, `ToCreated` y `ToNoContent`.

No uso MediatR ni un bus en proceso. Con un handler por caso de uso, el mediador solo agregaría un salto entre el endpoint y el handler, y con una interfaz genérica en medio «ir a la definición» ya no lleva al código que se ejecuta. La validación con FluentValidation se invoca dentro de cada handler, a la vista. El versionado (`/api/v1`) y las políticas de autorización se declaran en el endpoint, y una prueba falla si algún endpoint bajo `/api` no dice quién puede llamarlo.

## Consecuencias

- El camino de una petición se sigue de punta a punta con navegación normal del IDE.
- Una dependencia menos.
- Las preocupaciones transversales que MediatR resuelve con comportamientos (registro, validación, transacciones) aquí se escriben a mano en cada handler o se resuelven en el pipeline HTTP. Con unos quince handlers es manejable, y con muchos más habría que volver a pensarlo.
- Los controladores clásicos de MVC quedan fuera, así que no hay atributos de filtro ni binding de modelos de MVC.

## Alternativas descartadas

- MediatR: útil cuando hay muchos comportamientos transversales o notificaciones de dominio. Aquí no hay ninguno de los dos.
- Controladores MVC: más ceremonia por endpoint y más superficie del framework para lo que son rutas simples.
- FastEndpoints u otro marco de endpoints: agrega una dependencia y una forma de trabajar que no hace falta para este tamaño.
