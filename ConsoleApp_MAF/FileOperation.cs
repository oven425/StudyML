using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ConsoleApp_MAF
{
    public class FileOperation
    {
        [Description("讀取本機電腦中指定路徑的文字類型的檔案(*.csv,*.txt.....")]
        async public Task<ToolResponse> ReadTxt([Description("檔案的絕對路徑或相對路徑，例如 C:\\Users\\user\\Desktop\\config.json 或 ./data.csv")]string filename)
        {
            var resp = new ToolResponse();
            try
            {
                resp.Data = await File.ReadAllTextAsync(filename);
            }
            catch(Exception ee)
            {
                resp.FailMessgae = ee.Message;
            }
            return resp;
        }

        [Description("讀取本機電腦中指定路徑的圖片類型的檔案(*.jpg,*.png.....")]
        async public Task<ToolResponse> ReadImage([Description("檔案的絕對路徑或相對路徑，例如 C:\\Users\\user\\Desktop\\config.jpg 或 ./data.png")] string filename)
        {
            //ChatMessage cm = new ChatMessage();
            var resp = new ToolResponse();
            try
            {
                if(File.Exists(filename))
                {
                    resp.ImageFileName = filename;
                    resp.Data = "圖片已經載入";
                }
                else
                {
                    resp.FailMessgae = "找不到圖片檔案";
                }
            }
            catch (Exception ee)
            {
                resp.FailMessgae = ee.Message;
            }
            return resp;
            //cm.Contents.Add(new TextContent("123"));
            //cm.Contents.Add(new TextContent("456"));
            //return cm;
        }
    }

    public class ComputerInfo
    {
        [Description("取得現在的時間")]
        public ToolResponse GetCurrentDateTime()
            => new() { Data = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") };
        [Description("取得windows現在的使用者名稱")]
        public ChatMessage GetCurrentUser()
            => new() { Contents = [new TextContent(Environment.UserName)] };

        [Description("取得指定 Windows 系統特殊資料夾的絕對路徑")]
        public ToolResponse GetFolder([Description("The name of the .NET Environment.SpecialFolder enum (e.g., Desktop, MyDocuments).")]string folder)
        {
            var sss = folder switch
            {
                "Desktop" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                _ => "",
            };
            return new ToolResponse()
            {
                Data = sss,
            };
        }

        [Description("列出指定資料夾中的所有檔案與目錄")]
        public ToolResponse list_directory([Description("資料夾的絕對路徑")]string dir_path)
        {
            ToolResponse resp = new();
            try
            {
                var dir = new DirectoryInfo(dir_path);
                var dirs = dir.GetDirectories().Select(x => new { x.Name, x.CreationTime });
                var files = dir.GetFiles().Select(x => new { x.Name, x.CreationTime });
                var alls = dirs.Concat(files).OrderBy(x => x.CreationTime)
                    .Select(x => new { x.Name, CreateTime = x.CreationTime.ToString("yyyy/MM/dd HH:mm:ss") });
                //var str_alls = JsonSerializer.Serialize(alls, new JsonSerializerOptions()
                //{
                //    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                //});
                //resp.Data = str_alls;
                var strb = new StringBuilder();
                strb.AppendLine("| Name | CreateTime |");
                strb.AppendLine("| :--- | :--- |");
                foreach(var oo in  alls)
                {
                    strb.AppendLine($"| {oo.Name} | {oo.CreateTime} |");
                }
                resp.Data = strb.ToString();
            }
            catch (Exception ee)
            {
                resp.FailMessgae = ee.Message;
            }

            return resp;
        }

    }
}
