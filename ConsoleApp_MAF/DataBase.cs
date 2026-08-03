using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace ConsoleApp_MAF
{
    public class DataBase
    {
        // 最大允許回傳列數（保護性預設）
        private const int DEFAULT_MAX_ROWS = 500;

        private ToolResponse MakeError(string msg)
            => new() { FailMessgae = msg };

        private bool IsSelectOnly(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return false;
            var s = sql.TrimStart();
            return s.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                || s.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);
        }

        private string GetConnectionString(string dbPath)
        {
            return new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();
        }

        [Description("列出資料庫中的所有資料表名稱")]
        public async Task<ToolResponse> ListTables([Description("資料庫檔案路徑，例如 ./data.db")] string dbPath)
        {
            var resp = new ToolResponse();
            try
            {
                if (!File.Exists(dbPath)) return MakeError("找不到資料庫檔案");

                using var conn = new SqliteConnection(GetConnectionString(dbPath));
                await conn.OpenAsync();

                var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    SELECT name
                    FROM sqlite_master
                    WHERE type='table' AND name NOT LIKE 'sqlite_%'
                    ORDER BY name;
                    """;

                var list = new List<string>();
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(reader.GetString(0));
                    }
                }
                
                resp.Data = list.ToJsonString();
            }
            catch (Exception ex) { resp.FailMessgae = ex.Message; }
            return resp;
        }

        //[Description("取得指定資料表的欄位資訊")]
        //public async Task<ToolResponse> GetTableSchema([Description("資料庫檔案路徑")] string dbPath,
        //                                                             [Description("資料表名稱")] string tableName)
        //{
        //    var resp = new ToolResponse();
        //    try
        //    {
        //        if (!File.Exists(dbPath)) return MakeError("找不到資料庫檔案");
        //        if (string.IsNullOrWhiteSpace(tableName)) return MakeError("需要 tableName");

        //        using var conn = new SqliteConnection(GetConnectionString(dbPath));
        //        await conn.OpenAsync();

        //        var cmd = conn.CreateCommand();
        //        cmd.CommandText = $"PRAGMA table_info('{tableName.Replace("'", "''")}');";

        //        var cols = new List<object>();
        //        await using (var reader = await cmd.ExecuteReaderAsync())
        //        {
        //            while (await reader.ReadAsync())
        //            {
        //                cols.Add(new
        //                {
        //                    cid = reader.GetInt32(0),
        //                    name = reader.GetString(1),
        //                    type = reader.GetString(2),
        //                    notnull = reader.GetInt32(3) == 1,
        //                    dflt_value = reader.IsDBNull(4) ? null : reader.GetValue(4),
        //                    pk = reader.GetInt32(5) == 1
        //                });
        //            }
        //        }
        //        resp.Data = cols.ToJsonString();
        //    }
        //    catch (Exception ex) { resp.FailMessgae = ex.Message; }
        //    return resp;
        //}


        async public Task<ToolResponse> GetTableTable([Description("資料庫檔案路徑")] string dbPath, [Description("資料表名稱")] string tableName)
        {
            var resp = new ToolResponse();
            try
            {
                if (!File.Exists(dbPath)) return MakeError("找不到資料庫檔案");
                if (string.IsNullOrWhiteSpace(tableName)) return MakeError("需要 tableName");

                using var conn = new SqliteConnection(GetConnectionString(dbPath));
                await conn.OpenAsync();

                var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT sql FROM sqlite_master WHERE type = 'table' AND name ='{tableName.Replace("'", "''")}');";

                var cols = new List<object>();
                await using (var reader = await cmd.ExecuteReaderAsync())
                {

                }
                resp.Data = cols.ToJsonString();
            }
            catch (Exception ex) { resp.FailMessgae = ex.Message; }
            return resp;
        }

        //[Description("以 SQL 查詢資料（僅允許 SELECT），會回傳 columns 與 rows；可指定 limit/offset")]
        //public async Task<ToolResponse<Dictionary<string, object>>> Query([Description("資料庫檔案路徑")] string dbPath,
        //                                                                  [Description("僅允許 SELECT 查詢")] string sql,
        //                                                                  [Description("最大回傳列數, 預設500")] int maxRows = DEFAULT_MAX_ROWS,
        //                                                                  CancellationToken cancellation = default)
        //{
        //    var resp = new ToolResponse<Dictionary<string, object>>();
        //    try
        //    {
        //        if (!File.Exists(dbPath)) return MakeError<Dictionary<string, object>>("找不到資料庫檔案");
        //        if (!IsSelectOnly(sql)) return MakeError<Dictionary<string, object>>("僅允許 SELECT 查詢");

        //        // 確保有 LIMIT（避免無限回傳）
        //        string safeSql = sql.TrimEnd();
        //        if (!safeSql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase))
        //        {
        //            safeSql += $" LIMIT {maxRows}";
        //        }

        //        using var conn = new SqliteConnection(GetConnectionString(dbPath));
        //        await conn.OpenAsync(cancellation);

        //        var cmd = conn.CreateCommand();
        //        cmd.CommandText = safeSql;

        //        var result = new Dictionary<string, object>();
        //        var columns = new List<string>();
        //        var rows = new List<object[]>();

        //        await using (var reader = await cmd.ExecuteReaderAsync(cancellation))
        //        {
        //            for (int i = 0; i < reader.FieldCount; i++) columns.Add(reader.GetName(i));

        //            while (await reader.ReadAsync(cancellation))
        //            {
        //                var row = new object[reader.FieldCount];
        //                for (int i = 0; i < reader.FieldCount; i++)
        //                {
        //                    row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        //                }
        //                rows.Add(row);
        //            }
        //        }

        //        result["columns"] = columns;
        //        result["rows"] = rows;
        //        resp.Data = result;
        //    }
        //    catch (OperationCanceledException) { resp.FailMessgae = "查詢已取消"; }
        //    catch (Exception ex) { resp.FailMessgae = ex.Message; }
        //    return resp;
        //}

        //[Description("快速取得資料表前 N 列（預覽）")]
        //public Task<ToolResponse<Dictionary<string, object>>> PreviewTable([Description("資料庫檔案路徑")] string dbPath,
        //                                                                   [Description("資料表名稱")] string tableName,
        //                                                                   [Description("回傳列數")] int limit = 20,
        //                                                                   CancellationToken cancellation = default)
        //{
        //    var sql = $"SELECT * FROM \"{tableName.Replace("\"", "\"\"")}\" LIMIT {limit}";
        //    return Query(dbPath, sql, limit, cancellation);
        //}

        //[Description("取得資料表列數")]
        //public async Task<ToolResponse<long>> RowCount([Description("資料庫檔案路徑")] string dbPath,
        //                                               [Description("資料表名稱")] string tableName)
        //{
        //    var resp = new ToolResponse<long>();
        //    try
        //    {
        //        if (!File.Exists(dbPath)) return MakeError<long>("找不到資料庫檔案");
        //        using var conn = new SqliteConnection(GetConnectionString(dbPath));
        //        await conn.OpenAsync();

        //        var cmd = conn.CreateCommand();
        //        cmd.CommandText = $"SELECT COUNT(1) FROM \"{tableName.Replace("\"", "\"\"")}\";";

        //        var scalar = await cmd.ExecuteScalarAsync();
        //        resp.Data = Convert.ToInt64(scalar ?? 0);
        //    }
        //    catch (Exception ex) { resp.FailMessgae = ex.Message; }
        //    return resp;
        //}
    }
}
