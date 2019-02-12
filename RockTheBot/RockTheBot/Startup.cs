﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ready19.RockTheBot.Translation;

namespace Ready19.RockTheBot
{
    /// <summary>
    /// The Startup class configures services and the app's request pipeline.
    /// </summary>
    public class Startup
    {
        public static string TranslationKey;
        private ILoggerFactory _loggerFactory;
        private bool _isProduction = false;

        public Startup(IHostingEnvironment env)
        {
            _isProduction = env.IsProduction();

            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        /// <summary>
        /// Gets the configuration that represents a set of key/value application configuration properties.
        /// </summary>
        /// <value>
        /// The <see cref="IConfiguration"/> that represents a set of key/value application configuration properties.
        /// </value>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services">Specifies the contract for a <see cref="IServiceCollection"/> of service descriptors.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddBot<RockTheBot>(options =>
            {
                var secretKey = Configuration.GetSection("botFileSecret")?.Value;
                var botFilePath = Configuration.GetSection("botFilePath")?.Value;
                if (!File.Exists(botFilePath))
                {
                    throw new FileNotFoundException($"The .bot configuration file was not found. botFilePath: {botFilePath}");
                }

                // Loads .bot configuration file and adds a singleton that your Bot can access through dependency injection.
                var botConfig = BotConfiguration.Load(botFilePath ?? @".\rockthebot.bot", secretKey);
                services.AddSingleton(sp => botConfig ?? throw new InvalidOperationException($"The .bot configuration file could not be loaded. botFilePath: {botFilePath}"));

                // Retrieve current endpoint.
                var environment = _isProduction ? "production" : "development";
                var service = botConfig.Services.FirstOrDefault(s => s.Type == "endpoint" && s.Name == environment);
                if (!(service is EndpointService endpointService))
                {
                    throw new InvalidOperationException($"The .bot file does not contain an endpoint with name '{environment}'.");
                }

                //options.CredentialProvider = new SimpleCredentialProvider(endpointService.AppId, endpointService.AppPassword);
                //BOT ID and Pass are stored as env variables and injected by Kubernetes at runtime
                options.CredentialProvider = new SimpleCredentialProvider(
                    Environment.GetEnvironmentVariable("_ROCKTHEBOTAPPID") ?? string.Empty,
                    Environment.GetEnvironmentVariable("_ROCKTHEBOTAPPPWD") ?? string.Empty);

                // Creates a logger for the application to use.
                ILogger logger = _loggerFactory.CreateLogger<RockTheBot>();

                // Catches any errors that occur during a conversation turn and logs them.
                options.OnTurnError = async (turnContext, exception) =>
                {
                    logger.LogError($"Exception caught : {exception}");

                    // By-pass the middleware by sending the Activity directly on the Adapter.
                    var activity = MessageFactory.Text("Sorry, it looks like something went wrong.");
                    activity.ApplyConversationReference(turnContext.Activity.GetConversationReference());
                    await turnContext.Adapter.SendActivitiesAsync(turnContext, new[] { activity }, default(CancellationToken));
                };

                // The Memory Storage used here is for local bot debugging only. When the bot
                // is restarted, everything stored in memory will be gone.
                IStorage dataStore = new MemoryStorage();

                // For production bots use the Azure Blob or
                // Azure CosmosDB storage providers. For the Azure
                // based storage providers, add the Microsoft.Bot.Builder.Azure
                // Nuget package to your solution. That package is found at:
                // https://www.nuget.org/packages/Microsoft.Bot.Builder.Azure/
                // Uncomment the following lines to use Azure Blob Storage
                // Storage configuration name or ID from the .bot file.
                // const string StorageConfigurationId = "<STORAGE-NAME-OR-ID-FROM-BOT-FILE>";
                // var blobConfig = botConfig.FindServiceByNameOrId(StorageConfigurationId);
                // if (!(blobConfig is BlobStorageService blobStorageConfig))
                // {
                //    throw new InvalidOperationException($"The .bot file does not contain an blob storage with name '{StorageConfigurationId}'.");
                // }
                // // Default container name.
                // const string DefaultBotContainer = "<DEFAULT-CONTAINER>";
                // var storageContainer = string.IsNullOrWhiteSpace(blobStorageConfig.Container) ? DefaultBotContainer : blobStorageConfig.Container;
                // IStorage dataStore = new Microsoft.Bot.Builder.Azure.AzureBlobStorage(blobStorageConfig.ConnectionString, storageContainer);

                // Create and add conversation state.
                var convoState = new ConversationState(dataStore);
                options.State.Add(convoState);

                // Create and add user state.
                var userState = new UserState(dataStore);
                options.State.Add(userState);

                // Translation key from settings
                TranslationKey = Configuration.GetValue<string>("translatorKey");

                if (string.IsNullOrEmpty(TranslationKey))
                {
                    throw new InvalidOperationException("Microsoft Text Translation API key is missing. Please add your translation key to the 'translatorKey' setting.");
                }

                // Translation middleware setup
                var translator = new MicrosoftTranslator(TranslationKey);

                //TODO: complete the code!
            });

            // Create and register state accessors.
            // Accessors created here are passed into the IBot-derived class on every turn.
            services.AddSingleton(sp =>
            {
                // We need to grab the conversationState we added on the options in the previous step
                var options = sp.GetRequiredService<IOptions<BotFrameworkOptions>>().Value;
                if (options == null)
                {
                    throw new InvalidOperationException("BotFrameworkOptions must be configured prior to setting up the State Accessors");
                }

                var conversationState = options.State.OfType<ConversationState>().FirstOrDefault();
                if (conversationState == null)
                {
                    throw new InvalidOperationException("ConversationState must be defined and added before adding conversation-scoped state accessors.");
                }

                var userState = options.State.OfType<UserState>().FirstOrDefault();
                if (userState == null)
                {
                    throw new InvalidOperationException("UserState must be defined and added before adding user-scoped state accessors.");
                }

                // Create the custom state accessor.
                // State accessors enable other components to read and write individual properties of state.
                var accessors = new RockTheBotAccessors(conversationState, userState)
                {
                    ConversationDialogState = conversationState.CreateProperty<DialogState>("DialogState"),
                    LanguagePreference = userState.CreateProperty<string>("LanguagePreference"),
                    WelcomeUserState = userState.CreateProperty<WelcomeUserState>(RockTheBotAccessors.WelcomeUserName),
                    LanguageKey = userState.CreateProperty<string>(RockTheBotAccessors.TranslationKey),
                };

                return accessors;
            });

            // add config to the DI container
            services.AddSingleton<IConfiguration>(Configuration);
            services.AddSingleton<IRockTheBotServices, RockTheBotServices>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;

            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseBotFramework();
        }
    }
}
