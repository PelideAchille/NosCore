﻿//  __  _  __    __   ___ __  ___ ___
// |  \| |/__\ /' _/ / _//__\| _ \ __|
// | | ' | \/ |`._`.| \_| \/ | v / _|
// |_|\__|\__/ |___/ \__/\__/|_|_\___|
// 
// Copyright (C) 2019 - NosCore
// 
// NosCore is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NosCore.Core.Configuration;
using NosCore.Core.I18N;
using NosCore.Data.Enumerations.I18N;
using NosCore.Shared.Configuration;
using Serilog;
using ILogger = Serilog.ILogger;

namespace NosCore.MasterServer
{
    public static class MasterServerBootstrap
    {
        private static readonly ILogger Logger = Shared.I18N.Logger.GetLoggerConfiguration().CreateLogger();

        public static void Main()
        {
            try
            {
                BuildWebHost(new string[0]).Run();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, LogLanguage.Instance.GetMessageFromKey(LogLanguageKey.EXCEPTION), ex.Message);
            }
        }

        private static IWebHost BuildWebHost(string[] args)
        {
            var conf = new MasterConfiguration();
            ConfiguratorBuilder.InitializeConfiguration(args, new[] { "logger.yml", "master.yml" }, conf);
            return WebHost.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddSerilog();
                })
                .ConfigureServices((hostContext, services) => services.AddSingleton(conf))
                .UseUrls(conf.WebApi!.ToString())
                .UseStartup<Startup>()
                .PreferHostingUrls(true)
                .SuppressStatusMessages(true)
                .Build();
        }
    }
}