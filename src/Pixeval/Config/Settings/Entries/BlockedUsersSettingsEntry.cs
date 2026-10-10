// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Linq.Expressions;
using AutoSettingsPage.Models;
using Pixeval.Config;

namespace Pixeval.Config;

public sealed class BlockedUsersSettingsEntry(Expression<Func<BrowsingExperienceSettingsGroup, byte>> expression)
    : ObservableSettingsEntry(expression);
