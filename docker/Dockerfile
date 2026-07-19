# Stage 0: exact CodeGraphContext provider qualification runtime.
# The user-provided local CPython base is immutable and must never be pulled by
# qualification. The hash-locked installation happens only at image build time.
FROM python:3.12-bookworm@sha256:9bed8554e926c07c6f908841d5ee88c33e8df9236b191526bbce81a9062ab43a AS codegraph
WORKDIR /opt/codegraph

COPY docker/codegraph/requirements.lock ./requirements.lock
RUN python -m pip install --no-cache-dir --require-hashes --requirement requirements.lock \
    && python --version \
    && cgc version

# Stage 1: build and publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/dotnet/Vulgata.Web/Vulgata.Web.csproj
RUN dotnet publish src/dotnet/Vulgata.Web/Vulgata.Web.csproj -c Release -o /app/publish --no-restore

# Stage 2: runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install the application runtime dependency. CodeGraphContext is copied from
# the exact, hash-verified build stage above; no runtime resolver is invoked.
RUN apt-get update \
    && apt-get install -y --no-install-recommends git ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=codegraph /usr/local /usr/local
COPY --from=codegraph /opt/codegraph/requirements.lock /opt/codegraph/requirements.lock
RUN mkdir --parents /var/lib/vulgata-codegraph \
    && chown --recursive app:app /var/lib/vulgata-codegraph

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    HOME=/home/app \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONNOUSERSITE=1

USER app
ENTRYPOINT ["dotnet", "Vulgata.Web.dll"]
