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
    using System.Collections.Generic;
    using System.Linq;
    using System.Security;

    /// <summary>Extension methods for the <see cref="DbParameterCollection"/> class.</summary>
    public static class DbParameterCollectionExtensions
    {
        /// <summary>Returns a System.String that represents the parameter.</summary>
        /// <param name="parameter">The parameter.</param>
        /// <param name="named">Include the parameter name.</param>
        /// <returns>A formatted string for the input/output parameter.</returns>
        public static string ToString(this DbParameter parameter, bool named)
        {
            string value = string.Empty;

            switch (parameter.DbType)
            {
                // Quoted Values.
                case DbType.AnsiString:
                case DbType.AnsiStringFixedLength:
                case DbType.Date:
                case DbType.DateTime:
                case DbType.DateTime2:
                case DbType.DateTimeOffset:
                case DbType.String:
                case DbType.StringFixedLength:
                case DbType.Time:
                    if (named)
                        value = string.Format(
                                "{0}='{1}'",
                                parameter.ParameterName,
                                parameter.Value ?? "null");
                    else
                        value = string.Format("'{0}'", parameter.Value ?? "null");

                    break;

                case DbType.Xml:
                    if (named)
                        value = string.Format("{0}='{1}'", parameter.ParameterName, SecurityElement.Escape((parameter.Value ?? "null").ToString()));
                    else
                        value = string.Format("'{0}'", SecurityElement.Escape((parameter.Value ?? "null").ToString()));

                    break;

                // Numeric Values.
                case DbType.Currency:
                case DbType.Decimal:
                case DbType.Double:
                case DbType.Single:
                case DbType.Int16:
                case DbType.Int32:
                case DbType.Int64:
                case DbType.SByte:
                case DbType.UInt16:
                case DbType.UInt32:
                case DbType.UInt64:
                case DbType.VarNumeric:
                    if (named)
                        value = string.Format("{0}={1}", parameter.ParameterName, parameter.Value ?? "null");
                    else
                        value = string.Format("{0}", parameter.Value ?? "null");

                    break;

                // Binary Values.
                case DbType.Binary:
                    byte[] parameterValue = (parameter.Value as byte[]) ?? new byte[] { };

                    if (named)
                        value = string.Format(
                                "{0}=0x{1}",
                                parameter.ParameterName,
                                parameterValue.Length == 0
                                    ? "0"
                                    : string.Join("", parameterValue.Select(c => c.ToString("X2")).ToArray()));
                    else
                        value = string.Format(
                                "0x{0}",
                                parameterValue.Length == 0
                                    ? "0"
                                    : string.Join("", parameterValue.Select(c => c.ToString("X2")).ToArray()));

                    break;

                case DbType.Guid:
                    if (named)
                        value = string.Format("{0}='{{{1}}}'", parameter.ParameterName, (parameter.Value ?? "null").ToString());
                    else
                        value = string.Format("{{{0}}}", (parameter.Value ?? "null").ToString());

                    break;

                case DbType.Object:
                    SqlClient.SqlParameter sqlParameter = parameter as SqlClient.SqlParameter;

                    if (sqlParameter == null || sqlParameter.SqlDbType != SqlDbType.Structured)
                    {
                        if (named)
                            value = string.Format("{0}=[{1}]", parameter.ParameterName, (parameter.Value ?? "null").ToString().Substring(parameter.ParameterName.Length, 16));
                        else
                            value = string.Format("[{0}]", (parameter.Value ?? "null").ToString().Substring(0, 16));
                    }
                    else
                    {
                        if (named)
                            value = string.Format("{0}=[{1}]", parameter.ParameterName, (parameter.Value == null ? "null" : "[...]"));
                        else
                            value = string.Format("[{0}]", (parameter.Value == null ? "null" : "[...]"));
                    }
                    break;

                // Other Values.
                case DbType.Boolean:
                    if (named)
                        value = string.Format("{0}='{1}'", parameter.ParameterName, (parameter.Value ?? "null").ToString());
                    else
                        value = string.Format("'{0}'", (parameter.Value ?? "null").ToString());

                    break;

                case DbType.Byte:
                    if (named)
                        value = string.Format("{0}='{1}'", parameter.ParameterName, (parameter.Value ?? "null").ToString().Substring(0, 16));
                    else
                        value = string.Format("'{0}'", (parameter.Value ?? "null").ToString().Substring(0, 16));

                    break;
            }

            if (parameter.Direction == ParameterDirection.Output || parameter.Direction == ParameterDirection.InputOutput)
                value += " output";

            return value;
        }

        /// <summary>Returns a System.String that represents all input and output parameters.</summary>
        /// <param name="parameters">The parameters.</param>
        /// <param name="named">Include the parameter names.</param>
        /// <returns>A formatted string for all input/output parameters.</returns>
        public static string ToString(this DbParameterCollection parameters, bool named)
        {
            List<string> parameterValues = new List<string>();

            foreach (DbParameter parameter in parameters)
            {
                if (parameter.Direction == ParameterDirection.ReturnValue)
                    continue;

                return parameter.ToString(named);
            }

            return string.Join(",", parameterValues);
        }
    }
}