using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotVVM.LanguageServer.Configuration;

namespace DotVVM.LanguageServer.Compilation;

/// <summary>
/// The long-lived compiler process and the conversation with it.
///
/// Long-lived on purpose: the first compilation pays for Roslyn waking up and every one after it
/// costs milliseconds - measured on a real project of 244 views, a median of 13 ms and 45 ms at
/// the 90th percentile once warm. A process per request would spend seconds on every keystroke.
///
/// Requests are serialised: one line in, one line out over the child's standard streams.
/// </summary>
public sealed class CompilerProcess : IAsyncDisposable
{
    /// <summary>
    /// The **project's root**, the folder holding the .csproj - not the folder the view is in.
    /// DotVVM resolves a markup control's Src and a master page's path against it, so with the
    /// view's own folder a `&lt;cc:MyControl&gt;` registered as `Controls/MyControl.dotcontrol`
    /// is reported as a file that was not found.
    /// </summary>
    private readonly string _projectRoot;

    private readonly TimeSpan _timeout;
    private readonly SemaphoreSlim _turn = new(1, 1);

    private Process? _process;
    private int _nextId;

    /// <summary>Set once the process could not be started, so it is not retried on every keystroke.</summary>
    private bool _unavailable;

    public CompilerProcess(string projectRoot, TimeSpan? timeout = null)
    {
        _projectRoot = projectRoot;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>Whether a compiler could be started for this project at all.</summary>
    public bool Available => !_unavailable;

    /// <summary>
    /// What the view compiler says about a file. Null means there is no answer at all - no
    /// build to run against, or a process that died - and the caller leaves what is shown alone.
    /// </summary>
    public async Task<IReadOnlyList<CompilerDiagnostic>?> CompileAsync(
        string path, string text, CancellationToken ct)
    {
        var response = await ExchangeAsync(id => new Request(id, path, text), ct);
        return response is null
            ? null
            : response.Diagnostics ?? (IReadOnlyList<CompilerDiagnostic>)Array.Empty<CompilerDiagnostic>();
    }

    /// <summary>
    /// What may be written inside a binding at the given offset. It goes to the same process as
    /// a compilation and for the same reason: only it holds both DotVVM and the project's own
    /// assembly, and only it can say what the data context is at that place in the file.
    /// </summary>
    public async Task<IReadOnlyList<CompilerCompletionItem>?> CompleteAsync(
        string path, string text, int offset, string expression, string binding,
        CancellationToken ct)
    {
        var response = await ExchangeAsync(
            id => new Request(id, path, text, "complete", offset, expression, binding), ct);

        return response is null
            ? null
            : response.Items ?? (IReadOnlyList<CompilerCompletionItem>)Array.Empty<CompilerCompletionItem>();
    }

    /// <summary>
    /// Starts the process without asking it anything, so the seconds it spends waking Roslyn are
    /// spent while the file is being read rather than while the first popup is waiting for it.
    /// </summary>
    public async Task WarmAsync(CancellationToken ct)
    {
        if (_unavailable) return;

        await _turn.WaitAsync(ct);
        try { EnsureStarted(); }
        catch (Exception) { /* the next request reports it; there is nobody to tell here */ }
        finally { _turn.Release(); }
    }

    /// <summary>
    /// One request out, one response back. Requests are serialised: the child reads a line at a
    /// time, and a completion asked for while a compilation is running waits its turn - which
    /// costs milliseconds once the process is warm.
    /// </summary>
    private async Task<Response?> ExchangeAsync(Func<int, Request> build, CancellationToken ct)
    {
        if (_unavailable) return null;

        await _turn.WaitAsync(ct);
        try
        {
            if (!EnsureStarted()) return null;

            var request = build(++_nextId);
            await _process!.StandardInput.WriteLineAsync(
                JsonSerializer.Serialize(request, WireContext.Default.Request));
            await _process.StandardInput.FlushAsync(ct);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(_timeout);

            var line = await _process.StandardOutput.ReadLineAsync(timeout.Token);
            if (line is null)
            {
                // The child died mid-request; the next call starts a fresh one
                Stop();
                return null;
            }

            var response = JsonSerializer.Deserialize(line, WireContext.Default.Response);
            if (response?.Error is not null)
            {
                await Console.Error.WriteLineAsync($"[dotvvm-ls] compiler: {response.Error}");
                return new Response(response.Id, null, null, null);
            }

            return response;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timed out on this file. The process is left in an unknown state, so it goes.
            Stop();
            return null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            Stop();
            return null;
        }
        finally
        {
            _turn.Release();
        }
    }

    private bool EnsureStarted()
    {
        if (_process is { HasExited: false }) return true;
        Stop();

        var assembly = ProjectAssembly.Find(_projectRoot);
        if (assembly is null || !CompilerBinary.CanRunAgainst(assembly, File.Exists))
        {
            // No build yet, or a build without the deps the compiler needs. Not an error: the
            // structural validation carries on and this simply stays silent.
            _unavailable = true;
            return false;
        }

        var compiler = CompilerBinary.Resolve(
            ProjectAssembly.ReadTargetFramework(assembly), File.Exists);
        if (compiler is null)
        {
            _unavailable = true;
            return false;
        }

        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in CompilerBinary.BuildArguments(compiler, assembly, _projectRoot))
        {
            info.ArgumentList.Add(argument);
        }

        try
        {
            _process = Process.Start(info);
            if (_process is null) { _unavailable = true; return false; }

            // The child says so once it has warmed up; until then it is not worth asking
            var ready = _process.StandardOutput.ReadLine();
            if (ready is null)
            {
                Stop();
                _unavailable = true;
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[dotvvm-ls] compiler failed to start: {ex.Message}");
            _unavailable = true;
            return false;
        }
    }

    private void Stop()
    {
        if (_process is null) return;
        try
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        }
        catch { /* already gone */ }
        _process.Dispose();
        _process = null;
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        _turn.Dispose();
        return ValueTask.CompletedTask;
    }

}

/// <summary>The two messages that cross the pipe. Kept out of the class so the JSON source
/// generator can see them - it needs every containing type to be partial.</summary>
internal record Request(
    int Id, string Path, string Text, string Kind = "compile", int Offset = 0,
    string Expression = "", string Binding = "value");

internal record Response(
    int Id, List<CompilerDiagnostic>? Diagnostics, List<CompilerCompletionItem>? Items, string? Error);

[JsonSerializable(typeof(Request))]
[JsonSerializable(typeof(Response))]
internal partial class WireContext : JsonSerializerContext;
