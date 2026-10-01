#!/bin/bash
set -e

cd /var/www/DriverTime

git fetch origin main
git checkout -f main
git reset --hard origin/main

docker compose down
docker compose build --no-cache
docker compose up -d

docker ps
