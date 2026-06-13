@echo off
title Timesheet Copilot App Runner
cls

echo ==========================================================
echo               Timesheet Copilot App Runner
echo ==========================================================
echo.
echo This script will start both the Backend (.NET 10 API)
echo and Frontend (Next.js) in separate windows.
echo.

:: Check for backend folder
if not exist backend (
    echo [ERROR] backend directory not found!
    goto error
)

:: Check for frontend folder
if not exist frontend (
    echo [ERROR] frontend directory not found!
    goto error
)

:: Check for dotnet
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo [ERROR] .NET SDK is not installed or not in PATH!
    goto error
)

:: Check for node/npm
where npm >nul 2>nul
if %errorlevel% neq 0 (
    echo [ERROR] Node.js/npm is not installed or not in PATH!
    goto error
)

echo [INFO] Starting Backend in a new window...
start "Timesheet Copilot - Backend (Port 5116)" cmd /k "cd backend && echo Starting ASP.NET Core backend... && dotnet run"

echo [INFO] Starting Frontend in a new window...
start "Timesheet Copilot - Frontend (Port 3000)" cmd /k "cd frontend && echo Starting Next.js frontend... && if not exist node_modules (echo Installing dependencies... && npm.cmd install) && npm.cmd run dev"

echo.
echo [SUCCESS] Both services have been launched!
echo - Backend: http://localhost:5116
echo - Frontend: http://localhost:3000
echo.
echo You can close this window now. The services will keep running in their own windows.
echo.
pause
exit

:error
echo.
echo [FAILED] Could not launch the applications. Please fix the errors above.
echo.
pause
