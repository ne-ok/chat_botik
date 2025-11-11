using VkNet;
using VkNet.Model;
using VkNet.Model.GroupUpdate;
using VkNet.Model.RequestParams;

namespace VkLongPollBot
{
    class Program
    {
        private static VkApi _vkApi = new VkApi();
        private static LongPollServerResponse _server;
        private static string _ts;
        private static ulong _groupId;

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== VK Long Poll Bot ===");

            // ⚠️ ЗАМЕНИТЕ ЭТИ ДАННЫЕ НА СВОИ ⚠️
            var accessToken = "vk1.a.04sSk9DZjbdgyzLMx4U2o-5m0wiVBRTk17OczNxiATr8-lCN1J7-7teRKJ8hLwRg5nW5VOUDCehKiA53x74kfWmZh0hqcB6wLPhbmPEBfHPMEuYWbBryc4KGWEjqo4ijGchRIIRdA1yGSywtYd5OUqEI9E8weu1xWEpJ294NYNn671vQ2XqwjPxVIBLK_4jgTXRZq2gp8gvk3UVL80Qu5w";
            _groupId = 233846417; // Ваш ID группы (числовой)

            try
            {
                // Авторизация
                await _vkApi.AuthorizeAsync(new ApiAuthParams
                {
                    AccessToken = accessToken
                });

                Console.WriteLine("✅ Успешная авторизация");

                // Получаем сервер для Long Poll
                await UpdateLongPollServer();

                Console.WriteLine("🔄 Бот запущен и слушает события...");
                Console.WriteLine("Нажмите Ctrl+C для остановки");

                // Запускаем прослушивание событий
                await StartLongPolling();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка: {ex.Message}");
                Console.WriteLine("Нажмите любую клавишу для выхода...");
                Console.ReadKey();
            }
        }

        private static async Task UpdateLongPollServer()
        {
            _server = await _vkApi.Groups.GetLongPollServerAsync(_groupId);
            _ts = _server.Ts;
        }

