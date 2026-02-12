@echo off
if exist logs rd /s /q logs
if exist recalldb.json del /f /q recalldb.json
echo Clean complete.
