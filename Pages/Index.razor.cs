using ArkSharp;
using Microsoft.AspNetCore.Components;
using System;
using System.IO;

namespace RepoHub;

public partial class Index
{
    [Inject] private NavigationManager NavigationManager { get; set; }

    // 用于判断是否是程序启动后的首次访问
    private static bool isFirstVisit = true;

    protected override void OnInitialized()
    {
        // 只在首次访问时执行自动跳转
        if (isFirstVisit)
        {
            isFirstVisit = false;

            var settings = AppSettings.Load();

            // 先检查是否指定启动路径
            var args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 1)
            {
                var argPath = args[1];

                if (!string.IsNullOrEmpty(argPath))
                {
                    // 由于命令行参数可能传入"路径\"，可能被错误解释保留了最后的双引号
                    if (argPath.EndsWith('"'))
                        argPath = argPath.Substring(0, argPath.Length - 1);

                    if (argPath.EndsWith('/') || argPath.EndsWith('\\'))
                        argPath = argPath.Substring(0, argPath.Length - 1);

                    if (Directory.Exists(argPath))
                    {
                        settings.WorkspacePaths.AddUnique(argPath);
                        settings.LastWorkspacePath = argPath;
                        settings.Save();
                    }
                }
            }

            if (!string.IsNullOrEmpty(settings.LastWorkspacePath) &&
                Directory.Exists(settings.LastWorkspacePath))
            {
                NavigationManager.NavigateTo("workspace");
            }
        }
    }
}
