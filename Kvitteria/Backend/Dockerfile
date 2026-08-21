# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

# Stamped into AssemblyInformationalVersion and served by GET /version. The build context carries no
# usable .git, so SourceLink cannot infer either value — CI passes them explicitly.
ARG VERSION=0.0.0
ARG SOURCE_COMMIT=unknown

WORKDIR /src
# Both props files must land before restore. Directory.Packages.props centralizes every package
# version, so restore fails with NU1015 without it. Directory.Build.props carries VersionPrefix and
# LangVersion, and its absence fails *silently*: the build succeeds against SDK defaults and every
# image reports version 0.0.0 forever.
COPY ["Directory.Packages.props", "Directory.Build.props", "global.json", "./"]
COPY ["Backend/Backend.csproj", "Backend/"]
RUN dotnet restore "Backend/Backend.csproj"

COPY . .
WORKDIR "/src/Backend"
RUN dotnet publish "./Backend.csproj" \
	-c $BUILD_CONFIGURATION \
	-o /app/publish \
	--no-restore \
	/p:UseAppHost=false \
	/p:Version=$VERSION \
	/p:SourceRevisionId=$SOURCE_COMMIT

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
# tzdata: Danish purchase dates are resolved against Europe/Copenhagen, including the DST gap.
# curl: the HEALTHCHECK below, and compose's own healthcheck for this service.
RUN apt-get update \
	&& apt-get install -y --no-install-recommends tzdata curl \
	&& rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# Liveness, not readiness: this answers whether the process is up, and deliberately touches no
# dependency. Compose probes /health/ready instead, because that is what dependents wait on.
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
	CMD curl -fsS http://127.0.0.1:8080/health/live || exit 1

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Backend.dll"]
