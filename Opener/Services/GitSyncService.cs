using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Opener.Services;

public record SyncResult(bool Success, string Message);

/// <summary>
/// Pushes/pulls the already-encrypted vault file through a git remote instead of
/// relying on a cloud-storage client's local sync agent (which the project's own
/// CLOUD_SYNC_RESEARCH.md documents as unreliable for direct file I/O, e.g. OneDrive
/// Files-On-Demand). Git only ever sees ciphertext - it's used purely as a versioned
/// transport for a binary blob, not as a place to diff/merge secrets.
/// </summary>
public interface IGitSyncService
{
    bool IsConfigured();
    Task<SyncResult> PushAsync();
    Task<SyncResult> PullAsync();
}

public class GitSyncService : IGitSyncService
{
    private const string CommandName = "git";
    private const string VaultFileName = "opener.dat";
    private const string Branch = "main";

    private readonly IConfigService _configService;
    private readonly IProcessRunner _runner;
    private readonly ICredentialService _gitCredentialService;
    private readonly string _syncRepoDir;

    public GitSyncService(IConfigService configService, IProcessRunner? runner = null, string? syncRepoDirOverride = null, ICredentialService? gitCredentialService = null)
    {
        _configService = configService;
        _runner = runner ?? new SystemProcessRunner();
        _gitCredentialService = gitCredentialService ?? CredentialServiceFactory.Create("git-sync");
        var home = ExecutionContextHelper.GetExecutionContextPath();
        _syncRepoDir = syncRepoDirOverride ?? Path.Combine(home, ".opener", "sync-repo");
    }

    public bool IsConfigured()
    {
        var conf = _configService.GetConfig();
        return !string.IsNullOrWhiteSpace(conf.GitSyncRemote) && _runner.CommandExists(CommandName);
    }

    public async Task<SyncResult> PushAsync()
    {
        var conf = _configService.GetConfig();
        if (string.IsNullOrWhiteSpace(conf.GitSyncRemote))
        {
            return new SyncResult(false, "No sync remote configured. Run 'o config set-sync-remote <url>' first.");
        }
        if (!_runner.CommandExists(CommandName))
        {
            return new SyncResult(false, "git is not installed or not on PATH.");
        }

        return await Task.Run(() =>
        {
            try
            {
                var dataFile = _configService.GetDataFilePath();
                if (!File.Exists(dataFile))
                {
                    return new SyncResult(false, "No vault file to sync yet.");
                }

                EnsureRepo(conf.GitSyncRemote);

                File.Copy(dataFile, Path.Combine(_syncRepoDir, VaultFileName), true);

                RunGit(new[] { "add", "." });
                // A "nothing to commit" exit is expected and not an error - fall through to push
                // in case earlier commits are still unpushed (e.g. a prior push attempt failed).
                RunGit(new[] { "commit", "-m", $"sync {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}" });

                var pushResult = RunGitWithAuth(new[] { "push", "-u", "origin", Branch }, conf.GitSyncRemote);
                if (pushResult.ExitCode != 0)
                {
                    return new SyncResult(false, $"Push failed: {FirstLine(pushResult.StandardError)}");
                }

                return new SyncResult(true, "Pushed to sync remote.");
            }
            catch (Exception ex)
            {
                return new SyncResult(false, $"Push failed: {ex.Message}");
            }
        });
    }

