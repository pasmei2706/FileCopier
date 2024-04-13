using FileCopier.Model;
using Newtonsoft.Json;
using Serilog;
using System;
using System.IO;

namespace FileCopier.Configuration;

public static class ConfigManager
{
    public static ServiceConfig LoadConfig(string filePath)
    {
        try
        {
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<ServiceConfig>(json);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in loading config.");
            throw;
        }
    }
}