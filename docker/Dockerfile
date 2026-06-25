# Stage 1: build and publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/dotnet/Vulgata.Web/Vulgata.Web.csproj
RUN dotnet publish src/dotnet/Vulgata.Web/Vulgata.Web.csproj -c Release -o /app/publish --no-restore

# Stage 2: runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install git and curl (curl is needed for future CodeGraph CLI bootstrap updates)
RUN apt-get update \
    && apt-get install -y --no-install-recommends git curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Provide a deterministic codegraph command path for now.
# Replace this shim with an official installer in a future story when distribution URL is finalized.
RUN printf '#!/bin/sh\n' > /usr/local/bin/codegraph \
    && printf 'echo "CodeGraph CLI shim installed. Replace with official binary before production use."\n' >> /usr/local/bin/codegraph \
    && chmod +x /usr/local/bin/codegraph

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

USER app
ENTRYPOINT ["dotnet", "Vulgata.Web.dll"]
