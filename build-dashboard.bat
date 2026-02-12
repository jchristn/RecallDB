@echo off
echo Building RecallDB Dashboard...
set TAG=%1
if "%TAG%"=="" set TAG=latest
cd /d "%~dp0\dashboard"
echo Building and pushing multi-platform Docker image...
if "%TAG%"=="latest" (
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/recalldb-dashboard:latest --push .
) else (
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/recalldb-dashboard:%TAG% -t jchristn77/recalldb-dashboard:latest --push .
)
