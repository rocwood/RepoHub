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
            if (!string.IsNullOrEmpty(settings.LastWorkspacePath) && 
                Directory.Exists(settings.LastWorkspacePath))
            {
                NavigationManager.NavigateTo("workspace");
            }
        }
    }
} 
