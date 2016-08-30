// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tailspin.Surveys.Data.DataModels;
using Tailspin.Surveys.Data.DataStore;
using Tailspin.Surveys.Security.Policy;
using AppConfiguration = Tailspin.Surveys.WebAPI.Configuration;
using Constants = Tailspin.Surveys.Common.Constants;
using Microsoft.Extensions.PlatformAbstractions;

namespace Tailspin.Surveys.WebAPI
{
    /// <summary>
    /// This class contains the starup logic for this WebAPI project.
    /// </summary>
    public class Startup
    {
        private AppConfiguration.ConfigurationOptions _configOptions = new AppConfiguration.ConfigurationOptions();

        public Startup(IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            InitializeLogging(loggerFactory);
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json");

            if (env.IsDevelopment())
            {
                // This reads the configuration keys from the secret store.
                // For more details on using the user secret store see http://go.microsoft.com/fwlink/?LinkID=532709
                builder.AddUserSecrets();
            }
            builder.AddEnvironmentVariables();

            // Uncomment the block of code below if you want to load secrets from KeyVault
            // It is recommended to use certs for all authentication when using KeyVault
//#if NET451
//            var config = builder.Build();
//            builder.AddKeyVaultSecrets(config["AzureAd:ClientId"],
//                config["KeyVault:Name"],
//                config["AzureAd:Asymmetric:CertificateThumbprint"],
//                Convert.ToBoolean(config["AzureAd:Asymmetric:ValidationRequired"]),
//                loggerFactory);
//#endif

            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by a runtime.
        // Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy(PolicyNames.RequireSurveyCreator,
                    policy =>
                    {
                        policy.AddRequirements(new SurveyCreatorRequirement());
                        policy.RequireAuthenticatedUser(); // Adds DenyAnonymousAuthorizationRequirement 
                        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                    });
                options.AddPolicy(PolicyNames.RequireSurveyAdmin,
                    policy =>
                    {
                        policy.AddRequirements(new SurveyAdminRequirement());
                        policy.RequireAuthenticatedUser(); // Adds DenyAnonymousAuthorizationRequirement 
                        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                    });
            });

            // Add Entity Framework services to the services container.
            services.AddEntityFrameworkSqlServer()
                .AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(Configuration.GetSection("Data")["SurveysConnectionString"]));

            services.AddScoped<TenantManager, TenantManager>();
            services.AddScoped<UserManager, UserManager>();

            services.AddMvc();

            services.AddScoped<ISurveyStore, SqlServerSurveyStore>();
            services.AddScoped<IQuestionStore, SqlServerQuestionStore>();
            services.AddScoped<IContributorRequestStore, SqlServerContributorRequestStore>();
            services.AddSingleton<IAuthorizationHandler>(factory =>
            {
                var loggerFactory = factory.GetService<ILoggerFactory>();
                return new SurveyAuthorizationHandler(loggerFactory.CreateLogger<SurveyAuthorizationHandler>());
            });
        }

        // Configure is called after ConfigureServices is called.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ApplicationDbContext dbContext, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                //app.UseBrowserLink();
                app.UseDeveloperExceptionPage();

                app.UseDatabaseErrorPage();
                //app.UseDatabaseErrorPage(options =>
                //{
                //    options.ShowExceptionDetails = true;
                //});
            }

            // https://github.com/aspnet/Announcements/issues/164
            //app.UseIISPlatformHandler();

            app.UseJwtBearerAuthentication(new JwtBearerOptions {
                //
                Audience = _configOptions.AzureAd.WebApiResourceId,
                //
                Authority = Constants.AuthEndpointPrefix,
                TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters {
                    ValidateIssuer = false
                },
                Events= new SurveysJwtBearerEvents(loggerFactory.CreateLogger<SurveysJwtBearerEvents>())
            });
            //app.UseJwtBearerAuthentication(options =>
            //{
            //    options.Audience = _configOptions.AzureAd.WebApiResourceId;
            //    options.Authority = Constants.AuthEndpointPrefix + "common/";
            //    options.TokenValidationParameters = new TokenValidationParameters
            //    {
            //        //Instead of validating against a fixed set of known issuers, we perform custom multi-tenant validation logic
            //        ValidateIssuer = false,
            //    };
            //    options.Events = new SurveysJwtBearerEvents(loggerFactory.CreateLogger<SurveysJwtBearerEvents>());
            //});
            // Add MVC to the request pipeline.
            app.UseMvc();
        }
        private void InitializeLogging(ILoggerFactory loggerFactory)
        {
            //https://github.com/aspnet/Logging/commit/1308245d2c470fcf437299331b8175e2e417af04
            //loggerFactory.MinimumLevel = LogLevel.Information;

            loggerFactory.AddDebug(LogLevel.Information);
        }
    }
}