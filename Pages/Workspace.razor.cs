using LibGit2Sharp;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Photino.NET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace RepoHub;

public partial class Workspace : ComponentBase, IDisposable
{
    [Inject] private ISnackbar Snackbar { get; set; }
    [Inject] private NavigationManager NavigationManager { get; set; }
    [Inject] private PhotinoWindow Window { get; set; }
    [Inject] private IJSRuntime JSRuntime { get; set; }
    [Inject] private IDialogService DialogService { get; set; }

    protected string workspacePath = "";
    protected string errorMessage = "";
    protected List<RepoStatus> repositories;
    protected AppSettings settings;
    private Timer refreshTimer;
    private const int REFRESH_INTERVAL = 30000; // 30秒
    private bool isRefreshing = false;
    private bool isDisposed = false;

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
                    await RefreshWorkspace();
                }
            });
        };
        refreshTimer.Start();

        // 注册窗口激活事件
        if (Window != null)
            Window.WindowFocusIn += OnWindowActivated;
    }

    private async void OnWindowActivated(object sender, EventArgs e)
    {
        if (!isDisposed && !string.IsNullOrEmpty(workspacePath))
        {
            await InvokeAsync(async () =>
            {
                await RefreshWorkspace();
                StateHasChanged();
            });
        }
    }

    private async Task RefreshWorkspace()
    {
        if (isRefreshing) return;

        try
        {
            isRefreshing = true;
            await LoadWorkspace();
        }
        finally
        {
            isRefreshing = false;
        }
    }

    public void Dispose()
    {
        isDisposed = true;
        refreshTimer?.Stop();
        refreshTimer?.Dispose();

        if (Window != null)
            Window.WindowFocusIn -= OnWindowActivated;
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

                    var branches = repo.Branches
                        .Where(b => !b.IsRemote || settings.ShowRemoteBranches) // 根据设置显示远程分支
                        .Where(b => b.FriendlyName != "HEAD") // 排除 HEAD 分支
                        .Select(b => b.FriendlyName)
                        .ToList();

                    repositories.Add(new RepoStatus
                    {
                        Path = dir,
                        Branch = branch.FriendlyName,
                        Branches = branches,
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
        await ExecuteGitClientOperation(repo, "提交", config => config.CommitCommand);
    }

    private async Task ExecuteGitClientOperation(RepoStatus repo, string operationType, Func<GitClientConfig, string> getCommand)
    {
        try
        {
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
            var processArgs = string.Format(getCommand(client.Config), repo.Path);

            var startInfo = new ProcessStartInfo
            {
                FileName = client.ActualPath,
                Arguments = processArgs,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            Snackbar.Add($"已启动 {client.Config.Name} 执行{operationType}操作", Severity.Success);
        }
        catch (Exception ex)
        {
            errorMessage = $"启动Git客户端时出错: {ex.Message}";
            Snackbar.Add(errorMessage, Severity.Error);
        }
	}

    protected async Task DoPull(RepoStatus repo)
    {
        await ExecuteGitClientOperation(repo, "拉取", config => config.PullCommand);
    }

    protected async Task DoPush(RepoStatus repo)
    {
        await ExecuteGitClientOperation(repo, "推送", config => config.PushCommand);
    }

    protected async Task DoReset(RepoStatus repo)
    {
        var options = new DialogOptions { CloseOnEscapeKey = true, CloseButton = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var parameters = new DialogParameters
        {
            { "Title", "重置确认" },
            { "ContentText", "请选择重置类型：\n\n" +
                "【软重置】\n-  保留所有修改。\n-  已提交的更改保持在暂存区\n\n" +
                "【硬重置】\n-  丢弃所有未提交修改。\n-  将工作区和暂存区恢复到最新提交状态。\n-  此操作不可撤销，请谨慎使用！" },
            { "AdditionalInfo", $"仓库：{GetRelativePath(repo.Path)}  分支：{repo.Branch}\n" },
            { "Color", Color.Warning },
            { "Severity", Severity.Warning },
            { "Actions", new List<DialogAction>
                {
                    new()
                    {
                        Text = "软重置",
                        Value = GitResetType.Soft,
                        Color = Color.Warning,
                        Variant = Variant.Filled
                    },
                    new()
                    {
                        Text = "硬重置(危险)",
                        Value = GitResetType.Hard,
                        Color = Color.Error,
                        Variant = Variant.Filled
                    },
                }
            }
        };

        var dialog = await DialogService.ShowAsync<MessageBox>("重置确认", parameters, options);
        var result = await dialog.Result;
        if (result.Canceled)
            return;

        try
        {
            settings = AppSettings.Load();
            var resetTypeArg = ((GitResetType)result.Data == GitResetType.Soft) ? "--soft" : "--hard";
            var resetTarget = "HEAD";
            
            var startInfo = new ProcessStartInfo
            {
                FileName = settings.GitExecutablePath,
                Arguments = $"-C \"{repo.Path}\" reset {resetTypeArg} {resetTarget}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = Process.Start(startInfo);
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                var message = ((GitResetType)result.Data == GitResetType.Soft) ? 
                    "已软重置，工作区的修改已保留" : 
                    "已硬重置，所有修改已丢弃";
                Snackbar.Add(message, Severity.Success);
                await RefreshWorkspace();
            }
            else
            {
                Snackbar.Add($"重置失败: {error}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"执行重置命令时出错: {ex.Message}";
            Snackbar.Add(errorMessage, Severity.Error);
        }
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
            
            // 如果是远程分支，创建本地跟踪分支
            if (branch != null && branch.IsRemote)
            {
                var localBranchName = branch.FriendlyName.Replace(branch.RemoteName + "/", "");
                var localBranch = repository.Branches[localBranchName];
                
                if (localBranch == null)
                {
                    // 创建本地跟踪分支
                    localBranch = repository.CreateBranch(localBranchName, branch.Tip);
                    repository.Branches.Update(localBranch, 
                        b => b.TrackedBranch = branch.CanonicalName);
                }
                
                branch = localBranch;
            }

            if (branch != null)
            {
                Commands.Checkout(repository, branch);
                repo.Branch = branch.FriendlyName;
                
                // 更新状态
                var status = repository.RetrieveStatus();
                var tracking = branch.TrackingDetails;
                repo.ChangesCount = status.Added.Count() + status.Modified.Count() + status.Removed.Count();
                repo.AheadCount = tracking?.AheadBy ?? 0;
                repo.BehindCount = tracking?.BehindBy ?? 0;

                Snackbar.Add($"已切换到分支: {branch.FriendlyName}", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"切换分支时出错: {ex.Message}";
            Snackbar.Add(errorMessage, Severity.Error);
        }
    }
} 
