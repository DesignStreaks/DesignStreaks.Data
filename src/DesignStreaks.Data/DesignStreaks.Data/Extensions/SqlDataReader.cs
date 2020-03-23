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


namespace System.Data.SqlClient
{
    using System;
    using System.Collections.Generic;

    /// <summary>Extension methods for the <see cref="System.Data.SqlClient.SqlDataReader"/> class.</summary>
    public static class SqlDataReaderExtensions
    {
        /// <summary>Gets the value of the specified column as an instance of type T.</summary>
        /// <typeparam name="T">The type of element to return.</typeparam>
        /// <param name="reader">The reader.</param>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>This method returns default values for null database column values.</returns>
        public static T GetSafeValue<T>(this SqlDataReader reader, int ordinal)
        {
            return !reader.IsDBNull(ordinal) ? (T)reader.GetValue(ordinal) : default(T);
        }


        /// <summary>Gets the value of the specified column as an instance of type T.</summary>
        /// <typeparam name="T">The type of element to return.</typeparam>
        /// <param name="reader">The reader.</param>
        /// <param name="columnName">Name of the column.</param>
        /// <returns>This method returns default values for null database column values or undefined columns.</returns>
        public static T GetSafeValue<T>(this SqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.InvariantCultureIgnoreCase))
                    return reader.GetSafeValue<T>(i);
            }
            return default(T);
        }

        /// <summary>Determines whether the specified column is contained in the reader..</summary>
        /// <param name="reader">The reader.</param>
        /// <param name="columnName">Name of the column.</param>
        /// <returns>
        ///   <c>true</c> if the reader contains the column; otherwise, <c>false</c>.
        /// </returns>
        public static bool Contains(this SqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.InvariantCultureIgnoreCase))
                    return true;;
            }
            return false;
        }

        /// <summary>Creates a <typeparamref name="T"/> entity from the <see cref="System.Data.SqlClient.SqlDataReader"/>.</summary>
        /// <param name="reader">The reader object.</param>
        /// <param name="builder">Object used to build and item from a <see cref="System.Data.SqlClient.SqlDataReader"/>.</param>
        /// <returns>Returns a <typeparamref name="T"/> object.</returns>
        public static T BuildItem<T>(this SqlDataReader reader, DesignStreaks.Presentation.IBuilder<T, SqlDataReader> builder)
            where T: class
        {
            if (!reader.HasRows)
                return (T)Activator.CreateInstance(typeof(T), new object[] { });

            T result = builder.Build(reader);

            return result;
        }

        /// <summary>Create a list of <typeparamref name="T"/> entities from the <see cref="System.Data.SqlClient.SqlDataReader"/>.</summary>
        /// <param name="reader">The reader object.</param>
        /// <param name="builder">Object used to build and item from a <see cref="System.Data.SqlClient.SqlDataReader"/>.</param>
        /// <returns>Returns a list of <typeparamref name="T"/> objects.</returns>
        public static List<T> BuildList<T>(this SqlDataReader reader, DesignStreaks.Presentation.IBuilder<T, SqlDataReader> builder)
            where T: class
        {
            List<T> result = new List<T>();

            if (!reader.HasRows)
                return result;

            do
            {
                T item = builder.Build(reader);
                result.Add(item);
            } while (reader.Read());

            return result;
        }

    }

}
