// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Pixeval.Native.Download;

public static class SnapshotListDiff
{
    public static bool Matches<T, TKey>(
        IReadOnlyList<T> current,
        IReadOnlyList<T> incoming,
        Func<T, TKey> keyOf)
        where T : IEquatable<T>
        where TKey : notnull
    {
        if (current.Count != incoming.Count)
            return false;

        for (var i = 0; i < current.Count; i++)
        {
            if (!EqualityComparer<TKey>.Default.Equals(keyOf(current[i]), keyOf(incoming[i]))
                || !current[i].Equals(incoming[i]))
                return false;
        }

        return true;
    }

    public static void Apply<T, TKey>(
        ObservableCollection<T> target,
        IReadOnlyList<T> incoming,
        Func<T, TKey> keyOf)
        where T : IEquatable<T>
        where TKey : notnull
    {
        var incomingKeys = new HashSet<TKey>(incoming.Count);
        foreach (var item in incoming)
            incomingKeys.Add(keyOf(item));

        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (!incomingKeys.Contains(keyOf(target[index])))
                target.RemoveAt(index);
        }

        for (var index = 0; index < incoming.Count; index++)
        {
            var item = incoming[index];
            var key = keyOf(item);
            var existing = -1;
            for (var scan = 0; scan < target.Count; scan++)
            {
                if (EqualityComparer<TKey>.Default.Equals(keyOf(target[scan]), key))
                {
                    existing = scan;
                    break;
                }
            }

            if (existing < 0)
            {
                target.Insert(index, item);
                continue;
            }

            if (existing != index)
            {
                if (target[existing].Equals(item))
                    target.Move(existing, index);
                else
                {
                    target.RemoveAt(existing);
                    target.Insert(index, item);
                }

                continue;
            }

            if (!target[index].Equals(item))
                target[index] = item;
        }
    }
}
