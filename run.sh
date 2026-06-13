#!/bin/bash

# Exit on error
set -e

echo "=========================================================="
echo "              Timesheet Copilot App Runner"
echo "=========================================================="
echo ""

# Check if directories exist
if [ ! -d "backend" ]; then
    echo "[ERROR] backend directory not found!"
    exit 1
fi

if [ ! -d "frontend" ]; then
    echo "[ERROR] frontend directory not found!"
    exit 1
fi

# Check requirements
if ! command -v dotnet &> /dev/null; then
    echo "[ERROR] .NET SDK is not installed or not in PATH!"
    exit 1
fi

if ! command -v npm &> /dev/null; then
    echo "[ERROR] Node.js/npm is not installed or not in PATH!"
    exit 1
fi

# Start Backend in background
echo "[INFO] Starting Backend..."
cd backend
dotnet run &
BACKEND_PID=$!
cd ..

# Start Frontend in background
echo "[INFO] Starting Frontend..."
cd frontend
if [ ! -d "node_modules" ]; then
    echo "[INFO] node_modules not found, installing dependencies..."
    npm install
fi
npm run dev &
FRONTEND_PID=$!
cd ..

echo ""
echo "[SUCCESS] Both services are running in the background!"
echo " - Backend PID: $BACKEND_PID (http://localhost:5116)"
echo " - Frontend PID: $FRONTEND_PID (http://localhost:3000)"
echo ""
echo "Press Ctrl+C to stop both services."

# Trap Ctrl+C to kill both background processes
cleanup() {
    echo ""
    echo "[INFO] Stopping services..."
    kill $BACKEND_PID 2>/dev/null || true
    kill $FRONTEND_PID 2>/dev/null || true
    echo "[INFO] Both services stopped."
    exit 0
}

trap cleanup INT TERM

# Keep script running
wait
