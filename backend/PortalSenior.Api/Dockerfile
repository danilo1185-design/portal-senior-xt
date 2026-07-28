# Imagem do backend .NET 8 para o Cloud Run.
# Build com Cloud Build (gcloud run deploy --source .), sem precisar de Docker local.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY PortalSenior.Api.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish PortalSenior.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_ENVIRONMENT=Production
# O Cloud Run injeta a porta em $PORT (default 8080). O Kestrel precisa escutar nela.
ENTRYPOINT ["sh", "-c", "dotnet PortalSenior.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
