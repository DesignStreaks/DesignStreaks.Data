// *
// * DESIGNSTREAKS CONFIDENTIAL
// * __________________
// *
// *  Copyright © Design Streaks - 2010 - 2020
// *  All Rights Reserved.
// *
// * NOTICE:  All information contained herein is, and remains
// * the property of DesignStreaks and its suppliers, if any.
// * The intellectual and technical concepts contained
// * herein are proprietary to DesignStreaks and its suppliers and may
// * be covered by Australian, U.S. and Foreign Patents,
// * patents in process, and are protected by trade secret or copyright law.
// * Dissemination of this information or reproduction of this material
// * is strictly forbidden unless prior written permission is obtained
// * from DesignStreaks.

using System.Diagnostics;

namespace System
{
    using System;
    using System.Linq;

    /// <summary>Some nice handy extensions.</summary>
    public static class GenericExtensions
    {
        /// <summary>Returns a flag indicating if the <paramref name="value" /> is in <paramref name="values" />.</summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="value">The value to check.</param>
        /// <param name="values">The list of values to check for.</param>
        /// <returns></returns>
        [DebuggerHidden]
        public static bool In<T>(this T value, params T[] values)
        {
            return values.Contains(value);
        }

        [DebuggerHidden]
        public static T ToEnum<T>(this string value) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
                throw new ArgumentException("T must be an Enumerable Type");

            var a = default(T);

            if (string.IsNullOrEmpty(value))
                throw new NullReferenceException("Value is null or empty.");

            if (!Enum.TryParse<T>(value, true, out a))
                throw new ArgumentException($"Enum type '{typeof(T).Name}' does ot contain the requested value `{value}'.");

            return a;
        }
    }
}