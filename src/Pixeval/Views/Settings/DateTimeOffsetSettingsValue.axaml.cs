// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.ComponentModel;
using System.Globalization;
using AutoSettingsPage.Avalonia;
using AutoSettingsPage.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Material3Expressive.Components;
using Pixeval.Views.Converters;

namespace Pixeval.Views.Settings;

public partial class DateTimeOffsetSettingsValue : TextBox, IEntryControl<ISingleValueSettingsEntry<DateTimeOffset>>
{
    private INotifyPropertyChanged? _observedEntry;
    private bool _updatingText;
    private bool _updatingEntry;

    protected override Type StyleKeyOverride => typeof(TextBox);

    public ISingleValueSettingsEntry<DateTimeOffset> Entry { set => DataContext = value; }

    public DateTimeOffsetSettingsValue() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ObserveEntry();
        UpdateText();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveEntry();
        UpdateText();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_observedEntry is not null)
            _observedEntry.PropertyChanged -= EntryOnPropertyChanged;
        _observedEntry = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void ObserveEntry()
    {
        if (_observedEntry is not null)
            _observedEntry.PropertyChanged -= EntryOnPropertyChanged;
        _observedEntry = VisualRoot is not null ? DataContext as INotifyPropertyChanged : null;
        if (_observedEntry is not null)
            _observedEntry.PropertyChanged += EntryOnPropertyChanged;
    }

    private void EntryOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_updatingEntry && (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(ISingleValueSettingsEntry<DateTimeOffset>.Value)))
            UpdateText();
    }

    private void UpdateText()
    {
        _updatingText = true;
        try
        {
            Text = DataContext is ISingleValueSettingsEntry<DateTimeOffset> entry
                ? (string) DateTimeOffsetTextConverter.Instance.Convert(entry.Value, typeof(string), null, CultureInfo.InvariantCulture)
                : null;
            SetValidationError(null);
        }
        finally
        {
            _updatingText = false;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != TextProperty || _updatingText || DataContext is not ISingleValueSettingsEntry<DateTimeOffset> entry)
            return;

        // Avalonia's binding equality also compares DateTimeOffset by UTC instant.
        // Write the parsed value directly so offset-only edits reach the setting.
        var parsed = DateTimeOffsetTextConverter.Instance.ConvertBack(Text, typeof(DateTimeOffset), null, CultureInfo.InvariantCulture);
        if (parsed is not DateTimeOffset date)
        {
            SetValidationError(((BindingNotification) parsed).Error);
            return;
        }

        SetValidationError(null);
        _updatingEntry = true;
        try
        {
            entry.Value = date;
        }
        catch (Exception exception)
        {
            // Match a validated two-way binding: surface extension setter failures on the editor.
            SetValidationError(exception);
        }
        finally
        {
            _updatingEntry = false;
        }
    }

    private void SetValidationError(Exception? exception)
    {
        DataValidationErrors.SetErrors(this, exception is null ? null : new[] { exception });
        MaterialTextField.SetIsError(this, exception is not null);
        MaterialTextField.SetErrorMessage(this, exception?.Message ?? "");
        MaterialTextField.SetSupportingText(this, exception?.Message);
    }
}
