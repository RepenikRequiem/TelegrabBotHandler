using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
    #region JSonDataClasses
    public class Update
    {
        public int update_id { get; set; }
        public Message message { get; set; }
    }

    public class Message
    {
        public int message_id { get; set; }
        public From from { get; set; }
        public Chat chat { get; set; }
        public int date { get; set; }
        public string text { get; set; }
    }

    public class From
    {
        public int id { get; set; }
        public bool is_bot { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string username { get; set; }
        public string language_code { get; set; }
    }

    public class Chat
    {
        public int id { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string username { get; set; }
        public string type { get; set; }
    }
    #endregion
    #region TelegramBotCode
    public interface ITelegramBotClient
    {
        Task SendMessageAsync(long chatId, string text);
        Task<TelegramUpdatesResponse> GetUpdatesAsync(int offset = 0, int timeout = 30);
        Task LogErrorAsync(string errorMessage);
    }

    public class TelegramBotClient : ITelegramBotClient 
    {
        private readonly string _apiToken;
        private readonly HttpClient _httpClient;
        private readonly List<ITelegramMessageHandler> _messageHandlers = new List<ITelegramMessageHandler>();

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

        public async Task SendMessageAsync(long chatId, string text)
        {
            var url = $"https://api.telegram.org/bot{_apiToken}/sendMessage";
            var payload = new { chat_id = chatId, text = text };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        public async Task<TelegramUpdatesResponse> GetUpdatesAsync(int offset = 0, int timeout = 30) 
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
    }
    #endregion

}
