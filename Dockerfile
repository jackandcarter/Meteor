# AetherXIV 1.3 stack image.
#
# Two independent targets built from this one file:
#   docker build --target web    -t aetherxiv-13-web:local .   (PHP launcher/web)
#   docker build             -t aetherxiv-13:local .           (mono lobby/world/map, default)
#
# "web" is declared first and "server" last so a plain `docker build`
# (no --target) resolves to the server image.

# ---------------------------------------------------------------------------
# target: web
# ---------------------------------------------------------------------------
FROM php:8.3-cli AS web

RUN docker-php-ext-install mysqli

WORKDIR /opt/aetherxiv/Data/www

# Live traffic runs against the compose bind mount of Data/www; this COPY
# only exists so the image is runnable standalone (e.g. this build check).
COPY Data/www/ ./

EXPOSE 8080

CMD ["php", "-S", "0.0.0.0:8080", "-t", "/opt/aetherxiv/Data/www"]

# ---------------------------------------------------------------------------
# target: server
# ---------------------------------------------------------------------------
# Debian, not Ubuntu: Ubuntu 24.04 removed the mono packages entirely,
# while Debian bookworm still ships mono 6.8 + xbuild + nuget for arm64/x86.
FROM debian:bookworm-slim AS server

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y --no-install-recommends \
      mono-complete \
      mono-xbuild \
      ca-certificates \
      perl \
      bash \
      curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /opt/aetherxiv
COPY . .

# Debian's apt nuget (2.8.x) only speaks the retired NuGet v2 feed and
# cannot restore anything from today's nuget.org, so always use a current
# nuget.exe under mono - the same pattern tools/bootstrap-ubuntu-build.sh
# uses for local dev boxes.
RUN set -eux; \
    mkdir -p /opt/aetherxiv/.tools/bin /opt/aetherxiv/.tools/nuget; \
    curl -fsSL https://dist.nuget.org/win-x86-commandline/latest/nuget.exe \
      -o /opt/aetherxiv/.tools/nuget/nuget.exe; \
    printf '#!/usr/bin/env bash\nexec mono /opt/aetherxiv/.tools/nuget/nuget.exe "$@"\n' \
      > /opt/aetherxiv/.tools/bin/nuget; \
    chmod +x /opt/aetherxiv/.tools/bin/nuget

ENV PATH="/opt/aetherxiv/.tools/bin:${PATH}"

# BUILD_TOOL is left unset on purpose: build-legacy.sh auto-detects msbuild
# then falls back to xbuild; Debian's mono has no msbuild, so xbuild is used.
RUN cd /opt/aetherxiv && CONFIGURATION=Release RESTORE=1 ./tools/build-legacy.sh

# Fail the image build loudly if any server binary did not come out the
# other end, instead of shipping a container that fails only at runtime.
RUN set -eux; \
    for exe in \
      "/opt/aetherxiv/Lobby Server/bin/Release/AetherXIV.Core.Lobby.exe" \
      "/opt/aetherxiv/World Server/bin/Release/AetherXIV.Core.World.exe" \
      "/opt/aetherxiv/Map Server/bin/Release/AetherXIV.Core.Map.exe"; \
    do \
      if [ ! -f "$exe" ]; then \
        echo "BUILD FAILED: missing $exe" >&2; \
        exit 1; \
      fi; \
    done

# The bind-mounted repo checkout this COPY comes from may not preserve the
# executable bit, so set it explicitly here too.
RUN chmod +x /opt/aetherxiv/docker/entrypoint-server.sh

ENTRYPOINT ["/opt/aetherxiv/docker/entrypoint-server.sh"]
