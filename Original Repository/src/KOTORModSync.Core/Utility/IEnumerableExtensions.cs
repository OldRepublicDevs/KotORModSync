// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace KOTORModSync.Core.Utility
{

    public static class IEnumerableExtensions
    {

        public static bool IsNullOrEmptyCollection<T>(this IEnumerable<T> collection) =>
            IsNullOrEmptyCollectionInternal(collection);

        public static bool IsNullOrEmptyCollection(this IEnumerable collection) =>
            IsNullOrEmptyCollectionInternal(collection);

        public static bool IsNullOrEmptyOrAllNull<T>(this IEnumerable<T> collection) =>
            IsNullOrEmptyOrAllNullInternal(collection);

        public static bool IsNullOrEmptyOrAllNull(this IEnumerable collection) =>
            IsNullOrEmptyOrAllNullInternal(collection);

        private static bool IsNullOrEmptyCollectionInternal(IEnumerable collection) =>
            collection is null || !collection.Cast<object>().Any();

        private static bool IsNullOrEmptyOrAllNullInternal(IEnumerable collection) =>
            collection is null || collection.Cast<object>().All(item => item is null);
    }
}
