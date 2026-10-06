// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using Microsoft.Win32.SafeHandles;
using Pixeval.Extensions.Common;
using Pixeval.I18N;
using Pixeval.Utilities;

namespace Pixeval.Models.Extensions;

public sealed partial class ExtensionsHostModel(
    IExtensionsHost host,
    Dictionary<string, object?> values) : ObservableObject, IDisposable
{
    private IExtensionsHost? _host = host;

    public IExtensionsHost Host => _host ?? throw new ObjectDisposedException(nameof(ExtensionsHostModel));

    public bool IsActive
    {
        get => Values.TryGetTargetOrAddDefault(nameof(IsActive), true);
        set
        {
            if (Values.TryGetTargetOrAddDefault(nameof(IsActive), true) == value)
