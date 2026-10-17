FilterQueryError = Filter Query Error

Diagnostics-CompletionSuffixFormatted = { " Completions: " }{ $completions }

Diagnostics-MessageWithPositionFormatted = { $message }{ " (position " }{ $position }){ $completionSuffix }

Diagnostics-UnexpectedToken = { "Encountered unrecognized content: " }{ $value }

Diagnostics-MissingPredicateAfterNegation = { "Missing a filter condition after the negation " }{ $value }

Diagnostics-MissingTextValue = { "Missing a value after filter condition " }{ $value }

Diagnostics-MissingLongValue = { "Missing an integer value after filter condition " }{ $value }

Diagnostics-MissingDoubleValue = { "Missing a decimal value after filter condition " }{ $value }

Diagnostics-MissingRangeValue = { "Missing a range value after filter condition " }{ $value }

Diagnostics-MissingDateValue = { "Missing a date value after filter condition " }{ $value }

Diagnostics-MissingGroupOperator = { "Expected 'and' or 'or' after '(', got: " }{ $value }

Diagnostics-MissingRightParenthesis = { "Missing the closing parenthesis ')' matching " }{ $value }

Diagnostics-InvalidValue = { "The value of filter condition " }{ $value }{ " is invalid" }

Diagnostics-MissingStringQuote = { "String " }{ $value }{ " is missing the closing quote" }

Diagnostics-InvalidLongRangeFormat = { "The integer range " }{ $value }{ " of filter condition " }{ $predicate }{ " is invalid; use a-b, -b, or a-" }

Diagnostics-InvalidDoubleRangeFormat = { "The decimal range " }{ $value }{ " of filter condition " }{ $predicate }{ " is invalid; use a-b, -b, or a-" }

Diagnostics-DateRequiresMonthAndDay = { "The date " }{ $value }{ " of filter condition " }{ $predicate }{ " must contain at least the month and day" }

Diagnostics-ExpectedInteger = { "Expected an integer, got " }{ $value }

Diagnostics-IntegerOutOfRange = { "The integer value " }{ $value }{ " is out of the supported range" }

Diagnostics-DenominatorCannotBeZero = { "The denominator of the fraction " }{ $value }{ " cannot be 0" }

Diagnostics-MissingFractionalPart = { "The decimal " }{ $value }{ " is missing its fractional part" }

Diagnostics-InvalidDoubleValue = { "Invalid decimal value: " }{ $value }

Diagnostics-DateValueTooLarge = { "The date value " }{ $value }{ " is too large" }

Diagnostics-NegativeRangeUnsupported = { "The range start for filter condition " }{ $predicate }{ " cannot be less than 0, got " }{ $value }

Diagnostics-RangeMinimumGreaterThanMaximum = The range minimum ({ $minimum }{ ") of filter condition " }{ $predicate }{ " must be less than or equal to the maximum (" }{ $maximum })

Diagnostics-DoubleRangeOpenIntervalUnsupported = { "Open-interval notation is not yet supported for the decimal range of filter condition " }{ $value }

Diagnostics-InvalidDate = { "Invalid date for filter condition " }{ $predicate }{ ": " }{ $value }

Diagnostics-UnsupportedValueKind = { "Filter condition " }{ $predicate }{ " uses an unsupported filter value type: " }{ $value }

Diagnostics-InternalExpectedTextValue = { "Internal filter error: filter condition " }{ $value }{ " expected a string value" }

Diagnostics-InternalExpectedLongValue = { "Internal filter error: filter condition " }{ $value }{ " expected an integer value" }

Diagnostics-InternalExpectedDoubleValue = { "Internal filter error: filter condition " }{ $value }{ " expected a decimal value" }

Diagnostics-InternalExpectedLongRangeValue = { "Internal filter error: filter condition " }{ $value }{ " expected an integer range value" }

Diagnostics-InternalExpectedDoubleRangeValue = { "Internal filter error: filter condition " }{ $value }{ " expected a decimal range value" }

Diagnostics-InternalExpectedDateValue = { "Internal filter error: filter condition " }{ $value }{ " expected a date value" }

Completions-And = Logical AND group, syntax: (and condition1 condition2)

Completions-Or = Logical OR group, syntax: (or condition1 condition2)

Completions-Not = Logical NOT; applies to the next condition or the whole group

Completions-Title = Filter by title

Completions-Tag = Filter by tag

Completions-Author = Filter by author

Completions-Bookmark = Filter by bookmark count

Completions-Ratio = Filter by image aspect ratio

Completions-StartDate = Filter by start publish date

Completions-EndDate = Filter by end publish date

Completions-Include-Constraint = Positive constraint

Completions-Include-R18 = Only R18 (including R18G)

Completions-Include-R18G = Only R18G

Completions-Include-Gif = Only animated images

Completions-Include-Ai = Only AI works

Completions-Exclude-Constraint = Negative constraint

Completions-Exclude-R18 = Exclude R18 (including R18G)

Completions-Exclude-R18G = Exclude R18G

Completions-Exclude-Gif = Exclude animated images

Completions-Exclude-Ai = Exclude AI works

Completions-ValueHints-Text-Plain = 普通的字符串

Completions-ValueHints-Text-Quoted = A string with spaces/special characters

Completions-ValueHints-Text-Exact = A string that exactly matches

Completions-ValueHints-Text-QuotedExact = A string that exactly matches with spaces/special characters

Completions-ValueHints-Long-Plain = Ordinary integer

Completions-ValueHints-Double-Integer = Decimal value in integer form

Completions-ValueHints-Double-Decimal = Decimal value in decimal form

Completions-ValueHints-Double-Fraction = Decimal value in fraction form

Completions-ValueHints-LongRange-Lower = Greater than or equal to 2

Completions-ValueHints-LongRange-Upper = Less than or equal to 3

Completions-ValueHints-LongRange-Closed = Between 2 and 3 inclusive

Completions-ValueHints-DoubleRange-Lower = Greater than or equal to 2

Completions-ValueHints-DoubleRange-UpperDecimal = Less than or equal to 1.5

Completions-ValueHints-DoubleRange-UpperFraction = Less than or equal to 1/2

Completions-ValueHints-DoubleRange-ClosedFraction = Between 1/2 and 3 inclusive

Completions-ValueHints-DoubleRange-ClosedDecimalFraction = Between 0.3 and 1/2 inclusive

Completions-ValueHints-Date-MonthDayDash = Month and day of this year, separated by '-'

Completions-ValueHints-Date-MonthDayDot = Month and day of this year, separated by '.'

Completions-ValueHints-Date-FullDash = Full date, separated by '-'

Completions-ValueHints-Date-FullDot = Full date, separated by '.'
