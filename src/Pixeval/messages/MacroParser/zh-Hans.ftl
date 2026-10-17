UnexpectedToken = 存在非法符号：{ $token }

ExpectedLeftBraceAfterAt = { "在 '@' 后应该跟随 '{'" }

ExpectedMacroName = { "在 '@{' 后应该跟随宏名称" }

MissingRightBrace = 括号不匹配

MissingConditionalSeparator = 条件宏缺少 ':'

UnknownMacroName = { "未知的宏名称：'@{" }{ $macroName }{ "}'" }

NonParameterizedMacroBearingParameter = { "宏 '@{" }{ $macroName }{ "}' 不能包含任何参数" }

ConditionalBranchesMissing = { "条件宏 '@{" }{ $macroName }{ "?:}' 必须使用 ? 和 : 同时指定正反分支" }

InvalidFormatter = { "宏 '@{" }{ $macroName }{ "}' 的格式化参数无效：'" }{ $formatter }'

MacroShouldBeInLastSegment = { "宏 '@{" }{ $macroName }{ "}' 必须在文件名上" }

MacroContextRestrictionNotSatisfied = { "宏 '@{" }{ $macroName }{ "}' 的上下文限制不满足" }

MessageWithPositionFormatted = { $message }{ "（位置 " }{ $position }）

MacroDescription-Ext = { "文件扩展名，例如 jpg、png；不包含前导点号，通常写作 .@{ext}；必须在文件名上使用" }

MacroDescription-Id = 作品ID（字符串类型）

MacroDescription-Title = 作品标题（字符串类型）

MacroDescription-ArtistId = 作者ID（字符串类型）

MacroDescription-ArtistName = 作者名（字符串类型）

MacroDescription-SeriesId = { "系列ID（整数类型）；必须在 @{is_series?:} 条件宏正分支中使用" }

MacroDescription-SeriesTitle = { "系列标题（字符串类型）；必须在 @{is_series?:} 条件宏正分支中使用" }

MacroDescription-PublishTime = 作品发布时间（时间类型）

MacroDescription-PicSetIndex = { "该宏会被替换为当前图片在多图中的索引（第几张）；必须在 @{is_pic_set?:} 条件宏正分支中使用，必须在文件名上使用（数字类型）" }

MacroDescription-GroupId = { "订阅下载组ID（整数类型）；必须在 @{is_group?:}、@{is_bookmark_group?:}、@{is_post_group?:} 或 @{is_series_group?:} 任意一个条件宏的正分支中使用" }

MacroDescription-IsPicSet = 作品是否为多图

MacroDescription-IsR18 = 作品是否为限制级的（包括R18及R18G）

MacroDescription-IsR18G = 作品是否为R18G的

MacroDescription-IsAi = 作品是否为 AI 生成的

MacroDescription-IsPicOne = 作品是否为静态单图

MacroDescription-IsNovel = 作品是否为小说

MacroDescription-IsPicGif = 作品是否为动图

MacroDescription-IsSeries = 作品是否属于系列

MacroDescription-IsGroup = 作品是否在订阅下载组中

MacroDescription-IsBookmarkGroup = 作品是否在收藏订阅下载组中

MacroDescription-IsPostGroup = 作品是否在投稿订阅下载组中

MacroDescription-IsSeriesGroup = 作品是否在系列订阅下载组中