        private static async Task StartLongPolling()
        {
            while (true)
            {
                try
                {
                    // Получаем новые события
                    var longPollHistory = await _vkApi.Groups.GetBotsLongPollHistoryAsync(
                        new BotsLongPollHistoryParams
                        {
                            Server = _server.Server,
                            Key = _server.Key,
                            Ts = _ts,
                            Wait = 25 // Ожидание 25 секунд
                        });

                    // Обновляем ts для следующего запроса
                    _ts = longPollHistory.Ts;

                    // Обрабатываем события
                    if (longPollHistory?.Updates != null && longPollHistory.Updates.Any())
                    {
                        foreach (var update in longPollHistory.Updates)
                        {
                            await HandleUpdateAsync(update);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Ошибка Long Poll: {ex.Message}");
                    Console.WriteLine("Попытка переподключения через 5 секунд...");

                    await Task.Delay(5000);
                    await UpdateLongPollServer(); // Переподключаемся
                }
            }
        }

        private static async Task HandleUpdateAsync(GroupUpdate update)
        {
            try
            {
                switch (update.Type)
                {
                    case GroupUpdateType.MessageNew:
                        await HandleNewMessageAsync(update.MessageNew.Message);
                        break;

                    case GroupUpdateType.MessageReply:
                        Console.WriteLine($"💬 Ответ на сообщение: {update.MessageReply.Text}");
                        break;

                    case GroupUpdateType.GroupJoin:
                        Console.WriteLine($"✅ Пользователь вступил в группу: {update.GroupJoin.UserId}");
                        await HandleNewMemberAsync(update.GroupJoin.UserId);
                        break;

                    case GroupUpdateType.GroupLeave:
                        Console.WriteLine($"❌ Пользователь покинул группу: {update.GroupLeave.UserId}");
                        break;

                    default:
                        Console.WriteLine($"📦 Другое событие: {update.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки события: {ex.Message}");
            }
        }

        private static async Task HandleNewMessageAsync(Message message)
        {
            var userId = message.FromId.Value;
            var text = message.Text?.ToLower() ?? "";

            Console.WriteLine($"✉️ Новое сообщение от {userId}: {text}");

            var response = ProcessMessage(text, userId);

            if (!string.IsNullOrEmpty(response.Text))
            {
                await _vkApi.Messages.SendAsync(new MessagesSendParams
                {
                    UserId = userId,
                    Message = response.Text,
                    Keyboard = response.Keyboard,
                    RandomId = DateTime.Now.Millisecond
                });

                Console.WriteLine($"✅ Ответ отправлен пользователю {userId}");
            }
        }

        private static async Task HandleNewMemberAsync(long userId)
        {
            // Приветствие нового участника
            await _vkApi.Messages.SendAsync(new MessagesSendParams
            {
                UserId = userId,
                Message = $"🎉 Добро пожаловать в наше сообщество!\n\n" +
                         "Я чат-бот, вот что я умею:\n" +
                         "• Отвечать на команды\n" +
                         "• Показывать время\n" +
                         "• Отправлять клавиатуру\n\n" +
                         "Напиши 'команды' для списка доступных команд.",
                Keyboard = CreateMainKeyboard(),
                RandomId = DateTime.Now.Millisecond
            });
        }

        private static BotResponse ProcessMessage(string text, long userId)
        {
            return text.ToLower() switch
            {
                "привет" or "начать" or "hello" => new BotResponse
                {
                    Text = $"👋 Привет, пользователь #{userId}!\n\n" +
                          "🤖 Я бот, работающий на Long Poll API\n" +
                          "🕐 Текущее время: " + DateTime.Now.ToString("HH:mm") + "\n" +
                          "📅 Дата: " + DateTime.Now.ToString("dd.MM.yyyy") + "\n\n" +
                          "Выбери команду из меню ниже:",
                    Keyboard = CreateMainKeyboard()
                },

                "команды" or "помощь" or "help" => new BotResponse
                {
                    Text = "📋 Доступные команды:\n\n" +
                          "• 🕐 Время - показать текущее время\n" +
                          "• ℹ️ Инфо - информация о боте\n" +
                          "• 📋 Команды - этот список\n" +
                          "• 🎮 Кнопки - показать клавиатуру",
                    Keyboard = CreateMainKeyboard()
                },

                "время" or "time" => new BotResponse
                {
                    Text = $"🕐 Точное время: {DateTime.Now:HH:mm:ss}\n" +
                          $"📅 Дата: {DateTime.Now:dd.MM.yyyy}\n" +
                          $"🌍 Часовой пояс: UTC+3 (Мск)"
                },

                "инфо" or "info" or "о боте" => new BotResponse
                {
                    Text = "🤖 Информация о боте:\n\n" +
                          "• Платформа: .NET 8.0\n" +
                          "• Библиотека: VkNet\n" +
                          "• Тип: Long Poll API\n" +
                          "• Запущен: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + "\n" +
                          "• ID пользователя: " + userId
                },

                "кнопки" or "клавиатура" or "menu" => new BotResponse
                {
                    Text = "🎮 Вот интерактивная клавиатура:",
                    Keyboard = CreateMainKeyboard()
                },

                "" => new BotResponse
                {
                    Text = "⚠️ Вы отправили пустое сообщение"
                },

                _ => new BotResponse
                {
                    Text = "❌ Я не понимаю эту команду\n" +
                          "Напиши 'команды' для списка доступных команд\n" +
                          "Или нажми одну из кнопок ниже:",
                    Keyboard = CreateMainKeyboard()
                }
            };
        }

        private static MessageKeyboard CreateMainKeyboard()
        {
            return new MessageKeyboard
            {
                Buttons = new List<List<MessageKeyboardButton>>
                {
                    new()
                    {
                        new MessageKeyboardButton
                        {
                            Action = new MessageKeyboardButtonAction
                            {
                                Type = KeyboardButtonActionType.Text,
                                Label = "🕐 Время"
                            },
                            Color = KeyboardButtonColor.Primary
                        },
                        new MessageKeyboardButton
                        {
                            Action = new MessageKeyboardButtonAction
                            {
                                Type = KeyboardButtonActionType.Text,
                                Label = "ℹ️ Инфо"
                            },
                            Color = KeyboardButtonColor.Default
                        }
                    },
                    new()
                    {
                        new MessageKeyboardButton
                        {
                            Action = new MessageKeyboardButtonAction
                            {
                                Type = KeyboardButtonActionType.Text,
                                Label = "📋 Команды"
                            },
                            Color = KeyboardButtonColor.Positive
                        },
                        new MessageKeyboardButton
                        {
                            Action = new MessageKeyboardButtonAction
                            {
                                Type = KeyboardButtonActionType.Text,
                                Label = "🎮 Кнопки"
                            },
                            Color = KeyboardButtonColor.Default
                        }
                    }
                },
                OneTime = false,
                Inline = false
            };
        }
    }

    public class BotResponse
    {
        public string Text { get; set; } = string.Empty;
        public MessageKeyboard? Keyboard { get; set; }
    }
}