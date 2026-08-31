# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

# Stamped into AssemblyInformationalVersion and served by GET /version.
#
# Both default to empty rather than to a literal, because a default here would *override* the
# VersionPrefix release-please maintains in Backend.csproj — a plain `docker build` would then report
# 0.0.0 on a released commit. Empty means the project file is the fallback and stays the single source
# of truth. CI passes VERSION anyway, for the same number by a shorter path.
#
# SOURCE_COMMIT has no fallback to inherit: the build context carries no usable .git, so SourceLink
# cannot infer it and only CI can supply it.
ARG VERSION=
ARG SOURCE_COMMIT=

WORKDIR /src
# Both props files must land before restore. Directory.Packages.props centralizes every package
# version, so restore fails with NU1015 without it. Directory.Build.props carries VersionPrefix and
# LangVersion, and its absence fails *silently*: the build succeeds against SDK defaults and every
# image reports version 0.0.0 forever.
COPY ["Directory.Packages.props", "Directory.Build.props", "global.json", "./"]
#
# Contracts comes along because Backend references it. Restoring a project restores its references
# too, so without this the restore below silently covers Backend alone and the publish — which runs
# --no-restore — dies on Contracts/obj/project.assets.json with NETSDK1004, naming a file rather than
# the missing COPY. `.dockerignore` excludes **/obj, so a locally restored tree cannot paper over it.
COPY ["Backend/Backend.csproj", "Backend/"]
COPY ["Contracts/Contracts.csproj", "Contracts/"]
RUN dotnet restore "Backend/Backend.csproj"

COPY . .
WORKDIR "/src/Backend"
# Each override is passed only when it has a value, so an unset one leaves the project file's own
# setting alone instead of blanking it.
RUN dotnet publish "./Backend.csproj" \
	-c $BUILD_CONFIGURATION \
	-o /app/publish \
	--no-restore \
	/p:UseAppHost=false \
	${VERSION:+/p:Version=$VERSION} \
	${SOURCE_COMMIT:+/p:SourceRevisionId=$SOURCE_COMMIT}

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
