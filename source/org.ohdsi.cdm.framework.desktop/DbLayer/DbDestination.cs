﻿using org.ohdsi.cdm.framework.desktop.Helpers;
using System;
using System.Data.Odbc;

namespace org.ohdsi.cdm.framework.desktop.DbLayer
{
    public class DbDestination
    {
        private readonly string _connectionString;
        private readonly string _schemaName;
        private readonly string _database;

        public DbDestination(string connectionString, string schemaName, string database)
        {
            _connectionString = connectionString;
            _schemaName = schemaName;
            _database = database;
        }

        public void CreateDatabase(string query)
        {
            var sqlConnectionStringBuilder = new OdbcConnectionStringBuilder(_connectionString);
            var database = sqlConnectionStringBuilder["database"];

            // TMP
            var mySql = _connectionString.ToLower().Contains("mysql");
            var postgres = _connectionString.ToLower().Contains("postgres");

            // if the db name is not specified, it's hardcoded based on the database type
            if (_database != null)
                sqlConnectionStringBuilder["database"] = _database;
            else if (mySql)
                sqlConnectionStringBuilder["database"] = "mysql";
            else if (_connectionString.ToLower().Contains("amazon redshift"))
                sqlConnectionStringBuilder["database"] = "poc";
            else
                sqlConnectionStringBuilder["database"] = "master";

            using (var connection = SqlConnectionHelper.OpenOdbcConnection(sqlConnectionStringBuilder.ConnectionString))
            {
                query = string.Format(query, database);

                foreach (var subQuery in query.Split(new[] { "\r\nGO", "\nGO" }, StringSplitOptions.None))
                {
                    using (var command = new OdbcCommand(subQuery, connection))
                    {
                        command.CommandTimeout = 30000;
                        command.ExecuteNonQuery();
                    }
                }
            }

            if (!mySql && !postgres && _schemaName.ToLower().Trim() != "dbo")
            {
                CreateSchema(postgres);
            }
        }

        public void CreateSchema(bool isPostgres)
        {
            using (var connection = SqlConnectionHelper.OpenOdbcConnection(_connectionString))
            {
                string query;
                if (isPostgres) query = $"CREATE SCHEMA IF NOT EXISTS {_schemaName}";
                else query = $"create schema [{_schemaName}]";

                using (var command = new OdbcCommand(query, connection))
                {
                    command.CommandTimeout = 0;
                    command.ExecuteNonQuery();
                }
            }
        }

        public void ExecuteQuery(string query)
        {
            using (var connection = SqlConnectionHelper.OpenOdbcConnection(_connectionString))
            {
                query = query.Replace("{sc}", _schemaName);

                foreach (var subQuery in query.Split(new[] { "\r\nGO", "\nGO", ";" }, StringSplitOptions.None))
                {
                    if (string.IsNullOrEmpty(subQuery))
                        continue;

                    using (var command = new OdbcCommand(subQuery, connection))
                    {
                        command.CommandTimeout = 30000;
                        command.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
