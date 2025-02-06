using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TelegrabBotHandler
{
    #region MessageHandlers
    public interface ITelegramMessageHandler
    {
        Task HandleAsync(Update update);
        bool CanHandle(Update update);
    }
    public abstract class AbstractMessageHandler : ITelegramMessageHandler
    {
        protected ITelegramBotClient _telegramBot;

        public AbstractMessageHandler(ITelegramBotClient telegramBot)
        {
            _telegramBot = telegramBot;
        }
        public abstract Task HandleAsync(Update update);
        public abstract bool CanHandle(Update update);
    }
    public class TelegramUpdatesResponse
    {
        public bool ok { get; set; }
        public Update[] result { get; set; }
    }
    #endregion
    #region DataClasses

    #region Json
    public class Update
    {
        public long update_id { get; set; }
        public Message message { get; set; }
        public CallbackQuery callback_query { get; set; }
        public ChatMemberUpdated chat_member { get; set; } // Добавлено
    }

    public class ChatMemberUpdated
    {
        public Chat chat { get; set; }
        public ChatMember new_chat_member { get; set; }
    }

    public class ChatMember
    {
        public User user { get; set; }
        public string status { get; set; }
    }

    public class User
    {
        public long id { get; set; }
        public bool is_bot { get; set; }
        public string first_name { get; set; }
        public string username { get; set; }
    }

    public class CallbackQuery
    {
        public string id { get; set; }
        public From from { get; set; }
        public Message message { get; set; }
        public string data { get; set; }
    }

    public class Message
    {
        public long message_id { get; set; }
        public From from { get; set; }
        public Chat chat { get; set; }
        public int date { get; set; }
        public string text { get; set; }
    }

    public class From
    {
        public long id { get; set; }
        public bool is_bot { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string username { get; set; }
        public string language_code { get; set; }
    }

    public class Chat
    {
        public long id { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string username { get; set; }
        public string type { get; set; }
    }

    public class BotCommand
    {

        [JsonPropertyName("command")]
        public string Command { get; set; }


        [JsonPropertyName("description")]
        public string Description { get; set; }
    }
    #endregion


    public interface IReplyMarkup { }

    public class InlineKeyboardMarkup 
    {
        [JsonPropertyName("inline_keyboard")] 
        public InlineKeyboardButton[][] inline_keyboard { get; set; } 
    }

    public class InlineKeyboardButton
    {
        public string text { get; set; } = null!;
        [JsonPropertyName("callback_data")] 
        public string? callback_data { get; set; }
    }
    #endregion
    #region TelegramBotCode
    public interface ITelegramBotClient
    {
        Task SendMessageAsync(long chatId, string text, InlineKeyboardMarkup replyMarkup = null); 
        Task<TelegramUpdatesResponse> GetUpdatesAsync(long offset = 0, int timeout = 30);
        Task LogErrorAsync(string errorMessage);
        Task AnswerCallbackQueryAsync(string callbackQueryId, string text = null, bool showAlert = false); 
        Task EditMessageTextAsync(long chatId, long messageId, string text, InlineKeyboardMarkup replyMarkup = null); 
        Task EditMessageReplyMarkupAsync(long chatId, long messageId, InlineKeyboardMarkup replyMarkup = null); 
        Task SendPhotoAsync(long chatId, string photoUrl, string caption = null); 
        Task SetMyCommandsAsync(IEnumerable<BotCommand> commands); 
    }

    

    public class TelegramBotClient : ITelegramBotClient
    {
        private readonly string _apiToken;
        private readonly HttpClient _httpClient;
        private readonly List<ITelegramMessageHandler> _messageHandlers = new List<ITelegramMessageHandler>();
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull // Используем DefaultIgnoreCondition
        };

        public TelegramBotClient(string apiToken, IEnumerable<ITelegramMessageHandler> messageHandlers)
        {
            _apiToken = apiToken;
            _httpClient = new HttpClient();
            _messageHandlers.AddRange(messageHandlers);
        }
        public TelegramBotClient(string apiToken)
        {
            _apiToken = apiToken;
            _httpClient = new HttpClient();
        }

        public async Task SendMessageAsync(long chatId, string text, InlineKeyboardMarkup? replyMarkup = null)
        {
            var url = $"https://api.telegram.org/bot{_apiToken}/sendMessage";
            var payload = new
            {
                chat_id = chatId,
                text = text,
                reply_markup = replyMarkup
            };

            string json = "";
            try
            {
                json = JsonSerializer.Serialize(payload,_jsonOptions);
                Console.WriteLine($"JSON Payload: {json}");
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);

                string responseContent = await response.Content.ReadAsStringAsync(); 

                Console.WriteLine($"Response Content: {responseContent}"); 

                response.EnsureSuccessStatusCode(); 

            }
            catch (Exception ex)
            {
                Console.WriteLine($"JSON: {json}");
                Console.WriteLine($"Ошибка при отправке сообщения: {ex}");
            }
        }

        public async Task<TelegramUpdatesResponse> GetUpdatesAsync(long offset = 0, int timeout = 30)
        {
            var url = $"https://api.telegram.org/bot{_apiToken}/getUpdates?offset={offset}&timeout={timeout}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var updatesResponse = JsonSerializer.Deserialize<TelegramUpdatesResponse>(json);
            return updatesResponse;
        }

        public void AddMessageHandler(ITelegramMessageHandler handler)
        {
            _messageHandlers.Add(handler);
        }

        public async Task HandleUpdateAsync(Update update)
        {
            foreach (var handler in _messageHandlers)
            {
                if (handler.CanHandle(update))
                {
                    await handler.HandleAsync(update);
                    return;
                }
            }
        }

        public async Task LogErrorAsync(string errorMessage)
        {
            Console.WriteLine($"Ошибка: {errorMessage}");
        }

        public async Task AnswerCallbackQueryAsync(string callbackQueryId, string text = null, bool showAlert = false) 
        {
            var url = $"https://api.telegram.org/bot{_apiToken}/answerCallbackQuery";
            var payload = new { callback_query_id = callbackQueryId, text = text, show_alert = showAlert };
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        public async Task EditMessageTextAsync(long chatId, long messageId, string text, InlineKeyboardMarkup replyMarkup = null) 
        {
            var url = $"https://api.telegram.org/bot{_apiToken}/editMessageText";
            var payload = new { chat_id = chatId, message_id = messageId, text = text, reply_markup = replyMarkup };
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        public async Task EditMessageReplyMarkupAsync(long chatId, long messageId, InlineKeyboardMarkup replyMarkup = null) 
        {
            var url = $"https://api.telegram.org/bot{_apiToken}/editMessageReplyMarkup";
            var payload = new { chat_id = chatId, message_id = messageId, reply_markup = replyMarkup };
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        public async Task SendPhotoAsync(long chatId, string photoUrl, string caption = null) 
        {
            var url = $"https://api.telegram.org/bot{_apiToken}/sendPhoto";
            var payload = new { chat_id = chatId, photo = photoUrl, caption = caption };
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        public async Task SetMyCommandsAsync(IEnumerable<BotCommand> commands) // Добавлено
        {
            var url = $"https://api.telegram.org/bot{_apiToken}/setMyCommands";
            var payload = new { commands = commands };
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }
    }
    #endregion
}