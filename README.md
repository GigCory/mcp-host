# mcp-host

A MCPHost practice project with weather MCP Servers.

## Tech Stack

- **Host**: .NET Core
- **MCP Server**: Python

## Run

### Start the MCP Server (Python)

```bash
cd mcp-server
pip install -r requirements.txt
python server.py
```

### Start the Host (.NET Core)

```bash
cd host
dotnet restore
dotnet run
```
