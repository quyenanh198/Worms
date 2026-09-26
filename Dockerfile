# Game server + Unity Web build in one image (see docs/PLAN.md §3.13.2).
# The server is published framework-dependent, so the same build runs on
# linux/arm64 (Mac mini, OrbStack) and linux/amd64 without cross-compiling.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY shared/com.worms.sim/ shared/com.worms.sim/
COPY shared/com.worms.protocol/ shared/com.worms.protocol/
COPY shared/Sim/ shared/Sim/
COPY shared/Protocol/ shared/Protocol/
COPY server/Server/ server/Server/
RUN dotnet publish server/Server/Server.csproj -c Release -o /app -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY --from=build /app ./
# CI passes WEB_DIR=web-dist (the Unity Web build); locally the placeholder page is used.
ARG WEB_DIR=server/Server/wwwroot
COPY ${WEB_DIR}/ ./wwwroot/
ENV PORT=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Worms.Server.dll"]
