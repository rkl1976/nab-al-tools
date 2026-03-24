using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol;
using NabXliffWeb.Models;

namespace NabXliffWeb.Services;

public sealed class McpBridgeService : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<McpBridgeService> _logger;
    private McpClient? _mcpClient;
    private bool _isInitialized;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool IsConnected => _mcpClient is not null;
    public bool IsInitialized => _isInitialized;
    public string? CurrentAppFolder { get; private set; }

    public McpBridgeService(IConfiguration configuration, ILogger<McpBridgeService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_mcpClient is not null) return;

            var command = _configuration["McpServer:Command"] ?? "npx";
            var arguments = _configuration.GetSection("McpServer:Arguments").Get<string[]>()
                ?? ["-y", "@nabsolutions/nab-al-tools-mcp"];

            _logger.LogInformation("[MCP] Connecting to MCP server: {Command} {Arguments}",
                command, string.Join(" ", arguments));

            var sw = Stopwatch.StartNew();
            _mcpClient = await McpClient.CreateAsync(
                new StdioClientTransport(new()
                {
                    Name = "nab-al-tools-mcp",
                    Command = command,
                    Arguments = arguments,
                }),
                cancellationToken: cancellationToken);

            _logger.LogInformation("[MCP] Connected to MCP server in {ElapsedMs}ms", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MCP] Failed to connect to MCP server");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task InitializeAsync(string appFolderPath, string? workspaceFilePath = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[MCP] Initializing with appFolderPath={AppFolder}, workspaceFilePath={Workspace}",
            appFolderPath, workspaceFilePath ?? "(none)");

        if (_mcpClient is null)
            await ConnectAsync(cancellationToken);

        var args = new Dictionary<string, object?>
        {
            ["appFolderPath"] = appFolderPath
        };
        if (!string.IsNullOrEmpty(workspaceFilePath))
            args["workspaceFilePath"] = workspaceFilePath;

        await CallToolAsync("initialize", args, cancellationToken);
        _isInitialized = true;
        CurrentAppFolder = appFolderPath;
        _logger.LogInformation("[MCP] Initialized successfully for app folder: {AppFolder}", appFolderPath);
    }

    public async Task<IList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        if (_mcpClient is null)
            throw new InvalidOperationException("MCP server not connected");

        _logger.LogInformation("[MCP] Listing available tools");
        var sw = Stopwatch.StartNew();
        var tools = await _mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
        var toolList = tools.Cast<AITool>().ToList();
        _logger.LogInformation("[MCP] Listed {Count} tools in {ElapsedMs}ms: [{ToolNames}]",
            toolList.Count, sw.ElapsedMilliseconds,
            string.Join(", ", toolList.Select(t => t.Name)));
        return toolList;
    }

    public async Task<UntranslatedTextsResult> GetTextsToTranslateAsync(
        string filePath, int offset = 0, int limit = 50, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[McpBridge] GetTextsToTranslateAsync(filePath={FilePath}, offset={Offset}, limit={Limit})",
            filePath, offset, limit);

        var result = await CallToolAsync("getTextsToTranslate", new Dictionary<string, object?>
        {
            ["filePath"] = filePath,
            ["offset"] = offset,
            ["limit"] = limit
        }, cancellationToken);

        var parsed = JsonSerializer.Deserialize<UntranslatedTextsResult>(result)
            ?? throw new InvalidOperationException("Failed to deserialize untranslated texts");

        _logger.LogInformation("[McpBridge] GetTextsToTranslate returned {Returned}/{Total} untranslated texts",
            parsed.ReturnedCount, parsed.TotalUntranslatedCount);
        return parsed;
    }

    public async Task<string> SaveTranslatedTextsAsync(
        string filePath, List<TranslationToSave> translations, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[McpBridge] SaveTranslatedTextsAsync(filePath={FilePath}, count={Count})",
            filePath, translations.Count);
        foreach (var t in translations)
        {
            _logger.LogInformation("[McpBridge]   Save: id={Id}, targetText={TargetText}, state={State}",
                t.Id, Truncate(t.TargetText, 60), t.TargetState ?? "(default)");
        }

        var result = await CallToolAsync("saveTranslatedTexts", new Dictionary<string, object?>
        {
            ["filePath"] = filePath,
            ["translations"] = translations
        }, cancellationToken);

        _logger.LogInformation("[McpBridge] SaveTranslatedTexts result: {Result}", result);
        return result;
    }

    public async Task<List<TranslatedTextWithState>> GetTranslatedTextsByStateAsync(
        string filePath, int offset = 0, int limit = 50, string? stateFilter = null,
        string? sourceText = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[McpBridge] GetTranslatedTextsByStateAsync(filePath={FilePath}, offset={Offset}, limit={Limit}, state={State}, sourceText={SourceText})",
            filePath, offset, limit, stateFilter ?? "(all)", sourceText ?? "(none)");

        var args = new Dictionary<string, object?>
        {
            ["filePath"] = filePath,
            ["offset"] = offset,
            ["limit"] = limit
        };
        if (!string.IsNullOrEmpty(stateFilter))
            args["translationStateFilter"] = stateFilter;
        if (!string.IsNullOrEmpty(sourceText))
            args["sourceText"] = sourceText;

        var result = await CallToolAsync("getTranslatedTextsByState", args, cancellationToken);
        var parsed = JsonSerializer.Deserialize<List<TranslatedTextWithState>>(result) ?? [];
        _logger.LogInformation("[McpBridge] GetTranslatedTextsByState returned {Count} texts", parsed.Count);
        return parsed;
    }

    public async Task<List<TranslatedTextWithState>> GetTextsByKeywordAsync(
        string filePath, string keyword, int offset = 0, int limit = 50,
        bool searchInTarget = false, bool caseSensitive = false, bool isRegex = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[McpBridge] GetTextsByKeywordAsync(filePath={FilePath}, keyword={Keyword}, offset={Offset}, limit={Limit}, inTarget={InTarget}, caseSensitive={CaseSensitive}, regex={Regex})",
            filePath, keyword, offset, limit, searchInTarget, caseSensitive, isRegex);

        var result = await CallToolAsync("getTextsByKeyword", new Dictionary<string, object?>
        {
            ["filePath"] = filePath,
            ["keyword"] = keyword,
            ["offset"] = offset,
            ["limit"] = limit,
            ["searchInTarget"] = searchInTarget,
            ["caseSensitive"] = caseSensitive,
            ["isRegex"] = isRegex
        }, cancellationToken);

        var parsed = JsonSerializer.Deserialize<List<TranslatedTextWithState>>(result) ?? [];
        _logger.LogInformation("[McpBridge] GetTextsByKeyword returned {Count} results for '{Keyword}'",
            parsed.Count, keyword);
        return parsed;
    }

    public async Task<List<GlossaryEntry>> GetGlossaryTermsAsync(
        string targetLanguageCode, string? sourceLanguageCode = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[McpBridge] GetGlossaryTermsAsync(target={Target}, source={Source})",
            targetLanguageCode, sourceLanguageCode ?? "en-US");

        var args = new Dictionary<string, object?>
        {
            ["targetLanguageCode"] = targetLanguageCode
        };
        if (!string.IsNullOrEmpty(sourceLanguageCode))
            args["sourceLanguageCode"] = sourceLanguageCode;
        args["ignoreMissingLanguage"] = true;

        var result = await CallToolAsync("getGlossaryTerms", args, cancellationToken);
        var parsed = JsonSerializer.Deserialize<List<GlossaryEntry>>(result) ?? [];
        _logger.LogInformation("[McpBridge] GetGlossaryTerms returned {Count} entries", parsed.Count);
        return parsed;
    }

    public async Task<CreateLanguageXlfResult> CreateLanguageXlfAsync(
        string targetLanguageCode, bool matchBaseAppTranslation = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[McpBridge] CreateLanguageXlfAsync(target={Target}, matchBaseApp={Match})",
            targetLanguageCode, matchBaseAppTranslation);

        var result = await CallToolAsync("createLanguageXlf", new Dictionary<string, object?>
        {
            ["targetLanguageCode"] = targetLanguageCode,
            ["matchBaseAppTranslation"] = matchBaseAppTranslation
        }, cancellationToken);

        // MCP tool returns plain text: Successfully created XLF file: "path" with N matches.
        var parsed = ParseCreateLanguageResult(result);

        _logger.LogInformation("[McpBridge] CreateLanguageXlf created {FilePath} with {Matches} matches",
            parsed.TargetXlfFilepath, parsed.NumberOfMatches);
        return parsed;
    }

    private static CreateLanguageXlfResult ParseCreateLanguageResult(string response)
    {
        // Format: Successfully created XLF file: "C:/path/to/file.xlf" with 142 matches.
        var filePath = "";
        var matches = 0;

        var fileMatch = System.Text.RegularExpressions.Regex.Match(response, "\"([^\"]+\\.xlf)\"");
        if (fileMatch.Success)
            filePath = fileMatch.Groups[1].Value;

        var matchCount = System.Text.RegularExpressions.Regex.Match(response, @"(\d+)\s+match");
        if (matchCount.Success)
            matches = int.Parse(matchCount.Groups[1].Value);

        if (string.IsNullOrEmpty(filePath))
            throw new InvalidOperationException($"Could not parse createLanguageXlf response: {response}");

        return new CreateLanguageXlfResult(matches, filePath);
    }

    public async Task<string> RefreshXlfAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[McpBridge] RefreshXlfAsync(filePath={FilePath})", filePath);

        var result = await CallToolAsync("refreshXlf", new Dictionary<string, object?>
        {
            ["filePath"] = filePath
        }, cancellationToken);

        _logger.LogInformation("[McpBridge] RefreshXlf result: {Result}", result);
        return result;
    }

    private async Task<string> CallToolAsync(string toolName, Dictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        if (_mcpClient is null)
            throw new InvalidOperationException("MCP server not connected");

        var argsJson = JsonSerializer.Serialize(arguments, new JsonSerializerOptions { WriteIndented = false });
        _logger.LogInformation("[MCP] >>> CallTool: {ToolName}({Args})", toolName, argsJson);

        var sw = Stopwatch.StartNew();
        var result = await _mcpClient.CallToolAsync(toolName, arguments, cancellationToken: cancellationToken);
        var elapsed = sw.ElapsedMilliseconds;

        var responseText = string.Join("\n", result.Content
            .OfType<TextContentBlock>()
            .Select(c => c.Text));

        if (result.IsError == true)
        {
            _logger.LogError("[MCP] <<< CallTool: {ToolName} FAILED in {ElapsedMs}ms: {Error}",
                toolName, elapsed, responseText);
            throw new InvalidOperationException($"MCP tool '{toolName}' error: {responseText}");
        }

        _logger.LogInformation("[MCP] <<< CallTool: {ToolName} OK in {ElapsedMs}ms ({ResponseLength} chars)",
            toolName, elapsed, responseText.Length);
        _logger.LogInformation("[MCP] <<< CallTool: {ToolName} response:\n{Response}",
            toolName, responseText);

        return responseText;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("[MCP] Disposing MCP client");
        if (_mcpClient is not null)
        {
            await _mcpClient.DisposeAsync();
            _mcpClient = null;
        }
        _lock.Dispose();
    }
}
