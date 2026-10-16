UnexpectedToken = { "Illegal character: " }{ $token }

ExpectedLeftBraceAfterAt = { "Expected '{' after '@'" }

ExpectedMacroName = { "Expected a macro name after '@{'" }

MissingRightBrace = Unmatched brace

MissingConditionalSeparator = Missing ':' in conditional macro

UnknownMacroName = { "Unknown macro name: '@{" }{ $macroName }{ "}'" }

NonParameterizedMacroBearingParameter = { "Macro '@{" }{ $macroName }{ "}' cannot accept parameters" }

ConditionalBranchesMissing = { "Conditional macro '@{" }{ $macroName }{ "?:}' must specify both true and false branches using '?' and ':'" }

InvalidFormatter = { "Invalid format parameter for macro '@{" }{ $macroName }{ "}': '" }{ $formatter }'

MacroShouldBeInLastSegment = { "Macro '@{" }{ $macroName }{ "}' must be in the filename" }

MacroContextRestrictionNotSatisfied = { "Context restriction not satisfied for macro '@{" }{ $macroName }{ "}'" }

MessageWithPositionFormatted = { $message }{ " (position " }{ $position })

MacroDescription-Ext = { "File extension (e.g., jpg, png); without the leading dot, usually written as .@{ext}; must be in the filename" }

MacroDescription-Id = Work ID (string)

MacroDescription-Title = Work title (string)

MacroDescription-ArtistId = Author ID (string)

MacroDescription-ArtistName = Author name (string)

MacroDescription-SeriesId = { "Series ID (integer); must be used in the true branch of @{is_series?:}" }

MacroDescription-SeriesTitle = { "Series title (string); must be used in the true branch of @{is_series?:}" }

MacroDescription-PublishTime = Work publish time (datetime)

MacroDescription-PicSetIndex = { "Replaced with the index of the current image within an image set; must be used in the true branch of @{is_pic_set?:} and in the filename (number)" }

MacroDescription-GroupId = { "Subscription download group ID (integer); must be used in the true branch of @{is_group?:}, @{is_bookmark_group?:}, @{is_post_group?:}, or @{is_series_group?:}" }

MacroDescription-IsPicSet = Whether the work is an image set (multiple images)

MacroDescription-IsR18 = Whether the work is age-restricted (R18 or R18G)

MacroDescription-IsR18G = Whether the work is R18G

MacroDescription-IsAi = Whether the work is AI-generated

MacroDescription-IsPicOne = Whether the work is a single static image

MacroDescription-IsNovel = Whether the work is a novel

MacroDescription-IsPicGif = Whether the work is an animated image (ugoira)

MacroDescription-IsSeries = Whether the work belongs to a series

MacroDescription-IsGroup = Whether the work is in a subscription download group

MacroDescription-IsBookmarkGroup = Whether the work is in a bookmark subscription group

MacroDescription-IsPostGroup = Whether the work is in a post subscription group

MacroDescription-IsSeriesGroup = Whether the work is in a series subscription group
