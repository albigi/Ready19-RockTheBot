// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Ready19.RockTheBot.Translation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
        private const string SwedishEnglish = "in";
        private const string SwedishSwedish = "of";
        private const string RomanianRomanian = "of";
        private const string EnglishRomanian = "ro";
        private const string RomanianEnglish = "in";

        // Messages sent to the user.
        private const string WelcomeMessage = @"Hello! I am a MultiLingual Bot that tells you information about stocks and the weather. Start by selecting your language. Then select what information you need";

        private readonly RockTheBotAccessors _accessors;

        private readonly IRockTheBotServices _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="RockTheBot"/> class.
        /// </summary>
        /// <param name="accessors">Bot State Accessors.</param>
        /// <param name="statePropertyAccessor"> Bot state accessor object.</param>
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
                    didBotWelcomeUser.DidBotWelcomeUser = true;

                    // Update user state flag to reflect bot handled first user interaction.
                    await _accessors.WelcomeUserState.SetAsync(turnContext, didBotWelcomeUser);
                    await _accessors.UserState.SaveChangesAsync(turnContext);

                    // the channel should sends the user name in the 'From' object
                    var userName = turnContext.Activity.From.Name;
                }

                string userLanguage = await _accessors.LanguagePreference.GetAsync(turnContext, () => TranslationSettings.DefaultLanguage) ?? TranslationSettings.DefaultLanguage;

                bool translate = userLanguage != TranslationSettings.DefaultLanguage;

                if (IsLanguageChangeRequested(turnContext.Activity.Text))
                {
                    var curentLang = turnContext.Activity.Text.ToLower();
                    var lang = curentLang;
                    if (curentLang == SwedishEnglish || curentLang == RomanianEnglish)
                    {
                        lang = EnglishEnglish;
                    }

                    // var lang = curentLang == EnglishEnglish || curentLang == SwedishEnglish ? EnglishEnglish : EnglishSwedish;

                    // If the user requested a language change through the suggested actions with values "es" or "en",
                    // simply change the user's language preference in the user state.
                    // The translation middleware will catch this setting and translate both ways to the user's
                    // selected language.
                    // If Swedish was selected by the user, the reply below will actually be shown in Swedish to the user.
                    await _accessors.LanguagePreference.SetAsync(turnContext, lang);
                    var reply = turnContext.Activity.CreateReply($"Your current language code is: {lang}");

                    await turnContext.SendActivityAsync(reply, cancellationToken);

                    // Save the user profile updates into the user state.
                    await _accessors.UserState.SaveChangesAsync(turnContext, false, cancellationToken);
                }

                if (turnContext.Activity.Text.ToLower().Contains("language"))
                {
                    // Show the user the possible options for language. If the user chooses a different language
                    // than the default, then the translation middleware will pick it up from the user state and
                    // translate messages both ways, i.e. user to bot and bot to user.
                    await SendLanguageCardAsync(turnContext, cancellationToken);
                }
                else if (turnContext.Activity.Text.ToLower().Contains("weather"))
                {
                    // responseMessage = $"Here's the latest weather for Seattle, WA: <strong>{await GetLatestStockValueAsync()}</strong>\n";
                    var responseMessage = $"Here's the latest weather for Seattle, WA: "
                        + await _services.GetWeatherAsync();
                    await turnContext.SendActivityAsync(responseMessage);
                    await SendSuggestedActionsAsync(turnContext, cancellationToken);
                }
                else if (turnContext.Activity.Text.ToLower().Contains("stocks"))
                {
                    // responseMessage = $"I see, always thinking about money you are!\nHere's the latest MSFT stock value: <strong>{await GetLatestStockValueAsync()}</strong>\n";
                    var responseMessage = $"I see, always thinking about money you are!\nHere's the latest MSFT stock value: "
                        + await _services.GetStocksAsync();
                    await turnContext.SendActivityAsync(responseMessage);
                    await SendSuggestedActionsAsync(turnContext, cancellationToken);
                }
                else
                {
                    await SendSuggestedActionsAsync(turnContext, cancellationToken);
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
                            await SendLanguageCardAsync(turnContext, cancellationToken);
                        }
                    }
                }
            }

            // save any state changes made to your state objects.
            await _accessors.UserState.SaveChangesAsync(turnContext);
        }

        private static async Task SendLanguageCardAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var reply = turnContext.Activity.CreateReply("Choose your language:");
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                        {
                            new CardAction() { Title = "Swedish", Type = ActionTypes.PostBack, Value = EnglishSwedish },
                            new CardAction() { Title = "English", Type = ActionTypes.PostBack, Value = EnglishEnglish },
                            new CardAction() { Title = "Romanian", Type = ActionTypes.PostBack, Value = EnglishRomanian },
                        },
            };

            await turnContext.SendActivityAsync(reply);
        }

        private static async Task SendSuggestedActionsAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var cardsuggestion = turnContext.Activity.CreateReply("What do you want to know:");
            cardsuggestion.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                        {
                            new CardAction() { Title = "Stocks", Type = ActionTypes.PostBack, Value = "Stocks" },
                            new CardAction() { Title = "Weather", Type = ActionTypes.PostBack, Value = "Weather" },
                        },
            };

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
                || utterance == SwedishSwedish || utterance == SwedishEnglish || utterance == EnglishRomanian;
        }

    }
}
