@echo off
rem Stoppar AgentFlow. Korningarna finns kvar i databasen till nasta start.
cd /d "%~dp0"
docker compose stop
pause
