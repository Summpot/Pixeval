// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;

namespace Pixeval.Models;

public readonly record struct SafeRating(sbyte SafeRatingValue)
{
    public static SafeRating NotSpecified => new(-1);
    public static SafeRating General => new(10);
    public static SafeRating Sensitive => new(20);
    public static SafeRating Questionable => new(30);
    public static SafeRating Explicit => new(40);
    public static SafeRating Guro => new(50);

    public bool IsNotSpecified => SafeRatingValue == -1;
    public bool IsGeneral => SafeRatingValue == 10;
    public bool IsSensitive => SafeRatingValue == 20;
    public bool IsQuestionable => SafeRatingValue == 30;
    public bool IsExplicit => SafeRatingValue == 40;
    public bool IsGuro => SafeRatingValue == 50;

    public bool IsSafe => SafeRatingValue is 10 or -1;
    public bool IsR17 => SafeRatingValue >= 20;
    public bool IsR18 => SafeRatingValue >= 40;
    public bool IsR18G => SafeRatingValue >= 50;

    public override string ToString() => SafeRatingValue switch
    {
        10 => nameof(General),
        20 => nameof(Sensitive),
        30 => nameof(Questionable),
        40 => nameof(Explicit),
        50 => nameof(Guro),
        _ => nameof(NotSpecified)
    };

    public static SafeRating Parse(string value)
    {
        if (TryParse(value, out var result))
            return result;
        throw new FormatException($"Invalid safe rating: '{value}'");
    }

    public static bool TryParse(string? value, out SafeRating result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = NotSpecified;
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "general":
            case "safe":
                result = General;
                return true;
            case "sensitive":
                result = Sensitive;
                return true;
            case "questionable":
                result = Questionable;
                return true;
            case "explicit":
                result = Explicit;
                return true;
            case "guro":
                result = Guro;
                return true;
            case "notspecified":
            case "-1":
                result = NotSpecified;
                return true;
            default:
                if (sbyte.TryParse(value, out var sbyteVal))
                {
                    result = new SafeRating(sbyteVal);
                    return true;
                }
                result = NotSpecified;
                return false;
        }
    }
}
