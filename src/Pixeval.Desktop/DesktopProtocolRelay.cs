using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using Pixeval.AppManagement;

namespace Pixeval.Desktop;

internal static class DesktopProtocolRelay
{
    private const string MutexName = "Pixeval.Desktop.SingleInstance";

    private const string PipeName = "Pixeval.Desktop.ProtocolPipe";

    private static readonly Mutex Mutex = new(false, MutexName);

    private static readonly bool IsPrimaryInstance = AcquirePrimaryState();

    public static bool Initialize(string[] args)
    {
        CallbackProtocolRegistrationState.EnsureRegistration = () =>
        {
            var ready = DesktopProtocolRegistrar.EnsureRegistered(out var registrationError);
            return (ready, registrationError);
        };
        _ = CallbackProtocolRegistrationState.EnsureReadyNow();

        var protocolArgs = ExtractProtocolArgs(args).ToArray();

        if (!IsPrimaryInstance)
        {
            if (protocolArgs.Length > 0)
                ForwardProtocolArgs(protocolArgs);

            if (OperatingSystem.IsMacOS())
                return false;

            return protocolArgs.Length == 0;
        }

        foreach (var protocolArg in protocolArgs)
            ProtocolActivationHub.Publish(protocolArg);

        StartListener();
        return true;
    }

    private static bool AcquirePrimaryState()
    {
        try
        {
            return Mutex.WaitOne(0, false);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }

    private static IEnumerable<string> ExtractProtocolArgs(IEnumerable<string> args)
    {
        return args.Where(arg =>
            arg.StartsWith("pixiv:", StringComparison.OrdinalIgnoreCase)
            || arg.StartsWith("pixeval:", StringComparison.OrdinalIgnoreCase));
    }

    private static void ForwardProtocolArgs(IEnumerable<string> protocolArgs)
    {
        foreach (var protocolArg in protocolArgs)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(800);
                using var writer = CreateAutoFlushWriter(client);
                writer.WriteLine(protocolArg);
            }
            catch
            {
                // ignore forwarding errors to avoid blocking startup path
            }
        }
    }

    private static void StartListener()
    {
        var listenerThread = new Thread(ListenLoop)
        {
            IsBackground = true,
            Name = "Pixeval Protocol Pipe Listener"
        };

        listenerThread.Start();
    }

    private static void ListenLoop()
    {
        while (!Environment.HasShutdownStarted)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.None);

                server.WaitForConnection();
                using var reader = new StreamReader(server);
                var line = reader.ReadLine();
                ProtocolActivationHub.Publish(line);
            }
            catch
            {
                // Keep listening even if one transfer fails.
            }
        }
    }

    private static StreamWriter CreateAutoFlushWriter(Stream stream)
    {
        return new StreamWriter(stream)
        {
            AutoFlush = true
        };
    }
}
