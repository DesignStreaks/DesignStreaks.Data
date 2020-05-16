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

// ReSharper disable BadControlBracesIndent
namespace DesignStreaks.Data.SqlClient.Types
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlTypes;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.SqlServer.Server;

    /// <summary>
    ///   Extention methods to create <see cref="IEnumerable{T}" /> of
    ///   <see cref="T:Microsoft.SqlServer.Server.SqlDataRecord" /> objects from any <see cref="System.Collections.Generic.IEnumerable{T}" />.
    /// </summary>
    public static class TableValuedParameterExtensions
    {
        /// <summary>
        ///   Translates any IEnumerable into a Table Value Parameter for passing through to Sql Server stored procedures.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parameterList">The list of items to convert to a <see cref="TableValuedParameter{T}" />.</param>
        /// <param name="includedColumns">
        ///   The list of column names to include in being mapped to the <see cref="TableValuedParameter{T}" />. If null,
        ///   all fields with <see cref="DbColumnAttribute" /> are mapped.
        /// </param>
        /// <param name="excludedColumns">The list of column names to exclude from being mapped to the <see cref="TableValuedParameter{T}" />.</param>
        /// <returns>
        ///   Returns an enumeration of <see cref="T:Microsoft.SqlServer.Server.SqlDataRecord" /> items to be used as a
        ///   parameter input into a SqlServer table valued parameter.
        /// </returns>
        /// <example>
        ///   The following example will only map the fields <c>Id</c> and <c>Code</c> from each item in the
        ///   <see cref="IEnumerable{T}" /><c>list</c> of a class with a single field
        ///   <see cref="T:Microsoft.SqlServer.Server.SqlDataRecord" /> objects.
        ///   <code lang="cs">
        /// public class SomeClass
        /// {
        ///     [DbColumnAttribute(1)]
        ///     public string Code { get; set; }
        ///     public string Description { get; set; }
        ///     [DbColumnAttribute(0)]
        ///     public int Id { get; set; }
        /// }
        ///
        /// IEnumerable&lt;SomeClass&gt; list = new List&lt;SomeClass&gt;();
        /// list.ToTableValuedParameter();
        ///   </code>
        /// </example>
        public static TableValuedParameter<T> ToTableValuedParameter<T>(this IEnumerable<T> parameterList, IEnumerable<string> includedColumns = null, IEnumerable<string> excludedColumns = null) where T : new()
        {
            return new TableValuedParameter<T>(parameterList, includedColumns, excludedColumns);
        }
    }

    /// <summary>
    ///   Represents an <see cref="IEnumerable{T}" /> of a specified type as
    ///   <see cref="T:Microsoft.SqlServer.Server.SqlDataRecord" /> objects for passing a Sql Server stored procedure
    /// </summary>
    /// <typeparam name="T">
    ///   The type of the item to translate to a <see cref="T:Microsoft.SqlServer.Server.SqlDataRecord" /> object.
    /// </typeparam>
    /// <seealso cref="System.Collections.Generic.IEnumerable{T}" />
    /// <seealso cref="T:Microsoft.SqlServer.Server.SqlDataRecord" />
    public class TableValuedParameter<T> : IEnumerable<SqlDataRecord> where T : new()
    {
#pragma warning disable RECS0108 // Warns about static fields in generic types

        private static SortedList<int, FunctionStub> propertyAccessors;

        /// <summary>Dictionary of Native to Sql type mappings.</summary>
        private static readonly Dictionary<Type, SqlDataRecordMethod> sdrDataTypeMappings;

#pragma warning restore RECS0108 // Warns about static fields in generic types

        private readonly IEnumerable<string> excludedColumns;

        private readonly IEnumerable<string> includedColumns;

        private readonly IEnumerable<T> parameterList;

        private static SortedList<int, FunctionStub> PropertyAccessors => propertyAccessors;

        /// <summary>Initializes static members of the <see cref="TableValuedParameter{T}"/> class.</summary>
        static TableValuedParameter()
        {
            sdrDataTypeMappings = DefineTypeMappingMethods();
            BuildPropertyAccessors();
        }

        /// <summary>Initializes a new instance of the <see cref="TableValuedParameter{T}" /> class.</summary>
        /// <param name="parameterList">The list of items to convert to a <see cref="TableValuedParameter{T}" />.</param>
        /// <param name="includedColumns">
        ///   The list of column names to include in being mapped to the <see cref="TableValuedParameter{T}" />. If null,
        ///   all fields with <see cref="T:DesignStreaks.Data.DbColumnAttribute" /> are mapped.
        /// </param>
        /// <param name="excludedColumns">The list of column names to exclude from being mapped to the <see cref="TableValuedParameter{T}" />.</param>
        public TableValuedParameter(IEnumerable<T> parameterList, IEnumerable<string> includedColumns = null, IEnumerable<string> excludedColumns = null)
        {
            this.parameterList = parameterList;
            this.includedColumns = includedColumns ?? new string[0];
            this.excludedColumns = excludedColumns ?? new string[0];
        }

        /// <summary>Returns a <see cref="SqlDataRecord"/> enumerator that iterates through the collection.</summary>
        /// <returns>
        ///   A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        IEnumerator<SqlDataRecord> IEnumerable<SqlDataRecord>.GetEnumerator()
        {
            var finalMetaDataParams = new List<SqlMetaData>();
            var finalPropertySetters = new List<FunctionStub>();

            var mdIndex = 0;

            var hasDbColumnAttributes = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Any(prop => prop.IsDefined(typeof(DbColumnAttribute), false));

            foreach (var fs in propertyAccessors.Values)
            {
                // A column is included if
                // a) it is not explicitly excluded (overrides all other rules).
                // b) it has the DbColumnAttribute attribute.
                // c) it is explicitly included.
                // d) no field has the DbColumnAttribute and the field.
                if ((!excludedColumns.Contains(fs.PropertyName) 
                     && (
                         hasDbColumnAttributes && fs.ColumnOrder >= 0
                        || includedColumns.Contains(fs.PropertyName)
                        || (!hasDbColumnAttributes && !this.includedColumns.Any())))
                )
                {
                    if (fs.MetaTypeEnum == SqlDbType.VarBinary)
                    {
                        // Must specify max length for varbinary - md5 requires 16
                        finalMetaDataParams.Add(new SqlMetaData(
                                        name: fs.PropertyName,
                                        dbType: fs.MetaTypeEnum,
                                        maxLength: 8000L,
                                        useServerDefault: false,
                                        isUniqueKey: true,
                                        columnSortOrder: System.Data.SqlClient.SortOrder.Ascending,
                                        sortOrdinal: mdIndex++)
                        );
                    }
                    else
                    {
                        finalMetaDataParams.Add(new SqlMetaData(
                                        name: fs.PropertyName,
                                        dbType: fs.MetaTypeEnum,
                                        useServerDefault: false,
                                        isUniqueKey: true,
                                        columnSortOrder: System.Data.SqlClient.SortOrder.Ascending,
                                        sortOrdinal: mdIndex++)
                        );
                    }

                    finalPropertySetters.Add(fs);
                }
            }

            var sdr = new SqlDataRecord(finalMetaDataParams.ToArray());

            foreach (var item in parameterList)
            {
                for (var i = 0; i < finalPropertySetters.Count; ++i)
                {
                    finalPropertySetters[i].Setter(sdr, i, item);
                }
                yield return sdr;
            }
        }

        /// <summary>Returns an enumerator that iterates through a collection.</summary>
        /// <returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return PropertyAccessors.GetEnumerator();
        }

        /// <summary>Builds the property accessors.</summary>
        private static void BuildPropertyAccessors()
        {
            propertyAccessors = new SortedList<int, FunctionStub>();

            // If the type contains any property with a DbColumnAttribute, those columns will be sorted first followed by
            // all additional columns.
            var numDbColumnAttributes = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Count(prop => prop.IsDefined(typeof(DbColumnAttribute), false));

            var invalidMappings = new List<string>();
            var paramIndex = 0;

            typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Where(prop => prop.CanRead)

                        // We need to order the fields by their DbColumnAttribute.Order value first, then
                        .OrderBy(prop =>
                        {
                            paramIndex++;
                            var ca = prop.GetCustomAttribute<DbColumnAttribute>();

                            return ca?.Order ?? numDbColumnAttributes + paramIndex;
                        })
                        .ToList()
                        .ForEach(prop =>
                        {
                            // We need to check for a mapping for the PropertyType and also the PropertyType.BaseType. If
                            // a property is defined as an enum data type (Gender for example), the "prop.PropertyType"
                            // will be "Gender". Since it is not possible to create a mapping for all known (and unknown)
                            // enum data types, a single mapping is defined for all Enums. Therefore, we need to use the
                            // BaseType of the property type.
                            if (sdrDataTypeMappings.ContainsKey(prop.PropertyType) || sdrDataTypeMappings.ContainsKey(prop.PropertyType.BaseType))
                            {
                                var fStub = CreatePropertyAccessorFunctionStub(prop, ref paramIndex);

                                propertyAccessors.Add(paramIndex++, fStub);
                            }
                            else
                            {
                                invalidMappings.Add(prop.PropertyType.Name);
                            }
                        });

            if (invalidMappings.Any())
            {
                throw new AggregateException(invalidMappings.Select(im => new InvalidOperationException($"The type '{ im }' does not have a mapping defined.")));
            }
        }

        /// <summary>Creates a lambda expression for setting the SqlDataRecord field with the property value.</summary>
        /// <param name="prop">The property.</param>
        /// <param name="setterMethod">The <see cref="SqlDataRecord"/> setter method.</param>
        /// <returns>
        ///   Returns an expression of type <see cref="T:System.Action{T1, T2, T3}">Action&lt;SqlDataRecord, System.Int32,
        ///   T&gt;</see> containing the expression used to set the <see cref="T:Microsoft.SqlServer.Server.SqlDataRecord"/> field with the property value.
        /// </returns>
        private static Expression<Action<SqlDataRecord, int, T>> CreatePropertyAccessorFunctionExpression(PropertyInfo prop, SqlDataRecordMethod setterMethod)
        {
            var instanceParam = Expression.Parameter(typeof(T), "obj");                                                 // | T {obj};
                                                                                                                        // |
            var memberAccessExpression = Expression.MakeMemberAccess(instanceParam, prop);                              // | MemberExpression {memberAccessExpression} = {obj}.prop;
                                                                                                                        // |
            Expression bodyExpression;                                                                                  // | Expression bodyExpression = null;
                                                                                                                        // |
            // The Enum data type has been defined to pass the Enum value into a SqlString field of the Table Parameter // |
            // but the Enum data type cannot be automatically converted to a String object which causes the             // |
            // "Expression.Convert(memberAccessExpression, setter.SqlDataType)" to throw an exception.                  // |
            // As such, we need to call the "ToString" function on the value.                                           // |
            if (prop.PropertyType.BaseType?.Name.In("Enum", "System.Enum") ?? false)                                    // | if (prop.PropertyType.BaseType.Name.In(new[] { "Enum", "System.Enum" }))
            {                                                                                                           // | {
                Expression toStringCall = Expression.Call(                                                              // |
                            memberAccessExpression,                                                                     // |
                            "ToString",                                                                                 // |
                            null);                                                                                      // |     Expression {toStringCall} = {memberAccessExpression}.ToString();
                bodyExpression = Expression.Convert(toStringCall, setterMethod.SqlDataType);                            // |     bodyExpression = Convert({toStringCall});
            }                                                                                                           // | }
            else                                                                                                        // | else
            {                                                                                                           // | {
                // ReSharper disable once AssignNullToNotNullAttribute
                bodyExpression = prop.PropertyType == typeof(byte[])                                                    // |     bodyExpression = prop.PropertyType == typeof(byte[])
                            ? (Expression)(Expression.New(                                                              // |            ? new SqlBinary({memberAccessExpression})
                                            typeof(SqlBinary).GetConstructor(new[] { typeof(byte[]) }),                 // |
                                            memberAccessExpression))                                                    // |
                            : (Expression)(Expression.Convert(memberAccessExpression, setterMethod.SqlDataType));       // |            : Convert({memberAccessExpression})
            }                                                                                                           // | }
                                                                                                                        // |
            var sdrParam = Expression.Parameter(typeof(SqlDataRecord), "sdr");                                          // | SqlDataRecord {sdr};
            var indexParam = Expression.Parameter(typeof(int), "i");                                                    // | int {i};
                                                                                                                        // |
            var sdrSetMethod = Expression.Call(                                                                         // |
                            sdrParam,                                                                                   // |
                            setterMethod.SetterMethodInfo,                                                              // |
                            indexParam,                                                                                 // |
                            bodyExpression);                                                                            // | sdrSetMethod = {sdr}.{setterMethod.MethodInfo}(i, {bodyExpression})
                                                                                                                        // |
            return Expression.Lambda<Action<SqlDataRecord, int, T>>(                                                    // |
                            sdrSetMethod,                                                                               // |
                            sdrParam,                                                                                   // |
                            indexParam,                                                                                 // |
                            instanceParam);                                                                             // | return (sdr, i, obj) => {sdrSetMethod}
                                                                                                                        // | ie. (sdr, i, obj) => sdr.SetSqlDateTime(i, Convert(obj.Property1))
        }

        /// <summary>Creates the property accessor function stub.</summary>
        /// <param name="prop">The property.</param>
        /// <param name="paramIndex">Index of the parameter.</param>
        /// <returns>FunctionStub.</returns>
        private static FunctionStub CreatePropertyAccessorFunctionStub(PropertyInfo prop, ref int paramIndex)
        {
            var setterMethod = sdrDataTypeMappings.ContainsKey(prop.PropertyType)
                        ? sdrDataTypeMappings[prop.PropertyType]
                        : sdrDataTypeMappings[prop.PropertyType.BaseType];

            var lambda = CreatePropertyAccessorFunctionExpression(prop, setterMethod);
            var ca = prop.GetCustomAttribute<DbColumnAttribute>();

            var fStub = new FunctionStub
            (
                prop.Name,
                setterMethod.TypeEnum,
                lambda.Compile(),
                ca?.Order ?? -1
            );


            paramIndex = ca?.Order ?? paramIndex;

            return fStub;
        }

        /// <summary>Defines the SqlDataRecord type mapping methods to convert between C# and Sql types.</summary>
        /// <returns>Dictionary&lt;Type, SqlDataRecordMethod&gt;.</returns>
        private static Dictionary<Type, SqlDataRecordMethod> DefineTypeMappingMethods()
        {
            var sdrMethodNames = new Dictionary<Type, SqlDataRecordMethod>();
            var sdrType = typeof(SqlDataRecord);

            new List<SqlTypeMappingDefinition>() {
                new SqlTypeMappingDefinition(typeof(byte[]), typeof(SqlBinary), "SetSqlBinary", SqlDbType.VarBinary),
                new SqlTypeMappingDefinition(typeof(bool), typeof(SqlBoolean), "SetSqlBoolean", SqlDbType.Bit),
                new SqlTypeMappingDefinition(typeof(byte), typeof(SqlByte), "SetSqlByte", SqlDbType.TinyInt),

                new SqlTypeMappingDefinition(typeof(int), typeof(SqlInt32), "SetSqlInt32", SqlDbType.Int),
                new SqlTypeMappingDefinition(typeof(short), typeof(SqlInt16), "SetSqlInt16", SqlDbType.SmallInt),
                new SqlTypeMappingDefinition(typeof(long), typeof(SqlInt64), "SetSqlInt64", SqlDbType.BigInt),
                new SqlTypeMappingDefinition(typeof(decimal), typeof(SqlDecimal), "SetSqlDecimal", SqlDbType.Decimal),
                new SqlTypeMappingDefinition(typeof(float), typeof(SqlDouble), "SetSqlFloat", SqlDbType.Float),

                new SqlTypeMappingDefinition(typeof(string), typeof(SqlString), "SetSqlString", SqlDbType.Text),

                new SqlTypeMappingDefinition(typeof(DateTime), typeof(SqlDateTime), "SetSqlDateTime", SqlDbType.DateTime),
                new SqlTypeMappingDefinition(typeof(Guid), typeof(SqlGuid), "SetSqlGuid", SqlDbType.UniqueIdentifier),

                // Enums are to be stored in the database as the string representation of the enum value 'name'.
                new SqlTypeMappingDefinition(typeof(Enum), typeof(SqlString), "SetSqlString", SqlDbType.Text),

                //new SqlTypeMappingDefinition(typeof(byte[]),    typeof(SqlBytes), "SetSqlBytes", SqlDbType.Binary),
                //new SqlTypeMappingDefinition(typeof(char[]),    typeof(SqlChars), "SetSqlChars", SqlDbType.NVarChar),
                //new SqlTypeMappingDefinition(typeof(char),      typeof(SqlChars), "SetSqlChar", SqlDbType.Char),

            }
                .ForEach(md =>
                {
                    sdrMethodNames.Add(
                                md.NativeType,
                                new SqlDataRecordMethod
                                {
                                    SqlDataType = md.SqlDataType,
                                    SetterMethodInfo = sdrType.GetMethod(md.SetterMethod),
                                    TypeEnum = md.SqlMetaType
                                }
                    );
                });

            return sdrMethodNames;
        }

        private struct SqlDataRecordMethod
        {
            /// <summary>The method info of the method on the SqlDataRecord used to set the property.</summary>
            public MethodInfo SetterMethodInfo { get; set; }

            /// <summary>The .Net Sql data type of the field.</summary>
            public Type SqlDataType { get; set; }

            /// <summary>Specifies Sql Server-specific data type of the field.</summary>
            public SqlDbType TypeEnum { get; set; }
        }

        /// <summary>Simple struct to define the mapping methods to convert between C# and Sql types.</summary>
        private struct SqlTypeMappingDefinition
        {
            /// <summary>The method on the SqlDataRecord used to set the property.</summary>
            public readonly string SetterMethod;

            /// <summary>The native (.NET) type to be mapped.</summary>
            public readonly Type NativeType;

            /// <summary>The Sql data type to map to.</summary>
            public readonly Type SqlDataType;

            /// <summary>The Sql enum representation of the Sql data type.</summary>
            public readonly SqlDbType SqlMetaType;

            /// <summary>Initializes a new instance of the <see cref="SqlTypeMappingDefinition"/> struct.</summary>
            /// <param name="nativeType">The native (.NET) type to be mapped.</param>
            /// <param name="sqlDataType">The Sql type to map to.</param>
            /// <param name="setterMethod">The method on the SqlDataRecord used to set the property.</param>
            /// <param name="sqlMetaType">The Sql enum representation of the Sql data type.</param>
            public SqlTypeMappingDefinition(Type nativeType, Type sqlDataType, string setterMethod, SqlDbType sqlMetaType)
            {
                this.NativeType = nativeType;
                this.SqlDataType = sqlDataType;
                this.SetterMethod = setterMethod;
                this.SqlMetaType = sqlMetaType;
            }
        }

        private class FunctionStub
        {
            /// <summary>Initializes a new instance of the <see cref="FunctionStub" /> class.</summary>
            /// <param name="propertyName">The name of the property to set.</param>
            /// <param name="metaTypeEnum">The Sql enum representation of the Sql data type.</param>
            /// <param name="setter">The <see cref="Action" /> used to set the property.</param>
            /// <param name="columnOrder">The position the column should appear in the table parameter.</param>
            public FunctionStub(string propertyName, SqlDbType metaTypeEnum, Action<SqlDataRecord, int, T> setter, int columnOrder)
            {
                this.PropertyName = propertyName;
                this.MetaTypeEnum = metaTypeEnum;
                this.Setter = setter;
                this.ColumnOrder = columnOrder;
            }

            /// <summary>The position the column should appear in the table parameter.</summary>
            public readonly int ColumnOrder;

            /// <summary>The Sql enum representation of the Sql data type.</summary>
            public readonly SqlDbType MetaTypeEnum;

            /// <summary>The name of the property to set.</summary>
            /// <value>The name of the property.</value>
            public readonly string PropertyName;

            /// <summary>The <see cref="Action"/> used to set the property.</summary>
            /// <value>The setter.</value>
            public readonly Action<SqlDataRecord, int, T> Setter;
        }
    }
}