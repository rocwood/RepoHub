using System;
using System.Collections.Generic;

namespace RepoHub;

public class RepoStatus
{
    public string Path { get; set; }
    public string Branch { get; set; }
    public List<string> Branches { get; set; } = new();
    public CommitInfo LastCommit { get; set; }
    public int ChangesCount { get; set; }
    public int AheadCount { get; set; }
    public int BehindCount { get; set; }
}

public class CommitInfo
{
    public string Sha { get; set; }
    public string Message { get; set; }
    public string Author { get; set; }
    public DateTimeOffset Time { get; set; }
} 
