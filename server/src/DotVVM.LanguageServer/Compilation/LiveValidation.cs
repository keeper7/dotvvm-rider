using System.Collections.Concurrent;
using DotVVM.LanguageServer.Configuration;

namespace DotVVM.LanguageServer.Compilation;

/// <summary>
/// Decides *when* the view compiler runs. Compiling costs milliseconds once warm, but a file
/// halfway through a keystroke is not worth compiling at all: an unfinished tag alone yields
/// three complaints, one of them about the end of the file. So a change waits for the typing to
/// stop, while saving is taken as the author saying the file is finished.
/// </summary>
public sealed class LiveValidation : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, CompilerProcess> _compilers = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pending = new();
    private readonly TimeSpan _quietPeriod;

    private readonly bool _enabled;

    /// <summary>
    /// Set DOTVVM_LS_LIVE_VALIDATION=off to switch the whole thing off. It starts a process that
    /// runs the project's own code and holds a Roslyn of its own, so there has to be a way out
    /// that does not involve uninstalling the plugin.
    /// </summary>
    public const string DisableVariable = "DOTVVM_LS_LIVE_VALIDATION";

    public LiveValidation(TimeSpan? quietPeriod = null, bool? enabled = null)
    {
        _quietPeriod = quietPeriod ?? TimeSpan.FromMilliseconds(500);
        _enabled = enabled ?? IsEnabled(Environment.GetEnvironmentVariable(DisableVariable));
    }

    /// <summary>Anything but "off"/"false"/"0" leaves it on; an absent variable does too.</summary>
    public static bool IsEnabled(string? value) =>
        value is null ||
        !(value.Equals("off", StringComparison.OrdinalIgnoreCase) ||
          value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
          value == "0");

    /// <summary>
    /// Compiles once the file has been quiet for the debounce period. A newer change cancels the
    /// older wait, so a burst of typing produces one compilation, not one per character.
    /// </summary>
    public void Schedule(
        string uri, string projectDir, string path, string text,
        Func<IReadOnlyList<CompilerDiagnostic>, Task> publish)
    {
        if (!_enabled) return;

        var source = new CancellationTokenSource();
        if (_pending.TryRemove(uri, out var previous))
        {
            previous.Cancel();
            previous.Dispose();
        }
        _pending[uri] = source;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_quietPeriod, source.Token);
                var diagnostics = await CompileAsync(projectDir, path, text, source.Token);
                if (diagnostics is not null) await publish(diagnostics);
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer change, which is the ordinary case
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"[dotvvm-ls] live validation: {ex.Message}");
            }
            finally
            {
                if (_pending.TryGetValue(uri, out var current) && current == source)
                {
                    _pending.TryRemove(uri, out _);
                }
                source.Dispose();
            }
        });
    }

    /// <summary>Compiles straight away, for a file that has just been saved.</summary>
    public Task<IReadOnlyList<CompilerDiagnostic>?> CompileAsync(
        string projectDir, string path, string text, CancellationToken ct)
    {
        if (!_enabled) return Task.FromResult<IReadOnlyList<CompilerDiagnostic>?>(null);

        // One compiler per **project**, not per directory: keyed by the folder a view sits in,
        // a project would end up with a process per folder, each holding its own Roslyn.
        var root = ProjectRoot.Find(projectDir) ?? projectDir;

        var compiler = _compilers.GetOrAdd(root, dir => new CompilerProcess(dir));
        return compiler.Available
            ? compiler.CompileAsync(path, text, ct)
            : Task.FromResult<IReadOnlyList<CompilerDiagnostic>?>(null);
    }

    /// <summary>Stops waiting on a file nobody is editing any more.</summary>
    public async Task ForgetAsync(string uri)
    {
        if (!_pending.TryRemove(uri, out var source)) return;
        await source.CancelAsync();
        source.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var source in _pending.Values)
        {
            await source.CancelAsync();
            source.Dispose();
        }
        _pending.Clear();

        foreach (var compiler in _compilers.Values) await compiler.DisposeAsync();
        _compilers.Clear();
    }
}
