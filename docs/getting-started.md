# mcp-host

A MCPHost practice project with weather MCP Servers.

## Tech Stack

- **Host**: .NET Core 8.0
- **MCP Server**: Python 3.10+

## Project Structure

```
mcphost/
├── README.md
├── mcp-server/          # Python MCP Server
│   ├── requirements.txt
│   └── server.py
└── host/                # .NET Core Host
    ├── MCPHost.csproj
    └── Program.cs
```

## Features

The weather MCP server provides two tools:

- `get_weather` - Get weather by latitude/longitude
- `get_weather_by_city` - Get weather by city name

Uses the free [Open-Meteo API](https://open-meteo.com/) (no API key required).

## Prerequisites

- Python 3.10 or higher
- .NET 8.0 SDK
- pip (Python package manager)

## Run

### 1. Install Python Dependencies

```bash
cd mcp-server
pip install -r requirements.txt
```

### 2. Run the Host Application

```bash
cd host
dotnet restore
dotnet run
```

The host will automatically start the Python MCP server and communicate with it via stdio.

## Example Output

```
Available Tools:
  - get_weather: Get current weather for a location by latitude and longitude
  - get_weather_by_city: Get current weather for a city name

--- Getting weather for Tokyo ---
Location: Tokyo, Japan (35.6895, 139.6917)

Current Weather:
- Temperature: 15.2°C
- Wind Speed: 12.3 km/h
- Humidity: 65%

--- Getting weather for New York (coordinates) ---
Current Weather:
- Temperature: 8.5°C
- Wind Speed: 18.7 km/h
- Humidity: 72%
```
