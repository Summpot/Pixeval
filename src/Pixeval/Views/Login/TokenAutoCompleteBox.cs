// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Pixeval.Storage;

namespace Pixeval.Views.Login;

/// <summary>Account completion with the same protected automation values as a password TextBox.</summary>
public sealed class TokenAutoCompleteBox : AutoCompleteBox
{
    private const char MaskCharacter = '•';

    // A blank automation name lets content peers fall back to the entry's
    // ToString(), which is its token. Match the presenter's numeric-ID fallback.
    public static FuncValueConverter<LoginUserRecord?, string> AccountNameConverter { get; } =
        new(user => user is not { } account ? "" : string.IsNullOrWhiteSpace(account.Name) ? account.UserId.ToString() : account.Name);

    protected override Type StyleKeyOverride => typeof(AutoCompleteBox);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<TextBox>("PART_TextBox") is { } editor)
        {
            editor.SetCurrentValue(TextBox.PasswordCharProperty, MaskCharacter);
            editor.SetCurrentValue(TextBox.RevealPasswordProperty, false);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TokenAutomationPeer(this);

    // Do not derive from AutoCompleteBoxAutomationPeer: its private change handler
    // publishes raw Text values even if a subclass masks the Value getter.
    private sealed class TokenAutomationPeer : ControlAutomationPeer, IValueProvider, IExpandCollapseProvider
    {
        private new TokenAutoCompleteBox Owner => (TokenAutoCompleteBox) base.Owner;

        internal TokenAutomationPeer(TokenAutoCompleteBox owner) : base(owner) => owner.PropertyChanged += OwnerPropertyChanged;

        public bool IsReadOnly => false;
        public string? Value => Mask(Owner.Text);
        public bool ShowsMenu => true;
        public ExpandCollapseState ExpandCollapseState => State(Owner.IsDropDownOpen);

        public void SetValue(string? value) => Owner.SetCurrentValue(TextProperty, value);
        public void Expand() => Owner.SetCurrentValue(IsDropDownOpenProperty, true);
        public void Collapse() => Owner.SetCurrentValue(IsDropDownOpenProperty, false);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetClassNameCore() => nameof(AutoCompleteBox);

        private void OwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == TextProperty)
                RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, Mask(change.OldValue as string), Mask(change.NewValue as string));
            else if (change.Property == IsDropDownOpenProperty)
                RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                    State(change.GetOldValue<bool>()), State(change.GetNewValue<bool>()));
        }

        private static string? Mask(string? value) => value is null ? null : new(MaskCharacter, value.Length);
        private static ExpandCollapseState State(bool expanded) => expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
    }
}
