using Newtonsoft.Json;
using Serilog;
using Serilog.Events;

namespace FileCopier;

public class Worker : BackgroundService
{
    private FileSystemWatcher _watcher;
    private string _sourcePath;
    private string _destinationPath;
    private readonly string _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
    private bool _useGermanMonths;

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Initialize Serilog logger
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .WriteTo.File("logs/log.txt", outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}", rollingInterval: RollingInterval.Day)
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("Worker started.");

            // Load configuration
            LoadConfiguration();

            // Check if source directory exists
            if (!Directory.Exists(_sourcePath))
            {
                Log.Error($"Source directory {_sourcePath} does not exist.");
                return Task.CompletedTask; // Exit method if source directory does not exist
            }

            // Initialize FileSystemWatcher
            _watcher = new FileSystemWatcher
            {
                Path = _sourcePath,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
            };
            _watcher.Created += OnFileCreated;
            _watcher.Error += OnWatcherError;
            _watcher.EnableRaisingEvents = true;

            Log.Information($"Watching for files in {_sourcePath} to copy to {_destinationPath}.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error starting worker.");
        }
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        Log.Information("Worker stopped.");

        return base.StopAsync(cancellationToken);
    }

    private void LoadConfiguration()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                // Load configuration from JSON file
                string json = File.ReadAllText(_configFilePath);
                dynamic config = JsonConvert.DeserializeObject(json);
                _sourcePath = Path.GetFullPath(config["SourcePath"]);
                _destinationPath = Path.GetFullPath(config["DestinationPath"]);
                _useGermanMonths = config["UseGermanMonths"] ?? false; // Default to false if not specified

                // If using German months, update source folder path
                if (_useGermanMonths)
                {
                    string currentYear = DateTime.Now.Year.ToString();
                    string germanMonth = DateTime.Now.ToString("MMMM", new System.Globalization.CultureInfo("de-DE"));
                    _sourcePath = Path.Combine(_sourcePath, currentYear, germanMonth);
                }
            }
            else
            {
                // Create an empty JSON configuration file
                var defaultConfig = new { SourcePath = string.Empty, DestinationPath = string.Empty, UseGermanMonths = false };
                File.WriteAllText(_configFilePath, JsonConvert.SerializeObject(defaultConfig));

                Log.Warning($"Config file not found. An empty JSON configuration file has been created at {_configFilePath}.");
            }

            // Create source directory if it doesn't exist
            if (!Directory.Exists(_sourcePath))
            {
                try
                {
                    Directory.CreateDirectory(_sourcePath);
                    Log.Information($"Source directory {_sourcePath} created.");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error creating source directory");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in configuration.");
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            string fileName = e.Name;
            string sourceFilePath = Path.Combine(_sourcePath, fileName);
            string destinationFilePath = Path.Combine(_destinationPath, fileName);

            if (File.Exists(destinationFilePath))
            {
                Log.Information($"File {fileName} already exists in {_destinationPath}. Skipping copying.");
                return;
            }

            // Retry logic
            const int maxRetries = 5;
            const int delayMs = 500; // Delay in milliseconds between retries

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    File.Copy(sourceFilePath, destinationFilePath);
                    Log.Information($"Copied file {fileName} to {_destinationPath}.");
                    return; // Exit the loop if copy is successful
                }
                catch (IOException ex) when (i < maxRetries - 1)
                {
                    Log.Warning($"Error copying file: {ex.Message}. Retrying...");
                    Thread.Sleep(delayMs); // Wait before retrying
                }
            }

            // If all retries fail, log an error
            Log.Error($"Failed to copy file {fileName} after {maxRetries} attempts.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error copying file");
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        Log.Error(e.GetException(), "File system watcher error");
    }
}
