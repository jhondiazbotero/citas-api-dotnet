# syntax=docker/dockerfile:1

# ---- Restore: only the files that decide the packages, so this layer is cached until a package changes ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /build
COPY global.json Directory.*.props CitasApi.slnx ./
COPY src/Citas.Domain/Citas.Domain.csproj src/Citas.Domain/
COPY src/Citas.Application/Citas.Application.csproj src/Citas.Application/
COPY src/Citas.Infrastructure/Citas.Infrastructure.csproj src/Citas.Infrastructure/
COPY src/Citas.Api/Citas.Api.csproj src/Citas.Api/
# The API project pulls in the other three; the test projects are not part of the image.
RUN dotnet restore src/Citas.Api/Citas.Api.csproj

# ---- Build and publish (Release; warnings are errors, as in CI) ----
FROM restore AS publish
COPY .editorconfig ./
COPY src/ src/
RUN dotnet build src/Citas.Api/Citas.Api.csproj -c Release --no-restore \
 && dotnet publish src/Citas.Api/Citas.Api.csproj -c Release --no-build -o /app/publish

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish ./

# Production by default. Secrets (ConnectionStrings__Citas, Jwt__SigningKey, Demo__UsersPassword) come from the environment at run time.
# ForwardedHeaders is on because the container is meant to sit behind a reverse proxy (see the README before exposing it directly).
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ForwardedHeaders__Enabled=true
EXPOSE 8080

# Not root: the base image ships a non-root user, $APP_UID.
USER $APP_UID
ENTRYPOINT ["dotnet", "Citas.Api.dll"]
