@echo off
echo Building all RecallDB images...
set TAG=%1
if "%TAG%"=="" set TAG=latest
cd /d "%~dp0"
call build-server.bat %TAG%
if errorlevel 1 exit /b %errorlevel%
call build-dashboard.bat %TAG%
if errorlevel 1 exit /b %errorlevel%
echo Done building all RecallDB images (tag: %TAG%).
