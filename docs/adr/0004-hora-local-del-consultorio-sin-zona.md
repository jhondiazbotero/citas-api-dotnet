# 0004. Hora local del consultorio sin zona en la API

Estado: aceptada.

## Contexto

Una cita es un evento del consultorio: «el martes a las 9:00», en la hora de pared de ese lugar. Los pacientes y la recepción piensan en esa hora, no en un instante UTC. Guardar y transportar instantes con zona obliga a convertir en cada borde, y un error de conversión mueve la cita una hora o cinco sin avisar.

## Decisión

Todo el sistema habla la hora local del consultorio, sin zona ni desfase:

- La API lee y escribe `yyyy-MM-ddTHH:mm[:ss]`. `ClinicDateTimeConverter` rechaza con 400 (`Request.InvalidDateTime`) cualquier valor que termine en `Z` o en un desfase como `-05:00`, en lugar de convertirlo en silencio.
- En el dominio son `DateTime` con `Kind = Unspecified`. En PostgreSQL son columnas `timestamp without time zone`.
- La zona del consultorio es un dato de configuración (`Clinic:TimeZone`, por defecto `America/Bogota`). Solo `ClinicClock` la usa: toma el instante UTC de `TimeProvider` y lo convierte a la hora de pared para obtener «ahora».
- Las reglas de tiempo (una hora de anticipación, 24 horas para cancelar) reciben «ahora» de quien llama, así las pruebas fijan el reloj (en las de infraestructura con `FakeTimeProvider`).

## Consecuencias

- Una cita se ve igual en la base, en el JSON y en las pruebas.
- Funciona para un consultorio en una zona. Si hubiera sedes en zonas distintas, la zona tendría que ser un atributo de cada sede.
- Los cambios de hora de verano no se manejan: una hora que no existe o que se repite sería ambigua. Colombia no los tiene, y lo dejo como límite conocido.
- Los clientes que trabajan en UTC tienen que convertir por su cuenta antes de enviar.

## Alternativas descartadas

- `DateTimeOffset` o UTC en toda la API: es lo correcto para instantes, pero aquí el dato es una hora de pared, y la conversión repetida añade errores sin dar nada.
- Aceptar el desfase y convertirlo a la hora local: más cómodo para el cliente, pero esconde errores de zona en vez de mostrarlos.
- Guardar `timestamptz` y convertir al leer: obliga a cada consulta de agenda por día a pasar por la zona.
