// *
// * DESIGNSTREAKS CONFIDENTIAL
// * __________________
// *
// *  Copyright © Design Streaks - 2010 - 2012
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

namespace System.Data.Common
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using DesignStreaks.Data.SqlClient;
    using Linq;

    /// <summary>Extension methods for the <see cref="System.Data.Common.DbDataReader"/> class.</summary>
    public static class DbDataReaderExtensions
    {
        /// <summary>Determines whether the specified column is contained in the reader..</summary>
        /// <param name="reader">The reader.</param>
        /// <param name="columnName">Name of the column.</param>
        /// <returns>
        ///   <c>true</c> if the reader contains the column; otherwise, <c>false</c>.
        /// </returns>
        public static bool Contains(this DbDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>Gets the value of the specified column as an instance of type T.</summary>
        /// <typeparam name="T">The type of element to return.</typeparam>
        /// <param name="reader">The reader.</param>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>This method returns default values for null database column values.</returns>
        public static T GetSafeValue<T>(this DbDataReader reader, int ordinal)
        {
            return !reader.IsDBNull(ordinal) ? (T)reader.GetValue(ordinal) : default(T);
        }

        /// <summary>Gets the value of the specified column as an instance of type T.</summary>
        /// <typeparam name="T">The type of element to return.</typeparam>
        /// <param name="reader">The reader.</param>
        /// <param name="columnName">Name of the column.</param>
        /// <returns>This method returns default values for null database column values or undefined columns.</returns>
        public static T GetSafeValue<T>(this DbDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.InvariantCultureIgnoreCase))
                    return reader.GetSafeValue<T>(i);
            }
            return default(T);
        }

        /// <summary>Gets the value of the specified column as an instance of type T.</summary>
        /// <typeparam name="T">The type of element to return.</typeparam>
        /// <param name="reader">The reader.</param>
        /// <param name="columnNames">The list of possible names of the column.</param>
        /// <returns>This method returns default values for null database column values or undefined columns.</returns>
        public static T GetSafeValue<T>(this DbDataReader reader, IList<string> columnNames)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                for (int c = 0; c < columnNames.Count; c++)
                {
                    if (reader.GetName(i).Equals(columnNames[c], StringComparison.InvariantCultureIgnoreCase))
                        return reader.GetSafeValue<T>(i);
                }
            }
            return default(T);
        }

        /// <summary>Create a list of <typeparamref name="T"/> entities from the <see cref="System.Data.Common.DbDataReader"/>.</summary>
        /// <param name="reader">The reader object.</param>
        /// <returns>Returns a list of <typeparamref name="T"/> objects.</returns>
        public static List<T> ToList<T>(this DbDataReader reader) where T : class, new()
        {
            List<T> results = new List<T>();

            if (!reader.HasRows)
                return results;

            Dictionary<string, Delegate> typeConverter = ReaderTypeConverter.GetTypeConverter(typeof(T));// TypeConverters[typeof(TModel).FullName];

            do
            {
                T item = reader.ToPoco<T>();

                results.Add(item);
            } while (reader.Read());

            return results;
        }

        /// <summary>Creates a <typeparamref name="T"/> entity from the <see cref="System.Data.Common.DbDataReader"/>.</summary>
        /// <param name="reader">The reader object.</param>
        /// <returns>Returns a <typeparamref name="T"/> object.</returns>
        public static T ToPoco<T>(this DbDataReader reader) where T : class, new()
        {
            T item = (T)Activator.CreateInstance(typeof(T), new object[0]);

            if (!reader.HasRows)
                return item;

            Dictionary<string, Delegate> typeConverter = ReaderTypeConverter.GetTypeConverter(typeof(T));

            typeof(T).GetProperties()
                .ToList()
                .ForEach(prop =>
                {
                    var setPropFunction = typeConverter[prop.Name] as Delegate;
                    if (setPropFunction == null)
                        return;

                    var output = setPropFunction.DynamicInvoke(new object[] { prop.Name, reader });

                    prop.SetValue(item, output, new object[0]);
                });

            return item;
        }
    }
}