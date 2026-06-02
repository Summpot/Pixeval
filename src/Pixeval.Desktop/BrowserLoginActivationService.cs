using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Pixeval.AppManagement;

namespace Pixeval.Desktop;

internal sealed class BrowserLoginActivationService : IDisposable
{
    private const string MutexName = "Local\\Pixeval.PixivLoginActivation";

    private const string PipeName = "Pixeval.PixivLoginActivation";

    private const string ActivateMessage = "activate";

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly Mutex? _mutex;

    private readonly Task? _listeningTask;

    private BrowserLoginActivationService(Mutex? mutex, bool shouldExit)
    {
        _mutex = mutex;
        ShouldExit = shouldExit;

        if (_mutex is null || ShouldExit)
            return;

        TryRegisterProtocol();
        _listeningTask = Task.Run(() => ListenAsync(_cancellationTokenSource.Token));
    }

    public bool ShouldExit { get; }

    public static BrowserLoginActivationService Create(IReadOnlyList<string> args)
    {
        var callbackUris = ExtractCallbackUris(args);
        var mutex = new Mutex(true, MutexName, out var ownsMutex);
        if (!ownsMutex)
        {
            mutex.Dispose();
            _ = TryForward(callbackUris);
            return new(null, true);
        }

        foreach (var uri in callbackUris)
            PixivLoginActivationHub.Submit(uri);

        return new(mutex, false);
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        try
        {
            _listeningTask?.Wait(TimeSpan.FromMilliseconds(500));
        }
        catch
        {
            // ignored
        }

        try
        {
            _mutex?.ReleaseMutex();
        }
        catch
        {
            // ignored
        }

        _mutex?.Dispose();
        _cancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }

    private static IReadOnlyList<Uri> ExtractCallbackUris(IReadOnlyList<string> args)
    {
        List<Uri>? uris = null;
        foreach (var arg in args)
        {
            if (!PixivLoginActivationHub.TryCreateCallbackUri(arg, out var uri))
                continue;

            uris ??= [];
            uris.Add(uri);
        }

        return uris ?? [];
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(server);
                while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                    if (line.Equals(ActivateMessage, StringComparison.Ordinal))
                    {
                        AppActivationHub.Submit();
                    }
                    else if (PixivLoginActivationHub.TryCreateCallbackUri(line, out var uri))
                    {
                        PixivLoginActivationHub.Submit(uri);
                    }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool TryForward(IReadOnlyList<Uri> callbackUris)
    {
        for (var attempt = 0; attempt < 20; ++attempt)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(150);
                using var writer = new StreamWriter(client);
                writer.WriteLine(ActivateMessage);
                foreach (var uri in callbackUris)
                    writer.WriteLine(uri.OriginalString);

                return true;
            }
            catch
            {
                Thread.Sleep(100);
            }
        }

        return false;
    }

    private static void TryRegisterProtocol()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            RegisterWindowsProtocol();
        }
        catch
        {
            // ignored
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindowsProtocol()
    {
        if (BuildLaunchCommand() is not { } command)
            return;

        using var protocolKey = Registry.CurrentUser.CreateSubKey(
            $@"Software\Classes\{PixivLoginActivationHub.CallbackScheme}");
        protocolKey?.SetValue("", $"URL:{PixivLoginActivationHub.CallbackScheme}");
        protocolKey?.SetValue("URL Protocol", "");

        using var commandKey = protocolKey?.CreateSubKey(@"shell\open\command");
        commandKey?.SetValue("", command);
    }

    private static string? BuildLaunchCommand()
    {
        if (Environment.ProcessPath is not { Length: > 0 } processPath)
            return null;

        return Path.GetExtension(processPath).Equals(".dll", StringComparison.OrdinalIgnoreCase)
            ? $"dotnet {Quote(processPath)} \"%1\""
            : $"{Quote(processPath)} \"%1\"";
    }

    private static string Quote(string path) => $"\"{path.Replace("\"", "\\\"")}\"";
}
