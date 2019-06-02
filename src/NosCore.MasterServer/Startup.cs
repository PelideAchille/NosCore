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
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using FastExpressionCompiler;
using JetBrains.Annotations;
using Mapster;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NosCore.Configuration;
using NosCore.Core.Encryption;
using Swashbuckle.AspNetCore.Swagger;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using AutofacSerilogIntegration;
using NosCore.Core.Controllers;
using NosCore.Core.I18N;
using NosCore.Core.Networking;
using NosCore.Database.Entities;
using NosCore.Core;
using NosCore.Database.DAL;
using NosCore.Database;
using NosCore.Data;
using NosCore.MasterServer.Controllers;
using Microsoft.EntityFrameworkCore;

namespace NosCore.MasterServer
{
    public class Startup
    {
        private const string ConfigurationPath = "../../../configuration";
        private const string Title = "NosCore - MasterServer";
        private const string ConsoleText = "MASTER SERVER - NosCoreIO";

        private MasterConfiguration InitializeConfiguration()
        {
            var builder = new ConfigurationBuilder();
            var masterConfiguration = new MasterConfiguration();
            builder.SetBasePath(Directory.GetCurrentDirectory() + ConfigurationPath);
            builder.AddJsonFile("master.json", false);
            builder.Build().Bind(masterConfiguration);
            Validator.ValidateObject(masterConfiguration, new ValidationContext(masterConfiguration),
                validateAllProperties: true);
            var optionsBuilder = new DbContextOptionsBuilder<NosCoreContext>();
            optionsBuilder.UseNpgsql(masterConfiguration.Database.ConnectionString);
            DataAccessHelper.Instance.Initialize(optionsBuilder.Options);

            return masterConfiguration;
        }

        private static void RegisterDto(ContainerBuilder containerBuilder)
        {
            var registerDatabaseObject = typeof(Startup).GetMethod(nameof(RegisterDatabaseObject));
            var assemblyDto = typeof(IStaticDto).Assembly.GetTypes();
            var assemblyDb = typeof(Account).Assembly.GetTypes();

            assemblyDto.Where(p => typeof(IDto).IsAssignableFrom(p) && !p.Name.Contains("InstanceDto") && p.IsClass)
                .ToList()
                .ForEach(t =>
                {
                    var type = assemblyDb.First(tgo =>
                        string.Compare(t.Name, $"{tgo.Name}Dto", StringComparison.OrdinalIgnoreCase) == 0);
                    registerDatabaseObject.MakeGenericMethod(t, type).Invoke(null, new[] { containerBuilder });
                });

            containerBuilder.RegisterType<ItemInstanceDao>().As<IGenericDao<IItemInstanceDto>>().SingleInstance();
        }

        public static void RegisterDatabaseObject<TDto, TDb>(ContainerBuilder containerBuilder) where TDb : class
        {
            containerBuilder.RegisterType<GenericDao<TDb, TDto>>().As<IGenericDao<TDto>>().SingleInstance();
        }

        private ContainerBuilder InitializeContainer(IServiceCollection services)
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterType<MasterServer>().PropertiesAutowired();
            containerBuilder.RegisterType<TokenController>().PropertiesAutowired();
            containerBuilder.RegisterType<TokenController>().PropertiesAutowired();
            containerBuilder.RegisterLogger();
            containerBuilder.RegisterType<FriendRequestHolder>().SingleInstance();
            containerBuilder.RegisterType<WebApiAccess>().AsImplementedInterfaces().SingleInstance();
            containerBuilder.Populate(services);
            RegisterDto(containerBuilder);
            return containerBuilder;
        }

        [UsedImplicitly]
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            Console.Title = Title;
            Logger.PrintHeader(ConsoleText);
            var configuration = InitializeConfiguration();
            services.AddSingleton<IServerAddressesFeature>(new ServerAddressesFeature
            {
                PreferHostingUrls = true,
                Addresses = {configuration.WebApi.ToString()}
            });
            LogLanguage.Language = configuration.Language;
            services.AddSwaggerGen(c => c.SwaggerDoc("v1", new Info {Title = "NosCore Master API", Version = "v1"}));
            var keyByteArray = Encoding.Default.GetBytes(configuration.WebApi.Password.ToSha512());
            var signinKey = new SymmetricSecurityKey(keyByteArray);
            services.AddLogging(builder => builder.AddFilter("Microsoft", LogLevel.Warning));
            services.AddAuthentication(config => config.DefaultScheme = JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(cfg =>
                {
                    cfg.RequireHttpsMetadata = false;
                    cfg.SaveToken = true;
                    cfg.TokenValidationParameters = new TokenValidationParameters
                    {
                        IssuerSigningKey = signinKey,
                        ValidAudience = "Audience",
                        ValidIssuer = "Issuer",
                        ValidateIssuerSigningKey = true,
                        ValidateLifetime = true
                    };
                });

            services.AddMvc(o =>
                {
                    var policy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
                    o.Filters.Add(new AuthorizeFilter(policy));
                })
                .AddApplicationPart(typeof(TokenController).GetTypeInfo().Assembly)
                .AddApplicationPart(typeof(FriendController).GetTypeInfo().Assembly)
                .AddControllersAsServices();

            services.AddSignalR();
            var containerBuilder = InitializeContainer(services);
            containerBuilder.RegisterInstance(configuration).As<MasterConfiguration>();
            containerBuilder.RegisterInstance(configuration.WebApi).As<WebApiConfiguration>();
            var container = containerBuilder.Build();
            TypeAdapterConfig.GlobalSettings.Compiler = exp => exp.CompileFast();
            Task.Run(() => container.Resolve<MasterServer>().Run());
            return new AutofacServiceProvider(container);
        }

        [UsedImplicitly]
        public void Configure(IApplicationBuilder app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "NosCore Master API"));
            app.UseAuthentication();
            app.UseMvc();
        }
    }
}