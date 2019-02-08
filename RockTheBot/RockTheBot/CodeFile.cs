//1. Plug-in Translation 

//Add the translation Middlewarevar translationMiddleware = new TranslationMiddleware(translator, userState.CreateProperty<string>("LanguagePreference"));options.Middleware.Add(translationMiddleware);

//2. Create the language selection card
private static async Task SendLanguageCardAsync(ITurnContext turnContext, CancellationToken cancellationToken, string language)
{
    var reply = turnContext.Activity.CreateReply("Choose your language:");
    reply.SuggestedActions = new SuggestedActions()
    {
        Actions = new List<CardAction>()
                        {
                            new MultilingualCardAction(language) { CardTitle = "Swedish", Type = ActionTypes.PostBack, Value = EnglishSwedish },
                            new MultilingualCardAction(language) { CardTitle = "English", Type = ActionTypes.PostBack, Value = EnglishEnglish },
                            new MultilingualCardAction(language) { CardTitle = "Romanian", Type = ActionTypes.PostBack, Value = EnglishRomanian },
                            new MultilingualCardAction(language) { CardTitle = "Italian", Type = ActionTypes.PostBack, Value = EnglishItalian },
                        },
    };

    await turnContext.SendActivityAsync(reply);
}
//3. Keeping track of the user state

if (didBotWelcomeUser.DidBotWelcomeUser == false)
                {
                    didBotWelcomeUser.DidBotWelcomeUser = true;

                    // Update user state flag to reflect bot handled first user interaction.
                    await _accessors.WelcomeUserState.SetAsync(turnContext, didBotWelcomeUser);
                    await _accessors.UserState.SaveChangesAsync(turnContext);
                }
