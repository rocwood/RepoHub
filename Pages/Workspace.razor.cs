using LibGit2Sharp;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Photino.NET;
using System;
using System.Collections.Concurrent;
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
    private const int REFRESH_INTERVAL = 60000; // 60秒
    private bool isRefreshing = false;
    private bool isDisposed = false;

    private class CachedCredentials
    {
        public Credentials Credentials { get; set; }
        public DateTime? ExpiresAt { get; set; }  // null 表示永不过期
    }

    private static readonly ConcurrentDictionary<string, CachedCredentials> _credentialsCache = new();
    private const int CREDENTIALS_CACHE_MINUTES = 30;

    protected override async Task OnInitializedAsync()
    {
        settings = AppSettings.Load();

        // 如果工作区路径列表为空，添加上次使用的路径
        if (settings.WorkspacePaths.Count == 0 && !string.IsNullOrEmpty(settings.LastWorkspacePath))
        {
            settings.WorkspacePaths.Add(settings.LastWorkspacePath);
            settings.Save();
        }

        // 如果列表仍为空，使用当前目录
        if (settings.WorkspacePaths.Count == 0)
        {
            settings.WorkspacePaths.Add(Environment.CurrentDirectory);
            settings.Save();
        }

        workspacePath = settings.LastWorkspacePath;
        if (string.IsNullOrEmpty(workspacePath) && settings.WorkspacePaths.Any())
        {
            workspacePath = settings.WorkspacePaths[0];
        }

        if (!string.IsNullOrEmpty(workspacePath))
            await LoadWorkspace(true);

        // 初始化定时器
        refreshTimer = new Timer(REFRESH_INTERVAL);
        refreshTimer.Elapsed += async (sender, e) => {
            await InvokeAsync(async () => {
                if (!string.IsNullOrEmpty(workspacePath))
                    await RefreshWorkspace(true);
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
            await InvokeAsync(async () => {
                await RefreshWorkspace(false);
                StateHasChanged();
            });
        }
    }

    private async Task RefreshWorkspace(bool fetchRemote)
    {
        if (isRefreshing) return;

        try
        {
            isRefreshing = true;
            await LoadWorkspace(fetchRemote);
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

    protected async Task LoadWorkspace(bool fetchRemote)
    {
        try
        {
            errorMessage = "";
            if (string.IsNullOrEmpty(workspacePath) || !Directory.Exists(workspacePath))
            {
                errorMessage = "请指定有效的工作区路径";
                repositories = new List<RepoStatus>();  // 清空列表
                return;
            }

            await ScanRepositories(workspacePath, fetchRemote);
            await SaveWorkspacePath(workspacePath);
        }
        catch (Exception ex)
        {
            errorMessage = $"加载工作区时出错: {ex.Message}";
            repositories = new List<RepoStatus>();  // 发生错误时也清空列表
        }
        finally
        {
            StateHasChanged();  // 确保UI更新
        }
    }

    protected async Task ScanRepositories(string path, bool fetchRemote)
    {
        try
        {
            // 在开始加载之前清空列表
            repositories = new List<RepoStatus>();

            // 只扫描顶层目录
            foreach (var dir in Directory.GetDirectories(path))
            {
                // 跳过.git目录
                if (Path.GetFileName(dir) == ".git")
                    continue;

                if (Repository.IsValid(dir))
                {
                    var repo = new RepoStatus { Path = dir };
                    repositories.Add(repo);

                    using (var gitRepo = new Repository(dir))
                        UpdateRepositoryStatus(repo, gitRepo);

                    if (fetchRemote)
                        _ = FetchRepository(repo);
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"扫描仓库时出错: {ex.Message}";
        }
    }

    private static Credentials GetCredentialsProvider(string url, string usernameFromUrl, SupportedCredentialTypes types)
    {
        var cacheKey = $"{url}:{usernameFromUrl}";

        // 检查缓存
        if (_credentialsCache.TryGetValue(cacheKey, out var cached))
        {
            if (!cached.ExpiresAt.HasValue || DateTime.Now < cached.ExpiresAt.Value)
                return cached.Credentials;

            // 过期则移除
            _credentialsCache.TryRemove(cacheKey, out _);
        }

        try
        {
            // 尝试从 git config 获取凭据
            var startInfo = new ProcessStartInfo {
                FileName = "git",
                Arguments = "config --get credential.helper",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            var process = Process.Start(startInfo);
            var output = process?.StandardOutput.ReadToEnd()?.Trim();
            process?.WaitForExit();

            if (output?.Contains("manager") == true)
            {
                // 使用 git credential manager 获取凭据
                startInfo = new ProcessStartInfo {
                    FileName = "git",
                    Arguments = $"credential fill",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                process = Process.Start(startInfo);
                if (process != null)
                {
                    process.StandardInput.WriteLine($"url={url}");
                    process.StandardInput.WriteLine();
                    process.StandardInput.Close();

                    var credentials = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    var username = credentials.Split('\n')
                        .FirstOrDefault(l => l.StartsWith("username="))
                        ?.Replace("username=", "");
                    var password = credentials.Split('\n')
                        .FirstOrDefault(l => l.StartsWith("password="))
                        ?.Replace("password=", "");

                    if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                    {
                        var creds = new UsernamePasswordCredentials {
                            Username = username,
                            Password = password
                        };

                        // 添加到缓存，成功获取的凭据永不过期
                        _credentialsCache.TryAdd(cacheKey, new CachedCredentials {
                            Credentials = creds,
                            ExpiresAt = null  // 永不过期
                        });

                        return creds;
                    }
                }
            }
        }
        catch
        {
        }

        // 如果出现任何错误，返回默认凭据，并设置过期时间
        var defaultCreds = new DefaultCredentials();

        // 缓存默认凭据，30分钟后过期
        _credentialsCache.TryAdd(cacheKey, new CachedCredentials {
            Credentials = defaultCreds,
            ExpiresAt = DateTime.Now.AddMinutes(CREDENTIALS_CACHE_MINUTES)
        });

        return defaultCreds;
    }

    private async Task FetchRepository(RepoStatus repo)
    {
        if (repo == null)
            return;

        try
        {
            repo.IsFetching = true;
            StateHasChanged();

            // 后台运行获取远程状态
            await Task.Run(async () => {
                try
                {
                    using var gitRepo = new Repository(repo.Path);
                    foreach (var remote in gitRepo.Network.Remotes)
                    {
                        try
                        {
                            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification).ToList();
                            var fetchOptions = new FetchOptions {
                                Prune = false,
                                TagFetchMode = TagFetchMode.Auto,
                                CredentialsProvider = GetCredentialsProvider,
                            };

                            Commands.Fetch(gitRepo, remote.Name, refSpecs, fetchOptions, $"正在从 {remote.Name} 获取更新");
                        }
                        catch (Exception ex)
                        {
                            await InvokeAsync(() =>
                                Snackbar.Add($"仓库 {Path.GetFileName(repo.Path)} 从远程 {remote.Name} fetch 时出错: {ex.Message}", Severity.Warning));
                        }
                    }

                    await InvokeAsync(() => UpdateRepositoryStatus(repo, gitRepo));
                }
                catch (Exception ex)
                {
                    await InvokeAsync(() =>
                        Snackbar.Add($"仓库 {Path.GetFileName(repo.Path)} fetch 时出错: {ex.Message}", Severity.Warning));
                }
            });
        }
        finally
        {
            repo.IsFetching = false;
            StateHasChanged();
        }
    }

    private void UpdateRepositoryStatus(RepoStatus repo, Repository gitRepo)
    {
        var status = gitRepo.RetrieveStatus();
        var branch = gitRepo.Head;
        var tracking = branch.TrackingDetails;

        repo.Branch = branch.FriendlyName;
        repo.Branches = gitRepo.Branches
            .Where(b => !b.IsRemote || settings.ShowRemoteBranches)
            .Where(b => b.FriendlyName != "HEAD")
            .Select(b => b.FriendlyName)
            .ToList();

        repo.LastCommit = new CommitInfo {
            Sha = branch.Tip.Sha,
            Message = branch.Tip.Message,
            Author = branch.Tip.Author.Name,
            Time = branch.Tip.Author.When
        };

        repo.ChangesCount = status.Staged.Count() + status.Added.Count() + status.Modified.Count() + status.Removed.Count();
        repo.AheadCount = tracking?.AheadBy ?? 0;
        repo.BehindCount = tracking?.BehindBy ?? 0;
    }

    protected string GetRelativePath(string fullPath)
    {
        return Path.GetRelativePath(workspacePath, fullPath);
    }

    protected async Task OpenFolderDialog()
    {
        try
        {
            var folders = await Window.ShowOpenFolderAsync("选择工作区路径");
            if (folders == null || folders.Length == 0)
                return;

            var folderPath = folders[0];

            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                if (!settings.WorkspacePaths.Contains(folderPath))
                {
                    settings.WorkspacePaths.Add(folderPath);
                    settings.LastWorkspacePath = folderPath;
                    settings.Save();

                    workspacePath = folderPath;
                    await LoadWorkspace(true);
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"选择目录时出错: {ex.Message}";
            Snackbar.Add(errorMessage, Severity.Error);
        }
    }

    protected async Task DeleteCurrentWorkspace()
    {
        if (settings.WorkspacePaths.Count <= 1)
        {
            Snackbar.Add("必须保留至少一个工作区路径", Severity.Warning);
            return;
        }

        var options = new DialogOptions { CloseOnEscapeKey = true, CloseButton = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var parameters = new DialogParameters
        {
            { "Title", "移除工作区" },
            { "ContentText", "确定要从列表中移除此工作区吗？\n这不会删除磁盘上的文件。" },
            { "AdditionalInfo", $"工作区路径：{workspacePath}" },
            { "Color", Color.Warning },
			{ "Actions", new List<DialogAction>
				{
					new()
					{
						Text = "移除",
						Value = 1,
						Color = Color.Warning,
						Variant = Variant.Filled
					},
				}
			}
		};

        var dialog = await DialogService.ShowAsync<MessageBox>("移除确认", parameters, options);
        var result = await dialog.Result;
		if (result.Canceled)
			return;

        settings.WorkspacePaths.Remove(workspacePath);

        // 切换到列表中的第一个工作区
        workspacePath = settings.WorkspacePaths[0];
        settings.LastWorkspacePath = workspacePath;
        settings.Save();

        await LoadWorkspace(true);
        Snackbar.Add("工作区已从列表中移除", Severity.Success);
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

            var startInfo = new ProcessStartInfo {
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
            using var repository = new Repository(repo.Path);
            var resetType = (GitResetType)result.Data == GitResetType.Soft ? ResetMode.Soft : ResetMode.Hard;

            // 获取当前分支的最新提交
            var targetCommit = repository.Head.Tip;

            // 执行重置
            repository.Reset(resetType, targetCommit);

            var message = resetType == ResetMode.Soft ?
                "已软重置，工作区的修改已保留" :
                "已硬重置，所有修改已丢弃";

            Snackbar.Add(message, Severity.Success);
            await RefreshWorkspace(false);
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

    protected async Task OnWorkspacePathChanged(string newPath)
    {
        if (workspacePath != newPath)
        {
            workspacePath = newPath;
            settings.LastWorkspacePath = newPath;
            settings.Save();

            // 确保在UI线程上执行加载
            await InvokeAsync(async () => {
                await LoadWorkspace(true);
                StateHasChanged();
            });
        }
    }
}
