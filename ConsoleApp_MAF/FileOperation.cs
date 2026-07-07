using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp_MAF
{
    public class FileOperation
    {
        [Description("讀取文字內容")]
        async public Task<ToolResponse> ReadFile([Description("完整檔案路徑")]string filename)
        {
            var resp = new ToolResponse();
            try
            {
                resp.Data = await File.ReadAllTextAsync(filename);
            }
            catch(Exception ee)
            {
                resp.IsFail = true;
                resp.FailMessgae = ee.Message;
            }
            return resp;
        }

        [Description("取得現在的時間")]
        public ToolResponse GetCurrentDateTime()
            => new() { Data = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") };
        [Description("取得windows現在的使用者名稱")]
        public ToolResponse GetCurrentUser()
            => new() { Data = Environment.UserName };

        public void ReadDir(string dir)
        {
            
        }
    }
}
