import asyncio
import httpx
from mcp.server import Server
from mcp.server.stdio import stdio_server
from mcp.types import Tool, TextContent

# Create MCP server instance
server = Server("weather-server")

# Free weather API (no key required)
WEATHER_API_URL = "https://api.open-meteo.com/v1/forecast"

@server.list_tools()
async def list_tools() -> list[Tool]:
    """List available weather tools."""
    return [
        Tool(
            name="get_weather",
            description="Get current weather for a location by latitude and longitude",
            inputSchema={
                "type": "object",
                "properties": {
                    "latitude": {
                        "type": "number",
                        "description": "Latitude of the location"
                    },
                    "longitude": {
                        "type": "number",
                        "description": "Longitude of the location"
                    }
                },
                "required": ["latitude", "longitude"]
            }
        ),
        Tool(
            name="get_weather_by_city",
            description="Get current weather for a city name",
            inputSchema={
                "type": "object",
                "properties": {
                    "city": {
                        "type": "string",
                        "description": "Name of the city"
                    }
                },
                "required": ["city"]
            }
        )
    ]

@server.call_tool()
async def call_tool(name: str, arguments: dict) -> list[TextContent]:
    """Handle tool calls."""
    if name == "get_weather":
        return await get_weather(arguments["latitude"], arguments["longitude"])
    elif name == "get_weather_by_city":
        return await get_weather_by_city(arguments["city"])
    else:
        return [TextContent(type="text", text=f"Unknown tool: {name}")]

async def get_weather(latitude: float, longitude: float) -> list[TextContent]:
    """Fetch weather data from Open-Meteo API."""
    async with httpx.AsyncClient() as client:
        response = await client.get(
            WEATHER_API_URL,
            params={
                "latitude": latitude,
                "longitude": longitude,
                "current": "temperature_2m,wind_speed_10m,relative_humidity_2m,weather_code",
                "temperature_unit": "celsius"
            }
        )

        if response.status_code != 200:
            return [TextContent(type="text", text=f"Error fetching weather: {response.status_code}")]

        data = response.json()
        current = data.get("current", {})

        weather_text = f"""Current Weather:
- Temperature: {current.get('temperature_2m', 'N/A')}°C
- Wind Speed: {current.get('wind_speed_10m', 'N/A')} km/h
- Humidity: {current.get('relative_humidity_2m', 'N/A')}%
- Weather Code: {current.get('weather_code', 'N/A')}"""

        return [TextContent(type="text", text=weather_text)]

async def get_weather_by_city(city: str) -> list[TextContent]:
    """Get coordinates for a city and fetch weather."""
    geocode_url = "https://geocoding-api.open-meteo.com/v1/search"

    async with httpx.AsyncClient() as client:
        geo_response = await client.get(geocode_url, params={"name": city, "count": 1})

        if geo_response.status_code != 200:
            return [TextContent(type="text", text=f"Error finding city: {geo_response.status_code}")]

        geo_data = geo_response.json()
        results = geo_data.get("results", [])

        if not results:
            return [TextContent(type="text", text=f"City not found: {city}")]

        location = results[0]
        latitude = location["latitude"]
        longitude = location["longitude"]
        city_name = location.get("name", city)
        country = location.get("country", "")

        weather_result = await get_weather(latitude, longitude)
        location_info = f"Location: {city_name}, {country} ({latitude}, {longitude})\n\n"

        return [TextContent(type="text", text=location_info + weather_result[0].text)]

async def main():
    """Run the MCP server."""
    async with stdio_server() as (read_stream, write_stream):
        await server.run(read_stream, write_stream, server.create_initialization_options())

if __name__ == "__main__":
    asyncio.run(main())
