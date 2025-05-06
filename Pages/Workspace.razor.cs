using LibGit2Sharp;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using MudBlazor;

namespace RepoHub;

public partial class Workspace : ComponentBase, IDisposable
{
    [Inject] private ISnackbar Snackbar { get; set; }
	[Inject] private NavigationManager NavigationManager { get; set; }

	protected string workspacePath = "";
    protected string errorMessage = "";
    protected List<RepoStatus> repositories;
    protected AppSettings settings;
    private Timer refreshTimer;
    private const int REFRESH_INTERVAL = 30000; // 30秒

    protected override async Task OnInitializedAsync()
    {
        settings = AppSettings.Load();
        workspacePath = settings.LastWorkspacePath;
        
        if (string.IsNullOrEmpty(workspacePath))
        {
            // 如果没有保存的路径，使用当前目录
            workspacePath = Environment.CurrentDirectory;
        }

        if (!string.IsNullOrEmpty(workspacePath))
        {
            await LoadWorkspace();
        }

        // 初始化定时器
        refreshTimer = new Timer(REFRESH_INTERVAL);
        refreshTimer.Elapsed += async (sender, e) =>
        {
            await InvokeAsync(async () =>
            {
                if (!string.IsNullOrEmpty(workspacePath))
                {
                    await LoadWorkspace();
                    StateHasChanged();
                }
            });
        };
        refreshTimer.Start();
    }

    public void Dispose()
    {
        refreshTimer?.Stop();
        refreshTimer?.Dispose();
    }

    protected async Task LoadWorkspace()
    {
        try
        {
            errorMessage = "";
            if (string.IsNullOrEmpty(workspacePath) || !Directory.Exists(workspacePath))
            {
                errorMessage = "请指定有效的工作区路径";
                return;
            }

            repositories = new List<RepoStatus>();
            await ScanRepositories(workspacePath);
            await SaveWorkspacePath(workspacePath);
        }
        catch (Exception ex)
        {
            errorMessage = $"加载工作区时出错: {ex.Message}";
        }
    }

    protected async Task ScanRepositories(string path)
    {
        try
        {
            // 只扫描顶层目录
            foreach (var dir in Directory.GetDirectories(path))
            {
                // 跳过.git目录
                if (Path.GetFileName(dir) == ".git")
                    continue;

                if (Repository.IsValid(dir))
                {
                    using var repo = new Repository(dir);
                    var status = repo.RetrieveStatus();
                    var branch = repo.Head;
                    var tracking = branch.TrackingDetails;

                    repositories.Add(new RepoStatus
                    {
                        Path = dir,
                        Branch = branch.FriendlyName,
                        Branches = repo.Branches.Select(b => b.FriendlyName).ToList(),
                        LastCommit = new CommitInfo
                        {
                            Sha = branch.Tip.Sha,
                            Message = branch.Tip.Message,
                            Author = branch.Tip.Author.Name,
                            Time = branch.Tip.Author.When
                        },
                        ChangesCount = status.Added.Count() + status.Modified.Count() + status.Removed.Count(),
                        AheadCount = tracking?.AheadBy ?? 0,
                        BehindCount = tracking?.BehindBy ?? 0
                    });
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"扫描仓库时出错: {ex.Message}";
        }
    }

    protected string GetRelativePath(string fullPath)
    {
        return Path.GetRelativePath(workspacePath, fullPath);
    }

    protected async Task OpenFolderDialog()
    {
        // 这里需要实现文件夹选择对话框
        // 可以使用JS互操作或其他方式实现
    }

    protected async Task DoCommit(RepoStatus repo)
    {
        // 实现提交更改逻辑
    }

    protected async Task DoPull(RepoStatus repo)
    {
        try
        {
            // 重新加载设置以获取最新配置
            settings = AppSettings.Load();
            
            var enabledClients = settings.GitClients
                .Where(c => c.IsEnabled)
                .Select(c => new { 
                    Config = c, 
                    ActualPath = c.Path.Replace("%USERNAME%", Environment.UserName)
                })
                .Where(x => File.Exists(x.ActualPath))
                .ToList();
            
            if (!enabledClients.Any())
            {
                if (!settings.GitClients.Any(c => c.IsEnabled))
                {
                    Snackbar.Add("请在设置中启用至少一个Git客户端", Severity.Warning);
                }
                else
                {
                    Snackbar.Add("已启用的Git客户端路径无效，请检查设置", Severity.Warning);
                }
                NavigationManager.NavigateTo("settings");
                return;
            }

            var client = enabledClients.First();
            var processArgs = string.Format(client.Config.PullCommand, repo.Path);

            var startInfo = new ProcessStartInfo
            {
                FileName = client.ActualPath,
                Arguments = processArgs,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            Snackbar.Add($"已启动 {client.Config.Name} 执行拉取操作", Severity.Success);
        }
        catch (Exception ex)
        {
            errorMessage = $"启动Git客户端时出错: {ex.Message}";
            Snackbar.Add(errorMessage, Severity.Error);
        }
    }

    protected async Task DoPush(RepoStatus repo)
    {
        // 实现推送更改逻辑
    }

    protected async Task DoReset(RepoStatus repo)
    {
        // 实现重置更改逻辑
    }

    protected async Task<string> LoadLastWorkspacePath()
    {
        return settings.LastWorkspacePath;
    }

    protected async Task SaveWorkspacePath(string path)
    {
        settings.LastWorkspacePath = path;
        settings.Save();
    }

    protected async Task SwitchBranch(RepoStatus repo, string newBranch)
    {
        try
        {
            using var repository = new Repository(repo.Path);
            var branch = repository.Branches[newBranch];
            if (branch != null)
            {
                Commands.Checkout(repository, branch);
                repo.Branch = newBranch;
                
                // 更新状态
                var status = repository.RetrieveStatus();
                var tracking = branch.TrackingDetails;
                repo.ChangesCount = status.Added.Count() + status.Modified.Count() + status.Removed.Count();
                repo.AheadCount = tracking?.AheadBy ?? 0;
                repo.BehindCount = tracking?.BehindBy ?? 0;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"切换分支时出错: {ex.Message}";
        }
    }
} 
