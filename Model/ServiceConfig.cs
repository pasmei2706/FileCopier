namespace FileCopier.Model;

public class ServiceConfig
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public bool UseGermanMonths { get; set; } = false;
    public int RestartTime { get; set; } = 4;
}