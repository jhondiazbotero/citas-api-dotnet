using System.Text.Json.Nodes;
using Asp.Versioning.OpenApi;
using Citas.Api.Contracts;
using Citas.Application.Auth;
using Citas.Application.Common;
using Citas.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Citas.Api.OpenApi;

internal static class OpenApiSetup
{
    public const string BearerScheme = "Bearer";

    private const string Description = """
        API to book appointments in a clinic: professionals with weekly working hours, patients, and appointments that never overlap.

        **Dates and times.** Everything is local clinic time. Send `yyyy-MM-ddTHH:mm[:ss]` without zone or offset
        (`2026-12-08T09:30:00`). A value ending in `Z` or `+hh:mm` is rejected with 400.

        **Errors.** Every error is `application/problem+json` (RFC 9457) with `type`, `title`, `status`, `detail` and a stable
        `code` such as `Appointment.Overlap`. Statuses: 400 malformed input, 404 not found, 409 clash with existing data,
        422 a business rule refuses the request, 500 unexpected (no details are exposed).

        **Versions.** The version is part of the URL: `/api/v1`.

        **Sign in.** Staff users (Admin, Reception, Professional) call `POST /api/v1/auth/token` with email and password and get a
        bearer token. Send it as `Authorization: Bearer <token>`; in this page use the Authentication panel. Each operation says
        which role it needs under **Access**. 401 means no valid token, 403 means the role is not allowed, 429 means too many
        sign-in attempts (5 per minute per address). Patients do not sign in.
        """;

    public static void AddCitasTransformers(this VersionedOpenApiOptions options)
    {
        options.Document.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Citas API";
            document.Info.Description = Description;
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                [BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Access token from `POST /api/v1/auth/token`. Paste the token only, without the word Bearer.",
                },
            };
            document.Tags = new HashSet<OpenApiTag>
            {
                new() { Name = "Auth", Description = "Sign in with email and password to get an access token." },
                new() { Name = "Patients", Description = "People who book appointments." },
                new() { Name = "Professionals", Description = "Who sees patients, their weekly hours, agenda and free slots." },
                new() { Name = "Appointments", Description = "Book, move, cancel and close appointments." },
                new() { Name = "Staff users", Description = "Create the people who sign in: reception and professionals." },
            };
            return Task.CompletedTask;
        });

        options.Document.AddOperationTransformer((operation, context, _) =>
        {
            IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;
            bool anonymous = metadata.OfType<IAllowAnonymous>().Any();
            IAuthorizeData? authorize = metadata.OfType<IAuthorizeData>().FirstOrDefault();

            string access = anonymous
                ? "Public: no token needed."
                : $"Requires a bearer token. Role: {Policies.Describe(authorize?.Policy)}";
            operation.Description = $"{operation.Description}\n\n**Access:** {access}";

            if (!anonymous)
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [],
                });
            }

            return Task.CompletedTask;
        });

        options.Document.AddSchemaTransformer((schema, context, _) =>
        {
            Type type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
            if (type == typeof(DateTime))
            {
                DescribeLocalDateTime(schema);
            }
            else if (type.FullName == "Microsoft.AspNetCore.Mvc.ProblemDetails")
            {
                AddCodeProperty(schema);
            }
            else if (Examples.TryGetValue(type, out Func<JsonNode>? example))
            {
                schema.Examples = [example()];
            }

            return Task.CompletedTask;
        });
    }

    private static void DescribeLocalDateTime(IOpenApiSchema schema)
    {
        if (schema is not OpenApiSchema concrete)
        {
            return;
        }

        concrete.Format = null; // "date-time" would invite clients to send a zone.
        concrete.Pattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2})?$";
        concrete.Description = "Local clinic time, yyyy-MM-ddTHH:mm[:ss], without zone or offset. Send local clinic time without offset: values ending in Z or +hh:mm are rejected with 400.";
        concrete.Examples = [JsonValue.Create("2026-12-08T09:30:00")];
    }

    private static void AddCodeProperty(IOpenApiSchema schema)
    {
        if (schema is OpenApiSchema { Properties: not null } concrete)
        {
            concrete.Properties["code"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "Stable machine-readable error code, for example Appointment.Overlap or Request.InvalidDateTime. Safe to branch on; `detail` is for people.",
                Examples = [JsonValue.Create("Appointment.Overlap")],
            };
        }
    }

    private static readonly Dictionary<Type, Func<JsonNode>> Examples = new()
    {
        [typeof(RegisterPatientRequest)] = () => new JsonObject
        {
            ["fullName"] = "Lucía Ortega Pardo",
            ["documentNumber"] = "90000001",
            ["phone"] = "3000000101",
        },
        [typeof(WorkingSlotDto)] = () => new JsonObject
        {
            ["day"] = "Monday",
            ["from"] = "08:00:00",
            ["to"] = "12:00:00",
        },
        [typeof(ScheduleAppointmentRequest)] = () => new JsonObject
        {
            ["professionalId"] = "3f2b8c1e-6f0a-4c52-9a3e-0b7d1c2e4a10",
            ["patientId"] = "9d1e5a77-2b34-4f8e-8c61-5a0f3e7b2d42",
            ["start"] = "2026-12-08T09:30:00",
            ["durationMinutes"] = 30,
            ["reason"] = "Control general",
        },
        [typeof(RescheduleAppointmentRequest)] = () => new JsonObject
        {
            ["newStart"] = "2026-12-09T10:00:00",
            ["durationMinutes"] = 30,
        },
        [typeof(CancelAppointmentRequest)] = () => new JsonObject
        {
            ["reason"] = "El paciente no puede asistir",
            ["onBehalfOfPatient"] = true,
        },
        [typeof(LoginRequest)] = () => new JsonObject
        {
            ["email"] = "recepcion@demo.local",
            ["password"] = "tu-contraseña",
        },
        [typeof(CreateStaffUserRequest)] = () => new JsonObject
        {
            ["email"] = "recepcion.tarde@clinica.example",
            ["password"] = "una-contraseña-larga",
            ["role"] = "Reception",
        },
        [typeof(LoginResponse)] = () => new JsonObject
        {
            ["accessToken"] = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
            ["expiresAt"] = "2026-12-08T10:30:00+00:00",
            ["role"] = "Reception",
        },
    };
}
