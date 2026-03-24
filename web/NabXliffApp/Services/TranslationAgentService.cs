using System.Diagnostics;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NabXliffApp.Models;
using System.Text.Json;

namespace NabXliffApp.Services;

public class TranslationAgentService
{
    private readonly McpBridgeService _mcpBridge;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TranslationAgentService> _logger;

    public TranslationAgentService(
        McpBridgeService mcpBridge,
        IConfiguration configuration,
        ILogger<TranslationAgentService> logger)
    {
        _mcpBridge = mcpBridge;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            var endpoint = _configuration["AzureOpenAI:Endpoint"];
            return !string.IsNullOrEmpty(endpoint);
        }
    }

    private AIAgent CreateAgent(string systemPrompt, IList<AITool> mcpTools)
    {
        var endpoint = _configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
        var deploymentName = _configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4o";
        var apiKey = _configuration["AzureOpenAI:ApiKey"];

        _logger.LogInformation("[Agent] Creating AIAgent: endpoint={Endpoint}, deployment={Deployment}, auth={Auth}, tools={ToolCount}",
            endpoint, deploymentName,
            !string.IsNullOrEmpty(apiKey) ? "ApiKey" : "DefaultAzureCredential",
            mcpTools.Count);
        _logger.LogInformation("[Agent] System prompt ({Length} chars):\n{Prompt}",
            systemPrompt.Length, systemPrompt);

        if (!string.IsNullOrEmpty(apiKey))
        {
            return new AzureOpenAIClient(
                new Uri(endpoint),
                new System.ClientModel.ApiKeyCredential(apiKey))
                .GetChatClient(deploymentName)
                .AsIChatClient()
                .AsAIAgent(
                    instructions: systemPrompt,
                    tools: [.. mcpTools]);
        }

        return new AzureOpenAIClient(
            new Uri(endpoint),
            new DefaultAzureCredential())
            .GetChatClient(deploymentName)
            .AsIChatClient()
            .AsAIAgent(
                instructions: systemPrompt,
                tools: [.. mcpTools]);
    }

    public async Task<List<TranslationToSave>> BatchTranslateAsync(
        List<UntranslatedText> textsToTranslate,
        string targetLanguage,
        List<GlossaryEntry>? glossary = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Agent] BatchTranslateAsync: {Count} texts, targetLanguage={Lang}, glossaryTerms={GlossaryCount}",
            textsToTranslate.Count, targetLanguage, glossary?.Count ?? 0);

        progress?.Report($"Connecting to Azure OpenAI...");

        var mcpTools = await _mcpBridge.GetToolsAsync(cancellationToken);
        var systemPrompt = BuildSystemPrompt(targetLanguage, glossary);
        var agent = CreateAgent(systemPrompt, mcpTools);

        var textsJson = JsonSerializer.Serialize(textsToTranslate.Select(t => new
        {
            t.Id,
            t.SourceText,
            t.Comment,
            t.MaxLength,
            t.Context
        }));

        var userPrompt = $"""
            Translate the following texts to {targetLanguage}. Return ONLY a JSON array of objects with "id", "targetText", and "targetState" (always "translated").

            Texts to translate:
            {textsJson}
            """;

        _logger.LogInformation("[Agent] Sending batch translate prompt ({Length} chars)", userPrompt.Length);
        _logger.LogInformation("[Agent] Prompt:\n{Prompt}", userPrompt);

        progress?.Report($"Translating {textsToTranslate.Count} texts...");

        var sw = Stopwatch.StartNew();
        var response = await agent.RunAsync(userPrompt, cancellationToken: cancellationToken);
        var responseText = response?.ToString() ?? "";

        _logger.LogInformation("[Agent] Batch translate response received in {ElapsedMs}ms ({Length} chars)",
            sw.ElapsedMilliseconds, responseText.Length);
        _logger.LogInformation("[Agent] Response:\n{Response}", responseText);

        progress?.Report("Parsing translations...");

        var translations = ParseTranslationsFromResponse(responseText);

        _logger.LogInformation("[Agent] Parsed {Count} translations from response", translations.Count);
        foreach (var t in translations)
        {
            _logger.LogInformation("[Agent]   Translation: id={Id}, text={Text}",
                t.Id, Truncate(t.TargetText, 80));
        }

        progress?.Report($"Completed: {translations.Count} translations");
        return translations;
    }

    public async IAsyncEnumerable<string> BatchTranslateStreamingAsync(
        List<UntranslatedText> textsToTranslate,
        string targetLanguage,
        List<GlossaryEntry>? glossary = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Agent] BatchTranslateStreamingAsync: {Count} texts, targetLanguage={Lang}",
            textsToTranslate.Count, targetLanguage);

        var mcpTools = await _mcpBridge.GetToolsAsync(cancellationToken);
        var systemPrompt = BuildSystemPrompt(targetLanguage, glossary);
        var agent = CreateAgent(systemPrompt, mcpTools);

        var textsJson = JsonSerializer.Serialize(textsToTranslate.Select(t => new
        {
            t.Id,
            t.SourceText,
            t.Comment,
            t.MaxLength,
            t.Context
        }));

        var userPrompt = $"""
            Translate the following texts to {targetLanguage}. Return ONLY a JSON array of objects with "id", "targetText", and "targetState" (always "translated").

            Texts to translate:
            {textsJson}
            """;

        _logger.LogInformation("[Agent] Streaming batch translate ({Length} chars prompt)", userPrompt.Length);

        var sw = Stopwatch.StartNew();
        var chunkCount = 0;
        await foreach (var update in agent.RunStreamingAsync(userPrompt, cancellationToken: cancellationToken))
        {
            var text = update?.ToString();
            if (!string.IsNullOrEmpty(text))
            {
                chunkCount++;
                yield return text;
            }
        }

        _logger.LogInformation("[Agent] Streaming batch translate completed in {ElapsedMs}ms, {ChunkCount} chunks",
            sw.ElapsedMilliseconds, chunkCount);
    }

    public async Task<AIAgent> CreateChatAgentAsync(string? targetLanguage = null, string? xlfFilePath = null)
    {
        _logger.LogInformation("[Agent] CreateChatAgentAsync(targetLanguage={Lang}, xlfFilePath={Xlf})",
            targetLanguage ?? "(auto)", xlfFilePath ?? "(none)");

        var mcpTools = await _mcpBridge.GetToolsAsync();
        var systemPrompt = BuildChatSystemPrompt(targetLanguage, xlfFilePath);
        var agent = CreateAgent(systemPrompt, mcpTools);

        _logger.LogInformation("[Agent] Chat agent created with {ToolCount} MCP tools", mcpTools.Count);
        return agent;
    }

    private static string BuildChatSystemPrompt(string? targetLanguage, string? xlfFilePath)
    {
        var lang = targetLanguage ?? "the target language of the XLF file";
        var fileContext = !string.IsNullOrEmpty(xlfFilePath)
            ? $"""

            IMPORTANT - Current translation file context:
            - XLF file path: {xlfFilePath}
            - Target language: {lang}
            Always use this file path when calling MCP tools that require a filePath parameter.
            The user has already selected this file in the setup wizard - do NOT ask them for the file path.
            """
            : "";

        return $"""
            You are an expert translation assistant for Microsoft Dynamics 365 Business Central AL extensions.
            You have access to MCP tools for managing XLIFF translation files.

            The MCP server has already been initialized with the user's AL app folder.
            {fileContext}
            Available capabilities via MCP tools:
            - getTextsToTranslate: Get untranslated texts from an XLF file (use filePath above)
            - getTranslatedTextsByState: Get translated texts filtered by state (needs-review, translated, final, signed-off)
            - getTextsByKeyword: Search source/target texts by keyword or regex
            - getTranslatedTextsMap: Get a map of source->target translations
            - getGlossaryTerms: Get glossary terminology for consistent translations (use targetLanguageCode: "{lang}")
            - saveTranslatedTexts: Save translations to the XLF file (use filePath above)
            - refreshXlf: Refresh/sync XLF file with latest AL code changes (use filePath above)
            - createLanguageXlf: Create a new XLF file for a target language

            Translation rules:
            1. Preserve placeholders like %1, %2, %3 exactly as they appear
            2. Respect maxLength constraints
            3. Use context to understand what is being translated (table fields, page actions, etc.)
            4. Use developer comments to understand placeholder meanings
            5. Maintain consistent terminology - use the glossary when available
            6. The target language is: {lang}

            When the user asks you to translate, you should:
            1. First fetch the texts using the appropriate tool with the filePath provided above
            2. Translate them
            3. Save them using saveTranslatedTexts with the same filePath
            4. Report what you did

            Be conversational and helpful. Show the user what you're doing.
            Format your responses with markdown for readability.
            When showing translations, use tables when appropriate.
            """;
    }

    private static string BuildSystemPrompt(string targetLanguage, List<GlossaryEntry>? glossary)
    {
        var prompt = $"""
            You are an expert translator for Microsoft Dynamics 365 Business Central AL extensions.
            Your target language is: {targetLanguage}

            Translation rules:
            1. Preserve placeholders like %1, %2, %3 etc. exactly as they appear in source text
            2. Respect maxLength constraints - translations must not exceed the specified character limit
            3. Use the context field to understand what is being translated (table fields, page actions, etc.)
            4. Use developer comments to understand placeholder meanings
            5. Maintain consistent terminology throughout the translation
            6. Return translations as a JSON array of objects with "id", "targetText", and "targetState" fields
            7. The "targetState" should always be "translated"
            """;

        if (glossary is { Count: > 0 })
        {
            prompt += "\n\nGlossary terms (use these consistently):\n";
            foreach (var entry in glossary)
            {
                prompt += $"- {entry.Source} -> {entry.Target}";
                if (!string.IsNullOrEmpty(entry.Description))
                    prompt += $" ({entry.Description})";
                prompt += "\n";
            }
        }

        return prompt;
    }

    private static List<TranslationToSave> ParseTranslationsFromResponse(string response)
    {
        var jsonStart = response.IndexOf('[');
        var jsonEnd = response.LastIndexOf(']');

        if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
            return [];

        var json = response[jsonStart..(jsonEnd + 1)];

        try
        {
            return JsonSerializer.Deserialize<List<TranslationToSave>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }
}
