# AGUI Setup - Corrected Architecture

## Issue Fixed

The original proxy setup was just forwarding requests without implementing CopilotRuntime. This has been corrected:

**Before (❌ Incorrect):**
```
Frontend → Proxy (simple forwarding) → Python Agent
```

**After (✅ Correct):**
```
Frontend → Proxy (with CopilotRuntime) → Python Agent
          ↑
    Both React & Angular connect here
```

## How It Works Now

1. **Express Proxy Server** (`server.js`) on port 3001:
   - Implements full CopilotRuntime
   - Connects to Python agent at `http://localhost:8000`
   - Handles CopilotKit requests from both frontends
   - CORS enabled for localhost:3000 (React) and localhost:4200 (Angular)

2. **React Next.js** on port 3000:
   - Has its own CopilotRuntime endpoint at `/api/copilotkit`
   - Connects directly to Python agent at `http://localhost:8000`
   - Uses standard Next.js API routes

3. **Angular** on port 4200:
   - Points to proxy server at `http://localhost:3001/api/copilotkit`
   - Proxy's CopilotRuntime handles the request
   - Proxy forwards to Python agent

## Required Dependencies

Make sure React's `package.json` has these dependencies (for server.js):

```json
"dependencies": {
  "@copilotkit/runtime": "^1.50.1",
  "cors": "^2.8.5",
  "express": "^4.18.2"
}
```

These are already added to `aguibasic/package.json`.

## Installation

### 1. Install React Dependencies
```bash
cd aguibasic
npm install
```

### 2. Install Angular Dependencies (if using Angular)
```bash
cd aguibasic-angular
npm install
```

### 3. Install Python Agent
```bash
cd agent
pip install -r requirements.txt
```

## Running

### Start Individual Services

**Terminal 1 - Python Agent:**
```bash
cd agent
python -m uvicorn src.main:app --reload --port 8000
```

**Terminal 2 - Express Proxy (CopilotRuntime):**
```bash
npm run dev:server
```

**Terminal 3 - React:**
```bash
cd aguibasic
npm run dev:ui
```

**Terminal 4 - Angular (optional):**
```bash
cd aguibasic-angular
npm start
```

### All at Once (Windows)
```bash
run-dev-all.bat
```

### All at Once (Unix/Mac)
```bash
./run-dev-all.sh
```

## Verify Everything Works

After starting all services, check these endpoints:

1. **Health Check - Proxy:**
```bash
curl http://localhost:3001/health
# Should return: {"status":"ok","service":"copilotkit-proxy-server"}
```

2. **React App:**
- Open http://localhost:3000
- Chat should work with agent

3. **Angular App:**
- Open http://localhost:4200
- Chat should work with agent (via proxy)

4. **Python Agent:**
- Visit http://localhost:8000/docs
- Should see Swagger API documentation

## Port Allocation

| Service | Port | Purpose |
|---------|------|---------|
| React | 3000 | Frontend with Next.js |
| Express Proxy | 3001 | CopilotRuntime gateway |
| Angular | 4200 | Angular frontend |
| Python Agent | 8000 | FastAPI backend |

## Key Architecture Files

- **React CopilotKit**: [aguibasic/src/app/api/copilotkit/route.ts](aguibasic/src/app/api/copilotkit/route.ts)
  - Creates CopilotRuntime
  - Connects to Python agent directly

- **Proxy Server**: [server.js](server.js)
  - Creates CopilotRuntime
  - Handles requests from Angular
  - Forwards to Python agent

- **Angular Config**: `aguibasic-angular/src/app/app.config.ts`
  - Points to proxy: `http://localhost:3001/api/copilotkit`

## Environment Variables

Create `.env` file in `agent/` or root:

```env
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
AZURE_OPENAI_API_KEY=your-api-key
OPENAI_API_VERSION=2024-02-15
MODEL_NAME=gpt-4
```

## Troubleshooting

### "ECONNREFUSED" Error
This means one of the services isn't running:
1. Ensure Python agent is running on 8000
2. Ensure Express proxy is running on 3001
3. Check that all npm modules are installed

```bash
# Test each service
curl http://localhost:8000/docs      # Python Agent
curl http://localhost:3001/health    # Proxy Server
curl http://localhost:3000           # React
```

### "Cannot reach agent" in Chat
1. Verify Python agent is running and accessible
2. Check network connectivity between services
3. Ensure Azure OpenAI credentials are set

### Port Already in Use
Kill the process using the port:

**Windows:**
```bash
netstat -ano | findstr :3001
taskkill /PID <PID> /F
```

**Unix/Mac:**
```bash
lsof -i :3001
kill -9 <PID>
```

### Missing Dependencies
Reinstall all dependencies:

```bash
cd aguibasic && npm install && npm audit fix
cd ../aguibasic-angular && npm install
cd ../agent && pip install -r requirements.txt
```

## Next Steps

1. ✅ Ensure all dependencies are installed
2. ✅ Set Azure OpenAI environment variables
3. ✅ Start Python agent first
4. ✅ Start Express proxy server
5. ✅ Start React and/or Angular dev servers
6. ✅ Test chat functionality in both frontends
7. ✅ Try the quick-start suggestions

## Documentation

- **[SETUP.md](SETUP.md)** - Full architecture overview
- **[INSTALL.md](INSTALL.md)** - Detailed installation steps
- **[QUICKREF.md](QUICKREF.md)** - Quick reference guide
- **[aguibasic-angular/README.md](aguibasic-angular/README.md)** - Angular docs

---

**Updated**: December 31, 2025
**Status**: ✅ Corrected and Ready
