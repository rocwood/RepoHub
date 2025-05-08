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

public enum GitResetType
{
	Soft = 1,    // 软重置：保留工作区和暂存区的修改
	Hard = 2,    // 硬重置：丢弃所有修改，包括工作区和暂存区
}
