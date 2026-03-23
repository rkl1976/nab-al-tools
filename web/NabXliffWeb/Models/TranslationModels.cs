using System.Text.Json.Serialization;

namespace NabXliffWeb.Models;

public record UntranslatedText(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("sourceText")] string SourceText,
    [property: JsonPropertyName("sourceLanguage")] string SourceLanguage,
    [property: JsonPropertyName("comment")] string? Comment,
    [property: JsonPropertyName("maxLength")] int? MaxLength,
    [property: JsonPropertyName("context")] string Context
);

public record UntranslatedTextsResult(
    [property: JsonPropertyName("totalUntranslatedCount")] int TotalUntranslatedCount,
    [property: JsonPropertyName("returnedCount")] int ReturnedCount,
    [property: JsonPropertyName("texts")] List<UntranslatedText> Texts
);

public record TranslatedTextWithState(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("sourceText")] string SourceText,
    [property: JsonPropertyName("sourceLanguage")] string SourceLanguage,
    [property: JsonPropertyName("targetText")] string TargetText,
    [property: JsonPropertyName("alternativeTranslations")] string[]? AlternativeTranslations,
    [property: JsonPropertyName("comment")] string? Comment,
    [property: JsonPropertyName("translationState")] string? TranslationState,
    [property: JsonPropertyName("reviewReason")] string? ReviewReason,
    [property: JsonPropertyName("maxLength")] int? MaxLength,
    [property: JsonPropertyName("context")] string Context
);

public record TranslationToSave(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("targetText")] string TargetText,
    [property: JsonPropertyName("targetState")] string? TargetState
);

public record GlossaryEntry(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("description")] string Description
);

public record TranslatedText(
    [property: JsonPropertyName("sourceText")] string SourceText,
    [property: JsonPropertyName("targetTexts")] string[] TargetTexts,
    [property: JsonPropertyName("sourceLanguage")] string SourceLanguage
);

public record TextsByKeywordResult(
    List<TranslatedTextWithState> Texts,
    int TotalCount
);
