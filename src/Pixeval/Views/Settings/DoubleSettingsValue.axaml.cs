// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using AutoSettingsPage.Avalonia;
using AutoSettingsPage.Models;
using Avalonia.Controls;

namespace Pixeval.Views.Settings;

public partial class DoubleSettingsValue : NumericUpDown, IEntryControl<INumberSettingsEntry<double>>
{
    protected override Type StyleKeyOverride => typeof(NumericUpDown);

    public INumberSettingsEntry<double> Entry { set => DataContext = value; }

    public DoubleSettingsValue() => InitializeComponent();
}
