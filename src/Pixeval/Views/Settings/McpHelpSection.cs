// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia.Controls;
#if PIXEVAL_MCP
using Pixeval.Localization;
#endif

namespace Pixeval.Views.Settings;

public partial class McpHelpSection : UserControl
{
    public McpHelpSection()
    {
#if PIXEVAL_MCP
        InitializeComponent();
        SetMcpStatus(Strings.HelpPageMcpToolsStatusDisabled);
        _languageSubscription = AppLocalization.Instance.Subscribe(() =>
        {
            if (!CheckAccess()) return;
            McpToolsStatus = _status();
        });
        Pixeval.ViewModels.ViewModelDisposal.Register(this, _languageSubscription);
#endif
    }
}
