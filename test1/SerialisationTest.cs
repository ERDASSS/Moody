using System.Data.SQLite;
using ApiMethods;
using Database;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using TGBot;
using VkNet;
using VkNet.AudioBypassService.Exceptions;
using VkNet.AudioBypassService.Extensions;
using VkNet.Enums.Filters;
using VkNet.Model;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json;

// Эпик Фейл T_T

namespace serialisationTest;

[TestFixture]
public class SerialisationTests
{
    [Test]
    [Obsolete("Obsolete")]
    public async Task SerialisationTest1()
    {
        // var authorization = new Authorization();
        // authorization.SetCorrectData(true);
        // authorization.AddLogin("[логин]");
        // authorization.AddPassword("[пароль]");

        Console.WriteLine("введите логин:");
        var login = Console.ReadLine();
        Console.WriteLine("введите пароль:");
        var password = Console.ReadLine();

        VkApiWrapper vkApi;
        try
        {
            vkApi = new VkApiWrapper();
            vkApi.AuthorizeWithout2FA(login, password);
        }
        catch (VkAuthException exception)
        {
            Console.WriteLine(exception);
            return;
        }
        catch (InvalidOperationException exception)
        {
            Console.WriteLine(exception);
            return;
        }
        //
        // Console.WriteLine("введите код:");
        // var code = Console.ReadLine();
        //
        // try
        // {
        //     vkApi = new VkApiWrapper();
        //     vkApi.AuthorizeWith2FA(login, password, code);
        // }
        // catch (VkAuthException exception)
        // {
        //     Console.WriteLine(exception);
        //     return;
        // }
        // catch (InvalidOperationException exception)
        // {
        //     Console.WriteLine(exception);
        //     return;
        // }


        // while (true)
        // {
        //     try
        //     {
        //         var path = "GusevApi.bin";
        //         BinarySerializer.SerializeObject(vkApi, path);
        //         var deserializedAccount = BinarySerializer.DeserializeObject<VkApiWrapper>(path);
        //
        //         var tracks = deserializedAccount.GetFavouriteTracks();
        //         foreach (var track in tracks)
        //             Console.WriteLine(track.Title);
        //     }
        //     catch (Exception e)
        //     {
        //         Console.WriteLine(e);
        //         throw;
        //     }
        // }
    }
}

public class Serializer
{
    public static string SerializeToJson<T>(T obj)
    {
        try
        {
            string json =
                JsonConvert.SerializeObject(obj, Formatting.Indented); // Formatting.Indented для красивого вывода
            return json;
        }
        catch (JsonException ex)
        {
            // Обработка ошибок сериализации
            Console.WriteLine($"Ошибка сериализации: {ex.Message}");
            return null;
        }
    }

    public static T DeserializeFromJson<T>(string json)
    {
        try
        {
            T obj = JsonConvert.DeserializeObject<T>(json);
            return obj;
        }
        catch (JsonException ex)
        {
            // Обработка ошибок десериализации
            Console.WriteLine($"Ошибка десериализации: {ex.Message}");
            return default(T); // Возвращаем значение по умолчанию для типа T
        }
    }

    // Дополнительный метод для сериализации в файл
    public static bool SerializeToFile<T>(T obj, string filePath)
    {
        try
        {
            string json = SerializeToJson(obj);
            if (json != null)
            {
                File.WriteAllText(filePath, json);
                return true;
            }

            return false;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Ошибка записи в файл: {ex.Message}");
            return false;
        }
    }

    // Дополнительный метод для десериализации из файла
    public static T DeserializeFromFile<T>(string filePath)
    {
        try
        {
            string json = File.ReadAllText(filePath);
            return DeserializeFromJson<T>(json);
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine($"Файл не найден: {ex.Message}");
            return default(T);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Ошибка чтения файла: {ex.Message}");
            return default(T);
        }
    }
}
//
// // Пример использования:
// public class Person
// {
//     public string Name { get; set; }
//     public int Age { get; set; }
// }
//
// public class Example
// {
//     public static void Main(string[] args)
//     {
//         Person person = new Person { Name = "John Doe", Age = 30 };
//
//         // Сериализация в JSON строку
//         string jsonString = Serializer.SerializeToJson(person);
//         Console.WriteLine("JSON строка:\n" + jsonString);
//
//         // Десериализация из JSON строки
//         Person deserializedPerson = Serializer.DeserializeFromJson<Person>(jsonString);
//         Console.WriteLine(
//             $"\nДесериализованный объект: Name = {deserializedPerson.Name}, Age = {deserializedPerson.Age}");
//
//
//         // Сериализация в файл
//         bool success = Serializer.SerializeToFile(person, "person.json");
//         Console.WriteLine($"\nСериализация в файл: {(success ? "Успешно" : "Ошибка")}");
//
//         // Десериализация из файла
//         Person personFromFile = Serializer.DeserializeFromFile<Person>("person.json");
//         Console.WriteLine(
//             $"\nДесериализованный объект из файла: Name = {personFromFile.Name}, Age = {personFromFile.Age}");
//     }
// }


// public static class BinarySerializer
// {
//     [Obsolete("Obsolete")]
//     public static void SerializeObject(VkApiWrapper account, string filePath)
//     {
//         using Stream stream = File.Open(filePath, FileMode.Create);
//         var bf = new BinaryFormatter();
//         bf.Serialize(stream, account);
//     }
//
//     [Obsolete("Obsolete")]
//     public static T DeserializeObject<T>(string filePath)
//     {
//         using Stream stream = File.Open(filePath, FileMode.Open);
//         var bf = new BinaryFormatter();
//         return (T)bf.Deserialize(stream);
//     }
// }