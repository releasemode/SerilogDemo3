using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Serilog;
using Serilog.Events;
using System;
using Serilog.Enrichers.AspnetcoreHttpcontext;
using Microsoft.AspNetCore.Http;
using SimpleUI.Models;
using System.Linq;
using Serilog.Core;
using System.Reflection;
using Serilog.Exceptions;
using Serilog.Formatting.Compact;
using Serilog.Sinks.MSSqlServer;
using System.Collections.ObjectModel;
using System.Data;

namespace SimpleUI
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                CreateWebHostBuilder(args).Build().Run();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Host terminated unexpectedly");
                Console.Write(ex.ToString());
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }

            //var name = Assembly.GetExecutingAssembly().GetName();
            //Log.Logger = new LoggerConfiguration()
            //    .MinimumLevel.Debug()
            //    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            //    .Enrich.FromLogContext()
            //    .Enrich.WithMachineName()
            //    .Enrich.WithProperty("Assembly", $"{name.Name}")
            //    .Enrich.WithProperty("Version", $"{name.Version}")
            //    .WriteTo.File(new RenderedCompactJsonFormatter(), @"C:\users\edahl\Source\Logs\SimpleUi.json")
            //    .CreateLogger();

            //try
            //{
            //    Log.Information("Starting web host");
            //    CreateWebHostBuilder(args).Build().Run();
            //    return 0;
            //}
            //catch (Exception ex)
            //{
            //    Log.Fatal(ex, "Host terminated unexpectedly");
            //    return 1;
            //}
            //finally
            //{
            //    Log.CloseAndFlush();
            //}
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            return WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseSerilog((provider, context, loggerConfig) =>
                {
                    var name = Assembly.GetExecutingAssembly().GetName();
                    loggerConfig
                        .MinimumLevel.Information()
                        //.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                     
                        .Enrich.WithAspnetcoreHttpcontext(provider, false, 
                            AddCustomContextInfo)
                        .Enrich.FromLogContext()
                        //.Enrich.WithExceptionDetails()
                        .Enrich.WithMachineName()
                        .Enrich.WithProperty("Assembly", $"{name.Name}")
                        .Enrich.WithProperty("Version", $"{name.Version}")
                        //.WriteTo.File(new CompactJsonFormatter(), 
                        //    @"C:\users\edahl\Source\Logs\SimpleUi.json");
                        .WriteTo.MSSqlServer(
                        connectionString: @"Server=DESKTOP-TS8641A;Database=LogingDB;User Id=sa;Password=123456",
                        sinkOptions: new MSSqlServerSinkOptions { AutoCreateSqlTable = true, TableName = "UsageLog" },
                        columnOptions: GetSqlColumnOptions());
                });
        }
        private static ColumnOptions GetSqlColumnOptions()
        {
            var options = new ColumnOptions();
            options.Store.Remove(StandardColumn.Message);
            options.Store.Remove(StandardColumn.MessageTemplate);
            //options.Store.Remove(StandardColumn.Level);
            //options.Store.Remove(StandardColumn.Exception);

            //options.Store.Remove(StandardColumn.Properties);
            options.Store.Add(StandardColumn.LogEvent);
            options.LogEvent.ExcludeStandardColumns = true;
            options.LogEvent.ExcludeAdditionalProperties = true;

            options.AdditionalColumns = new Collection<SqlColumn>
            {
                new SqlColumn
                {
                    ColumnName = "UsageName",
                    AllowNull = false,
                    DataType = SqlDbType.NVarChar,
                    DataLength = 200,
                    NonClusteredIndex = true

                },
                new SqlColumn
                {
                    ColumnName = "ActionName", AllowNull = false
                },
                new SqlColumn
                {
                    ColumnName = "MachineName", AllowNull = false
                },
                new SqlColumn
                {
                    ColumnName = "ClientIP", AllowNull = true
                },

            };

            return options;
        }
        public static void AddCustomContextInfo(IHttpContextAccessor ctx,
            LogEvent le, ILogEventPropertyFactory pf)
        {
            HttpContext context = ctx.HttpContext;
            if (context == null) return;

            //var userInfo = context.Items["my-custom-info"] as UserInfo;
            //if (userInfo == null)
            //{
            //    var user = context.User.Identity;
            //    if (user == null || !user.IsAuthenticated) return;
            //    var i = 0;
            //    userInfo = new UserInfo
            //    {
            //        Name = user.Name,

            //        Claims = context.User.Claims.ToDictionary(x => $"{x.Type} ({i++})", y => y.Value)
            //    };
            //    context.Items["my-custom-info"] = userInfo;
            //}
            var ClientIP = context.Connection.RemoteIpAddress;
            le.AddPropertyIfAbsent(pf.CreateProperty("ClientIP", ClientIP, false));

        }
        //public static void AddCustomContextInfo(IHttpContextAccessor ctx, 
        //    LogEvent logEvent, ILogEventPropertyFactory pf)
        //{
        //    var context = ctx.HttpContext;
        //    if (context == null) return;

        //    var userInfo = context.Items["my-custom-info"] as UserInfo;
        //    if (userInfo == null)
        //    {
        //        var user = context.User.Identity;
        //        if (user == null || !user.IsAuthenticated) return;
        //        var i=0;
        //        userInfo = new UserInfo
        //        {
        //            Name = user.Name,
        //            Claims = context.User.Claims.ToDictionary(x => $"{x.Type} ({i++})", y => y.Value)
        //        };
        //        context.Items["my-custom-info"] = userInfo;
        //    }            

        //    logEvent.AddPropertyIfAbsent(pf.CreateProperty("UserInfo", userInfo, true));
        //}
    }
}
