# syntax=docker/dockerfile:1

# License stage
FROM scratch AS license
COPY LICENSE LICENSE
COPY NOTICE NOTICE

# Build stage - publish the ASP.NET Core server as a framework-dependent app.
# Locked-mode restore uses the committed packages.lock.json files.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props MothaSync.slnx ./
COPY src/MothaSync.Shared/MothaSync.Shared.csproj src/MothaSync.Shared/packages.lock.json src/MothaSync.Shared/
COPY src/MothaSync.Server/MothaSync.Server.csproj src/MothaSync.Server/packages.lock.json src/MothaSync.Server/
RUN dotnet restore src/MothaSync.Server --locked-mode

COPY src/MothaSync.Shared/ src/MothaSync.Shared/
COPY src/MothaSync.Server/ src/MothaSync.Server/
RUN dotnet publish src/MothaSync.Server -c Release --no-restore -o /app/publish

# Empty data directory, owned by the app user (uid 1654) of the chiseled
# runtime stage, so the SQLite database is writable in the container.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS data
RUN mkdir -p /data \
    && touch /data/.keep

# Runtime stage - Ubuntu chiseled (distroless): no shell, no package manager,
# non-root user uid 1654, invariant globalization, HTTP on port 8080.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
LABEL org.opencontainers.image.title="MothaSync Server" \
      org.opencontainers.image.description="Optional sync reporting server for MothaSync clients." \
      org.opencontainers.image.licenses='MIT' \
      org.opencontainers.image.vendor="Timo Reymann <mail@timo-reymann.de>" \
      org.opencontainers.image.authors="Timo Reymann <mail@timo-reymann.de>" \
      org.opencontainers.image.url="https://github.com/timo-reymann/MothaSync" \
      org.opencontainers.image.documentation="https://github.com/timo-reymann/MothaSync" \
      org.opencontainers.image.source="https://github.com/timo-reymann/MothaSync.git"

COPY --from=license / /
COPY --from=build /app/publish /app
COPY --chown=1654:1654 --from=data /data /data

WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV Server__DatabasePath=/data/mothasync-server.db
VOLUME /data
EXPOSE 8080 9100
ENTRYPOINT ["dotnet", "MothaSync.Server.dll"]
