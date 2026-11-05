# syntax=docker/dockerfile:1

# Visual Studio fast mode uses this stage and copies a locally built app into it.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM node:24-bookworm AS web
WORKDIR /web
COPY src/PickArr.WebApp/package.json src/PickArr.WebApp/package-lock.json* ./
RUN npm ci
COPY src/PickArr.WebApp/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION=0.0.0
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/PickArr.Api/PickArr.Api.csproj", "src/PickArr.Api/"]
COPY ["src/PickArr.Core/PickArr.Core.csproj", "src/PickArr.Core/"]
COPY ["src/PickArr.Infrastructure/PickArr.Infrastructure.csproj", "src/PickArr.Infrastructure/"]
RUN dotnet restore "./src/PickArr.Api/PickArr.Api.csproj"
COPY . .
WORKDIR "/src/src/PickArr.Api"
RUN dotnet build "./PickArr.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build -p:Version=$VERSION

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG VERSION=0.0.0
RUN dotnet publish "./PickArr.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -p:Version=$VERSION
COPY --from=web /web/dist /app/publish/wwwroot

FROM base AS final
USER root
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/* \
 && mkdir -p /config \
 && chown -R app:app /app /config
ENV ASPNETCORE_URLS=http://+:8080 \
    PickArr__DataPath=/config \
    TZ=UTC
WORKDIR /app
COPY --from=publish /app/publish .
VOLUME /config
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s \
  CMD curl -fsS http://localhost:8080/health || exit 1
USER app
ENTRYPOINT ["dotnet", "PickArr.Api.dll"]
