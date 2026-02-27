using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

Console.WriteLine("Starting MCP Host with Claude...\n");

// Load system prompt from file
var promptPath = Path.Combine(Directory.GetCurrentDirectory(), "Prompts", "system.txt");
var systemPrompt = File.Exists(promptPath)
    ? await File.ReadAllTextAsync(promptPath)
    : "You are a helpful weather assistant.";

// Connect to MCP Server
var clientTransport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "weather-server",
    Command = "python",
    Arguments = ["server.py"],
    WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..", "mcp-server")
});

await using var mcpClient = await McpClient.CreateAsync(clientTransport);
Console.WriteLine("Connected to MCP Server");

// Get tools from MCP server
var mcpTools = await mcpClient.ListToolsAsync();
Console.WriteLine($"Loaded {mcpTools.Count} tools: {string.Join(", ", mcpTools.Select(t => t.Name))}\n");

// Create Anthropic client
var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("Error: ANTHROPIC_API_KEY environment variable is not set.");
return;
}

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

// Convert MCP tools to Anthropic format
var anthropicTools = mcpTools.Select(tool =>
{
    var inputSchema = new JsonObject { ["type"] = "object" };

    if (tool.JsonSchema.ValueKind != JsonValueKind.Undefined)
    {
        var schemaJson = tool.JsonSchema.GetRawText();
        var schema = JsonNode.Parse(schemaJson)?.AsObject();
        if (schema != null)
        {
            if (schema.TryGetPropertyValue("properties", out var props))
                inputSchema["properties"] = props?.DeepClone();
            if (schema.TryGetPropertyValue("required", out var req))
                inputSchema["required"] = req?.DeepClone();
        }
    }

    return new JsonObject
    {
        ["name"] = tool.Name,
        ["description"] = tool.Description ?? "",
        ["input_schema"] = inputSchema
    };
}).ToList();

// Check for command line argument or interactive mode
var messages = new JsonArray();
string? userInput = args.Length > 0 ? string.Join(" ", args) : null;

if (userInput == null)
{
    Console.WriteLine("Weather Assistant Ready! (type 'exit' to quit)\n");
}

while (true)
{
    if (userInput == null)
    {
        Console.Write("You: ");
        userInput = Console.ReadLine();
        if (string.IsNullOrEmpty(userInput) || userInput.ToLower() == "exit")
            break;
    }
    else
    {
        Console.WriteLine($"Query: {userInput}\n");
    }

    messages.Add(new JsonObject
    {
        ["role"] = "user",
        ["content"] = userInput
    });

    var singleQuery = args.Length > 0;

    // Send to Claude
    var response = await CallClaude(httpClient, systemPrompt, messages, anthropicTools);

    // Process response - handle tool calls
    while (response?["stop_reason"]?.GetValue<string>() == "tool_use")
    {
        var content = response["content"]?.AsArray();
        messages.Add(new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = content?.DeepClone()
        });

        var toolResults = new JsonArray();

        foreach (var block in content ?? [])
        {
            if (block?["type"]?.GetValue<string>() == "tool_use")
            {
                var toolName = block["name"]?.GetValue<string>() ?? "";
                var toolId = block["id"]?.GetValue<string>() ?? "";
                var toolInput = block["input"];

                Console.WriteLine($"[Calling tool: {toolName}]");

                // Call MCP tool
                var toolArgs = new Dictionary<string, object?>();
                if (toolInput != null)
                {
                    toolArgs = JsonSerializer.Deserialize<Dictionary<string, object?>>(toolInput.ToJsonString())
                        ?? [];
                }

                var toolResult = await mcpClient.CallToolAsync(toolName, toolArgs);
                var resultText = string.Join("\n", toolResult.Content
                    .OfType<TextContentBlock>()
                    .Select(c => c.Text));

                toolResults.Add(new JsonObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = toolId,
                    ["content"] = resultText
                });
            }
        }

        messages.Add(new JsonObject
        {
            ["role"] = "user",
            ["content"] = toolResults
        });

        // Continue with tool results
        response = await CallClaude(httpClient, systemPrompt, messages, anthropicTools);
    }

    // Get final text response
    var responseContent = response?["content"]?.AsArray();
    messages.Add(new JsonObject
    {
        ["role"] = "assistant",
        ["content"] = responseContent?.DeepClone()
    });

    var textResponse = string.Join("", responseContent?
        .Where(b => b?["type"]?.GetValue<string>() == "text")
        .Select(b => b?["text"]?.GetValue<string>() ?? "") ?? []);

    Console.WriteLine($"\nAssistant: {textResponse}\n");

    if (singleQuery)
        break;

    userInput = null;
}

if (args.Length == 0)
    Console.WriteLine("Goodbye!");

// Helper function to call Claude API
async Task<JsonObject?> CallClaude(HttpClient client, string system, JsonArray msgs, List<JsonObject> tools)
{
    var requestBody = new JsonObject
    {
        ["model"] = "claude-sonnet-4-20250514",
        ["max_tokens"] = 1024,
        ["system"] = system,
        ["messages"] = msgs.DeepClone(),
        ["tools"] = new JsonArray(tools.Select(t => t.DeepClone()).ToArray())
    };

    var content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json");
    var response = await client.PostAsync("https://api.anthropic.com/v1/messages", content);

    var responseText = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"API Error: {response.StatusCode}");
        Console.WriteLine(responseText);
        return null;
    }

    return JsonNode.Parse(responseText)?.AsObject();
}
