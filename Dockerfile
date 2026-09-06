# MIMEDU.AZ backend - production image.
# Render (və digər Docker-based hostinq) üçün: "Environment" = Docker seçilməlidir,
# Build/Start Command sahələri boş qalır - Render bu faylı avtomatik oxuyur.

# ---- Build & publish mərhələsi ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Əvvəlcə yalnız .csproj fayllarını kopyalayırıq ki, Docker layer cache
# yalnız asılılıqlar dəyişəndə yenidən restore etsin (kod dəyişikliyi cache-i pozmasın).
COPY MimeduAz.sln ./
COPY src/MimeduAz.Domain/MimeduAz.Domain.csproj src/MimeduAz.Domain/
COPY src/MimeduAz.Contracts/MimeduAz.Contracts.csproj src/MimeduAz.Contracts/
COPY src/MimeduAz.Application/MimeduAz.Application.csproj src/MimeduAz.Application/
COPY src/MimeduAz.Infrastructure/MimeduAz.Infrastructure.csproj src/MimeduAz.Infrastructure/
COPY src/MimeduAz.Api/MimeduAz.Api.csproj src/MimeduAz.Api/

RUN dotnet restore src/MimeduAz.Api/MimeduAz.Api.csproj

COPY src/ src/
RUN dotnet publish src/MimeduAz.Api/MimeduAz.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---- Runtime mərhələsi ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
# Render PORT dəyişənini avtomatik verir; lokal `docker run` üçün default 8080.
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "dotnet MimeduAz.Api.dll --urls http://0.0.0.0:${PORT}"]
