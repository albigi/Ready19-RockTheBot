using System.Threading.Tasks;
using Microsoft.Bot.Schema;
using Ready19.RockTheBot.Translation;

namespace RockTheBot
{
    public class MultilingualCardAction : CardAction
    {
        private readonly MicrosoftTranslator _translator;

        private string _language;

        public MultilingualCardAction(string language)
        {
            _language = language;

            // Translation key from settings
            var translatorKey = Ready19.RockTheBot.Startup.TranslationKey;
            _translator = new MicrosoftTranslator(translatorKey);
        }

        public string CardTitle
        {
            get
            {
                return this.Title;
            }

            set
            {
                this.Title = GetTranslatedTextAsync(value).Result;
            }
        }

        private async Task<string> GetTranslatedTextAsync(string title)
        {
            return await _translator.TranslateAsync(title, _language);
        }
    }
}
