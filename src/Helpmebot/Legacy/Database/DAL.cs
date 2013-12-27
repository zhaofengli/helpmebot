﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DAL.cs" company="Helpmebot Development Team">
//   Helpmebot is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//   
//   Helpmebot is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//   
//   You should have received a copy of the GNU General Public License
//   along with Helpmebot.  If not, see http://www.gnu.org/licenses/ .
// </copyright>
// <summary>
//   Database access class
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Helpmebot.Legacy.Database
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;

    using Castle.Core.Logging;

    using Helpmebot.Configuration;
    using Helpmebot.Configuration.XmlSections;

    using Microsoft.Practices.ServiceLocation;

    using MySql.Data.MySqlClient;

    /// <summary>
    /// Database access class
    /// </summary>
    public class DAL : IDisposable, IDAL
    {
        /// <summary>
        /// Gets or sets the Castle.Windsor Logger
        /// </summary>
        public ILogger Log { get; set; }

        private MySqlConnection _connection;

        #region singleton

        private static DAL _singleton;

        /// <summary>
        /// Singletons the specified host.
        /// </summary>
        /// <returns></returns>
        public static DAL singleton()
        {

            return _singleton ?? (_singleton = new DAL(ServiceLocator.Current.GetInstance<ILogger>()));
        }

        public DAL(ILogger logger)
        {
            this.Log = logger.CreateChildLogger("Helpmebot.Legacy.Database.DAL");
        }
        #endregion

        /// <summary>
        /// Connects this instance to the database.
        /// </summary>
        /// <returns></returns>
        public bool connect()
        {
            DatabaseConfiguration dbConfiguration = ConfigurationHelper.DatabaseConfiguration;

            try
            {
                lock (this)
                {
                    Log.Info("Opening database connection...");
                    var csb = new MySqlConnectionStringBuilder
                                  {
                                      Database = dbConfiguration.Schema,
                                      Password = dbConfiguration.Password,
                                      Server = dbConfiguration.Hostname,
                                      UserID = dbConfiguration.Username,
                                      Port = (uint)dbConfiguration.Port
                                  };

                    this._connection = new MySqlConnection(csb.ConnectionString);
                    this._connection.Open();
                }

                return true;
            }
            catch (MySqlException ex)
            {
                Log.Error(ex.Message, ex);
                return false;
            }
        }

        #region internals

        /// <summary>
        /// The execute non query.
        /// </summary>
        /// <param name="cmd">
        /// The cmd.
        /// </param>
        private void executeNonQuery(ref MySqlCommand cmd)
        {
            lock (this)
            {
                Log.Debug(string.Format("Executing non-query: {0}", cmd.CommandText));
                try
                {
                    this.runConnectionTest();
                    cmd.Connection = this._connection;
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message, ex);
                }

                Log.Debug("Done executing query");
            }
        }

        /// <summary>
        /// Executes the reader query
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        /// <remarks>Needs Lock!</remarks>
        private MySqlDataReader executeReaderQuery(string query)
        {
            MySqlDataReader result = null;

            Log.Debug("Executing (reader)query: " + query);

            try
            {
                this.runConnectionTest();

                MySqlCommand cmd = new MySqlCommand(query) {Connection = this._connection};
                result = cmd.ExecuteReader();
                Log.Debug("Done executing (reader)query: " + query);

                return result;
            }
            catch (Exception ex)
            {
                Log.Error("Problem executing (reader)query", ex);
            }

            return result;
        }

        #endregion
       
        #region sql statements
        /// <summary>
        /// Inserts values the specified table.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="values">The values.</param>
        /// <returns></returns>
        public long insert(string table, params string[] values)
        {
            string query = "INSERT INTO `" + sanitise(table) + "` VALUES (";
            foreach (string item in values)
            {
                if (item != string.Empty)
                {
                    query += " \"" + sanitise(item) + "\",";
                }
                else
                {
                    query += "null,";
                }
            }

            query = query.TrimEnd(',');
            query += " );";

            long lastInsertedId = -1;
            try
            {
                MySqlCommand cmd = new MySqlCommand(query);
                this.executeNonQuery(ref cmd);
                lastInsertedId = cmd.LastInsertedId;
            }
            catch (MySqlException ex)
            {
                Log.Error(ex.Message, ex);
            }

            return lastInsertedId;
        }


        /// <summary>
        /// Deletes from the specified table.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="conditions">The conditions.</param>
        public bool delete(string table, int limit, params WhereConds[] conditions)
        {
            bool succeed = false;

            string query = "DELETE FROM `" + sanitise(table) + "`";
            for (int i = 0; i < conditions.Length; i++)
            {
                if (i == 0)
                {
                    query += " WHERE ";
                }
                else
                {
                    query += " AND ";
                }

                query += conditions[i].ToString();
            }

            if (limit > 0)
            {
                query += " LIMIT " + limit;
            }

            query += ";";
            try
            {
                MySqlCommand deleteCommand = new MySqlCommand(query);
                this.executeNonQuery(ref deleteCommand);
                succeed = true;
            }
            catch (MySqlException ex)
            {
                Log.Error(ex.Message, ex);
            }

            return succeed;
        }

        /// <summary>
        /// Updates rows in the specified table.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="items">The items.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="conditions">The conditions.</param>
        public bool update(string table, Dictionary<string, string> items, int limit, params WhereConds[] conditions)
        {
            bool succeed = false;

            if (items.Count < 1)
            {
                return true;
            }

            string query = "UPDATE `" + sanitise(table) + "` SET ";

            foreach (KeyValuePair<string, string> col in items)
            {
                query += "`" + sanitise(col.Key) + "` = \"" + sanitise(col.Value) + "\", ";
            }

            query = query.TrimEnd(',', ' ');

            for (int i = 0; i < conditions.Length; i++)
            {
                if (i == 0)
                {
                    query += " WHERE ";
                }
                else
                {
                    query += " AND ";
                }

                query += conditions[i].ToString();
            }

            if (limit > 0)
            {
                query += " LIMIT " + limit;
            }

            query += ";";

            try
            {
                MySqlCommand updateCommand = new MySqlCommand(query);
                this.executeNonQuery(ref updateCommand);
                succeed = true;
            }
            catch (MySqlException ex)
            {
                Log.Error(ex.Message, ex);
            }

            return succeed;
        }

        /// <summary>
        /// Executes the select.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Arraylist of arrays. Each array is one row in the dataset.</returns>
        public ArrayList executeSelect(Select query)
        {
            List<string> cols;
            return this.executeSelect(query, out cols);
        }

        /// <summary>
        /// Executes the select.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="columns">A list of column names</param>
        /// <returns>Arraylist of arrays. Each array is one row in the dataset.</returns>
        public ArrayList executeSelect(Select query, out List<string> columns)
        {
            columns = new List<string>();
            ArrayList resultSet = new ArrayList();
            lock (this)
            {
                MySqlDataReader dr = this.executeReaderQuery(query.ToString());
                if (dr != null)
                {
                    try
                    {
                        DataTableReader cols = dr.GetSchemaTable().CreateDataReader();
                        while (cols.Read())
                        {
                            columns.Add((string) cols.GetValue(0));
                        }

                        cols.Close();

                        while (dr.Read())
                        {
                            object[] row = new object[dr.FieldCount];
                            dr.GetValues(row);
                            resultSet.Add(row);
                        }
                    }
                    catch (MySqlException ex)
                    {
                        Log.Error(ex.Message, ex);
                        throw;
                    }
                    finally
                    {
                        dr.Close();
                    }
                }
            }

            return resultSet;
        }

        /// <summary>
        /// Executes the scalar select.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>A single value as a string</returns>
        public string executeScalarSelect(Select query)
        {
            ArrayList al = this.executeSelect(query);
            return al.Count > 0 ? ((object[])al[0])[0].ToString() : string.Empty;
        }

        #endregion

        private void runConnectionTest()
        {
            // ok, first let's assume the connection is dead.
            bool connectionOk = false;

            // first time through, skip the connection attempt
            bool firstTime = true;

            int sleepTime = 1000;

            int totalTimeSlept = 0;

            while (!connectionOk || totalTimeSlept >= 180 /*seconds*/ * 1000 /*transform to milliseconds*/)
            {
                if (!firstTime)
                {
                    Log.Warn("Reconnecting to database...");

                    this.connect();

                    Thread.Sleep(sleepTime);
                    totalTimeSlept += sleepTime;

                    sleepTime = (int)(sleepTime * 1.5) > int.MaxValue ? sleepTime : (int)(sleepTime * 1.5);
                }

                while (this._connection.State == ConnectionState.Connecting)
                {
                    Thread.Sleep(100);
                    totalTimeSlept += 100;
                }

                connectionOk = (this._connection.State == ConnectionState.Open) ||
                                (this._connection.State == ConnectionState.Fetching) ||
                                (this._connection.State == ConnectionState.Executing);

                firstTime = false;
            }

            if (!connectionOk)
            {
                throw new SocketException();
            }
        }

        /// <summary>
        /// Executes the stored procedure.
        /// </summary>
        /// <param name="name">The procedure name.</param>
        /// <param name="args">The args.</param>
        public void executeProcedure(string name, params string[] args)
        {
            MySqlCommand cmd = new MySqlCommand
                                   {
                                       CommandType = CommandType.StoredProcedure,
                                       CommandText = name
                                   };

            foreach (string item in args)
            {
                cmd.Parameters.Add(new MySqlParameter(item, MySqlDbType.Int16));
            }

            cmd.Connection = this._connection;

            this.runConnectionTest();

            cmd.ExecuteNonQuery();
        }

        // ReSharper disable InconsistentNaming
        public string proc_HMB_GET_LOCAL_OPTION(string option, string channel)
