//1. Plug-in Translation 
var translationMiddleware = new TranslationMiddleware(translator, userState.CreateProperty<string>("LanguagePreference"));
options.Middleware.Add(translationMiddleware);

//2. Create the language selection card
Actions = new List<CardAction>()
    {
        new MultilingualCardAction(language) { CardTitle = "Swedish", Type = ActionTypes.PostBack, Value = EnglishSwedish },
        new MultilingualCardAction(language) { CardTitle = "English", Type = ActionTypes.PostBack, Value = EnglishEnglish },
        new MultilingualCardAction(language) { CardTitle = "Romanian", Type = ActionTypes.PostBack, Value = EnglishRomanian },
        new MultilingualCardAction(language) { CardTitle = "Italian", Type = ActionTypes.PostBack, Value = EnglishItalian },
    },


//3. Keeping track of the user state
didBotWelcomeUser.DidBotWelcomeUser = true;

// Update user state flag to reflect bot handled first user interaction.
await _accessors.WelcomeUserState.SetAsync(turnContext, didBotWelcomeUser);
await _accessors.UserState.SaveChangesAsync(turnContext);
