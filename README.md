# Altitude Hold Autopilot for Microsoft Flight Simulator 2024, SimConnect exercise

A real-time altitude-hold autopilot system that connects to **Microsoft Flight Simulator 2024** via the **SimConnect SDK**. This ASP.NET Core MVC application runs two PID controllers to maintain target altitude and keep wings level.

---

## Overview

This application provides a web-based interface to control an autopilot system that:

- **Maintains target altitude** using an Elevator Trim PID controller
- **Keeps wings level (roll = 0)** using an Aileron Trim PID controller
- Provides real-time telemetry display with live graphs
- Offers PID parameter tuning for fine-grained control

### Architecture

```
┌─────────────┐
│   Browser   │
│ site.js     │◄──── GET /Home/SimConnectStatus (every 2s)
└─────────────┘    GET /Home/AltitudeHoldState (every 250ms)
                    POST /Home/* endpoints

              ┌─────────────────────────────────────┐
              │        AltitudeHoldHostedService    │
              │         (BackgroundService)         │
              ├─────────────────────────────────────┤
              │  SimConnect dispatch loop           │
              │  ┌───────────────────────────────┐  │
              │  │ ControlLoop() - per SIM_FRAME │  │
              │  │ • ElevatorTrimPID → event     │  │
              │  │ • AileronTrimPID → event      │  │
              │  └───────────────────────────────┘  │
              │  Thread-safe SimulatorState         │
              └─────────────────────────────────────┘

              ┌─────────────────────────────────────┐
              │          HomeController             │
              │         (REST Endpoints)            │
              └─────────────────────────────────────┘
```

---

## System Requirements

- **Microsoft Flight Simulator 2024** installed and running
- **MSFS 2024 SDK** with SimConnect DLLs:
  - `C:\MSFS 2024 SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll`
  - `C:\MSFS 2024 SDK\SimConnect SDK\lib\SimConnect.dll`
- **.NET 10.0 Runtime** (or later)

---

## Build & Run

### Development Mode

```bash
# Navigate to project directory
cd Autopilot

# Restore dependencies and build
dotnet build

# Start the application
dotnet run --project Autopilot
```

## Access the Application

After running `dotnet run --project Autopilot`, open your browser and navigate to:

- **Local URL**: http://localhost:5278/ (or the port shown in console)

---

## User Interface

### Main Dashboard

The application provides a single-page dashboard with:

#### Status Indicator
- Visual connection state (green pulse = connected, red pulse = disconnected)
- Real-time simulation status
- Last update timestamp

#### Altitude Hold Controls
- ✓ Active toggle switch
- Target altitude input field

#### Real-time Graphs
- **Altitude Graph**: Shows altitude vs target with 100-point sliding window
- **Climb Angle Graph**: Shows climb angle deviation from target

#### PID Tuning Panels
Two independent tuning sections:

**Elevator Trim PID** (controls pitch/altitude)
- Kp (Proportional gain) × 1e⁻⁴
- Ki (Integral gain) × 1e⁻⁴
- Kd (Derivative gain) × 1e⁻⁴
- Real-time output monitoring (P, I, D terms)

**Aileron Trim PID** (controls roll/wings level)
- Same parameters as elevator trim
- Keeps aircraft wings level (roll = 0)

---

## 🔌 API Endpoints

### GET Endpoints

| Endpoint | Description |
|----------|-------------|
| `/Home/SimConnectStatus` | Returns connection state and simulation info |
| `/Home/AltitudeHoldState` | Returns full autopilot state + PID internals |

### POST Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/Home/ActivateAltitudeHold` | POST | Enable autopilot altitude hold |
| `/Home/DeactivateAltitudeHold` | POST | Disable autopilot altitude hold |
| `/Home/SetTargetAltitude` | POST | Set target altitude (query: `targetAltitude=N`) |
| `/Home/SetPIDParameters` | POST | Tune PID gains for specific controller |

---
