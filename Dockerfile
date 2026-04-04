# syntax=docker/dockerfile:1.7

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS base
WORKDIR /src

ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    NUGET_XMLDOC_MODE=skip

ARG SOLUTION=GML4Net.sln
ARG CONFIGURATION=Release

FROM base AS restore
ARG SOLUTION=GML4Net.sln
COPY . .
RUN test -f "${SOLUTION}" || { echo "Solution file '${SOLUTION}' not found in /src"; exit 1; }
RUN dotnet restore "${SOLUTION}"

FROM restore AS build
ARG SOLUTION=GML4Net.sln
ARG CONFIGURATION=Release
RUN dotnet build "${SOLUTION}" -c "${CONFIGURATION}" --no-restore

FROM build AS test
ARG SOLUTION=GML4Net.sln
ARG CONFIGURATION=Release
RUN dotnet test "${SOLUTION}" -c "${CONFIGURATION}" --no-build --logger "trx;LogFileName=test-results.trx"

FROM build AS pack
ARG SOLUTION=GML4Net.sln
ARG CONFIGURATION=Release
ARG VERSION=
ARG PACKAGE_VERSION=
RUN set -eu; \
    version_args=""; \
    if [ -n "${PACKAGE_VERSION}" ]; then \
        version_args="/p:PackageVersion=${PACKAGE_VERSION} /p:Version=${PACKAGE_VERSION}"; \
    elif [ -n "${VERSION}" ]; then \
        version_args="/p:PackageVersion=${VERSION} /p:Version=${VERSION}"; \
    fi; \
    mkdir -p /artifacts; \
    dotnet pack "${SOLUTION}" -c "${CONFIGURATION}" --no-build -o /artifacts ${version_args}

FROM pack AS push
ARG NUGET_SOURCE=https://api.nuget.org/v3/index.json
ARG NUGET_API_KEY=
RUN --mount=type=secret,id=nuget_api_key \
    set -eu; \
    api_key="${NUGET_API_KEY}"; \
    if [ -z "${api_key}" ] && [ -f /run/secrets/nuget_api_key ]; then \
        api_key="$(cat /run/secrets/nuget_api_key)"; \
    fi; \
    test -n "${api_key}"; \
    found=0; \
    for pkg in /artifacts/*.nupkg; do \
        if [ ! -e "${pkg}" ]; then \
            continue; \
        fi; \
        case "${pkg}" in \
        *.snupkg) \
            continue; \
            ;; \
        esac; \
        found=1; \
        dotnet nuget push "${pkg}" \
            --api-key "${api_key}" \
            --source "${NUGET_SOURCE}" \
            --skip-duplicate; \
    done; \
    test "${found}" -eq 1

FROM scratch AS artifacts
COPY --from=pack /artifacts/ /
