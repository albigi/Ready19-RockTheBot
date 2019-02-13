// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ready19.RockTheBot.Translation;
using RockTheBot;

namespace Ready19.RockTheBot
{
    /// <summary>
    /// Represents a bot that processes incoming activities.
    /// For each user interaction, an instance of this class is created and the OnTurnAsync method is called.
    /// This is a Transient lifetime service. Transient lifetime services are created
    /// each time they're requested. For each Activity received, a new instance of this
    /// class is created. Objects that are expensive to construct, or have a lifetime
    /// beyond the single turn, should be carefully managed.
    /// For example, the <see cref="MemoryStorage"/> object and associated
    /// <see cref="IStatePropertyAccessor{T}"/> object are created with a singleton lifetime.
    /// </summary>
    /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1"/>
    public class RockTheBot : IBot
    {
        private const string EnglishEnglish = "en";
        private const string EnglishSwedish = "sv";
        private const string EnglishRomanian = "ro";
        private const string EnglishItalian = "it";
        private const string OtherToEnglish = "in";
        private const string KeepOther = "of";

        // Messages sent to the user.
        private const string WelcomeMessage = @"Hello! I am a MultiLingual Bot that tells you information about stocks and the weather. Start by selecting your language. Then select what information you need";

        private readonly RockTheBotAccessors _accessors;

        private readonly IRockTheBotServices _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="RockTheBot"/> class.
        /// </summary>
        /// <param name="accessors">Bot State Accessors.</param>
        /// <param name="statePropertyAccessor"> Bot state accessor object.</param>
        /// <param name="services"> Stocks and Weather services.</param>
        public RockTheBot(RockTheBotAccessors accessors, IRockTheBotServices services)
        {
            _accessors = accessors ?? throw new ArgumentNullException(nameof(accessors));
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        private DialogSet Dialogs { get; set; }

        /// <summary>
        /// Run every turn of the conversation. Handles orchestration of messages.
        /// </summary>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
        /// for processing this conversation turn. </param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        /// <seealso cref="BotStateSet"/>
        /// <seealso cref="ConversationState"/>
        /// <seealso cref="IMiddleware"/>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            // use state accessor to extract the didBotWelcomeUser flag
            var didBotWelcomeUser = await _accessors.WelcomeUserState.GetAsync(turnContext, () => new WelcomeUserState());

            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                if (didBotWelcomeUser.DidBotWelcomeUser == false)
                {
                    //TODO: complete the code!
                }

                string userLanguage = await _accessors.LanguagePreference.GetAsync(turnContext, () => TranslationSettings.DefaultLanguage) ?? TranslationSettings.DefaultLanguage;

                bool translate = userLanguage != TranslationSettings.DefaultLanguage;

                if (IsLanguageChangeRequested(turnContext.Activity.Text))
                {
                    var currentLang = turnContext.Activity.Text.ToLower();
                    var lang = currentLang == OtherToEnglish ? EnglishEnglish : currentLang;
                    userLanguage = lang;

                    // If the user requested a language change through the suggested actions,
                    // simply change the user's language preference in the user state.
                    // The translation middleware will catch this setting and translate both ways to the user's
                    // selected language.
                    // The reply below will actually be shown in the language that the user selected.
                    await _accessors.LanguagePreference.SetAsync(turnContext, lang);
                    var reply = turnContext.Activity.CreateReply($"Your current language code is: {lang}");

                    await turnContext.SendActivityAsync(reply, cancellationToken);

                    // Save the user profile updates into the user state.
                    await _accessors.UserState.SaveChangesAsync(turnContext, false, cancellationToken);
                }

                var messageText = turnContext.Activity.Text;
                if (messageText.ToLower().Contains("language"))
                {
                    // Show the user the possible options for language. If the user chooses a different language
                    // than the default, then the translation middleware will pick it up from the user state and
                    // translate messages both ways, i.e. user to bot and bot to user.
                    await SendLanguageCardAsync(turnContext, cancellationToken, userLanguage);
                }
                else if (messageText.ToLower().Contains("weather"))
                {
                    var parsedMessage = messageText.Split("weather", StringSplitOptions.RemoveEmptyEntries);
                    string city = "Seattle";
                    if (parsedMessage.Length > 1)
                        city = parsedMessage[parsedMessage.Length - 1] ?? city;

                    var responseMessage = $"Here's the weather info for {city}:\n";
                    var theResponse = _services.GetWeatherAsync(city);
                    await turnContext.SendActivityAsync(new Activity("typing"));
                    var jsonresponse = JObject.Parse(await theResponse);
                    responseMessage += JsonConvert.SerializeObject(jsonresponse, Formatting.Indented).TrimStart('{').TrimEnd('}');
                    responseMessage = responseMessage.Replace("\"", " ");
                    await turnContext.SendActivityAsync(responseMessage);
                    await SendSuggestedActionsAsync(turnContext, cancellationToken, userLanguage);
                }
                else if (messageText.ToLower().Contains("stocks"))
                {
                    var theResponse = _services.GetStocksAsync();
                    await turnContext.SendActivityAsync(new Activity("typing"));
                    var responseMessage = $"I see, always thinking about money!\nHere's the latest MSFT stock value: "
                        + await theResponse;
                    await turnContext.SendActivityAsync(responseMessage);
                    await SendSuggestedActionsAsync(turnContext, cancellationToken, userLanguage);
                }
                else
                {
                    await SendSuggestedActionsAsync(turnContext, cancellationToken, userLanguage);
                }
            }

