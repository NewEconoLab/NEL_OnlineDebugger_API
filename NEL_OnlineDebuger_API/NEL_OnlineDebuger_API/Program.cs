using System.IO;
using System.Net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace NEL_OnlineDebuger_API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseKestrel(options =>
                {
                    options.Listen(IPAddress.Any, getServerPort());
                })
                .Build();


        private static int getServerPort()
        {
            int serverPort = 0;

            //尝试从配置文件读取，失败则默认1189
            try
            {
                var config = new ConfigurationBuilder()
               .AddInMemoryCollection()    //将配置文件的数据加载到内存中
               .SetBasePath(Directory.GetCurrentDirectory())   //指定配置文件所在的目录
               .AddJsonFile("mongodbsettings.json", optional: true, reloadOnChange: true)  //指定加载的配置文件
               .Build();    //编译成对象        

                serverPort = int.Parse(config["appPort"].ToString());
            }
            catch
            {
                //serverPort = 1189;
                serverPort = 88;
            }

            return serverPort;
        }
    }
}
