# syntax=docker/dockerfile:1

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

# Add NuGet server here
RUN dotnet nuget add source "https://nuget.pkg.github.com/CIRFMF/index.json" --name github-cirf --username token --password TUTAJ_PAT_TOKEN --store-password-in-clear-text

COPY ./Shared /source/Shared
COPY ./Mcp-Ksef.HybridApp /source/Mcp-Ksef.HybridApp

WORKDIR /source/Mcp-Ksef.HybridApp

ARG TARGETARCH
RUN case "$TARGETARCH" in \
      "amd64") RID="linux-musl-x64" ;; \
      "arm64") RID="linux-musl-arm64" ;; \
      *) RID="linux-musl-x64" ;; \
    esac && \
    dotnet publish -c Release -o /app -r $RID --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final

WORKDIR /app

COPY --from=build /app .

USER $APP_UID

ENTRYPOINT ["dotnet", "Mcp-Ksef.HybridApp.dll"]
