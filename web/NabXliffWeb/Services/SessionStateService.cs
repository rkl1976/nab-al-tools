namespace NabXliffWeb.Services;

public class SessionStateService
{
    public string? AppFolderPath { get; set; }
    public string? WorkspaceFilePath { get; set; }
    public string? XlfFilePath { get; set; }
    public string? TargetLanguage { get; set; }
    public int PageOffset { get; set; }
    public int PageSize { get; set; } = 50;
    public string? StateFilter { get; set; }
    public string? SearchKeyword { get; set; }
    public bool SearchInTarget { get; set; }
    public List<string> AvailableXlfFiles { get; set; } = [];
}
