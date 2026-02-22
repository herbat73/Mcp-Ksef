FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

ARG PAT_TOKEN
# Add Cirf NuGet server here
RUN dotnet nuget add source "https://nuget.pkg.github.com/CIRFMF/index.json" --name github-cirf --username token --password ${PAT_TOKEN} --store-password-in-clear-text

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

ARG APP_VERSION

RUN echo "Setting image version: $APP_VERSION"

LABEL version="${APP_VERSION}"
LABEL release="mcp-ksef"
LABEL org.opencontainers.image.version="${APP_VERSION}"
LABEL org.opencontainers.image.title="mcp-ksef"
LABEL org.opencontainers.image.description="Serwer MCP dla KSeF"
LABEL org.opencontainers.image.url="https://github.com/herbat73/Mcp-Ksef"
LABEL org.opencontainers.image.source="https://github.com/herbat73/Mcp-Ksef"
LABEL org.opencontainers.image.licenses="MIT"

WORKDIR /app

COPY --from=build /app .

USER $APP_UID

ENTRYPOINT ["dotnet", "Mcp-Ksef.HybridApp.dll"]
