ARG VULGATA_SOURCE_COMMIT
ARG VULGATA_SOURCE_TREE
ARG VULGATA_PRODUCTION_SOURCE_MANIFEST_SHA256

# Stage 1: build and publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/dotnet/Vulgata.Web/Vulgata.Web.csproj
RUN dotnet publish src/dotnet/Vulgata.Web/Vulgata.Web.csproj -c Release -o /app/publish --no-restore

# Stage 2: runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG VULGATA_SOURCE_COMMIT
ARG VULGATA_SOURCE_TREE
ARG VULGATA_PRODUCTION_SOURCE_MANIFEST_SHA256
WORKDIR /app

LABEL org.vulgata.source-commit=${VULGATA_SOURCE_COMMIT} \
    org.vulgata.source-tree=${VULGATA_SOURCE_TREE} \
    org.vulgata.production-source-manifest-sha256=${VULGATA_PRODUCTION_SOURCE_MANIFEST_SHA256}

# Install application runtime prerequisites for repository operations and
# health checks.
# Use the user-approved Tsinghua mirror for the ordinary Noble archives while
# retaining Ubuntu's official security archive.
RUN printf '%s\n' \
    'Types: deb' \
    'URIs: https://mirrors4.tuna.tsinghua.edu.cn/ubuntu/' \
    'Suites: noble noble-updates noble-backports' \
    'Components: main restricted universe multiverse' \
    'Signed-By: /usr/share/keyrings/ubuntu-archive-keyring.gpg' \
    '' \
    'Types: deb' \
    'URIs: http://security.ubuntu.com/ubuntu/' \
    'Suites: noble-security' \
    'Components: main restricted universe multiverse' \
    'Signed-By: /usr/share/keyrings/ubuntu-archive-keyring.gpg' \
    > /etc/apt/sources.list.d/ubuntu.sources
RUN apt-get -o Acquire::Retries=5 -o Acquire::http::Timeout=30 -o Acquire::https::Timeout=30 update \
    && apt-get -o Acquire::Retries=5 -o Acquire::http::Timeout=30 -o Acquire::https::Timeout=30 install -y --no-install-recommends git ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir --parents /var/lib/vulgata-codegraph \
    && chown --recursive app:app /var/lib/vulgata-codegraph

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    HOME=/home/app

USER app
ENTRYPOINT ["dotnet", "Vulgata.Web.dll"]
