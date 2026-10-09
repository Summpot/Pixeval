// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Pixeval.AppManagement;
using Pixeval.Extensions.Common;
using Pixeval.Extensions.Common.Commands.Transformers;
using Pixeval.Extensions.Common.Downloaders;
using Pixeval.Extensions.Common.FormatProviders;
using Pixeval.Extensions.Common.Settings;
using Pixeval.Native.Plugin;
using Pixeval.Utilities;

namespace Pixeval.Models.Extensions;

public sealed partial class ExtensionService
{
    public ExtensionHostLoadResult TryLoadHostWithResult(
        string path,
        ILogger logger,
        out ExtensionsHostModel? model,
        out string? outdatedVersion,
        string? uninstallTargetRelativePath = null)
    {
        model = null;
        outdatedVersion = null;
        uninstallTargetRelativePath ??= PluginEngine.GetUninstallTargetRelativePath(path, AppInfo.ExtensionsFolder) ?? "";

        var libraryHandle = IntPtr.Zero;
        var libraryHandleTransferredToModel = false;
        try
        {
            if (!NativeLibrary.TryLoad(
                    path,
                    typeof(ExtensionService).Assembly,
                    DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.SafeDirectories,
                    out libraryHandle))
                return ExtensionHostLoadResult.NativeLibraryLoadFailed;

            if (!NativeLibrary.TryGetExport(libraryHandle, nameof(GetExtensionsHost), out var getExtensionsHostPtr))
                return ExtensionHostLoadResult.MissingEntryPoint;

            var getExtensionsHost = Marshal.GetDelegateForFunctionPointer<GetExtensionsHost>(getExtensionsHostPtr);
            var result = getExtensionsHost(out var ppv);
            if (result is not 0)
                return ExtensionHostLoadResult.EntryPointInvocationFailed;

            var wrappers = new StrategyBasedComWrappers();
            var rcw = (IExtensionsHost)wrappers.GetOrCreateObjectForComInstance(ppv, CreateObjectFlags.UniqueInstance);
            _ = Marshal.Release(ppv);

            if (rcw.SdkVersion != CurrentVersion)
            {
                outdatedVersion = rcw.SdkVersion;
                return ExtensionHostLoadResult.OutdatedSdk;
            }

            rcw.Initialize(CultureInfo.CurrentCulture.Name, AppInfo.TempFolder,
                Path.GetDirectoryName(path) ?? AppInfo.ExtensionsFolder, logger);

            ref var values = ref CollectionsMarshal.GetValueRefOrAddDefault(_extensionSettings, rcw.ExtensionName, out var exists);
            if (!exists) values = [];

            model = new(rcw, values!)
            {
                Handle = new NativeLibrarySafeHandle(libraryHandle),
                HostLibraryPath = path,
                UninstallTargetRelativePath = uninstallTargetRelativePath
            };
            libraryHandleTransferredToModel = true;
        }
        catch
        {
            return libraryHandle == IntPtr.Zero
                ? ExtensionHostLoadResult.NativeLibraryLoadFailed
                : ExtensionHostLoadResult.InitializationFailed;
        }
        finally
        {
            if (!libraryHandleTransferredToModel && libraryHandle != IntPtr.Zero)
                try { NativeLibrary.Free(libraryHandle); } catch { /* ignored */ }
        }

        var loadedModel = model!;
        try
        {
            foreach (var extension in loadedModel.Extensions)
                extension.OnExtensionLoaded();
            LoadSettingsExtension(loadedModel, loadedModel.Extensions);
            loadedModel.IsPendingUninstall = _pendingExtensionUninstallTargets.Contains(loadedModel.UninstallTargetRelativePath);
            InsertHost(loadedModel);
            RegisterHostWithPluginEngine(loadedModel, path, logger);
            return ExtensionHostLoadResult.Loaded;
        }
        catch
        {
            loadedModel.Dispose();
            model = null;
            return ExtensionHostLoadResult.ExtensionLoadFailed;
        }
    }

    private void InsertHost(ExtensionsHostModel model)
    {
        model.PropertyChanged += OnHostModelPropertyChanged;
        for (var i = 0; i < HostModels.Count; ++i)
        {
            if (HostModels[i].Priority >= model.Priority)
            {
                HostModels.Insert(i, model);
                return;
            }
        }
        HostModels.Add(model);
    }

    private void RegisterHostWithPluginEngine(ExtensionsHostModel loadedModel, string path, ILogger logger)
    {
        try
        {
            var host = loadedModel.Host;
            var extDescriptors = loadedModel.Extensions.Select(ext => new PluginExtensionDescriptor(
                ext switch
                {
                    IImageTransformerCommandExtension => "image_transformer",
                    ITextTransformerCommandExtension => "text_transformer",
                    IDownloaderExtension => "downloader",
                    IStaticImageFormatProviderExtension => "static_image_format_provider",
                    IAnimatedImageFormatProviderExtension => "animated_image_format_provider",
                    INovelFormatProviderExtension => "novel_format_provider",
                    ISettingsExtension => "settings",
                    _ => "extension"
                },
                ext switch
                {
                    IEntryExtension entry => entry.Label ?? "",
                    IFormatProviderExtension fp => fp.FormatExtension ?? "",
                    _ => ext.GetType().Name
                },
                ext switch
                {
                    IEntryExtension entry => entry.Description ?? "",
                    IFormatProviderExtension fp => fp.FormatDescription ?? "",
                    _ => ""
                },
                ext.GetType().FullName ?? $"{host.ExtensionName}.{ext.GetType().Name}"
            )).ToList();

            var pluginMeta = new PluginMetadata(
                host.ExtensionName,
                host.ExtensionName,
                host.AuthorName ?? "",
                host.Version ?? "",
                host.Description ?? "",
                host.SdkVersion ?? CurrentVersion,
                path,
                extDescriptors,
                loadedModel.IsActive
            );

            PluginEngine.RegisterMetadata(pluginMeta);
            try { _ = PluginEngine.LoadPlugin(path); } catch { /* ignored */ }
        }
        catch (Exception ex)
        {
            if (logger is FileLogger fileLogger)
                fileLogger.LogWarning($"Failed to register plugin metadata with native engine for {path}", ex);
        }
    }
}
