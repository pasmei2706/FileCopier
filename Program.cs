using FileCopier.Model;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;

namespace FileCopier;

class Program
{
    private static FileSystemWatcher _watcher;
    private static string _sourcePath;
    private static string _destinationPath;
    private static readonly string _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
    private static bool _useGermanMonths;
    private static int _restartTime;

    static void Main(string[] args)
    {
        // Initialize Serilog logger
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .WriteTo.File("logs/log.txt", outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information("Application started.");

        // Load configuration
        LoadConfiguration();

        #region watcher logic
        // Initialize FileSystemWatcher
        _watcher = new FileSystemWatcher
        {
            Path = _sourcePath,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
        };

        _watcher.Created += OnFileCreatedOrChanged;
        _watcher.Changed += OnFileCreatedOrChanged;
        _watcher.Error += OnWatcherError;
        _watcher.EnableRaisingEvents = true;
        #endregion

        Log.Information($"Watching for files in {_sourcePath} to copy to {_destinationPath}.");

        // Set the time for daily restart
        TimeSpan restartTime = LoadRestartTime();

        // Calculate the delay until the next restart time
        TimeSpan delay = CalculateDelay(restartTime);

        // Create a timer that triggers at the specified time daily
        Timer timer = new Timer(RestartApplication, null, delay, TimeSpan.FromDays(1));

        // Keep the application running
        Thread.Sleep(Timeout.Infinite);
    }

    private static void LoadConfiguration()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                // Load configuration from JSON file
                string json = File.ReadAllText(_configFilePath);
                var config = JsonConvert.DeserializeObject<ServiceConfig>(json) ?? new();
                _sourcePath = config.SourcePath;
                _destinationPath = config.DestinationPath;
                _useGermanMonths = config.UseGermanMonths; // Default to false if not specified

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

    private static void OnFileCreatedOrChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            string fileName = e.Name;
            string sourceFilePath = Path.Combine(_sourcePath, fileName);
            string destinationFilePath = Path.Combine(_destinationPath, fileName);

            if (File.Exists(destinationFilePath) && e.ChangeType != WatcherChangeTypes.Changed)
            {
                Log.Information($"File {fileName} already exists in {_destinationPath}. Skipping copying.");
                return;
            }

            // Retry logic
            const int maxRetries = 5;
            const int delayMs = 1000; // Delay in milliseconds between retries

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    File.Copy(sourceFilePath, destinationFilePath, false);
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

    private static void OnWatcherError(object sender, ErrorEventArgs e)
    {
        Log.Error(e.GetException(), "File system watcher error");
    }

    static TimeSpan CalculateDelay(TimeSpan restartTime)
    {
        DateTime now = DateTime.Now;
        DateTime nextRestart = now.Date.Add(restartTime);

        // If restart time has already passed for today, schedule for tomorrow
        if (now > nextRestart)
        {
            nextRestart = nextRestart.AddDays(1);
        }

        return nextRestart - now;
    }

    static void RestartApplication(object state)
    {
        // Log the restart event
        Log.Information("Restarting application...");

        try
        {
            // Get the path of the current executable
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;

            // Start a new instance of the application
            System.Diagnostics.Process.Start(exePath);

            // Exit the current instance of the application
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            // Log any errors that occur during restart
            Log.Error($"Error occurred during application restart: {ex.Message}");
        }
    }

    static TimeSpan LoadRestartTime()
    {
        // Load the restart time from your configuration
        _ = TimeSpan.TryParse(_restartTime.ToString(), out var result);

        return result;
    }
}
