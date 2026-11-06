# syntax=docker/dockerfile:1

# Visual Studio fast mode uses this stage and copies a locally built app into it.
# It never runs the final stage. The aspnet image defaults to the non-root app user,
# and a named volume mounted on a path missing from the image is created as root
# (mode 755), so the app cannot create /config/logs. Seed /config here; a new volume
# copies this ownership. An existing root-owned volume is not reseeded.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN mkdir -p /config \
 && chown -R app:app /config
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# JavaScript only, so the slim image is enough. npm ci is cached until the lockfile changes,
# and this stage does not depend on the .NET restore.
FROM --platform=$BUILDPLATFORM node:24-bookworm-slim AS web
WORKDIR /web
COPY src/PickArr.WebApp/package.json src/PickArr.WebApp/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY src/PickArr.WebApp/ ./
RUN npm run build

# Restore before the sources so a code change does not download packages again.
# Only the projects that publish are copied: the web app and the rest of the repo
# do not invalidate this stage. Publish runs on the builder CPU; -r picks the
# target's native assets (ONNX, SQLite) without embedding a second runtime.
# The full aspnet image stays the runtime base because ONNX needs libgomp.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/PickArr.Api/PickArr.Api.csproj src/PickArr.Api/
COPY src/PickArr.Core/PickArr.Core.csproj src/PickArr.Core/
COPY src/PickArr.Infrastructure/PickArr.Infrastructure.csproj src/PickArr.Infrastructure/
RUN arch="$([ "$TARGETARCH" = "arm64" ] && echo arm64 || echo x64)" \
 && dotnet restore "src/PickArr.Api/PickArr.Api.csproj" -r "linux-$arch"
COPY src/PickArr.Api/ src/PickArr.Api/
COPY src/PickArr.Core/ src/PickArr.Core/
COPY src/PickArr.Infrastructure/ src/PickArr.Infrastructure/
ARG VERSION=0.0.0
RUN arch="$([ "$TARGETARCH" = "arm64" ] && echo arm64 || echo x64)" \
 && dotnet publish "src/PickArr.Api/PickArr.Api.csproj" \
      -c "$BUILD_CONFIGURATION" \
      -r "linux-$arch" \
      --no-self-contained \
      --no-restore \
      -o /app/publish \
      -p:UseAppHost=false \
      -p:DebugType=none \
      -p:DebugSymbols=false \
      -p:Version="$VERSION"

# Static binary. The runtime image does not gain a package manager or curl.
FROM busybox:1.37.0-musl AS wget

FROM base AS final
USER root
COPY --from=wget --chmod=555 /bin/busybox /usr/local/bin/wget
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080 \
    PickArr__DataPath=/config \
    TZ=UTC
WORKDIR /app
# App bits and the built web app are separate layers: a frontend-only change
# reuses the publish stage, and a backend-only change reuses npm ci.
COPY --from=build --chown=app:app /app/publish .
COPY --from=web --chown=app:app /web/dist ./wwwroot
VOLUME /config
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s \
  CMD ["/usr/local/bin/wget", "-q", "-O", "-", "http://127.0.0.1:8080/health"]
ENTRYPOINT ["dotnet", "PickArr.Api.dll"]
