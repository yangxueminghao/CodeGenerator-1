﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using Banana.AutoCode.Core;

namespace Banana.AutoCode.DbSchema
{
    public abstract class DbSchemaBase
    {
        protected virtual String ConnectionName { get; set; }

        public virtual DataContext Context { get; set; }
        
        public virtual String MetaDataCollectionName_Databases { get { return "Databases"; } }
        
        public virtual String MetaDataCollectionName_Tables { get { return "Tables"; } }

        public virtual String MetaDataCollectionName_Columns { get { return "Columns"; } }

        public virtual List<Database> GetDatabases()
        {
            var result = new List<Database>();

            var dt = GetSchema(MetaDataCollectionName_Databases, null);

            foreach (DataRow dr in dt.Rows)
            {
                var db = new Database();
                db.Name = dr["database_name"].ToString();

                result.Add(db);
            }

            return result;
        }

        public virtual List<Table> GetTables(Database db)
        {
            var result = new List<Table>();

            string[] restrictions = new string[4];
            restrictions[0] = db.Name;
            DataTable dt = GetSchema(MetaDataCollectionName_Tables, restrictions);

            foreach (DataRow dr in dt.Rows)
            {
                var table = new Table();
                table.Name = dr["table_name"].ToString();
                table.Owner = db.Name;

                result.Add(table);
            }

            return result.OrderBy(m => m.Name).ToList();
        }

        public virtual List<Column> GetColumns(Table table)
        {
            var result = new List<Column>();

            string[] restrictions = new string[4];
            restrictions[0] = table.Owner;
            restrictions[2] = table.Name;
            DataTable dt = GetSchema(MetaDataCollectionName_Columns, restrictions);
            var index = 0;

            foreach (DataRow dr in dt.Rows)
            {
                var column = new Column();
                column.Name = dr["column_name"].ToString();
                column.Comment = column.Name;
                column.IsNullable = dr["is_nullable"].ToString() == "YES" ? true : false;
                column.RawType = dr["data_type"].ToString();
                column.Length = dr["character_maximum_length"] == DBNull.Value ? -1 : Convert.ToInt32(dr["character_maximum_length"]);
                column.Precision = dr["numeric_precision"] == DBNull.Value ? (Int16)(-1) : Convert.ToInt16(dr["numeric_precision"]);
                column.Scale = dr["numeric_scale"] == DBNull.Value ? (Int16)(-1) : Convert.ToInt16(dr["numeric_scale"]);
                
                column.Type = this.GetType(column.RawType, column.Precision, column.Scale, column.IsNullable);
                column.TypeName = this.GetTypeName(column.RawType, column.Precision, column.Scale, column.IsNullable);
                column.DataType = this.GetDbType(column.RawType, column.Precision, column.Scale);
                column.Index = index++;

                result.Add(column);
            }

            return result;
        }

        public abstract Type GetType(String rawType, Int16 precision, Int16 scale, Boolean isNullable);

        public virtual String GetTypeName(String rawType, Int16 precision, Int16 scale, Boolean isNullable)
        {
            var type = GetType(rawType, precision, scale, isNullable);
            var nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType != null)
            {
                return GetTypeNameNormal(nullableType.Name + "?");
            }

            return GetTypeNameNormal(type.Name);
        }
        public string GetTypeNameNormal(string typeName)
        {
            return typeName.Replace("Byte", "byte")
                .Replace("Decimal", "decimal")
                .Replace("Int64", "long")
                .Replace("Int32", "int")
                .Replace("String", "string")
                .Replace("Object", "object")
                .Replace("Boolean", "bool");
        }

        public abstract DbType GetDbType(String rawType, Int16 precision, Int16 scale);

        protected static Type GetTypeOf<T>(Boolean isNullable) where T : struct
        {
            if (isNullable)
            {
                return typeof(Nullable<T>);
            }

            return typeof(T);
        }

        protected virtual void FixRawType(Column column)
        { 
            
        }

        public virtual Column Fill(IDataReader reader)
        {
            var column = new Column();

            column.Id = Convert.ToString(reader.GetValue(reader.GetOrdinal("Id")));
            column.Name = reader.GetString(reader.GetOrdinal("Name"));
            column.RawType = reader.GetString(reader.GetOrdinal("RawType"));
            //column.RawType2 = reader["RawType2"] == DBNull.Value ? string.Empty : reader.GetString(reader.GetOrdinal("RawType2"));
            column.Comment = reader["Comment"] == DBNull.Value ? string.Empty : reader.GetString(reader.GetOrdinal("Comment"));

            column.IsPrimaryKey = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("IsPrimaryKey")));
            column.IsForeignKey = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("IsForeignKey")));
            column.IsUnique = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("IsUnique")));
            column.IsNullable = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("IsNullable")));

            column.Length = reader["Length"] == DBNull.Value ? 0 : Convert.ToInt64(reader.GetValue(reader.GetOrdinal("Length")));
            column.Precision = reader["Precision"] == DBNull.Value ? (Int16)0 : Convert.ToInt16(reader.GetValue(reader.GetOrdinal("Precision")));
            column.Scale = reader["Scale"] == DBNull.Value ? (Int16)0 : Convert.ToInt16(reader.GetValue(reader.GetOrdinal("Scale")));
            
            FixRawType(column);
            column.Type = GetType(column.RawType, column.Precision, column.Scale, column.IsNullable);
            column.TypeName = GetTypeName(column.RawType, column.Precision, column.Scale, column.IsNullable);
            column.DataType = GetDbType(column.RawType, column.Precision, column.Scale);

            return column;
        }

        public DbSchemaBase(String connName)
        {
            ConnectionName = connName;

            Context = DataContextScope.GetCurrent(ConnectionName).DataContext;
        }

        public virtual DataTable GetSchema(String metaDataCollectionName, string[] restrictions)
        {
            DataTable resultTable;
            using (DbConnection conn = Context.DbProviderFactory.CreateConnection())
            {
                conn.ConnectionString = Context.GetConnectionString();
                conn.Open();

                if (string.IsNullOrEmpty(metaDataCollectionName))
                {
                    resultTable = conn.GetSchema();
                }
                else
                {
                    if (restrictions == null || restrictions.All(s => s == null))
                    {
                        resultTable = conn.GetSchema(metaDataCollectionName);
                    }
                    else
                    {
                        resultTable = conn.GetSchema(metaDataCollectionName, restrictions);
                    }
                }
            }

            return resultTable;
        }

        /// <summary>
        /// convert Oracle/MySQL/SQLite number type
        /// http://docs.oracle.com/cd/E51173_01/win.122/e17732/entityDataTypeMapping.htm
        /// </summary>
        /// <param name="precision"></param>
        /// <param name="scale"></param>
        /// <param name="isNullable"></param>
        /// <returns></returns>
        protected static Type ConvertToNumberType(Int16 precision, Int16 scale, Boolean isNullable)
        {
            if (scale == 0)
            {
                if (precision == 0)
                {
                    return GetTypeOf<Int64>(isNullable);
                }

                if (precision == 1)
                {
                    return GetTypeOf<Boolean>(isNullable);
                }

                if (precision <= 3)
                {
                    return GetTypeOf<Byte>(isNullable);
                }

                if (precision <= 4)
                {
                    return GetTypeOf<Int16>(isNullable);
                }

                if (precision <= 10)
                {
                    return GetTypeOf<Int32>(isNullable);
                }

                if (precision <= 19)
                {
                    return GetTypeOf<Int64>(isNullable);
                }
            }

            return GetTypeOf<Decimal>(isNullable);
        }
    }
}
