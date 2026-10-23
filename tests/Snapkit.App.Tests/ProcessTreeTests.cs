using System.Diagnostics;
using Snapkit.App.Interop;

namespace Snapkit.App.Tests;

public class ProcessTreeTests
{
    [Fact]
    public void ChildProcess_BelongsToParent_ButNotViceVersa()
    {
        // У Claude Desktop и Chrome окно — у главного процесса, а читать буфер может дочерний
        using var child = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 5 127.0.0.1 > nul")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        })!;
        try
        {
            Assert.True(ProcessTree.IsSameOrDescendant(child.Id, Environment.ProcessId));
            Assert.True(ProcessTree.IsSameOrDescendant(Environment.ProcessId, Environment.ProcessId));
            Assert.False(ProcessTree.IsSameOrDescendant(Environment.ProcessId, child.Id));
            Assert.False(ProcessTree.IsSameOrDescendant(0, Environment.ProcessId));
        }
        finally
        {
            child.Kill(entireProcessTree: true);
        }
    }
}
