FilterQueryError = Ошибка поискового фильтра

Diagnostics-CompletionSuffixFormatted = { " Доступно автодополнение: " }{ $completions }

Diagnostics-MessageWithPositionFormatted = { $message }{ " (позиция " }{ $position }{ ") " }{ $completionSuffix }

Diagnostics-UnexpectedToken = { "Обнаружено нераспознанное содержимое: " }{ $value }

Diagnostics-MissingPredicateAfterNegation = { "Отсутствует условие после отрицания " }{ $value }

Diagnostics-MissingTextValue = { "Отсутствует значение после фильтра " }{ $value }

Diagnostics-MissingLongValue = { "Отсутствует целое число после фильтра " }{ $value }

Diagnostics-MissingDoubleValue = { "Отсутствует дробное число после фильтра " }{ $value }

Diagnostics-MissingRangeValue = { "Отсутствует диапазон значений после фильтра " }{ $value }

Diagnostics-MissingDateValue = { "Отсутствует дата после фильтра " }{ $value }

Diagnostics-MissingGroupOperator = { "После ( требуется and или or, получено: " }{ $value }

Diagnostics-MissingRightParenthesis = { "Отсутствует закрывающая скобка ), соответствующая " }{ $value }

Diagnostics-InvalidValue = { "Недопустимое значение для фильтра " }{ $value }

Diagnostics-MissingStringQuote = { "Отсутствует закрывающая кавычка у строки " }{ $value }

Diagnostics-InvalidLongRangeFormat = { "Недопустимый формат целого диапазона " }{ $value }{ " для фильтра " }{ $predicate }, используйте a-b, -b или a-

Diagnostics-InvalidDoubleRangeFormat = { "Недопустимый формат дробного диапазона " }{ $value }{ " для фильтра " }{ $predicate }, используйте a-b, -b или a-

Diagnostics-DateRequiresMonthAndDay = { "Дата " }{ $value }{ " для фильтра " }{ $predicate }{ " должна содержать как минимум месяц и день" }

Diagnostics-ExpectedInteger = { "Ожидалось целое число, получено: " }{ $value }

Diagnostics-IntegerOutOfRange = { "Целое число " }{ $value }{ " выходит за допустимый диапазон" }

Diagnostics-DenominatorCannotBeZero = { "Знаменатель дроби " }{ $value }{ " не может быть равен 0" }

Diagnostics-MissingFractionalPart = { "У числа " }{ $value }{ " отсутствует дробная часть" }

Diagnostics-InvalidDoubleValue = { "Недопустимое дробное значение: " }{ $value }

Diagnostics-DateValueTooLarge = { "Значение даты " }{ $value }{ " слишком велико" }

Diagnostics-NegativeRangeUnsupported = { "Начало диапазона для фильтра " }{ $predicate }{ " не может быть меньше 0, получено: " }{ $value }

Diagnostics-RangeMinimumGreaterThanMaximum = Минимум диапазона ({ $minimum }{ ") фильтра " }{ $predicate }{ " должен быть меньше или равен максимуму (" }{ $maximum })

Diagnostics-DoubleRangeOpenIntervalUnsupported = { "Дробные диапазоны фильтра " }{ $value }{ " пока не поддерживают открытые интервалы" }

Diagnostics-InvalidDate = { "Недопустимая дата для фильтра " }{ $predicate }{ ": " }{ $value }

Diagnostics-UnsupportedValueKind = { "Использован неподдерживаемый тип значения " }{ $value }{ " для фильтра " }{ $predicate }

Diagnostics-InternalExpectedTextValue = { "Внутренняя ошибка фильтра: " }{ $value }{ " ожидает строковое значение" }

Diagnostics-InternalExpectedLongValue = { "Внутренняя ошибка фильтра: " }{ $value }{ " ожидает целое значение" }

Diagnostics-InternalExpectedDoubleValue = { "Внутренняя ошибка фильтра: " }{ $value }{ " ожидает дробное значение" }

Diagnostics-InternalExpectedLongRangeValue = { "Внутренняя ошибка фильтра: " }{ $value }{ " ожидает диапазон целых чисел" }

Diagnostics-InternalExpectedDoubleRangeValue = { "Внутренняя ошибка фильтра: " }{ $value }{ " ожидает диапазон дробных чисел" }

Diagnostics-InternalExpectedDateValue = { "Внутренняя ошибка фильтра: " }{ $value }{ " ожидает значение даты" }

Completions-And = Логическое И: (and условие1 условие2)

Completions-Or = Логическое ИЛИ: (or условие1 условие2)

Completions-Not = Логическое НЕ: применяется к следующему условию или группе

Completions-Title = Фильтр по названию

Completions-Tag = Фильтр по тегам

Completions-Author = Фильтр по автору

Completions-Bookmark = Фильтр по числу закладок

Completions-Ratio = Фильтр по соотношению сторон

Completions-StartDate = Фильтр по начальной дате

Completions-EndDate = Фильтр по конечной дате

Completions-Include-Constraint = Включающее ограничение

Completions-Include-R18 = Только R18 (включая R18G)

Completions-Include-R18G = Только R18G

Completions-Include-Gif = Только анимации (GIF)

Completions-Include-Ai = Только работы AI

Completions-Exclude-Constraint = Исключающее ограничение

Completions-Exclude-R18 = Исключить R18 (включая R18G)

Completions-Exclude-R18G = Исключить R18G

Completions-Exclude-Gif = Исключить анимации

Completions-Exclude-Ai = Исключить работы AI

Completions-ValueHints-Text-Plain = Обычная строка

Completions-ValueHints-Text-Quoted = Строка с пробелами или спецсимволами

Completions-ValueHints-Text-Exact = Точное совпадение

Completions-ValueHints-Text-QuotedExact = Точное совпадение с пробелами или спецсимволами

Completions-ValueHints-Long-Plain = Обычное целое число

Completions-ValueHints-Double-Integer = Дробное число в виде целого

Completions-ValueHints-Double-Decimal = Десятичная дробь

Completions-ValueHints-Double-Fraction = Простая дробь

Completions-ValueHints-LongRange-Lower = Больше или равно 2

Completions-ValueHints-LongRange-Upper = Меньше или равно 3

Completions-ValueHints-LongRange-Closed = От 2 до 3 включительно

Completions-ValueHints-DoubleRange-Lower = Больше или равно 2

Completions-ValueHints-DoubleRange-UpperDecimal = Меньше или равно 1.5

Completions-ValueHints-DoubleRange-UpperFraction = Меньше или равно 1/2

Completions-ValueHints-DoubleRange-ClosedFraction = От 1/2 до 3 включительно

Completions-ValueHints-DoubleRange-ClosedDecimalFraction = От 0.3 до 1/2 включительно

Completions-ValueHints-Date-MonthDayDash = День и месяц этого года через -

Completions-ValueHints-Date-MonthDayDot = День и месяц этого года через .

Completions-ValueHints-Date-FullDash = Полная дата через -

Completions-ValueHints-Date-FullDot = Полная дата через .
