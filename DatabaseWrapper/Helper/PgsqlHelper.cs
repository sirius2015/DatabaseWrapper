﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseWrapper
{
    internal static class PgsqlHelper
    {
        public static string ConnectionString(string serverIp, int serverPort, string username, string password, string database)
        {
            string ret = "";

            //
            // http://www.connectionstrings.com/postgresql/
            //
            // PgSQL does not use 'Instance'
            ret += "Server=" + serverIp + "; ";
            if (serverPort > 0) ret += "Port=" + serverPort + "; ";
            ret += "Database=" + database + "; ";
            if (!String.IsNullOrEmpty(username)) ret += "User ID=" + username + "; ";
            if (!String.IsNullOrEmpty(password)) ret += "Password=" + password + "; ";

            return ret;
        }

        public static string LoadTableNamesQuery()
        {
            return "SELECT * FROM pg_catalog.pg_tables WHERE schemaname != 'pg_catalog' AND schemaname != 'information_schema'";
        }

        public static string LoadTableColumnsQuery(string database, string table)
        {
            return
                "SELECT " +
                "  cols.COLUMN_NAME AS column_name, " +
                "  cols.IS_NULLABLE AS is_nullable, " +
                "  cols.DATA_TYPE AS data_type, " +
                "  cols.CHARACTER_MAXIMUM_LENGTH AS max_len, " +
                "  CASE " +
                "    WHEN cons.COLUMN_NAME IS NULL THEN 'NO' ELSE 'YES' " +
                "  END AS is_primary_key " +
                "FROM test.INFORMATION_SCHEMA.COLUMNS cols " +
                "LEFT JOIN " + database + ".INFORMATION_SCHEMA.KEY_COLUMN_USAGE cons ON cols.COLUMN_NAME = cons.COLUMN_NAME " +
                "WHERE cols.TABLE_NAME = '" + table + "';";
        }

        public static string SanitizeString(string val)
        {
            string tag = "$" + EscapeString(val, 2) + "$";
            return tag + val + tag;
        }

        private static string EscapeString(string val, int numChar)
        {
            string ret = "";
            Random random = new Random();
            if (numChar < 1) return ret;

            while (true)
            {
                ret = "";
                random = new Random();

                int valid = 0; 
                int num = 0;

                for (int i = 0; i < numChar; i++)
                {
                    num = 0;
                    valid = 0;
                    while (valid == 0)
                    {
                        num = random.Next(126);
                        if (((num > 64) && (num < 91)) ||
                            ((num > 96) && (num < 123)))
                        {
                            valid = 1;
                        }
                    }
                    ret += (char)num;
                }

                if (!val.Contains("$" + ret + "$")) break;
            }

            return ret;
        }

        private static string SanitizeFieldname(string val)
        {
            string ret = "";

            //
            // null, below ASCII range, above ASCII range
            //
            for (int i = 0; i < val.Length; i++)
            {
                if (((int)(val[i]) == 10) ||      // Preserve carriage return
                    ((int)(val[i]) == 13))        // and line feed
                {
                    ret += val[i];
                }
                else if ((int)(val[i]) < 32)
                {
                    continue;
                }
                else
                {
                    ret += val[i];
                }
            }

            //
            // double dash
            //
            int doubleDash = 0;
            while (true)
            {
                doubleDash = ret.IndexOf("--");
                if (doubleDash < 0)
                {
                    break;
                }
                else
                {
                    ret = ret.Remove(doubleDash, 2);
                }
            }

            //
            // open comment
            // 
            int openComment = 0;
            while (true)
            {
                openComment = ret.IndexOf("/*");
                if (openComment < 0) break;
                else
                {
                    ret = ret.Remove(openComment, 2);
                }
            }

            //
            // close comment
            //
            int closeComment = 0;
            while (true)
            {
                closeComment = ret.IndexOf("*/");
                if (closeComment < 0) break;
                else
                {
                    ret = ret.Remove(closeComment, 2);
                }
            }

            //
            // in-string replacement
            //
            ret = ret.Replace("'", "''");
            return ret;
        }

        public static string SelectQuery(string tableName, int? indexStart, int? maxResults, List<string> returnFields, Expression filter, string orderByClause)
        {
            string outerQuery = "";
            string whereClause = "";

            //
            // SELECT
            //
            outerQuery += "SELECT ";

            //
            // fields
            //
            if (returnFields == null || returnFields.Count < 1) outerQuery += "* ";
            else
            {
                int fieldsAdded = 0;
                foreach (string curr in returnFields)
                {
                    if (fieldsAdded == 0)
                    {
                        outerQuery += "\"" + SanitizeFieldname(curr) + "\"";
                        fieldsAdded++;
                    }
                    else
                    {
                        outerQuery += ",\"" + SanitizeFieldname(curr) + "\"";
                        fieldsAdded++;
                    }
                }
            }
            outerQuery += " ";

            //
            // table
            //
            outerQuery += "FROM " + tableName + " ";

            //
            // expressions
            //
            if (filter != null) whereClause = filter.ToWhereClause(DbTypes.PgSql);
            if (!String.IsNullOrEmpty(whereClause))
            {
                outerQuery += "WHERE " + whereClause + " ";
            }

            // 
            // order clause
            //
            if (!String.IsNullOrEmpty(orderByClause)) outerQuery += PreparedOrderByClause(orderByClause) + " ";

            //
            // limit
            //
            if (maxResults > 0)
            {
                if (indexStart != null && indexStart >= 0)
                {
                    outerQuery += "OFFSET " + indexStart + " LIMIT " + maxResults;
                }
                else
                {
                    outerQuery += "LIMIT " + maxResults;
                }
            }

            return outerQuery;
        }

        private static string PreparedOrderByClause(string val)
        {
            string ret = "";

            //
            // null, below ASCII range, above ASCII range
            //
            for (int i = 0; i < val.Length; i++)
            {
                if (((int)(val[i]) == 10) ||      // Preserve carriage return
                    ((int)(val[i]) == 13))        // and line feed
                {
                    ret += val[i];
                }
                else if ((int)(val[i]) < 32)
                {
                    continue;
                }
                else
                {
                    ret += val[i];
                }
            }

            //
            // double dash
            //
            int doubleDash = 0;
            while (true)
            {
                doubleDash = ret.IndexOf("--");
                if (doubleDash < 0)
                {
                    break;
                }
                else
                {
                    ret = ret.Remove(doubleDash, 2);
                }
            }

            //
            // open comment
            // 
            int openComment = 0;
            while (true)
            {
                openComment = ret.IndexOf("/*");
                if (openComment < 0) break;
                else
                {
                    ret = ret.Remove(openComment, 2);
                }
            }

            //
            // close comment
            //
            int closeComment = 0;
            while (true)
            {
                closeComment = ret.IndexOf("*/");
                if (closeComment < 0) break;
                else
                {
                    ret = ret.Remove(closeComment, 2);
                }
            }

            //
            // in-string replacement
            //
            ret = ret.Replace("'", "''");
            return ret;
        }

        public static string InsertQuery(string tableName, string keys, string values)
        {
            string ret =
                "INSERT INTO " + tableName + " " +
                "(" + keys + ") " +
                "VALUES " +
                "(" + values + ") " +
                "RETURNING *;"; 
            return ret;
        }

        public static string UpdateQuery(string tableName, string keyValueClause, Expression filter)
        {
            string ret =
                "UPDATE " + tableName + " SET " +
                keyValueClause + " ";

            if (filter != null) ret += "WHERE " + filter.ToWhereClause(DbTypes.PgSql) + " ";
            ret += "RETURNING *";

            return ret;
        }

        public static string DeleteQuery(string tableName, Expression filter)
        {
            string ret =
                "DELETE FROM " + tableName + " ";

            if (filter != null) ret += "WHERE " + filter.ToWhereClause(DbTypes.PgSql) + " ";

            return ret;
        }
    }
}
