namespace RepoHub;

public class GitClientConfig
{
    public string Name { get; set; }
    public string Path { get; set; }
    public string PullCommand { get; set; }
    public string PushCommand { get; set; }
    public string CommitCommand { get; set; }
    public bool IsEnabled { get; set; }
}
