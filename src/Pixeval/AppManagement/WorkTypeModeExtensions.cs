using Mako.Global.Enum;

namespace Pixeval.AppManagement;

public static class WorkTypeModeExtensions
{
    public static SimpleWorkType ToSimpleWorkType(this WorkType workType)
    {
        return workType is WorkType.Novel
            ? SimpleWorkType.Novel
            : SimpleWorkType.IllustrationAndManga;
    }

    public static WorkType ToWorkType(this SimpleWorkType simpleWorkType, WorkType fallbackNonNovel)
    {
        if (simpleWorkType is SimpleWorkType.Novel)
            return WorkType.Novel;

        return fallbackNonNovel is WorkType.Manga
            ? WorkType.Manga
            : WorkType.Illustration;
    }
}
