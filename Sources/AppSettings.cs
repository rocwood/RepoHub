using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RepoHub;

public class GitClientConfig
{
    public string Name { get; set; }
    public string Path { get; set; }
    public string PullCommand { get; set; }
    public bool IsEnabled { get; set; }
}

public class AppSettings
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RepoHub",
        "settings.json"
    );

    public string LastWorkspacePath { get; set; } = "";
    
    // Git客户端配置
    public List<GitClientConfig> GitClients { get; set; } = new()
    {
        new GitClientConfig 
        { 
            Name = "TortoiseGit", 
            Path = @"C:\Program Files\TortoiseGit\bin\TortoiseGitProc.exe",
            PullCommand = "/command:pull /path:\"{0}\"",
            IsEnabled = true
        },
		new GitClientConfig
		{
			Name = "SourceTree",
			Path = @"C:\Users\%USERNAME%\AppData\Local\SourceTree\SourceTree.exe",
			PullCommand = "-f pull \"{0}\"",
			IsEnabled = false
		},
		new GitClientConfig
		{
			Name = "GitExtensions",
			Path = @"C:\Program Files (x86)\GitExtensions\GitExtensions.exe",
			PullCommand = "pull \"{0}\"",
			IsEnabled = false
		},
		new GitClientConfig 
        { 
            Name = "Fork", 
            Path = @"C:\Users\%USERNAME%\AppData\Local\Fork\Fork.exe",
            PullCommand = "pull \"{0}\"",
            IsEnabled = false
        },
    };

    public static AppSettings Load()
    {
        try
        {
            var configDir = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                
                // 确保默认客户端列表完整
                foreach (var defaultClient in new AppSettings().GitClients)
                {
                    if (!settings.GitClients.Any(c => c.Name == defaultClient.Name))
                    {
                        settings.GitClients.Add(defaultClient);
                    }
                }
                return settings;
            }
        }
        catch
        {
            // 如果出现任何错误，返回默认设置
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // 忽略保存错误
        }
    }
} 
