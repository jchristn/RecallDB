@echo off
echo Building RecallDB Server...
set TAG=%1
if "%TAG%"=="" set TAG=latest
cd /d "%~dp0"
echo Building and pushing multi-platform Docker image...
if "%TAG%"=="latest" (
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/recalldb-server:latest -f src/RecallDb.Server/Dockerfile --push .
) else (
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/recalldb-server:%TAG% -t jchristn77/recalldb-server:latest -f src/RecallDb.Server/Dockerfile --push .
)
