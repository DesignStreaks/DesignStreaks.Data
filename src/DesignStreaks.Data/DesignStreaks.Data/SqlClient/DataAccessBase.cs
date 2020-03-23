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

namespace DesignStreaks.Data
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data;
    using System.Data.Common;
    using System.Linq;

    //using DesignStreaks.Presentation;

    /// <summary>Database Access base class allowing automatic model creation.</summary>
    public class DataAccessBase
    {
        /// <summary>Database connection object.</summary>
        protected readonly DbConnection connection;

        /// <summary>The command object used to execute the stored procedure.</summary>
        protected DbCommand command;

        /// <summary>The data reader used to read query results.</summary>
        protected DbDataReader reader;

        /// <summary>Initializes a new instance of the <see cref="DataAccessBase" /> class.</summary>
        /// <param name="connectionStringSettings">The connection string settings.</param>
        public DataAccessBase(ConnectionStringSettings connectionStringSettings)
        {
            DbProviderFactory factory = DbProviderFactories.GetFactory(connectionStringSettings.ProviderName);
            connection = factory.CreateConnection();
            connection.ConnectionString = connectionStringSettings.ConnectionString;
        }

        /// <summary>Create and <see cref="DbCommand">DbCommand</see> object.</summary>
        /// <param name="storedProcedure">The stored procedure to execute.</param>
        /// <param name="parameters">The list of parameters to pass to the stored procedure.</param>
        /// <returns>A <see cref="DbCommand" /> object.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        protected DbCommand CreateCommand(string storedProcedure, DbParameter[] parameters)
        {
            DbCommand cmd = connection.CreateCommand();

            cmd.CommandText = storedProcedure;
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandTimeout = 30;

            if (parameters != null)
            {
                foreach (DbParameter parameter in parameters)
                {
                    cmd.Parameters.Add((parameter is ICloneable) ? (parameter as ICloneable).Clone() : parameter);
                }
            }
            return cmd;
        }

        /// <summary>Executes a stored procedure with parameters.</summary>
        /// <param name="storedProcedure">The stored procedure name to execute.</param>
        /// <param name="parameters">The list of parameters to pass to the stored procedure.</param>
        [SqlParameterTraceAspect]
        protected void ExecuteStoredProcedure(string storedProcedure, DbParameter[] parameters)
        {
            bool cleanupConnection = false;
            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                    cleanupConnection = true;
                }

                this.command = CreateCommand(storedProcedure, parameters);
                this.reader = command.ExecuteReader(System.Data.CommandBehavior.Default);
            }
            catch (Exception ex)
            {
                DataException ex2 = new DataException(
                            string.Format(
                                    "{0}({1}): {2}",
                                    storedProcedure,
                                    string.Join(",", parameters.Select(p => p.Value)),
                                    ex.Message),
                            ex);

                System.Diagnostics.Trace.WriteLine(ex);

                throw ex2;
            }
            finally
            {
                if (cleanupConnection && connection.State != ConnectionState.Closed)
                    connection.Close();
            }
        }




        /// <summary>Update the ParameterValues with the value of any output parameters.</summary>
        /// <param name="parameters">The parameter collection.</param>
        /// <param name="parameterValues">the parameter values.</param>
        /// <returns>Returns the value of the <c>ReturnValue</c> internal stored procedure parameter.</returns>
        protected int UpdateOutputParameterValues(DbParameterCollection parameters, DbParameter[] parameterValues)
        {
            if (connection.State != ConnectionState.Closed)
            {
                connection.Close();
            }

            int returnValue = 0;

            for (int paramIndex = 0, paramCount = parameters.Count; paramIndex < paramCount; paramIndex++)
            {
                ParameterDirection direction = parameters[paramIndex].Direction;
                if (direction == ParameterDirection.ReturnValue)
                    int.TryParse(parameters[paramIndex].Value.ToString(), out returnValue);

                if (direction == ParameterDirection.InputOutput || direction == ParameterDirection.Output)
                    parameterValues[paramIndex] = parameters[paramIndex];
            }

            return returnValue;
        }


        /// <summary>Executes a stored procedure.</summary>
        /// <param name="storedProcedure">The stored procedure name to execute.</param>
        /// <returns>Returns the number of rows affected.</returns>
        [System.Diagnostics.DebuggerStepThrough]
        protected virtual int ExecuteStoredProcedureAction(string storedProcedure)
        {
            return ExecuteStoredProcedureAction(storedProcedure, new DbParameter[] { });
        }

        /// <summary>Executes an action stored procedure with parameters.</summary>
        /// <param name="storedProcedure">The stored procedure name to execute.</param>
        /// <param name="parameters">The list of parameters to pass to the stored procedure.</param>
        /// <returns>Returns the number of rows affected.</returns>
        /// <exception cref="DataException"></exception>
        protected virtual int ExecuteStoredProcedureAction(string storedProcedure, DbParameter[] parameters)
        {
            try
            {
                connection.Open();
                this.ExecuteStoredProcedure(storedProcedure, parameters);

                // If the command has returned a record set, then an error has occurred.
                if (reader.HasRows)
                {
                    reader.Read();
                    if (reader.GetName(0) == "ErrorNumber")
                        throw new DataException(reader["ErrorMessage"].ToString());
                }

                return this.UpdateOutputParameterValues(command.Parameters, parameters);
            }
            catch (Exception ex)
            {
                DataException ex2 = new DataException(
                            string.Format(
                                    "{0}({1}): {2}",
                                    storedProcedure,
                                    string.Join(",", parameters.Select(p => p.Value)),
                                    ex.Message),
                            ex);

                System.Diagnostics.Trace.WriteLine(ex);

                throw ex2;
            }
            finally
            {
                if (reader != null)
                    reader.Close();
                if (connection.State != ConnectionState.Closed)
                    connection.Close();
            }
        }

        /// <summary>Executes a stored procedure, returning a list of <typeparamref name="TModel" /> objects.</summary>
        /// <param name="storedProcedure">The stored procedure name to execute.</param>
        /// <returns>Returns a list of <typeparamref name="TModel" /> objects.</returns>
        [System.Diagnostics.DebuggerStepThrough]
        protected virtual List<TModel> ExecuteStoredProcedureMany<TModel>(string storedProcedure) where TModel : class, new()
        {
            return ExecuteStoredProcedureMany<TModel>(storedProcedure, new DbParameter[] { });
        }

        /// <summary>Executes a stored procedure with parameters, returning a list of <typeparamref name="TModel" /> objects.</summary>
        /// <param name="storedProcedure">The stored procedure name to execute.</param>
        /// <param name="parameters">The list of parameters to pass to the stored procedure.</param>
        /// <returns>Returns a list of <typeparamref name="TModel" /> objects.</returns>
        protected virtual List<TModel> ExecuteStoredProcedureMany<TModel>(string storedProcedure, DbParameter[] parameters) where TModel : class, new()
        {
            try
            {
                connection.Open();
                this.ExecuteStoredProcedure(storedProcedure, parameters);

                reader.Read();
                List<TModel> results = reader.ToList<TModel>();

                this.UpdateOutputParameterValues(command.Parameters, parameters);

                return results;
            }
            catch (Exception ex)
            {
                DataException ex2 = new DataException(
                            string.Format(
                                    "{0}({1}): {2}",
                                    storedProcedure,
                                    string.Join(",", parameters.Select(p => p.Value)),
                                    ex.Message),
                            ex);

                System.Diagnostics.Trace.WriteLine(ex);

                throw ex2;
            }
            finally
            {
                if (reader != null)
                    reader.Close();
                if (connection.State != ConnectionState.Closed)
                    connection.Close();
            }
        }

        /// <summary>Executes a stored procedure with parameters, returning a single <typeparamref name="TModel" /> object.</summary>
        /// <param name="storedProcedure">The stored procedure name to execute.</param>
        /// <returns>Returns a <typeparamref name="TModel" /> object.</returns>
        protected virtual TModel ExecuteStoredProcedureSingle<TModel>(string storedProcedure) where TModel : class, new()
        {
            return this.ExecuteStoredProcedureSingle<TModel>(storedProcedure, new DbParameter[] { });
        }

        /// <summary>Executes a stored procedure, returning a <typeparamref name="TModel" /> object.</summary>
        /// <param name="storedProcedure">The stored procedure name to execute.</param>
        /// <param name="parameters">The list of parameters to pass to the stored procedure.</param>
        /// <returns>Returns a <typeparamref name="TModel" /> object.</returns>
        protected virtual TModel ExecuteStoredProcedureSingle<TModel>(string storedProcedure, DbParameter[] parameters) where TModel : class, new()
        {
            try
            {
                connection.Open();
                ExecuteStoredProcedure(storedProcedure, parameters);

                reader.Read();
                if (!reader.HasRows)
                    return null;

                //TModel results = reader.BuildItem<TModel>(builder);
                TModel results = reader.ToPoco<TModel>();

                UpdateOutputParameterValues(command.Parameters, parameters);

                return results;
            }
            catch (Exception ex)
            {
                DataException ex2 = new DataException(
                            string.Format(
                                    "{0}({1}): {2}",
                                    storedProcedure,
                                    string.Join(",", parameters.Select(p => p.Value)),
                                    ex.Message),
                            ex);

                System.Diagnostics.Trace.WriteLine(ex);

                throw ex2;
            }
            finally
            {
                if (reader != null)
                    reader.Close();
                if (connection.State != ConnectionState.Closed)
                    connection.Close();

                UpdateOutputParameterValues(command.Parameters, parameters);
            }
        }

    }

    ///// <summary>Database Access base class allowing automatic model creation.</summary>
    ///// <typeparam name="TModel">The type of object to create.</typeparam>
    ///// <seealso cref="DesignStreaks.Data.DataAccessBase" />
    //public class DataAccessBase<TModel> : DataAccessBase where TModel : class, new()
    //{
    //    /// <summary>Initializes a new instance of the <see cref="DataAccessBase{TModel}"/> class.</summary>
    //    /// <param name="connectionStringSettings">The connection string settings.</param>
    //    public DataAccessBase(ConnectionStringSettings connectionStringSettings) : base(connectionStringSettings) { }

    //    /// <summary>Executes a stored procedure.</summary>
    //    /// <param name="storedProcedure">The stored procedure name to execute.</param>
    //    /// <returns>Returns the number of rows affected.</returns>
    //    [System.Diagnostics.DebuggerStepThrough]
    //    protected override int ExecuteStoredProcedureAction(string storedProcedure)
    //    {
    //        return base.ExecuteStoredProcedureAction(storedProcedure, new DbParameter[] { });
    //    }

    //    /// <summary>Executes an action stored procedure with parameters.</summary>
    //    /// <param name="storedProcedure">The stored procedure name to execute.</param>
    //    /// <param name="parameters">The list of parameters to pass to the stored procedure.</param>
    //    /// <returns>Returns the number of rows affected.</returns>
    //    /// <exception cref="DataException"></exception>
    //    protected override int ExecuteStoredProcedureAction(string storedProcedure, DbParameter[] parameters)
    //    {
    //        return base.ExecuteStoredProcedureAction(storedProcedure, parameters);
    //    }

    //    /// <summary>Executes a stored procedure, returning a list of <typeparamref name="TModel" /> objects.</summary>
    //    /// <param name="storedProcedure">The stored procedure name to execute.</param>
    //    /// <returns>Returns a list of <typeparamref name="TModel" /> objects.</returns>
    //    [System.Diagnostics.DebuggerStepThrough]
    //    protected override List<TModel> ExecuteStoredProcedureMany(string storedProcedure)
    //    {
    //        return base.ExecuteStoredProcedureMany<TModel>(storedProcedure, new DbParameter[] { });
    //    }

    //    /// <summary>Executes a stored procedure with parameters, returning a list of <typeparamref name="TModel" /> objects.</summary>
    //    /// <param name="storedProcedure">The stored procedure name to execute.</param>
    //    /// <param name="parameters">The list of parameters to pass to the stored procedure.</param>
    //    /// <returns>Returns a list of <typeparamref name="TModel" /> objects.</returns>
    //    protected override List<TModel> ExecuteStoredProcedureMany(string storedProcedure, DbParameter[] parameters)
    //    {
    //        return base.ExecuteStoredProcedureMany<TModel>(storedProcedure, parameters);
    //    }

    //    /// <summary>Executes a stored procedure with parameters, returning a single <typeparamref name="TModel" /> object.</summary>
    //    /// <param name="storedProcedure">The stored procedure name to execute.</param>
    //    /// <returns>Returns a <typeparamref name="TModel" /> object.</returns>
    //    [System.Diagnostics.DebuggerStepThrough]
    //    protected override TModel ExecuteStoredProcedureSingle(string storedProcedure)
    //    {
    //        return base.ExecuteStoredProcedureSingle<TModel>(storedProcedure, new DbParameter[] { });
    //    }

    //    /// <summary>Executes a stored procedure, returning a <typeparamref name="TModel" /> object.</summary>
    //    /// <param name="storedProcedure">The stored procedure name to execute.</param>
    //    /// <param name="parameters">The list of parameters to pass to the stored procedure.</param>
    //    /// <returns>Returns a <typeparamref name="TModel" /> object.</returns>
    //    protected override TModel ExecuteStoredProcedureSingle(string storedProcedure, DbParameter[] parameters)
    //    {
    //        return base.ExecuteStoredProcedureSingle<TModel>(storedProcedure, parameters);
    //    }
    //}
}