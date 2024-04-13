using System.IO;

namespace FileCopier.Model;

public class ServiceConfig
{
    public string ServiceName { get; set; } = "FileCopierService";
    public string ServiceDisplayName { get; set; } = "File Copier Service";
    public string ServiceDescription { get; set; } = "A service that copies files from source to destination.";
    public string LogLocation { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), nameof(ServiceDisplayName), "logs/log.txt");
    public string SourcePath { get; set; }
    public string DestinationPath { get; set; }
    public bool UseGermanMonths { get; set; } = false;
    public int RestartTime { get; set; } = 4;
}