    public async Task<SyncResult> PullAsync()
    {
        var conf = _configService.GetConfig();
        if (string.IsNullOrWhiteSpace(conf.GitSyncRemote))
        {
            return new SyncResult(false, "No sync remote configured. Run 'o config set-sync-remote <url>' first.");
        }
        if (!_runner.CommandExists(CommandName))
        {
            return new SyncResult(false, "git is not installed or not on PATH.");
        }

        return await Task.Run(() =>
        {
            try
            {
                EnsureRepo(conf.GitSyncRemote);

                var pullResult = RunGitWithAuth(new[] { "pull", "--no-rebase", "origin", Branch }, conf.GitSyncRemote);
                if (pullResult.ExitCode != 0)
                {
                    // Leave the sync repo in a clean state for the next attempt instead of
                    // stuck mid-merge. This is a best-effort cleanup, not a resolution.
                    RunGit(new[] { "merge", "--abort" });
                    return new SyncResult(false,
                        "Pull failed or conflicted - this can happen if two machines changed the vault before syncing. " +
                        $"No local changes were made. Details: {FirstLine(pullResult.StandardError)}");
                }

                var pulledFile = Path.Combine(_syncRepoDir, VaultFileName);
                if (!File.Exists(pulledFile))
                {
                    return new SyncResult(false, "Sync remote has no vault file yet - nothing to pull.");
                }

                var dataFile = _configService.GetDataFilePath();
                if (File.Exists(dataFile))
                {
                    SnapshotBeforeOverwrite(dataFile);
                }

                var destDir = Path.GetDirectoryName(dataFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                File.Copy(pulledFile, dataFile, true);

                return new SyncResult(true, "Pulled from sync remote. Your previous vault was backed up to .backup/ first.");
            }
            catch (Exception ex)
            {
                return new SyncResult(false, $"Pull failed: {ex.Message}");
            }
        });
    }

    private void EnsureRepo(string remote)
    {
        Directory.CreateDirectory(_syncRepoDir);

        if (!Directory.Exists(Path.Combine(_syncRepoDir, ".git")))
        {
            RunGit(new[] { "init" });
            RunGit(new[] { "checkout", "-b", Branch });
        }

        // A local-only identity so commits never fail on a machine with no global git
        // user.email/user.name configured - this repo is never inspected by a human.
        RunGit(new[] { "config", "user.email", "opener-cli@local" });
        RunGit(new[] { "config", "user.name", "Opener CLI" });

        var addResult = RunGit(new[] { "remote", "add", "origin", remote });
        if (addResult.ExitCode != 0)
        {
            RunGit(new[] { "remote", "set-url", "origin", remote });
        }
    }

    /// <summary>
    /// Snapshots the current vault into the same .backup/ folder/naming convention
    /// BackupCommand uses, before a pull overwrites it.
    /// </summary>
    private void SnapshotBeforeOverwrite(string dataFile)
    {
        var backupDir = Path.Combine(Path.GetDirectoryName(dataFile) ?? ".", ".backup");
        Directory.CreateDirectory(backupDir);
        var backupFile = Path.Combine(backupDir, $"opener_backup_{DateTime.Now:yyyyMMdd_HHmmss}.dat");
        File.Copy(dataFile, backupFile, true);
    }

    private ProcessRunResult RunGit(string[] args)
    {
        var full = new List<string> { "-C", _syncRepoDir };
        full.AddRange(args);
        return _runner.Run(CommandName, full.ToArray());
    }

    private ProcessRunResult RunGitWithAuth(string[] args, string remote)
    {
        var full = new List<string> { "-C", _syncRepoDir };
        full.AddRange(BuildAuthConfigArgs(remote));
        full.AddRange(args);
        return _runner.Run(CommandName, full.ToArray());
    }

    /// <summary>
    /// SSH remotes need no help here - they use the user's existing SSH agent/keys like
    /// any other git operation. HTTPS remotes get a token (if one is stored) injected as
    /// a one-off header for this single invocation only - never written to git config or
    /// disk, and never appearing in the remote URL.
    /// </summary>
    private string[] BuildAuthConfigArgs(string remote)
    {
        if (!remote.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<string>();
        }

        var token = _gitCredentialService.GetPassword();
        if (string.IsNullOrEmpty(token))
        {
            return Array.Empty<string>();
        }

        var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"x-access-token:{token}"));
        return new[] { "-c", $"http.extraheader=AUTHORIZATION: basic {basicAuth}" };
    }

    private static string FirstLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "(no error output)";
        var idx = text.IndexOfAny(new[] { '\r', '\n' });
        return idx >= 0 ? text[..idx] : text;
    }
}
