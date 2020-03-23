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
    using System.Collections.Generic;
    using System.Data;
    using System.Security;

    /// <summary>Extension methods for the <see cref="System.Data.SqlClient.SqlParameterCollection"/> class.</summary>
    public static class SqlParameterCollectionExtensions
    {
        /// <summary>Returns a System.String that represents all input and output parameters.</summary>
        /// <param name="parameters">The parameters.</param>
        /// <param name="named">Include the parameter names.</param>
        /// <returns>A formatted string for all input/output parameters.</returns>
        public static string Values(this SqlParameterCollection parameters, bool named)
        {
            List<string> parameterValues = new List<string>();

            foreach (SqlParameter parameter in parameters)
            {
                if (parameter.Direction == ParameterDirection.ReturnValue)
                    continue;

                switch (parameter.SqlDbType)
                {
                    // Quoted Values.
                    case SqlDbType.Char:
                    case SqlDbType.Date:
                    case SqlDbType.DateTime:
                    case SqlDbType.DateTime2:
                    case SqlDbType.DateTimeOffset:
                    case SqlDbType.NChar:
                    case SqlDbType.NText:
                    case SqlDbType.NVarChar:
                    case SqlDbType.SmallDateTime:
                    case SqlDbType.Text:
                    case SqlDbType.Time:
                    case SqlDbType.VarChar:
                        if (named)
                            parameterValues.Add(string.Format("{0} = '{1}'", parameter.ParameterName, parameter.Value ?? ""));
                        else
                            parameterValues.Add(string.Format("'{0}'", parameter.Value ?? ""));
                        break;

                    case SqlDbType.Xml:
                        if (named)
                            parameterValues.Add(string.Format("{0} = '{1}'", parameter.ParameterName, SecurityElement.Escape((parameter.Value ?? "").ToString())));
                        else
                            parameterValues.Add(string.Format("'{0}'", SecurityElement.Escape((parameter.Value ?? "").ToString())));
                        break;

                    // Numeric Values.
                    case SqlDbType.BigInt:
                    case SqlDbType.Bit:
                    case SqlDbType.Decimal:
                    case SqlDbType.Float:
                    case SqlDbType.Int:
                    case SqlDbType.Money:
                    case SqlDbType.Real:
                    case SqlDbType.SmallInt:
                    case SqlDbType.SmallMoney:
                    case SqlDbType.TinyInt:
                        if (named)
                            parameterValues.Add(string.Format("{0} = {1}", parameter.ParameterName, parameter.Value ?? 0));
                        else
                            parameterValues.Add(string.Format("{0}", parameter.Value ?? 0));
                        break;

                    // Binary Values.
                    case SqlDbType.Binary:
                    case SqlDbType.Image:
                    case SqlDbType.Structured:
                    case SqlDbType.Timestamp:
                    case SqlDbType.Udt:
                    case SqlDbType.VarBinary:
                        if (named)
                            parameterValues.Add(string.Format("{0} = [{1}]", parameter.ParameterName, (parameter.Value ?? "").ToString().Substring(0, 16)));
                        else
                            parameterValues.Add(string.Format("[{0}]", (parameter.Value ?? "").ToString().Substring(0, 16)));
                        break;

                    // Other Values.
                    case SqlDbType.UniqueIdentifier:
                    case SqlDbType.Variant:
                        if (named)
                            parameterValues.Add(string.Format("{0} = '{1}'", parameter.ParameterName, (parameter.Value ?? "").ToString().Substring(0, 16)));
                        else
                            parameterValues.Add(string.Format("'{0}'", (parameter.Value ?? "").ToString().Substring(0, 16)));
                        break;
                }

            }

            return string.Join(",", parameterValues);
        }

        /// <summary>Returns a System.String that represents all input and output parameters.</summary>
        /// <param name="parameters">The parameters.</param>
        /// <returns>A formatted string for all input/output parameters.</returns>
        public static string Values(this SqlParameterCollection parameters)
        {
            return parameters.Values(false);
        }

    }

}
