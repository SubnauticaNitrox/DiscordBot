#!/usr/bin/env bash

docker build \
  --tag nitroxdiscordbot:dev \
  -f ./NitroxDiscordBot/Dockerfile \
  . && \
docker run \
  -it \
  --rm \
  -e DOTNET_ENVIRONMENT=Development \
  nitroxdiscordbot:dev