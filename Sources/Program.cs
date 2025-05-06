using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using MudBlazor.Services;

namespace RepoHub
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);

            appBuilder.Services
                .AddLogging()
                .AddMudServices(config =>
                {
                    // 设置 Snackbar 显示时长为 1 秒，显隐过渡时长为 0.4 秒
                    config.SnackbarConfiguration.VisibleStateDuration = 1000;
                    config.SnackbarConfiguration.ShowTransitionDuration = 400;
                    config.SnackbarConfiguration.HideTransitionDuration = 400;
                });

            // register root component and selector
            appBuilder.RootComponents.Add<App>("app");

            var app = appBuilder.Build();

            // customize window
            app.MainWindow
                .SetIconFile("favicon.ico")
                .SetTitle("RepoHub")
                .SetWidth(1280)         // 设置默认宽度
                .SetHeight(800)         // 设置默认高度
                .Center();              // 窗口居中显示

            AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
            {
                app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString());
            };

            app.Run();
        }
    }
}