            // Greet when users are added to the conversation.
            // Note that all channels do not send the conversation update activity.
            // If you find that this bot works in the emulator, but does not in
            // another channel the reason is most likely that the channel does not
            // send this activity.
            else if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
            {
                if (turnContext.Activity.MembersAdded != null)
                {
                    // Iterate over all new members added to the conversation
                    foreach (var member in turnContext.Activity.MembersAdded)
                    {
                        // Greet anyone that was not the target (recipient) of this message
                        // the 'bot' is the recipient for events from the channel,
                        // turnContext.Activity.MembersAdded == turnContext.Activity.Recipient.Id indicates the
                        // bot was added to the conversation.
                        if (member.Id != turnContext.Activity.Recipient.Id)
                        {
                            await turnContext.SendActivityAsync(WelcomeMessage, cancellationToken: cancellationToken);
                            await SendLanguageCardAsync(turnContext, cancellationToken, "en");
                        }
                    }
                }
            }

            // save any state changes made to your state objects.
            await _accessors.UserState.SaveChangesAsync(turnContext);
        }

        private static async Task SendLanguageCardAsync(ITurnContext turnContext, CancellationToken cancellationToken, string language)
        {
            var reply = turnContext.Activity.CreateReply("Choose your language:");
            reply.Attachments = new List<Attachment>();
            List<CardAction> cardButtons = new List<CardAction>();

            // TODO: complete the code!
            HeroCard heroCard = new HeroCard()
            {
                Buttons = cardButtons,
            };
            Attachment attachment = heroCard.ToAttachment();
            reply.Attachments.Add(attachment);

            await turnContext.SendActivityAsync(reply);
        }

        private static async Task SendSuggestedActionsAsync(ITurnContext turnContext, CancellationToken cancellationToken, string language)
        {
            var cardsuggestion = turnContext.Activity.CreateReply("What do you want to know:");
            cardsuggestion.Attachments = new List<Attachment>();
            List<CardAction> cardButtons = new List<CardAction>();
            cardButtons.Add(new MultilingualCardAction(language) { CardTitle = "Stocks", Type = ActionTypes.PostBack, Value = "Stocks" });
            cardButtons.Add(new MultilingualCardAction(language) { CardTitle = "Weather", Type = ActionTypes.PostBack, Value = "Weather" });
            HeroCard heroCard = new HeroCard()
            {
                Buttons = cardButtons,
            };
            Attachment attachment = heroCard.ToAttachment();
            cardsuggestion.Attachments.Add(attachment);

            await turnContext.SendActivityAsync(cardsuggestion, cancellationToken);
        }

        private static bool IsLanguageChangeRequested(string utterance)
        {
            if (string.IsNullOrEmpty(utterance))
            {
                return false;
            }

            utterance = utterance.ToLower().Trim();
            return utterance == EnglishSwedish || utterance == EnglishEnglish
                || utterance == KeepOther || utterance == OtherToEnglish || utterance == EnglishRomanian || utterance == EnglishItalian;
        }
    }
}
