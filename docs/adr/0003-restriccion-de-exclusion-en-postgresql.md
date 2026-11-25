# 0003. Restricción de exclusión en PostgreSQL contra reservas simultáneas

Estado: aceptada.

## Contexto

El dominio ya rechaza una cita que se cruza con otra (`AppointmentScheduler`). Esa validación lee las citas existentes y decide en memoria, así que no alcanza cuando dos solicitudes llegan a la vez: las dos leen que la hora está libre, las dos pasan la regla y las dos guardan.

## Decisión

Mantengo la validación del dominio, que da el error claro en el caso normal, y agrego la garantía en la base de datos. La migración inicial crea dos restricciones de exclusión de PostgreSQL con un índice GiST:

```sql
ALTER TABLE appointments
    ADD CONSTRAINT ex_appointments_professional_overlap
    EXCLUDE USING gist (professional_id WITH =, tsrange(start_at, end_at, '[)') WITH &&)
    WHERE (status = 'Scheduled');
```

La otra, `ex_appointments_patient_overlap`, hace lo mismo por paciente. Usan la extensión `btree_gist` para comparar la igualdad del identificador junto con el rango. El rango es semiabierto (`[)`), por eso una cita que termina a las 10:00 y otra que empieza a las 10:00 no chocan, igual que en `TimeRange.Overlaps`. El `WHERE` deja fuera las canceladas, completadas y no asistidas: solo las agendadas bloquean la hora.

`UnitOfWork` atrapa el error de exclusión de PostgreSQL (SQLSTATE 23P01) y mira el nombre de la restricción y lo devuelve como `Appointment.Overlap` o `Appointment.PatientOverlap`, que la API responde con 409. Una violación de concurrencia sobre la misma fila (columna `xmin` como token de concurrencia) sale como `Appointment.ConcurrencyConflict`.

La prueba `Save_TwoRequestsAtTheSameTime_OnlyOneWinsAndTheOtherGetsOverlap` guarda dos citas idénticas desde dos contextos en paralelo contra PostgreSQL real y exige que una gane y la otra reciba el cruce. `Database_RejectsOverlapEvenWithoutTheUnitOfWork` comprueba que la base rechaza el cruce aunque el código no lo traduzca.

## Consecuencias

- La garantía no depende de que el código de aplicación se acuerde de bloquear. Cualquier camino que escriba en la tabla queda cubierto.
- La regla vive en dos sitios (dominio y base de datos). Los dos deben coincidir en los bordes, y hay pruebas de los bordes en ambos.
- EF Core no sabe expresar estas restricciones, así que van como SQL dentro de la migración, y el modelo no las describe.
- El proyecto queda atado a PostgreSQL y a la extensión `btree_gist`. Las pruebas de infraestructura necesitan Docker.

## Alternativas descartadas

- Transacción serializable con reintentos: funciona, pero deja la corrección en manos de cada caso de uso y obliga a reintentar.
- Bloquear la fila del profesional (`SELECT ... FOR UPDATE`) antes de validar: serializa todas las reservas de un profesional, también las que no compiten.
- Un índice único sobre bloques fijos de tiempo: obligaría a que todas las citas duren lo mismo y a partir el rango en celdas.
- Un candado en memoria dentro de la API: no sirve en cuanto hay más de una instancia.
