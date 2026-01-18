using System;
using System.Diagnostics;
using System.IO;
using System.Text;

public class GitRepoUpdater
{
    public string RepoPath { get; }
    public string Branch { get; }
    public string PrivateKeyPath { get; }

    public GitRepoUpdater(string repoPath, string privateKeyPath, string branch = "main")
    {
        if (!Directory.Exists(repoPath))
            throw new DirectoryNotFoundException(repoPath);
        if (!File.Exists(privateKeyPath))
            throw new FileNotFoundException("Private SSH key not found.", privateKeyPath);

        RepoPath = repoPath;
        PrivateKeyPath = privateKeyPath;
        Branch = branch;
    }

    public bool ValidateRepository(out string error)
    {
        error = string.Empty;

        if (!Directory.Exists(Path.Combine(RepoPath, ".git")))
        {
            error = "Directory is not a git repository.";
            return false;
        }

        var result = RunGit("status --porcelain");
        if (!result.Success)
        {
            error = $"Git not accessible or authentication failed: {result.Error}";
            return false;
        }

        return true;
    }

    public CommandResult FetchRemote()
        => RunGit($"fetch origin {Branch}");

    public bool HasRemoteUpdates()
    {
        FetchRemote();

        var result = RunGit($"rev-list --count HEAD..origin/{Branch}");
        if (!result.Success)
            throw new InvalidOperationException(result.Error);

        return int.Parse(result.Output.Trim()) > 0;
    }

    public CommandResult Pull()
        => RunGit($"pull origin {Branch}");

    public CommandResult PullIfNeeded()
    {
        if (!HasRemoteUpdates())
            return CommandResult.Ok("Already up to date.");

        return Pull();
    }

    private CommandResult RunGit(string arguments)
    {
        // Use GIT_SSH_COMMAND to explicitly specify private key
        string sshCommand = $"ssh -i \"{PrivateKeyPath}\" -o IdentitiesOnly=yes";

        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = RepoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Set environment variable for this process
        psi.Environment["GIT_SSH_COMMAND"] = sshCommand;

        var output = new StringBuilder();
        var error = new StringBuilder();

        try
        {
            using var process = Process.Start(psi);
            process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) error.AppendLine(e.Data); };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            return new CommandResult(
                process.ExitCode == 0,
                output.ToString(),
                error.ToString(),
                process.ExitCode
            );
        }
        catch (Exception ex)
        {
            return CommandResult.Fail(ex.Message);
        }
    }

    public string[] GetUpdatedFiles()
    {
        // Fetch remote first to ensure we're up-to-date
        var fetchResult = FetchRemote();
        if (!fetchResult.Success)
            throw new InvalidOperationException("Failed to fetch remote: " + fetchResult.Error);

        // Run git diff to list changed files
        var diffResult = RunGit($"diff --name-only HEAD..origin/{Branch}");
        if (!diffResult.Success)
            throw new InvalidOperationException("Failed to get diff: " + diffResult.Error);

        // Split output into file paths
        var files = diffResult.Output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        return files;
    }
}

public record CommandResult(
    bool Success,
    string Output,
    string Error,
    int ExitCode)
{
    public static CommandResult Ok(string message = "") => new(true, message, "", 0);
    public static CommandResult Fail(string error) => new(false, "", error, -1);
}
