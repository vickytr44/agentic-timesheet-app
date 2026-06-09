# Agentic Timesheet App 🪁

A state-of-the-art AI-assisted timesheet management dashboard featuring **bidirectional state sync** and **generative UI**. 

This application integrates the **Microsoft Agent Framework (.NET)** on the backend with **Next.js + CopilotKit** on the frontend, communicating over the **AG-UI protocol**. Users can log, update, clear, and submit their weekly charge hours simply by conversing with an embedded AI copilot in a premium, glassmorphism dark-themed user interface.

---

## 🏗️ Architecture

```
                                  [ AG-UI Protocol ]
┌────────────────────────┐  API Route  ┌──────────────┐  TCP Port 5116  ┌────────────────────────┐
│  Next.js Frontend      │ ──────────> │  CopilotKit  │ ──────────────> │  .NET Core Backend     │
│  React 19 + TypeScript │             │  Runtime     │                 │  Microsoft.Agents.AI   │
└────────────────────────┘             └──────────────┘                 └────────────────────────┘
```

* **Frontend**: Built with **Next.js 16 (App Router)**, TypeScript, and TailwindCSS. Leverages `@copilotkit/react-core` and `@copilotkit/react-ui` for chat sidebar integrations and `useCoAgent` for bidirectional state updates.
* **Backend**: Powered by **ASP.NET Core (.NET 10)** and the `Microsoft.Agents.AI` framework. Uses `IChatClient` configured for **Groq (Llama-3.3-70b)** as the LLM provider.
* **State Synchronization**: Custom `TimesheetSharedStateAgent` (inheriting from `DelegatingAIAgent`) maps frontend UI changes to the backend business service (`TimesheetService`), allowing the agent to execute actions based on the current state.

---

## ✨ Features

* **Natural Language Logging**: Speak naturally to log work: *"Add 8 hours to Project Antigravity for styling today."*
* **Bidirectional Sync**: Edits made in the UI form are immediately visible to the AI copilot, and changes made by the copilot instantly update the UI table.
* **Timesheet Submission & Locking**: Ask the assistant to submit/lock the timesheet, disabling manual edits.
* **Lock Reversal (Undo)**: Revert locked submissions by asking the assistant to *"unlock"* or *"undo"* the timesheet.
* **Dynamic Theme Customization**: Instruct the AI to change the theme color dynamically: *"Set the theme to #10b981 (emerald green)."*
* **Aesthetics**: Sleek glassmorphism card layouts, tailored HSL color schemes, and dark-mode ambient glows.

---

## 🚀 Getting Started

### Prerequisites
* [Node.js 20+](https://nodejs.org/)
* [.NET 10 SDK](https://dotnet.microsoft.com/)
* A [Groq API Key](https://console.groq.com/)

---

### Setup Instructions

#### 1. Clone & Configure Backend
Create an `appsettings.Development.json` file inside the `backend/` folder to hold your local development keys (this file is excluded from git):

```json
{
  "OpenAI": {
    "ApiKey": "YOUR_GROQ_API_KEY",
    "ModelId": "llama-3.3-70b-versatile",
    "Endpoint": "https://api.groq.com/openai/v1/",
    "OpenAI_ApiKey_Backup": "YOUR_OPENAI_API_KEY"
  }
}
```

*Alternatively, you can set the `OPENAI_API_KEY` environment variable in your terminal.*

#### 2. Run the Backend
Start the .NET backend server on port `5116`:
```bash
cd backend
dotnet run
```

#### 3. Run the Frontend
Install frontend packages and run the Next.js development server on port `3000`:
```bash
cd frontend
npm install
npm run dev
```

Open **[http://localhost:3000](http://localhost:3000)** in your browser.

---

## 🛠️ API & Endpoint Specs

### Backend Endpoints (Port 5116)
* `GET /info` - AG-UI Metadata and Agent registration info.
* `POST /agent/my_agent/run` - Streaming interface endpoint serving the AG-UI agent.
* `GET /api/timesheet` - Returns the raw list of logged timesheet entries.
* `GET /api/timesheet/summary` - Returns summary metrics (TotalHours, ProjectCount, Status).
* `POST /api/timesheet/entry` - Manually appends a work entry.
* `DELETE /api/timesheet/entry/{id}` - Removes a specific timesheet entry.
* `POST /api/timesheet/submit` - Locks the timesheet and sets status to `Submitted`.
* `POST /api/timesheet/unlock` - Unlocks the timesheet and resets status to `Draft`.

---

## 🔒 License
Distributed under the MIT License. See `LICENSE` for more information.
