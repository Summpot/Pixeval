FilterQueryError = 筛选查询语句错误

Diagnostics-CompletionSuffixFormatted = { " 可补全：" }{ $completions }

Diagnostics-MessageWithPositionFormatted = { $message }{ "（位置 " }{ $position }）{ $completionSuffix }

Diagnostics-UnexpectedToken = 遇到了无法识别的内容：{ $value }

Diagnostics-MissingPredicateAfterNegation = { "逻辑非 " }{ $value }{ " 后缺少筛选条件" }

Diagnostics-MissingTextValue = { "筛选条件 " }{ $value }{ " 后缺少值" }

Diagnostics-MissingLongValue = { "筛选条件 " }{ $value }{ " 后缺少整数值" }

Diagnostics-MissingDoubleValue = { "筛选条件 " }{ $value }{ " 后缺少小数值" }

Diagnostics-MissingRangeValue = { "筛选条件 " }{ $value }{ " 后缺少范围值" }

Diagnostics-MissingDateValue = { "筛选条件 " }{ $value }{ " 后缺少日期值" }

Diagnostics-MissingGroupOperator = { "在 ( 后需要 and 或 or，实际为 " }{ $value }

Diagnostics-MissingRightParenthesis = { "缺少与 " }{ $value }{ " 匹配的右括号 )" }

Diagnostics-InvalidValue = { "筛选条件 " }{ $value }{ " 的值无效" }

Diagnostics-MissingStringQuote = { "字符串 " }{ $value }{ " 缺少结束引号" }

Diagnostics-InvalidLongRangeFormat = { "筛选条件 " }{ $predicate }{ " 的整数范围 " }{ $value }{ " 无效，应使用 a-b、-b 或 a- 形式" }

Diagnostics-InvalidDoubleRangeFormat = { "筛选条件 " }{ $predicate }{ " 的小数范围 " }{ $value }{ " 无效，应使用 a-b、-b 或 a- 形式" }

Diagnostics-DateRequiresMonthAndDay = { "筛选条件 " }{ $predicate }{ " 的日期 " }{ $value }{ " 至少需要月和日两部分" }

Diagnostics-ExpectedInteger = { "需要整数，实际为 " }{ $value }

Diagnostics-IntegerOutOfRange = { "整数值 " }{ $value }{ " 超出支持范围" }

Diagnostics-DenominatorCannotBeZero = { "分数 " }{ $value }{ " 的分母不能为 0" }

Diagnostics-MissingFractionalPart = { "小数 " }{ $value }{ " 缺少小数部分" }

Diagnostics-InvalidDoubleValue = 无效的小数值：{ $value }

Diagnostics-DateValueTooLarge = { "日期值 " }{ $value }{ " 过大" }

Diagnostics-NegativeRangeUnsupported = { "筛选条件 " }{ $predicate }{ " 的范围起点不能小于 0，实际为 " }{ $value }

Diagnostics-RangeMinimumGreaterThanMaximum = { "筛选条件 " }{ $predicate }{ " 的范围最小值（" }{ $minimum }）必须小于或等于最大值（{ $maximum }）

Diagnostics-DoubleRangeOpenIntervalUnsupported = { "筛选条件 " }{ $value }{ " 的小数范围暂不支持开区间写法" }

Diagnostics-InvalidDate = { "筛选条件 " }{ $predicate }{ " 的日期无效：" }{ $value }

Diagnostics-UnsupportedValueKind = { "筛选条件 " }{ $predicate }{ " 使用了未支持的筛选值类型：" }{ $value }

Diagnostics-InternalExpectedTextValue = { "筛选器内部错误：筛选条件 " }{ $value }{ " 期望字符串值" }

Diagnostics-InternalExpectedLongValue = { "筛选器内部错误：筛选条件 " }{ $value }{ " 期望整数值" }

Diagnostics-InternalExpectedDoubleValue = { "筛选器内部错误：筛选条件 " }{ $value }{ " 期望小数值" }

Diagnostics-InternalExpectedLongRangeValue = { "筛选器内部错误：筛选条件 " }{ $value }{ " 期望整数范围值" }

Diagnostics-InternalExpectedDoubleRangeValue = { "筛选器内部错误：筛选条件 " }{ $value }{ " 期望小数范围值" }

Diagnostics-InternalExpectedDateValue = { "筛选器内部错误：筛选条件 " }{ $value }{ " 期望日期值" }

Completions-And = 逻辑与分组，写法：(and 条件1 条件2)

Completions-Or = 逻辑或分组，写法：(or 条件1 条件2)

Completions-Not = 逻辑非，作用于下一个条件或整个分组

Completions-Title = 按标题筛选

Completions-Tag = 按标签筛选

Completions-Author = 按作者筛选

Completions-Bookmark = 按收藏数筛选

Completions-Ratio = 按图片比例筛选

Completions-StartDate = 按起始发布日期筛选

Completions-EndDate = 按结束发布日期筛选

Completions-Include-Constraint = 正约束

Completions-Include-R18 = 仅显示 R18（含R18G）

Completions-Include-R18G = 仅显示 R18G

Completions-Include-Gif = 仅显示动图

Completions-Include-Ai = 仅显示 AI 作品

Completions-Exclude-Constraint = 反约束

Completions-Exclude-R18 = 排除 R18（含R18G）

Completions-Exclude-R18G = 排除 R18G

Completions-Exclude-Gif = 排除动图

Completions-Exclude-Ai = 排除 AI 作品

Completions-ValueHints-Text-Plain = 普通的字符串

Completions-ValueHints-Text-Quoted = 带空格或转义字符的字符串

Completions-ValueHints-Text-Exact = 完全匹配的字符串

Completions-ValueHints-Text-QuotedExact = 完全匹配并且带空格或转义字符的字符串

Completions-ValueHints-Long-Plain = 普通的整数

Completions-ValueHints-Double-Integer = 整数形式的小数值

Completions-ValueHints-Double-Decimal = 小数形式的小数值

Completions-ValueHints-Double-Fraction = 分数形式的小数值

Completions-ValueHints-LongRange-Lower = 大于等于 2

Completions-ValueHints-LongRange-Upper = 小于等于 3

Completions-ValueHints-LongRange-Closed = 大于等于 2 且小于等于 3

Completions-ValueHints-DoubleRange-Lower = 大于等于 2

Completions-ValueHints-DoubleRange-UpperDecimal = 小于等于 1.5

Completions-ValueHints-DoubleRange-UpperFraction = 小于等于 1/2

Completions-ValueHints-DoubleRange-ClosedFraction = 大于等于 1/2 且小于等于 3

Completions-ValueHints-DoubleRange-ClosedDecimalFraction = 大于等于 0.3 且小于等于 1/2

Completions-ValueHints-Date-MonthDayDash = 今年某月某日，- 分隔

Completions-ValueHints-Date-MonthDayDot = 今年某月某日，. 分隔

Completions-ValueHints-Date-FullDash = 某年某月某日，- 分隔

Completions-ValueHints-Date-FullDot = 某年某月某日，. 分隔
