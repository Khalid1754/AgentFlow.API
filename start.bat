@echo off
rem Startar AgentFlow i Docker och oppnar appen i webblasaren. Dubbelklicka for att kora.
title AgentFlow
cd /d "%~dp0"

if exist .env goto env_klar
copy .env.example .env >nul
echo Skapade .env. Lagg in din GROQ_API_KEY, spara och kor start.bat igen.
notepad .env
exit /b 0
:env_klar

docker info >nul 2>&1
if not errorlevel 1 goto docker_klar
echo Startar Docker Desktop...
start "" "%ProgramFiles%\Docker\Docker\Docker Desktop.exe"
:vanta_docker
timeout /t 3 /nobreak >nul
docker info >nul 2>&1
if errorlevel 1 goto vanta_docker
:docker_klar

echo Startar AgentFlow (forsta gangen kan ta nagon minut)...
docker compose up -d --build
if not errorlevel 1 goto vanta_app
echo.
echo Nagot gick fel. Las felmeddelandet ovan.
pause
exit /b 1

:vanta_app
echo Vantar pa att appen ska svara...
set forsok=0
:vanta_igen
timeout /t 2 /nobreak >nul
curl -sf -o nul http://localhost:8090/api/status
if not errorlevel 1 goto oppna
set /a forsok+=1
rem Ge upp efter ca 2 minuter och oppna appen anda.
if %forsok% lss 60 goto vanta_igen

:oppna
start "" http://localhost:8090
echo AgentFlow kor pa http://localhost:8090
echo.
pause
