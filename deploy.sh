#!/bin/bash
set -e

cd /var/www/DriverTime

git pull origin main

docker compose down
docker compose build --no-cache
docker compose up -d

docker ps
