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

    /// <summary>Translates any IEnumerable into a Table Value Parameter for passing through to Sql Server stored procedures..</summary>
    /// <typeparam name="T">The list item type.</typeparam>
    /// <seealso cref="System.Collections.Generic.IEnumerable{T}" />
    /// <seealso cref="Microsoft.SqlServer.Server.SqlDataRecord"/>
    public class TableValuedParameter<T> : IEnumerable<SqlDataRecord> where T : new()
    {
#pragma warning disable RECS0108 // Warns about static fields in generic types
        private static SortedList<int, FunctionStub> propertyAccessors;

        /// <summary>Dictionary of Native to Sql type mappings.</summary>
        private static Dictionary<Type, SqlDataRecordMethod> sdrDataTypeMappings;

#pragma warning restore RECS0108 // Warns about static fields in generic types

        private IEnumerable<string> excludedColumns;

        private IEnumerable<string> includedColumns;

        private IEnumerable<T> parameterList { get; set; }

        private SortedList<int, FunctionStub> PropertyAccessors
        {
            get
            {
                return propertyAccessors;
            }
        }

        /// <summary>Initializes static members of the <see cref="TableValuedParameter{T}"/> class.</summary>
        static TableValuedParameter()
        {
            sdrDataTypeMappings = DefineTypeMappingMethods();
            BuildPropertyAccessors();
        }

        /// <summary>Initializes a new instance of the <see cref="TableValuedParameter{T}"/> class.</summary>
        /// <param name="parameterList">The list of items to convert to a <see cref="TableValuedParameter{T}"/>.</param>
        /// <param name="includedColumns">
        ///   The list of column names to include in being mapped to the <see cref="TableValuedParameter{T}"/>. If null,
        ///   all fields with <see cref="DbColumnAttribute"/> are mapped.
        /// </param>
        /// <param name="excludedColumns">The list of column names to exclude from being mapped to the <see cref="TableValuedParameter{T}"/>.</param>
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
            List<SqlMetaData> finalMetaDataParams = new List<SqlMetaData>();
            List<FunctionStub> finalPropertySetters = new List<FunctionStub>();

            int mdIndex = 0;

            var hasDbColumnAttributes = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Any(prop => prop.IsDefined(typeof(DbColumnAttribute), false));

            foreach (var fs in propertyAccessors.Values)
            {
                // A column is included if
                // a) it is not explicitly excluded (overrides all other rules).
                // b) it has the DbColumnAttribute attribute.
                // c) it is explicitly included.
                // d) no field has the DbColumnAttribute and the field.
                if ((!excludedColumns.Contains(fs.PropertyName) && ((hasDbColumnAttributes && fs.ColumnOrder >= 0)
                        || includedColumns.Contains(fs.PropertyName)
                        || (!hasDbColumnAttributes && includedColumns.Count() == 0)))
                        )
                {
                    if (fs.MetaTypeEnum == SqlDbType.VarBinary)
                    {
                        // Must specific max length for varbinary - md5 requires 16
                        finalMetaDataParams.Add(new SqlMetaData(
                                        fs.PropertyName,
                                        fs.MetaTypeEnum,
                                        maxLength: 2048,
                                        useServerDefault: false,
                                        isUniqueKey: true,
                                        columnSortOrder: System.Data.SqlClient.SortOrder.Ascending,
                                        sortOrdinal: mdIndex++)
                        );
                    }
                    else
                    {
                        finalMetaDataParams.Add(new SqlMetaData(
                                        fs.PropertyName,
                                        fs.MetaTypeEnum,
                                        useServerDefault: false,
                                        isUniqueKey: true,
                                        columnSortOrder: System.Data.SqlClient.SortOrder.Ascending,
                                        sortOrdinal: mdIndex++)
                        );
                    }

                    finalPropertySetters.Add(fs);
                }
            }

            SqlDataRecord sdr = new SqlDataRecord(finalMetaDataParams.ToArray());

            foreach (T item in parameterList)
            {
                for (int i = 0; i < finalPropertySetters.Count; ++i)
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
            return this.PropertyAccessors.GetEnumerator();
        }

        /// <summary>Builds the property accessors.</summary>
        private static void BuildPropertyAccessors()
        {
            propertyAccessors = new SortedList<int, FunctionStub>();

            string output = String.Empty;

            // If the type contains any property with a DbColumnAttribute, those columns will be sorted first followed by
            // all additional columns.
            var numDbColumnAttributes = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Count(prop => prop.IsDefined(typeof(DbColumnAttribute), false));

            List<string> invalidMappings = new List<string>();
            int paramIndex = 0;

            typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Where(prop => prop.CanRead)

                        // We need to order the fields by their DbColumnAttribute.Order value first, then
                        .OrderBy(prop =>
                        {
                            paramIndex++;
                            var columnAttributes = (prop.GetCustomAttributes(typeof(DbColumnAttribute), true));

                            return columnAttributes.Length > 0
                                            ? (columnAttributes[0] as DbColumnAttribute).Order
                                            : numDbColumnAttributes + paramIndex;
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
                                FunctionStub fStub = CreatePropertyAccessorFunctionStub(prop, ref paramIndex);

                                propertyAccessors.Add(paramIndex++, fStub);
                            }
                            else
                            {
                                invalidMappings.Add(prop.PropertyType.Name);
                            }
                        });

            if (invalidMappings.Count() > 0)
            {
                throw new AggregateException(invalidMappings.Select(im => new InvalidOperationException($"The type '{ im }' does not have a mapping defined.")));
            }
        }

        /// <summary>Creates a lambda expression for setting the SqlDataRecord field with the property value.</summary>
        /// <param name="prop">The property.</param>
        /// <param name="setterMethod">The <see cref="SqlDataRecord"/> setter method.</param>
        /// <returns>
        ///   Returns an expression of type <see cref="Action{T1, T2, T3}">Action&lt;SqlDataRecord, System.Int32,
        ///   T&gt;</see> containing the expression used to set the <see cref="SqlDataRecord"/> field with the property value.
        /// </returns>
        private static Expression<Action<SqlDataRecord, int, T>> CreatePropertyAccessorFunctionExpression(PropertyInfo prop, SqlDataRecordMethod setterMethod)
        {
            ParameterExpression instanceParam = Expression.Parameter(typeof(T), "obj");                                 // | T {obj};
                                                                                                                        // |
            MemberExpression memberAccessExpression = Expression.MakeMemberAccess(instanceParam, prop);                 // | MemberExpression {memberAccessExpression} = {obj}.prop;
                                                                                                                        // |
            Expression bodyExpression = null;                                                                           // | Expression bodyExpression = null;
                                                                                                                        // |
                                                                                                                        // The Enum data type has been defined to pass the Enum value into a SqlString field of the Table Parameter // |
                                                                                                                        // but the Enum data type cannot be automatically converted to a String object which causes the             // |
                                                                                                                        // "Expression.Convert(memberAccessExpression, setter.SqlDataType)" to throw an exception.                  // |
                                                                                                                        // As such, we need to call the "ToString" function on the value.                                           // |
            if (prop.PropertyType.BaseType.Name.In("Enum", "System.Enum"))                                   // | if (prop.PropertyType.BaseType.Name.In(new[] { "Enum", "System.Enum" }))
            {                                                                                                           // | {
                Expression toStringCall = Expression.Call(                                                              // |
                            memberAccessExpression,                                                                     // |
                            "ToString",                                                                                 // |
                            null);                                                                                      // |     Expression {toStringCall} = {memberAccessExpression}.ToString();
                bodyExpression = Expression.Convert(toStringCall, setterMethod.SqlDataType);                            // |     bodyExpression = Convert({toStringCall});
            }                                                                                                           // | }
            else                                                                                                        // | else
            {                                                                                                           // | {
                bodyExpression = prop.PropertyType == typeof(byte[])                                                    // |     bodyExpression = prop.PropertyType == typeof(byte[])
                            ? (Expression)(Expression.New(typeof(SqlBinary).GetConstructor(                             // |
                                            new[] { typeof(byte[]) }),                                             // |
                                            memberAccessExpression))                                                    // |            ? new SqlBinary({memberAccessExpression})
                            : (Expression)(Expression.Convert(memberAccessExpression, setterMethod.SqlDataType));       // |            : Convert({memberAccessExpression})
            }                                                                                                           // | }
                                                                                                                        // |
            ParameterExpression sdrParam = Expression.Parameter(typeof(SqlDataRecord), "sdr");                          // | SqlDataRecord {sdr};
            ParameterExpression indexParam = Expression.Parameter(typeof(int), "i");                                    // | int {i};
                                                                                                                        // |
            MethodCallExpression sdrSetMethod = Expression.Call(                                                        // |
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
            SqlDataRecordMethod setterMethod = sdrDataTypeMappings.ContainsKey(prop.PropertyType)
                        ? sdrDataTypeMappings[prop.PropertyType]
                        : sdrDataTypeMappings[prop.PropertyType.BaseType];

            Expression<Action<SqlDataRecord, int, T>> lambda = CreatePropertyAccessorFunctionExpression(prop, setterMethod);

            FunctionStub fStub = new FunctionStub();
            fStub.PropertyName = prop.Name;
            fStub.MetaTypeEnum = setterMethod.TypeEnum;
            fStub.Setter = lambda.Compile();

            var columnAttributes = (prop.GetCustomAttributes(typeof(DbColumnAttribute), true));

            paramIndex = columnAttributes.Length > 0
                        ? (columnAttributes[0] as DbColumnAttribute).Order
                        : paramIndex;

            fStub.ColumnOrder = columnAttributes.Length > 0
                        ? (columnAttributes[0] as DbColumnAttribute).Order
                        : -1;
            return fStub;
        }

        /// <summary>Defines the SqlDataRecord type mapping methods to convert between C# and Sql types.</summary>
        /// <returns>Dictionary&lt;Type, SqlDataRecordMethod&gt;.</returns>
        private static Dictionary<Type, SqlDataRecordMethod> DefineTypeMappingMethods()
        {
            Dictionary<Type, SqlDataRecordMethod> sdrMethodNames = new Dictionary<Type, SqlDataRecordMethod>();
            Type sdrType = typeof(SqlDataRecord);

            new List<SqlTypeMappingDefinition>() {
                new SqlTypeMappingDefinition(typeof(byte[]), typeof(SqlBinary), "SetSqlBinary", SqlDbType.VarBinary),
                new SqlTypeMappingDefinition(typeof(bool), typeof(SqlBoolean), "SetSqlBoolean", SqlDbType.Bit),
                new SqlTypeMappingDefinition(typeof(byte), typeof(SqlByte), "SetSqlByte", SqlDbType.TinyInt),
                //new SqlTypeMappingDefinition(typeof(byte[]),    typeof(SqlBytes), "SetSqlBytes", SqlDbType.Binary),
                //new SqlTypeMappingDefinition(typeof(char[]),    typeof(SqlChars), "SetSqlChars", SqlDbType.NVarChar),

                new SqlTypeMappingDefinition(typeof(int), typeof(SqlInt32), "SetSqlInt32", SqlDbType.Int),
                new SqlTypeMappingDefinition(typeof(short), typeof(SqlInt16), "SetSqlInt16", SqlDbType.SmallInt),
                new SqlTypeMappingDefinition(typeof(long), typeof(SqlInt64), "SetSqlInt64", SqlDbType.BigInt),
                new SqlTypeMappingDefinition(typeof(string), typeof(SqlString), "SetSqlString", SqlDbType.Text),
                new SqlTypeMappingDefinition(typeof(DateTime), typeof(SqlDateTime), "SetSqlDateTime", SqlDbType.DateTime),
                new SqlTypeMappingDefinition(typeof(decimal), typeof(SqlDecimal), "SetSqlDecimal", SqlDbType.Decimal),
                new SqlTypeMappingDefinition(typeof(Guid), typeof(SqlGuid), "SetSqlGuid", SqlDbType.UniqueIdentifier),
                new SqlTypeMappingDefinition(typeof(Enum), typeof(SqlString), "SetSqlString", SqlDbType.Text),

                //new SqlTypeMappingDefinition(typeof(char),      typeof(SqlChars), "SetSqlChar", SqlDbType.Char),
                new SqlTypeMappingDefinition(typeof(float), typeof(SqlDouble), "SetSqlFloat", SqlDbType.Float),
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
            public string SetterMethod;

            /// <summary>The native (.NET) type to be mapped.</summary>
            public Type NativeType;

            /// <summary>The Sql data type to map to.</summary>
            public Type SqlDataType;

            /// <summary>The Sql enum representation of the Sql data type.</summary>
            public SqlDbType SqlMetaType;

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
            public int ColumnOrder { get; set; }

            /// <summary>The Sql enum representation of the Sql data type.</summary>
            public SqlDbType MetaTypeEnum { get; set; }

            /// <summary>The name of the property to set.</summary>
            /// <value>The name of the property.</value>
            public string PropertyName { get; set; }

            /// <summary>The <see cref="Action"/> used to set the property.</summary>
            /// <value>The setter.</value>
            public Action<SqlDataRecord, int, T> Setter { get; set; }
        }
    }
}