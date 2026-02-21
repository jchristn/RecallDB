@echo off
REM ============================================================================
REM RecallDB Factory Reset Script
REM Resets the Docker deployment to default factory configuration.
REM ============================================================================

setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
set "DOCKER_DIR=%SCRIPT_DIR%.."

echo =========================================
echo   RecallDB Factory Reset
echo =========================================
echo.
echo WARNING: This will destroy ALL data including:
echo   - All tenants, users, credentials, and collections
echo   - All documents and vector embeddings
echo   - All log files
echo   - All configuration changes
echo.
echo The deployment will be restored to factory defaults:
echo   - Default tenant: 'Default Tenant'
echo   - Default user: admin@recall / password
echo   - Default credential: bearer token 'default'
echo   - Default collection: 'Default Collection' (384 dimensions)
echo.
set /p "confirmation=Type RESET to confirm: "

if not "%confirmation%"=="RESET" (
    echo.
    echo Reset cancelled.
    exit /b 1
)

echo.
echo [1/6] Stopping containers and removing volumes...
pushd "%DOCKER_DIR%"
docker compose down -v 2>nul
if errorlevel 1 (
    docker-compose down -v 2>nul
)
echo        Done.

echo.
echo [2/6] Restoring factory configuration...
copy /y "%SCRIPT_DIR%recalldb.json" "%DOCKER_DIR%\recalldb.json" >nul
echo        Copied recalldb.json

echo.
echo [3/6] Cleaning log files...
if exist "%DOCKER_DIR%\logs" (
    del /q /s "%DOCKER_DIR%\logs\*.log" 2>nul
    del /q /s "%DOCKER_DIR%\logs\*.log.*" 2>nul
    echo        Removed log files from %DOCKER_DIR%\logs\
) else (
    echo        No logs directory found, skipping.
)

echo.
echo [4/6] Starting PostgreSQL (pgvector)...
docker compose up -d pgvector 2>nul
if errorlevel 1 (
    docker-compose up -d pgvector 2>nul
)

echo        Waiting for PostgreSQL to become ready...
set retries=0
set max_retries=30
:wait_loop
docker exec recalldb-pgvector pg_isready -U recalldb -d recalldb >nul 2>&1
if not errorlevel 1 goto :pg_ready
set /a retries+=1
if %retries% geq %max_retries% (
    echo        ERROR: PostgreSQL did not become ready within %max_retries% seconds.
    popd
    exit /b 1
)
timeout /t 1 /nobreak >nul
goto :wait_loop

:pg_ready
echo        PostgreSQL is ready.

echo.
echo [5/6] Applying factory database schema and default records...
docker exec -i recalldb-pgvector psql -U recalldb -d recalldb < "%SCRIPT_DIR%recalldb_factory.sql"
echo        Database initialized.

echo.
echo [6/6] Starting all services...
docker compose up -d 2>nul
if errorlevel 1 (
    docker-compose up -d 2>nul
)

popd

echo.
echo =========================================
echo   Factory reset complete!
echo =========================================
echo.
echo   Server:     http://localhost:8600
echo   Dashboard:  http://localhost:8601
echo.
echo   Default login:
echo     Email:    admin@recall
echo     Password: password
echo     Bearer:   default
echo     Admin key: recalldbadmin
echo.

endlocal
