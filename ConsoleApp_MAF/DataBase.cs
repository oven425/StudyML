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

        async Task<List<string>> GetAllTables(string dbPath)
        {
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
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(reader.GetString(0));
            }
            return list;
        }

        [Description("列出 SQLite 資料庫中的所有資料表名稱。撰寫資料查詢前，先使用此工具確認資料表是否存在。")]
        public async Task<ToolResponse> ListTables([Description("SQLite 資料庫檔案路徑；可使用完整絕對路徑或相對於目前工作目錄的路徑，例如 data/mydb.sqlite")] string dbPath)
        {
            var resp = new ToolResponse();
            try
            {
                if (!File.Exists(dbPath)) return MakeError("找不到資料庫檔案");

                //using var conn = new SqliteConnection(GetConnectionString(dbPath));
                //await conn.OpenAsync();

                //var cmd = conn.CreateCommand();
                //cmd.CommandText = """
                //    SELECT name
                //    FROM sqlite_master
                //    WHERE type='table' AND name NOT LIKE 'sqlite_%'
                //    ORDER BY name;
                //    """;

                //var list = new List<string>();
                //await using (var reader = await cmd.ExecuteReaderAsync())
                //{
                //    while (await reader.ReadAsync())
                //    {
                //        list.Add(reader.GetString(0));
                //    }
                //}

                //resp.Data = list.ToJsonString();
                var list = await GetAllTables(dbPath);
                resp.Data = list.ToJsonString();
            }
            catch (Exception ex) { resp.FailMessgae = ex.Message; }
            return resp;
        }

        async Task<List<string>> GetAllSchemas(string dbPath)
        {
            var alltables = await this.GetAllTables(dbPath);
            using var conn = new SqliteConnection(GetConnectionString(dbPath));
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            List<string> allsechma = [];
            foreach (var table in alltables)
            {
                cmd.CommandText = $"SELECT sql FROM sqlite_master WHERE type = 'table' AND name ='{table.Replace("'", "''")}';";
                await using var reader = await cmd.ExecuteReaderAsync();
                await reader.ReadAsync();
                allsechma.Add(reader.GetString(0));
            }
            return allsechma;
        }

        [Description("取得 SQLite 資料庫所有資料表的 DDL Schema（CREATE TABLE）。撰寫資料查詢前，使用此工具確認資料表欄位與 Foreign Key 關聯，避免猜測名稱。")]
        async public Task<ToolResponse> GetTableSchemas([Description("SQLite 資料庫檔案路徑；可使用完整絕對路徑或相對於目前工作目錄的路徑，例如 data/mydb.sqlite")] string dbPath)
        {
            var resp = new ToolResponse();
            try
            {
                if (!File.Exists(dbPath)) return MakeError("找不到資料庫檔案");
                var alltables = await this.GetAllTables(dbPath);
                using var conn = new SqliteConnection(GetConnectionString(dbPath));
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                List<string> allschma = [];
                foreach (var table in alltables)
                {
                    cmd.CommandText = $"SELECT sql FROM sqlite_master WHERE type = 'table' AND name ='{table.Replace("'", "''")}';";
                    await using var reader = await cmd.ExecuteReaderAsync();
                    await reader.ReadAsync();
                    allschma.Add(reader.GetString(0));
                }
                resp.Data = allschma.ToJsonString();
                //cmd.CommandText = $"SELECT sql FROM sqlite_master WHERE type = 'table' AND name ='{tableName.Replace("'", "''")}';";
                //await using var reader = await cmd.ExecuteReaderAsync();
                //await reader.ReadAsync();
                //resp.Data = reader.GetString(0);
            }
            catch (Exception ex)
            {
                resp.FailMessgae = ex.Message;
            }
            return resp;
        }


        //[Description("取得指定資料表或整個資料庫的 DDL Schema，用來分析表格欄位與 Foreign Key 關聯結構。")]
        //async public Task<ToolResponse> GetTableSchema([Description("資料庫檔案路徑")] string dbPath, [Description("資料表名稱")] string tableName)
        //{
        //    var resp = new ToolResponse();
        //    try
        //    {
        //        if (!File.Exists(dbPath)) return MakeError("找不到資料庫檔案");
        //        if (string.IsNullOrWhiteSpace(tableName)) return MakeError("需要 tableName");

        //        using var conn = new SqliteConnection(GetConnectionString(dbPath));
        //        await conn.OpenAsync();

        //        var cmd = conn.CreateCommand();
        //        cmd.CommandText = $"SELECT sql FROM sqlite_master WHERE type = 'table' AND name ='{tableName.Replace("'", "''")}';";
        //        await using var reader = await cmd.ExecuteReaderAsync();
        //        await reader.ReadAsync();
        //        resp.Data = reader.GetString(0);
        //    }
        //    catch (Exception ex) 
        //    {
        //        resp.FailMessgae = ex.Message; 
        //    }
        //    return resp;
        //}

        [Description("在 SQLite 資料庫執行唯讀查詢，回傳以欄位名稱對應值的 JSON 物件陣列。撰寫 SQL 前必須先使用 ListTables 或 GetTableSchemas 確認資料表與欄位名稱。")]
        public async Task<ToolResponse> Query([Description("SQLite 資料庫檔案路徑；可使用完整絕對路徑或相對於目前工作目錄的路徑，例如 data/mydb.sqlite")] string dbPath,
                                              [Description("唯讀 SQL；只能以 SELECT 或 WITH 開頭，禁止 INSERT、UPDATE、DELETE、DROP 等修改語句。必須加入 LIMIT（建議 LIMIT 100）以避免回傳資料過大。")] string sql)
        {
            var resp = new ToolResponse();
            try
            {
                if (!File.Exists(dbPath)) return MakeError("找不到資料庫檔案");
                if (!IsSelectOnly(sql)) return MakeError("僅允許 SELECT 查詢");

                using var conn = new SqliteConnection(GetConnectionString(dbPath));
                await conn.OpenAsync();

                var cmd = conn.CreateCommand();
                cmd.CommandText = sql;

                var rows = new List<Dictionary<string, object?>>();

                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    var columns = new string[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++) columns[i] = reader.GetName(i);

                    while (await reader.ReadAsync())
                    {
                        var row = new Dictionary<string, object?>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                        rows.Add(row);
                    }
                }

                resp.Data = rows.ToJsonString();
            }
            catch (OperationCanceledException) { resp.FailMessgae = "查詢已取消"; }
            catch (Exception ex) { resp.FailMessgae = ex.Message; }
            return resp;
        }


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
