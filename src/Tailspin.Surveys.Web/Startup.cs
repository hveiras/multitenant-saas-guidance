// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tailspin.Surveys.Data.DataModels;
using Tailspin.Surveys.Web.Security;
using Tailspin.Surveys.Security.Policy;
using Tailspin.Surveys.Web.Services;
using Constants = Tailspin.Surveys.Common.Constants;
using SurveyAppConfiguration = Tailspin.Surveys.Web.Configuration;
using Microsoft.Extensions.PlatformAbstractions;
using System.Threading.Tasks;
using Tailspin.Surveys.Common;
using Tailspin.Surveys.TokenStorage;
using System.IO;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace Tailspin.Surveys.Web
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IHostingEnvironment env, ApplicationEnvironment appEnv, ILoggerFactory loggerFactory)
        {
            InitializeLogging(loggerFactory);

            // Setup configuration sources.
            var builder = new ConfigurationBuilder()
                .SetBasePath(appEnv.ApplicationBasePath)
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

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
//            _configuration = builder.Build();
//            builder.AddKeyVaultSecrets(_configuration["AzureAd:ClientId"],
//                _configuration["KeyVault:Name"],
//                _configuration["AzureAd:Asymmetric:CertificateThumbprint"],
//                Convert.ToBoolean(_configuration["AzureAd:Asymmetric:ValidationRequired"]),
//                loggerFactory);
//#endif

            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var configOptions = new SurveyAppConfiguration.ConfigurationOptions();
            //Configuration.Bind(configOptions);

            var adOptions = configOptions.AzureAd;
            services.Configure<SurveyAppConfiguration.ConfigurationOptions>(co => {
                co = configOptions;
            });

#if NET451
            // This will add the Redis implementation of IDistributedCache
            services.AddDistributedRedisCache(setup => {
                setup.Configuration = configOptions.Redis.Configuration;
            });
#endif

            // This will only add the LocalCache implementation of IDistributedCache if there is not an IDistributedCache already registered.
            services.AddDistributedMemoryCache();
            
            services.AddAuthorization(options =>
            {
                options.AddPolicy(PolicyNames.RequireSurveyCreator,
                    policy =>
                    {
                        policy.AddRequirements(new SurveyCreatorRequirement());
                        policy.RequireAuthenticatedUser(); // Adds DenyAnonymousAuthorizationRequirement 
                        // By adding the CookieAuthenticationDefaults.AuthenticationScheme,
                        // if an authenticated user is not in the appropriate role, they will be redirected to the "forbidden" experience.
                        policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
                    });

                options.AddPolicy(PolicyNames.RequireSurveyAdmin,
                    policy =>
                    {
                        policy.AddRequirements(new SurveyAdminRequirement());
                        policy.RequireAuthenticatedUser(); // Adds DenyAnonymousAuthorizationRequirement 
                        // By adding the CookieAuthenticationDefaults.AuthenticationScheme,
                        // if an authenticated user is not in the appropriate role, they will be redirected to the "forbidden" experience.
                        policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
                    });
            });

            // Add Entity Framework services to the services container.
            services.AddEntityFrameworkSqlServer()
                .AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(Configuration.GetSection("Data")["SurveysConnectionString"]));

            // Add MVC services to the services container.
            services.AddMvc();

            // Uncomment the following line to add Web API services which makes it easier to port Web API 2 controllers.
            // You will also need to add the Microsoft.AspNet.Mvc.WebApiCompatShim package to the 'dependencies' section of project.json.
            // services.AddWebApiConventions();

            // Register application services.

            // This will register IDistributedCache based token cache which ADAL will use for caching access tokens.
            services.AddScoped<ITokenCacheService, DistributedTokenCacheService>();

            services.AddScoped<ISurveysTokenService, SurveysTokenService>();
            services.AddSingleton<HttpClientService>();

            // Use this for client certificate support
            //services.AddSingleton<ICredentialService, CertificateCredentialService>();
            services.AddSingleton<ICredentialService, ClientCredentialService>();
            services.AddScoped<ISurveyService, SurveyService>();
            services.AddScoped<IQuestionService, QuestionService>();
            services.AddScoped<SignInManager, SignInManager>();
            services.AddScoped<TenantManager, TenantManager>();
            services.AddScoped<UserManager, UserManager>();
        }

        // Configure is called after ConfigureServices is called.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            var configOptions = app.ApplicationServices.GetService<IOptions<SurveyAppConfiguration.ConfigurationOptions>>().Value;
            // Configure the HTTP request pipeline.
            // Add the following to the request pipeline only in development environment.
            if (env.IsDevelopment())
            {
                //app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                // Add Error handling middleware which catches all application specific errors and
                // sends the request to the following path or controller action.
                app.UseExceptionHandler("/Home/Error");
            }

            // Add the platform handler to the request pipeline.
            // https://github.com/aspnet/Announcements/issues/164
            //app.UseIISPlatformHandler();

            // Add static files to the request pipeline.
            app.UseStaticFiles();

            // Add cookie-based authentication to the request pipeline.
            app.UseCookieAuthentication(new CookieAuthenticationOptions {
                AutomaticAuthenticate = true,
                AutomaticChallenge = true,
                AccessDeniedPath = "/Home/Forbidden",
                CookieSecure = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always,

                // The default setting for cookie expiration is 14 days. SlidingExpiration is set to true by default
                ExpireTimeSpan = TimeSpan.FromHours(1),
                SlidingExpiration = true
            });

            // Add OpenIdConnect middleware so you can login using Azure AD.
            app.UseOpenIdConnectAuthentication(new OpenIdConnectOptions {
                AutomaticAuthenticate = true,
                AutomaticChallenge = true,
                ClientId = configOptions.AzureAd.ClientId,
                Authority = Constants.AuthEndpointPrefix + "common/",
                PostLogoutRedirectUri = configOptions.AzureAd.PostLogoutRedirectUri,
                SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme,
                TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = false },
                Events = new SurveyAuthenticationEvents(configOptions.AzureAd, loggerFactory)
            });

            // Add MVC to the request pipeline.
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");

                // Uncomment the following line to add a route for porting Web API 2 controllers.
                // routes.MapWebApiRoute("DefaultApi", "api/{controller}/{id?}");
            });
        }

        private void InitializeLogging(ILoggerFactory loggerFactory)
        {
            //https://github.com/aspnet/Logging/commit/1308245d2c470fcf437299331b8175e2e417af04
            //loggerFactory.MinimumLevel = LogLevel.Information;

            loggerFactory.AddDebug(LogLevel.Information);
        }

        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
