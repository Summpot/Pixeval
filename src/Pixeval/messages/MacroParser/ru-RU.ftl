UnexpectedToken = { "Недопустимый символ: " }{ $token }

ExpectedLeftBraceAfterAt = { "После '@' должна следовать '{'" }

ExpectedMacroName = { "После '@{' должно следовать имя макроса" }

MissingRightBrace = Несоответствие скобок

MissingConditionalSeparator = В условном макросе отсутствует ':'

UnknownMacroName = { "Неизвестное имя макроса: '@{" }{ $macroName }{ "}'" }

NonParameterizedMacroBearingParameter = { "Макрос '@{" }{ $macroName }{ "}' не может содержать параметров" }

ConditionalBranchesMissing = { "Условный макрос '@{" }{ $macroName }{ "?:}' должен указывать как истинную, так и ложную ветки через ? и :" }

InvalidFormatter = { "Недопустимый параметр форматирования для макроса '@{" }{ $macroName }{ "}': '" }{ $formatter }'

MacroShouldBeInLastSegment = { "Макрос '@{" }{ $macroName }{ "}' должен использоваться в имени файла" }

MacroContextRestrictionNotSatisfied = { "Ограничение контекста макроса '@{" }{ $macroName }{ "}' не выполнено" }

MessageWithPositionFormatted = { $message }{ " (позиция " }{ $position })

MacroDescription-Ext = { "Расширение файла (например, jpg, png); без начальной точки, обычно пишется .@{ext}; должно быть в имени файла" }

MacroDescription-Id = ID работы (строка)

MacroDescription-Title = Название работы (строка)

MacroDescription-ArtistId = ID автора (строка)

MacroDescription-ArtistName = Имя автора (строка)

MacroDescription-SeriesId = { "ID серии (целое число); должно быть в истинной ветке условного макроса @{is_series?:}" }

MacroDescription-SeriesTitle = { "Название серии (строка); должно быть в истинной ветке условного макроса @{is_series?:}" }

MacroDescription-PublishTime = Время публикации работы (дата/время)

MacroDescription-PicSetIndex = { "Индекс текущего изображения в галерее; должно быть в истинной ветке @{is_pic_set?:} и в имени файла (число)" }

MacroDescription-GroupId = { "ID группы подписки на скачивание (целое число); должно быть в истинной ветке @{is_group?:}, @{is_bookmark_group?:}, @{is_post_group?:} или @{is_series_group?:}" }

MacroDescription-IsPicSet = Является ли работа галереей (много изображений)

MacroDescription-IsR18 = Является ли работа ограничением по возрасту (R18 или R18G)

MacroDescription-IsR18G = Является ли работа R18G

MacroDescription-IsAi = Сгенерирована ли работа ИИ (AI)

MacroDescription-IsPicOne = Является ли работа одиночным статическим изображением

MacroDescription-IsNovel = Является ли работа новеллой

MacroDescription-IsPicGif = Является ли работа анимацией (GIF/Ugoira)

MacroDescription-IsSeries = Принадлежит ли работа к серии

MacroDescription-IsGroup = Находится ли работа в группе подписки на скачивание

MacroDescription-IsBookmarkGroup = Находится ли работа в группе подписки закладок

MacroDescription-IsPostGroup = Находится ли работа в группе подписки публикаций

MacroDescription-IsSeriesGroup = Находится ли работа в группе подписки серий