// ReSharper restore InconsistentNaming
        {
            try
            {
                lock (this)
                {
                    this.runConnectionTest();


                    MySqlCommand cmd = new MySqlCommand
                                           {
                                               Connection = this._connection,
                                               CommandType =
                                                   CommandType.StoredProcedure,
                                               CommandText = "HMB_GET_LOCAL_OPTION"
                                           };

                    cmd.Parameters.AddWithValue("@optionName", option);
                    cmd.Parameters["@optionName"].Direction = ParameterDirection.Input;

                    cmd.Parameters.AddWithValue("@channel", channel);
                    cmd.Parameters["@channel"].Direction = ParameterDirection.Input;

                    cmd.Parameters.AddWithValue("@optionValue", string.Empty);
                    cmd.Parameters["@optionValue"].Direction = ParameterDirection.Output;


                    cmd.ExecuteNonQuery();

                    return (string)cmd.Parameters["@optionValue"].Value;
                }
            }
            catch (FormatException ex)
            {
                Log.Error(ex.Message, ex);
                throw;
            }
            catch (InvalidOperationException ex)
            {
                Log.Error(ex.Message, ex);
            }

            return null;
        }

// ReSharper disable InconsistentNaming
        public string proc_HMB_GET_MESSAGE_CONTENT(string title)
        // ReSharper restore InconsistentNaming
        {
            byte[] binarymessage = new byte[0];

            try
            {
                lock (this)
                {
                    this.runConnectionTest();

                    MySqlCommand cmd = new MySqlCommand
                                           {
                                               Connection = this._connection,
                                               CommandType =
                                                   CommandType.StoredProcedure,
                                               CommandText =
                                                   "HMB_GET_MESSAGE_CONTENT"
                                           };

                    byte[] titlebytes = new byte[255];
                    Encoding.ASCII.GetBytes(title, 0, title.Length, titlebytes, 0);

                    cmd.Parameters.Add("@title", MySqlDbType.VarBinary).Value = title;
                    cmd.Parameters["@title"].Direction = ParameterDirection.Input;

                    byte[] messagebytes = new byte[0];
                    cmd.Parameters.Add("@message", MySqlDbType.MediumBlob).Value = messagebytes;
                    cmd.Parameters["@message"].Direction = ParameterDirection.Output;

                    cmd.ExecuteNonQuery();


                    object foo = cmd.Parameters["@message"].Value is DBNull
                                     ? new byte[0]
                                     : cmd.Parameters["@message"].Value;

                    binarymessage = (byte[])foo;
                }
            }
            catch (InvalidOperationException ex)
            {
                Log.Error(ex.Message, ex);
            }
            return Encoding.UTF8.GetString(binarymessage);
        }

        // ReSharper disable InconsistentNaming
        public string proc_HMB_GET_IW_URL(string prefix)
        // ReSharper restore InconsistentNaming
        {
            string surl = string.Empty;
            try
            {
                lock (this)
                {
                    this.runConnectionTest();

                    MySqlCommand cmd = new MySqlCommand
                                           {
                                               Connection = this._connection,
                                               CommandType =
                                                   CommandType.StoredProcedure,
                                               CommandText =
                                                   "HMB_GET_IW_URL"
                                           };

                    if (prefix.Length > 32)
                    {
                        return string.Empty;
                    }

                    cmd.Parameters.Add("@prefix", MySqlDbType.VarChar).Value = prefix;
                    cmd.Parameters["@prefix"].Direction = ParameterDirection.Input;


                    byte[] url = new byte[0];
                    cmd.Parameters.Add("@url", MySqlDbType.VarChar).Value = url;
                    cmd.Parameters["@url"].Direction = ParameterDirection.Output;

                    cmd.ExecuteNonQuery();

                    surl =
                        (string)
                        (cmd.Parameters["@url"].Value is System.DBNull ? string.Empty : cmd.Parameters["@url"].Value);
                }
            }
            catch (InvalidOperationException ex)
            {
                Log.Error(ex.Message, ex);
            }
            return surl;
        }

        #region data structures
        /// <summary>
        ///   Class encapsulating a SELECT statement
        /// </summary>
        public class Select
        {
            private bool _shallIEscapeSelects = true;

            private readonly string[] _fields;
            private string _from;
            private readonly LinkedList<Join> _joins = new LinkedList<Join>();
            private readonly LinkedList<WhereConds> _wheres;
            private readonly LinkedList<string> _groups;
            private readonly LinkedList<Order> _orders;
            private readonly LinkedList<WhereConds> _havings;
            private int _limit;
            private int _offset;

            /// <summary>
            /// Initializes a new instance of the <see cref="Select"/> class.
            /// </summary>
            /// <param name="fields">The fields to return.</param>
            public Select(params string[] fields)
            {
                this._fields = fields;
                this._from = string.Empty;
                this._limit = this._offset = 0;
                this._joins = new LinkedList<Join>();
                this._wheres = new LinkedList<WhereConds>();
                this._groups = new LinkedList<string>();
                this._orders = new LinkedList<Order>();
                this._havings = new LinkedList<WhereConds>();
            }

            /// <summary>
            /// Escape the selects?
            /// </summary>
            /// <param name="escape">if set to <c>true</c> [escape].</param>
            public void escapeSelects(bool escape)
            {
                this._shallIEscapeSelects = escape;
            }

            /// <summary>
            /// Sets from.
            /// </summary>
            /// <param name="from">From.</param>
            public void setFrom(string from)
            {
                this._from = from;
            }

            public Select From(string from)
            {
                this.setFrom(from);
                return this;
            }

            /// <summary>
            /// Adds a JOIN clause.
            /// </summary>
            /// <param name="table">The table.</param>
            /// <param name="joinType">Type of the join.</param>
            /// <param name="conditions">The conditions.</param>
            public void addJoin(string table, JoinTypes joinType, WhereConds conditions)
            {
                this._joins.AddLast(new Join(joinType, table, conditions));
            }

            /// <summary>
            /// Adds a where clause.
            /// </summary>
            /// <param name="conditions">The conditions.</param>
            public void addWhere(params WhereConds[] conditions)
            {
                foreach (WhereConds condition in conditions)
                {
                    this._wheres.AddLast(condition);    
                }
            }

            public Select Where(params WhereConds[] conditions)
            {
                this.addWhere(conditions);
                return this;
            }

            /// <summary>
            /// Adds a grouping.
            /// </summary>
            /// <param name="field">The field.</param>
            public void addGroup(string field)
            {
                this._groups.AddLast(field);
            }

            /// <summary>
            /// Adds the order.
            /// </summary>
            /// <param name="order">The order.</param>
            public void addOrder(Order order)
            {
                this._orders.AddLast(order);
            }

            /// <summary>
            /// Adds a having clause.
            /// </summary>
            /// <param name="conditions">The conditions.</param>
            public void addHaving(WhereConds conditions)
            {
                this._havings.AddLast(conditions);
            }

            /// <summary>
            /// Adds a limit.
            /// </summary>
            /// <param name="limit">The limit.</param>
            /// <param name="offset">The offset.</param>
            public void addLimit(int limit, int offset)
            {
                this._limit = limit;
                this._offset = offset;
            }

            /// <summary>
            /// Returns a <see cref="System.String"/> that represents this instance.
            /// </summary>
            /// <returns>
            /// A <see cref="System.String"/> that represents this instance.
            /// </returns>
            public override string ToString()
            {
                string query = "SELECT ";
                bool firstField = true;
                foreach (string  f in this._fields)
                {
                    if (!firstField)
                        query += ", ";

                    string fok = MySqlHelper.EscapeString(f);
                    if (! this._shallIEscapeSelects)
                        fok = f;

                    firstField = false;

                    query += fok;
                }

                if (this._from != string.Empty)
                {
                    query += " FROM " + "`" + MySqlHelper.EscapeString(this._from) + "`";
                }

                if (this._joins.Count != 0)
                {
                    foreach (Join item in this._joins)
                    {
                        switch (item.joinType)
                        {
                            case JoinTypes.Inner:
                                query += " INNER JOIN ";
                                break;
                            case JoinTypes.Left:
                                query += " LEFT OUTER JOIN ";
                                break;
                            case JoinTypes.Right:
                                query += " RIGHT OUTER JOIN ";
                                break;
                            case JoinTypes.FullOuter:
                                query += " FULL OUTER JOIN ";
                                break;
                            default:
                                break;
                        }

                        query += "`" + MySqlHelper.EscapeString(item.table) + "`";

                        query += " ON " + item.joinConditions;
                    }
                }

                if (this._wheres.Count > 0)
                {
                    query += " WHERE ";

                    bool first = true;

                    foreach (WhereConds w in this._wheres)
                    {
                        if (!first)
                            query += " AND ";
                        first = false;
                        query += w.ToString();
                    }
                }
                if (this._groups.Count != 0)
                {
                    query += " GROUP BY ";
                    bool first = true;
                    foreach (string group in this._groups)
                    {
                        if (!first)
                            query += ", ";
                        first = false;
                        query += MySqlHelper.EscapeString(group);
                    }
                }
                if (this._orders.Count > 0)
                {
                    query += " ORDER BY ";

                    bool first = true;
                    foreach (Order order in this._orders)
                    {
                        if (!first)
                            query += ", ";
                        first = false;
                        query += order.ToString();
                    }
                }
                if (this._havings.Count > 0)
                {
                    query += " HAVING ";

                    bool first = true;

                    foreach (WhereConds w in this._havings)
                    {
                        if (!first)
                            query += " AND ";
                        first = false;
                        query += w.ToString();
                    }
                }

                if (this._limit != 0)
                    query += " LIMIT " + this._limit;

                if (this._offset != 0)
                    query += " OFFSET " + this._offset;

                query += ";";
                return query;
            }

            public struct Order
            {
                public Order(string column, bool asc)
                {
                    this._column = column;
                    this._asc = asc;
                    this._escape = true;
                }

                public Order(string column, bool asc, bool escape)
                {
                    this._column = column;
                    this._asc = asc;
                    this._escape = escape;
                }

                private readonly string _column;
                private readonly bool _asc;
                private readonly bool _escape;

                public override string ToString()
                {
                    return "`" + (this._escape ? MySqlHelper.EscapeString(this._column) : this._column) + "` " + (this._asc ? "ASC" : "DESC");
                }
            }

            private struct Join
            {
                public readonly JoinTypes joinType;
                public readonly string table;
                public readonly WhereConds joinConditions;

                public Join(JoinTypes type, string table, WhereConds conditions)
                {
                    this.joinType = type;
                    this.table = table;
                    this.joinConditions = conditions;
                }
            }

            public enum JoinTypes
            {
                Inner,
                Left,
                Right,
                FullOuter
            }
        }

        public struct WhereConds
        {
            private readonly bool _quoteA;
            private readonly bool _quoteB;
            private readonly string _a;
            private readonly string _b;
            private readonly string _comparer;

            public WhereConds(bool aNeedsQuoting, string a, string comparer, bool bNeedsQuoting, string b)
            {
                this._quoteA = aNeedsQuoting;
                this._quoteB = bNeedsQuoting;
                this._a = a;
                this._b = b;
                this._comparer = comparer;
            }

            public WhereConds(string column, string value)
            {
                this._quoteA = false;
                this._quoteB = true;
                this._a = column;
                this._b = value;
                this._comparer = "=";
            }

            public WhereConds(string column, int value)
            {
                this._quoteA = false;
                this._quoteB = true;
                this._a = column;
                this._b = value.ToString();
                this._comparer = "=";
            }

            public override string ToString()
            {
                string actualA = (this._quoteA ? "\"" : string.Empty) + MySqlHelper.EscapeString(this._a) + (this._quoteA ? "\"" : string.Empty);
                string actualB = (this._quoteB ? "\"" : string.Empty) + MySqlHelper.EscapeString(this._b) + (this._quoteB ? "\"" : string.Empty);
                string actualComp = MySqlHelper.EscapeString(this._comparer);
                return actualA + " " + actualComp + " " + actualB;
            }
        }

        #endregion

        /// <summary>
        /// Sanitises the specified raw data.
        /// </summary>
        /// <param name="rawData">The raw data.</param>
        /// <returns></returns>
        private static string sanitise(string rawData)
        {
            return MySqlHelper.EscapeString(rawData);
        }

        public void Dispose()
        {
            this._connection.Dispose();
        }
    }
}