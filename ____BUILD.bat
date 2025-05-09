dotnet publish -c Release -r win-x64
xcopy /i /s /y bin\Release\net8.0\win-x64\publish\*.* ..\RepoHub-Release
@pause
