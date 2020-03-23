// *
// * DESIGNSTREAKS CONFIDENTIAL
// * __________________
// *
// *  Copyright © Design Streaks - 2010 - 2018
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

namespace DesignStreaks.Data.SqlClient
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading;

    /// <summary>Class for converting a DbDataReader result into a concrete class or list of classes.</summary>
    internal static class ReaderTypeConverter
    {
        /// <summary>Lock used for Read/Write lock creation.</summary>
        private static readonly ReaderWriterLockSlim __lock__ = new ReaderWriterLockSlim();

        /// <summary>Cached instance of the method info for the BuildGetValue method.</summary>
        private static readonly MethodInfo buildGetValueMethod = typeof(ReaderTypeConverter).GetMethod("BuildGetValue", BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>A dictionary containing the set of compiled expressions used to convert a DbDataReader row into a concrete class representation.</summary>
        private static Dictionary<string, Dictionary<string, Delegate>> typeConverters = new Dictionary<string, Dictionary<string, Delegate>>();

        /// <summary>Gets a dictionary of conversion functions for each property of the specified <paramref name="type"/>.</summary>
        /// <param name="type">The type to retrieve the dictionary for.</param>
        /// <returns>Dictionary&lt;System.String, Delegate&gt; containing the functions used to .</returns>
        public static Dictionary<string, Delegate> GetTypeConverter(Type type)
        {
            Dictionary<string, Delegate> typeConverter;

            if (!GetTypeConverterSync(type.FullName, out typeConverter))
            {
                typeConverter = new Dictionary<string, Delegate>();

                type.GetProperties()
                        .ToList()
                        .ForEach(prop =>
                        {
                            var buildGetValueGenericMethod = buildGetValueMethod.MakeGenericMethod(new[] { prop.PropertyType });
                            var x = buildGetValueGenericMethod.Invoke(null, new object[0]) as Delegate;
                            typeConverter.Add(prop.Name, x);
                        });

                AddTypeConverterSync(type.FullName, typeConverter);
            }

            return typeConverter;
        }

        /// <summary>Adds the type converter with locks.</summary>
        /// <param name="typeName">Name of the type.</param>
        /// <param name="value">The type converter.</param>
        private static void AddTypeConverterSync(string typeName, Dictionary<string, Delegate> value)
        {
            try
            {
                __lock__.EnterWriteLock();
                typeConverters.Add(typeName, value);
            }
            finally
            {
                if (__lock__.IsWriteLockHeld)
                    __lock__.ExitWriteLock();
            }
        }

        /// <summary>Builds the expression to get the value for a type from the a DbDataReader.</summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>Func&lt;System.String, DbDataReader, T&gt;.</returns>
        private static Func<string, DbDataReader, T> BuildGetValue<T>()
        {
            Trace.WriteLine($"{typeof(ReaderTypeConverter).Name }.BuildGetValue: Building Expression - {typeof(T).Name}");

            var value = Expression.Parameter(typeof(T), "value");                                                               // T value;

            var readerParam = Expression.Parameter(typeof(DbDataReader), "reader");                                             // DbDataReader reader;
            var columnNameParam = Expression.Parameter(typeof(string), "columnName");                                           // string columnName;
            var indexParam = Expression.Parameter(typeof(int), "i");                                                            // int i;

            var fieldCountProp = Expression.Property(readerParam, nameof(DbDataReader.FieldCount));                             // reader.FieldCount
                                                                                                                                //
            var getNameCall = Expression.Call(                                                                                  // reader.GetName(i)
                    readerParam,                                                                                                //
                    typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetName)),                                               //
                    indexParam);                                                                                                //
                                                                                                                                //
            var getNameEqualsCall = Expression.Call(                                                                            // reader.GetName(i).Equals(columnName, StringComparison.InvariantCultureIgnoreCase)
                    getNameCall,                                                                                                //
                    typeof(string).GetMethod(nameof(string.Equals), new[] { typeof(string), typeof(StringComparison) }),        //
                    new Expression[] { columnNameParam, Expression.Constant(StringComparison.InvariantCultureIgnoreCase) });    //
                                                                                                                                //
            var isDbNullCall = Expression.Call(                                                                                 // reader.IsDBNull(i)
                    readerParam,                                                                                                //
                    typeof(DbDataReader).GetMethod(nameof(DbDataReader.IsDBNull), new[] { typeof(int) }),                       //
                    indexParam);                                                                                                //
                                                                                                                                //
            var readerGetValueCall = Expression.Call(                                                                           // reader.GetValue(i)
                    readerParam,                                                                                                //
                    typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetValue), new[] { typeof(int) }),                       //
                    indexParam);                                                                                                //

            MethodCallExpression stringToEnum = null;
            if (typeof(T).BaseType == typeof(Enum))
            {                                                                                                                   //
                stringToEnum = Expression.Call(                                                                                 // GenericExtensions.ToEnum<T>(valueString)
                    typeof(GenericExtensions).GetMethod(nameof(GenericExtensions.ToEnum), new[] { typeof(string) })             //
                        .MakeGenericMethod(typeof(T)),                                                                          //
                    Expression.Convert(readerGetValueCall, typeof(string)));                                                    //
            }                                                                                                                   //

            var returnLabel = Expression.Label(typeof(T));                                                                      //
            
            var forBlock = Expression.Block(
                new[] { indexParam, value },                                                                                    //
                Expression.Assign(indexParam, Expression.Constant(0)),                                                          // var i = 0
                Expression.Loop(                                                                                                // while(true)
                    Expression.Block(                                                                                           // {
                        Expression.IfThenElse(                                                                                  //     if(i < reader.FieldCount)
                            Expression.LessThan(indexParam, fieldCountProp),                                                    //     {
                            Expression.IfThen(                                                                                  //         if(reader.GetName(i).Equals(columnName, StringComparison.InvariantCultureIgnoreCase) == true)
                                Expression.Equal(getNameEqualsCall, Expression.Constant(true)),                                 //         {
                                Expression.IfThenElse(                                                                          //             if(reader.IsDBNull(i)
                                    Expression.Equal(isDbNullCall, Expression.Constant(true)),                                  //             {
                                    Expression.Block(                                                                           //
                                        Expression.Assign(value, Expression.Default(typeof(T))),                                //                 value = Default(DateTime)
                                        Expression.Return(returnLabel, value)                                                   //                 return value
                                    ),                                                                                          //              }
                                    typeof(T).BaseType == typeof(Enum)                                                          // #IF typeof(T).BaseType = typeof(Enum)
                                        ? Expression.Block(                                                                     //              else {
                                            // ReSharper disable once AssignNullToNotNullAttribute
                                            Expression.Assign(value, stringToEnum),                                             //                  value = ((string)reader.GetValue(i)).ToEnum<T>();
                                            Expression.Return(returnLabel, value)                                               //                  return value
                                        )                                                                                       //              }
                                        : Expression.Block(                                                                     // #ELSE        else{
                                            Expression.Assign(value, Expression.Convert(readerGetValueCall, typeof(T))),        //                  value = (T)(reader.GetValue(i))
                                            Expression.Return(returnLabel, value)                                               //                  return value
                                        )                                                                                       //              }
                                )                                                                                               //
                            ),                                                                                                  //         }
                            Expression.Block(                                                                                   //         else {
                                Expression.Assign(value, Expression.Default(typeof(T))),                                        //             value = Default(DateTime)
                                Expression.Return(returnLabel, value)                                                           //             return value
                            )                                                                                                   //         }
                        ),                                                                                                      //    }
                        Expression.PostIncrementAssign(indexParam)                                                              //    i++;
                    ),                                                                                                          // }
                    returnLabel                                                                                                 //    returnLabel:
                )                                                                                                               //

            );

            return Expression.Lambda<Func<string, DbDataReader, T>>(forBlock, columnNameParam, readerParam).Compile();
        }

        /// <summary>Gets the associated type converter for the specified key. Uses read lock.</summary>
        /// <param name="key">The key of the type converter to get.</param>
        /// <param name="value">
        ///   When this method returns, contains the value associated with the specified key, if the key is found; otherwise <c>null</c>.
        ///   This parameter is passed uninitialized.
        /// </param>
        /// <returns><c>true</c> if the dictionary contains the contains a type converter with the specified type name; otherwise <c>false</c>.</returns>
        private static bool GetTypeConverterSync(string key, out Dictionary<string, Delegate> value)
        {
            value = null;

            try
            {
                __lock__.EnterReadLock();
                return typeConverters.TryGetValue(key, out value);
            }
            finally
            {
                //Trace.WriteLine($"{typeof(ReaderTypeConverter).Name }.{nameof(GetTypeConverter)}: Cache {(value != null ? "Hit" : "Miss")} - {key}", "Verbose");

                if (__lock__.IsReadLockHeld)
                    __lock__.ExitReadLock();
            }
        }
    }
}