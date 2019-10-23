﻿using System;
using System.Data.SqlClient;
using System.Collections;
using System.Data;
using System.IO;
using CsvHelper;

//https://github.com/22222/CsvTextFieldParser
namespace ArdiLabs.Yuniql
{
    public class CsvImportService : ICsvImportService
    {
        public CsvImportService()
        {
        }
        public void Run(IDbConnection connection, IDbTransaction transaction, string csvFileFullPath)
        {
            //read csv file and load into data table
            var dataTable = ParseCsvFile(csvFileFullPath);

            //save the csv data into staging sql table
            BulkCopyWithDataTable(connection, transaction, dataTable);

            //TODO: validate staging data against destination table schema defs

            //TODO: transport staging data into destination table
        }

        private DataTable ParseCsvFile(string csvFileFullPath)
        {
            var csvDatatable = new DataTable();
            csvDatatable.TableName = Path.GetFileNameWithoutExtension(csvFileFullPath);

            using (var csvReader = new CsvTextFieldParser(csvFileFullPath))
            {
                csvReader.Delimiters = (new string[] { "," });
                csvReader.HasFieldsEnclosedInQuotes = true;

                string[] csvColumns = csvReader.ReadFields();
                foreach (string csvColumn in csvColumns)
                {
                    var dataColumn = new DataColumn(csvColumn);
                    dataColumn.AllowDBNull = true;
                    csvDatatable.Columns.Add(dataColumn);
                }

                while (!csvReader.EndOfData)
                {
                    string[] fieldData = csvReader.ReadFields();
                    for (int i = 0; i < fieldData.Length; i++)
                    {
                        if (fieldData[i] == "")
                        {
                            fieldData[i] = null;
                        }
                    }
                    csvDatatable.Rows.Add(fieldData);
                }
            }
            return csvDatatable;
        }

        private IDataReader ParseCsvAsDataReader(string csvFileFullPath)
        {
            using (var streamReader = new StreamReader(csvFileFullPath))
            using (var csvReader = new CsvReader(streamReader))
            {
                return new CsvDataReader(csvReader);
            }
        }

        private void BulkCopyWithDataTable(IDbConnection connection, IDbTransaction transaction, DataTable csvFileDatatTable)
        {
            using (var sqlBulkCopy = new SqlBulkCopy(connection as SqlConnection, SqlBulkCopyOptions.Default, transaction as SqlTransaction))
            {
                sqlBulkCopy.DestinationTableName = csvFileDatatTable.TableName;
                sqlBulkCopy.BatchSize = 0;
                sqlBulkCopy.EnableStreaming = true;
                sqlBulkCopy.SqlRowsCopied += SqlBulkCopy_SqlRowsCopied;
                foreach (var column in csvFileDatatTable.Columns)
                {
                    sqlBulkCopy.ColumnMappings.Add(column.ToString(), column.ToString());
                }
                sqlBulkCopy.WriteToServer(csvFileDatatTable);
            }
        }

        private void BulkCopyWithDataReader(IDbConnection connection, IDbTransaction transaction, string destinationTableName, IDataReader csvFileDatatTable)
        {
            using (var sqlBulkCopy = new SqlBulkCopy(connection as SqlConnection, SqlBulkCopyOptions.Default, transaction as SqlTransaction))
            {
                sqlBulkCopy.DestinationTableName = destinationTableName;
                sqlBulkCopy.BatchSize = 0;
                sqlBulkCopy.EnableStreaming = true;
                sqlBulkCopy.SqlRowsCopied += SqlBulkCopy_SqlRowsCopied;
                sqlBulkCopy.WriteToServer(csvFileDatatTable);
            }
        }

        private void SqlBulkCopy_SqlRowsCopied(object sender, SqlRowsCopiedEventArgs e)
        {
            TraceService.Info($"CsvImportService copied {e.RowsCopied} rows");
        }
    }
}

